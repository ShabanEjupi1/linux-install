# UpdateStockFromScript.ps1
# Carefully updates ONLY stock quantities from the original SQL script
# DOES NOT touch any other data in the production database

param(
    [string]$ScriptPath = "$PSScriptRoot\..\script.sql",
    [string]$DatabasePath = "$PSScriptRoot\..\publish\production\Database\KosovaPOS.db",
    [switch]$DryRun = $false
)

Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Stock Quantity Update Tool - PRODUCTION SAFE                     ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠️  DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Check files exist
if (-not (Test-Path $ScriptPath)) {
    Write-Host "❌ SQL Script not found: $ScriptPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $DatabasePath)) {
    Write-Host "❌ Database not found: $DatabasePath" -ForegroundColor Red
    exit 1
}

Write-Host "📂 SQL Script: $ScriptPath" -ForegroundColor Gray
Write-Host "📂 Database: $DatabasePath" -ForegroundColor Gray
Write-Host ""

# Create backup first
if (-not $DryRun) {
    $backupPath = "$DatabasePath.backup_stock_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "📦 Creating backup: $backupPath" -ForegroundColor Yellow
    Copy-Item $DatabasePath $backupPath -Force
    Write-Host "✅ Backup created" -ForegroundColor Green
    Write-Host ""
}

# Parse stock quantities from SQL script
Write-Host "🔍 Parsing stock quantities from SQL script..." -ForegroundColor Cyan

$stockData = @{}

# Read the script file in chunks to handle large files
$reader = [System.IO.StreamReader]::new($ScriptPath)
$lineNum = 0
$insertPattern = "INSERT \[dbo\]\.\[Artikujt\].*\[Sasia\].*VALUES"
$inInserts = $false
$articleData = @()

while ($null -ne ($line = $reader.ReadLine())) {
    $lineNum++
    
    # Detect start of article inserts
    if ($line -match "INSERT \[dbo\]\.\[Artikujt\]") {
        $inInserts = $true
    }
    
    # Parse VALUES lines
    if ($inInserts -and $line -match "^INSERT \[dbo\]\.\[Artikujt\]") {
        # This is a continuation of INSERT statements
        # Extract barcode and sasia from the VALUES
        # Format: INSERT [dbo].[Artikujt] ([columns]) VALUES (id, 'barcode', 'name', ... sasia(15th), ...)
        
        if ($line -match "VALUES \((\d+),\s*N?'([^']*)'.*") {
            $id = $matches[1]
            $barcode = $matches[2]
            
            # Parse the values - Sasia is at position 16 (0-indexed 15)
            # The line has format: VALUES (id, 'barcode', 'name', ..., sasia, ...)
            # Need to extract all values and get position 15
            
            if ($line -match "VALUES \(([^)]+)\)") {
                $valuesStr = $matches[1]
                # Split by comma but handle quoted strings
                $values = @()
                $current = ""
                $inQuote = $false
                $quoteChar = ''
                
                for ($i = 0; $i -lt $valuesStr.Length; $i++) {
                    $char = $valuesStr[$i]
                    
                    if (-not $inQuote -and ($char -eq "'" -or $char -eq '"')) {
                        $inQuote = $true
                        $quoteChar = $char
                        $current += $char
                    }
                    elseif ($inQuote -and $char -eq $quoteChar) {
                        $inQuote = $false
                        $current += $char
                    }
                    elseif (-not $inQuote -and $char -eq ',') {
                        $values += $current.Trim()
                        $current = ""
                    }
                    else {
                        $current += $char
                    }
                }
                $values += $current.Trim()
                
                # Position 15 is Sasia (index starts at 0)
                # 0=id, 1=Barkodi, 2=Emertimi, 3=NjesiaP, 4=NjesiaSH, 5=Kategoria, 6=PaBarkod, 7=IRregullt,
                # 8=CFurnizimit, 9=Marzha, 10=Paketimi, 11=CPaketimit, 12=CShumices, 13=CShitjes, 14=CShitjes1, 15=Sasia
                
                if ($values.Count -gt 15) {
                    $sasia = $values[15]
                    # Clean the barcode (remove N' prefix if present)
                    $cleanBarcode = $barcode -replace "^N?'", "" -replace "'$", ""
                    
                    # Parse sasia - handle NULL
                    $stockQty = 0.0
                    if ($sasia -ne "NULL" -and $sasia -ne "") {
                        $stockQty = [double]::Parse($sasia)
                    }
                    
                    if ($cleanBarcode -and $cleanBarcode -ne "") {
                        $stockData[$cleanBarcode] = $stockQty
                    }
                }
            }
        }
    }
    
    # Stop after Artikujt section
    if ($inInserts -and $line -match "^SET IDENTITY_INSERT \[dbo\]\.\[Artikujt\] OFF") {
        break
    }
}

