param(
    [Parameter(Mandatory=$true)]
    [string]$MachineId,
    
    [Parameter(Mandatory=$true)]
    [string]$CustomerName,
    
    [Parameter(Mandatory=$false)]
    [int]$YearsValid = 1
)

# License Key Generator for KosovaPOS
# This script generates valid license keys for customer activation
# Usage: .\Generate-LicenseKey.ps1 -MachineId "ABC123" -CustomerName "John Doe"

Add-Type -AssemblyName System.Security

function Generate-LicenseKey {
    param(
        [string]$MachineId,
        [string]$CustomerName,
        [int]$YearsValid
    )
    
    # Calculate expiration date
    $issuedDate = Get-Date
    $expirationDate = $issuedDate.AddYears($YearsValid)
    
    # Create the data to hash
    $dataToHash = "$MachineId|$($expirationDate.ToString('yyyyMMdd'))|$CustomerName"
    
    # HMAC-SHA256 signature with shared secret
    $secret = "KosovaPOS_SECRET_2024"
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
    $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($dataToHash))
    $licenseKey = [Convert]::ToBase64String($hash).Substring(0, 32)
    
    return @{
        LicenseKey = $licenseKey
        MachineId = $MachineId
        CustomerName = $CustomerName
        IssuedDate = $issuedDate
        ExpirationDate = $expirationDate
        YearsValid = $YearsValid
    }
}

# Generate the license key
$license = Generate-LicenseKey -MachineId $MachineId -CustomerName $CustomerName -YearsValid $YearsValid

# Display results
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KosovaPOS License Key Generated" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Customer Name    : $($license.CustomerName)" -ForegroundColor White
Write-Host "Machine ID       : $($license.MachineId)" -ForegroundColor White
Write-Host "License Key      : $($license.LicenseKey)" -ForegroundColor Yellow
Write-Host "Issued Date      : $($license.IssuedDate.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
Write-Host "Expiration Date  : $($license.ExpirationDate.ToString('yyyy-MM-dd'))" -ForegroundColor White
Write-Host "Valid for        : $($license.YearsValid) year(s)" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "SEND TO CUSTOMER:" -ForegroundColor Yellow
Write-Host "Please activate your KosovaPOS license with the key below:"
Write-Host ""
Write-Host "License Key: $($license.LicenseKey)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Instructions:"
Write-Host "1. Launch KosovaPOS"
Write-Host "2. On the License Activation window, paste the key above"
Write-Host "3. Enter your name: $($license.CustomerName)"
Write-Host "4. Click 'Activate License'"
Write-Host ""
Write-Host "Your license will be valid until $($license.ExpirationDate.ToString('yyyy-MM-dd'))."
Write-Host ""

# Copy to clipboard
$license.LicenseKey | Set-Clipboard
Write-Host "✓ License key copied to clipboard" -ForegroundColor Green
