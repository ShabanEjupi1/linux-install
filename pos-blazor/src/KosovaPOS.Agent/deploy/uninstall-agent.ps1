#Requires -Version 5.1
<#
.SYNOPSIS
    Removes the KosovaPOS Hardware Agent service from this PC.

.DESCRIPTION
    Stops and deletes the service and its configuration. Logs and the installed
    binary are kept unless -Purge is given, so a failed install can still be
    diagnosed after the service is gone.

.EXAMPLE
    .\uninstall-agent.ps1
    .\uninstall-agent.ps1 -Purge     # also delete the binary and the logs
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "$env:ProgramFiles\KosovaPOS Agent",
    [switch]$Purge
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'KosovaPOSAgent'
$LogDir      = "$env:ProgramData\KosovaPOS\Agent\logs"

$identity = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this from an elevated PowerShell (right-click -> Run as administrator)."
}

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') {
        Write-Host "==> Stopping $ServiceName" -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force
        $svc.WaitForStatus('Stopped', '00:00:30')
    }
    Write-Host "==> Deleting the service" -ForegroundColor Cyan
    # Remove-Service only exists on PowerShell 6+; sc.exe works everywhere.
    & sc.exe delete $ServiceName | Out-Null
    Write-Host "    removed (service config and its env vars go with the key)" -ForegroundColor Green
} else {
    Write-Host "Service $ServiceName is not installed." -ForegroundColor Yellow
}

if ($Purge) {
    foreach ($path in @($InstallDir, $LogDir)) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force
            Write-Host "    deleted $path" -ForegroundColor Green
        }
    }
} else {
    Write-Host "Binary and logs kept. Re-run with -Purge to delete them." -ForegroundColor Yellow
}
