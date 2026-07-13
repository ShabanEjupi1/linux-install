#Requires -Version 5.1
<#
.SYNOPSIS
    Installs the KosovaPOS Hardware Agent on this PC, downloading it from the POS itself.

.DESCRIPTION
    This is the whole shop-PC setup, in one script. It downloads the agent package from the
    POS server, unpacks it, and hands over to install-agent.ps1 (which registers the Windows
    service, writes the printer configuration and verifies /health).

    Once the agent runs, the POS prints receipts and labels straight to the printers — raw
    ESC/POS and TSPL — with NO print dialog, and each document goes to its own printer.

    Get the exact command, with this shop's printer names already filled in, from
    Cilësimet -> Pajisjet in the POS.

    Run it from an ELEVATED PowerShell (right-click -> Run as administrator):

        irm https://pos.spacecode.tech/shkarko/instalo.ps1 -OutFile "$env:TEMP\instalo.ps1"
        & "$env:TEMP\instalo.ps1" -PosUrl "https://pos.spacecode.tech"

.PARAMETER PosUrl
    The POS this PC uses — the agent is downloaded from it, and only this origin is allowed
    to call the agent afterwards. Use the shop's own address (e.g. https://pos-811274183.spacecode.tech).

.PARAMETER ReceiptPrinter
    Windows printer name for the 80mm thermal receipts. Empty = the system default printer.

.PARAMETER BarcodePrinter
    Windows printer name for the barcode labels (the HPRT). Empty = the system default printer.

.PARAMETER InvoicePrinter
    Windows printer name for the A4 paper — the invoice and the waybill. Empty = the system
    default printer, which on a till is usually the thermal roll: an A4 invoice sent there comes
    out as a metre of receipt paper. Pick the office printer.

.PARAMETER Mock
    Install with mock drivers: nothing is sent to real hardware, but the whole path is
    exercised end to end. Re-run without -Mock once the printers are wired up.

.EXAMPLE
    & "$env:TEMP\instalo.ps1" -PosUrl "https://pos-811274183.spacecode.tech" `
                              -ReceiptPrinter "POS-80" -BarcodePrinter "HPRT HT300"
#>
[CmdletBinding()]
param(
    [string] $PosUrl         = 'https://pos.spacecode.tech',
    [string] $ReceiptPrinter = '',
    [string] $BarcodePrinter = '',
    [string] $InvoicePrinter = '',
    [string] $FiscalTempPath = 'C:\TEMP\',
    [string] $FiscalComPort  = 'COM1',
    [string] $FiscalNumber   = '003910',
    [switch] $Mock
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Hape PowerShell-in si Administrator (djathtas -> Run as administrator) dhe provo perseri."
}

# TLS 1.2 — Windows PowerShell 5.1 still defaults to SSL3/TLS1.0 on older builds, and the
# download would fail against a modern server with a confusing handshake error.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$base    = $PosUrl.TrimEnd('/')
$zipUrl  = "$base/shkarko/agjenti.zip"
$work    = Join-Path $env:TEMP ("kosovapos-agent-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$zipPath = Join-Path $work 'agjenti.zip'

New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    Write-Host "==> Duke shkarkuar agjentin nga $zipUrl" -ForegroundColor Cyan
    # -UseBasicParsing: on a fresh Windows profile Invoke-WebRequest otherwise needs Internet
    # Explorer's engine to be initialised, which on a locked-down shop PC it is not.
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
    Write-Host "    " (Get-Item $zipPath).Length "bytes" -ForegroundColor Green

    Write-Host "==> Duke shpaketuar" -ForegroundColor Cyan
    Expand-Archive -Path $zipPath -DestinationPath $work -Force

    # The zip holds one KosovaPOS-Agent-<version>\ folder; find the installer inside it
    # rather than assuming the version, so a newer package still installs.
    $installer = Get-ChildItem -Path $work -Filter 'install-agent.ps1' -Recurse |
                 Select-Object -First 1
    if (-not $installer) {
        throw "install-agent.ps1 nuk u gjet ne paketen e shkarkuar."
    }

    $params = @{
        PosUrl         = $base
        FiscalTempPath = $FiscalTempPath
        FiscalComPort  = $FiscalComPort
        FiscalNumber   = $FiscalNumber
    }
    if ($ReceiptPrinter) { $params.ReceiptPrinter = $ReceiptPrinter }
    if ($BarcodePrinter) { $params.BarcodePrinter = $BarcodePrinter }
    if ($InvoicePrinter) { $params.InvoicePrinter = $InvoicePrinter }
    if ($Mock)           { $params.Mock           = $true }

    Write-Host "==> Duke instaluar sherbimin" -ForegroundColor Cyan
    & $installer.FullName @params
}
finally {
    # The service now runs from Program Files; the download is scratch.
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
