# Set-ProductionEnvVars.ps1
# Script to set environment variables in web.config on production server
# Usage: .\Set-ProductionEnvVars.ps1

param(
    [string]$ServerIP = "95.156.253.171",
    [string]$SitePath = "C:\Workspace\RUN\RefahiPlus-WebApi"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "   Set Environment Variables" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check credentials
$credentialPath = Join-Path $env:USERPROFILE ".refahi\server-credential.xml"
if (-not (Test-Path $credentialPath)) {
    Write-Host "[ERROR] Credentials not found!" -ForegroundColor Red
    Write-Host "Run Setup-DevMachine.ps1 first" -ForegroundColor Yellow
    exit 1
}
$credential = Import-Clixml -Path $credentialPath

# Get environment variable values
Write-Host "Enter the environment variable values:" -ForegroundColor Yellow
Write-Host ""

$dbUser = Read-Host "Database User (REFAHIPLUS_DB_USER)"
$dbPassword = Read-Host "Database Password (REFAHIPLUS_DB_PASSWORD)" -AsSecureString
$dbPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($dbPassword))

$jwtKey = Read-Host "JWT Key - min 32 chars (REFAHIPLUS_JWT_KEY)" -AsSecureString
$jwtKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($jwtKey))

if ($jwtKeyPlain.Length -lt 32) {
    Write-Host "[ERROR] JWT Key must be at least 32 characters!" -ForegroundColor Red
    exit 1
}

$snappTripKey = Read-Host "SnappTrip API Key (REFAHIPLUS_SNAPPTRIP_API_KEY)" -AsSecureString
$snappTripKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($snappTripKey))

Write-Host ""
Write-Host "Connecting to server..." -ForegroundColor Yellow

try {
    $session = New-PSSession -ComputerName $ServerIP -Credential $credential
    
    Write-Host "Updating web.config..." -ForegroundColor Yellow
    
    $result = Invoke-Command -Session $session -ScriptBlock {
        param($sitePath, $dbUser, $dbPassword, $jwtKey, $snappTripKey)
        
        $configPath = Join-Path $sitePath "web.config"
        
        if (-not (Test-Path $configPath)) {
            throw "web.config not found at: $configPath"
        }
        
        # Backup
        $backupPath = "$configPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Copy-Item $configPath $backupPath
        
        # Load XML
        $xml = [xml](Get-Content $configPath)
        
        # Update environment variables
        $envVars = $xml.configuration.location.'system.webServer'.aspNetCore.environmentVariables
        
        if ($null -eq $envVars) {
            throw "environmentVariables section not found in web.config"
        }
        
        $updated = 0
        foreach ($var in $envVars.environmentVariable) {
            switch ($var.name) {
                "REFAHIPLUS_DB_USER" { 
                    $var.value = $dbUser
                    $updated++
                }
                "REFAHIPLUS_DB_PASSWORD" { 
                    $var.value = $dbPassword
                    $updated++
                }
                "REFAHIPLUS_JWT_KEY" { 
                    $var.value = $jwtKey
                    $updated++
                }
                "REFAHIPLUS_SNAPPTRIP_API_KEY" { 
                    $var.value = $snappTripKey
                    $updated++
                }
            }
        }
        
        # Save
        $xml.Save($configPath)
        
        # Restart App Pool
        Import-Module WebAdministration
        $appPoolName = "RefahiPlus-WebApi"
        
        Stop-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Start-WebAppPool -Name $appPoolName
        Start-Sleep -Seconds 2
        
        $state = (Get-WebAppPoolState -Name $appPoolName).Value
        
        return @{
            Updated = $updated
            Backup = $backupPath
            AppPoolState = $state
        }
        
    } -ArgumentList $SitePath, $dbUser, $dbPasswordPlain, $jwtKeyPlain, $snappTripKeyPlain
    
    Remove-PSSession -Session $session
    
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Green
    Write-Host " SUCCESS!" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Updated $($result.Updated) environment variables" -ForegroundColor Green
    Write-Host "Backup saved: $($result.Backup)" -ForegroundColor Gray
    Write-Host "App Pool State: $($result.AppPoolState)" -ForegroundColor $(if ($result.AppPoolState -eq 'Started') { 'Green' } else { 'Yellow' })
    Write-Host ""
    Write-Host "Test the API:" -ForegroundColor Cyan
    Write-Host "  curl http://refahiplus.com/api/health" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if ($session) { Remove-PSSession -Session $session -ErrorAction SilentlyContinue }
    exit 1
}
