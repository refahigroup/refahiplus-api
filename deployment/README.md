# Refahi Plus Web API - Deployment Guide

## Overview
ASP.NET Core Web API deployment to Windows Server IIS.

## Architecture
- **Type:**  ASP.NET Core Web API
- **Server:** Windows Server 2019/2022 with IIS 10
- **Domain:** api.refahiplus.ir (or refahiplus.com/api)
- **Port:** 80 (HTTP) or 443 (HTTPS)

## Prerequisites
- .NET 10.0 SDK
- Windows Server with IIS 10+
- PowerShell 5.1 or later
- PowerShell Remoting enabled on server

---

## Initial Setup

### 1. Server Setup (One-time)
On your **development machine**, run:

```powershell
cd deployment/web-api
.\Setup-Server.ps1
```

This will:
- Configure IIS on remote server
- Create Website and App Pool
- Set up bindings
- Configure permissions
- Install required IIS features

**Server Info:**
- IP: 95.156.253.171
- Site Name: RefahiPlus-API
- App Pool: RefahiPlus-API
- Path: C:\Workspace\RUN\RefahiPlus-API

### 2. Dev Machine Setup (One-time)
Save server credentials securely:

```powershell
.\Setup-DevMachine.ps1
```

This will:
- Prompt for server credentials
- Save encrypted credentials to `~\.refahi\server-credential.xml` 
- Test PowerShell Remoting connection

---

## Daily Deployment

### Quick Deploy
```powershell
.\Deploy.ps1
```

### Options
```powershell
# Skip restore (faster if no package changes)
.\Deploy.ps1 -SkipRestore

# Skip build (use previous build)
.\Deploy.ps1 -SkipBuild
```

### What Deploy.ps1 Does
1. **Restore**: dotnet restore (optional)
2. **Build**: dotnet build in Release mode (optional)
3. **Publish**: dotnet publish to bin/Release/net10.0/publish
4. **Package**: Creates ZIP archive
5. **Upload**: Transfers to server via PowerShell Remoting
6. **Deploy**: 
   - Stops App Pool
   - Creates backup
   - Extracts files
   - Sets permissions
   - Starts App Pool

### Typical Deployment Time
- Full deploy: ~3-4 minutes  
- With -SkipRestore: ~2-3 minutes
- With -SkipBuild: ~1-2 minutes

---

## Visual Studio Integration

Right-click project → Publish → Use profile: **Production-API**

This will automatically run Deploy.ps1 after publish completes.

---

## Testing API

After deployment:
```
http://refahiplus.com/api/health
http://api.refahiplus.ir/health
```

Or test with PowerShell:
```powershell
Invoke-RestMethod -Uri "http://refahiplus.com/api/health"
```

---

## Troubleshooting

### Deployment Fails
```powershell
# Test connection  
Test-WSMan -ComputerName 95.156.253.171

# Re-run dev setup
.\Setup-DevMachine.ps1
```

### API Not Responding
1. Check App Pool status on server
2. Check IIS logs: `C:\Workspace\RUN\RefahiPlus-API\logs\`
3. Verify web.config exists in root folder
4. Test direct server access: `http://95.156.253.171/api/health`

### CORS Issues
Check web.config and Startup.cs for CORS configuration.

---

## File Structure

```
deployment/web-api/
├── Setup-Server.ps1        # IIS setup on remote server
├── Setup-DevMachine.ps1    # Save credentials locally  
├── Deploy.ps1              # Daily deployment script
└── README.md               # This file

Project: ../../refahi-plus/services/refahi-plus-api/
Entry Point: src/Refahi.Api/Refahi.Api.csproj
Published: src/Refahi.Api/bin/Release/net10.0/publish/
```

---

## Security Notes

- Credentials stored encrypted in `%USERPROFILE%\.refahi\`
- PowerShell Remoting uses WinRM (ports 5985/5986)
- SMB/File Sharing NOT required (secure)
- Automatic backup before each deployment

---

## Related Documentation

- [T-202 Implementation Summary](../../docs/implementation-summaries/T-202-IMPLEMENTATION.md)
- [Wallet Architecture](../../docs/wallet-architecture.md)
- [API Endpoints](../../docs/identity-api-endpoints.md)

---

## Support

For issues or questions, refer to project documentation in `docs/` folder.
