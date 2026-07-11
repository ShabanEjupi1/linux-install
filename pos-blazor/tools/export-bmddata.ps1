<#
.SYNOPSIS
  Exports fresh BMDData rows from the shop PC's SQL Server for import into the
  Blazor POS Postgres database.

.DESCRIPTION
  Run this ON THE SHOP PC (the one with IP .41), as the normal logged-in user.
  It uses Windows integrated auth against the local SQL Server instance, exactly
  like the desktop POS does (see POS2/App.xaml.cs), so no password is needed.

  Big journal tables are exported INCREMENTALLY (only rows newer than what the
  web POS already holds). Small lookup tables are exported in full.

  Run with -Probe first: it touches nothing and just reports how many new rows
  exist. Send that output back before doing the real export.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\export-bmddata.ps1 -Probe
  powershell -ExecutionPolicy Bypass -File .\export-bmddata.ps1
#>
[CmdletBinding()]
param(
    [switch]$Probe,
    [string]$Server   = $(if ($env:SQL_SERVER)   { $env:SQL_SERVER }   else { "localhost" }),
    [string]$Database = $(if ($env:SQL_DATABASE) { $env:SQL_DATABASE } else { "BMDData" }),
    [string]$OutDir   = "$env:USERPROFILE\Desktop\bmd-export"
)

$ErrorActionPreference = 'Stop'

# Never let Albanian locale turn 2.99 into "2,99" or reformat dates.
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

# Watermarks = MAX(pk) currently in the web POS Postgres DB (as of 2026-07-10).
# $null means "export the whole table" (small lookup tables / things that mutate in place).
$Tables = [ordered]@{
    'DitariD'           = @{ Pk = 'ID'; After = 18775 }   # sales journal
    'DitariH'           = @{ Pk = 'ID'; After = 3365  }   # purchase journal
    'ArkaHyrjeDalje'    = @{ Pk = 'id'; After = 4477  }   # cash ledger
    'tbl_Stoku'         = @{ Pk = 'ID'; After = 0     }   # stock ledger (audit trail behind Artikujt.Sasia)
    'Kartela_Subjektit' = @{ Pk = 'ID'; After = 0     }   # partner account ledger (Mbeti = outstanding)
    'Artikujt'          = @{ Pk = 'id'; After = $null }   # catalogue: prices/stock change in place
    'FurnitoriNew'      = @{ Pk = 'Id'; After = $null }
    'Punetoret'         = @{ Pk = 'id'; After = $null }
    'Qytetet'           = @{ Pk = 'ID'; After = $null }
    'Kategoria'         = @{ Pk = 'id'; After = $null }
    'Filiala'           = @{ Pk = 'id'; After = $null }
    'Sektori'           = @{ Pk = 'id'; After = $null }
    'Tatimi'            = @{ Pk = 'ID'; After = $null }   # VAT classes: T3/T8/T18
    'Arkat'             = @{ Pk = 'id'; After = $null }   # cash registers
    'MetodaPagese'      = @{ Pk = 'id'; After = $null }   # payment methods
    'NjesitMatese'      = @{ Pk = 'id'; After = $null }   # units of measure
    'LlojiShpenzimeve'  = @{ Pk = 'id'; After = $null }   # expense types
    'KategoriaPos'      = @{ Pk = 'id'; After = $null }   # POS categories (Image blob is dropped)
}

$ConnStr = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=10;"

function New-Conn {
    $c = New-Object System.Data.SqlClient.SqlConnection $ConnStr
    $c.Open()
    return $c
}

function Format-Val($v) {
    if ($null -eq $v -or $v -is [System.DBNull]) { return '\N' }
    if ($v -is [byte[]])   { return '\N' }                                   # image/Foto blobs: dropped
    if ($v -is [datetime]) { return '"' + $v.ToString('yyyy-MM-dd HH:mm:ss') + '"' }
    if ($v -is [bool])     { return '"' + $(if ($v) { '1' } else { '0' }) + '"' }
    if ($v -is [decimal] -or $v -is [double] -or $v -is [single]) {
        return '"' + $v.ToString([System.Globalization.CultureInfo]::InvariantCulture) + '"'
    }
    return '"' + ([string]$v).Replace('"', '""') + '"'                       # RFC4180 quoting
}

