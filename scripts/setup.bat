@echo off
REM JumpDock Setup Script
REM Compiles and installs JumpDock system-wide (requires Administrator)

echo ========================================
echo JumpDock Setup
echo ========================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator
    echo Right-click the file and select "Run as administrator"
    pause
    exit /b 1
)

set SCRIPT_DIR=%~dp0
set ROOT_DIR=%SCRIPT_DIR%..
set BIN_DIR=%ROOT_DIR%\bin
set SRC_DIR=%ROOT_DIR%\src
set ICONS_DIR=%ROOT_DIR%\icons

if not exist "%SRC_DIR%\JumpDock.cs" (
    echo ERROR: Source files not found in %SRC_DIR%
    echo Please ensure you have cloned the repository correctly
    pause
    exit /b 1
)

echo Step 1: Compiling JumpDock...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1"
if %errorLevel% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo Step 2: Creating installation directory...
set INSTALL_DIR=C:\Program Files\JumpDock
if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
)
if not exist "%INSTALL_DIR%\icons" (
    mkdir "%INSTALL_DIR%\icons"
)

echo Step 3: Copying files...
copy /Y "%BIN_DIR%\JumpDock.exe" "%INSTALL_DIR%\" >nul
if %errorLevel% neq 0 (
    echo ERROR: Failed to copy JumpDock.exe
    pause
    exit /b 1
)
if exist "%BIN_DIR%\JumpDockLauncher.exe" (
    copy /Y "%BIN_DIR%\JumpDockLauncher.exe" "%INSTALL_DIR%\" >nul
)
if exist "%ICONS_DIR%\" (
    xcopy /Y /Q "%ICONS_DIR%\*" "%INSTALL_DIR%\icons\" >nul
)

echo Step 4: Creating uninstall script...
echo @echo off > "%INSTALL_DIR%\uninstall.bat"
echo set INSTALL_DIR=C:\Program Files\JumpDock >> "%INSTALL_DIR%\uninstall.bat"
echo echo Uninstalling JumpDock... >> "%INSTALL_DIR%\uninstall.bat"
echo taskkill /F /IM JumpDock.exe 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo timeout /t 2 /nobreak >nul >> "%INSTALL_DIR%\uninstall.bat"
echo del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\JumpDock.lnk" 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo rd /s /q "%LOCALAPPDATA%\JumpDock" 2>nul >> "%INSTALL_DIR%\uninstall.bat"
echo cd /d "%INSTALL_DIR%" >> "%INSTALL_DIR%\uninstall.bat"
echo cd .. >> "%INSTALL_DIR%\uninstall.bat"
echo rd /s /q JumpDock >> "%INSTALL_DIR%\uninstall.bat"
echo echo JumpDock has been uninstalled >> "%INSTALL_DIR%\uninstall.bat"
echo pause >> "%INSTALL_DIR%\uninstall.bat"

echo Step 5: Creating desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%USERPROFILE%\Desktop\JumpDock.lnk'); $s.TargetPath = '%INSTALL_DIR%\JumpDock.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'JumpDock - Top-edge app dock'; $s.Save()"

echo Step 6: Creating Start Menu shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\JumpDock.lnk'); $s.TargetPath = '%INSTALL_DIR%\JumpDock.exe'; $s.WorkingDirectory = '%INSTALL_DIR%'; $s.Description = 'JumpDock - Top-edge app dock'; $s.Save()"

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo JumpDock has been installed to: %INSTALL_DIR%
echo.
echo Shortcuts created:
echo   - Desktop: JumpDock.lnk
echo   - Start Menu: JumpDock.lnk
echo.
echo Live config and tile icons live under:
echo   %%LOCALAPPDATA%%\JumpDock
echo.
echo To disable auto-start, delete JumpDock.lnk from:
echo   %%APPDATA%%\Microsoft\Windows\Start Menu\Programs\Startup
echo.
echo To uninstall, run: "%INSTALL_DIR%\uninstall.bat"
echo.
echo Starting JumpDock...
start "" "%INSTALL_DIR%\JumpDock.exe"

timeout /t 3 /nobreak >nul
