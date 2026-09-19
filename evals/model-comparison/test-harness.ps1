[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script = Join-Path $root 'scripts\test-model-comparison.ps1'
$pilot = Join-Path $PSScriptRoot 'pilot-v1.json'
$replay = Join-Path $PSScriptRoot 'replay\captured-synthetic-v1.json'
$output = Join-Path ([System.IO.Path]::GetTempPath()) ('job-agent-model-replay-' + [guid]::NewGuid().ToString('N'))

try {
    & $script -Mode Replay -PilotPath $pilot -ReplayPath $replay -OutputDirectory $output
    if (-not $?) { throw 'Replay harness failed.' }

    $report = Get-Content (Join-Path $output 'report.json') -Raw | ConvertFrom-Json
    if ($report.synthetic -ne $true) { throw 'Report must remain explicitly synthetic.' }
    if ($report.caseCount -ne 24 -or $report.profileCount -ne 12) { throw 'Pilot counts changed.' }
    if ($report.strategies.B1.strict.numerator -ne 70 -or $report.strategies.B1.strict.denominator -ne 72) {
        throw 'B1 replay strict score was not 70/72.'
    }
    if ($report.strategies.B1.badClaims -ne 1 -or $report.strategies.B1.failedRuns -ne 0) {
        throw 'B1 replay must expose the deliberate bad claim without treating the run as failed.'
    }
    if ($report.strategies.B2.strict.numerator -ne 72 -or $report.strategies.B2.strict.denominator -ne 72) {
        throw 'B2 replay strict score was not 72/72.'
    }
    if ($report.strategies.B2.badClaims -ne 0 -or $report.strategies.B2.failedRuns -ne 0) {
        throw 'B2 replay metrics changed.'
    }
    if ($report.strategies.B1.answerable.denominator -ne 27 -or $report.strategies.B1.abstention.denominator -ne 45) {
        throw 'Replay strata denominators changed.'
    }
    if ($report.strategies.B1.abstention.numerator -ne 44) { throw 'Bad claim was not removed from abstention credit.' }
    if ($report.strategies.B1.abstentionWithValue -ne 1) { throw 'A retained value on abstention must be a case mismatch, not a failed run.' }
    if ($report.datasetSha256 -ne '17EDD99D9DCC17C9074EA782F5457957BDD40AF6AE71D84C0AAD55BADA82D7E0') {
        throw 'Expanded dataset hash changed.'
    }

    foreach ($promptName in @('b1-prompt.txt', 'b2-prompt.txt')) {
        $prompt = Get-Content (Join-Path $output $promptName) -Raw
        if ($prompt -match 'ruleRationale|expectedStatus|expectedValue|A locally confirmed synthetic name resolves exactly') {
            throw "$promptName leaked scorer-only expectations or rationales."
        }
    }

    Write-Host 'Model-comparison replay tests passed: 12 assertions, 6 captured runs, 144 scored outcomes.'
}
finally {
    if (Test-Path $output) { Remove-Item -LiteralPath $output -Recurse -Force }
}
