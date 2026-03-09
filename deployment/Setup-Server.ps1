# =============================================================================
# IIS Server Setup Script for ASP.NET Core API
# Run this script on IIS Server as Administrator
# =============================================================================

param(
    [Parameter(Mandatory=$false)]
    [string]$SiteName = "RefahiPlus-WebApi",
    
    [Parameter(Mandatory=$false)]
    [string]$AppPoolName = "RefahiPlus-WebApi",
    
    [Parameter(Mandatory=$false)]
    [string]$HostName = "api.refahiplus.ir",
    
    [Parameter(Mandatory=$false)]
    [int]$Port = 80,
    
    [Parameter(Mandatory=$false)]
    [string]$SitePath = "C:\Workspace\RUN\RefahiPlus-WebApi",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipSSL
)

# Check Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator!"
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   IIS Setup for ASP.NET Core API" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# =============================================================================
# 1. Install IIS and Required Features
# =============================================================================

Write-Host "1. Checking and Installing IIS..." -ForegroundColor Yellow

$iisFeatures = @(
    'Web-Server',
    'Web-WebServer',
    'Web-Common-Http',
    'Web-Default-Doc',
    'Web-Dir-Browsing',
    'Web-Http-Errors',
    'Web-Static-Content',
    'Web-Health',
    'Web-Http-Logging',
    'Web-Performance',
    'Web-Stat-Compression',
    'Web-Dyn-Compression',
    'Web-Security',
    'Web-Filtering',
    'Web-App-Dev',
    'Web-Net-Ext45',
    'Web-Asp-Net45',
    'Web-ISAPI-Ext',
    'Web-ISAPI-Filter',
    'Web-Mgmt-Tools',
    'Web-Mgmt-Console'
)

foreach ($feature in $iisFeatures) {
    $installed = (Get-WindowsFeature -Name $feature).Installed
    if (-not $installed) {
        Write-Host "  Installing $feature..." -ForegroundColor Gray
        Install-WindowsFeature -Name $feature -IncludeManagementTools | Out-Null
    } else {
        Write-Host "  [OK] $feature is installed" -ForegroundColor Green
    }
}

# =============================================================================
# 2. Check .NET Runtime
# =============================================================================

Write-Host ""
Write-Host "2. Checking .NET Runtime..." -ForegroundColor Yellow

$runtimes = dotnet --list-runtimes 2>$null

if ($null -eq $runtimes) {
    Write-Host "  [WARNING] .NET Runtime not found!" -ForegroundColor Red
    Write-Host "  Please install from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    
    $response = Read-Host "  Continue anyway? (y/n)"
    if ($response -ne 'y') {
        exit 0
    }
} else {
    Write-Host "  [OK] .NET Runtime is installed:" -ForegroundColor Green
    $runtimes | Select-String "Microsoft.AspNetCore.App 10" | ForEach-Object {
        Write-Host "    $_" -ForegroundColor Gray
    }
}

# =============================================================================
# 3. Check ASP.NET Core Module
# =============================================================================

Write-Host ""
Write-Host "3. Checking ASP.NET Core Module..." -ForegroundColor Yellow

$ancmPath = "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"

if (Test-Path $ancmPath) {
    Write-Host "  [OK] ASP.NET Core Module is installed" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] ASP.NET Core Module not found!" -ForegroundColor Red
    Write-Host "  Please install .NET Hosting Bundle:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Cyan
    
    $response = Read-Host "  Continue anyway? (y/n)"
    if ($response -ne 'y') {
        exit 0
    }
}

# =============================================================================
# 4. Enable PowerShell Remoting
# =============================================================================

Write-Host ""
Write-Host "4. Enabling PowerShell Remoting..." -ForegroundColor Yellow

try {
    Enable-PSRemoting -Force -SkipNetworkProfileCheck | Out-Null
    Write-Host "  [OK] PowerShell Remoting enabled" -ForegroundColor Green
} catch {
    Write-Host "  [WARNING] Error enabling: $_" -ForegroundColor Yellow
}

try {
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value "*" -Force
    Write-Host "  [OK] TrustedHosts configured" -ForegroundColor Green
} catch {
    Write-Host "  [WARNING] Error setting TrustedHosts: $_" -ForegroundColor Yellow
}

Restart-Service WinRM -Force
Write-Host "  [OK] WinRM restarted" -ForegroundColor Green

# =============================================================================
# 5. Configure Firewall
# =============================================================================

Write-Host ""
Write-Host "5. Configuring Firewall..." -ForegroundColor Yellow

