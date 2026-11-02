$ErrorActionPreference = 'Stop'
$apiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtime = Join-Path $apiRoot 'artifacts\media-local'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
foreach ($port in @(5000,5001)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use; stop the running local API first."
    }
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'https://localhost:5001;http://localhost:5000'
$env:ConnectionStrings__Default = 'Host=localhost;Database=refahi-media-local;Username=postgres;Password=postgres'
$env:ExternalMedia__PublicBaseUrl = 'https://localhost:5001'
$env:ExternalMedia__CachePath = Join-Path $runtime 'cache'
$env:ExternalMedia__MaxBytes = '134217728'
$env:ExternalMedia__MinFreeBytes = '2147483648'
$env:Hotels__Providers__SnappTrip__PurchaseEnabled = 'false'
$env:Hotels__Providers__Lamasoo__PurchaseEnabled = 'false'
$env:Commerce__Providers__TouristPanel__Enabled = 'false'
$env:Commerce__Providers__Aabsar__Enabled = 'false'
$env:NotificationService__UseInMemory = 'true'
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command', 'Warning', 'Process')
$project = Join-Path $apiRoot 'src\Refahi.Api'
if (!(Test-Path (Join-Path $project 'bin\Debug\net10.0\Refahi.Api.dll'))) {
    throw 'Build Refahi.Backend.slnx in Debug before running this script.'
}
Push-Location $project
try { dotnet (Join-Path $project 'bin\Debug\net10.0\Refahi.Api.dll') }
finally { Pop-Location }
