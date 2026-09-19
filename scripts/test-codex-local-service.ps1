#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$McpDirectory,
    [Parameter(Mandatory)][string]$CompanionDirectory,
    [switch]$PrepareOnly,
    [switch]$RunLive,
    [string]$Model = 'gpt-6-astra'
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or ($PrepareOnly -eq $RunLive)) { throw 'Choose exactly one of -PrepareOnly or -RunLive on Windows.' }
$repoRoot = Split-Path $PSScriptRoot -Parent
$McpDirectory = (Resolve-Path -LiteralPath $McpDirectory).Path
$CompanionDirectory = (Resolve-Path -LiteralPath $CompanionDirectory).Path
$mcpDll = Join-Path $McpDirectory 'JobAgent.Mcp.dll'
foreach ($path in @($mcpDll, (Join-Path $CompanionDirectory 'JobAgent.Web.dll'), (Join-Path $CompanionDirectory 'JobAgent.FakeCareerSite.dll'))) {
    if (-not (Test-Path -LiteralPath $path)) { throw 'Provide stable complete MCP and companion binary directories.' }
}
$localData = $env:LOCALAPPDATA
$dotnetExe = Join-Path $localData 'JobApplicationAgent/tools/dotnet/dotnet.exe'
$codexExe = Join-Path $localData 'JobApplicationAgent/tools/codex-0.155.0/node_modules/@openai/codex-win32-x64/vendor/x86_64-pc-windows-msvc/bin/codex.exe'
$originalCodex = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
$authPath = Join-Path $originalCodex 'auth.json'
$configPath = Join-Path $originalCodex 'config.toml'
$configHash = if (Test-Path -LiteralPath $configPath) { (Get-FileHash -LiteralPath $configPath).Hash } else { $null }
if ($RunLive -and -not (Test-Path -LiteralPath $authPath)) { throw 'Existing file-based ChatGPT authentication is required; no API fallback.' }
$started = [DateTimeOffset]::UtcNow
$evidence = Join-Path $repoRoot ('artifacts/codex-local-service/' + $started.ToString('yyyyMMddTHHmmssfffZ'))
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$manifest = @(
    foreach ($set in @(@{ name = 'mcp'; path = $McpDirectory }, @{ name = 'companion'; path = $CompanionDirectory })) {
        foreach ($binary in Get-ChildItem -LiteralPath $set.path -Filter '*.dll') {
            @{ set = $set.name; name = $binary.Name; sha256 = (Get-FileHash -LiteralPath $binary.FullName).Hash.ToLowerInvariant() }
        }
    }
)
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'input-binaries.json')
$privateParent = [IO.Path]::GetFullPath((Join-Path $localData 'JobApplicationAgent/local-host-tests'))
$privateRoot = Join-Path $privateParent ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
$acl = Get-Acl -LiteralPath $privateRoot
$acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.WindowsIdentity]::GetCurrent().User,
    'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $privateRoot -AclObject $acl
$isolatedCodex = Join-Path $privateRoot 'codex-home'
$runtime = Join-Path $privateRoot 'runtime'
$emptyWork = Join-Path $privateRoot 'empty-work'
$isolatedLocalData = Join-Path $privateRoot 'local-data'
foreach ($directory in @($isolatedCodex, $runtime, $emptyWork, $isolatedLocalData)) { New-Item -ItemType Directory -Path $directory | Out-Null }
$tools = @('runtime_get_capabilities', 'profile_get_summary', 'profile_propose_patch', 'job_import_text', 'job_evaluate',
    'application_create_draft', 'application_get_questions', 'application_propose_answers', 'application_prepare_review',
    'application_execute_approved', 'application_cancel', 'application_get_status')
$turns = [Collections.Generic.List[object]]::new()
$driver = $null; $driverError = $null; $rpc = 0; $pass = $false; $failure = $null
$prepared = $null; $verified = $null; $smoke = $false; $configUnchanged = $false; $removed = $false; $binariesUnchanged = $false

