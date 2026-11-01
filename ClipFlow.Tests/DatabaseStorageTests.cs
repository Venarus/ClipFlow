using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ClipFlow.Core.Models;
using ClipFlow.Core.Utilities;
using ClipFlow.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClipFlow.Tests;

public class DatabaseStorageTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteClipboardRepository _repository;

    public DatabaseStorageTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_clipflow_{Guid.NewGuid():N}.db");
        _repository = new SqliteClipboardRepository(_tempDbPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            var shm = _tempDbPath + "-shm";
            var wal = _tempDbPath + "-wal";
            if (File.Exists(shm)) File.Delete(shm);
            if (File.Exists(wal)) File.Delete(wal);
        }
        catch { }
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabaseAndIndexes()
    {
        await _repository.InitializeAsync();
        Assert.True(File.Exists(_tempDbPath));

        var stats = await _repository.GetStorageStatsAsync();
        Assert.Equal(0, stats.TotalItemCount);
    }

    [Fact]
    public async Task AddItem_And_GetItems_ShouldStoreAndRetrieveData()
    {
        await _repository.InitializeAsync();

        var item = new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "Hello from ClipFlow Unit Tests!",
            ContentHash = HashUtilities.ComputeSha256("Hello from ClipFlow Unit Tests!"),
            Category = "Development",
            Tags = new List<string> { "csharp", "dotnet" }
        };

        var added = await _repository.AddOrUpdateAsync(item);
        Assert.True(added);

        var retrieved = await _repository.GetItemByIdAsync(item.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(item.TextContent, retrieved.TextContent);
        Assert.Equal("Development", retrieved.Category);
        Assert.Contains("csharp", retrieved.Tags);
    }

    [Fact]
    public async Task AddDuplicateItem_ShouldBumpUseCountAndNotDuplicateRow()
    {
        await _repository.InitializeAsync();

        var text = "Duplicate content test";
        var hash = HashUtilities.ComputeSha256(text);

        var item1 = new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = text,
            ContentHash = hash
        };
        await _repository.AddOrUpdateAsync(item1);

        var item2 = new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = text,
            ContentHash = hash
        };
        await _repository.AddOrUpdateAsync(item2);

        var all = await _repository.GetItemsAsync(new FilterCriteria());
        Assert.Single(all);
        Assert.Equal(2, all[0].UseCount);
    }

    [Fact]
    public async Task ToggleFavoriteAndPin_ShouldPersistState()
    {
        await _repository.InitializeAsync();

        var item = new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "Pin and Favorite item",
            ContentHash = HashUtilities.ComputeSha256("Pin and Favorite item")
        };
        await _repository.AddOrUpdateAsync(item);

        await _repository.ToggleFavoriteAsync(item.Id);
        var favItem = await _repository.GetItemByIdAsync(item.Id);
        Assert.True(favItem!.IsFavorite);

        await _repository.TogglePinAsync(item.Id);
        var pinnedItem = await _repository.GetItemByIdAsync(item.Id);
        Assert.True(pinnedItem!.IsPinned);
    }

    [Fact]
    public async Task Search_ByQueryAndCategory_ShouldReturnFilteredResults()
    {
        await _repository.InitializeAsync();

        await _repository.AddOrUpdateAsync(new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "ClipFlow WPF Windows 11",
            ContentHash = HashUtilities.ComputeSha256("1"),
            Category = "UI"
        });

        await _repository.AddOrUpdateAsync(new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "Python FastApi Backend",
            ContentHash = HashUtilities.ComputeSha256("2"),
            Category = "Web"
        });

        // Search text
        var searchResult = await _repository.GetItemsAsync(new FilterCriteria { SearchQuery = "Windows" });
        Assert.Single(searchResult);
        Assert.Equal("UI", searchResult[0].Category);

        // Search category
        var catResult = await _repository.GetItemsAsync(new FilterCriteria { Category = "Web" });
        Assert.Single(catResult);
        Assert.Contains("FastApi", catResult[0].TextContent);
    }

    [Fact]
    public async Task ClearHistory_ShouldPreservePinnedItems()
    {
        await _repository.InitializeAsync();

        var normalItem = new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "Normal item",
            ContentHash = HashUtilities.ComputeSha256("normal"),
            IsPinned = false
        };
        var pinnedItem = new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "Pinned essential note",
            ContentHash = HashUtilities.ComputeSha256("pinned"),
            IsPinned = true
        };

        await _repository.AddOrUpdateAsync(normalItem);
        await _repository.AddOrUpdateAsync(pinnedItem);

        var clearedCount = await _repository.ClearHistoryAsync(preservePinned: true);
        Assert.Equal(1, clearedCount);

        var remaining = await _repository.GetItemsAsync(new FilterCriteria());
        Assert.Single(remaining);
        Assert.Equal(pinnedItem.TextContent, remaining[0].TextContent);
    }

    [Fact]
    public async Task RunAutoCleanup_ShouldEnforceMaxCount()
    {
        await _repository.InitializeAsync();

        for (int i = 0; i < 15; i++)
        {
            await _repository.AddOrUpdateAsync(new ClipboardItem
            {
                ContentType = ContentType.Text,
                TextContent = $"Item #{i}",
                ContentHash = HashUtilities.ComputeSha256($"item_{i}"),
                CreatedAt = DateTime.UtcNow.AddMinutes(i),
                LastUsedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        // Cleanup: max 10 items
        var deleted = await _repository.RunAutoCleanupAsync(retentionDays: 0, maxItems: 10);
        Assert.Equal(5, deleted);

        var count = (await _repository.GetItemsAsync(new FilterCriteria())).Count;
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task BackupAndRestore_ShouldSuccessfullyCloneDatabase()
    {
        await _repository.InitializeAsync();

        await _repository.AddOrUpdateAsync(new ClipboardItem
        {
            ContentType = ContentType.Text,
            TextContent = "Database Backup Test Content",
            ContentHash = HashUtilities.ComputeSha256("backup_test")
        });

        var backupPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid():N}.db");
        try
        {
            await _repository.BackupDatabaseAsync(backupPath);
            Assert.True(File.Exists(backupPath));

            // Verify backup content
            using (var backupRepo = new SqliteClipboardRepository(backupPath))
            {
                await backupRepo.InitializeAsync();
                var items = await backupRepo.GetItemsAsync(new FilterCriteria());
                Assert.Single(items);
                Assert.Equal("Database Backup Test Content", items[0].TextContent);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            catch { }
        }
    }

    [Fact]
    public async Task LargeHistoryPerformance_1000Items_QuerySpeed()
    {
        await _repository.InitializeAsync();

        // Batch insert 1000 items
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            await _repository.AddOrUpdateAsync(new ClipboardItem
            {
                ContentType = (ContentType)(i % 5),
                TextContent = $"Performance item {i} with payload data",
                ContentHash = HashUtilities.ComputeSha256($"item_{i}"),
                CreatedAt = DateTime.UtcNow.AddHours(-i),
                LastUsedAt = DateTime.UtcNow.AddHours(-i),
                IsFavorite = i % 10 == 0,
                IsPinned = i % 50 == 0,
                Category = i % 2 == 0 ? "Dev" : "Work"
            });
        }
        sw.Stop();

        // Search query should be extremely fast (indexed)
        var querySw = Stopwatch.StartNew();
        var searchResults = await _repository.GetItemsAsync(new FilterCriteria
        {
            SearchQuery = "item 99",
            Category = "Dev",
            Limit = 50
        });
        querySw.Stop();

        Assert.NotEmpty(searchResults);
        Assert.True(querySw.ElapsedMilliseconds < 100, $"Query was {querySw.ElapsedMilliseconds}ms, expected < 100ms");
    }
}
