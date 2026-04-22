# =============================================================================
# Setup Development Machine for Remote Deploy
# Run this ONCE on your development machine as Administrator
# =============================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Setup Dev Machine for Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator!"
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

# =============================================================================
# 1. Enable PowerShell Remoting
# =============================================================================

Write-Host "1. Enabling PowerShell Remoting..." -ForegroundColor Yellow

try {
    Enable-PSRemoting -Force -SkipNetworkProfileCheck | Out-Null
    Write-Host "  [OK] PowerShell Remoting enabled" -ForegroundColor Green
} catch {
    Write-Host "  [WARNING] Error: $_" -ForegroundColor Yellow
}

# =============================================================================
# 2. Configure TrustedHosts
# =============================================================================

Write-Host ""
Write-Host "2. Configuring TrustedHosts..." -ForegroundColor Yellow

$currentTrustedHosts = (Get-Item WSMan:\localhost\Client\TrustedHosts).Value

Write-Host "  Current TrustedHosts: $currentTrustedHosts" -ForegroundColor Gray

$serverIP = Read-Host "  Enter server IP address (e.g., 95.156.253.171)"
$serverIP = $serverIP.Trim()  # Remove any whitespace

if ([string]::IsNullOrEmpty($currentTrustedHosts)) {
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value $serverIP -Force
    Write-Host "  [OK] TrustedHosts set to: $serverIP" -ForegroundColor Green
} elseif ($currentTrustedHosts -notlike "*$serverIP*") {
    $newValue = "$currentTrustedHosts,$serverIP"
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value $newValue -Force
    Write-Host "  [OK] Server added to TrustedHosts" -ForegroundColor Green
} else {
    Write-Host "  [OK] Server already in TrustedHosts" -ForegroundColor Green
}

$newTrustedHosts = (Get-Item WSMan:\localhost\Client\TrustedHosts).Value
Write-Host "  Current TrustedHosts: $newTrustedHosts" -ForegroundColor Gray

# =============================================================================
# 3. Configure WinRM
# =============================================================================

Write-Host ""
Write-Host "3. Configuring WinRM..." -ForegroundColor Yellow

try {
    Set-Item WSMan:\localhost\Client\AllowUnencrypted -Value $false -Force
    Write-Host "  [OK] WinRM configured" -ForegroundColor Green
} catch {
    Write-Host "  [WARNING] Could not configure WinRM: $_" -ForegroundColor Yellow
}

# =============================================================================
# 4. Restart WinRM
# =============================================================================

Write-Host ""
Write-Host "4. Restarting WinRM..." -ForegroundColor Yellow

try {
    Stop-Service WinRM -Force -ErrorAction Stop
    Start-Sleep -Seconds 2
    Start-Service WinRM -ErrorAction Stop
    Write-Host "  [OK] WinRM restarted" -ForegroundColor Green
} catch {
    Write-Host "  [WARNING] Could not restart WinRM: $_" -ForegroundColor Yellow
    Write-Host "  WinRM may already be configured correctly" -ForegroundColor Gray
}

# =============================================================================
# 5. Test Connection
# =============================================================================

Write-Host ""
Write-Host "5. Testing connection to server..." -ForegroundColor Yellow

# --- Pre-check: TCP port 5985 reachable ---
Write-Host "  Checking port 5985 on $serverIP ..." -ForegroundColor Gray
$tcpOk = Test-NetConnection -ComputerName $serverIP -Port 5985 -WarningAction SilentlyContinue -InformationLevel Quiet
if (-not $tcpOk) {
    Write-Host "  [ERROR] Port 5985 (WinRM HTTP) is NOT reachable on $serverIP !" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Root cause: server firewall is blocking port 5985, or the server is offline." -ForegroundColor Yellow
    Write-Host "  Fix on server (run as Admin):" -ForegroundColor Yellow
    Write-Host "    netsh advfirewall firewall add rule name='WinRM-HTTP' protocol=TCP dir=in localport=5985 action=allow" -ForegroundColor Cyan
    Write-Host "  OR run: deployment\Setup-Server.ps1 (section 5 configures the firewall rule)" -ForegroundColor Cyan
    exit 1
}
Write-Host "  [OK] Port 5985 is reachable" -ForegroundColor Green

# --- Pre-check: WinRM service responds ---
Write-Host "  Testing WinRM service response..." -ForegroundColor Gray
try {
    Test-WSMan -ComputerName $serverIP -ErrorAction Stop | Out-Null
    Write-Host "  [OK] WinRM service is responding" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] WinRM service on $serverIP is NOT responding correctly!" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Root cause: WinRM is running (port 5985 open) but service auth is misconfigured." -ForegroundColor Yellow
    Write-Host "  Fix: Log in to server and run deployment\Setup-Server.ps1 as Administrator." -ForegroundColor Yellow
    Write-Host "  That script enables Negotiate auth and AllowUnencrypted on the WinRM service." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Manual fix (run on server as Admin):" -ForegroundColor Yellow
    Write-Host "    winrm quickconfig -force" -ForegroundColor Cyan
    Write-Host "    Set-Item WSMan:\localhost\Service\AllowUnencrypted -Value `$true -Force" -ForegroundColor Cyan
    Write-Host "    Set-Item WSMan:\localhost\Service\Auth\Negotiate -Value `$true -Force" -ForegroundColor Cyan
    Write-Host "    Restart-Service WinRM" -ForegroundColor Cyan
    exit 1
}

Write-Host "  Enter server credentials:" -ForegroundColor Cyan
$credential = Get-Credential -Message "Server credentials (e.g. Administrator)"

try {
    $result = Invoke-Command -ComputerName $serverIP -Credential $credential -Authentication Negotiate -ScriptBlock {
        $env:COMPUTERNAME
    } -ErrorAction Stop
    
    Write-Host "  [OK] Connected to server: $result" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Cannot connect to server!" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Diagnosis:" -ForegroundColor Yellow
    Write-Host "    - Port 5985 is open (checked above)" -ForegroundColor Gray
    Write-Host "    - WinRM service is responding (checked above)" -ForegroundColor Gray
    Write-Host "    - This is likely an authentication failure (wrong username/password," -ForegroundColor Gray
    Write-Host "      or the account doesn't have remote PS access)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Fix on server (run as Admin):" -ForegroundColor Yellow
    Write-Host "    Set-Item WSMan:\localhost\Service\Auth\Negotiate -Value `$true -Force" -ForegroundColor Cyan
    Write-Host "    Restart-Service WinRM" -ForegroundColor Cyan
    exit 1
}

# =============================================================================
# Summary
# =============================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Dev Machine Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Server IP:        $serverIP" -ForegroundColor White
Write-Host "  TrustedHosts:     $newTrustedHosts" -ForegroundColor White
Write-Host "  Connection Test:  SUCCESS" -ForegroundColor Green
Write-Host ""
Write-Host "Next Step:" -ForegroundColor Yellow
Write-Host "  Run: .\FirstDeploy-Manual.ps1" -ForegroundColor Cyan
Write-Host ""
