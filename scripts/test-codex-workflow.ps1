#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][switch]$RunLive,
    [Parameter(Mandatory)][string]$McpDirectory,
    [Parameter(Mandatory)][string]$CompanionDirectory,
    [string]$Model = 'gpt-6-astra',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
if (-not $RunLive -or -not $IsWindows) { throw 'An explicit Windows synthetic host test is required.' }
$repoRoot = Split-Path $PSScriptRoot -Parent
$McpDirectory = (Resolve-Path -LiteralPath $McpDirectory).Path
$CompanionDirectory = (Resolve-Path -LiteralPath $CompanionDirectory).Path
$mcpDll = Join-Path $McpDirectory 'JobAgent.Mcp.dll'
foreach ($path in @($mcpDll, (Join-Path $CompanionDirectory 'JobAgent.Web.dll'), (Join-Path $CompanionDirectory 'JobAgent.FakeCareerSite.dll'))) {
    if (-not (Test-Path -LiteralPath $path)) { throw 'Use complete, previously built MCP and companion binary directories.' }
}
$originalLocalData = $env:LOCALAPPDATA
$codexExecutable = Join-Path $originalLocalData 'JobApplicationAgent/tools/codex-0.155.0/node_modules/@openai/codex-win32-x64/vendor/x86_64-pc-windows-msvc/bin/codex.exe'
$dotnetExecutable = Join-Path $originalLocalData 'JobApplicationAgent/tools/dotnet/dotnet.exe'
$originalCodexDirectory = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
$authFile = Join-Path $originalCodexDirectory 'auth.json'
if (-not $PrepareOnly -and -not (Test-Path -LiteralPath $authFile)) { throw 'Existing file-based ChatGPT authentication is required.' }
$configPath = Join-Path $originalCodexDirectory 'config.toml'
$configHash = if (Test-Path -LiteralPath $configPath) { (Get-FileHash -LiteralPath $configPath).Hash } else { $null }
$runAt = [DateTimeOffset]::UtcNow
$evidenceDirectory = Join-Path $repoRoot ('artifacts/codex-workflow/' + $runAt.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$inputBinaryManifest = @(
    foreach ($binarySet in @(@{ name = 'mcp'; path = $McpDirectory }, @{ name = 'companion'; path = $CompanionDirectory })) {
        foreach ($binary in Get-ChildItem -LiteralPath $binarySet.path -Filter '*.dll') {
            [ordered]@{ set = $binarySet.name; name = $binary.Name; sha256 = (Get-FileHash -LiteralPath $binary.FullName).Hash.ToLowerInvariant() }
        }
    }
)
$inputBinaryManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidenceDirectory 'input-binaries.json') -Encoding utf8
$privateParent = [IO.Path]::GetFullPath((Join-Path $originalLocalData 'JobApplicationAgent/workflow-host-tests'))
$privateRoot = Join-Path $privateParent ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
$acl = Get-Acl -LiteralPath $privateRoot
$acl.SetAccessRuleProtection($true, $false)
$userSid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($userSid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $privateRoot -AclObject $acl
$testCodexDirectory = Join-Path $privateRoot 'codex-home'
$runtimeDirectory = Join-Path $privateRoot 'runtime'
$workDirectory = Join-Path $privateRoot 'empty-work'
$testLocalData = Join-Path $privateRoot 'local-data'
foreach ($directory in @($testCodexDirectory, $runtimeDirectory, $workDirectory, $testLocalData)) { New-Item -ItemType Directory -Path $directory | Out-Null }
$browserCache = if ($env:PLAYWRIGHT_BROWSERS_PATH) { $env:PLAYWRIGHT_BROWSERS_PATH } else { Join-Path $originalLocalData 'ms-playwright' }
$driver = $null
$driverStderr = $null
$rpcId = 0
$turns = [Collections.Generic.List[object]]::new()
$completed = $false

function New-ChildStart([string]$Executable, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.WorkingDirectory = $workDirectory
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['CODEX_HOME'] = $testCodexDirectory
    $start.Environment['LOCALAPPDATA'] = $testLocalData
    $start.Environment['JOBAGENT_RUNTIME_DIR'] = $runtimeDirectory
    $start.Environment['JOBAGENT_ENABLE_SYNTHETIC_COMMANDS'] = '1'
    $start.Environment['DOTNET_ROOT'] = Split-Path $dotnetExecutable -Parent
    $start.Environment['PATH'] = (Split-Path $dotnetExecutable -Parent) + ';' + $env:PATH
    $start.Environment['PLAYWRIGHT_BROWSERS_PATH'] = $browserCache
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.Environment['DOTNET_NOLOGO'] = '1'
    foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'AZURE_OPENAI_API_KEY', 'OPENAI_BASE_URL')) { $null = $start.Environment.Remove($name) }
    return $start
}
function Invoke-Child([string]$Executable, [string[]]$Arguments, [string]$Name, [int]$TimeoutSeconds = 180) {
    $process = [Diagnostics.Process]::Start((New-ChildStart $Executable $Arguments))
    try {
        $process.StandardInput.Close()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidenceDirectory ($Name + '.stdout.log')), $stdout)
        [IO.File]::WriteAllText((Join-Path $evidenceDirectory ($Name + '.stderr.log')), $stderr)
        if ($timedOut -or $process.ExitCode -ne 0) { throw ('Child process failed or timed out: ' + $Name + '. Inspect ignored evidence logs.') }
        return $stdout
    } finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}
