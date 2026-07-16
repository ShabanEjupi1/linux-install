@echo off
REM KosovaPOS USB Deployment Script
REM This script prepares the application for deployment to USB

setlocal enabledelayedexpansion

cls
echo ========================================
echo KosovaPOS Deployment to USB
echo ========================================
echo.

REM Check if destination USB path is provided
if "%~1"=="" (
    echo Usage: deploy.bat D:\
    echo Example: deploy.bat D:\
    echo.
    echo This will copy the entire KosovaPOS application to the USB drive.
    exit /b 1
)

set "USB_DRIVE=%~1"
set "DEPLOY_DIR=%USB_DRIVE%KosovaPOS\"

REM Verify USB drive exists
if not exist "%USB_DRIVE%" (
    echo ERROR: USB drive %USB_DRIVE% not found!
    exit /b 1
)

echo Deploying to: %DEPLOY_DIR%
echo.

REM Create deployment directory
if not exist "%DEPLOY_DIR%" mkdir "%DEPLOY_DIR%"

REM Copy the entire solution
echo [1/5] Copying application files...
xcopy /E /I /Y "." "%DEPLOY_DIR%" ^
    /EXCLUDE:deploy-exclude.txt >nul 2>&1

if errorlevel 1 (
    echo ERROR: Failed to copy application files
    exit /b 1
)
echo Done.

REM Create installation batch file
echo [2/5] Creating installation scripts...
call :create_install_script "%DEPLOY_DIR%"
if errorlevel 1 exit /b 1

REM Create license activation script
call :create_license_script "%DEPLOY_DIR%"
if errorlevel 1 exit /b 1

REM Create README
call :create_readme "%DEPLOY_DIR%"
if errorlevel 1 exit /b 1

REM Create configuration template
echo [3/5] Creating configuration template...
call :create_config "%DEPLOY_DIR%"
if errorlevel 1 exit /b 1

REM Build release binaries
echo [4/5] Building release binaries...
cd "%USB_DRIVE%KosovaPOS\"

REM Check if we have dotnet CLI
where dotnet >nul 2>&1
if errorlevel 1 (
    echo WARNING: dotnet CLI not found. Skipping automatic build.
    echo Please build manually before distributing.
) else (
    dotnet publish -c Release -o "%DEPLOY_DIR%publish" >nul 2>&1
    if errorlevel 1 (
        echo WARNING: Build failed. Check the output directory.
    ) else (
        echo Build successful.
    )
)

echo [5/5] Creating deployment package...
echo.
echo ========================================
echo Deployment Complete!
echo ========================================
echo.
echo Location: %DEPLOY_DIR%
echo.
echo Contents:
echo   - Application source code
echo   - Installation script (Install.bat)
echo   - License activation guide (LICENSE_SETUP.txt)
echo   - Configuration template (.env.template)
echo   - README with setup instructions
echo.
echo Next steps:
echo   1. Insert USB into target PC
echo   2. Run: Install.bat
echo   3. Enter your Machine ID to get a license
echo.
echo ========================================
pause
exit /b 0

:create_install_script
setlocal
set "target_dir=%~1"
(
    echo @echo off
    echo cls
    echo echo ========================================
    echo echo KosovaPOS - Installation Setup
    echo echo ========================================
    echo echo.
    echo REM Check for administrator privileges
    echo net session >nul 2^>^&1
    echo if errorlevel 1 (
    echo     echo This script requires Administrator privileges.
    echo     echo Please right-click and select "Run as administrator"
    echo     pause
    echo     exit /b 1
    echo )
    echo.
    echo set "INSTALL_DIR=%%ProgramFiles%%\KosovaPOS"
    echo.
    echo echo [1/4] Creating installation directory...
    echo if not exist "!INSTALL_DIR!" mkdir "!INSTALL_DIR!"
    echo.
    echo echo [2/4] Copying application files...
    echo xcopy /E /I /Y "." "!INSTALL_DIR!" ^>nul 2^>^&1
    echo if errorlevel 1 (
    echo     echo ERROR: Failed to copy files
    echo     pause
    echo     exit /b 1
    echo )
    echo.
    echo echo [3/4] Creating shortcuts...
    echo powershell -Command "$desktop = [Environment]::GetFolderPath('Desktop'); if (-not (Test-Path "$desktop")) {$desktop = [Environment]::GetFolderPath('CommonDesktop')} $WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut(\"$desktop\KosovaPOS.lnk\"); $Shortcut.TargetPath = \"!INSTALL_DIR!\KosovaPOS.exe\"; $Shortcut.IconLocation = \"!INSTALL_DIR!\Resources\KosovaPOS.ico\"; $Shortcut.Save()"
    echo.
    echo echo [4/4] Initializing database...
    echo set "SQL_SERVER=^(localdb^)\\MSSQLLocalDB"
    echo set "SQL_DATABASE=BMDData"
    echo.
    echo echo.
    echo echo ========================================
    echo echo Installation Complete!
    echo echo ========================================
    echo echo.
    echo echo Application installed to: !INSTALL_DIR!
    echo echo.
    echo echo IMPORTANT - First Time Setup:
    echo echo   1. Launch KosovaPOS from your Desktop
    echo echo   2. On first run, you'll be prompted for license activation
    echo echo   3. Your Machine ID will be displayed
    echo echo   4. Send this ID to: support@kosovaBusiness.com
    echo echo   5. You'll receive a license key via email
    echo echo   6. Paste the key into the activation window
    echo echo   7. Your license is valid for 1 year
    echo echo.
    echo echo ========================================
    echo pause
) > "%target_dir%Install.bat"
endlocal
exit /b 0

