using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace ClipFlow.App.Services;

public class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 101;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_LBUTTONDBLCLK = 0x0203;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded;
    private readonly ContextMenu _contextMenu;

    public event Action? TrayClicked;

    public TrayIconManager(ContextMenu contextMenu)
    {
        _contextMenu = contextMenu;
    }

    public void Initialize(IntPtr windowHandle)
    {
        _hwnd = windowHandle;

        try
        {
            var stream = Application.GetResourceStream(new Uri("pack://application:,,,/ClipFlow.App;component/Assets/app-icon.ico"))?.Stream;
            if (stream != null)
            {
                using var icon = new Icon(stream);
                _hIcon = icon.Handle;
            }
            else
            {
                _hIcon = SystemIcons.Application.Handle;
            }

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "ClipFlow — Smart Clipboard Manager"
            };

            _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create tray icon: {ex.Message}");
        }
    }

    public void HandleTrayMessage(IntPtr lParam)
    {
        int msg = lParam.ToInt32();
        if (msg == WM_LBUTTONUP || msg == WM_LBUTTONDBLCLK)
        {
            TrayClicked?.Invoke();
        }
        else if (msg == WM_RBUTTONUP)
        {
            if (_hwnd != IntPtr.Zero)
            {
                SetForegroundWindow(_hwnd);
            }
            _contextMenu.IsOpen = true;
        }
    }

    public void Dispose()
    {
        if (_isAdded && _hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isAdded = false;
        }
    }
}
