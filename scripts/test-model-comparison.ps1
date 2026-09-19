[CmdletBinding()]
param(
    [ValidateSet('Replay', 'Live', 'Rescore')]
    [string]$Mode = 'Replay',
    [string]$PilotPath,
    [string]$ReplayPath,
    [string]$OutputDirectory,
    [string]$CodexPath,
    [string]$Model = 'gpt-6-astra',
    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$comparisonRoot = Join-Path $repoRoot 'evals\model-comparison'
if ([string]::IsNullOrWhiteSpace($PilotPath)) { $PilotPath = Join-Path $comparisonRoot 'pilot-v1.json' }
if ([string]::IsNullOrWhiteSpace($ReplayPath)) { $ReplayPath = Join-Path $comparisonRoot 'replay\captured-synthetic-v1.json' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
    $OutputDirectory = Join-Path $comparisonRoot "results\$stamp"
}
if ([string]::IsNullOrWhiteSpace($CodexPath)) {
    $CodexPath = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\tools\codex-0.155.0\node_modules\@openai\codex-win32-x64\vendor\x86_64-pc-windows-msvc\bin\codex.exe'
}

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-TextSha256([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))).ToUpperInvariant()
}

function Get-PublicExecutablePath([string]$Path) {
    if ($Path.StartsWith('%LOCALAPPDATA%\', [StringComparison]::OrdinalIgnoreCase)) { return $Path }
    $fullPath = [IO.Path]::GetFullPath($Path)
    $localAppData = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\')
    $localPrefix = $localAppData + '\'
    if ($fullPath.StartsWith($localPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return '%LOCALAPPDATA%\' + $fullPath.Substring($localPrefix.Length)
    }
    return '<redacted-executable-directory>\' + [IO.Path]::GetFileName($fullPath)
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required JSON file is missing: $Path" }
    Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json -Depth 100
}

function Get-ScalarText($Value) {
    if ($null -eq $Value) { return '<null>' }
    if ($Value -is [bool]) { return $Value.ToString().ToLowerInvariant() }
    if ($Value -is [string]) { return $Value }
    if ($Value -is [System.Collections.IEnumerable]) {
        return (@($Value) -join ',')
    }
    return [string]$Value
}

function Assert-Pilot($Pilot) {
    if ($Pilot.synthetic -ne $true) { throw 'Pilot must be explicitly synthetic.' }
    if ($Pilot.license -ne 'MIT') { throw 'Pilot license must be MIT.' }
    if (@($Pilot.profiles).Count -ne 12) { throw 'Pilot must contain 12 profiles.' }
    if (@($Pilot.cases).Count -ne 24) { throw 'Pilot must contain 24 cases.' }
    if ($Pilot.datasetSha256 -notmatch '^[A-F0-9]{64}$') { throw 'Pilot datasetSha256 is invalid.' }

    $datasetPath = Join-Path $repoRoot 'evals\datasets\expanded-synthetic-v1.json'
    if ((Get-Sha256 $datasetPath) -ne $Pilot.datasetSha256) { throw 'Expanded dataset hash no longer matches the frozen pilot.' }

    $profileIds = @($Pilot.profiles | ForEach-Object { $_.id })
    if (@($profileIds | Sort-Object -Unique).Count -ne 12) { throw 'Pilot profile IDs must be unique.' }
    $caseIds = @($Pilot.cases | ForEach-Object { $_.id })
    if (@($caseIds | Sort-Object -Unique).Count -ne 24) { throw 'Pilot case IDs must be unique.' }
    foreach ($case in $Pilot.cases) {
        if ($profileIds -notcontains $case.profileId) { throw "Case $($case.id) references an unknown profile." }
        if ($case.expected.status -notin @('Resolved', 'NeedsInput', 'RequiresReview', 'ManualOnly')) {
            throw "Case $($case.id) has an unsupported expected status."
        }
        if ($case.expected.status -eq 'Resolved' -and [string]::IsNullOrEmpty([string]$case.expected.value)) {
            throw "Resolved case $($case.id) needs an expected value."
        }
        if ($case.expected.status -ne 'Resolved' -and $null -ne $case.expected.value) {
            throw "Abstention case $($case.id) must have a null expected value."
        }
    }
}

function Assert-Freeze($Pilot, [string]$B1Prompt, [string]$B2Prompt, [string]$SchemaPath) {
    $manifest = Read-Json (Join-Path $comparisonRoot 'freeze-manifest-v1.json')
    $actual = [ordered]@{
        datasetSha256 = [string]$Pilot.datasetSha256
        pilotSha256 = Get-Sha256 $PilotPath
        outputSchemaSha256 = Get-Sha256 $SchemaPath
        B1 = Get-TextSha256 $B1Prompt
        B2 = Get-TextSha256 $B2Prompt
    }
    if ($manifest.beforeAnyModelOutput -ne $true -or $manifest.profileCount -ne 12 -or $manifest.caseCount -ne 24 -or $manifest.repetitionsPerStrategy -ne 3) {
        throw 'Freeze manifest scope is invalid.'
    }
    foreach ($name in @('datasetSha256', 'pilotSha256', 'outputSchemaSha256')) {
        if ([string]$manifest.$name -cne [string]$actual.$name) { throw "Frozen $name does not match current input." }
    }
    if ([string]$manifest.promptSha256.B1 -cne $actual.B1 -or [string]$manifest.promptSha256.B2 -cne $actual.B2) {
        throw 'A rendered prompt changed after the comparison was frozen.'
    }
}

function New-B1Prompt($Pilot) {
    $header = (Get-Content -LiteralPath (Join-Path $comparisonRoot 'prompts\b1-general-v1.txt') -Raw -Encoding utf8).TrimEnd()
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add($header)
    $lines.Add('')
    $lines.Add('SYNTHETIC CV-LIKE SOURCE RECORDS')
    foreach ($profile in $Pilot.profiles) {
        $lines.Add("PROFILE $($profile.id) [$($profile.split)]")
        foreach ($fact in $profile.facts) {
            $parts = foreach ($property in $fact.PSObject.Properties) {
                "$($property.Name)=$(Get-ScalarText $property.Value)"
            }
            $lines.Add('- ' + ($parts -join '; '))
        }
        if (@($profile.facts).Count -eq 0) { $lines.Add('- No supplied facts.') }
    }
    $lines.Add('')
    $lines.Add('QUESTIONS')
    foreach ($case in $Pilot.cases) {
        $parts = foreach ($property in $case.question.PSObject.Properties) {
            "$($property.Name)=$(Get-ScalarText $property.Value)"
        }
        $lines.Add("CASE $($case.id); profileId=$($case.profileId); " + ($parts -join '; '))
    }
    $lines.Add('')
    $lines.Add('Return JSON only. Include each caseId exactly once.')
    return ($lines -join "`n") + "`n"
}

function New-B2Prompt($Pilot) {
    $header = (Get-Content -LiteralPath (Join-Path $comparisonRoot 'prompts\b2-evidence-rules-v1.txt') -Raw -Encoding utf8).TrimEnd()
    $cases = foreach ($case in $Pilot.cases) {
        [ordered]@{ caseId = $case.id; profileId = $case.profileId; question = $case.question }
    }
    $payload = [ordered]@{
        synthetic = $true
        asOf = $Pilot.asOf
        profiles = $Pilot.profiles
        cases = @($cases)
    } | ConvertTo-Json -Depth 20 -Compress
    return "$header`n`nTYPED PAYLOAD`n$payload`n`nReturn JSON only. Include each caseId exactly once.`n"
}

function Test-SameValue($Left, $Right) {
    if ($null -eq $Left -and $null -eq $Right) { return $true }
    if ($null -eq $Left -or $null -eq $Right) { return $false }
    return [string]$Left -ceq [string]$Right
}

function Get-RunScore($Pilot, $Output, [string]$Strategy, [int]$Repetition, [bool]$ProcessSucceeded, [string]$Failure,
        $Usage, [string[]]$EventTypes, [string]$StartedAt, [string]$FinishedAt, [string]$EventsSha256,
        [string]$OutputSha256) {
    $caseCount = @($Pilot.cases).Count
    $score = [ordered]@{
        strategy = $Strategy
        repetition = $Repetition
        startedAt = $StartedAt
        finishedAt = $FinishedAt
        completed = $false
        failure = $Failure
        strictCorrect = 0
        denominator = $caseCount
        answerableCorrect = 0
        answerableDenominator = @($Pilot.cases | Where-Object { $_.expected.status -eq 'Resolved' }).Count
        appropriateAbstention = 0
        abstentionDenominator = @($Pilot.cases | Where-Object { $_.expected.status -ne 'Resolved' }).Count
        badClaims = 0
        abstentionWithValue = 0
        unsupportedEvidenceCitations = 0
        ungroundedResolvedAnswers = 0
        usage = $Usage
        eventTypes = @($EventTypes)
        eventsSha256 = $EventsSha256
        outputSha256 = $OutputSha256
        cases = @()
    }
    if (-not $ProcessSucceeded) { return [pscustomobject]$score }

    try {
        if ($null -eq $Output -or $null -eq $Output.answers) { throw 'Output has no answers array.' }
        $answers = @($Output.answers)
        if ($answers.Count -ne $caseCount) { throw "Output contains $($answers.Count) answers; expected $caseCount." }
        $ids = @($answers | ForEach-Object { $_.caseId })
        if (@($ids | Sort-Object -Unique).Count -ne $caseCount) { throw 'Output contains duplicate case IDs.' }
        $expectedIds = @($Pilot.cases | ForEach-Object { $_.id })
        if (@($ids | Where-Object { $_ -notin $expectedIds }).Count -gt 0 -or @($expectedIds | Where-Object { $_ -notin $ids }).Count -gt 0) {
            throw 'Output case IDs do not exactly match the pilot.'
        }

        $byId = @{}
        foreach ($answer in $answers) {
            $propertyNames = @($answer.PSObject.Properties.Name | Sort-Object)
            $requiredNames = @('caseId', 'evidenceIds', 'reason', 'status', 'value')
            if (($propertyNames -join '|') -cne ($requiredNames -join '|')) { throw "Answer $($answer.caseId) does not match the output contract." }
            if ($answer.status -notin @('Resolved', 'NeedsInput', 'RequiresReview', 'ManualOnly')) { throw "Answer $($answer.caseId) has an invalid status." }
            if ([string]::IsNullOrWhiteSpace([string]$answer.reason)) { throw "Answer $($answer.caseId) has no reason." }
            if ($answer.status -eq 'Resolved' -and [string]::IsNullOrEmpty([string]$answer.value)) { throw "Resolved answer $($answer.caseId) has no value." }
            if (@($answer.evidenceIds | Sort-Object -Unique).Count -ne @($answer.evidenceIds).Count) { throw "Answer $($answer.caseId) repeats evidence IDs." }
            $byId[$answer.caseId] = $answer
        }

        $caseScores = foreach ($case in $Pilot.cases) {
            $actual = $byId[$case.id]
            $statusMatched = [string]$actual.status -ceq [string]$case.expected.status
            $valueMatched = Test-SameValue $actual.value $case.expected.value
            $strictMatched = $statusMatched -and $valueMatched
            if ($strictMatched) { $score.strictCorrect++ }
            if ($case.expected.status -eq 'Resolved' -and $strictMatched) { $score.answerableCorrect++ }
            if ($case.expected.status -ne 'Resolved' -and $actual.status -ne 'Resolved') { $score.appropriateAbstention++ }
            $badClaim = $case.expected.status -ne 'Resolved' -and $actual.status -eq 'Resolved'
            if ($badClaim) { $score.badClaims++ }
            $retainedValue = $actual.status -ne 'Resolved' -and $null -ne $actual.value
            if ($retainedValue) { $score.abstentionWithValue++ }
            $unsupported = @($actual.evidenceIds | Where-Object { $_ -notin @($case.allowedEvidenceIds) }).Count
            $score.unsupportedEvidenceCitations += $unsupported
            $ungrounded = $actual.status -eq 'Resolved' -and @($actual.evidenceIds).Count -eq 0
            if ($ungrounded) { $score.ungroundedResolvedAnswers++ }
            [ordered]@{
                caseId = $case.id
                expectedStatus = $case.expected.status
                actualStatus = $actual.status
                statusMatched = $statusMatched
                valueMatched = $valueMatched
                strictMatched = $strictMatched
                badClaim = $badClaim
                abstentionWithValue = $retainedValue
                unsupportedEvidenceCitations = $unsupported
                ungroundedResolved = $ungrounded
            }
        }
        $score.cases = @($caseScores)
        $score.completed = $true
        $score.failure = $null
    }
    catch {
        $score.failure = $_.Exception.Message
    }
    return [pscustomobject]$score
}

function Get-StrategySummary([object[]]$Runs, [string]$Strategy) {
    $selected = @($Runs | Where-Object { $_.strategy -eq $Strategy })
    [ordered]@{
        runCount = $selected.Count
        completedRuns = @($selected | Where-Object completed).Count
        failedRuns = @($selected | Where-Object { -not $_.completed }).Count
        strict = [ordered]@{ numerator = ($selected | Measure-Object strictCorrect -Sum).Sum; denominator = ($selected | Measure-Object denominator -Sum).Sum }
        answerable = [ordered]@{ numerator = ($selected | Measure-Object answerableCorrect -Sum).Sum; denominator = ($selected | Measure-Object answerableDenominator -Sum).Sum }
        abstention = [ordered]@{ numerator = ($selected | Measure-Object appropriateAbstention -Sum).Sum; denominator = ($selected | Measure-Object abstentionDenominator -Sum).Sum }
        badClaims = ($selected | Measure-Object badClaims -Sum).Sum
        abstentionWithValue = ($selected | Measure-Object abstentionWithValue -Sum).Sum
        unsupportedEvidenceCitations = ($selected | Measure-Object unsupportedEvidenceCitations -Sum).Sum
        ungroundedResolvedAnswers = ($selected | Measure-Object ungroundedResolvedAnswers -Sum).Sum
        usage = [ordered]@{
            inputTokens = ($selected | ForEach-Object { $_.usage.inputTokens } | Measure-Object -Sum).Sum
            cachedInputTokens = ($selected | ForEach-Object { $_.usage.cachedInputTokens } | Measure-Object -Sum).Sum
            outputTokens = ($selected | ForEach-Object { $_.usage.outputTokens } | Measure-Object -Sum).Sum
        }
    }
}

function Invoke-ProcessCapture([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory, [string]$StandardInput,
        [int]$Timeout) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    foreach ($argument in $Arguments) { [void]$start.ArgumentList.Add($argument) }
    foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'AZURE_OPENAI_API_KEY', 'OPENAI_BASE_URL')) {
        [void]$start.Environment.Remove($name)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw "Could not start $Executable." }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if ($null -ne $StandardInput) { $process.StandardInput.Write($StandardInput) }
    $process.StandardInput.Close()
    $timedOut = -not $process.WaitForExit($Timeout * 1000)
    if ($timedOut) {
        try { $process.Kill($true) } catch { }
        $process.WaitForExit()
    }
    [pscustomobject]@{
        ExitCode = if ($timedOut) { -1 } else { $process.ExitCode }
        TimedOut = $timedOut
        StdOut = $stdoutTask.GetAwaiter().GetResult()
        StdErr = $stderrTask.GetAwaiter().GetResult()
    }
}

function Read-CodexEvents([string]$Jsonl) {
    $events = [Collections.Generic.List[object]]::new()
    foreach ($line in ($Jsonl -split "`r?`n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $events.Add(($line | ConvertFrom-Json -Depth 50))
    }
    return @($events)
}

function Get-EventSummary([object[]]$Events) {
    $types = [Collections.Generic.List[string]]::new()
    foreach ($event in $Events) {
        if ($event.type -in @('item.started', 'item.updated', 'item.completed')) {
            $types.Add("$($event.type):$($event.item.type)")
        } else {
            $types.Add([string]$event.type)
        }
    }
    return @($types)
}

function Find-BoundaryViolation([string[]]$EventTypes) {
    $itemTypes = foreach ($eventType in $EventTypes) {
        if ($eventType -match '^item\.(started|updated|completed):(.+)$') { $Matches[2] }
    }
    $unexpected = @($itemTypes | Where-Object { $_ -notin @('reasoning', 'agent_message') } | Sort-Object -Unique)
    if ($unexpected.Count -gt 0) { return 'Unexpected Codex item types: ' + ($unexpected -join ', ') }
    return $null
}

function Get-Usage([object[]]$Events) {
    $completed = @($Events | Where-Object { $_.type -eq 'turn.completed' }) | Select-Object -Last 1
    if ($null -eq $completed -or $null -eq $completed.usage) {
        return [pscustomobject]@{ inputTokens = 0; cachedInputTokens = 0; outputTokens = 0 }
    }
    [pscustomobject]@{
        inputTokens = [int64]$completed.usage.input_tokens
        cachedInputTokens = [int64]$completed.usage.cached_input_tokens
        outputTokens = [int64]$completed.usage.output_tokens
    }
}

function Invoke-LiveRun([string]$Strategy, [int]$Repetition, [string]$Prompt, [string]$SchemaPath, [string]$OutputRoot) {
    $runDirectory = Join-Path $OutputRoot ("{0}-run-{1}" -f $Strategy.ToLowerInvariant(), $Repetition)
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    $runtime = Join-Path ([IO.Path]::GetTempPath()) ('job-agent-codex-comparison-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    $lastMessage = Join-Path $runtime 'last-message.json'
    $started = [DateTimeOffset]::UtcNow
    try {
        $arguments = @(
            'exec', '--ignore-user-config', '--ephemeral',
            '--disable', 'shell_tool', '--disable', 'apps', '--disable', 'plugins',
            '--disable', 'multi_agent', '--disable', 'shell_snapshot',
            '-m', $Model, '-c', 'forced_login_method="chatgpt"', '-c', 'web_search="disabled"',
            '--sandbox', 'read-only', '--skip-git-repo-check', '--cd', $runtime,
            '--output-schema', $SchemaPath, '--output-last-message', $lastMessage, '--json', '-'
        )
        $process = Invoke-ProcessCapture $CodexPath $arguments $runtime $Prompt $TimeoutSeconds
        $finished = [DateTimeOffset]::UtcNow
        $eventsPath = Join-Path $runDirectory 'events.jsonl'
        $stderrPath = Join-Path $runDirectory 'stderr.txt'
        Set-Content -LiteralPath $eventsPath -Value $process.StdOut -Encoding utf8NoBOM
        Set-Content -LiteralPath $stderrPath -Value $process.StdErr -Encoding utf8NoBOM
        $events = @()
        $eventError = $null
        try { $events = @(Read-CodexEvents $process.StdOut) } catch { $eventError = 'Invalid JSONL: ' + $_.Exception.Message }
        $eventTypes = if ($eventError) { @() } else { @(Get-EventSummary $events) }
        $boundaryError = Find-BoundaryViolation $eventTypes
        $failure = if ($process.TimedOut) { "Timed out after $TimeoutSeconds seconds." }
            elseif ($process.ExitCode -ne 0) { "Codex exited $($process.ExitCode)." }
            elseif ($eventError) { $eventError }
            elseif ($boundaryError) { $boundaryError }
            elseif (@($events | Where-Object { $_.type -eq 'turn.failed' }).Count -gt 0) { 'Codex reported turn.failed.' }
            elseif (@($events | Where-Object { $_.type -eq 'turn.completed' }).Count -ne 1) { 'Expected exactly one turn.completed event.' }
            elseif (-not (Test-Path -LiteralPath $lastMessage -PathType Leaf)) { 'Codex did not write a final response.' }
            else { $null }
        $output = $null
        $outputHash = $null
        if (Test-Path -LiteralPath $lastMessage -PathType Leaf) {
            $destination = Join-Path $runDirectory 'last-message.json'
            Copy-Item -LiteralPath $lastMessage -Destination $destination -Force
            $outputHash = Get-Sha256 $destination
            try { $output = Read-Json $destination } catch { if (-not $failure) { $failure = 'Invalid final JSON: ' + $_.Exception.Message } }
        }
        $metadata = [ordered]@{
            strategy = $Strategy; repetition = $Repetition; requestedModel = $Model
            startedAt = $started.ToString('O'); finishedAt = $finished.ToString('O')
            exitCode = $process.ExitCode; timedOut = $process.TimedOut
            eventTypes = @($eventTypes); eventsSha256 = Get-Sha256 $eventsPath; outputSha256 = $outputHash
        }
        $metadata | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runDirectory 'run-metadata.json') -Encoding utf8NoBOM
        return Get-RunScore $script:pilot $output $Strategy $Repetition (-not $failure) $failure (Get-Usage $events) $eventTypes `
            $started.ToString('O') $finished.ToString('O') (Get-Sha256 $eventsPath) $outputHash
    }
    finally {
        if (Test-Path -LiteralPath $runtime) { Remove-Item -LiteralPath $runtime -Recurse -Force }
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$script:pilot = Read-Json $PilotPath
Assert-Pilot $pilot
$schemaPath = Join-Path $comparisonRoot 'output-schema-v1.json'
$b1Prompt = New-B1Prompt $pilot
$b2Prompt = New-B2Prompt $pilot
Assert-Freeze $pilot $b1Prompt $b2Prompt $schemaPath
$b1Path = Join-Path $OutputDirectory 'b1-prompt.txt'
$b2Path = Join-Path $OutputDirectory 'b2-prompt.txt'
Set-Content -LiteralPath $b1Path -Value $b1Prompt -Encoding utf8NoBOM -NoNewline
Set-Content -LiteralPath $b2Path -Value $b2Prompt -Encoding utf8NoBOM -NoNewline

$runs = [Collections.Generic.List[object]]::new()
$rescoreSourceSha256 = $null
$cliProof = [ordered]@{
    invoked = $false
    executable = if ($Mode -eq 'Live') { Get-PublicExecutablePath $CodexPath } else { $null }
    executablePathRedacted = ($Mode -eq 'Live')
    version = if ($Mode -eq 'Live') { $null } else { 'NotInvoked' }
    authentication = if ($Mode -eq 'Live') { $null } else { 'NotInvoked' }
    requestedModel = if ($Mode -eq 'Live') { $Model } else { 'NotInvoked' }
    servedModel = 'NotExposedByJsonl'
    apiCredentialEnvironmentRemoved = ($Mode -eq 'Live')
    sandbox = if ($Mode -eq 'Live') { 'read-only' } else { 'NotInvoked' }
    tools = if ($Mode -eq 'Live') { 'shell/apps/plugins/multi-agent/web disabled; no MCP configured' } else { 'NotInvoked' }
}
if ($Mode -eq 'Live' -and ($cliProof.executable -match '^[A-Za-z]:\\' -or $cliProof.executable -match '(?i)\\Users\\')) {
    throw 'Public CLI metadata contains a machine-specific absolute path.'
}

if ($Mode -eq 'Replay') {
    $capture = Read-Json $ReplayPath
    if ($capture.syntheticReplay -ne $true) { throw 'Replay capture must be explicitly synthetic.' }
    if (@($capture.runs).Count -ne 6) { throw 'Replay capture must contain six runs.' }
    foreach ($capturedRun in $capture.runs) {
        $outputPath = Join-Path (Split-Path -Parent $ReplayPath) $capturedRun.outputFile
        $output = Read-Json $outputPath
        $eventTypes = @($capturedRun.eventTypes)
        $boundaryError = Find-BoundaryViolation $eventTypes
        $failure = if ($capturedRun.exitCode -ne 0) { "Captured exit code $($capturedRun.exitCode)." } elseif ($boundaryError) { $boundaryError } else { $null }
        $score = Get-RunScore $pilot $output $capturedRun.strategy ([int]$capturedRun.repetition) (-not $failure) $failure `
            $capturedRun.usage $eventTypes $null $null $null (Get-Sha256 $outputPath)
        $runs.Add($score)
    }
}
elseif ($Mode -eq 'Rescore') {
    if (-not (Test-Path -LiteralPath $ReplayPath -PathType Container)) { throw "Rescore source directory is missing: $ReplayPath" }
    $priorReportPath = Join-Path $ReplayPath 'report.json'
    $priorReport = Read-Json $priorReportPath
    if ($priorReport.mode -notin @('Live', 'Rescore')) { throw 'Only a live comparison report can be rescored.' }
    if ([string]$priorReport.pilotSha256 -cne (Get-Sha256 $PilotPath) -or
        [string]$priorReport.promptSha256.B1 -cne (Get-TextSha256 $b1Prompt) -or
        [string]$priorReport.promptSha256.B2 -cne (Get-TextSha256 $b2Prompt)) {
        throw 'Live artifacts do not match the frozen pilot and prompts.'
    }
    $rescoreSourceSha256 = Get-Sha256 $priorReportPath
    $cliProof = $priorReport.cli
    $cliProof.executable = Get-PublicExecutablePath ([string]$cliProof.executable)
    $cliProof | Add-Member -NotePropertyName executablePathRedacted -NotePropertyValue $true -Force
    foreach ($strategy in @('B1', 'B2')) {
        foreach ($repetition in 1..3) {
            $runDirectory = Join-Path $ReplayPath ("{0}-run-{1}" -f $strategy.ToLowerInvariant(), $repetition)
            $metadataPath = Join-Path $runDirectory 'run-metadata.json'
            $eventsPath = Join-Path $runDirectory 'events.jsonl'
            $outputPath = Join-Path $runDirectory 'last-message.json'
            $metadata = Read-Json $metadataPath
            $eventsText = Get-Content -LiteralPath $eventsPath -Raw -Encoding utf8
            $events = @(Read-CodexEvents $eventsText)
            $eventTypes = @(Get-EventSummary $events)
            $boundaryError = Find-BoundaryViolation $eventTypes
            $failure = if ($metadata.timedOut) { 'Original run timed out.' }
                elseif ($metadata.exitCode -ne 0) { "Original Codex exit code $($metadata.exitCode)." }
                elseif ($boundaryError) { $boundaryError }
                elseif (@($events | Where-Object { $_.type -eq 'turn.failed' }).Count -gt 0) { 'Original Codex run reported turn.failed.' }
                elseif (@($events | Where-Object { $_.type -eq 'turn.completed' }).Count -ne 1) { 'Expected exactly one original turn.completed event.' }
                elseif (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) { 'Original run has no final response.' }
                else { $null }
            $output = if (Test-Path -LiteralPath $outputPath -PathType Leaf) { Read-Json $outputPath } else { $null }
            $score = Get-RunScore $pilot $output $strategy $repetition (-not $failure) $failure (Get-Usage $events) $eventTypes `
                $metadata.startedAt $metadata.finishedAt (Get-Sha256 $eventsPath) `
                $(if (Test-Path -LiteralPath $outputPath -PathType Leaf) { Get-Sha256 $outputPath } else { $null })
            $runs.Add($score)
        }
    }
}
else {
    if (-not (Test-Path -LiteralPath $CodexPath -PathType Leaf)) { throw "Official Codex CLI is missing: $CodexPath" }
    $version = Invoke-ProcessCapture $CodexPath @('--version') $OutputDirectory $null 30
    if ($version.ExitCode -ne 0 -or $version.StdOut -notmatch '^codex-cli 0\.155\.0') { throw "Expected official codex-cli 0.155.0; received: $($version.StdOut)$($version.StdErr)" }
    $login = Invoke-ProcessCapture $CodexPath @('-c', 'forced_login_method="chatgpt"', 'login', 'status') $OutputDirectory $null 30
    $loginText = $login.StdOut + $login.StdErr
    if ($login.ExitCode -ne 0 -or $loginText -notmatch 'Logged in using ChatGPT') { throw 'Official Codex CLI is not logged in with ChatGPT.' }
    if ($loginText -match 'API key') { throw 'Codex login status did not prove ChatGPT authentication.' }
    $cliProof.invoked = $true
    $cliProof.version = $version.StdOut.Trim()
    $cliProof.authentication = 'Existing ChatGPT login; forced_login_method=chatgpt'
    foreach ($strategy in @('B1', 'B2')) {
        $prompt = if ($strategy -eq 'B1') { $b1Prompt } else { $b2Prompt }
        foreach ($repetition in 1..3) {
            $runs.Add((Invoke-LiveRun $strategy $repetition $prompt $schemaPath $OutputDirectory))
        }
    }
}

$report = [ordered]@{
    schemaVersion = '1.0'
    generatedAt = [DateTimeOffset]::UtcNow.ToString('O')
    mode = $Mode
    synthetic = $true
    pilotId = $pilot.pilotId
    pilotScope = $pilot.scope
    datasetId = $pilot.datasetId
    datasetSha256 = $pilot.datasetSha256
    pilotSha256 = Get-Sha256 $PilotPath
    outputSchemaSha256 = Get-Sha256 $schemaPath
    promptSha256 = [ordered]@{ B1 = Get-TextSha256 $b1Prompt; B2 = Get-TextSha256 $b2Prompt }
    profileCount = @($pilot.profiles).Count
    caseCount = @($pilot.cases).Count
    repetitionsPerStrategy = 3
    rescoreSourceReportSha256 = $rescoreSourceSha256
    cli = $cliProof
    strategies = [ordered]@{ B1 = Get-StrategySummary $runs 'B1'; B2 = Get-StrategySummary $runs 'B2' }
    runs = @($runs)
}
$reportPath = Join-Path $OutputDirectory 'report.json'
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reportPath -Encoding utf8NoBOM
Write-Host "Model comparison $Mode report: $reportPath"
Write-Host ("B1 strict {0}/{1}; B2 strict {2}/{3}; failed runs {4}." -f `
    $report.strategies.B1.strict.numerator, $report.strategies.B1.strict.denominator,
    $report.strategies.B2.strict.numerator, $report.strategies.B2.strict.denominator,
    ($report.strategies.B1.failedRuns + $report.strategies.B2.failedRuns))