function New-Start([string]$Executable, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.WorkingDirectory = $emptyWork
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['CODEX_HOME'] = $isolatedCodex
    $start.Environment['LOCALAPPDATA'] = $isolatedLocalData
    $start.Environment['JOBAGENT_RUNTIME_DIR'] = $runtime
    $start.Environment['JOBAGENT_ENABLE_LOCAL_COMMANDS'] = '1'
    $start.Environment['JOBAGENT_ENABLE_SYNTHETIC_COMMANDS'] = '0'
    $start.Environment['DOTNET_ROOT'] = Split-Path $dotnetExe -Parent
    $start.Environment['PATH'] = (Split-Path $dotnetExe -Parent) + ';' + $env:PATH
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'; $start.Environment['DOTNET_NOLOGO'] = '1'
    foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'AZURE_OPENAI_API_KEY', 'OPENAI_BASE_URL')) { $null = $start.Environment.Remove($name) }
    return $start
}
function Invoke-Child([string]$Executable, [string[]]$Arguments, [string]$Name, [switch]$AllowFailure) {
    $process = [Diagnostics.Process]::Start((New-Start $Executable $Arguments))
    try {
        $process.StandardInput.Close()
        $outTask = $process.StandardOutput.ReadToEndAsync(); $errTask = $process.StandardError.ReadToEndAsync()
        $timeout = -not $process.WaitForExit(180000)
        if ($timeout) { $process.Kill($true); $process.WaitForExit() }
        $output = $outTask.GetAwaiter().GetResult(); $errorText = $errTask.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $evidence ($Name + '.stdout.log')), $output)
        [IO.File]::WriteAllText((Join-Path $evidence ($Name + '.stderr.log')), $errorText)
        @{ exitCode = $process.ExitCode; timedOut = $timeout } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence ($Name + '.process.json'))
        if (-not $AllowFailure -and ($timeout -or $process.ExitCode -ne 0)) { throw ('Child failed: ' + $Name + '; inspect ignored logs.') }
        return @{ stdout = $output; stderr = $errorText; exitCode = $process.ExitCode; timedOut = $timeout }
    } finally { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}
