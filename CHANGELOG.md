# Changelog

All notable changes to the **ClipFlow** project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-11-01

### Added
- **Native Windows 11 Fluent Interface:**
  - Real Windows 11 Acrylic and Mica backdrop materials via DWM APIs (`DwmSetWindowAttribute`).
  - Immersive dark mode with rounded corners (`DWMWCP_ROUND`).
  - Customizable transparency and blur controls.
- **Genuine Win32 Clipboard Engine:**
  - Event-driven monitoring via `AddClipboardFormatListener` and `WM_CLIPBOARDUPDATE`.
  - Zero-polling architecture for 0% CPU consumption during idle.
  - Automatic clipboard access conflict recovery with exponential backoff.
  - Multi-format support: Plain Text, HTML, RTF, Images (DIB/PNG), and File Drops (`HDROP`).
  - Rapid duplicate suppression within 800ms window.
- **Smart Content Recognition:**
  - Instant classification of HEX and RGB colors with live visual swatch previews.
  - URL and domain parsing with direct default browser invocation.
  - Code snippet recognition with programming language detection (C#, JavaScript, Python, SQL, Rust, HTML, CSS, JSON).
  - File drop classification with native Windows file icons, extension badges, and size formatting.
  - Email address and phone number detection.
- **Rich Visual Thumbnail Tiles:**
  - 46x46 high-contrast visual tiles for every clipboard entry.
  - High-resolution thumbnails for copied screenshots and image files.
  - Actual Windows associated file icons for documents (`.pdf`, `.docx`, `.zip`, `.exe`, etc.).
  - Mini document text lines card representation for plain text.
- **Linux / GNOME Fluid Micro-Animations:**
  - Smooth card hover scaling (`ScaleTransform 1.01`) and rightwards translation (`TranslateTransform X=3.5px`) with `CubicEaseOut`.
  - Smooth sidebar selection pill animation (`Height 0 -> 20px`, `Opacity 0 -> 1`).
  - Tactile selection and press feedback.
  - Spring-bounce toast notifications with `BackEase` physics.
- **Privacy & Security Shield:**
  - 100% Local SQLite WAL storage with zero telemetry, zero analytics, and zero cloud sync.
  - Native password manager ignore format detection (`Clipboard Viewer Ignore`, `ExcludeClipboardContentFromMonitor`).
  - Automatic detection and exclusion of KeePass, 1Password, Bitwarden, LastPass, Enpass, Dashlane, and Remote Desktop (`mstsc.exe`).
  - Sensitive token regex scanner protecting JWTs, private keys, API tokens (`sk_live`, `ghp`, `AKIA`, `AIza`), passwords, and credit cards.
- **System Tray & Global Hotkeys:**
  - Pure Win32 `Shell_NotifyIcon` integration with WPF ContextMenu.
  - System-wide customizable hotkeys:
    - `Win + Alt + V`: Toggle ClipFlow window
    - `Win + Alt + F`: Focus search bar
    - `Win + Alt + M`: Pause / Resume monitoring
    - `Win + Alt + C`: Clear clipboard history
- **Storage & Performance Tuning:**
  - SQLite Write-Ahead Logging (`WAL`), `PRAGMA temp_store = MEMORY`, `cache_size = -4000`, and `mmap_size = 268435456`.
  - Working set trimmer (`EmptyWorkingSet`) invoked automatically upon tray minimization (reducing RAM footprint to < 35 MB).
  - Virtualized WPF recycling canvas with pixel-based smooth scrolling.
- **Self-Contained Installer:**
  - Bundled .NET 9 desktop runtime eliminating external runtime installation requirements.
  - Professional Inno Setup installer (`dist/ClipFlow-Setup-v1.0.0.exe`) with desktop shortcut and uninstaller.
  - Automated PowerShell build and test script (`build.ps1`).

### Fixed
- Resolved WPF `EqualityToBooleanConverter.ConvertBack` unhandled exception when switching sidebar categories.
- Resolved process termination on window hide by setting `Application.ShutdownMode` to `OnExplicitShutdown`.
- Resolved icon GDI+ handle premature disposal in system tray notification loop.
