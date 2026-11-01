using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;
using ClipFlow.Core.Utilities;
using ClipFlow.Infrastructure.Native;

namespace ClipFlow.Infrastructure.Services;

public class WindowsClipboardMonitorService : IClipboardMonitorService
{
    private readonly ISettingsService _settingsService;
    private readonly ISecurityService _securityService;
    private readonly ISmartContentRecognizer _smartRecognizer;
    private readonly string _imagesDirectory;
    private readonly Dispatcher _dispatcher;

    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isMonitoring;
    private bool _isPaused;
    private volatile bool _isSelfCopy;
    private string? _lastCapturedHash;
    private DateTime _lastCapturedTime = DateTime.MinValue;

    public event EventHandler<ClipboardItemEventArgs>? ClipboardItemCaptured;
    public event EventHandler<bool>? MonitoringStateChanged;

    public bool IsMonitoring => _isMonitoring && !_isPaused;

    public WindowsClipboardMonitorService(
        ISettingsService settingsService,
        ISecurityService securityService,
        ISmartContentRecognizer smartRecognizer,
        string? imagesDirectory = null,
        Dispatcher? dispatcher = null)
    {
        _settingsService = settingsService;
        _securityService = securityService;
        _smartRecognizer = smartRecognizer;
        _dispatcher = dispatcher ?? (Application.Current != null ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher);

        if (!string.IsNullOrEmpty(imagesDirectory))
        {
            _imagesDirectory = imagesDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _imagesDirectory = Path.Combine(appData, "ClipFlow", "Images");
        }
        Directory.CreateDirectory(_imagesDirectory);
    }

    public void StartMonitoring(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;

        if (_hwnd != IntPtr.Zero && _hwnd != windowHandle)
        {
            StopMonitoring();
        }

        _hwnd = windowHandle;
        bool added = NativeMethods.AddClipboardFormatListener(_hwnd);
        _isMonitoring = added;
        MonitoringStateChanged?.Invoke(this, IsMonitoring);
        Debug.WriteLine($"ClipFlow Clipboard Monitor started on HWND: {_hwnd}, Status: {added}");
    }

    public void StopMonitoring()
    {
        if (_hwnd != IntPtr.Zero && _isMonitoring)
        {
            NativeMethods.RemoveClipboardFormatListener(_hwnd);
            _isMonitoring = false;
            MonitoringStateChanged?.Invoke(this, IsMonitoring);
            Debug.WriteLine("ClipFlow Clipboard Monitor stopped.");
        }
    }

    public void PauseMonitoring()
    {
        _isPaused = true;
        MonitoringStateChanged?.Invoke(this, IsMonitoring);
    }

    public void ResumeMonitoring()
    {
        _isPaused = false;
        MonitoringStateChanged?.Invoke(this, IsMonitoring);
    }

