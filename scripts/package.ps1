#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    . ./scripts/use-toolchain.ps1
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $packageRoot = Join-Path (Get-Location) "artifacts/packages/job-application-agent-$stamp"
    if (Test-Path $packageRoot) { throw 'Package path already exists; refusing overwrite.' }
    New-Item -ItemType Directory -Path $packageRoot | Out-Null
    & npm run build --prefix web
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    foreach ($project in @('Cli', 'Mcp')) {
        & dotnet publish "src/JobAgent.$project/JobAgent.$project.csproj" -c Release --no-restore --self-contained false -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $packageRoot $project.ToLowerInvariant())
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
        foreach ($required in @('JobAgent.DocumentWorker.dll', 'JobAgent.DocumentWorker.deps.json', 'JobAgent.DocumentWorker.runtimeconfig.json', 'UglyToad.PdfPig.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path (Join-Path $packageRoot $project.ToLowerInvariant()) $required))) {
                throw "Document worker dependency missing: $project/$required"
            }
        }
    }
    foreach ($document in @('LICENSE', 'README.md', 'README.tr.md', 'THIRD_PARTY_NOTICES.md')) {
        Copy-Item -LiteralPath $document -Destination $packageRoot
    }
    Copy-Item -LiteralPath docs -Destination (Join-Path $packageRoot 'docs') -Recurse
    Copy-Item -LiteralPath scripts/package-launch.ps1 -Destination (Join-Path $packageRoot 'run-demo.ps1')
    Copy-Item -LiteralPath scripts/use-toolchain.ps1 -Destination $packageRoot
    $noticeRoot = Join-Path $packageRoot 'third-party-notices'
    New-Item -ItemType Directory -Path $noticeRoot | Out-Null
    Copy-Item -LiteralPath third-party-notices/PdfPig-0.1.16-LICENSE -Destination $noticeRoot
    foreach ($module in @('react', 'react-dom', 'scheduler')) {
        Copy-Item -LiteralPath "web/node_modules/$module/LICENSE" -Destination (Join-Path $noticeRoot "$module-LICENSE")
    }
    # Keep upstream notices already embedded in Playwright's driver distribution.
    $forbidden = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse -Force | Where-Object {
        $_.Name -match '\.(db|sqlite|pfx|pem|p12|key|pdb)$' -or $_.Name -like '.env*' -or $_.FullName -match '[\\/](browser-state|\.auth)[\\/]'
    })
    if ($forbidden.Count) { throw 'A forbidden runtime/private/debug file entered the package.' }
    $manifest = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = [IO.Path]::GetRelativePath($packageRoot, $_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageRoot 'MANIFEST.json') -Encoding utf8
    $zipPath = "$packageRoot.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath)
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$zipPath.sha256" -Value "$zipHash  $([IO.Path]::GetFileName($zipPath))"
    Write-Host "Local package only: $zipPath"
    Write-Host "SHA256: $zipHash"
} finally { Pop-Location }