$firewallRules = @(
    @{Name='WinRM-HTTP'; Port=5985; Description='Windows Remote Management (HTTP)'},
    @{Name='WinRM-HTTPS'; Port=5986; Description='Windows Remote Management (HTTPS)'},
    @{Name='HTTP'; Port=80; Description='HTTP Traffic'},
    @{Name='HTTPS'; Port=443; Description='HTTPS Traffic'}
)

foreach ($rule in $firewallRules) {
    $exists = Get-NetFirewallRule -Name $rule.Name -ErrorAction SilentlyContinue
    
    if (-not $exists) {
        New-NetFirewallRule `
            -Name $rule.Name `
            -DisplayName $rule.Description `
            -Enabled True `
            -Direction Inbound `
            -Protocol TCP `
            -LocalPort $rule.Port | Out-Null
        
        Write-Host "  [OK] Rule $($rule.Name) created" -ForegroundColor Green
    } else {
        Write-Host "  [OK] Rule $($rule.Name) exists" -ForegroundColor Green
    }
}

Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing" -ErrorAction SilentlyContinue
Write-Host "  [OK] File Sharing enabled" -ForegroundColor Green

# =============================================================================
# 6. Configure File Sharing
# =============================================================================

Write-Host ""
Write-Host "6. Configuring File Sharing..." -ForegroundColor Yellow

$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
Set-ItemProperty -Path $regPath -Name "LocalAccountTokenFilterPolicy" -Value 1 -Type DWord -Force
Write-Host "  [OK] LocalAccountTokenFilterPolicy enabled" -ForegroundColor Green

Restart-Service Server -Force
Write-Host "  [OK] Server service restarted" -ForegroundColor Green

# =============================================================================
# 7. Create Site Folder
# =============================================================================

Write-Host ""
Write-Host "7. Creating Site Folder..." -ForegroundColor Yellow

if (-not (Test-Path $SitePath)) {
    New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
    Write-Host "  [OK] Folder created: $SitePath" -ForegroundColor Green
} else {
    Write-Host "  [OK] Folder exists: $SitePath" -ForegroundColor Green
}

$backupPath = "C:\inetpub\backups"
if (-not (Test-Path $backupPath)) {
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Write-Host "  [OK] Backup folder created: $backupPath" -ForegroundColor Green
}

# =============================================================================
# 8. Create Application Pool
# =============================================================================

Write-Host ""
Write-Host "8. Creating Application Pool..." -ForegroundColor Yellow

Import-Module WebAdministration

$appPool = Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue

if (-not $appPool) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "  [OK] Application Pool '$AppPoolName' created" -ForegroundColor Green
} else {
    Write-Host "  [OK] Application Pool '$AppPoolName' exists" -ForegroundColor Green
}

$appPoolPath = "IIS:\AppPools\$AppPoolName"

Set-ItemProperty -Path $appPoolPath -Name "managedRuntimeVersion" -Value ""
Write-Host "  [OK] Runtime: No Managed Code" -ForegroundColor Gray

Set-ItemProperty -Path $appPoolPath -Name "startMode" -Value "AlwaysRunning"
Write-Host "  [OK] Start Mode: AlwaysRunning" -ForegroundColor Gray

Set-ItemProperty -Path $appPoolPath -Name "processModel.idleTimeout" -Value "00:00:00"
Write-Host "  [OK] Idle Timeout: 0" -ForegroundColor Gray

Set-ItemProperty -Path $appPoolPath -Name "recycling.periodicRestart.time" -Value "00:00:00"
Write-Host "  [OK] Periodic Restart: Disabled" -ForegroundColor Gray

# =============================================================================
# 9. Create Website
# =============================================================================

Write-Host ""
Write-Host "9. Creating Website..." -ForegroundColor Yellow

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue

if (-not $site) {
    if ($SkipSSL) {
        $sitePort = 80
        $protocol = "http"
    } else {
        $sitePort = $Port
        $protocol = "https"
    }
    
    New-Website `
        -Name $SiteName `
        -PhysicalPath $SitePath `
        -ApplicationPool $AppPoolName `
        -HostHeader $HostName `
        -Port $sitePort `
        -Protocol $protocol | Out-Null
    
    Write-Host "  [OK] Website '$SiteName' created" -ForegroundColor Green
} else {
    Write-Host "  [OK] Website '$SiteName' exists" -ForegroundColor Green
}

$sitePathIIS = "IIS:\Sites\$SiteName"

Set-ItemProperty -Path $sitePathIIS -Name "applicationDefaults.preloadEnabled" -Value $true
Write-Host "  [OK] Preload Enabled" -ForegroundColor Gray

# =============================================================================
# 10. Configure Permissions
# =============================================================================