$reader.Close()

Write-Host "✅ Found $($stockData.Count) articles with stock data" -ForegroundColor Green
Write-Host ""

# Show sample data
Write-Host "📊 Sample stock data (first 10):" -ForegroundColor Cyan
$stockData.GetEnumerator() | Select-Object -First 10 | ForEach-Object {
    Write-Host "   Barcode: $($_.Key) -> Stock: $($_.Value)" -ForegroundColor Gray
}
Write-Host ""

# Now update the SQLite database
if (-not $DryRun) {
    Write-Host "🔄 Updating stock quantities in SQLite database..." -ForegroundColor Cyan
    
    # Load SQLite assembly
    Add-Type -Path "C:\Users\Dell\.nuget\packages\microsoft.data.sqlite.core\9.0.2\lib\netstandard2.0\Microsoft.Data.Sqlite.dll" -ErrorAction SilentlyContinue
    
    # Try using sqlite3 command line tool
    $updateCount = 0
    $skipCount = 0
    $errorCount = 0
    
    # Create a SQL file with all updates
    $sqlFile = "$env:TEMP\stock_updates.sql"
    $updates = @()
    $updates += "BEGIN TRANSACTION;"
    
    foreach ($entry in $stockData.GetEnumerator()) {
        $barcode = $entry.Key -replace "'", "''"
        $stock = $entry.Value
        $updates += "UPDATE Articles SET StockQuantity = $stock WHERE Barcode = '$barcode';"
    }
    
    $updates += "COMMIT;"
    $updates | Out-File -FilePath $sqlFile -Encoding UTF8
    
    # Execute with sqlite3
    $sqlite3 = "C:\Users\Dell\Desktop\POS\Tools\sqlite3.exe"
    if (-not (Test-Path $sqlite3)) {
        # Download sqlite3 if not present
        Write-Host "📥 Downloading sqlite3 tool..." -ForegroundColor Yellow
        $sqliteUrl = "https://www.sqlite.org/2024/sqlite-tools-win-x64-3470200.zip"
        $zipPath = "$env:TEMP\sqlite-tools.zip"
        $extractPath = "$env:TEMP\sqlite-tools"
        
        Invoke-WebRequest -Uri $sqliteUrl -OutFile $zipPath
        Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
        $sqlite3 = Get-ChildItem -Path $extractPath -Filter "sqlite3.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
        
        if ($sqlite3) {
            Copy-Item $sqlite3 "C:\Users\Dell\Desktop\POS\Tools\" -Force
            $sqlite3 = "C:\Users\Dell\Desktop\POS\Tools\sqlite3.exe"
        }
    }
    
    if (Test-Path $sqlite3) {
        Write-Host "🔄 Executing $($stockData.Count) stock updates..." -ForegroundColor Cyan
        & $sqlite3 $DatabasePath ".read $sqlFile" 2>&1
        Write-Host "✅ Stock update completed!" -ForegroundColor Green
    } else {
        Write-Host "❌ sqlite3.exe not found. Please install SQLite tools." -ForegroundColor Red
        Write-Host "   Download from: https://www.sqlite.org/download.html" -ForegroundColor Yellow
    }
    
    # Cleanup
    Remove-Item $sqlFile -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║     Stock Update Complete                                            ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
