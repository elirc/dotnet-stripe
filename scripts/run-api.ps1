$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet run --project src/Billing.Api --no-launch-profile --urls http://127.0.0.1:5088
    if ($LASTEXITCODE -ne 0) { throw 'API stopped with an error.' }
} finally { Pop-Location }
