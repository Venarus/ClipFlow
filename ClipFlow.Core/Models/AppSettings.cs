using System.Collections.Generic;

namespace ClipFlow.Core.Models;

public class AppSettings
{
    // Appearance
    public string Theme { get; set; } = "Dark"; // "Dark", "Light", "System"
    public string BackdropMaterial { get; set; } = "Acrylic"; // "Mica", "Acrylic", "MicaAlt", "Glass", "Solid"
    public double BackgroundOpacity { get; set; } = 0.88;
    public double BlurIntensity { get; set; } = 25.0;
    public string AccentColor { get; set; } = "#3B82F6"; // Modern Windows 11 Blue
    public bool CompactView { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = false;

    // Hotkeys
    public string ToggleWindowHotkey { get; set; } = "Win+Alt+V";
    public string QuickSearchHotkey { get; set; } = "Win+Alt+F";
    public string ClearHistoryHotkey { get; set; } = "Win+Alt+C";
    public string ToggleMonitoringHotkey { get; set; } = "Win+Alt+M";

    // General & Behavior
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool PlaySoundOnCopy { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool AutoPasteOnSelection { get; set; } = false;

    // Privacy & Security
    public bool IsMonitoringEnabled { get; set; } = true;
    public bool FilterSensitiveContent { get; set; } = true;
    public bool IgnorePasswordManagerFormats { get; set; } = true;
    public List<string> ExcludedApplications { get; set; } =
    [
        "keepass.exe",
        "keepassxc.exe",
        "1password.exe",
        "bitwarden.exe",
        "lastpass.exe",
        "enpass.exe",
        "dashlane.exe",
        "mstsc.exe",
        "credentialui.exe",
        "identityservices.exe"
    ];
    public bool StoreImages { get; set; } = true;
    public int MaxImageSizeMb { get; set; } = 15;

    // Storage & Limits
    public int MaxHistoryItems { get; set; } = 1000;
    public int AutoCleanupDays { get; set; } = 30; // 0 = never
    public string? CustomDatabasePath { get; set; }
}