Write-Host ""
Write-Host "10. Configuring Permissions..." -ForegroundColor Yellow

$appPoolIdentity = "IIS AppPool\$AppPoolName"

icacls $SitePath /grant "${appPoolIdentity}:(OI)(CI)RX" /T | Out-Null
Write-Host "  [OK] Read and Execute permission granted to App Pool" -ForegroundColor Green

$logsPath = Join-Path $SitePath "logs"
if (Test-Path $logsPath) {
    icacls $logsPath /grant "${appPoolIdentity}:(OI)(CI)M" /T | Out-Null
    Write-Host "  [OK] Modify permission granted to logs folder" -ForegroundColor Green
}

# =============================================================================
# 11. SSL Certificate (Optional)
# =============================================================================

if (-not $SkipSSL) {
    Write-Host ""
    Write-Host "11. Configuring SSL Certificate..." -ForegroundColor Yellow
    
    $cert = Get-ChildItem -Path Cert:\LocalMachine\My | 
        Where-Object { $_.Subject -like "*$HostName*" } |
        Select-Object -First 1
    
    if ($cert) {
        Write-Host "  [OK] Certificate found with thumbprint: $($cert.Thumbprint)" -ForegroundColor Green
        
        try {
            $binding = Get-WebBinding -Name $SiteName -Protocol "https"
            $binding.AddSslCertificate($cert.Thumbprint, "my")
            Write-Host "  [OK] Certificate bound to site" -ForegroundColor Green
        } catch {
            Write-Host "  [WARNING] Error binding certificate: $_" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  [WARNING] Certificate not found!" -ForegroundColor Yellow
        Write-Host "    To generate Self-Signed Certificate:" -ForegroundColor Gray
        Write-Host "    New-SelfSignedCertificate -DnsName '$HostName' -CertStoreLocation 'Cert:\LocalMachine\My'" -ForegroundColor Gray
    }
}

# =============================================================================
# 12. Final Check
# =============================================================================

Write-Host ""
Write-Host "12. Final Check..." -ForegroundColor Yellow

$appPoolState = (Get-WebAppPoolState -Name $AppPoolName).Value
Write-Host "  Application Pool Status: $appPoolState" -ForegroundColor $(if ($appPoolState -eq 'Started') { 'Green' } else { 'Yellow' })

$siteState = (Get-Website -Name $SiteName).State
Write-Host "  Website Status: $siteState" -ForegroundColor $(if ($siteState -eq 'Started') { 'Green' } else { 'Yellow' })

if ($appPoolState -ne 'Started') {
    Start-WebAppPool -Name $AppPoolName
    Write-Host "  [OK] Application Pool started" -ForegroundColor Green
}

if ($siteState -ne 'Started') {
    Start-Website -Name $SiteName
    Write-Host "  [OK] Website started" -ForegroundColor Green
}

# =============================================================================
# Summary
# =============================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Setup Completed Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Site Information:" -ForegroundColor Cyan
Write-Host "  Site Name:        $SiteName" -ForegroundColor White
Write-Host "  App Pool:         $AppPoolName" -ForegroundColor White
Write-Host "  Physical Path:    $SitePath" -ForegroundColor White
Write-Host "  Host Name:        $HostName" -ForegroundColor White
Write-Host "  Protocol:         $(if ($SkipSSL) { 'HTTP' } else { 'HTTPS' })" -ForegroundColor White
Write-Host "  Port:             $(if ($SkipSSL) { '80' } else { $Port })" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Install SSL Certificate (if needed)" -ForegroundColor Gray
Write-Host "  2. Deploy your application" -ForegroundColor Gray
Write-Host "  3. Configure DNS to point to this server" -ForegroundColor Gray
Write-Host ""
Write-Host "To deploy:" -ForegroundColor Yellow
Write-Host "  .\DeployToProduction.ps1 -Environment Production" -ForegroundColor Cyan
Write-Host ""

# Save setup information
$infoFile = "C:\inetpub\setup-info-$SiteName.txt"
@"
Setup Information
=================
Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Site Name: $SiteName
App Pool: $AppPoolName
Physical Path: $SitePath
Host Name: $HostName
Protocol: $(if ($SkipSSL) { 'HTTP' } else { 'HTTPS' })
Port: $(if ($SkipSSL) { '80' } else { $Port })

.NET Runtimes:
$(dotnet --list-runtimes | Out-String)

Server Name: $env:COMPUTERNAME
IP Addresses:
$(Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*'} | Select-Object -ExpandProperty IPAddress | Out-String)
"@ | Out-File -FilePath $infoFile

Write-Host "Setup information saved to: $infoFile" -ForegroundColor DarkGray
Write-Host ""
