using System;
using System.Collections.Generic;

namespace ClipFlow.Core.Models;

public class ClipboardItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ContentType ContentType { get; set; } = ContentType.Text;
    public string? TextContent { get; set; }
    public string? HtmlContent { get; set; }
    public string? RtfContent { get; set; }
    public string? ImageStoragePath { get; set; }
    public string? ImageThumbnailPath { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public List<string> FilePaths { get; set; } = [];
    public string ContentHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public int UseCount { get; set; } = 1;
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }
    public string Category { get; set; } = "General";
    public List<string> Tags { get; set; } = [];
    public string? SourceApplication { get; set; }
    public string? SourceWindowTitle { get; set; }
    public long SizeBytes { get; set; }
    public SmartDetectionResult SmartDetection { get; set; } = new();

    public string PreviewSummary
    {
        get
        {
            if (ContentType == ContentType.Image)
            {
                return $"Image ({ImageWidth}x{ImageHeight})";
            }
            if (ContentType == ContentType.FileDrop && FilePaths.Count > 0)
            {
                return FilePaths.Count == 1 
                    ? System.IO.Path.GetFileName(FilePaths[0]) 
                    : $"{FilePaths.Count} files ({System.IO.Path.GetFileName(FilePaths[0])}...)";
            }
            if (!string.IsNullOrWhiteSpace(TextContent))
            {
                var trimmed = TextContent.Trim();
                if (trimmed.Length > 200)
                {
                    return trimmed.Substring(0, 200) + "...";
                }
                return trimmed;
            }
            return "[Empty content]";
        }
    }
}
