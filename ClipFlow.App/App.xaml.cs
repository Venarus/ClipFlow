using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using ClipFlow.App.ViewModels;
using ClipFlow.Core.Interfaces;
using ClipFlow.Infrastructure.Services;
using ClipFlow.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClipFlow.App;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private IServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enforce single instance application
        const string mutexName = "ClipFlow_SingleInstance_Mutex_Global";
        _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            System.Windows.MessageBox.Show(
                "ClipFlow is already running in the background. Check your Windows system tray.",
                "ClipFlow",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Global exception logging
        DispatcherUnhandledException += (s, args) =>
        {
            Debug.WriteLine($"[ClipFlow] Unhandled UI Exception: {args.Exception.Message}\n{args.Exception.StackTrace}");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Debug.WriteLine($"[ClipFlow] Critical Unhandled Exception: {args.ExceptionObject}");
        };

        base.OnStartup(e);

        // Configure Services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Resolve and initialize MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Ensure HWND is created so OnSourceInitialized fires immediately
        var helper = new WindowInteropHelper(mainWindow);
        helper.EnsureHandle();

        // Check if started with --minimized flag (e.g. Windows startup)
        bool startMinimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase) ||
                              mainWindow.ViewModel.Settings.StartMinimized;

        if (startMinimized)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Hide();
        }
        else
        {
            mainWindow.Show();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISmartContentRecognizer, SmartContentRecognizer>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IClipboardStorageRepository, SqliteClipboardRepository>();
        services.AddSingleton<IStartupManager, StartupManager>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IClipboardMonitorService, WindowsClipboardMonitorService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
