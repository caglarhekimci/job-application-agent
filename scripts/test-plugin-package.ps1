#Requires -Version 7.0
param([string]$McpDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$pluginRoot = Join-Path $repoRoot 'plugins/job-application-agent'
if (-not $McpDirectory) { $McpDirectory = Join-Path $repoRoot 'src/JobAgent.Mcp/bin/Release/net10.0' }
$manifest = Get-Content -LiteralPath (Join-Path $pluginRoot 'plugin.json') -Raw | ConvertFrom-Json -AsHashtable
$legacy = Get-Content -LiteralPath (Join-Path $pluginRoot '.codex-plugin/plugin.json') -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.name -ne 'job-application-agent' -or $manifest.name -ne $legacy.name -or $manifest.version -ne $legacy.version) {
    throw 'Portable and compatibility plugin identities must agree.'
}
$mcp = Get-Content -LiteralPath (Join-Path $pluginRoot 'mcp.json') -Raw | ConvertFrom-Json -AsHashtable
$legacyMcp = Get-Content -LiteralPath (Join-Path $pluginRoot '.mcp.json') -Raw | ConvertFrom-Json -AsHashtable
if ($mcp.mcpServers.Count -ne 1 -or $legacyMcp.mcpServers.Count -ne 1) { throw 'Expected one bounded local MCP server.' }
$server = $mcp.mcpServers.job_agent
if ($server.type -ne 'stdio' -or $server.command -ne 'pwsh' -or $server.cwd -ne './' -or
    ($server.args -join '|') -ne '-NoLogo|-NoProfile|-File|./scripts/start-mcp.ps1') {
    throw 'Plugin must use its contained launcher, without shell expression expansion.'
}
if (($legacyMcp.mcpServers.job_agent | ConvertTo-Json -Compress) -ne ($server | ConvertTo-Json -Compress)) {
    throw 'Portable and legacy MCP launch configuration drifted.'
}
if (Test-Path -LiteralPath (Join-Path $pluginRoot 'hooks')) { throw 'This read-only package must not run lifecycle hooks.' }
Write-Host 'PASS: Portable/compatibility manifests and contained STDIO launch contract.'

