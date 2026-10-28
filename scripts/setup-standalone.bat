@echo off
REM JumpDock Standalone Setup — build and run from this tree (no Program Files).

echo ========================================
echo JumpDock Standalone Setup
echo ========================================
echo.

set SCRIPT_DIR=%~dp0
set ROOT_DIR=%SCRIPT_DIR%..
set BIN_DIR=%ROOT_DIR%\bin

if not exist "%ROOT_DIR%\src\JumpDock.cs" (
    echo ERROR: Source files not found in %ROOT_DIR%\src
    pause
    exit /b 1
)

echo Step 1: Compiling...
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1"
if %errorLevel% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo Step 2: Creating run.bat...
echo @echo off > "%ROOT_DIR%\run.bat"
echo cd /d "%ROOT_DIR%" >> "%ROOT_DIR%\run.bat"
echo bin\JumpDock.exe >> "%ROOT_DIR%\run.bat"

echo.
echo ========================================
echo Setup Complete
echo ========================================
echo.
echo Built: %BIN_DIR%\JumpDock.exe
echo.
echo Live machine config (dock.json / jumplists.json / icons) lives under
echo %%LOCALAPPDATA%%\JumpDock. Bundled icons\ seed missing tile artwork on start.
echo.
echo The application may auto-start on Windows login after first run.
echo To disable auto-start, delete JumpDock.lnk from:
echo   %%APPDATA%%\Microsoft\Windows\Start Menu\Programs\Startup
echo.
echo Starting JumpDock...
start "" "%BIN_DIR%\JumpDock.exe"
timeout /t 2 /nobreak >nul
