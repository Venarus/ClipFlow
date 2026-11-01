using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ClipFlow.App.Services;
using ClipFlow.App.ViewModels;
using ClipFlow.Core.Interfaces;
using ClipFlow.Infrastructure.Native;
using ClipFlow.Infrastructure.Services;

namespace ClipFlow.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IClipboardMonitorService _monitorService;
    private readonly IHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;

    private TrayIconManager? _trayManager;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isExplicitExit;
    private bool _isInitialized;

    public MainViewModel ViewModel => _viewModel;

    public MainWindow(
        MainViewModel viewModel,
        IClipboardMonitorService monitorService,
        IHotkeyService hotkeyService,
        ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _monitorService = monitorService;
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;

        DataContext = _viewModel;
        Closing += MainWindow_Closing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (_isInitialized) return;
        _isInitialized = true;

        var helper = new WindowInteropHelper(this);
        _hwnd = helper.Handle;

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(HwndMessageHook);

        // Apply native Windows 11 rounded corners and backdrop
        ApplyWindows11WindowAttributes(_hwnd);

        // Initialize Tray Icon with WPF ContextMenu
        InitializeTrayIcon();

        // Register Global Hotkeys
        RegisterAppHotkeys();

        // Begin listening to Windows clipboard events
        _monitorService.StartMonitoring(_hwnd);
        _hotkeyService.SetWindowHandle(_hwnd);

        // Initialize database and load history
        _ = _viewModel.InitializeAsync();
    }

    private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            if (_monitorService is WindowsClipboardMonitorService wcm)
            {
                wcm.HandleWmClipboardUpdate();
            }
            handled = true;
        }
        else if (msg == NativeMethods.WM_HOTKEY)
        {
            _hotkeyService.HandleWmHotkey(wParam);
            handled = true;
        }
        else if (msg == TrayIconManager.WM_TRAYICON)
        {
            _trayManager?.HandleTrayMessage(lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ApplyWindows11WindowAttributes(IntPtr hwnd)
    {
        try
        {
            // 1. Dark Mode attribute (20)
            int useDarkMode = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

            // 2. Rounded corner preference (33) -> Round (2)
            int cornerPref = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // 3. System Backdrop Type (38) -> Acrylic (3) or Mica (2)
            int backdrop = _viewModel.Settings.BackdropMaterial switch
            {
                "Mica" => NativeMethods.DWMSBT_MAINWINDOW,
                "MicaAlt" => NativeMethods.DWMSBT_TABBEDWINDOW,
                "Solid" => NativeMethods.DWMSBT_NONE,
                _ => NativeMethods.DWMSBT_TRANSIENTWINDOW // Acrylic default
            };

            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DWM backdrop configuration failed: {ex.Message}");
        }
    }

    private void RegisterAppHotkeys()
    {
        _hotkeyService.HotkeyPressed += (s, actionKey) =>
        {
            if (actionKey == "ToggleWindow")
            {
                ToggleWindowVisibility();
            }
        };

        var toggleKey = _viewModel.Settings.ToggleWindowHotkey;
        if (!string.IsNullOrEmpty(toggleKey))
        {
            _hotkeyService.RegisterHotkey("ToggleWindow", toggleKey);
        }

        var clearKey = _viewModel.Settings.ClearHistoryHotkey;
        if (!string.IsNullOrEmpty(clearKey))
        {
            _hotkeyService.RegisterHotkey("ClearHistory", clearKey);
        }

        var monKey = _viewModel.Settings.ToggleMonitoringHotkey;
        if (!string.IsNullOrEmpty(monKey))
        {
            _hotkeyService.RegisterHotkey("ToggleMonitoring", monKey);
        }
    }

    private void InitializeTrayIcon()
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open ClipFlow" };
        openItem.Click += (s, e) => ShowAndActivate();
        menu.Items.Add(openItem);

        menu.Items.Add(new Separator());

        var monItem = new MenuItem { Header = _viewModel.IsMonitoring ? "Pause Monitoring" : "Resume Monitoring" };
        monItem.Click += (s, e) =>
        {
            _viewModel.ToggleMonitoringCommand.Execute(null);
            monItem.Header = _viewModel.IsMonitoring ? "Pause Monitoring" : "Resume Monitoring";
        };
        menu.Items.Add(monItem);

        var clearItem = new MenuItem { Header = "Clear History" };
        clearItem.Click += async (s, e) => await _viewModel.ClearAllHistoryAsync();
        menu.Items.Add(clearItem);

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (s, e) =>
        {
            ShowAndActivate();
            _viewModel.OpenSettingsCommand.Execute(null);
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit ClipFlow" };
        exitItem.Click += (s, e) => ExitApplication();
        menu.Items.Add(exitItem);

        _trayManager = new TrayIconManager(menu);
        _trayManager.TrayClicked += ToggleWindowVisibility;
        _trayManager.Initialize(_hwnd);
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
    }

    public void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            System.Threading.Tasks.Task.Run(() => NativeMethods.TrimWorkingSet());
        }
        else
        {
            ShowAndActivate();
        }
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeRestore();
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        System.Threading.Tasks.Task.Run(() => NativeMethods.TrimWorkingSet());
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        MaximizeRestore();
    }

    private void MaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Settings.MinimizeToTrayOnClose)
        {
            Hide();
            System.Threading.Tasks.Task.Run(() => NativeMethods.TrimWorkingSet());
        }
        else
        {
            ExitApplication();
        }
    }

    private void AlwaysOnTopButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _viewModel.Settings.AlwaysOnTop = Topmost;
        _viewModel.ShowToast(Topmost ? "Always on top: Enabled" : "Always on top: Disabled");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.Settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        // Clean up resources
        _trayManager?.Dispose();
        _hotkeyService.UnregisterAll();
        _monitorService.StopMonitoring();
    }
}