$package = & (Join-Path $pluginRoot 'scripts/build-package.ps1') -McpDirectory $McpDirectory
$package = $package | Select-Object -Last 1
if (-not (Test-Path -LiteralPath $package.Archive)) { throw 'Plugin archive was not created.' }
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('jobagent-plugin-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
$process = $null
try {
    $extracted = Join-Path $temporaryRoot 'relocated package'
    [IO.Compression.ZipFile]::ExtractToDirectory($package.Archive, $extracted)
    $relocatedPlugin = Join-Path $extracted 'job-application-agent'
    if (-not (Test-Path -LiteralPath (Join-Path $relocatedPlugin '.codex-plugin/plugin.json'))) { throw 'Archive omitted hidden manifest.' }
    $pdfLicense = Join-Path $relocatedPlugin 'third-party-notices/PdfPig-0.1.16-LICENSE'
    if (-not (Test-Path -LiteralPath $pdfLicense) -or
        (Get-FileHash -LiteralPath $pdfLicense).Hash -ne (Get-FileHash -LiteralPath (Join-Path $repoRoot 'third-party-notices/PdfPig-0.1.16-LICENSE')).Hash) {
        throw 'Archive omitted or modified the required PdfPig license text.'
    }
    foreach ($name in @('JobAgent.DocumentWorker.dll', 'JobAgent.DocumentWorker.deps.json', 'JobAgent.DocumentWorker.runtimeconfig.json', 'UglyToad.PdfPig.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $relocatedPlugin ('runtime/' + $name)))) { throw ('Archive omitted required document worker artifact: ' + $name) }
    }
    Write-Host 'PASS: Archive preserves the exact PdfPig license and required document worker files.'
    $runtime = Join-Path $temporaryRoot 'empty-runtime'
    New-Item -ItemType Directory -Path $runtime | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
    $start.WorkingDirectory = $relocatedPlugin
    foreach ($argument in $server.args) { $start.ArgumentList.Add($argument) }
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['JOBAGENT_RUNTIME_DIR'] = $runtime
    # Ambient workflow opt-in must not widen this explicitly read-only package.
    $start.Environment['JOBAGENT_ENABLE_SYNTHETIC_COMMANDS'] = '1'
    # Exercise the installed-host fallback: no SDK under LOCALAPPDATA and more
    # than one dotnet executable on PATH. Only the first executable may run.
    $fallbackLocalData = Join-Path $temporaryRoot 'isolated-local-data'
    $sentinelDirectory = Join-Path $temporaryRoot 'second-dotnet'
    New-Item -ItemType Directory -Path $fallbackLocalData, $sentinelDirectory | Out-Null
    Set-Content -LiteralPath (Join-Path $sentinelDirectory 'dotnet.exe') -Value 'Never execute this second PATH candidate.'
    $primaryDotnet = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $primaryDotnet)) {
        $primaryDotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
    }
    $start.Environment['LOCALAPPDATA'] = $fallbackLocalData
    $start.Environment['PATH'] = (Split-Path $primaryDotnet -Parent) + ';' + $sentinelDirectory + ';' + $env:PATH
    $process = [Diagnostics.Process]::Start($start)
    $stderr = $process.StandardError.ReadToEndAsync()
    function Send-Rpc([hashtable]$Message) {
        $process.StandardInput.WriteLine(($Message | ConvertTo-Json -Depth 10 -Compress))
        $process.StandardInput.Flush()
    }
    function Receive-Rpc([int]$Id) {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while ($timer.Elapsed.TotalSeconds -lt 30) {
            $lineTask = $process.StandardOutput.ReadLineAsync()
            if (-not $lineTask.Wait(5000)) { throw 'Packaged MCP response timed out.' }
            $line = $lineTask.GetAwaiter().GetResult()
            if ($null -eq $line) { throw 'Packaged MCP ended before its response.' }
            $message = $line | ConvertFrom-Json -AsHashtable
            if ($message.id -eq $Id) { return $message }
        }
        throw 'Packaged MCP response was not found.'
    }
    Send-Rpc @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{ protocolVersion = '2025-11-25'; capabilities = @{}; clientInfo = @{ name = 'jobagent-plugin-offline-test'; version = '0.1.0' } } }
    $initialized = Receive-Rpc 1
    if ($initialized.error -or -not $initialized.result.protocolVersion) { throw 'Packaged MCP initialize failed.' }
    Send-Rpc @{ jsonrpc = '2.0'; method = 'notifications/initialized' }
    Send-Rpc @{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} }
    $toolsResponse = Receive-Rpc 2
    $names = @($toolsResponse.result.tools | ForEach-Object { $_.name } | Sort-Object)
    if (($names -join '|') -ne 'application_get_status|profile_get_summary|runtime_get_capabilities') { throw 'Packaged tool surface differs from the reviewed three-tool contract.' }
    foreach ($tool in $toolsResponse.result.tools) {
        if ($tool.annotations.readOnlyHint -ne $true -or $tool.annotations.openWorldHint -ne $false) { throw 'Incorrect tool annotations.' }
    }
    Write-Host 'PASS: Relocated archive starts, initializes, and lists exactly three read-only tools.'
    Write-Host 'PASS: Ambient synthetic-command opt-in cannot expose writes through the inspector package.'
    Write-Host 'PASS: Isolated LOCALAPPDATA and multiple dotnet PATH candidates select the first runtime.'
    Send-Rpc @{ jsonrpc = '2.0'; id = 3; method = 'tools/call'; params = @{ name = 'runtime_get_capabilities'; arguments = @{} } }
    $call = Receive-Rpc 3
    $capabilities = $call.result.structuredContent
    if (-not $capabilities) { $capabilities = $call.result.content[0].text | ConvertFrom-Json -AsHashtable }
    if ($call.error -or $call.result.isError -or $capabilities.mode -ne 'Fixture' -or
        $capabilities.paidApiEnabled -ne $false -or $capabilities.canMintApproval -ne $false -or $capabilities.linkedIn -ne 'Blocked') {
        throw 'Packaged runtime capabilities failed.'
    }
    Send-Rpc @{ jsonrpc = '2.0'; id = 4; method = 'tools/call'; params = @{ name = 'profile_get_summary'; arguments = @{ profileRef = '../private.db' } } }
    $invalid = Receive-Rpc 4
    if (-not $invalid.error -and -not $invalid.result.isError) { throw 'Packaged server accepted a path as a profile reference.' }
    if (@(Get-ChildItem -LiteralPath $runtime -Force).Count -ne 0) { throw 'Capability/error checks modified the empty runtime.' }
    Write-Host 'PASS: Actual capability call, invalid-path rejection, and empty runtime unchanged.'
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(5000)) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
    $process = $null

    $missingPackage = Join-Path $temporaryRoot 'missing-runtime'
    New-Item -ItemType Directory -Path (Join-Path $missingPackage 'scripts') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $pluginRoot 'scripts/start-mcp.ps1') -Destination (Join-Path $missingPackage 'scripts/start-mcp.ps1')
    $failed = & pwsh -NoProfile -File (Join-Path $missingPackage 'scripts/start-mcp.ps1') 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $failed -notmatch 'packaged runtime is missing') { throw 'Missing runtime did not fail with the required setup message.' }
    Write-Host 'PASS: Missing runtime fails closed with an actionable setup message.'

    $unsafeInput = Join-Path $temporaryRoot 'unsafe-package-source'
    New-Item -ItemType Directory -Path $unsafeInput | Out-Null
    foreach ($name in @('JobAgent.Mcp.dll', 'JobAgent.Mcp.deps.json', 'JobAgent.Mcp.runtimeconfig.json',
        'JobAgent.DocumentWorker.dll', 'JobAgent.DocumentWorker.deps.json', 'JobAgent.DocumentWorker.runtimeconfig.json', 'UglyToad.PdfPig.dll', 'test-secret.pem')) {
        Set-Content -LiteralPath (Join-Path $unsafeInput $name) -Value 'synthetic packaging rejection fixture'
    }
    $rejected = $false
    try { & (Join-Path $pluginRoot 'scripts/build-package.ps1') -McpDirectory $unsafeInput | Out-Null }
    catch { $rejected = $_.Exception.Message -match 'Forbidden' }
    if (-not $rejected) { throw 'Packaging accepted a private-key extension.' }
    Write-Host 'PASS: Packaging rejects forbidden private artifacts.'
    Remove-Item -LiteralPath (Join-Path $unsafeInput 'test-secret.pem')
    Remove-Item -LiteralPath (Join-Path $unsafeInput 'JobAgent.DocumentWorker.runtimeconfig.json')
    $rejected = $false
    try { & (Join-Path $pluginRoot 'scripts/build-package.ps1') -McpDirectory $unsafeInput | Out-Null }
    catch { $rejected = $_.Exception.Message -match 'Missing published MCP artifact: JobAgent.DocumentWorker.runtimeconfig.json' }
    if (-not $rejected) { throw 'Packaging accepted an incomplete document worker.' }
    Write-Host 'PASS: Packaging rejects an incomplete document worker.'
    Write-Host ('Plugin package checks passed. Archive: ' + $package.Archive)
} finally {
    if ($process) { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
    $resolved = [IO.Path]::GetFullPath($temporaryRoot)
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'jobagent-plugin-test-*') { throw 'Refusing cleanup outside owned plugin test directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
