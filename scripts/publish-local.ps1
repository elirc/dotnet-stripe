param([switch]$SkipInstall)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $projectRoot 'src/Billing.Web'
$apiRoot = Join-Path $projectRoot 'src/Billing.Api'
$outputRoot = Join-Path $projectRoot '.artifacts/publish'
Push-Location $webRoot
try {
    if (-not $SkipInstall) {
        npm.cmd ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency install failed.' }
    }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    $staticRoot = Join-Path $apiRoot 'wwwroot'
    New-Item -ItemType Directory -Path $staticRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $webRoot 'dist/*') -Destination $staticRoot -Recurse -Force
    dotnet publish (Join-Path $apiRoot 'Billing.Api.csproj') -c Release -o $outputRoot --nologo -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Backend publish failed.' }
    Write-Host "Local package created at $outputRoot"
    Write-Host 'Run from that directory: dotnet Billing.Api.dll --urls http://127.0.0.1:5088'
    Write-Host 'No site was deployed. App_Data is excluded; a fresh database is created on first start.'
} finally { Pop-Location }
