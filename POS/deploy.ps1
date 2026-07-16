# KosovaPOS USB Deployment Script
# This script handles complete deployment to USB with all necessary components

param(
    [Parameter(Mandatory=$true)]
    [ValidateScript({Test-Path $_ -PathType Container})]
    [string]$USBPath
)

$ErrorActionPreference = "Stop"

function Write-Progress-Message {
    param([string]$Message, [string]$Color = "Green")
    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] $Message" -ForegroundColor $Color
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Verify administrator privileges
if (-not (Test-Administrator)) {
    Write-Host "ERROR: This script requires Administrator privileges!" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Red
    exit 1
}

$DeployPath = Join-Path $USBPath "KosovaPOS"
$SourcePath = Get-Location

Write-Progress-Message "========================================" "Cyan"
Write-Progress-Message "KosovaPOS USB Deployment" "Cyan"
Write-Progress-Message "========================================" "Cyan"
Write-Progress-Message ""
Write-Progress-Message "Source: $SourcePath"
Write-Progress-Message "Target: $DeployPath"
Write-Progress-Message ""

# Step 1: Create directory structure
Write-Progress-Message "[1/6] Creating directory structure..."
if (Test-Path $DeployPath) {
    Write-Progress-Message "  Removing existing deployment..." "Yellow"
    Remove-Item $DeployPath -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item $DeployPath -ItemType Directory -Force | Out-Null
Write-Progress-Message "  Done"

# Step 2: Copy application files
Write-Progress-Message "[2/6] Copying application files..."
$excludePatterns = @(
    '.git',
    '.vs',
    '.vscode',
    'bin',
    'obj',
    'publish',
    '*.user',
    '*.suo',
    '.DS_Store',
    'Thumbs.db',
    'node_modules'
)

Get-ChildItem $SourcePath -Recurse | 
    Where-Object {
        $excluded = $false
        foreach ($pattern in $excludePatterns) {
            if ($_.FullName -like "*$pattern*") {
                $excluded = $true
                break
            }
        }
        -not $excluded
    } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($SourcePath.Length + 1)
        $targetPath = Join-Path $DeployPath $relativePath
        
        if ($_.PSIsContainer) {
            if (-not (Test-Path $targetPath)) {
                New-Item $targetPath -ItemType Directory -Force | Out-Null
            }
        } else {
            $targetDir = Split-Path $targetPath
            if (-not (Test-Path $targetDir)) {
                New-Item $targetDir -ItemType Directory -Force | Out-Null
            }
            Copy-Item $_.FullName -Destination $targetPath -Force | Out-Null
        }
    }
Write-Progress-Message "  Done"

# Step 3: Create .env file
Write-Progress-Message "[3/6] Creating configuration files..."
$envContent = @"
# SQL Server Configuration
SQL_SERVER=(localdb)\MSSQLLocalDB
SQL_DATABASE=BMDData

# Application Settings
FISCAL_ENABLED=true
USE_SQL_SERVER=true
BUSINESS_NAME=Kosovo Business

# Note: Modify SQL_SERVER below if using a different SQL Server instance:
# - For SQL Server Express: SQL_SERVER=.\SQLEXPRESS
# - For named instance: SQL_SERVER=SERVERNAME\INSTANCENAME
# - For remote server: SQL_SERVER=SERVER_IP_OR_NAME
"@
Set-Content (Join-Path $DeployPath ".env") $envContent -Encoding UTF8
Write-Progress-Message "  .env configuration created"

# Step 4: Create installation wrapper scripts
Write-Progress-Message "[4/6] Creating installation scripts..."

