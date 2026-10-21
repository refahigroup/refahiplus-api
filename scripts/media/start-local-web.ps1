param([string]$WebRoot = 'C:\Workspace\repo\refahiplus-webapp')
$ErrorActionPreference = 'Stop'
foreach ($port in @(5270,5271)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use; stop the running local web app first."
    }
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'https://localhost:5271;http://localhost:5270'
$env:ApiBaseUrl = 'https://localhost:5001'
$project = Join-Path $WebRoot 'src\Refahi.Clients.Web'
if (!(Test-Path (Join-Path $project 'bin\Debug\net10.0\Refahi.Clients.Web.dll'))) {
    throw 'Build Refahi.Clients.Solution.sln in Debug before running this script.'
}
Push-Location $project
try { dotnet (Join-Path $project 'bin\Debug\net10.0\Refahi.Clients.Web.dll') }
finally { Pop-Location }
