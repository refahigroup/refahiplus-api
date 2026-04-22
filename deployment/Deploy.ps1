# Deploy.ps1
# Fast automated deployment to Production (Web API)
# Usage:
#   .\Deploy.ps1                    # Full deployment (restore + build + publish + deploy)
#   .\Deploy.ps1 -SkipRestore       # Skip restore step
#   .\Deploy.ps1 -SkipBuild         # Skip build step (use previous build)

param(
    [string]$ServerIP = "95.156.253.171",
    [string]$SiteName = "RefahiPlus-WebApi",
    [string]$ServerPath = "C:\Workspace\RUN\RefahiPlus-WebApi",
    [switch]$SkipBuild,
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "   Deploy API to Production" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date

# Get absolute paths based on script location
$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
# From: deployment/ → refahi-plus-api/ → services/ → refahi-plus/ → Refahi_WorkSpace/
$solutionRoot = Split-Path $scriptRoot -Parent

$projectPath = Join-Path $solutionRoot "src\Refahi.Api\Refahi.Api.csproj"
$publishPath = Join-Path $solutionRoot "src\Refahi.Api\bin\Release\net10.0\publish"

# Verify project exists
if (-not (Test-Path $projectPath)) {
    Write-Error "Project not found: $projectPath"
    Write-Host "Script Root: $scriptRoot" -ForegroundColor Yellow
    Write-Host "Solution Root: $solutionRoot" -ForegroundColor Yellow
    exit 1
}

# Check credentials
$credentialPath = Join-Path $env:USERPROFILE ".refahi\server-credential.xml"
if (-not (Test-Path $credentialPath)) {
    Write-Host "[ERROR] Credentials not found!" -ForegroundColor Red
    Write-Host "Run Setup-DevMachine.ps1 first" -ForegroundColor Yellow
    exit 1
}
$credential = Import-Clixml -Path $credentialPath

# Step 1: Restore
if (-not $SkipRestore) {
    Write-Host "Step 1: Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore $projectPath --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Restore failed!"
        exit 1
    }
    Write-Host "  [OK] Restored" -ForegroundColor Green
} else {
    Write-Host "Step 1: SKIPPED (Restore)" -ForegroundColor Gray
}

# Step 2: Build
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Step 2: Building..." -ForegroundColor Yellow
    dotnet build $projectPath -c Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
    Write-Host "  [OK] Built" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Step 2: SKIPPED (Build)" -ForegroundColor Gray
}

# Step 3: Publish
Write-Host ""
Write-Host "Step 3: Publishing..." -ForegroundColor Yellow

# When SkipBuild is used with Visual Studio publish, files are already in publish folder
# Check if we need to run publish or files already exist
$needsPublish = $true
if ($SkipBuild) {
    # Check if Visual Studio already published the files
    if ((Test-Path $publishPath) -and ((Get-ChildItem -Path $publishPath -File -ErrorAction SilentlyContinue).Count -gt 0)) {
        $needsPublish = $false
        $fileCount = (Get-ChildItem -Path $publishPath -Recurse -File).Count
        Write-Host "  [OK] Using existing publish: $fileCount files" -ForegroundColor Green
    }
}

if ($needsPublish) {
    if (Test-Path $publishPath) {
        Remove-Item $publishPath -Recurse -Force | Out-Null
    }
    
    dotnet publish $projectPath -c Release -o $publishPath --no-build --self-contained false --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [WARN] Publish with --no-build failed, trying with build..." -ForegroundColor Yellow
        dotnet publish $projectPath -c Release -o $publishPath --self-contained false --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Publish failed!"
            exit 1
        }
    }
    $fileCount = (Get-ChildItem -Path $publishPath -Recurse -File).Count
    Write-Host "  [OK] $fileCount files" -ForegroundColor Green
}

# Step 4: Package
Write-Host ""
Write-Host "Step 4: Creating package..." -ForegroundColor Yellow
$zipPath = Join-Path $env:TEMP "deploy-api-$(Get-Date -Format 'yyyyMMddHHmmss').zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -CompressionLevel Fastest
$zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "  [OK] $zipSizeMB MB" -ForegroundColor Green

# Step 5: Deploy
Write-Host ""
Write-Host "Step 5: Deploying..." -ForegroundColor Yellow

