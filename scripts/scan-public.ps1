#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $files = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository files.' }
    $privateName = '(^|/)(\.env[^/]*|[^/]+\.(db|sqlite|pfx|key|pem|p12|p7b|jks))$|(^|/)(node_modules|browser-state|\.auth)/'
    $badNames = @($files | Where-Object { $_ -match $privateName })
    if ($badNames.Count) { throw "Private/runtime files would enter source: $($badNames -join ', ')" }
    $pattern = '(gh[pousr]_[A-Za-z0-9]{30,}|AKIA[0-9A-Z]{16}|-----BEGIN (RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----|sk-proj-[A-Za-z0-9_-]{30,})'
    foreach ($file in $files) {
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            if ((Get-Item -LiteralPath $file).Length -gt 10MB) { throw "Oversized source file needs manual review: $file" }
            if ([IO.File]::ReadAllText((Join-Path (Get-Location) $file)) -match $pattern) { throw "Possible secret in $file; do not publish." }
        }
    }
    $revisions = @(& git rev-list --all)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate git history.' }
    foreach ($revision in $revisions) {
        $historyFiles = @(& git -c core.quotepath=false ls-tree -r --name-only $revision)
        foreach ($historyFile in $historyFiles) {
            if ($historyFile -match $privateName) { throw "Private file in history $revision : $historyFile" }
            $contents = (& git show "${revision}:$historyFile") -join "`n"
            if ($LASTEXITCODE -ne 0) { throw "Cannot inspect historical file $historyFile" }
            if ($contents -match $pattern) { throw "Possible secret in history $revision : $historyFile" }
        }
    }
    Write-Host "Source scan passed: $($files.Count) candidate files, $($revisions.Count) commits. Known-secret patterns only; human privacy review required."
} finally { Pop-Location }