function Read-Driver {
    $read = $driver.StandardOutput.ReadLineAsync()
    if (-not $read.Wait(90000)) { throw 'Synthetic UI driver timed out.' }
    $line = $read.GetAwaiter().GetResult()
    if (-not $line) { throw 'Synthetic UI driver exited unexpectedly.' }
    $value = $line | ConvertFrom-Json -AsHashtable
    $line | Add-Content -LiteralPath (Join-Path $evidenceDirectory 'synthetic-ui.jsonl') -Encoding utf8
    return $value
}
function Invoke-Driver([string]$Action, [string]$ApplicationRef = '') {
    $script:rpcId++
    $command = @{ id = $script:rpcId; action = $Action }
    if ($ApplicationRef) { $command.applicationRef = $ApplicationRef }
    $driver.StandardInput.WriteLine(($command | ConvertTo-Json -Compress))
    $driver.StandardInput.Flush()
    $value = Read-Driver
    if ($value.id -ne $script:rpcId -or $value.ok -ne $true) { throw ('Synthetic UI driver rejected ' + $Action) }
    return $value
}
function Toml-String([string]$Value) { return '"' + $Value.Replace('\', '/').Replace('"', '\"') + '"' }
function Invoke-HostTurn([string]$Name, [string]$Prompt) {
    # User-authorized synthetic test invocation only. This scoped host permission
    # cannot mint the application's separate sharing/submission UI approvals.
    # Keep destructive metadata and the read-only sandbox unchanged.
    $toolPolicy = if ($Name -in @('02-denied-without-approval', '03-approved-execution')) {
        ',tools={application_execute_approved={approval_mode="approve"}}'
    } else { '' }
    $mcpOverride = 'mcp_servers.job_agent={command=' + (Toml-String $dotnetExecutable) +
        ',args=[' + (Toml-String $mcpDll) + '],env={DOTNET_ROOT=' + (Toml-String (Split-Path $dotnetExecutable -Parent)) +
        ',JOBAGENT_RUNTIME_DIR=' + (Toml-String $runtimeDirectory) + ',JOBAGENT_ENABLE_SYNTHETIC_COMMANDS="1"},required=true,startup_timeout_sec=30,' +
        'enabled_tools=["application_create_draft","application_prepare_review","application_execute_approved","application_get_status"]' + $toolPolicy + '}'
    $prefix = 'This is a closed synthetic loopback test. Use only the named job_agent MCP tools. Do not read files, use shell, browse, use other tools, or change configuration. All UI approval actions belong to a separate trusted synthetic test driver. Do not create or infer approval. '
    $raw = Invoke-Child $codexExecutable @('exec', '--strict-config', '--ignore-user-config', '--ephemeral', '--disable', 'shell_tool', '--disable', 'apps', '--disable', 'plugins', '--disable', 'multi_agent', '--disable', 'shell_snapshot',
        '--disable', 'browser_use', '--disable', 'computer_use', '--disable', 'view_image', '--disable', 'image_generation', '--disable', 'hooks', '-m', $Model,
        '-c', 'forced_login_method="chatgpt"', '-c', 'cli_auth_credentials_store="file"', '-c', 'web_search="disabled"', '-c', $mcpOverride,
        '--sandbox', 'read-only', '--skip-git-repo-check', '--cd', $workDirectory, '--json', ($prefix + $Prompt)) $Name
    $events = @($raw -split '\r?\n' | Where-Object { $_.StartsWith('{') } | ForEach-Object { $_ | ConvertFrom-Json -AsHashtable })
    if (@($events | Where-Object { $_.type -in @('turn.failed', 'error') }).Count -or
        @($events | Where-Object { $_.type -in @('item.started', 'item.completed') -and $_.item.type -notin @('mcp_tool_call', 'agent_message', 'reasoning') }).Count) { throw 'Host used an unexpected action or failed.' }
    $completedTurn = @($events | Where-Object { $_.type -eq 'turn.completed' })
    if ($completedTurn.Count -ne 1) { throw 'Expected one completed host turn.' }
    $calls = @($events | Where-Object { $_.type -eq 'item.completed' -and $_.item.type -eq 'mcp_tool_call' } | ForEach-Object { $_.item })
    foreach ($call in $calls) {
        if ($call.server -ne 'job_agent' -or $call.tool -notin @('application_create_draft', 'application_prepare_review', 'application_execute_approved', 'application_get_status')) { throw 'Host called a tool outside the closed workflow allowlist.' }
    }
    $turn = @{ name = $Name; calls = $calls; usage = $completedTurn[0].usage; final = (@($events | Where-Object { $_.type -eq 'item.completed' -and $_.item.type -eq 'agent_message' }) | Select-Object -Last 1).item.text }
    $turns.Add($turn)
    return $turn
}
function Get-OneCall([hashtable]$Turn, [string]$Tool) {
    $matches = @($Turn.calls | Where-Object { $_.tool -eq $Tool })
    if ($matches.Count -ne 1) { throw ('Exactly one actual ' + $Tool + ' call is required.') }
    return $matches[0]
}
function Get-SuccessData([hashtable]$Call) {
    if ($Call.error -or $Call.status -ne 'completed' -or $Call.result.is_error -or $Call.result.isError) { throw 'Expected an actual successful MCP result.' }
    if ($Call.result.structured_content) { return $Call.result.structured_content }
    return ($Call.result.content[0].text | ConvertFrom-Json -AsHashtable)
}

try {
    if (-not $PrepareOnly) {
        Copy-Item -LiteralPath $authFile -Destination (Join-Path $testCodexDirectory 'auth.json')
        Set-Content -LiteralPath (Join-Path $testCodexDirectory 'config.toml') -Value "forced_login_method = `"chatgpt`"`ncli_auth_credentials_store = `"file`"`n" -Encoding utf8
        $null = Invoke-Child $codexExecutable @('login', 'status') 'login-status'
        $login = (Get-Content -LiteralPath (Join-Path $evidenceDirectory 'login-status.stderr.log') -Raw) + (Get-Content -LiteralPath (Join-Path $evidenceDirectory 'login-status.stdout.log') -Raw)
        if ($login -notmatch 'Logged in using ChatGPT') { throw 'The isolated host did not confirm existing ChatGPT authentication.' }
    }
    $fixtureRoot = Join-Path $privateRoot 'fixture-driver'
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'workflow-host-fixture/Program.cs') -Destination $fixtureRoot
    $references = (Get-ChildItem -LiteralPath $CompanionDirectory -Filter '*.dll' | ForEach-Object {
        '<Reference Include="' + [Security.SecurityElement]::Escape($_.BaseName) + '"><HintPath>' + [Security.SecurityElement]::Escape($_.FullName) + '</HintPath></Reference>'
    }) -join "`n"
    $projectPath = Join-Path $fixtureRoot 'WorkflowHostFixture.csproj'
    Set-Content -LiteralPath $projectPath -Encoding utf8 -Value ('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />' + $references + '</ItemGroup></Project>')
    $nugetConfig = Join-Path $fixtureRoot 'NuGet.Config'
    Set-Content -LiteralPath $nugetConfig -Value '<configuration><packageSources><clear /></packageSources></configuration>'
    $null = Invoke-Child $dotnetExecutable @('restore', $projectPath, '--configfile', $nugetConfig, '--nologo') 'fixture-restore'
    $null = Invoke-Child $dotnetExecutable @('build', $projectPath, '--no-restore', '-c', 'Release', '--nologo') 'fixture-build'
    $fixtureOutput = Join-Path $fixtureRoot 'bin/Release/net10.0'
    foreach ($directory in @('runtimes', '.playwright')) { Copy-Item -LiteralPath (Join-Path $CompanionDirectory $directory) -Destination $fixtureOutput -Recurse }
    Copy-Item -LiteralPath (Join-Path $CompanionDirectory 'runtimes/win-x64/native/e_sqlite3.dll') -Destination $fixtureOutput
    $driver = [Diagnostics.Process]::Start((New-ChildStart $dotnetExecutable @((Join-Path $fixtureOutput 'WorkflowHostFixture.dll'), $runtimeDirectory)))
    $driverStderr = $driver.StandardError.ReadToEndAsync()
    $ready = Read-Driver
    if ($ready.ready -ne $true -or $ready.synthetic -ne $true) { throw 'Synthetic driver did not become ready.' }
    $prepared = Invoke-Driver 'prepare-profile'
    if (-not $prepared.profileConfirmed -or $prepared.applicationRef -or $prepared.submissionPosts -ne 0) { throw 'Synthetic UI preparation unexpectedly created an application or submission.' }
    if ($PrepareOnly) { Write-Host ('Synthetic UI/bridge preparation passed; no model call. Evidence: ' + $evidenceDirectory); return }

    $draftTurn = Invoke-HostTurn '01-draft-review' 'Call application_create_draft exactly once. Then use the returned applicationRef to call application_prepare_review exactly once. Do not execute. Report the reference, state and whether approval exists.'
    if ($draftTurn.calls.Count -ne 2) { throw 'Draft/review turn must make only its two requested calls.' }
    $created = Get-SuccessData (Get-OneCall $draftTurn 'application_create_draft')
    $applicationRef = ([guid]::Parse($created.applicationRef)).ToString('D')
    if ($created.status -ne 'ReadyForDataSharing' -or $created.submissionApproved) { throw 'Draft state or approval mismatch.' }
    $reviewCall = Get-OneCall $draftTurn 'application_prepare_review'
    $reviewed = Get-SuccessData $reviewCall
    if ($reviewCall.arguments.applicationRef -ne $applicationRef -or $reviewed.applicationRef -ne $applicationRef -or -not $reviewed.reviewRequested -or $reviewed.submissionApproved) { throw 'Review did not bind the created application without approval.' }
    $filled = Invoke-Driver 'share-fill'
    if ($filled.applicationRef -ne $applicationRef -or $filled.state -ne 'AwaitingSubmissionApproval' -or $filled.submissionApproved -or $filled.submissionPosts -ne 0) { throw 'Trusted synthetic sharing did not stop before submission approval.' }

    $deniedTurn = Invoke-HostTurn '02-denied-without-approval' ('Call application_execute_approved exactly once with these exact JSON arguments: {"applicationRef":"' + $applicationRef + '"}. Check that it rejects missing local submission approval. Do not retry. Report the actual denial.')
    if ($deniedTurn.calls.Count -ne 1) { throw 'Denial turn must make exactly one execute call.' }
    $denied = Get-OneCall $deniedTurn 'application_execute_approved'
    if ($denied.arguments.applicationRef -ne $applicationRef -or -not ($denied.error -or $denied.result.is_error -or $denied.result.isError) -or ($denied | ConvertTo-Json -Depth 12 -Compress) -notmatch 'ConsentRequired') { throw 'Missing UI approval was not denied with the expected policy result.' }
    $unchanged = Invoke-Driver 'inspect'
    if ($unchanged.submissionPosts -ne 0 -or $unchanged.receiptCount -ne 0 -or $unchanged.submissionApproved -or $unchanged.state -ne 'AwaitingSubmissionApproval') { throw 'Denied model execution changed submission state.' }
    $approved = Invoke-Driver 'approve-host' $applicationRef
    if (-not $approved.submissionApproved -or $approved.submissionPosts -ne 0 -or $approved.state -ne 'AwaitingSubmissionApproval') { throw 'Simulated trusted UI approval submitted prematurely or did not persist approval.' }

    $executeTurn = Invoke-HostTurn '03-approved-execution' ('The separate trusted synthetic UI test driver has now recorded local approval. Call application_execute_approved exactly once, then application_get_status exactly once. For both calls use these exact JSON arguments: {"applicationRef":"' + $applicationRef + '"}. Report the actual state and receipt. Do not retry on uncertainty; do not claim a real employer application.')
    if ($executeTurn.calls.Count -ne 2) { throw 'Approved turn must make exactly execute and status calls.' }
    $executeCall = Get-OneCall $executeTurn 'application_execute_approved'
    $executed = Get-SuccessData $executeCall
    $statusCall = Get-OneCall $executeTurn 'application_get_status'
    $status = Get-SuccessData $statusCall
    if ($executeCall.arguments.applicationRef -ne $applicationRef -or $executed.status -ne 'SubmittedVerified' -or -not $executed.receiptId -or $executed.submissionApproved -or
        $statusCall.arguments.applicationRef -ne $applicationRef -or $status.state -ne 'SubmittedVerified' -or $status.evidence.receiptId -ne $executed.receiptId) { throw 'Model tools did not return the same verified application receipt.' }
    $verified = Invoke-Driver 'verify-final'
    if (-not $verified.finalVerified -or $verified.receiptId -ne $executed.receiptId) { throw 'Fake career POST/receipt evidence did not match the model result.' }
    $completed = $true
} finally {
    if ($driver) {
        if (-not $driver.HasExited) {
            try { $null = Invoke-Driver 'stop' } catch { }
            if (-not $driver.WaitForExit(10000)) { $driver.Kill($true); $driver.WaitForExit() }
        }
        if ($driverStderr) { [IO.File]::WriteAllText((Join-Path $evidenceDirectory 'fixture-driver.stderr.log'), $driverStderr.GetAwaiter().GetResult()) }
        $driver.Dispose()
    }
    $currentHash = if (Test-Path -LiteralPath $configPath) { (Get-FileHash -LiteralPath $configPath).Hash } else { $null }
    $resolved = [IO.Path]::GetFullPath($privateRoot)
    $expected = $privateParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($expected, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notmatch '^[a-f0-9]{32}$') { throw 'Refusing cleanup outside the owned workflow-host directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if ($currentHash -ne $configHash) { throw 'Original Codex config changed during the run.' }
}
if ($completed) {
    foreach ($binary in $inputBinaryManifest) {
        $binaryRoot = if ($binary.set -eq 'mcp') { $McpDirectory } else { $CompanionDirectory }
        if ((Get-FileHash -LiteralPath (Join-Path $binaryRoot $binary.name)).Hash.ToLowerInvariant() -ne $binary.sha256) { throw 'Input binaries changed during the live run; this result cannot be attributed to one build.' }
    }
    [ordered]@{ runAt = $runAt.ToString('O'); model = $Model; cli = '0.155.0'; authentication = 'ExistingChatGPTIsolatedTemporaryCopy'; syntheticTrustedUiApproval = $true; actualHumanApproval = $false; processScopedHostToolApproval = 'application_execute_approved'; applicationRef = $applicationRef; receiptId = $verified.receiptId; submissionPosts = $verified.submissionPosts; receiptCount = $verified.receiptCount; resumeHash = $verified.resumeHash; mcpSha256 = (Get-FileHash -LiteralPath $mcpDll).Hash.ToLowerInvariant(); globalConfigUnchanged = $true; privateTestDirectoryRemoved = $true; turns = @($turns | ForEach-Object { @{ name = $_.name; usage = $_.usage; final = $_.final; toolCalls = $_.calls.Count } }) } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidenceDirectory 'summary.json') -Encoding utf8
    Write-Host ('Real Codex synthetic workflow passed. Evidence: ' + $evidenceDirectory)
}