    public void HandleWmClipboardUpdate()
    {
        if (!_isMonitoring || _isPaused || !_settingsService.Settings.IsMonitoringEnabled)
        {
            return;
        }

        if (_isSelfCopy)
        {
            // Reset self copy flag
            _isSelfCopy = false;
            return;
        }

        // Run extraction on Dispatcher (STA required for WPF Clipboard APIs)
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () =>
        {
            try
            {
                await ProcessClipboardChangeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing clipboard update: {ex.Message}");
            }
        }));
    }

    private async Task ProcessClipboardChangeAsync()
    {
        // 1. Check foreground application exclusion
        var (procName, windowTitle) = NativeMethods.GetForegroundAppInfo();
        if (_securityService.IsApplicationExcluded(procName))
        {
            Debug.WriteLine($"Ignored clipboard change from excluded application: {procName}");
            return;
        }

        // 2. Check format names for password managers
        var formats = NativeMethods.GetCurrentClipboardFormatNames();
        if (_securityService.ShouldIgnoreClipboardFormats(formats))
        {
            Debug.WriteLine("Ignored clipboard change due to password manager format flag.");
            return;
        }

        // 3. Retry reading clipboard data safely
        IDataObject? dataObj = null;
        for (int retry = 0; retry < 5; retry++)
        {
            try
            {
                dataObj = Clipboard.GetDataObject();
                if (dataObj != null) break;
            }
            catch
            {
                await Task.Delay(25 * (retry + 1));
            }
        }

        if (dataObj == null) return;

        ClipboardItem? capturedItem = null;

        // 4. File Drop List check
        if (dataObj.GetDataPresent(DataFormats.FileDrop))
        {
            try
            {
                var files = dataObj.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    var fileList = files.ToList();
                    var contentHash = HashUtilities.ComputeSha256(string.Join("|", fileList));

                    // Avoid duplicate event within 500ms
                    if (IsRapidDuplicate(contentHash)) return;

                    var smart = _smartRecognizer.Recognize(null, ContentType.FileDrop, fileList);
                    capturedItem = new ClipboardItem
                    {
                        ContentType = ContentType.FileDrop,
                        FilePaths = fileList,
                        ContentHash = contentHash,
                        SourceApplication = procName,
                        SourceWindowTitle = windowTitle,
                        SmartDetection = smart,
                        TextContent = string.Join(Environment.NewLine, fileList),
                        SizeBytes = fileList.Sum(f => {
                            try { return File.Exists(f) ? new FileInfo(f).Length : 0; } catch { return 0L; }
                        })
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FileDrop extraction error: {ex.Message}");
            }
        }

        // 5. Image check (if not file drop)
        if (capturedItem == null && dataObj.GetDataPresent(DataFormats.Bitmap) && _settingsService.Settings.StoreImages)
        {
            try
            {
                var bmp = Clipboard.GetImage();
                if (bmp != null)
                {
                    var (imagePath, thumbPath, sizeBytes, hash) = await SaveBitmapSourceAsync(bmp);
                    if (imagePath != null && !IsRapidDuplicate(hash))
                    {
                        var smart = new SmartDetectionResult
                        {
                            DetectedType = SmartType.None,
                            DisplayText = $"Image ({bmp.PixelWidth}x{bmp.PixelHeight})",
                            FileSizeFormatted = HashUtilities.FormatFileSize(sizeBytes)
                        };

                        capturedItem = new ClipboardItem
                        {
                            ContentType = ContentType.Image,
                            ImageStoragePath = imagePath,
                            ImageThumbnailPath = thumbPath,
                            ImageWidth = bmp.PixelWidth,
                            ImageHeight = bmp.PixelHeight,
                            ContentHash = hash,
                            SourceApplication = procName,
                            SourceWindowTitle = windowTitle,
                            SizeBytes = sizeBytes,
                            SmartDetection = smart,
                            TextContent = $"[Image: {bmp.PixelWidth}x{bmp.PixelHeight}]"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bitmap extraction error: {ex.Message}");
            }
        }

        // 6. Text check (plain text, html, rtf)
        if (capturedItem == null && (dataObj.GetDataPresent(DataFormats.UnicodeText) || dataObj.GetDataPresent(DataFormats.Text)))
        {
            try
            {
                string text = string.Empty;
                if (dataObj.GetDataPresent(DataFormats.UnicodeText))
                {
                    text = dataObj.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
                }
                else if (dataObj.GetDataPresent(DataFormats.Text))
                {
                    text = dataObj.GetData(DataFormats.Text) as string ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Privacy filter: check for sensitive tokens/passwords
                    if (_securityService.ContainsSensitiveData(text, out var reason))
                    {
                        Debug.WriteLine($"Clipboard text rejected due to sensitive data filter: {reason}");
                        return;
                    }

                    var hash = HashUtilities.ComputeSha256(text);
                    if (IsRapidDuplicate(hash)) return;

                    string? html = dataObj.GetDataPresent(DataFormats.Html) ? dataObj.GetData(DataFormats.Html) as string : null;
                    string? rtf = dataObj.GetDataPresent(DataFormats.Rtf) ? dataObj.GetData(DataFormats.Rtf) as string : null;

                    var smart = _smartRecognizer.Recognize(text, ContentType.Text);

                    // Determine refined ContentType
                    var finalType = smart.DetectedType switch
                    {
                        SmartType.HexColor or SmartType.RgbColor => ContentType.Color,
                        SmartType.Url => ContentType.Url,
                        SmartType.Email => ContentType.Email,
                        SmartType.Phone => ContentType.Phone,
                        SmartType.Code => ContentType.Code,
                        _ => ContentType.Text
                    };

                    capturedItem = new ClipboardItem
                    {
                        ContentType = finalType,
                        TextContent = text,
                        HtmlContent = html,
                        RtfContent = rtf,
                        ContentHash = hash,
                        SourceApplication = procName,
                        SourceWindowTitle = windowTitle,
                        SizeBytes = System.Text.Encoding.UTF8.GetByteCount(text),
                        SmartDetection = smart
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Text extraction error: {ex.Message}");
            }
        }

        if (capturedItem != null)
        {
            _lastCapturedHash = capturedItem.ContentHash;
            _lastCapturedTime = DateTime.UtcNow;
            ClipboardItemCaptured?.Invoke(this, new ClipboardItemEventArgs(capturedItem));
        }
    }

    private bool IsRapidDuplicate(string hash)
    {
        if (string.Equals(_lastCapturedHash, hash, StringComparison.Ordinal) && 
            (DateTime.UtcNow - _lastCapturedTime).TotalMilliseconds < 800)
        {
            return true;
        }
        return false;
    }

    private async Task<(string? path, string? thumbPath, long size, string hash)> SaveBitmapSourceAsync(BitmapSource bitmap)
    {
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var fullPath = Path.Combine(_imagesDirectory, $"{id}.png");
            var thumbPath = Path.Combine(_imagesDirectory, $"{id}_thumb.png");

            byte[] fullBytes;
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(ms);
                fullBytes = ms.ToArray();
            }

            // Check size against limits
            long maxBytes = (long)_settingsService.Settings.MaxImageSizeMb * 1024 * 1024;
            if (fullBytes.Length > maxBytes)
            {
                Debug.WriteLine($"Image rejected: size {fullBytes.Length} exceeds limit {maxBytes}");
                return (null, null, 0, string.Empty);
            }

            await File.WriteAllBytesAsync(fullPath, fullBytes);
            var hash = HashUtilities.ComputeSha256(fullBytes);

            // Generate thumbnail (max 200x200 maintaining aspect ratio)
            try
            {
                double scale = Math.Min(200.0 / bitmap.PixelWidth, 200.0 / bitmap.PixelHeight);
                if (scale < 1.0)
                {
                    var transformed = new TransformedBitmap(bitmap, new System.Windows.Media.ScaleTransform(scale, scale));
                    using var thumbMs = new MemoryStream();
                    var thumbEncoder = new PngBitmapEncoder();
                    thumbEncoder.Frames.Add(BitmapFrame.Create(transformed));
                    thumbEncoder.Save(thumbMs);
                    await File.WriteAllBytesAsync(thumbPath, thumbMs.ToArray());
                }
                else
                {
                    thumbPath = fullPath;
                }
            }
            catch
            {
                thumbPath = fullPath;
            }

            return (fullPath, thumbPath, fullBytes.Length, hash);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to encode/save bitmap: {ex.Message}");
            return (null, null, 0, string.Empty);
        }
    }

    public async Task<bool> CopyToClipboardAsync(ClipboardItem item)
    {
        return await _dispatcher.InvokeAsync(() =>
        {
            try
            {
                _isSelfCopy = true;

                if (item.ContentType == ContentType.Image && !string.IsNullOrEmpty(item.ImageStoragePath) && File.Exists(item.ImageStoragePath))
                {
                    var uri = new Uri(item.ImageStoragePath, UriKind.Absolute);
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = uri;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();

                    Clipboard.SetImage(img);
                    return true;
                }

                if (item.ContentType == ContentType.FileDrop && item.FilePaths.Count > 0)
                {
                    var fileCollection = new StringCollection();
                    foreach (var f in item.FilePaths)
                    {
                        if (File.Exists(f) || Directory.Exists(f))
                        {
                            fileCollection.Add(f);
                        }
                    }

                    if (fileCollection.Count > 0)
                    {
                        Clipboard.SetFileDropList(fileCollection);
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(item.TextContent))
                {
                    var dataObj = new DataObject();
                    dataObj.SetText(item.TextContent, TextDataFormat.UnicodeText);

                    if (!string.IsNullOrEmpty(item.HtmlContent))
                    {
                        dataObj.SetData(DataFormats.Html, item.HtmlContent);
                    }
                    if (!string.IsNullOrEmpty(item.RtfContent))
                    {
                        dataObj.SetData(DataFormats.Rtf, item.RtfContent);
                    }

                    Clipboard.SetDataObject(dataObj, true);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing to clipboard: {ex.Message}");
                return false;
            }
        });
    }

    public async Task ClearClipboardAsync()
    {
        await _dispatcher.InvokeAsync(() =>
        {
            try
            {
                _isSelfCopy = true;
                Clipboard.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing clipboard: {ex.Message}");
            }
        });
    }
}
