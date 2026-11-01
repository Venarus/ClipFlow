# Contributing to ClipFlow

Thank you for your interest in contributing to **ClipFlow — Smart Clipboard Manager**!

This document outlines our development guidelines, code standards, and the pull request process to ensure that ClipFlow remains secure, maintainable, and lightweight.

---

## 🛠️ Development Environment

### Prerequisites
- **Operating System:** Windows 11 (22H2 or newer recommended) or Windows 10 (Build 19041+)
- **SDK:** [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (v9.0.200 or newer)
- **IDE:** Visual Studio 2022 (v17.12+) or JetBrains Rider or VS Code with C# DevKit
- **Packaging (Optional):** [Inno Setup 6](https://jrsoftware.org/isdl.php) (for compiling the installer)

### Getting the Code
```powershell
git clone https://github.com/Venarus/ClipFlow.git
cd ClipFlow
```

### Building the Project
```powershell
# Restore & build solution in Release mode
dotnet build ClipFlow.sln -c Release

# Or run the complete build and verification script
.\build.ps1
```

### Running Automated Tests
```powershell
dotnet test ClipFlow.Tests\ClipFlow.Tests.csproj -c Release
```
All 49+ tests must pass before submitting any pull request.

---

## 📐 Architecture & Principles

ClipFlow is engineered as a clean 3-layer architecture:

1. **`ClipFlow.Core`**:
   - Zero external dependencies.
   - Houses domain models (`ClipboardItem`, `AppSettings`, `SmartDetectionResult`), domain interfaces, and static utilities (`HashUtilities`).
2. **`ClipFlow.Infrastructure`**:
   - Manages Win32 API clipboard notifications (`AddClipboardFormatListener`, `WM_CLIPBOARDUPDATE`), global hotkeys (`RegisterHotKey`), and SQLite storage.
   - Enforces privacy protections (password manager format markers, credential regex filtering).
3. **`ClipFlow.App`**:
   - WPF presentation layer using **CommunityToolkit.Mvvm**.
   - Applies Windows 11 Fluent Design (DWM Mica/Acrylic backdrop, immersive dark mode) and Linux-style fluid card micro-animations.

### Key Rules
- **Privacy First:** Never transmit clipboard content to external services or cloud APIs. Zero telemetry.
- **Performance:** Ensure clipboard monitoring uses event-driven Win32 messages rather than polling. Minimize UI thread allocations.
- **Native Look & Feel:** Preserve Fluent design principles and high-contrast accessibility.

---

## 🌿 Branching Model

Please create feature branches from `master`:
- `feature/your-feature-name` (e.g. `feature/rich-text-highlighter`)
- `bugfix/issue-description` (e.g. `bugfix/tray-icon-transparency`)
- `docs/topic-name` (e.g. `docs/add-api-architecture`)
- `perf/optimization-target` (e.g. `perf/sqlite-mmap-tuning`)

---

## 💬 Commit Message Convention

ClipFlow follows [Conventional Commits](https://www.conventionalcommits.org/):

| Type | Purpose | Example |
| :--- | :--- | :--- |
| `feat` | New user-facing feature | `feat(search): add instant tag-based filtering` |
| `fix` | Bug fix | `fix(sidebar): resolve radio button two-way binding failure` |
| `perf` | Performance optimization | `perf(storage): enable SQLite memory temp_store and WAL checkpointing` |
| `refactor` | Code restructuring without behavior change | `refactor(recognizer): consolidate regex token classifiers` |
| `docs` | Documentation updates | `docs(readme): add Linux fluid animation diagrams` |
| `style` | Formatting or styling adjustments | `style(itemcard): enhance hover scale and glow transition` |
| `test` | Adding or updating tests | `test(privacy): add Bitwarden and KeePass exclusion test cases` |
| `chore` | Build scripts, dependencies, or tool configs | `chore(ci): update GitHub Actions workflow to .NET 9` |

---

## 📋 Pull Request Process

1. **Fork & Branch:** Create a dedicated branch for your change.
2. **Coding Standards:**
   - Adhere to C# 13 and .NET 9 coding conventions.
   - Use file-scoped namespaces (`namespace ClipFlow.App;`).
   - Enable nullable reference types (`#nullable enable`).
3. **Write Tests:** Add unit tests for any new business logic, smart recognizers, or storage features under `ClipFlow.Tests`.
4. **Run Verification:**
   ```powershell
   dotnet test ClipFlow.Tests\ClipFlow.Tests.csproj -c Release
   ```
5. **Open Pull Request:** Provide a clear summary of changes, motivation, and screenshots/GIFs for UI changes.

---

## 🛡️ Reporting Security Vulnerabilities

If you discover a security vulnerability or sensitive data leakage in ClipFlow, please review our [SECURITY.md](SECURITY.md) for reporting procedures. Do not open public issues for sensitive vulnerabilities.