:create_license_script
setlocal
set "target_dir=%~1"
(
    echo LICENSE ACTIVATION GUIDE
    echo =======================
    echo.
    echo 1. First Launch
    echo    - Launch KosovaPOS.exe from the installation directory
    echo    - On first run, the License Activation window will appear
    echo.
    echo 2. Get Your Machine ID
    echo    - Your unique Machine ID will be displayed
    echo    - Click "Copy" to copy it to clipboard
    echo.
    echo 3. Request License Key
    echo    - Send an email to: support@kosovaBusiness.com
    echo    - Include your Machine ID and name
    echo    - Subject: "KosovaPOS License Request"
    echo.
    echo 4. Activate License
    echo    - Paste the received license key
    echo    - Enter your name
    echo    - Click "Activate License"
    echo.
    echo 5. License Details
    echo    - Licenses are valid for 1 year from issuance
    echo    - License is tied to your specific hardware
    echo    - Cannot be transferred to other machines
    echo.
    echo 6. Renewal
    echo    - 30 days before expiration, you'll see a warning
    echo    - Contact support to renew your license
    echo.
) > "%target_dir%LICENSE_SETUP.txt"
endlocal
exit /b 0

:create_config
setlocal
set "target_dir=%~1"
(
    echo REM SQL Server Configuration
    echo set SQL_SERVER=^(localdb^)^\\MSSQLLocalDB
    echo set SQL_DATABASE=BMDData
    echo.
    echo REM Application Settings
    echo set FISCAL_ENABLED=true
    echo.
    echo REM You can also configure:
    echo REM - SQL_SERVER=your_server_name for remote SQL Server
    echo REM - SQL_SERVER=localhost^\\SQLEXPRESS for Express Edition
    echo REM - SQL_SERVER=your_ip_address for network SQL Server
) > "%target_dir%.env.template"
endlocal
exit /b 0

:create_readme
setlocal
set "target_dir=%~1"
(
    echo # KosovaPOS - Point of Sale System
    echo.
    echo ## System Requirements
    echo.
    echo - Windows 10 or later (64-bit)
    echo - .NET 8 Runtime (included with installer)
    echo - SQL Server 2016 or later (LocalDB, Express, or Full)
    echo - At least 2GB RAM
    echo - 500MB free disk space
    echo.
    echo ## Installation
    echo.
    echo 1. Download or copy KosovaPOS folder to target PC
    echo 2. Run Install.bat as Administrator
    echo 3. Follow on-screen instructions
    echo 4. A desktop shortcut will be created
    echo.
    echo ## First Run Setup
    echo.
    echo 1. Launch KosovaPOS from your Desktop
    echo 2. License Activation window will appear
    echo 3. Your unique Machine ID will be shown
    echo 4. Send your Machine ID to support@kosovaBusiness.com
    echo 5. Receive license key via email
    echo 6. Paste key into activation window
    echo 7. Application will initialize and create database
    echo.
    echo ## Features
    echo.
    echo - Complete Point of Sale Management
    echo - Inventory Management
    echo - Sales Reporting
    echo - User Management
    echo - Hardware Integration
    echo - Fiscal Printer Support
    echo.
    echo ## Support
    echo.
    echo Email: support@kosovaBusiness.com
    echo Website: www.kosovaBusiness.com
    echo.
    echo ## License
    echo.
    echo This software is licensed for use by authorized users only.
    echo License is valid for 1 year and requires annual renewal.
    echo.
) > "%target_dir%README.md"
endlocal
exit /b 0
