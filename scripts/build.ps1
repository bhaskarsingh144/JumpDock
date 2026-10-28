# Build JumpDock.exe (+ optional JumpDockLauncher.exe) from src/
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$outputDir = Join-Path $rootDir "bin"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc. Install .NET Framework 4.5+."
    exit 1
}

$dockSources = Get-ChildItem -Path $srcDir -Filter "*.cs" |
    Where-Object { $_.Name -ne "JumpDockLauncher.cs" } |
    ForEach-Object { $_.FullName }

if ($dockSources.Count -lt 5) {
    Write-Error "Expected dock sources under $srcDir"
    exit 1
}

$refs = @(
    "/reference:$fw\System.Windows.Forms.dll",
    "/reference:$fw\System.Drawing.dll",
    "/reference:$fw\System.Web.Extensions.dll",
    "/reference:System.dll",
    "/reference:System.Core.dll"
)

$exePath = Join-Path $outputDir "JumpDock.exe"
$iconPath = Join-Path $rootDir "icons\favicon.ico"
$win32icon = @()
if ((Test-Path $iconPath) -and ((Get-Item $iconPath).Length -lt 100KB)) {
    # Skip obviously-corrupt HTML-as-.ico leftovers; require a real ICO header.
    $hdr = [System.IO.File]::ReadAllBytes($iconPath)[0..3]
    if ($hdr[0] -eq 0 -and $hdr[1] -eq 0 -and $hdr[2] -eq 1 -and $hdr[3] -eq 0) {
        $win32icon = @("/win32icon:$iconPath")
    }
}
Write-Host "Compiling JumpDock ($($dockSources.Count) files)..." -ForegroundColor Cyan
& $csc /nologo /optimize /target:winexe /out:$exePath $win32icon $refs $dockSources
if ($LASTEXITCODE -ne 0) {
    Write-Error "JumpDock compile failed"
    exit 1
}
Write-Host "Build successful: $exePath" -ForegroundColor Green

$launcher = Join-Path $srcDir "JumpDockLauncher.cs"
if (Test-Path $launcher) {
    $launcherExe = Join-Path $outputDir "JumpDockLauncher.exe"
    Write-Host "Compiling JumpDockLauncher..." -ForegroundColor Cyan
    & $csc /nologo /optimize /target:winexe /out:$launcherExe `
        "/reference:$fw\System.Windows.Forms.dll" `
        "/reference:System.dll" `
        $launcher
    if ($LASTEXITCODE -ne 0) {
        Write-Error "JumpDockLauncher compile failed"
        exit 1
    }
    Write-Host "Build successful: $launcherExe" -ForegroundColor Green
}
