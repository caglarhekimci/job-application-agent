#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    . ./scripts/use-toolchain.ps1
    foreach ($required in @('dotnet', 'node', 'npm')) {
        if (-not (Get-Command $required -ErrorAction SilentlyContinue)) {
            throw "$required is missing. See docs/guides/INSTALL.md."
        }
    }
    $expected = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -ne $expected) { throw "Pinned SDK $expected missing. Run scripts/bootstrap.ps1." }
    $node = & node --version
    if ($LASTEXITCODE -ne 0) { throw 'Node.js is missing.' }
    $npm = & npm --version
    if ($LASTEXITCODE -ne 0) { throw 'npm is missing.' }
    $browserRoot = if ($env:PLAYWRIGHT_BROWSERS_PATH) { $env:PLAYWRIGHT_BROWSERS_PATH } else { Join-Path $env:LOCALAPPDATA 'ms-playwright' }
    if (-not (Test-Path (Join-Path $browserRoot 'chromium_headless_shell-1234/chrome-headless-shell-win64/chrome-headless-shell.exe'))) {
        throw 'Compatible Chromium is missing. Run scripts/bootstrap.ps1.'
    }
    if (-not (Test-Path web/dist/index.html)) { throw 'Frontend is not built. Run scripts/bootstrap.ps1.' }
    if (-not (Test-Path src/JobAgent.Cli/bin/Release/net10.0/wwwroot/index.html)) {
        throw 'Launcher static assets are missing. Run scripts/bootstrap.ps1.'
    }
    Write-Host "SDK $sdk | Node $node | npm $npm | Playwright Chromium 1234 present"
    Write-Host 'Mode: Fixture only. API spend: 0. LinkedIn: blocked. Real model/host: not verified.'
    Write-Host 'Windows profiles use current-user DPAPI; application journal contains synthetic data only.'
    Write-Host 'Full browser launch, storage and workflow validation: scripts/verify.ps1.'
} finally { Pop-Location }
