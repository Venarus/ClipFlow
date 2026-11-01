using System.Collections.Generic;
using System.Threading.Tasks;
using ClipFlow.Core.Models;

namespace ClipFlow.Core.Interfaces;

public interface IClipboardStorageRepository
{
    Task InitializeAsync();
    Task<List<ClipboardItem>> GetItemsAsync(FilterCriteria filter);
    Task<ClipboardItem?> GetItemByIdAsync(string id);
    Task<ClipboardItem?> FindDuplicateAsync(string contentHash);
    Task<bool> AddOrUpdateAsync(ClipboardItem item);
    Task<bool> UpdateItemAsync(ClipboardItem item);
    Task<bool> DeleteItemAsync(string id);
    Task<int> DeleteItemsAsync(IEnumerable<string> ids);
    Task<int> ClearHistoryAsync(bool preservePinned = true);
    Task<bool> ToggleFavoriteAsync(string id);
    Task<bool> TogglePinAsync(string id);
    Task<bool> IncrementUseCountAsync(string id);
    Task<List<string>> GetCategoriesAsync();
    Task<List<string>> GetAllTagsAsync();
    Task<StorageStats> GetStorageStatsAsync();
    Task<int> RunAutoCleanupAsync(int retentionDays, int maxItems);
    Task VacuumDatabaseAsync();
    Task BackupDatabaseAsync(string destinationPath);
    Task RestoreDatabaseAsync(string sourcePath);
}
