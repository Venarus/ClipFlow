using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;

namespace ClipFlow.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private AppSettings _settings;
    private readonly object _lock = new();

    public AppSettings Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
        private set
        {
            lock (_lock)
            {
                _settings = value;
            }
        }
    }

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService(string? customSettingsPath = null)
    {
        if (!string.IsNullOrEmpty(customSettingsPath))
        {
            _settingsFilePath = customSettingsPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "ClipFlow");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        _settings = new AppSettings();
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Settings = loaded;
                    SettingsChanged?.Invoke(this, Settings);
                    return;
                }
            }
        }
        catch (Exception)
        {
            // Fallback to default settings on error
        }

        Settings = new AppSettings();
        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json;
            lock (_lock)
            {
                json = JsonSerializer.Serialize(_settings, JsonOptions);
            }

            await File.WriteAllTextAsync(_settingsFilePath, json);
            SettingsChanged?.Invoke(this, Settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public void ResetToDefaults()
    {
        Settings = new AppSettings();
        _ = SaveSettingsAsync();
    }
}
