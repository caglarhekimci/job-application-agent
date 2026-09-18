$ErrorActionPreference = 'Stop'
$candidateDotnet = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/dotnet'
if (Test-Path (Join-Path $candidateDotnet 'dotnet.exe')) {
    $env:DOTNET_ROOT = $candidateDotnet
    $env:PATH = "$candidateDotnet;$env:PATH"
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