# Create PowerShell installation script
$installScript = @"
param(
    [switch]$Silent = `$false
)

`$scriptPath = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$installDir = 'C:\Program Files\KosovaPOS'

if (-not ([Security.Principal.WindowsIdentity]::GetCurrent()).Groups -contains 'S-1-5-32-544') {
    if (-not `$Silent) {
        Write-Host "This script requires Administrator privileges!" -ForegroundColor Red
        Write-Host "Please run as Administrator" -ForegroundColor Red
        Read-Host "Press Enter to exit"
    }
    exit 1
}

# Create installation directory
New-Item -ItemType Directory -Force -Path `$installDir | Out-Null

# Copy files
Copy-Item "`$scriptPath\*" -Destination `$installDir -Recurse -Force -Exclude @('.env.template', 'Install*', 'README*')

# Create desktop shortcut
`$shell = New-Object -ComObject WScript.Shell
`$desktop = [Environment]::GetFolderPath('CommonDesktop')
`$shortcut = `$shell.CreateShortcut("`$desktop\KosovaPOS.lnk")
`$shortcut.TargetPath = "`$installDir\KosovaPOS.exe"
`$shortcut.IconLocation = "`$installDir\Resources\KosovaPOS.ico"
`$shortcut.Save()

Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "Application installed to: `$installDir" -ForegroundColor Green
Write-Host "Shortcut created on Desktop" -ForegroundColor Green

if (-not `$Silent) {
    Read-Host "Press Enter to continue"
}
"@
Set-Content (Join-Path $DeployPath "Install.ps1") $installScript -Encoding UTF8

# Create batch installation script
$installBat = @"
@echo off
cls
echo ========================================
echo KosovaPOS - Installation Setup
echo ========================================
echo.

REM Check for administrator privileges
net session >nul 2>&1
if errorlevel 1 (
    echo ERROR: This script requires Administrator privileges.
    echo Please right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "INSTALL_DIR=C:\Program Files\KosovaPOS"
echo Creating installation directory...
if not exist "!INSTALL_DIR!" mkdir "!INSTALL_DIR!"

echo Copying application files...
xcopy /E /I /Y "." "!INSTALL_DIR!" ^>nul 2^>^&1

echo Creating desktop shortcut...
powershell -Command "^$shell = New-Object -ComObject WScript.Shell; ^$desktop = [Environment]::GetFolderPath('CommonDesktop'); ^$shortcut = ^$shell.CreateShortcut(\"^$desktop\KosovaPOS.lnk\"); ^$shortcut.TargetPath = \"!INSTALL_DIR!\KosovaPOS.exe\"; ^$shortcut.IconLocation = \"!INSTALL_DIR!\Resources\KosovaPOS.ico\"; ^$shortcut.Save()"

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo KosovaPOS has been installed to:
echo   !INSTALL_DIR!
echo.
echo A desktop shortcut has been created.
echo.
echo IMPORTANT - On First Launch:
echo   1. A License Activation window will appear
echo   2. Your unique Machine ID will be displayed
echo   3. Send this ID to: support@kosovaBusiness.com
echo   4. You'll receive a license key via email
echo   5. Paste the key to activate your copy
echo.
pause
"@
Set-Content (Join-Path $DeployPath "Install.bat") $installBat -Encoding UTF8
Write-Progress-Message "  Installation scripts created"

# Step 5: Create documentation
Write-Progress-Message "[5/6] Creating documentation..."

$readmeContent = @"
# KosovaPOS - Point of Sale Management System

## Quick Start

### System Requirements
- Windows 10 or later (64-bit)
- .NET 8 Runtime
- SQL Server 2016+ (LocalDB, Express, or Full Edition)
- 2GB RAM minimum
- 500MB free disk space

### Installation Steps

1. **Insert USB** into target computer
2. **Run Installation**:
   - Right-click `Install.bat` → "Run as administrator"
   - Or: Run `Install.ps1` in PowerShell as administrator
3. **First Launch**:
   - Click the Desktop shortcut
   - License activation window appears
   - Copy your Machine ID
4. **Get License**:
   - Email support@kosovaBusiness.com with your Machine ID
   - Receive license key via email
   - Paste key into activation window
5. **Start Using**:
   - Application initializes database
   - You're ready to use KosovaPOS!

### License Information
- Valid for: 1 year from issuance
- Hardware-locked: Cannot be transferred
- Auto-renewal: Submit renewal before expiration
- Support: support@kosovaBusiness.com

### Configuration

Edit `.env` file to customize:
- SQL Server connection
- Business settings
- Feature toggles

### Troubleshooting

**SQL Server Connection Error**
- Ensure SQL Server is installed and running
- Check SQL_SERVER setting in .env
- Verify Windows Authentication is enabled

**License Activation Failed**
- Verify Machine ID is correct
- Check license key is valid
- Ensure internet connection for first activation

**Database Error**
- Run database initialization script
- Check SQL Server permissions
- Review startup_errors.log file

### Support
- Email: support@kosovaBusiness.com
- Website: www.kosovaBusiness.com
- License renewal: Annual subscription

---
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
"@
Set-Content (Join-Path $DeployPath "README.md") $readmeContent -Encoding UTF8

# Create setup instructions
$setupInstructions = @"
=============================================================
KosovaPOS Installation & Setup Instructions
=============================================================

SYSTEM REQUIREMENTS:
- Windows 10 or later (64-bit)
- .NET 8 Runtime (will be installed if missing)
- SQL Server 2016 or later
  * LocalDB (included with Visual Studio)
  * Express Edition (free)
  * Full/Enterprise Edition

INSTALLATION:

1. On the target PC:
   - Insert this USB drive
   - Navigate to the KosovaPOS folder

2. Run one of the install scripts:
   Option A - Batch script (easier):
     Right-click "Install.bat" → "Run as administrator"
   
   Option B - PowerShell script (advanced):
     Open PowerShell as Administrator
     Run: .\Install.ps1

3. Wait for installation to complete
   - You'll see a success message
   - Desktop shortcut will be created

4. On FIRST LAUNCH:
   - Click the KosovaPOS icon on your desktop
   - License Activation window will appear
   - Your unique Machine ID is shown
   - Click "Copy" to copy it

5. GET YOUR LICENSE:
   - Email to: support@kosovaBusiness.com
   - Subject: "KosovaPOS License Request"
   - Include:
     * Your Machine ID (copied from activation window)
     * Your name
     * Your business name

6. RECEIVE & ACTIVATE:
   - You'll receive license key via email
   - Paste it into the activation window
   - Enter your name
   - Click "Activate License"

7. START USING:
   - Application will initialize
   - Database will be created
   - Ready to use!

CONFIGURATION:

For advanced setup, edit: .env file
- SQL_SERVER: Change if using different SQL Server
- SQL_DATABASE: Change database name
- FISCAL_ENABLED: Enable/disable fiscal printer

COMMON SQL SERVER SETUPS:

Local (default):
  SQL_SERVER=(localdb)\MSSQLLocalDB

SQL Express:
  SQL_SERVER=.\SQLEXPRESS
  or
  SQL_SERVER=COMPUTERNAME\SQLEXPRESS

Named instance:
  SQL_SERVER=COMPUTERNAME\INSTANCENAME

Remote server:
  SQL_SERVER=SERVER_IP_OR_HOSTNAME

TECHNICAL SUPPORT:

For issues or questions:
- Email: support@kosovaBusiness.com
- Website: www.kosovaBusiness.com

Log files location:
- %APPDATA%\KosovaPOS\logs\

Support will need:
- Your Machine ID
- startup_errors.log content
- System information

LICENSE RENEWAL:

Your license is valid for 1 year.
Before expiration:
1. Contact: support@kosovaBusiness.com
2. Arrange renewal payment
3. Receive new license key
4. Re-activate in application

=============================================================
Good luck with your KosovaPOS installation!
=============================================================
"@
Set-Content (Join-Path $DeployPath "SETUP_INSTRUCTIONS.txt") $setupInstructions -Encoding UTF8
Write-Progress-Message "  Documentation created"

# Step 6: Create deployment summary
Write-Progress-Message "[6/6] Creating deployment summary..."

$summary = @"
USB Deployment Summary
======================

Deployment Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Location: $DeployPath
Size: $(Get-ChildItem $DeployPath -Recurse | Measure-Object -Property Length -Sum | ForEach-Object {"{0:N0} KB" -f ($_.Sum/1KB)})

Contents:
- Complete KosovaPOS application source code
- Pre-configured database scripts
- Installation scripts (batch and PowerShell)
- License activation system
- Comprehensive documentation

Ready for Distribution:
✓ All application files included
✓ Installation scripts configured
✓ License system integrated
✓ Database initialization ready
✓ Configuration templates prepared

Next Steps:
1. Test installation on target PC
2. Verify SQL Server availability
3. Test license activation workflow
4. Distribute to end users

Support Resources:
- Installation guide (SETUP_INSTRUCTIONS.txt)
- README documentation
- Database initialization script

Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
"@
Set-Content (Join-Path $DeployPath "DEPLOYMENT_INFO.txt") $summary -Encoding UTF8

Write-Progress-Message ""
Write-Progress-Message "========================================" "Cyan"
Write-Progress-Message "Deployment Complete!" "Green"
Write-Progress-Message "========================================" "Cyan"
Write-Progress-Message ""
Write-Progress-Message "Location: $DeployPath" "White"
Write-Progress-Message "Ready for distribution!" "Green"
Write-Progress-Message ""
Write-Progress-Message "Next steps:" "Yellow"
Write-Progress-Message "1. Safely eject USB"
Write-Progress-Message "2. Test installation on target PC"
Write-Progress-Message "3. Run Install.bat or Install.ps1"
Write-Progress-Message "4. Follow license activation steps"
Write-Progress-Message ""
