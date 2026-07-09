#Requires -Version 5.1
<#
.SYNOPSIS
    Installs (or upgrades) the KosovaPOS Hardware Agent as a Windows service.

.DESCRIPTION
    The agent is what lets the web POS at pos.spacecode.tech print fiscal receipts:
    the browser on this PC fetches http://127.0.0.1:9099, and the agent hands the
    payload to F-Link, the receipt printer, the label printer and the scale.

    Run this from an ELEVATED PowerShell, in the folder that holds KosovaPOS.Agent.exe:

        .\install-agent.ps1

    Re-running it upgrades in place (stops the service, swaps the exe, restarts).

.PARAMETER Mock
    Install with mock drivers. Nothing is sent to real hardware, but the POS badge
    turns amber and the whole path is exercised. Use this to validate the install
    before the F-Link licence is applied, then re-run without -Mock.

.EXAMPLE
    .\install-agent.ps1 -ReceiptPrinter "POS-80" -FiscalTempPath "C:\TEMP\"

.EXAMPLE
    .\install-agent.ps1 -Mock          # dry run, no hardware touched
#>
[CmdletBinding()]
param(
    [string]$PosUrl         = 'https://pos.spacecode.tech',
    [int]   $Port           = 9099,

    # Fiscal (F-Link) transport
    [string]$FiscalTempPath = 'C:\TEMP\',
    [string]$FiscalComPort  = 'COM1',
    [string]$FiscalModel    = 'FP700+',
    [string]$FiscalNumber   = '003910',

    # Windows printer names. Empty = system default printer / feature unused.
    [string]$ReceiptPrinter = '',
    [string]$BarcodePrinter = '',

    # Serial scale
    [string]$ScalePort      = 'COM3',
    [int]   $ScaleBaud      = 9600,

    [string]$InstallDir     = "$env:ProgramFiles\KosovaPOS Agent",
    [switch]$Mock
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'KosovaPOSAgent'
$DisplayName = 'KosovaPOS Hardware Agent'
$LogDir      = "$env:ProgramData\KosovaPOS\Agent\logs"

function Write-Step { param($m) Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok   { param($m) Write-Host "    $m" -ForegroundColor Green }
function Write-Warn { param($m) Write-Host "    $m" -ForegroundColor Yellow }

# --- preflight ---------------------------------------------------------------
$identity = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this from an elevated PowerShell (right-click -> Run as administrator)."
}

$sourceExe = Join-Path $PSScriptRoot 'KosovaPOS.Agent.exe'
if (-not (Test-Path $sourceExe)) {
    throw "KosovaPOS.Agent.exe not found next to this script ($PSScriptRoot). Unzip the whole package and run it from there."
}

# --- stop any existing service (upgrade path) --------------------------------
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Step "Existing service found - stopping for upgrade"
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        # The file stays locked briefly after the SCM reports Stopped.
        $existing.WaitForStatus('Stopped', '00:00:30')
        Start-Sleep -Seconds 2
    }
    Write-Ok "stopped"
}

# --- copy the binary ---------------------------------------------------------
Write-Step "Installing to $InstallDir"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir     | Out-Null
$targetExe = Join-Path $InstallDir 'KosovaPOS.Agent.exe'
Copy-Item $sourceExe $targetExe -Force
Write-Ok "KosovaPOS.Agent.exe copied"

# F-Link's watched folder must exist before the first sale.
New-Item -ItemType Directory -Force -Path $FiscalTempPath | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $FiscalTempPath 'PrintErrors') | Out-Null

# --- create the service ------------------------------------------------------
if (-not $existing) {
    Write-Step "Registering the '$DisplayName' service"
    # Quote the path: it contains a space (Program Files).
    New-Service -Name $ServiceName `
                -DisplayName $DisplayName `
                -BinaryPathName "`"$targetExe`"" `
                -Description 'Bridges the KosovaPOS web app to the fiscal printer, receipt printer, label printer and scale on this PC.' `
                -StartupType Automatic | Out-Null
    Write-Ok "service created"
} else {
    # Keep the binary path correct even if -InstallDir changed between runs.
    & sc.exe config $ServiceName binPath= "`"$targetExe`"" start= auto | Out-Null
}

# Restart on crash: after 5s, then 10s, then every 30s; reset the counter daily.
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# --- configuration, as service-scoped environment variables -------------------
# Scoped to the service key rather than the machine, so nothing else on the PC
# inherits them and an uninstall takes them with it.
Write-Step "Writing configuration"
$env_vars = @(
    "AGENT_PORT=$Port",
    "AGENT_ALLOWED_ORIGINS=$PosUrl",
    "AGENT_LOG_DIR=$LogDir",
    "FISCAL_TEMP_PATH=$FiscalTempPath",
    "FISCAL_PRINTER_PORT=$FiscalComPort",
    "FISCAL_PRINTER_MODEL=$FiscalModel",
    "FISCAL_NUMBER=$FiscalNumber",
    "SCALE_PORT=$ScalePort",
    "SCALE_BAUD=$ScaleBaud"
)
if ($Mock)                                  { $env_vars += 'AGENT_MOCK=true' }
if (-not [string]::IsNullOrWhiteSpace($ReceiptPrinter)) { $env_vars += "RECEIPT_PRINTER=$ReceiptPrinter" }
if (-not [string]::IsNullOrWhiteSpace($BarcodePrinter)) { $env_vars += "BARCODE_PRINTER=$BarcodePrinter" }

Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" `
                 -Name 'Environment' -Value $env_vars -Type MultiString
Write-Ok "$($env_vars.Count) settings written"
if ($Mock) { Write-Warn "MOCK MODE - no real hardware will be driven." }

# --- start + verify ----------------------------------------------------------
Write-Step "Starting the service"
Start-Service -Name $ServiceName
(Get-Service $ServiceName).WaitForStatus('Running', '00:00:30')

$health = $null
foreach ($attempt in 1..15) {
    try {
        $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2
        break
    } catch { Start-Sleep -Milliseconds 700 }
}

if (-not $health) {
    Write-Warn "The service started but /health did not answer on port $Port."
    Write-Warn "Most recent log lines:"
    Get-ChildItem $LogDir -Filter 'agent-*.log' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
        Get-Content -Tail 25 | ForEach-Object { Write-Host "      $_" }
    throw "Agent is not healthy. See $LogDir."
}

Write-Host ""
Write-Host "  KosovaPOS Agent is running." -ForegroundColor Green
Write-Host "  version      : $($health.version)"
Write-Host "  hardware     : $(if ($health.realHardware) { 'REAL' } else { 'MOCK' })"
Write-Host "  fiscal folder: $($health.fiscal.tempPath)"
Write-Host "  capabilities : fiscal=$($health.capabilities.fiscal) receipt=$($health.capabilities.receipt) barcode=$($health.capabilities.barcode) scale=$($health.capabilities.scale)"
Write-Host "  logs         : $($health.logDirectory)"
Write-Host ""
Write-Host "  Next: open $PosUrl on THIS PC and check the badge on the Shitje screen." -ForegroundColor Cyan
if (-not $health.realHardware -and -not $Mock) {
    Write-Warn "Expected REAL hardware but the agent reports MOCK - is this actually Windows?"
}
