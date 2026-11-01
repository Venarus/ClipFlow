# ClipFlow — Smart Clipboard Manager for Windows 11

[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20%7C%20Windows%2010-0078D4?logo=windows)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-9.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20MVVM-success)](#architecture)
[![Privacy](https://img.shields.io/badge/Privacy-100%25%20Local%20%7C%20Zero%20Telemetry-10B981)](#privacy--security)
[![Tests](https://img.shields.io/badge/Tests-49%2F49%20Passing-brightgreen)](#automated-testing)

**ClipFlow** is a lightweight, elegant, and intelligent clipboard manager built natively for Windows 11. It continuously monitors system clipboard changes with zero polling overhead, organizes copied items in a local SQLite database, performs smart content recognition (colors, code, URLs, dates, phone numbers, file drops), and provides an instant searchable history through a floating glassmorphic interface with true Windows 11 Acrylic and Mica backdrop materials.

---

## ✨ Key Features

### 1. 🪟 Windows 11 Fluent Design & Glassmorphism
- **Floating Glass Panel Aesthetic**: Borderless translucent window with hardware-accelerated Windows 11 DWM backdrop effects (Acrylic, Mica, Mica Alt).
- **Graceful Fallback**: Automatically provides high-performance translucent acrylic brush rendering on older Windows builds.
- **Fluent Typography & Icons**: Crisp vector iconography, smooth hover transitions, and rounded corners (`DWMWCP_ROUND`).
- **Dark & Light Themes**: Dark theme enabled by default, matching the modern Windows 11 Shell.
- **Customizable Opacity & Blur**: Adjust background transparency (50% – 100%) and materials in real time.

### 2. ⚡ Real Windows Clipboard Engine
- **Event-Driven Non-Polling Architecture**: Uses native Win32 `AddClipboardFormatListener` / `RemoveClipboardFormatListener` to receive `WM_CLIPBOARDUPDATE` messages from the OS with 0% idle CPU usage.
- **Multi-Format Support**:
  - Plain Text (`CF_UNICODETEXT`, `CF_TEXT`)
  - Rich Text & HTML (`CF_RTF`, `HTML Format`)
  - Images (`CF_DIB`, `CF_DIBV5`, `Bitmap`) with automatic disk caching and instant thumbnail generation
  - Files (`CF_HDROP`, FileDrop lists)
- **Duplicate Prevention & Bumping**: Duplicate copies are detected via SHA-256 content hashing. Copying identical content updates its `LastUsedAt` timestamp and increments its `UseCount` without cluttering history with duplicate rows.
- **Self-Copy Loop Suppression**: Avoids recursive copy loops when writing history items back into the clipboard.
- **Safe Concurrency & Retry**: Handles momentary clipboard locks by other applications with exponential backoff.

### 3. 🧠 Smart Content Recognition
ClipFlow automatically analyzes copied content and generates contextual previews:
- **HEX & RGB/RGBA Colors**: Recognizes `#RGB`, `#RRGGBB`, `#AARRGGBB`, `rgb(...)`, and `rgba(...)`. Displays live interactive color swatches with 1-click HEX/RGB copy.
- **URLs & Links**: Detects web protocols, validates URIs, extracts domain names, and provides a 1-click "Open in Default Browser" action.
- **Code Snippets**: Automatically detects JSON, SQL, C#, Python, JavaScript/TypeScript, HTML/XML, and CSS. Displays with monospace font and line numbers.
- **Email Addresses & Phone Numbers**: Identifies contact formats with formatted badges.
- **File Drops & Windows Paths**: Displays file extensions, formatted file sizes, and 1-click "Show in File Explorer" button.
- **Safe Execution Guarantee**: Recognition **never** automatically executes commands, scripts, or links.

### 4. 🔒 Privacy & Security (Zero-Telemetry)
- **100% Local Storage**: Everything is stored locally on your device in `%LocalAppData%\ClipFlow\clipflow.db`.
- **Zero External Telemetry**: No cloud sync required, no network requests, no analytics, no ads.
- **Password Manager Protection**: Checks Windows clipboard format flags for `Clipboard Viewer Ignore`, `ExcludeClipboardContentFromMonitor`, `CanIncludeInClipboardHistory`, and `CanUploadToCloudClipboard` (used by KeePass, 1Password, Bitwarden, LastPass). Items marked with these flags are instantly discarded.
- **Process Exclusion List**: Automatically identifies the foreground application (`GetForegroundWindow` -> `QueryFullProcessImageName`). If the source is in the exclusion list (e.g. `keepass.exe`, `bitwarden.exe`, `mstsc.exe`), the copy is ignored.
- **Sensitive Data Detection**: Scans and blocks storage of detected private keys (`-----BEGIN PRIVATE KEY-----`), JWT tokens, API keys (`sk_live_...`, `AKIA...`, `ghp_...`), and credit card numbers (validated via the Luhn algorithm).
- **Temporary Image Privacy**: Stored in a private AppData directory and wiped cleanly during history deletion.

### 5. 🔍 Fast Search & Organization
- **Instant Debounced Search**: Fast indexed search across content, source application name, window title, category, and tags.
- **Timeframe Filtering**: Filter by Today, Yesterday, This Week, This Month, or All.
- **Content Type Filter**: View All Items, Text, Images, Links, Files, or Colors with a single click.
- **Favorites & Pinning**: Pin crucial items so they always remain at the top and are preserved during history cleanup.
- **Multi-Select & Bulk Actions**: Select multiple items for bulk deletion or bulk favoriting.

### 6. ⌨️ Global Hotkeys & System Tray
- **Global Shortcut**: Press `Win+Alt+V` from any application to summon ClipFlow.
- **System Tray Agent**: Runs quietly in the Windows system tray with quick actions (Pause/Resume Monitoring, Clear History, Settings, Exit).
- **Run at Windows Startup**: Configurable via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

---

## 🏛️ Architecture & Project Structure

The solution follows Clean Architecture principles and MVVM:

```
ClipFlow/
├── ClipFlow.sln                          # Master Solution file
├── build.ps1                             # End-to-end build, test, and package script
├── dist/
│   └── ClipFlow-Setup-v1.0.0.exe         # Production Windows Installer & Uninstaller
├── publish/                              # Standalone release binaries
│   ├── ClipFlow.exe
│   └── ...
├── ClipFlow.Core/                        # Domain Models & Interfaces (Platform Agnostic)
│   ├── Models/
│   │   ├── ClipboardItem.cs              # Core clipboard entry entity
│   │   ├── Enums.cs                      # ContentType, SmartType, DateFilter, SortOption
│   │   ├── SmartDetectionResult.cs       # Metadata & recognition payload
│   │   ├── AppSettings.cs                # Configuration state model
│   │   └── FilterCriteria.cs             # Search, sort, and pagination filters
│   ├── Interfaces/
│   │   ├── IClipboardStorageRepository.cs
│   │   ├── IClipboardMonitorService.cs
│   │   ├── ISmartContentRecognizer.cs
│   │   ├── ISecurityService.cs
│   │   ├── ISettingsService.cs
│   │   ├── IStartupManager.cs
│   │   └── IHotkeyService.cs
│   └── Utilities/
│       └── HashUtilities.cs              # SHA256 & file formatting
├── ClipFlow.Infrastructure/              # Windows Native Interop & Storage
│   ├── Native/
│   │   └── NativeMethods.cs              # Win32 user32, dwmapi, kernel32 P/Invoke APIs
│   ├── Storage/
│   │   └── SqliteClipboardRepository.cs  # SQLite WAL repository with migration & cleanup
│   └── Services/
│       ├── SmartContentRecognizer.cs     # Color, code, URL, email regex parser
│       ├── SecurityService.cs            # Password manager & sensitive data filters
│       ├── WindowsClipboardMonitorService.cs # STA clipboard listener & format reader
│       ├── HotkeyService.cs              # Global hotkey registration & message routing
│       ├── StartupManager.cs             # Windows Registry Run key manager
│       └── SettingsService.cs            # JSON configuration persistence
├── ClipFlow.App/                         # Presentation Layer (WPF / Windows 11 Fluent)
│   ├── App.xaml / App.xaml.cs            # Mutex single-instance & DI bootstrap
│   ├── MainWindow.xaml / .cs             # Glassmorphic window & Win32 DWM backdrop
│   ├── ViewModels/
│   │   ├── MainViewModel.cs              # CommunityToolkit.Mvvm orchestrator
│   │   └── ClipboardItemViewModel.cs     # Card item wrapper with lazy thumbnails
│   ├── Converters/
│   │   └── Converters.cs                 # XAML data converters
│   ├── Styles/
│   │   ├── Icons.xaml                    # Windows 11 Fluent vector geometries
│   │   └── FluentStyles.xaml             # Acrylic brushes, card templates, controls
│   ├── Services/
│   │   └── TrayIconManager.cs            # Shell_NotifyIcon system tray wrapper
│   └── Assets/
│       ├── app-icon.ico                  # Multi-resolution icon (16, 32, 48, 64, 128, 256)
│       └── app-icon.png                  # High-res 256x256 PNG asset
├── ClipFlow.Tests/                       # Automated Test Suite (xUnit)
│   ├── DatabaseStorageTests.cs           # CRUD, duplicates, cleanup, vacuum, backup, large scale
│   ├── SmartContentRecognizerTests.cs    # Colors, URLs, emails, dates, phones, code detection
│   ├── SecurityAndPrivacyTests.cs        # Excluded apps, tokens, private keys, Luhn cards
│   ├── SettingsAndHotkeyTests.cs         # Serialization and hotkey parsing
│   └── LiveClipboardIntegrationTests.cs  # End-to-end capture and storage pipeline
└── installer/
    └── ClipFlow.iss                      # Inno Setup 6 packaging configuration
```

---

## 🧪 Automated Testing

ClipFlow includes 49 automated unit and integration tests executing across all layers:

| Test Suite | Tests | Description |
|:---|:---:|:---|
| **DatabaseStorageTests** | 8 | SQLite database initialization, WAL mode, CRUD, duplicate bumping, favorites, pinning, auto-cleanup, backup/restore, and large-history performance (1,000 items query benchmark). |
| **SmartContentRecognizerTests** | 10 | Recognition of URLs, domain parsing, emails, hex colors, RGB/RGBA colors, phone numbers, JSON, SQL, C#, and text word/char metrics. |
| **SecurityAndPrivacyTests** | 16 | Application exclusion matching, password manager format detection (`Clipboard Viewer Ignore`), private key detection, JWT detection, API key detection, and Luhn credit card validation. |
| **SettingsAndHotkeyTests** | 6 | Settings serialization, roundtrip persistence, hotkey parsing (`Win+Alt+V`, `Ctrl+Shift+V`, etc.). |
| **LiveClipboardIntegrationTests** | 2 | End-to-end data pipeline from capture to recognition and persistence. |

To run all automated tests:
```powershell
dotnet test
```

---

## 🚀 Getting Started

### Prerequisites
- **Windows 10 (version 19041+)** or **Windows 11 (recommended)**
- **.NET 9.0 Desktop Runtime** (or higher)

### Quick Run from Source
1. Clone the repository:
   ```bash
   git clone https://github.com/ClipFlow/ClipFlow.git
   cd ClipFlow
   ```
2. Build and run the application:
   ```powershell
   dotnet run --project ClipFlow.App\ClipFlow.App.csproj
   ```

### Building the Release & Windows Installer
Run the automated build script:
```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```
The output installer will be generated at:
```
dist\ClipFlow-Setup-v1.0.0.exe
```

---

## 📦 Windows Installer & Uninstaller

The provided installer (`ClipFlow-Setup-v1.0.0.exe`):
- Installs ClipFlow to `%LocalAppData%\Programs\ClipFlow` (no administrator privileges required).
- Creates Start Menu and optional Desktop shortcuts.
- Configures optional startup with Windows.
- Registers an uninstaller in Windows **Settings > Apps > Installed apps**, allowing one-click clean uninstallation (`unins000.exe`).

---

## ⌨️ Default Keyboard Shortcuts

| Shortcut | Action | Configurable |
|:---|:---|:---:|
| `Win + Alt + V` | Open / Toggle ClipFlow Window | Yes |
| `Win + Alt + C` | Quick Clear Clipboard History | Yes |
| `Win + Alt + M` | Toggle Monitoring (Pause / Resume) | Yes |
| `Delete` | Delete Selected Item | Yes |
| `Enter` / Double Click | Copy Selected Item to Clipboard | Built-in |
| `Esc` | Hide / Minimize to Tray | Built-in |

---

## 🛡️ License

This project is licensed under the MIT License.
