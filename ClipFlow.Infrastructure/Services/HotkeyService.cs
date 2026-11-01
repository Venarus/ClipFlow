using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ClipFlow.Core.Interfaces;
using ClipFlow.Infrastructure.Native;

namespace ClipFlow.Infrastructure.Services;

public class HotkeyService : IHotkeyService
{
    private IntPtr _hwnd = IntPtr.Zero;
    private int _currentId = 9000;
    private readonly object _lock = new();

    // Map: actionKey -> (hotkeyId, fsModifiers, vkCode, hotkeyString)
    private readonly Dictionary<string, HotkeyRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    // Map: hotkeyId -> actionKey
    private readonly ConcurrentDictionary<int, string> _idToAction = new();

    public event EventHandler<string>? HotkeyPressed;

    private class HotkeyRegistration
    {
        public int Id { get; set; }
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }
        public string RawString { get; set; } = string.Empty;
    }

    public void SetWindowHandle(IntPtr handle)
    {
        lock (_lock)
        {
            if (_hwnd != IntPtr.Zero && _hwnd != handle)
            {
                UnregisterAll();
            }

            _hwnd = handle;

            // Re-register any configured hotkeys on new handle
            foreach (var kvp in _registrations)
            {
                TryRegisterNative(kvp.Value);
            }
        }
    }

    public bool RegisterHotkey(string actionKey, string hotkeyString)
    {
        lock (_lock)
        {
            UnregisterHotkey(actionKey);

            if (!TryParseHotkey(hotkeyString, out var mods, out var vk))
            {
                return false;
            }

            var id = ++_currentId;
            var reg = new HotkeyRegistration
            {
                Id = id,
                Modifiers = mods,
                VirtualKey = vk,
                RawString = hotkeyString
            };

            _registrations[actionKey] = reg;
            _idToAction[id] = actionKey;

            if (_hwnd != IntPtr.Zero)
            {
                return TryRegisterNative(reg);
            }

            return true;
        }
    }

    public void UnregisterHotkey(string actionKey)
    {
        lock (_lock)
        {
            if (_registrations.TryGetValue(actionKey, out var reg))
            {
                if (_hwnd != IntPtr.Zero)
                {
                    NativeMethods.UnregisterHotKey(_hwnd, reg.Id);
                }
                _idToAction.TryRemove(reg.Id, out _);
                _registrations.Remove(actionKey);
            }
        }
    }

    public void UnregisterAll()
    {
        lock (_lock)
        {
            if (_hwnd != IntPtr.Zero)
            {
                foreach (var reg in _registrations.Values)
                {
                    NativeMethods.UnregisterHotKey(_hwnd, reg.Id);
                }
            }
            _registrations.Clear();
            _idToAction.Clear();
        }
    }

    public void HandleWmHotkey(IntPtr wParam)
    {
        int id = wParam.ToInt32();
        if (_idToAction.TryGetValue(id, out var actionKey))
        {
            HotkeyPressed?.Invoke(this, actionKey);
        }
    }

    private bool TryRegisterNative(HotkeyRegistration reg)
    {
        if (_hwnd == IntPtr.Zero) return false;
        // MOD_NOREPEAT = 0x4000 to avoid multiple events when key is held down
        uint flags = reg.Modifiers | 0x4000;
        return NativeMethods.RegisterHotKey(_hwnd, reg.Id, flags, reg.VirtualKey);
    }

    public static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

        // Constants for modifiers: MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008
        var parts = hotkeyString.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string? keyPart = null;

        foreach (var p in parts)
        {
            var part = p.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0002;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0001;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0004;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0008;
            }
            else
            {
                keyPart = part;
            }
        }

        if (string.IsNullOrEmpty(keyPart)) return false;

        // Parse key
        if (keyPart.Length == 1 && char.IsLetterOrDigit(keyPart[0]))
        {
            char c = char.ToUpperInvariant(keyPart[0]);
            virtualKey = (uint)c;
            return true;
        }

        if (Enum.TryParse<DayOfWeek>(keyPart, true, out _)) { } // avoid ambiguity

        // Common keys
        switch (keyPart.ToUpperInvariant())
        {
            case "F1": virtualKey = 0x70; return true;
            case "F2": virtualKey = 0x71; return true;
            case "F3": virtualKey = 0x72; return true;
            case "F4": virtualKey = 0x73; return true;
            case "F5": virtualKey = 0x74; return true;
            case "F6": virtualKey = 0x75; return true;
            case "F7": virtualKey = 0x76; return true;
            case "F8": virtualKey = 0x77; return true;
            case "F9": virtualKey = 0x78; return true;
            case "F10": virtualKey = 0x79; return true;
            case "F11": virtualKey = 0x7A; return true;
            case "F12": virtualKey = 0x7B; return true;
            case "SPACE": virtualKey = 0x20; return true;
            case "INSERT": virtualKey = 0x2D; return true;
            case "DELETE": virtualKey = 0x2E; return true;
            case "HOME": virtualKey = 0x24; return true;
            case "END": virtualKey = 0x23; return true;
            case "PAGEUP": virtualKey = 0x21; return true;
            case "PAGEDOWN": virtualKey = 0x22; return true;
            case "`":
            case "GRAVE":
            case "TILDE": virtualKey = 0xC0; return true;
        }

        return false;
    }
}
