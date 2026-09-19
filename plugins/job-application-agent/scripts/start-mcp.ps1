#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
try {
    if ($args.Count -ne 0) { throw 'This launcher accepts no command arguments.' }
    if (-not $IsWindows) { throw 'The local inspector currently requires Windows because profiles use Windows DPAPI.' }
    $pluginRoot = Split-Path $PSScriptRoot -Parent
    $runtimeRoot = Join-Path $pluginRoot 'runtime'
    foreach ($file in @('JobAgent.Mcp.dll', 'JobAgent.Mcp.deps.json', 'JobAgent.Mcp.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $runtimeRoot $file) -PathType Leaf)) {
            throw 'The packaged runtime is missing. Use a prepared plugin release or run scripts/build-package.ps1 against the published MCP output; this launcher does not install or build software.'
        }
    }
    $dotnetPath = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
        $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
        if (-not $dotnet) { throw 'The .NET 10 runtime is required. Follow the project installation guide before enabling this plugin.' }
        $dotnetPath = $dotnet.Source
    }
    $env:DOTNET_ROOT = Split-Path $dotnetPath -Parent
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    if (-not $env:JOBAGENT_RUNTIME_DIR) {
        $env:JOBAGENT_RUNTIME_DIR = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/demo'
    }
    # Inherit the STDIO handles; never mix launcher messages into MCP stdout.
    & $dotnetPath (Join-Path $runtimeRoot 'JobAgent.Mcp.dll')
    exit $LASTEXITCODE
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
