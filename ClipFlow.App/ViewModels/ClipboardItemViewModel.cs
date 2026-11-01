using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipFlow.App.ViewModels;

public partial class ClipboardItemViewModel : ObservableObject
{
    public ClipboardItem Model { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string _category;

    private ImageSource? _thumbnailSource;
    private bool _thumbnailLoaded;

    public ClipboardItemViewModel(ClipboardItem model)
    {
        Model = model;
        _isFavorite = model.IsFavorite;
        _isPinned = model.IsPinned;
        _category = model.Category ?? "General";
    }

    public string Id => Model.Id;
    public ContentType ContentType => Model.ContentType;
    public string? TextContent => Model.TextContent;
    public string? HtmlContent => Model.HtmlContent;
    public string? RtfContent => Model.RtfContent;
    public string? ImageStoragePath => Model.ImageStoragePath;
    public int? ImageWidth => Model.ImageWidth;
    public int? ImageHeight => Model.ImageHeight;
    public System.Collections.Generic.List<string> FilePaths => Model.FilePaths;
    public int UseCount => Model.UseCount;
    public DateTime CreatedAt => Model.CreatedAt;
    public DateTime LastUsedAt => Model.LastUsedAt;
    public string? SourceApplication => Model.SourceApplication;
    public string? SourceWindowTitle => Model.SourceWindowTitle;
    public long SizeBytes => Model.SizeBytes;
    public SmartDetectionResult SmartDetection => Model.SmartDetection;

    public bool HasImage => ContentType == ContentType.Image && !string.IsNullOrEmpty(ImageStoragePath);
    public bool HasColorPreview => ContentType == ContentType.Color && !string.IsNullOrEmpty(SmartDetection?.ColorHex);
    public string? ColorHex => SmartDetection?.ColorHex;
    public string? ColorRgb => SmartDetection?.ColorRgb;
    public bool IsCode => ContentType == ContentType.Code;
    public string? CodeLanguage => SmartDetection?.CodeLanguage ?? "Code";
    public bool IsUrl => ContentType == ContentType.Url;
    public string? Domain => SmartDetection?.Domain;
    public bool IsFileDrop => ContentType == ContentType.FileDrop;
    public int FileCount => FilePaths?.Count ?? 0;

    public string TypeDisplayName => ContentType switch
    {
        ContentType.Text => "Text",
        ContentType.Code => SmartDetection?.CodeLanguage ?? "Code",
        ContentType.Color => "Color",
        ContentType.Url => "Link",
        ContentType.Email => "Email",
        ContentType.Phone => "Phone",
        ContentType.Image => "Image",
        ContentType.FileDrop => FileCount == 1 ? "File" : "Files",
        _ => "Content"
    };

    public bool IsImageFile
    {
        get
        {
            if (ContentType == ContentType.FileDrop && FilePaths != null && FilePaths.Count > 0)
            {
                var ext = Path.GetExtension(FilePaths[0])?.ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".ico";
            }
            return false;
        }
    }

    public string? FirstFilePath => FilePaths != null && FilePaths.Count > 0 ? FilePaths[0] : null;

    public string FileExtension
    {
        get
        {
            if (!string.IsNullOrEmpty(FirstFilePath))
            {
                var ext = Path.GetExtension(FirstFilePath).TrimStart('.').ToUpperInvariant();
                return string.IsNullOrEmpty(ext) ? "FILE" : (ext.Length > 4 ? ext[..4] : ext);
            }
            return "FILE";
        }
    }

    public string FileDisplayName
    {
        get
        {
            if (FileCount > 1) return $"{FileCount} files";
            if (!string.IsNullOrEmpty(FirstFilePath)) return Path.GetFileName(FirstFilePath);
            return "File";
        }
    }

    public bool HasImageThumbnail => (HasImage || IsImageFile) && ThumbnailSource != null;
    public bool HasSystemFileIcon => ContentType == ContentType.FileDrop && !IsImageFile && ThumbnailSource != null;

    public string DisplayTitle
    {
        get
        {
            if (ContentType == ContentType.Image)
            {
                return ImageWidth > 0 && ImageHeight > 0
                    ? $"Image ({ImageWidth} × {ImageHeight} px)"
                    : "Image";
            }
            if (ContentType == ContentType.FileDrop)
            {
                return FileDisplayName;
            }
            if (ContentType == ContentType.Color)
            {
                return ColorHex ?? "Color Code";
            }
            if (ContentType == ContentType.Url)
            {
                return Domain ?? TextContent ?? "URL";
            }
            if (!string.IsNullOrWhiteSpace(TextContent))
            {
                var firstLine = TextContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine))
                {
                    return firstLine.Trim();
                }
            }
            return "Empty Item";
        }
    }

    public string DisplaySnippet
    {
        get
        {
            if (ContentType == ContentType.Image)
            {
                return FormattedSize;
            }
            if (ContentType == ContentType.FileDrop)
            {
                return FirstFilePath ?? FormattedSize;
            }
            if (ContentType == ContentType.Color)
            {
                return ColorRgb ?? ColorHex ?? "";
            }
            if (ContentType == ContentType.Url)
            {
                return TextContent ?? "";
            }
            if (!string.IsNullOrWhiteSpace(TextContent))
            {
                var lines = TextContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    return lines[1].Trim();
                }
                return TextContent.Trim();
            }
            return "";
        }
    }

    public string RelativeTime
    {
        get
        {
            var span = DateTime.UtcNow - LastUsedAt;
            if (span.TotalSeconds < 45) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 2) return "Yesterday";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return LastUsedAt.ToLocalTime().ToString("MMM dd");
        }
    }

    public string FormattedSize => Core.Utilities.HashUtilities.FormatFileSize(SizeBytes);

    public ImageSource? ThumbnailSource
    {
        get
        {
            if (!_thumbnailLoaded)
            {
                _thumbnailLoaded = true;
                _thumbnailSource = LoadThumbnail();
            }
            return _thumbnailSource;
        }
    }

    private ImageSource? LoadThumbnail()
    {
        try
        {
            // 1. Direct image
            var path = Model.ImageThumbnailPath ?? Model.ImageStoragePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 240;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }

            // 2. Image from file drop
            if (IsImageFile && !string.IsNullOrEmpty(FirstFilePath) && File.Exists(FirstFilePath))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(FirstFilePath, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 240;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }

            // 3. System file icon for non-image files
            if (ContentType == ContentType.FileDrop && !string.IsNullOrEmpty(FirstFilePath) && File.Exists(FirstFilePath))
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(FirstFilePath);
                if (icon != null)
                {
                    var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze();
                    return bs;
                }
            }
        }
        catch { }
        return null;
    }

    public void RefreshRelativeTime()
    {
        OnPropertyChanged(nameof(RelativeTime));
    }
}
