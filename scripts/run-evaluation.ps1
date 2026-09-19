#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    . ./scripts/use-toolchain.ps1
    $revision = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'A git revision is required for evaluation evidence.' }
    if (& git status --porcelain) { $revision += '+dirty' }
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $outputRoot = Join-Path (Get-Location) "artifacts/evaluations/$stamp"
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    & dotnet ./src/JobAgent.Cli/bin/Release/net10.0/JobAgent.Cli.dll eval ./evals/datasets/initial-synthetic-v1.json $revision > (Join-Path $outputRoot 'fixture.json')
    if ($LASTEXITCODE -ne 0) { throw "Fixture evaluation failed; inspect $outputRoot" }
    & dotnet ./src/JobAgent.Cli/bin/Release/net10.0/JobAgent.Cli.dll eval-expanded ./evals/datasets/expanded-synthetic-v1.json $revision > (Join-Path $outputRoot 'expanded-fixture.json')
    if ($LASTEXITCODE -ne 0) { throw "Expanded fixture evaluation failed; inspect $outputRoot" }
    $expanded = Get-Content -LiteralPath (Join-Path $outputRoot 'expanded-fixture.json') -Raw | ConvertFrom-Json
    if ($expanded.profileGroupCount -ne 12 -or $expanded.questionCaseCount -ne 240 -or $expanded.jobCaseCount -ne 60 -or
        $expanded.b0Status -ne 'Completed' -or $expanded.b1Status -ne 'NotRun' -or $expanded.b2Status -ne 'NotRun' -or
        @($expanded.results | Where-Object { -not $_.expectedOutcomeMatched }).Count -ne 0) {
        throw 'Expanded B0 result contract or expected outcomes failed.'
    }
    Write-Host "Fixture results: $outputRoot/fixture.json"
    Write-Host 'Expanded B0: 12 synthetic profiles, 240 question outcomes, 60 job outcomes. Model calls: none.'
    Write-Host 'Scenario timestamp is fixed at 2026-09-18; output directory records actual run time. Real model: NotRun.'
} finally { Pop-Location }
