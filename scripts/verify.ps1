#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    . ./scripts/use-toolchain.ps1
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $reportRoot = Join-Path (Get-Location) "artifacts/verification/$stamp"
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
    & npm run build --prefix web 2>&1 | Tee-Object (Join-Path $reportRoot 'frontend.log')
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    & dotnet restore JobAgent.slnx --locked-mode 2>&1 | Tee-Object (Join-Path $reportRoot 'restore.log')
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    & dotnet build JobAgent.slnx -c Release --no-restore 2>&1 | Tee-Object (Join-Path $reportRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet format whitespace JobAgent.slnx --verify-no-changes --no-restore 2>&1 | Tee-Object (Join-Path $reportRoot 'format.log')
    if ($LASTEXITCODE -ne 0) { throw 'Formatting check failed.' }
    $testProjects = Get-ChildItem -Path tests -Filter '*.Tests.csproj' -Recurse
    foreach ($testProject in $testProjects) {
        $results = Join-Path $reportRoot $testProject.BaseName
        & dotnet test $testProject.FullName -c Release --no-build --logger 'trx;LogFileName=results.trx' --results-directory $results 2>&1 |
            Tee-Object (Join-Path $reportRoot ($testProject.BaseName + '.log'))
        if ($LASTEXITCODE -ne 0) { throw "Tests failed in $($testProject.Name)." }
    }
    & ./scripts/test-launcher.ps1 2>&1 | Tee-Object (Join-Path $reportRoot 'launcher.log')
    & ./scripts/run-evaluation.ps1 2>&1 | Tee-Object (Join-Path $reportRoot 'evaluation.log')
    & ./evals/model-comparison/test-harness.ps1 2>&1 | Tee-Object (Join-Path $reportRoot 'model-scorer-replay.log')
    & ./scripts/test-source-scan.ps1 2>&1 | Tee-Object (Join-Path $reportRoot 'source-scan-tests.log')
    & ./scripts/scan-public.ps1 2>&1 | Tee-Object (Join-Path $reportRoot 'source-scan.log')
    & git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace errors in git diff.' }
    Write-Host "Verification passed. Actual logs and per-project TRX files: $reportRoot"
} finally { Pop-Location }
