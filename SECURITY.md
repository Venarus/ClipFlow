# Security Policy

## 🔒 Security & Privacy Guarantees

ClipFlow is designed with **privacy as a non-negotiable core tenet**. Because a clipboard manager handles everything copied to the operating system's clipboard, security and isolation are built into every architectural layer.

### 1. Zero External Data Transmission
- **No Telemetry:** ClipFlow contains zero tracking SDKs, analytical collectors, or diagnostic pings.
- **No Cloud Services:** All data remains strictly on your local machine (`%LOCALAPPDATA%\ClipFlow\clipflow.db`).
- **No Advertising or Remote Code Execution:** ClipFlow never downloads remote scripts, plugins, or webviews.

### 2. Password Manager Protection
ClipFlow actively queries standard Windows clipboard metadata formats used by password managers to flag ephemeral and sensitive entries:
- `Clipboard Viewer Ignore`
- `ExcludeClipboardContentFromMonitor`
- `CanIncludeInClipboardHistory`
- `CanUploadToCloudClipboard`

When any of these formats are detected on the clipboard, **the content is immediately discarded without inspection or logging**.

### 3. Application Exclusion List
ClipFlow maintains a configurable application exclusion list. Clipboard events originating from any process in this list are ignored:
- `keepass.exe` / `keepassxc.exe`
- `1password.exe`
- `bitwarden.exe`
- `lastpass.exe`
- `enpass.exe`
- `dashlane.exe`
- `mstsc.exe` (Windows Remote Desktop)
- `credentialui.exe` (Windows Security Prompt)

You can add or modify excluded applications at any time from the ClipFlow Settings window.

### 4. Sensitive Content Regex Filtering
Before saving any text to the database, ClipFlow passes the payload through a pre-compiled regex filter to detect and reject:
- **Private Keys:** `-----BEGIN [A-Z0-9\s_-]+PRIVATE KEY-----`
- **JSON Web Tokens (JWT):** `eyJh...`
- **API Keys & Tokens:** GitHub Personal Access Tokens (`ghp_`, `gho_`), AWS Access Keys (`AKIA...`), Google API Keys (`AIza...`), Slack tokens, Stripe live keys (`sk_live_...`).
- **Credential Lines:** Common `password=...`, `api_key=...`, `secret=...` patterns.
- **Credit Card Candidates:** 15-16 digit sequences matching payment card formats.

### 5. Local Storage Protection
- The SQLite database is stored locally inside the user's isolated Windows profile directory.
- Database Write-Ahead Logging (`WAL`) ensures ACID transactional durability without file corruption.
- Users can clear history instantly via `Win + Alt + C`, the system tray menu, or configure automatic history expiration (e.g., 7, 30, or 90 days).

---

## 🛡️ Reporting a Vulnerability

If you discover a potential security vulnerability, credential leak, or unintended clipboard retention in ClipFlow:

1. **Do not create a public GitHub issue.**
2. Send a report directly via GitHub Security Advisories or email the maintainers at **security@venarus.dev** (or open a confidential draft advisory on the repository).
3. Please include:
   - Detailed description of the vulnerability.
   - Operating system version (e.g. Windows 11 23H2).
   - Minimal reproduction steps or sample payload.
   - Expected vs observed behavior.

We take security reports seriously and will acknowledge receipt within 48 hours and work with you on a coordinated disclosure.
