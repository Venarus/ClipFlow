using System;

namespace ClipFlow.Core.Models;

public class FilterCriteria
{
    public string? SearchQuery { get; set; }
    public ContentType? ContentType { get; set; }
    public System.Collections.Generic.List<ContentType>? ContentTypes { get; set; }
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public bool OnlyFavorites { get; set; }
    public bool OnlyPinned { get; set; }
    public DateFilterOption DateFilter { get; set; } = DateFilterOption.All;
    public SortOption SortBy { get; set; } = SortOption.Newest;
    public int Limit { get; set; } = 300;
    public int Offset { get; set; } = 0;
}

public class StorageStats
{
    public int TotalItemCount { get; set; }
    public int FavoriteCount { get; set; }
    public int PinnedCount { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public long ImageStorageSizeBytes { get; set; }
    public DateTime? OldestItemDate { get; set; }
    public DateTime? NewestItemDate { get; set; }
}
