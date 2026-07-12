<#
.SYNOPSIS
    Creates a Desktop shortcut that opens KosovaPOS in Chrome with the print dialog disabled.

.DESCRIPTION
    A web page cannot suppress Chrome's print dialog — no JavaScript can. Chrome itself can:
    --kiosk-printing makes window.print() print IMMEDIATELY to the Windows default printer,
    with no dialog and no "Margins → None / Headers and footers" to get wrong.

    This is the FALLBACK. The hardware agent (install-agent.ps1) is the real answer: it prints
    raw ESC/POS and TSPL, so it needs no dialog to suppress AND it sends receipts to the Epson
    and labels to the HPRT independently. Install the agent, and the POS never calls
    window.print() at all.

    ⚠ THE CATCH, and it is the whole reason the agent exists: kiosk printing always goes to the
      WINDOWS DEFAULT PRINTER. Chrome offers no way for a page to choose. So on a PC with both
      an 80mm thermal and a label printer, only whichever is the default gets the right paper —
      and the A4 invoice (/fatura/{n}) will come out of the thermal printer as a long ribbon.
      If this shop prints A4 invoices, use the agent instead of, not alongside, this shortcut.

.PARAMETER Url
    The POS URL to open. Defaults to the live app.

.PARAMETER Name
    Shortcut name on the Desktop.

.EXAMPLE
    .\kiosk-shortcut.ps1
    .\kiosk-shortcut.ps1 -Url "https://pos.spacecode.tech" -Name "KosovaPOS"
#>
[CmdletBinding()]
param(
    [string] $Url  = "https://pos.spacecode.tech",
    [string] $Name = "KosovaPOS"
)

$ErrorActionPreference = "Stop"

$chrome = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $chrome) {
    throw "Chrome nuk u gjet. Instalo Google Chrome, ose hape POS-in me agjentin e pajisjeve."
}

# A dedicated profile directory: --kiosk-printing must not leak into the cashier's ordinary
# browsing, where a silent print to the thermal printer would be a nasty surprise.
$profileDir = Join-Path $env:LOCALAPPDATA "KosovaPOS\ChromeProfile"
New-Item -ItemType Directory -Force -Path $profileDir | Out-Null

$desktop  = [Environment]::GetFolderPath("Desktop")
$linkPath = Join-Path $desktop "$Name.lnk"

$shell = New-Object -ComObject WScript.Shell
$link  = $shell.CreateShortcut($linkPath)
$link.TargetPath = $chrome
$link.Arguments  = "--kiosk-printing --user-data-dir=`"$profileDir`" --app=`"$Url`""
$link.IconLocation = $chrome
$link.Description  = "KosovaPOS — printim pa dialog (kiosk printing)"
$link.Save()

Write-Host ""
Write-Host "  Shkurtorja u krijua:  $linkPath" -ForegroundColor Green
Write-Host "  Chrome:               $chrome"
Write-Host ""
Write-Host "  TANI: vendos printerin termik si printer I PARAZGJEDHUR i Windows-it" -ForegroundColor Yellow
Write-Host "        (Settings -> Bluetooth & devices -> Printers & scanners ->"
Write-Host "         printeri termik -> Set as default), sepse kiosk-printing printon"
Write-Host "         gjithmone te printeri i parazgjedhur."
Write-Host ""
Write-Host "  Hape POS-in VETEM nga kjo shkurtore. Printimi behet menjehere, pa dialog."
Write-Host ""
