using System;
using System.Threading.Tasks;
using ClipFlow.Core.Models;

namespace ClipFlow.Core.Interfaces;

public class ClipboardItemEventArgs : EventArgs
{
    public ClipboardItem Item { get; }

    public ClipboardItemEventArgs(ClipboardItem item)
    {
        Item = item;
    }
}

public interface IClipboardMonitorService
{
    event EventHandler<ClipboardItemEventArgs>? ClipboardItemCaptured;
    event EventHandler<bool>? MonitoringStateChanged;

    bool IsMonitoring { get; }
    void StartMonitoring(IntPtr windowHandle);
    void StopMonitoring();
    void PauseMonitoring();
    void ResumeMonitoring();
    Task<bool> CopyToClipboardAsync(ClipboardItem item);
    Task ClearClipboardAsync();
}