function Read-Line([Diagnostics.Process]$Process) {
    $task = $Process.StandardOutput.ReadLineAsync()
    if (-not $task.Wait(30000)) { throw 'Local fixture/protocol response timed out.' }
    $line = $task.GetAwaiter().GetResult()
    if (-not $line) { throw 'Local fixture/protocol process exited unexpectedly.' }
    return ($line | ConvertFrom-Json -AsHashtable)
}
function Invoke-Driver([string]$Action, [string]$ApplicationRef = '') {
    $script:rpc++
    $request = @{ id = $script:rpc; action = $Action }
    if ($ApplicationRef) { $request.applicationRef = $ApplicationRef }
    $driver.StandardInput.WriteLine(($request | ConvertTo-Json -Compress))
    $driver.StandardInput.Flush()
    $result = Read-Line $driver
    $result | ConvertTo-Json -Compress -Depth 10 | Add-Content -LiteralPath (Join-Path $evidence 'synthetic-fixture.jsonl')
    if ($result.id -ne $script:rpc -or -not $result.ok) { throw ('Synthetic fixture rejected ' + $Action) }
    return $result
}
function Invoke-ProtocolSmoke {
    $process = [Diagnostics.Process]::Start((New-Start $dotnetExe @($mcpDll)))
    $stderr = $process.StandardError.ReadToEndAsync()
    try {
        $process.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"local12-prepare","version":"1"}}}')
        $hello = Read-Line $process
        if ($hello.id -ne 1 -or $hello.error) { throw 'STDIO initialization failed.' }
        $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
        $process.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list"}')
        $catalog = Read-Line $process
        if ($catalog.error -or $catalog.result.tools.Count -ne 12 -or
            @(Compare-Object ($tools | Sort-Object) ($catalog.result.tools.name | Sort-Object)).Count) { throw 'Expected exact twelve-tool local catalog.' }
        $process.StandardInput.WriteLine('{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"runtime_get_capabilities","arguments":{}}}')
        $result = Read-Line $process
        if ($result.error -or $result.result.isError -or $result.result.structuredContent.mode -ne 'HostMediated' -or
            -not $result.result.structuredContent.companionAvailable -or $result.result.structuredContent.canMintApproval -or
            $result.result.structuredContent.workspace.profileRef -ne $prepared.references.profileRef) { throw 'STDIO capability call did not reach the synthetic protected workspace.' }
        @{ catalog = $catalog.result.tools.name; capability = $result.result.structuredContent } |
            ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'prepare-stdio.json')
    } finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        [IO.File]::WriteAllText((Join-Path $evidence 'prepare-stdio.stderr.log'), $stderr.GetAwaiter().GetResult()); $process.Dispose()
    }
}
function Toml([string]$Value) { return '"' + $Value.Replace('\', '/').Replace('"', '\"') + '"' }
function Invoke-Host([string]$Name, [string]$Prompt, [switch]$ExecutionTest) {
    $policy = if ($ExecutionTest) { ',tools={application_execute_approved={approval_mode="approve"},application_cancel={approval_mode="approve"}}' } else { '' }
    $override = 'mcp_servers.job_agent={command=' + (Toml $dotnetExe) + ',args=[' + (Toml $mcpDll) + '],env={DOTNET_ROOT=' +
        (Toml (Split-Path $dotnetExe -Parent)) + ',JOBAGENT_RUNTIME_DIR=' + (Toml $runtime) +
        ',JOBAGENT_ENABLE_LOCAL_COMMANDS="1",JOBAGENT_ENABLE_SYNTHETIC_COMMANDS="0"},required=true,startup_timeout_sec=30,enabled_tools=[' +
        (($tools | ForEach-Object { Toml $_ }) -join ',') + ']' + $policy + '}'
    $prefix = 'This is an explicitly authorized, closed synthetic integration test of a local workspace. Only the twelve job_agent MCP tools are available for this test. Use no shell, files, browser, other tools, configuration changes, purchases or API keys. No real applicant or employer is involved. Never infer or create user/application approval. Calls are test actions; a stated outcome without an actual tool call is not evidence. '
    $result = Invoke-Child $codexExe @('exec', '--strict-config', '--ignore-user-config', '--ephemeral', '--disable', 'shell_tool', '--disable', 'apps',
        '--disable', 'plugins', '--disable', 'multi_agent', '--disable', 'shell_snapshot', '--disable', 'browser_use', '--disable', 'computer_use',
        '--disable', 'view_image', '--disable', 'image_generation', '--disable', 'hooks', '-m', $Model,
        '-c', 'forced_login_method="chatgpt"', '-c', 'cli_auth_credentials_store="file"', '-c', 'web_search="disabled"', '-c', $override,
        '--sandbox', 'read-only', '--skip-git-repo-check', '--cd', $emptyWork, '--json', ($prefix + $Prompt)) $Name -AllowFailure
    $events = @($result.stdout -split '\r?\n' | Where-Object { $_.StartsWith('{') } | ForEach-Object { $_ | ConvertFrom-Json -AsHashtable })
    $completed = @($events | Where-Object { $_.type -eq 'turn.completed' })
    $calls = @($events | Where-Object { $_.type -eq 'item.completed' -and $_.item.type -eq 'mcp_tool_call' } | ForEach-Object { $_.item })
    $turn = @{ name = $Name; exitCode = $result.exitCode; timedOut = $result.timedOut; calls = $calls;
        usage = if ($completed.Count -eq 1) { $completed[0].usage } else { $null };
        final = (@($events | Where-Object { $_.type -eq 'item.completed' -and $_.item.type -eq 'agent_message' }) | Select-Object -Last 1).item.text }
    $turns.Add($turn)
    if ($result.exitCode -ne 0 -or $result.timedOut -or $completed.Count -ne 1 -or
        @($events | Where-Object { $_.type -in @('turn.failed', 'error') }).Count -or
        @($events | Where-Object { $_.type -in @('item.started', 'item.completed') -and $_.item.type -notin @('mcp_tool_call', 'agent_message', 'reasoning') }).Count) {
        throw 'Host failed or used an unexpected action; actual events and usage are retained.'
    }
    foreach ($call in $calls) {
        if ($call.server -ne 'job_agent' -or $call.tool -notin $tools) { throw 'A tool outside the local allowlist was used.' }
        $response = $call.result | ConvertTo-Json -Depth 30 -Compress
        if ($response -match 'synthetic@example\.invalid|Synthetic User|(?<!\d)(85000|100000)(?!\d)') { throw 'Private fixture values appeared in a host tool result.' }
    }
    return $turn
}
function One([hashtable]$Turn, [string]$Tool) {
    $calls = @($Turn.calls | Where-Object { $_.tool -eq $Tool })
    if ($calls.Count -ne 1) { throw ('Expected exactly one actual ' + $Tool + ' call.') }; return $calls[0]
}
function Get-CallData([hashtable]$Call) {
    if ($Call.error -or $Call.status -ne 'completed' -or $Call.result.is_error -or $Call.result.isError) { throw 'Expected a successful actual MCP result.' }
    if ($Call.result.structured_content) { return $Call.result.structured_content }
    return ($Call.result.content[0].text | ConvertFrom-Json -AsHashtable)
}

try {
    $fixtureRoot = Join-Path $privateRoot 'fixture'
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'local-host-fixture/Program.cs') -Destination $fixtureRoot
    $references = (Get-ChildItem -LiteralPath $CompanionDirectory -Filter '*.dll' | ForEach-Object {
        '<Reference Include="' + [Security.SecurityElement]::Escape($_.BaseName) + '"><HintPath>' + [Security.SecurityElement]::Escape($_.FullName) + '</HintPath></Reference>'
    }) -join "`n"
    $project = Join-Path $fixtureRoot 'LocalHostFixture.csproj'
    Set-Content -LiteralPath $project -Value ('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />' + $references + '</ItemGroup></Project>')
    $nuget = Join-Path $fixtureRoot 'NuGet.Config'
    Set-Content -LiteralPath $nuget -Value '<configuration><packageSources><clear /></packageSources></configuration>'
    $null = Invoke-Child $dotnetExe @('restore', $project, '--configfile', $nuget, '--nologo') 'fixture-restore'
    $null = Invoke-Child $dotnetExe @('build', $project, '--no-restore', '-c', 'Release', '--nologo') 'fixture-build'
    $output = Join-Path $fixtureRoot 'bin/Release/net10.0'
    Copy-Item -LiteralPath (Join-Path $CompanionDirectory 'runtimes') -Destination $output -Recurse
    Copy-Item -LiteralPath (Join-Path $CompanionDirectory 'runtimes/win-x64/native/e_sqlite3.dll') -Destination $output
    $driver = [Diagnostics.Process]::Start((New-Start $dotnetExe @((Join-Path $output 'LocalHostFixture.dll'), $runtime)))
    $driverError = $driver.StandardError.ReadToEndAsync()
    $ready = Read-Line $driver
    if (-not $ready.ready -or -not $ready.syntheticOnly -or -not $ready.localMode) { throw 'Local synthetic fixture did not initialize.' }
    $prepared = Invoke-Driver 'prepare'
    if (-not $prepared.profileConfirmed -or -not $prepared.profileUnchanged -or $prepared.applicationCount -ne 0 -or $prepared.trapRequests -ne 0) { throw 'Invalid synthetic UI baseline.' }
    Invoke-ProtocolSmoke
    $smoke = $true
    if ($RunLive) {
        Copy-Item -LiteralPath $authPath -Destination (Join-Path $isolatedCodex 'auth.json')
        Set-Content -LiteralPath (Join-Path $isolatedCodex 'config.toml') -Value "forced_login_method = `"chatgpt`"`ncli_auth_credentials_store = `"file`"`n"
        $login = Invoke-Child $codexExe @('login', 'status') 'login-status'
        if (($login.stdout + $login.stderr) -notmatch 'Logged in using ChatGPT') { throw 'Existing ChatGPT authentication was not confirmed; no API fallback.' }
        $first = Invoke-Host '01-discover-and-propose' ('Make exactly seven calls in this order: (1) runtime_get_capabilities; retain its ORIGINAL workspace refs. (2) profile_get_summary using that profileRef; retain a returned evidenceRef and version. (3) profile_propose_patch using profileRef, baseVersion=the returned profile version, changes=[{"field":"FullName","value":"Pending Synthetic Name","evidenceRefs":[]}]. (4) job_import_text with text="Required: 2 years of professional C# experience.", sourceUrl="' + $prepared.jobImportSourceUrl + '", employer="Synthetic Proposed Employer", title="Synthetic Proposed Role". (5) job_evaluate for the NEW imported jobRef and original profileRef; it must remain ReviewNeeded. (6) application_create_draft using the ORIGINAL reviewed jobRef, profileRef and resumeRef from capabilities, not the imported proposal. (7) application_get_questions for the created applicationRef. Stop there and report references, revision, missing motivation question and pending-only state. Do not answer or execute yet.')
        if ($first.calls.Count -ne 7) { throw 'First turn must contain exactly seven requested calls.' }
        $caps = Get-CallData (One $first 'runtime_get_capabilities')
        $summary = Get-CallData (One $first 'profile_get_summary')
        $patch = Get-CallData (One $first 'profile_propose_patch')
        $job = Get-CallData (One $first 'job_import_text')
        $evaluation = Get-CallData (One $first 'job_evaluate')
        $draft = Get-CallData (One $first 'application_create_draft')
        $questions = Get-CallData (One $first 'application_get_questions')
        $applicationRef = ([guid]::Parse($draft.applicationRef)).ToString('D')
        if ($caps.mode -ne 'HostMediated' -or $caps.canMintApproval -or $caps.workspace.profileRef -ne $prepared.references.profileRef -or
            $patch.status -ne 'Pending' -or -not $patch.requiresUserReview -or -not $job.requiresUserReview -or
            $evaluation.jobRef -ne $job.jobRef -or $evaluation.evaluation.status -ne 'ReviewNeeded' -or
            $draft.status -ne 'NeedsInput' -or $draft.submissionApproved -or $questions.applicationRef -ne $applicationRef -or
            @($questions.questions | Where-Object { $_.key -eq 'motivation' }).Count -ne 1) { throw 'First turn result contract mismatch.' }
        $evidenceRef = $summary.evidence[0].evidenceRef
        $proposal = @{ applicationRef = $applicationRef; baseRevision = $questions.revision; evidenceRefs = @($evidenceRef);
            answers = @(@{ schemaVersion = 1; semanticKey = 'motivation'; proposedValue = 'I want to develop C# tools.';
                language = 'tr'; evidenceIds = @($evidenceRef); rationale = 'Synthetic grounded proposal requiring local user review.' }) } | ConvertTo-Json -Depth 8 -Compress
        $second = Invoke-Host '02-propose-deny-cancel' ('Make exactly five calls in this order. (1) application_propose_answers with these exact JSON arguments: ' + $proposal + '. (2) application_prepare_review with {"applicationRef":"' + $applicationRef + '"}. (3) application_execute_approved with the same applicationRef, exactly once to verify the actual BlockedPermission policy denial. This is an authorized synthetic negative test; no live adapter exists and no application approval is granted. Do not skip this call, retry it, or change policy. (4) application_cancel with the same applicationRef. (5) application_get_status with the same applicationRef. Report only actual outcomes: pending proposal, no approval, policy denial and final cancellation. Do not claim submission.') -ExecutionTest
        if ($second.calls.Count -ne 5) { throw 'Second turn must contain exactly five requested calls.' }
        $proposed = Get-CallData (One $second 'application_propose_answers')
        $review = Get-CallData (One $second 'application_prepare_review')
        $denied = One $second 'application_execute_approved'
        $cancel = Get-CallData (One $second 'application_cancel')
        $status = Get-CallData (One $second 'application_get_status')
        if ($proposed.applicationRef -ne $applicationRef -or -not $proposed.requiresUserReview -or
            $proposed.results.Count -ne 1 -or $proposed.results[0].disposition -ne 'RequiresReview' -or
            -not $review.reviewRequested -or $review.submissionApproved -or
            $denied.arguments.applicationRef -ne $applicationRef -or -not ($denied.error -or $denied.result.is_error -or $denied.result.isError) -or
            ($denied | ConvertTo-Json -Depth 20 -Compress) -notmatch 'BlockedPermission' -or
            $cancel.applicationRef -ne $applicationRef -or $cancel.status -ne 'Cancelled' -or
            $status.applicationRef -ne $applicationRef -or $status.status -ne 'Cancelled' -or $status.submissionApproved -or $status.evidence) { throw 'Second turn result contract mismatch.' }
        $verified = Invoke-Driver 'verify-final' $applicationRef
        if (-not $verified.finalVerified) { throw 'Stored final state did not independently confirm the host outcomes.' }
    }
    $pass = $true
} catch { $failure = $_.Exception.Message; throw }
finally {
    if ($driver) {
        if (-not $driver.HasExited) {
            try { $null = Invoke-Driver 'stop' } catch { }
            if (-not $driver.WaitForExit(10000)) { $driver.Kill($true); $driver.WaitForExit() }
        }
        if ($driverError) { [IO.File]::WriteAllText((Join-Path $evidence 'fixture.stderr.log'), $driverError.GetAwaiter().GetResult()) }
        $driver.Dispose()
    }
    $binariesUnchanged = $true
    foreach ($binary in $manifest) {
        $path = if ($binary.set -eq 'mcp') { $McpDirectory } else { $CompanionDirectory }
        if ((Get-FileHash -LiteralPath (Join-Path $path $binary.name)).Hash.ToLowerInvariant() -ne $binary.sha256) { $binariesUnchanged = $false }
    }
    $currentConfig = if (Test-Path -LiteralPath $configPath) { (Get-FileHash -LiteralPath $configPath).Hash } else { $null }
    $configUnchanged = $currentConfig -eq $configHash
    $resolved = [IO.Path]::GetFullPath($privateRoot)
    if (-not $resolved.StartsWith($privateParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $resolved -Leaf) -notmatch '^[a-f0-9]{32}$') { throw 'Refusing cleanup outside the owned local-host directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    $removed = -not (Test-Path -LiteralPath $resolved)
    [ordered]@{
        startedAt = $started.ToString('O'); completedAt = [DateTimeOffset]::UtcNow.ToString('O'); prepareOnly = [bool]$PrepareOnly;
        passed = $pass -and $configUnchanged -and $binariesUnchanged -and $removed; failure = $failure;
        model = if ($RunLive) { $Model } else { $null }; cli = '0.155.0'; actualModelTurns = $turns.Count;
        authentication = if ($RunLive) { 'ExistingChatGPTIsolatedTemporaryCopy' } else { 'NotAccessed' };
        syntheticDataOnly = $true; syntheticTrustedUiSetup = $true; applicationApprovalGranted = $false;
        scopedHostToolInvocationApproval = if ($RunLive) { @('application_execute_approved', 'application_cancel') } else { @() };
        stdioPreparationPassed = $smoke; finalState = $verified; globalConfigUnchanged = $configUnchanged;
        inputBinariesUnchanged = $binariesUnchanged; privateTestDirectoryRemoved = $removed;
        turns = @($turns | ForEach-Object { @{ name = $_.name; usage = $_.usage; exitCode = $_.exitCode; timedOut = $_.timedOut;
            toolCalls = $_.calls.Count; final = $_.final } })
    } | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $evidence 'summary.json')
    if (-not $configUnchanged -or -not $binariesUnchanged -or -not $removed) { throw 'Isolation or input-binary integrity check failed; result cannot be called passed.' }
}
Write-Host ('Local12 ' + $(if ($PrepareOnly) { 'preparation (no model)' } else { 'actual Codex host verification' }) + ' passed. Evidence: ' + $evidence)
