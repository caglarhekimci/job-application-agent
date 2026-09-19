#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'use-toolchain.ps1')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 ASP.NET runtime is required. See docs/guides/INSTALL.md.' }
& dotnet (Join-Path $PSScriptRoot 'cli/JobAgent.Cli.dll') demo
if ($LASTEXITCODE -ne 0) { throw 'Demo exited with an error.' }
