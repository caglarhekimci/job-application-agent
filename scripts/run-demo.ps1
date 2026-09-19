#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    . ./scripts/use-toolchain.ps1
    $entry = './src/JobAgent.Cli/bin/Release/net10.0/JobAgent.Cli.dll'
    if (-not (Test-Path $entry)) { throw 'Build is missing. Run scripts/bootstrap.ps1 first.' }
    & dotnet $entry demo
    if ($LASTEXITCODE -ne 0) { throw 'Demo exited with an error.' }
} finally { Pop-Location }
