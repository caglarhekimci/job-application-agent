#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$scanRoot = Join-Path ([IO.Path]::GetTempPath()) ('jobagent-scan-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $scanRoot 'scripts') -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'scan-public.ps1') -Destination (Join-Path $scanRoot 'scripts/scan-public.ps1')
    & git -C $scanRoot init --quiet
    foreach ($name in @('private.pem', 'private.p12', 'id_private')) {
        $testFile = Join-Path $scanRoot $name
        Set-Content -LiteralPath $testFile -Value ('-----BEGIN ' + 'PRIVATE KEY-----')
        $output = & pwsh -NoProfile -File (Join-Path $scanRoot 'scripts/scan-public.ps1') 2>&1
        if ($LASTEXITCODE -eq 0) { throw "Scanner accepted forbidden fixture $name." }
        Remove-Item -LiteralPath $testFile
    }
    Set-Content -LiteralPath (Join-Path $scanRoot 'removed-secret') -Value ('-----BEGIN ' + 'PRIVATE KEY-----')
    & git -C $scanRoot add removed-secret
    & git -C $scanRoot -c user.name=Synthetic -c user.email=candidate@example.invalid commit --quiet -m 'Synthetic secret fixture'
    Remove-Item -LiteralPath (Join-Path $scanRoot 'removed-secret')
    & git -C $scanRoot add -u
    & git -C $scanRoot -c user.name=Synthetic -c user.email=candidate@example.invalid commit --quiet -m 'Remove fixture from working tree'
    $output = & pwsh -NoProfile -File (Join-Path $scanRoot 'scripts/scan-public.ps1') 2>&1
    if ($LASTEXITCODE -eq 0) { throw 'Scanner missed forbidden content in git history.' }
    Write-Host 'Source scan regression: 4/4 passed (PEM, P12, extensionless key, removed historical key).'
} finally {
    $resolved = [IO.Path]::GetFullPath($scanRoot)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'jobagent-scan-test-*') {
        throw 'Refusing cleanup outside the test temp directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
