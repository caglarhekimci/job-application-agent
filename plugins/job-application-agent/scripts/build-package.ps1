#Requires -Version 7.0
param([Parameter(Mandatory)][string]$McpDirectory)
$ErrorActionPreference = 'Stop'
$pluginRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = [IO.Path]::GetFullPath((Join-Path $pluginRoot '../..'))
$McpDirectory = (Resolve-Path -LiteralPath $McpDirectory).Path
foreach ($name in @('JobAgent.Mcp.dll', 'JobAgent.Mcp.deps.json', 'JobAgent.Mcp.runtimeconfig.json',
    'JobAgent.DocumentWorker.dll', 'JobAgent.DocumentWorker.deps.json', 'JobAgent.DocumentWorker.runtimeconfig.json', 'UglyToad.PdfPig.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $McpDirectory $name) -PathType Leaf)) { throw ('Missing published MCP artifact: ' + $name) }
}
$pdfLicensePath = Join-Path $repoRoot 'third-party-notices/PdfPig-0.1.16-LICENSE'
if (-not (Test-Path -LiteralPath $pdfLicensePath -PathType Leaf)) { throw 'Missing required PdfPig license text.' }
$inputEntries = @(Get-ChildItem -LiteralPath $McpDirectory -Recurse -Force)
foreach ($entry in $inputEntries) {
    if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $entry.Name -match '\.(db(?:-.*)?|sqlite[23]?(?:-(?:wal|shm|journal))?|pfx|p12|pem|key)$' -or $entry.Name -like '.env*' -or
        $entry.FullName -match '[\\/](browser-state|\.auth)[\\/]') { throw 'Forbidden private artifact or link in MCP package input.' }
}
$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$outputRoot = Join-Path $repoRoot ('artifacts/plugin-packages/' + $stamp)
if (Test-Path -LiteralPath $outputRoot) { throw 'Package directory already exists.' }
$outputPlugin = Join-Path $outputRoot 'job-application-agent'
New-Item -ItemType Directory -Path $outputPlugin -Force | Out-Null
# Copy the reviewed source package and add runtime binaries only to ignored artifacts.
foreach ($name in @('plugin.json', 'mcp.json', '.mcp.json', '.codex-plugin', 'README.md', 'PRIVACY.md', 'REVIEW_CASES.md', 'scripts')) {
    Copy-Item -LiteralPath (Join-Path $pluginRoot $name) -Destination $outputPlugin -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $outputPlugin
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $outputPlugin
$noticeRoot = Join-Path $outputPlugin 'third-party-notices'
New-Item -ItemType Directory -Path $noticeRoot | Out-Null
Copy-Item -LiteralPath $pdfLicensePath -Destination $noticeRoot
$outputRuntime = Join-Path $outputPlugin 'runtime'
New-Item -ItemType Directory -Path $outputRuntime | Out-Null
foreach ($file in $inputEntries | Where-Object { -not $_.PSIsContainer -and $_.Extension -ne '.pdb' }) {
    $relative = [IO.Path]::GetRelativePath($McpDirectory, $file.FullName)
    $destination = Join-Path $outputRuntime $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
$files = @(Get-ChildItem -LiteralPath $outputPlugin -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($outputPlugin, $_.FullName).Replace('\', '/'); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$files | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPlugin 'MANIFEST.json') -Encoding utf8
$archive = Join-Path (Split-Path $outputRoot -Parent) ('job-application-agent-local-' + $stamp + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($outputRoot, $archive)
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($archive + '.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($archive)) -Encoding utf8
[pscustomobject]@{ PluginRoot = $outputPlugin; Archive = $archive; Sha256 = $hash; FileCount = $files.Count }
