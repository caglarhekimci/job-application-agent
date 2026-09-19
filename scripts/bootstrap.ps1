#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if (-not $IsWindows) { throw 'This installer is currently verified on Windows only.' }
    . ./scripts/use-toolchain.ps1
    $sdkVersion = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    $available = $false
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $available = (& dotnet --list-sdks) -match "^$([regex]::Escape($sdkVersion)) "
    }
    if (-not $available) {
        $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
        $sdk = @($metadata.releases | ForEach-Object { $_.sdks } | Where-Object version -eq $sdkVersion)[0]
        if (-not $sdk) { throw "Pinned SDK $sdkVersion was not found in official release metadata." }
        $archive = @($sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' })[0]
        $sdkRoot = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/dotnet'
        $downloadPath = Join-Path ([IO.Path]::GetTempPath()) "jobagent-sdk-$sdkVersion.zip"
        Write-Host "Installing pinned .NET SDK $sdkVersion per-user; no administrator access."
        Invoke-WebRequest $archive.url -OutFile $downloadPath
        if ((Get-FileHash $downloadPath -Algorithm SHA512).Hash -ne $archive.hash) { throw 'SDK checksum mismatch.' }
        New-Item -ItemType Directory -Path $sdkRoot -Force | Out-Null
        Expand-Archive -LiteralPath $downloadPath -DestinationPath $sdkRoot -Force
        . ./scripts/use-toolchain.ps1
    }
    foreach ($required in @('node', 'npm', 'git')) {
        if (-not (Get-Command $required -ErrorAction SilentlyContinue)) {
            throw "$required is required. See docs/guides/INSTALL.md."
        }
    }
    & node --version
    if ($LASTEXITCODE -ne 0) { throw 'Node.js is required. See docs/guides/INSTALL.md.' }
    & npm ci --prefix web --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Frontend restore failed.' }
    & npm run build --prefix web
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    & dotnet restore JobAgent.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked dependency restore failed.' }
    & dotnet build JobAgent.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & ./tests/JobAgent.E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium
    if ($LASTEXITCODE -ne 0) { throw 'Chromium installation failed.' }
    Write-Host 'Local dependencies ready. Run scripts/verify.ps1, then scripts/run-demo.ps1.'
} finally { Pop-Location }
