#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Replay')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Live')][switch]$RunLive,
    [Parameter(Mandatory, ParameterSetName = 'Replay')][string]$EvidenceLog,
    [Parameter(ParameterSetName = 'Live')][string]$CodexPath = '',
    [Parameter(ParameterSetName = 'Live')][string]$Model = 'gpt-6-astra',
    [Parameter(ParameterSetName = 'Live')][ValidateRange(30, 300)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

function Assert-HostEvidence([string]$Path) {
    $events = @(Get-Content -LiteralPath $Path | Where-Object { $_.StartsWith('{') } | ForEach-Object { ConvertFrom-Json -InputObject $_ -AsHashtable })
    $calls = @($events | Where-Object { $_.type -eq 'item.completed' -and $_.item.type -eq 'mcp_tool_call' })
    if ($calls.Count -ne 1) { throw 'Expected exactly one completed MCP call; a model statement alone is not verification.' }
    $call = $calls[0].item
    if ($call.server -ne 'job_agent' -or $call.tool -ne 'runtime_get_capabilities' -or
        $call.status -ne 'completed' -or $call.error -or $call.arguments.Count -ne 0) {
        throw 'The capability tool did not complete successfully with empty arguments.'
    }
    $unexpected = @($events | Where-Object {
        $_.type -in @('item.started', 'item.completed') -and $_.item.type -notin @('agent_message', 'reasoning', 'mcp_tool_call')
    })
    if ($unexpected.Count -ne 0) { throw 'The host used an unexpected action during the bounded capability smoke.' }
    $starts = @($events | Where-Object { $_.type -eq 'item.started' -and $_.item.type -eq 'mcp_tool_call' })
    if ($starts.Count -ne 1) { throw 'Expected exactly one MCP invocation start.' }
    $capabilities = $call.result.structured_content
    if (-not $capabilities -and $call.result.content.Count -eq 1) {
        $capabilities = ConvertFrom-Json -InputObject $call.result.content[0].text -AsHashtable
    }
    if ($capabilities.mode -cne 'Fixture' -or $capabilities.syntheticOnly -isnot [bool] -or
        $capabilities.syntheticOnly -ne $true -or $capabilities.linkedIn -cne 'Blocked' -or
        $capabilities.paidApiEnabled -isnot [bool] -or $capabilities.paidApiEnabled -ne $false -or
        $capabilities.canMintApproval -isnot [bool] -or $capabilities.canMintApproval -ne $false) {
        throw 'The actual tool result does not match the constrained fixture capability contract.'
    }
    $turns = @($events | Where-Object { $_.type -eq 'turn.completed' })
    if ($turns.Count -ne 1 -or @($events | Where-Object { $_.type -in @('turn.failed', 'error') }).Count -ne 0) {
        throw 'A single successful host turn is required.'
    }
    [ordered]@{
        status = 'VerifiedOnHostCapabilitySmoke'
        tool = 'runtime_get_capabilities'
        calls = $calls.Count
        capabilities = $capabilities
        usage = $turns[0].usage
        logSha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if (-not $RunLive) {
    Assert-HostEvidence $EvidenceLog | ConvertTo-Json -Depth 8
    exit 0
}

# Opt-in only: consumes the signed-in ChatGPT plan's normal Codex allowance.
# No install, login, global config edit, API key, billing or credit purchase occurs.
if (-not $IsWindows) { throw 'This verified host harness currently supports Windows only.' }
if (-not $CodexPath) {
    $CodexPath = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/codex-0.155.0/node_modules/@openai/codex-win32-x64/vendor/x86_64-pc-windows-msvc/bin/codex.exe'
}
$CodexPath = (Resolve-Path -LiteralPath $CodexPath).Path
if ([IO.Path]::GetExtension($CodexPath) -ne '.exe') { throw 'Supply the official Codex native executable, not a shell command.' }
$dotnetPath = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/dotnet/dotnet.exe'
$dotnetPath = (Resolve-Path -LiteralPath $dotnetPath).Path
$mcpPath = (Resolve-Path -LiteralPath (Join-Path $repoRoot 'src/JobAgent.Mcp/bin/Release/net10.0/JobAgent.Mcp.dll')).Path
$codexVersion = (& $CodexPath --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Codex version check failed.' }
$loginStatus = (& $CodexPath -c 'forced_login_method="chatgpt"' login status 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $loginStatus -notmatch 'Logged in using ChatGPT') {
    throw 'Existing ChatGPT authentication is required. This script will not use API authentication or log in automatically.'
}

$runAt = [DateTimeOffset]::UtcNow
$logDirectory = Join-Path $repoRoot ('artifacts/codex-host/' + $runAt.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$EvidenceLog = Join-Path $logDirectory 'events.jsonl'
$temporaryParent = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/host-checks'))
$temporaryRuntime = Join-Path $temporaryParent ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRuntime -Force | Out-Null
function ConvertTo-TomlString([string]$Value) { return '"' + $Value.Replace('\', '/').Replace('"', '\"') + '"' }
$mcpOverride = 'mcp_servers.job_agent={command=' + (ConvertTo-TomlString $dotnetPath) +
    ',args=[' + (ConvertTo-TomlString $mcpPath) + '],env={DOTNET_ROOT=' +
    (ConvertTo-TomlString (Split-Path $dotnetPath -Parent)) + ',JOBAGENT_RUNTIME_DIR=' +
    (ConvertTo-TomlString $temporaryRuntime) + '},required=true,startup_timeout_sec=30,enabled_tools=["runtime_get_capabilities"]}'
$prompt = 'Call the job_agent MCP runtime_get_capabilities tool exactly once. Do not read files, use shell, browse, call other tools, or modify anything. Then report only Mode, SyntheticOnly, LinkedIn and PaidApiEnabled from its actual result. If unavailable report unavailable; do not guess.'
$start = [Diagnostics.ProcessStartInfo]::new($CodexPath)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.WorkingDirectory = $temporaryRuntime
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'AZURE_OPENAI_API_KEY', 'OPENAI_BASE_URL')) { $null = $start.Environment.Remove($name) }
foreach ($argument in @('exec', '--ignore-user-config', '--ephemeral', '--disable', 'shell_tool', '--disable', 'apps',
    '--disable', 'plugins', '--disable', 'multi_agent', '--disable', 'shell_snapshot', '-m', $Model,
    '-c', 'forced_login_method="chatgpt"', '-c', 'web_search="disabled"', '-c', $mcpOverride,
    '--sandbox', 'read-only', '--skip-git-repo-check', '--cd', $temporaryRuntime, '--json', $prompt)) {
    $start.ArgumentList.Add($argument)
}
$process = $null
try {
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.Close()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        [IO.File]::WriteAllText($EvidenceLog, $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $logDirectory 'stderr.log'), $stderr.GetAwaiter().GetResult())
        throw 'Host smoke exceeded its time limit; no automatic model retry was attempted.'
    }
    [IO.File]::WriteAllText($EvidenceLog, $stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $logDirectory 'stderr.log'), $stderr.GetAwaiter().GetResult())
    if ($process.ExitCode -ne 0) { throw ('Codex failed with exit ' + $process.ExitCode + '; inspect ' + $logDirectory) }
    $result = Assert-HostEvidence $EvidenceLog
    if (@(Get-ChildItem -LiteralPath $temporaryRuntime -Force).Count -ne 0) { throw 'The empty runtime was unexpectedly modified.' }
    $result['runAt'] = $runAt.ToString('O')
    $result['cliVersion'] = $codexVersion
    $result['model'] = $Model
    $result['authentication'] = 'ExistingChatGPT'
    $result['exitCode'] = $process.ExitCode
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $logDirectory 'summary.json') -Encoding utf8
    Write-Host ('Host capability smoke passed; evidence: ' + $logDirectory)
    $result | ConvertTo-Json -Depth 8
} finally {
    if ($process) { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
    $resolved = [IO.Path]::GetFullPath($temporaryRuntime)
    $expectedParent = $temporaryParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $resolved -Leaf) -notmatch '^[a-f0-9]{32}$') { throw 'Refusing cleanup outside the dedicated host-checks directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
