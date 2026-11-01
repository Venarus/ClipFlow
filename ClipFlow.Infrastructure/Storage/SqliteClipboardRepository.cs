using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;
using Microsoft.Data.Sqlite;

namespace ClipFlow.Infrastructure.Storage;

public class SqliteClipboardRepository : IClipboardStorageRepository, IDisposable
{
    private readonly string _dbPath;
    private readonly string _imagesDirectory;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isInitialized;

    public SqliteClipboardRepository(string? customDbPath = null)
    {
        if (!string.IsNullOrEmpty(customDbPath))
        {
            _dbPath = customDbPath;
            var dir = Path.GetDirectoryName(_dbPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            _imagesDirectory = Path.Combine(dir, "Images");
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "ClipFlow");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "clipflow.db");
            _imagesDirectory = Path.Combine(folder, "Images");
        }

        Directory.CreateDirectory(_imagesDirectory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 15
        }.ToString();
    }

    public string ImagesDirectory => _imagesDirectory;
    public string DatabasePath => _dbPath;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _lock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // Enable WAL mode and memory cache for maximum concurrency and ultra-low latency
            using (var walCmd = conn.CreateCommand())
            {
                walCmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA foreign_keys = ON;
                    PRAGMA temp_store = MEMORY;
                    PRAGMA cache_size = -4000;
                    PRAGMA mmap_size = 268435456;
                ";
                await walCmd.ExecuteNonQueryAsync();
            }

            // Create table
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS ClipboardItems (
                    Id TEXT PRIMARY KEY,
                    ContentType INTEGER NOT NULL,
                    TextContent TEXT,
                    HtmlContent TEXT,
                    RtfContent TEXT,
                    ImageStoragePath TEXT,
                    ImageThumbnailPath TEXT,
                    ImageWidth INTEGER,
                    ImageHeight INTEGER,
                    FilePathsJson TEXT,
                    ContentHash TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    LastUsedAt TEXT NOT NULL,
                    UseCount INTEGER NOT NULL DEFAULT 1,
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    IsPinned INTEGER NOT NULL DEFAULT 0,
                    Category TEXT DEFAULT 'General',
                    TagsJson TEXT,
                    SourceApplication TEXT,
                    SourceWindowTitle TEXT,
                    SizeBytes INTEGER NOT NULL DEFAULT 0,
                    SmartDetectionJson TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_clipboard_hash ON ClipboardItems(ContentHash);
                CREATE INDEX IF NOT EXISTS idx_clipboard_created ON ClipboardItems(CreatedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_clipboard_last_used ON ClipboardItems(LastUsedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_clipboard_type ON ClipboardItems(ContentType);
                CREATE INDEX IF NOT EXISTS idx_clipboard_fav ON ClipboardItems(IsFavorite);
                CREATE INDEX IF NOT EXISTS idx_clipboard_pinned ON ClipboardItems(IsPinned);
                CREATE INDEX IF NOT EXISTS idx_clipboard_category ON ClipboardItems(Category);
                CREATE INDEX IF NOT EXISTS idx_clipboard_type_lastused ON ClipboardItems(ContentType, LastUsedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_clipboard_fav_lastused ON ClipboardItems(IsFavorite, LastUsedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_clipboard_pinned_lastused ON ClipboardItems(IsPinned, LastUsedAt DESC);
            ";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = createTableSql;
                await cmd.ExecuteNonQueryAsync();
            }

            _isInitialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ClipboardItem>> GetItemsAsync(FilterCriteria filter)
    {
        await EnsureInitializedAsync();

        var items = new List<ClipboardItem>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var query = "SELECT * FROM ClipboardItems WHERE 1=1 ";
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            query += "AND (TextContent LIKE @search OR SourceApplication LIKE @search OR SourceWindowTitle LIKE @search OR Category LIKE @search OR TagsJson LIKE @search) ";
            parameters.Add(new SqliteParameter("@search", $"%{filter.SearchQuery.Trim()}%"));
        }

        if (filter.ContentTypes != null && filter.ContentTypes.Count > 0)
        {
            var typeParams = new List<string>();
            for (int i = 0; i < filter.ContentTypes.Count; i++)
            {
                var paramName = $"@ctype_{i}";
                typeParams.Add(paramName);
                parameters.Add(new SqliteParameter(paramName, (int)filter.ContentTypes[i]));
            }
            query += $"AND ContentType IN ({string.Join(", ", typeParams)}) ";
        }
        else if (filter.ContentType.HasValue)
        {
            query += "AND ContentType = @contentType ";
            parameters.Add(new SqliteParameter("@contentType", (int)filter.ContentType.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category) && !string.Equals(filter.Category, "All", StringComparison.OrdinalIgnoreCase))
        {
            query += "AND Category = @category ";
            parameters.Add(new SqliteParameter("@category", filter.Category));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            query += "AND TagsJson LIKE @tag ";
            parameters.Add(new SqliteParameter("@tag", $"%\"{filter.Tag}\"%"));
        }

        if (filter.OnlyFavorites)
        {
            query += "AND IsFavorite = 1 ";
        }

        if (filter.OnlyPinned)
        {
            query += "AND IsPinned = 1 ";
        }

        var now = DateTime.UtcNow;
        if (filter.DateFilter == DateFilterOption.Today)
        {
            var startOfToday = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).ToString("o");
            query += "AND CreatedAt >= @startToday ";
            parameters.Add(new SqliteParameter("@startToday", startOfToday));
        }
        else if (filter.DateFilter == DateFilterOption.Yesterday)
        {
            var yesterdayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-1).ToString("o");
            var yesterdayEnd = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).ToString("o");
            query += "AND CreatedAt >= @yesterdayStart AND CreatedAt < @yesterdayEnd ";
            parameters.Add(new SqliteParameter("@yesterdayStart", yesterdayStart));
            parameters.Add(new SqliteParameter("@yesterdayEnd", yesterdayEnd));
        }
        else if (filter.DateFilter == DateFilterOption.ThisWeek)
        {
            var weekAgo = now.AddDays(-7).ToString("o");
            query += "AND CreatedAt >= @weekAgo ";
            parameters.Add(new SqliteParameter("@weekAgo", weekAgo));
        }
        else if (filter.DateFilter == DateFilterOption.ThisMonth)
        {
            var monthAgo = now.AddDays(-30).ToString("o");
            query += "AND CreatedAt >= @monthAgo ";
            parameters.Add(new SqliteParameter("@monthAgo", monthAgo));
        }

        // Sorting: Pinned items can optionally stay on top
        var sortClause = filter.SortBy switch
        {
            SortOption.Oldest => "IsPinned DESC, LastUsedAt ASC",
            SortOption.MostUsed => "IsPinned DESC, UseCount DESC, LastUsedAt DESC",
            SortOption.ContentType => "IsPinned DESC, ContentType ASC, LastUsedAt DESC",
            _ => "IsPinned DESC, LastUsedAt DESC"
        };

        query += $"ORDER BY {sortClause} LIMIT @limit OFFSET @offset;";
        parameters.Add(new SqliteParameter("@limit", filter.Limit));
        parameters.Add(new SqliteParameter("@offset", filter.Offset));

        using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddRange(parameters.ToArray());

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(MapFromReader(reader));
        }

        return items;
    }

    public async Task<ClipboardItem?> GetItemByIdAsync(string id)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ClipboardItems WHERE Id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task<ClipboardItem?> FindDuplicateAsync(string contentHash)
    {
        if (string.IsNullOrEmpty(contentHash)) return null;
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ClipboardItems WHERE ContentHash = @hash LIMIT 1;";
        cmd.Parameters.AddWithValue("@hash", contentHash);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task<bool> AddOrUpdateAsync(ClipboardItem item)
    {
        await EnsureInitializedAsync();

        // Check if duplicate exists
        var existing = await FindDuplicateAsync(item.ContentHash);
        if (existing != null)
        {
            // Bump existing item to top without creating a duplicate row
            existing.LastUsedAt = DateTime.UtcNow;
            existing.UseCount++;
            if (!string.IsNullOrEmpty(item.SourceApplication)) existing.SourceApplication = item.SourceApplication;
            if (!string.IsNullOrEmpty(item.SourceWindowTitle)) existing.SourceWindowTitle = item.SourceWindowTitle;
            return await UpdateItemAsync(existing);
        }

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ClipboardItems (
                    Id, ContentType, TextContent, HtmlContent, RtfContent,
                    ImageStoragePath, ImageThumbnailPath, ImageWidth, ImageHeight,
                    FilePathsJson, ContentHash, CreatedAt, LastUsedAt, UseCount,
                    IsFavorite, IsPinned, Category, TagsJson, SourceApplication,
                    SourceWindowTitle, SizeBytes, SmartDetectionJson
                ) VALUES (
                    @id, @contentType, @textContent, @htmlContent, @rtfContent,
                    @imageStoragePath, @imageThumbnailPath, @imageWidth, @imageHeight,
                    @filePathsJson, @contentHash, @createdAt, @lastUsedAt, @useCount,
                    @isFavorite, @isPinned, @category, @tagsJson, @sourceApplication,
                    @sourceWindowTitle, @sizeBytes, @smartDetectionJson
                );";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            BindItemParameters(cmd, item);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdateItemAsync(ClipboardItem item)
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE ClipboardItems SET
                    ContentType = @contentType,
                    TextContent = @textContent,
                    HtmlContent = @htmlContent,
                    RtfContent = @rtfContent,
                    ImageStoragePath = @imageStoragePath,
                    ImageThumbnailPath = @imageThumbnailPath,
                    ImageWidth = @imageWidth,
                    ImageHeight = @imageHeight,
                    FilePathsJson = @filePathsJson,
                    ContentHash = @contentHash,
                    CreatedAt = @createdAt,
                    LastUsedAt = @lastUsedAt,
                    UseCount = @useCount,
                    IsFavorite = @isFavorite,
                    IsPinned = @isPinned,
                    Category = @category,
                    TagsJson = @tagsJson,
                    SourceApplication = @sourceApplication,
                    SourceWindowTitle = @sourceWindowTitle,
                    SizeBytes = @sizeBytes,
                    SmartDetectionJson = @smartDetectionJson
                WHERE Id = @id;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            BindItemParameters(cmd, item);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteItemAsync(string id)
    {
        await EnsureInitializedAsync();

        var item = await GetItemByIdAsync(id);
        if (item == null) return false;

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ClipboardItems WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            var result = await cmd.ExecuteNonQueryAsync() > 0;
            if (result)
            {
                DeleteAssociatedImageFiles(item);
            }
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> DeleteItemsAsync(IEnumerable<string> ids)
    {
        await EnsureInitializedAsync();

        int deletedCount = 0;
        foreach (var id in ids)
        {
            if (await DeleteItemAsync(id))
            {
                deletedCount++;
            }
        }
        return deletedCount;
    }

    public async Task<int> ClearHistoryAsync(bool preservePinned = true)
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // Find images to delete
            var imagesToDelete = new List<string>();
            var selectSql = preservePinned 
                ? "SELECT ImageStoragePath, ImageThumbnailPath FROM ClipboardItems WHERE IsPinned = 0;"
                : "SELECT ImageStoragePath, ImageThumbnailPath FROM ClipboardItems;";

            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = selectSql;
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) imagesToDelete.Add(reader.GetString(0));
                    if (!reader.IsDBNull(1)) imagesToDelete.Add(reader.GetString(1));
                }
            }

            var deleteSql = preservePinned 
                ? "DELETE FROM ClipboardItems WHERE IsPinned = 0;"
                : "DELETE FROM ClipboardItems;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = deleteSql;
            var count = await cmd.ExecuteNonQueryAsync();

            // Delete disk files
            foreach (var imgPath in imagesToDelete)
            {
                try
                {
                    if (File.Exists(imgPath)) File.Delete(imgPath);
                }
                catch { }
            }

            return count;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ToggleFavoriteAsync(string id)
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ClipboardItems SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> TogglePinAsync(string id)
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ClipboardItems SET IsPinned = CASE WHEN IsPinned = 1 THEN 0 ELSE 1 END WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IncrementUseCountAsync(string id)
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ClipboardItems SET UseCount = UseCount + 1, LastUsedAt = @now WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        await EnsureInitializedAsync();

        var categories = new List<string> { "General" };
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category FROM ClipboardItems WHERE Category IS NOT NULL AND Category != '' ORDER BY Category ASC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var cat = reader.GetString(0);
            if (!categories.Contains(cat))
            {
                categories.Add(cat);
            }
        }

        return categories;
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        await EnsureInitializedAsync();

        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TagsJson FROM ClipboardItems WHERE TagsJson IS NOT NULL AND TagsJson != '';";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(json);
                if (tags != null)
                {
                    foreach (var t in tags) tagSet.Add(t);
                }
            }
            catch { }
        }

        return new List<string>(tagSet);
    }

    public async Task<StorageStats> GetStorageStatsAsync()
    {
        await EnsureInitializedAsync();

        var stats = new StorageStats();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    COUNT(*), 
                    SUM(CASE WHEN IsFavorite = 1 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN IsPinned = 1 THEN 1 ELSE 0 END),
                    MIN(CreatedAt),
                    MAX(CreatedAt)
                FROM ClipboardItems;";

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalItemCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                stats.FavoriteCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                stats.PinnedCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

                if (!reader.IsDBNull(3) && DateTime.TryParse(reader.GetString(3), out var oldest))
                    stats.OldestItemDate = oldest;
                if (!reader.IsDBNull(4) && DateTime.TryParse(reader.GetString(4), out var newest))
                    stats.NewestItemDate = newest;
            }
        }

        try
        {
            if (File.Exists(_dbPath))
            {
                stats.DatabaseSizeBytes = new FileInfo(_dbPath).Length;
            }
        }
        catch { }

        try
        {
            if (Directory.Exists(_imagesDirectory))
            {
                long imgSize = 0;
                var dirInfo = new DirectoryInfo(_imagesDirectory);
                foreach (var f in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    imgSize += f.Length;
                }
                stats.ImageStorageSizeBytes = imgSize;
            }
        }
        catch { }

        return stats;
    }

    public async Task<int> RunAutoCleanupAsync(int retentionDays, int maxItems)
    {
        await EnsureInitializedAsync();

        int totalDeleted = 0;

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Delete items older than retentionDays (preserving pinned)
            if (retentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("o");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM ClipboardItems WHERE IsPinned = 0 AND CreatedAt < @cutoff;";
                cmd.Parameters.AddWithValue("@cutoff", cutoff);
                totalDeleted += await cmd.ExecuteNonQueryAsync();
            }

            // 2. If count exceeds maxItems, delete oldest unpinned items
            if (maxItems > 0)
            {
                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM ClipboardItems;";
                var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                if (count > maxItems)
                {
                    int toDelete = count - maxItems;
                    using var limitCmd = conn.CreateCommand();
                    limitCmd.CommandText = @"
                        DELETE FROM ClipboardItems WHERE Id IN (
                            SELECT Id FROM ClipboardItems WHERE IsPinned = 0 
                            ORDER BY LastUsedAt ASC LIMIT @toDelete
                        );";
                    limitCmd.Parameters.AddWithValue("@toDelete", toDelete);
                    totalDeleted += await limitCmd.ExecuteNonQueryAsync();
                }
            }

            return totalDeleted;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task VacuumDatabaseAsync()
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM;";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task BackupDatabaseAsync(string destinationPath)
    {
        await EnsureInitializedAsync();

        await _lock.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var destConn = new SqliteConnection($"Data Source={destinationPath}");
            await destConn.OpenAsync();

            conn.BackupDatabase(destConn);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RestoreDatabaseAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Backup source database file not found.", sourcePath);
        }

        await _lock.WaitAsync();
        try
        {
            SqliteConnection.ClearAllPools();

            // Perform backup from source file into current db file
            using var sourceConn = new SqliteConnection($"Data Source={sourcePath}");
            await sourceConn.OpenAsync();

            using var destConn = new SqliteConnection(_connectionString);
            await destConn.OpenAsync();

            sourceConn.BackupDatabase(destConn);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void BindItemParameters(SqliteCommand cmd, ClipboardItem item)
    {
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@contentType", (int)item.ContentType);
        cmd.Parameters.AddWithValue("@textContent", (object?)item.TextContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@htmlContent", (object?)item.HtmlContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rtfContent", (object?)item.RtfContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@imageStoragePath", (object?)item.ImageStoragePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@imageThumbnailPath", (object?)item.ImageThumbnailPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@imageWidth", (object?)item.ImageWidth ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@imageHeight", (object?)item.ImageHeight ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@filePathsJson", JsonSerializer.Serialize(item.FilePaths ?? []));
        cmd.Parameters.AddWithValue("@contentHash", item.ContentHash ?? string.Empty);
        cmd.Parameters.AddWithValue("@createdAt", item.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@lastUsedAt", item.LastUsedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@useCount", item.UseCount);
        cmd.Parameters.AddWithValue("@isFavorite", item.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@isPinned", item.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@category", item.Category ?? "General");
        cmd.Parameters.AddWithValue("@tagsJson", JsonSerializer.Serialize(item.Tags ?? []));
        cmd.Parameters.AddWithValue("@sourceApplication", (object?)item.SourceApplication ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sourceWindowTitle", (object?)item.SourceWindowTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sizeBytes", item.SizeBytes);
        cmd.Parameters.AddWithValue("@smartDetectionJson", JsonSerializer.Serialize(item.SmartDetection ?? new()));
    }

    private static ClipboardItem MapFromReader(SqliteDataReader reader)
    {
        var item = new ClipboardItem
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            ContentType = (ContentType)reader.GetInt32(reader.GetOrdinal("ContentType")),
            TextContent = reader.IsDBNull(reader.GetOrdinal("TextContent")) ? null : reader.GetString(reader.GetOrdinal("TextContent")),
            HtmlContent = reader.IsDBNull(reader.GetOrdinal("HtmlContent")) ? null : reader.GetString(reader.GetOrdinal("HtmlContent")),
            RtfContent = reader.IsDBNull(reader.GetOrdinal("RtfContent")) ? null : reader.GetString(reader.GetOrdinal("RtfContent")),
            ImageStoragePath = reader.IsDBNull(reader.GetOrdinal("ImageStoragePath")) ? null : reader.GetString(reader.GetOrdinal("ImageStoragePath")),
            ImageThumbnailPath = reader.IsDBNull(reader.GetOrdinal("ImageThumbnailPath")) ? null : reader.GetString(reader.GetOrdinal("ImageThumbnailPath")),
            ImageWidth = reader.IsDBNull(reader.GetOrdinal("ImageWidth")) ? null : reader.GetInt32(reader.GetOrdinal("ImageWidth")),
            ImageHeight = reader.IsDBNull(reader.GetOrdinal("ImageHeight")) ? null : reader.GetInt32(reader.GetOrdinal("ImageHeight")),
            ContentHash = reader.GetString(reader.GetOrdinal("ContentHash")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            LastUsedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUsedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            UseCount = reader.GetInt32(reader.GetOrdinal("UseCount")),
            IsFavorite = reader.GetInt32(reader.GetOrdinal("IsFavorite")) == 1,
            IsPinned = reader.GetInt32(reader.GetOrdinal("IsPinned")) == 1,
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "General" : reader.GetString(reader.GetOrdinal("Category")),
            SourceApplication = reader.IsDBNull(reader.GetOrdinal("SourceApplication")) ? null : reader.GetString(reader.GetOrdinal("SourceApplication")),
            SourceWindowTitle = reader.IsDBNull(reader.GetOrdinal("SourceWindowTitle")) ? null : reader.GetString(reader.GetOrdinal("SourceWindowTitle")),
            SizeBytes = reader.GetInt64(reader.GetOrdinal("SizeBytes"))
        };

        if (!reader.IsDBNull(reader.GetOrdinal("FilePathsJson")))
        {
            try
            {
                var files = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("FilePathsJson")));
                if (files != null) item.FilePaths = files;
            }
            catch { }
        }

        if (!reader.IsDBNull(reader.GetOrdinal("TagsJson")))
        {
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("TagsJson")));
                if (tags != null) item.Tags = tags;
            }
            catch { }
        }

        if (!reader.IsDBNull(reader.GetOrdinal("SmartDetectionJson")))
        {
            try
            {
                var smart = JsonSerializer.Deserialize<SmartDetectionResult>(reader.GetString(reader.GetOrdinal("SmartDetectionJson")));
                if (smart != null) item.SmartDetection = smart;
            }
            catch { }
        }

        return item;
    }

    private static void DeleteAssociatedImageFiles(ClipboardItem item)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.ImageStoragePath) && File.Exists(item.ImageStoragePath))
            {
                File.Delete(item.ImageStoragePath);
            }
            if (!string.IsNullOrEmpty(item.ImageThumbnailPath) && File.Exists(item.ImageThumbnailPath))
            {
                File.Delete(item.ImageThumbnailPath);
            }
        }
        catch { }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
