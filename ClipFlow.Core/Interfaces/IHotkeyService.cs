using System;

namespace ClipFlow.Core.Interfaces;

public interface IHotkeyService
{
    event EventHandler<string>? HotkeyPressed;
    bool RegisterHotkey(string actionKey, string hotkeyString);
    void UnregisterHotkey(string actionKey);
    void UnregisterAll();
    void SetWindowHandle(IntPtr handle);
    void HandleWmHotkey(IntPtr wParam);
}