# ---------------------------------------------------------------- probe mode
if ($Probe) {
    Write-Host "Probing $Server/$Database ...`n"
    $conn = New-Conn
    "{0,-16} {1,10} {2,12} {3,12}" -f 'TABLE', 'TOTAL', 'MAX(PK)', 'NEW ROWS' | Write-Host
    Write-Host ('-' * 54)
    foreach ($t in $Tables.Keys) {
        $pk    = $Tables[$t].Pk
        $after = $Tables[$t].After
        $cmd = $conn.CreateCommand()
        if ($null -eq $after) {
            $cmd.CommandText = "SELECT COUNT(*), MAX([$pk]), COUNT(*) FROM [dbo].[$t]"
        } else {
            $cmd.CommandText = "SELECT COUNT(*), MAX([$pk]), (SELECT COUNT(*) FROM [dbo].[$t] WHERE [$pk] > $after) FROM [dbo].[$t]"
        }
        try {
            $r = $cmd.ExecuteReader()
            [void]$r.Read()
            $tot = $r.GetValue(0); $mx = $r.GetValue(1); $new = $r.GetValue(2)
            $r.Close()
            "{0,-16} {1,10} {2,12} {3,12}" -f $t, $tot, $mx, $new | Write-Host
        } catch {
            "{0,-16} {1,10}" -f $t, "MISSING/ERR" | Write-Host
        }
    }
    # how fresh is the desktop data, per journal?
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT MAX([DATA]) FROM [dbo].[DitariD]"
    Write-Host "`nNewest SALE     in BMDData: $($cmd.ExecuteScalar())"
    $cmd.CommandText = "SELECT MAX([DATA]) FROM [dbo].[DitariH]"
    Write-Host "Newest PURCHASE in BMDData: $($cmd.ExecuteScalar())"
    # Date span of the purchases the web POS is still MISSING (ID > watermark 3365).
    $cmd.CommandText = "SELECT MIN([DATA]), MAX([DATA]) FROM [dbo].[DitariH] WHERE [ID] > 3365"
    $rr = $cmd.ExecuteReader(); [void]$rr.Read()
    if (-not $rr.IsDBNull(0)) { Write-Host "Purchases NEWER than the web POS: $($rr.GetValue(0)) .. $($rr.GetValue(1))" }
    else { Write-Host "Purchases NEWER than the web POS: NONE (desktop has nothing past ID 3365 / 2026-03-27)" }
    $rr.Close()
    Write-Host "`nWeb POS currently holds: sales up to 2026-07-09 (ID 18775), purchases up to 2026-03-27 (ID 3365)."
    $conn.Close()
    Write-Host "`nProbe only - nothing exported. Send this output back."
    exit 0
}

# --------------------------------------------------------------- export mode
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir | Out-Null
Write-Host "Exporting $Server/$Database -> $OutDir`n"

$conn = New-Conn
$summary = @()

foreach ($t in $Tables.Keys) {
    $pk    = $Tables[$t].Pk
    $after = $Tables[$t].After
    $sql   = if ($null -eq $after) { "SELECT * FROM [dbo].[$t]" }
             else { "SELECT * FROM [dbo].[$t] WHERE [$pk] > $after ORDER BY [$pk]" }

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 300

    try { $rdr = $cmd.ExecuteReader() }
    catch { Write-Warning "$t : skipped ($($_.Exception.Message))"; continue }

    $path = Join-Path $OutDir "$t.csv"
    $enc  = New-Object System.Text.UTF8Encoding($false)      # no BOM
    $sw   = New-Object System.IO.StreamWriter($path, $false, $enc)

    $cols = @(); for ($i = 0; $i -lt $rdr.FieldCount; $i++) { $cols += $rdr.GetName($i) }
    $sw.WriteLine((($cols | ForEach-Object { '"' + $_ + '"' }) -join ','))

    $n = 0
    while ($rdr.Read()) {
        $vals = @()
        for ($i = 0; $i -lt $rdr.FieldCount; $i++) { $vals += (Format-Val $rdr.GetValue($i)) }
        $sw.WriteLine(($vals -join ','))
        $n++
    }
    $sw.Close(); $rdr.Close()

    $mode = if ($null -eq $after) { "full" } else { "new (${pk}>${after})" }
    Write-Host ("{0,-16} {1,8} rows  [{2}]" -f $t, $n, $mode)
    $summary += [pscustomobject]@{ Table = $t; Rows = $n; Mode = $mode }
}
$conn.Close()

$summary | Export-Csv (Join-Path $OutDir '_manifest.csv') -NoTypeInformation -Encoding UTF8

$zip = "$env:USERPROFILE\Desktop\bmd-export.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$OutDir\*" -DestinationPath $zip
Write-Host "`nDone -> $zip  ($([math]::Round((Get-Item $zip).Length / 1MB, 2)) MB)"
Write-Host "Send that zip back to continue the import."