try {
    # Pre-check 1: TrustedHosts must include the server IP
    $trustedHosts = (Get-Item WSMan:\localhost\Client\TrustedHosts -ErrorAction SilentlyContinue).Value
    if ($trustedHosts -ne "*" -and $trustedHosts -notlike "*$ServerIP*") {
        Write-Host "  [ERROR] Server IP ($ServerIP) is not in TrustedHosts!" -ForegroundColor Red
        Write-Host "  Current TrustedHosts: '$trustedHosts'" -ForegroundColor Yellow
        Write-Host "  Fix: Run deployment\Setup-DevMachine.ps1 as Administrator and add the server IP." -ForegroundColor Yellow
        exit 1
    }

    # Pre-check 2: WinRM port (5985) must be reachable
    Write-Host "  Checking WinRM connectivity (port 5985)..." -ForegroundColor Gray
    $tcpTest = Test-NetConnection -ComputerName $ServerIP -Port 5985 -WarningAction SilentlyContinue -InformationLevel Quiet
    if (-not $tcpTest) {
        Write-Host "  [ERROR] Cannot reach $ServerIP on port 5985 (WinRM HTTP)!" -ForegroundColor Red
        Write-Host "  Check: server is online, firewall allows port 5985, server IP is correct." -ForegroundColor Yellow
        exit 1
    }

    # Create PSSession — use Negotiate (NTLM) explicitly; Kerberos does not work for standalone servers
    Write-Host "  Connecting to server..." -ForegroundColor Gray
    $session = New-PSSession -ComputerName $ServerIP -Credential $credential -Authentication Negotiate
    
    # Upload ZIP using Copy-Item -ToSession (no byte array, no size limit issues)
    $remoteTempZip = Invoke-Command -Session $session -ScriptBlock {
        Join-Path $env:TEMP "deploy-api-temp-$(Get-Date -Format 'yyyyMMddHHmmss').zip"
    }
    
    Write-Host "  Uploading package ($zipSizeMB MB)..." -ForegroundColor Gray
    Copy-Item -Path $zipPath -Destination $remoteTempZip -ToSession $session -Force
    Write-Host "  [OK] Uploaded" -ForegroundColor Green
    
    # Extract and deploy on server
    Write-Host "  Extracting and deploying..." -ForegroundColor Gray
    $result = Invoke-Command -Session $session -ScriptBlock {
        param($siteName, $sitePath, $remoteZip)
        
        Import-Module WebAdministration -ErrorAction Stop
        
        # Stop
        Stop-WebAppPool -Name $siteName -ErrorAction SilentlyContinue | Out-Null
        Start-Sleep -Seconds 2
        
        # Backup web.config (preserve environment variables)
        $webConfigPath = Join-Path $sitePath "web.config"
        $webConfigBackup = $null
        if (Test-Path $webConfigPath) {
            $webConfigBackup = Join-Path $env:TEMP "web.config.backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
            Copy-Item -Path $webConfigPath -Destination $webConfigBackup -Force
        }
        
        # Backup entire site
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupPath = "C:\inetpub\backups\$siteName-$timestamp"
        if (Test-Path $sitePath) {
            if ((Get-ChildItem $sitePath -File -Recurse -ErrorAction SilentlyContinue).Count -gt 0) {
                New-Item -ItemType Directory -Path (Split-Path $backupPath) -Force -ErrorAction SilentlyContinue | Out-Null
                Copy-Item -Path $sitePath -Destination $backupPath -Recurse -Force | Out-Null
            }
        }
        
        # Clear and extract
        if (Test-Path $sitePath) {
            Get-ChildItem -Path $sitePath -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
        } else {
            New-Item -Path $sitePath -ItemType Directory -Force | Out-Null
        }
        
        Expand-Archive -Path $remoteZip -DestinationPath $sitePath -Force
        Remove-Item $remoteZip -Force
        
        # Restore web.config (with production environment variables)
        if ($webConfigBackup -and (Test-Path $webConfigBackup)) {
            Copy-Item -Path $webConfigBackup -Destination $webConfigPath -Force
            Remove-Item $webConfigBackup -Force
        }
        
        # Permissions
        $acl = Get-Acl $sitePath
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
        $acl.SetAccessRule($rule)
        Set-Acl $sitePath $acl
        
        # Start
        Start-WebAppPool -Name $siteName | Out-Null
        Start-Sleep -Seconds 3
        
        return @{
            Files = (Get-ChildItem -Path $sitePath -Recurse -File).Count
            State = (Get-WebAppPoolState -Name $siteName).Value
            Backup = $backupPath
        }
        
    } -ArgumentList $SiteName, $ServerPath, $remoteTempZip
    
    # Close session
    Remove-PSSession -Session $session
    
    Write-Host "  [OK] Deployed $($result.Files) files" -ForegroundColor Green
    Write-Host "  [OK] App Pool: $($result.State)" -ForegroundColor Green
    Write-Host "  Backup: $($result.Backup)" -ForegroundColor Gray
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if ($session) { Remove-PSSession -Session $session -ErrorAction SilentlyContinue }
    exit 1
} finally {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
}

# Done
$elapsed = (Get-Date) - $startTime
Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host " SUCCESS! ($([math]::Round($elapsed.TotalSeconds, 1))s)" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Test: http://refahiplus.com/api" -ForegroundColor Cyan
Write-Host ""
