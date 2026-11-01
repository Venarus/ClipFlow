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
        // Setup file logging immediately
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = System.IO.Path.Combine(appData, "ClipFlow");
        System.IO.Directory.CreateDirectory(logDir);
        var logFile = System.IO.Path.Combine(logDir, "startup.log");

        void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            try
            {
                System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        Log("App starting up...");

        // Global exception logging
        DispatcherUnhandledException += (s, args) =>
        {
            Log($"Unhandled UI Exception: {args.Exception.Message}\n{args.Exception.StackTrace}");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log($"Critical Unhandled Exception: {args.ExceptionObject}");
        };

        try
        {
            // Enforce single instance application
            const string mutexName = "ClipFlow_SingleInstance_Mutex_Global";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                Log("Another instance is already running. Shutting down.");
                System.Windows.MessageBox.Show(
                    "ClipFlow is already running in the background. Check your Windows system tray.",
                    "ClipFlow",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            Log("Mutex acquired. Base on startup...");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            Log("Configuring services...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            Log("Resolving MainWindow...");
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            Log("Ensuring HWND...");
            var helper = new WindowInteropHelper(mainWindow);
            helper.EnsureHandle();

            bool startMinimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase) ||
                                  mainWindow.ViewModel.Settings.StartMinimized;

            Log($"Showing window (startMinimized={startMinimized})...");
            if (startMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.Hide();
            }
            else
            {
                mainWindow.Show();
            }
            Log("Startup complete. Running message pump.");
        }
        catch (Exception ex)
        {
            Log($"FATAL error during startup: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            System.Windows.MessageBox.Show($"ClipFlow encountered a startup error:\n{ex.Message}", "ClipFlow Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
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
