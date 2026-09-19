$ErrorActionPreference = 'Stop'
$candidateDotnet = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/dotnet'
if (Test-Path (Join-Path $candidateDotnet 'dotnet.exe')) {
    $env:DOTNET_ROOT = $candidateDotnet
    $env:PATH = "$candidateDotnet;$env:PATH"
}
elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $env:DOTNET_ROOT = Split-Path (Get-Command dotnet).Source -Parent
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
