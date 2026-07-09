#Requires -Version 5.1
<#
.SYNOPSIS
    Exercises the installed agent without making a sale.

.DESCRIPTION
    Use during F-Link bring-up. -Fiscal prints a REAL fiscal test receipt through
    F-Link when the agent is in real-hardware mode, so only run it when the shop
    is closed / the fiscal day allows it.

.EXAMPLE
    .\test-agent.ps1                    # health + courtesy receipt + scale
    .\test-agent.ps1 -Fiscal            # also send a fiscal test receipt
#>
[CmdletBinding()]
param(
    [int]$Port = 9099,
    [switch]$Fiscal
)

$ErrorActionPreference = 'Stop'
$base = "http://127.0.0.1:$Port"

function Try-Step {
    param([string]$Name, [scriptblock]$Body)
    Write-Host "==> $Name" -ForegroundColor Cyan
    try {
        $result = & $Body
        Write-Host "    OK" -ForegroundColor Green
        return $result
    } catch {
        Write-Host "    FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

$health = Try-Step 'health' { Invoke-RestMethod -Uri "$base/health" -TimeoutSec 3 }
if (-not $health) {
    Write-Host "The agent is not answering on $base. Is the KosovaPOSAgent service running?" -ForegroundColor Red
    exit 1
}

Write-Host "    version $($health.version), hardware=$(if ($health.realHardware) {'REAL'} else {'MOCK'})"
Write-Host "    fiscal folder $($health.fiscal.tempPath), COM $($health.fiscal.comPort)"
Write-Host ""

Try-Step 'courtesy receipt (receipt printer)' {
    $body = @{
        businessName  = 'KosovaPOS'
        receiptNumber = 'TEST'
        cashierName   = 'install-test'
        paymentMethod = 'Kesh'
        lines         = @(
            @{ name = 'TEST ARTIKULL'; quantity = 1; unitPrice = 0.01; lineTotal = 0.01; vatRate = 0 }
        )
        subtotal = 0.01; vat = 0; total = 0.01; paid = 0.01; change = 0
        footer   = 'Nese e lexoni kete, printeri punon.'
    } | ConvertTo-Json -Depth 4
    Invoke-RestMethod -Uri "$base/receipt/print" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 20
} | Out-Null

Try-Step 'scale read' {
    $r = Invoke-RestMethod -Uri "$base/scale/read" -TimeoutSec 10
    if ($r.ok) { Write-Host "    weight: $($r.weight) kg" } else { Write-Host "    scale said: $($r.error)" -ForegroundColor Yellow }
    $r
} | Out-Null

if ($Fiscal) {
    if ($health.realHardware) {
        Write-Host ""
        Write-Host "About to send a REAL fiscal receipt to the tax device." -ForegroundColor Yellow
        $answer = Read-Host "Type YES to continue"
        if ($answer -ne 'YES') { Write-Host "Skipped."; exit 0 }
    }
    Try-Step 'fiscal print (F-Link)' {
        # Same INP shape FiscalReceiptBuilder emits: one 0%-VAT (tax group 3) line
        # at 0.01 EUR, dept 1, cash. LF endings, as the builder writes them.
        $payload = "S,1,______,_,__;TEST ARTIKULL;0.01;1.00;3;1;3;0;0;0;0`n" +
                   "Q,1,______,_,__;1;Pagoi: 0.01`n" +
                   "Q,1,______,_,__;2;Kusur: 0.00`n" +
                   "T,1,______,_,__;`n"
        $body = @{ payload = $payload; receiptNumber = 'TEST'; timeoutSeconds = 60 } | ConvertTo-Json
        $r = Invoke-RestMethod -Uri "$base/fiscal/print" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 90
        if (-not $r.ok) { throw $r.error }
        Write-Host "    F-Link accepted in $($r.waitedSeconds)s"
        $r
    } | Out-Null
} else {
    Write-Host "==> fiscal print skipped (pass -Fiscal to test the tax device)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Logs: $($health.logDirectory)" -ForegroundColor Cyan
