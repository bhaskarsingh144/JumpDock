# Run JumpDock unit tests (no NuGet).
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$srcDir = Join-Path $rootDir "src"
$testsDir = Join-Path $rootDir "tests"
$outputDir = Join-Path $rootDir "bin"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at $csc"
    exit 1
}

function Run-Suite([string]$name, [string[]]$sources, [string]$exeName, [string[]]$extraRefs) {
    foreach ($f in $sources) {
        if (-not (Test-Path $f)) {
            Write-Error "Missing source: $f"
            exit 1
        }
    }
    $exePath = Join-Path $outputDir $exeName
    Write-Host "Compiling $name..." -ForegroundColor Cyan
    $args = @("/nologo", "/optimize", "/target:exe", "/out:$exePath")
    if ($extraRefs) { $args += $extraRefs }
    $args += $sources
    & $csc @args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$name compile failed"
        exit 1
    }
    Write-Host "Running $exePath" -ForegroundColor Cyan
    & $exePath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$name failed (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "$name passed." -ForegroundColor Green
}

$refs = @(
    "/reference:$fw\System.Web.Extensions.dll",
    "/reference:$fw\System.Windows.Forms.dll",
    "/reference:$fw\System.Drawing.dll",
    "/reference:System.dll",
    "/reference:System.Core.dll"
)

Run-Suite "JumpListsTests" @(
    (Join-Path $srcDir "JumpLists.cs"),
    (Join-Path $testsDir "JumpListsTests.cs")
) "JumpListsTests.exe" @("/reference:System.dll")

Run-Suite "JumpListStoreTests" @(
    (Join-Path $srcDir "JumpListStore.cs"),
    (Join-Path $srcDir "JumpLists.cs"),
    (Join-Path $srcDir "JumpPaths.cs"),
    (Join-Path $srcDir "ChromeAppUrl.cs"),
    (Join-Path $srcDir "JumpLaunch.cs"),
    (Join-Path $testsDir "JumpListStoreTests.cs")
) "JumpListStoreTests.exe" $refs

$refreshSrc = @(
    (Join-Path $srcDir "JumpLists.cs"),
    (Join-Path $srcDir "JumpListRefresh.cs"),
    (Join-Path $srcDir "JumpListStore.cs"),
    (Join-Path $srcDir "JumpPaths.cs"),
    (Join-Path $srcDir "WindowsJumpList.cs"),
    (Join-Path $srcDir "AppRecents.cs"),
    (Join-Path $srcDir "ChromeAppUrl.cs"),
    (Join-Path $srcDir "ChromeHistory.cs"),
    (Join-Path $srcDir "JumpLaunch.cs"),
    (Join-Path $srcDir "JumpProviders.cs"),
    (Join-Path $srcDir "DockExt.cs"),
    (Join-Path $srcDir "DockAccent.cs"),
    (Join-Path $testsDir "JumpListRefreshTests.cs")
)
Run-Suite "JumpListRefreshTests" $refreshSrc "JumpListRefreshTests.exe" $refs

Run-Suite "DisplayScaleTests" @(
    (Join-Path $srcDir "DisplayScale.cs"),
    (Join-Path $testsDir "DisplayScaleTests.cs")
) "DisplayScaleTests.exe" $refs

Run-Suite "DockLayoutScaleTests" @(
    (Join-Path $srcDir "DisplayScale.cs"),
    (Join-Path $srcDir "DockLayout.cs"),
    (Join-Path $testsDir "DockLayoutScaleTests.cs")
) "DockLayoutScaleTests.exe" $refs

Write-Host "All test suites passed." -ForegroundColor Green
exit 0
