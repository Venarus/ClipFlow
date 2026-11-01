param(
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

if (Test-Path "$env:USERPROFILE\.dotnet") {
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ClipFlow - Smart Clipboard Manager Build System" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean
Write-Host ""
Write-Host "[1/4] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path ".\publish") { Remove-Item ".\publish" -Recurse -Force }
if (Test-Path ".\dist") { Remove-Item ".\dist" -Recurse -Force }

# 2. Build Solution
Write-Host ""
Write-Host "[2/4] Building Solution (Release)..." -ForegroundColor Yellow
dotnet build ClipFlow.sln -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# 3. Run Automated Tests
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "[3/4] Running Automated Tests..." -ForegroundColor Yellow
    dotnet test ClipFlow.Tests\ClipFlow.Tests.csproj -c Release --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Automated tests failed!"
        exit 1
    }
} else {
    Write-Host ""
    Write-Host "[3/4] Skipping tests..." -ForegroundColor DarkGray
}

# 4. Publish App
Write-Host ""
Write-Host "[4/4] Publishing Application (win-x64)..." -ForegroundColor Yellow
dotnet publish ClipFlow.App\ClipFlow.App.csproj -c Release -r win-x64 --self-contained false -o .\publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publishing failed!"
    exit 1
}

# 5. Compile Windows Installer
if (-not $SkipInstaller) {
    Write-Host ""
    Write-Host "[Packaging] Building Inno Setup Installer..." -ForegroundColor Yellow
    $isccPaths = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($iscc) {
        & $iscc .\installer\ClipFlow.iss
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "Installer generated successfully:" -ForegroundColor Green
            Get-Item .\dist\*.exe | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
        } else {
            Write-Warning "Inno Setup compilation returned exit code $LASTEXITCODE"
        }
    } else {
        Write-Warning "ISCC.exe not found. Install Inno Setup 6 to generate setup executable."
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Build and Packaging completed successfully!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
