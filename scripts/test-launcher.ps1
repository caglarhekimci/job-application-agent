#Requires -Version 7.0
param([string]$LauncherDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'use-toolchain.ps1')
if (-not $LauncherDirectory) { $LauncherDirectory = Join-Path $repoRoot 'src/JobAgent.Cli/bin/Release/net10.0' }
$LauncherDirectory = [IO.Path]::GetFullPath($LauncherDirectory)
$runtimeTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('jobagent-launch-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runtimeTestRoot | Out-Null
$staleBridge = Join-Path $runtimeTestRoot 'host-bridge.dpapi'
[IO.File]::WriteAllText($staleBridge, 'stale-registration-from-an-interrupted-instance')
$process = $null
try {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.ArgumentList.Add((Join-Path $LauncherDirectory 'JobAgent.Cli.dll'))
    $start.ArgumentList.Add('demo')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['JOBAGENT_RUNTIME_DIR'] = $runtimeTestRoot
    $start.Environment['JOBAGENT_ENABLE_SYNTHETIC_COMMANDS'] = '0'
    $process = [Diagnostics.Process]::Start($start)
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { throw ('Launcher exited: ' + $process.StandardError.ReadToEnd()) }
        try {
            $page = Invoke-WebRequest 'http://127.0.0.1:5178/' -TimeoutSec 2
            $ready = $page.StatusCode -eq 200 -and $page.Content -match 'id="root"'
            if ($ready) { break }
        } catch { }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'CLI did not serve the built dashboard.' }
    if (Test-Path -LiteralPath $staleBridge) { throw 'Default-off startup left a stale host bridge registration.' }
    $scripts = [regex]::Matches($page.Content, '(?:src|href)="(/assets/[^\"]+)"')
    if ($scripts.Count -lt 2) { throw 'Compiled script/style references are missing.' }
    foreach ($asset in $scripts) {
        $response = Invoke-WebRequest ('http://127.0.0.1:5178' + $asset.Groups[1].Value) -TimeoutSec 5
        if ($response.StatusCode -ne 200 -or $response.Headers['Content-Type'] -match 'text/html') { throw 'Compiled asset was not served correctly.' }
    }
    $health = Invoke-RestMethod 'http://127.0.0.1:5179/health' -TimeoutSec 5
    if (-not $health.synthetic) { throw 'The paired fixture site is not healthy.' }
    $second = [Diagnostics.Process]::Start($start)
    try {
        if (-not $second.WaitForExit(5000) -or $second.ExitCode -ne 1) { throw 'Second launcher did not reject the locked runtime.' }
    } finally { if (-not $second.HasExited) { $second.Kill($true) }; $second.Dispose() }
    Write-Host 'CLI smoke passed: dashboard, all referenced assets, paired fixture, exclusive runtime lock, stale bridge removal.'
} finally {
    if ($process) { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
    $resolved = [IO.Path]::GetFullPath($runtimeTestRoot)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'jobagent-launch-test-*') {
        throw 'Refusing cleanup outside the launcher test temp directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
