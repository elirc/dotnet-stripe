$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet test tests/Billing.Tests/Billing.Tests.csproj --nologo -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    Push-Location (Join-Path $projectRoot 'src/Billing.Web')
    try {
        npm.cmd ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency install failed.' }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend type check or build failed.' }
    } finally { Pop-Location }
} finally { Pop-Location }
