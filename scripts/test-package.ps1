#Requires -Version 7.0
param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'use-toolchain.ps1')
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('jobagent-package-test-' + [guid]::NewGuid().ToString('N'))
$process = $null
$client = $null
try {
    foreach ($entry in (Get-Content -LiteralPath (Join-Path $PackageDirectory 'MANIFEST.json') -Raw | ConvertFrom-Json)) {
        $path = Join-Path $PackageDirectory $entry.path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Package manifest mismatch: $($entry.path)" }
    }
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.ArgumentList.Add((Join-Path $PackageDirectory 'cli/JobAgent.Cli.dll'))
    $start.ArgumentList.Add('demo')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['JOBAGENT_RUNTIME_DIR'] = $testRoot
    $process = [Diagnostics.Process]::Start($start)
    $sessionUrl = $null
    for ($lineNumber = 0; $lineNumber -lt 8; $lineNumber++) {
        $read = $process.StandardOutput.ReadLineAsync()
        if (-not $read.Wait(10000)) { throw 'Packaged launcher did not become ready.' }
        $line = $read.Result
        if ($null -eq $line) { throw 'Packaged launcher exited before readiness.' }
        if ($line.StartsWith('http://127.0.0.1:5178/#token=')) { $sessionUrl = $line; break }
    }
    if (-not $sessionUrl) { throw 'Private local session link was not emitted.' }
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer = [Net.CookieContainer]::new()
    $client = [Net.Http.HttpClient]::new($handler)
    $client.BaseAddress = [uri]'http://127.0.0.1:5178'
    $client.DefaultRequestHeaders.Add('Origin', 'http://127.0.0.1:5178')
    $token = ([uri]$sessionUrl).Fragment.Substring(7)
    $json = @{ token = $token } | ConvertTo-Json -Compress
    $response = $client.PostAsync('/api/session', [Net.Http.StringContent]::new($json, [Text.Encoding]::UTF8, 'application/json')).GetAwaiter().GetResult()
    $response.EnsureSuccessStatusCode() | Out-Null
    $session = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $client.DefaultRequestHeaders.Add('X-JobAgent-Csrf', $session.csrf)

    $pdf = [Text.StringBuilder]::new("%PDF-1.4`n")
    $content = 'BT /F1 12 Tf 72 720 Td (Synthetic package resume) Tj ET'
    $objects = @('<< /Type /Catalog /Pages 2 0 R >>', '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
        '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>',
        "<< /Length $($content.Length) >>`nstream`n$content`nendstream", '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>')
    $offsets = [Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $objects.Count; $i++) { $offsets.Add($pdf.Length); [void]$pdf.Append("$($i + 1) 0 obj`n$($objects[$i])`nendobj`n") }
    $xref = $pdf.Length
    [void]$pdf.Append("xref`n0 6`n0000000000 65535 f `n")
    foreach ($offset in $offsets) { [void]$pdf.Append($offset.ToString('D10') + " 00000 n `n") }
    [void]$pdf.Append("trailer`n<< /Size 6 /Root 1 0 R >>`nstartxref`n$xref`n%%EOF`n")
    $pdfBytes = [Text.Encoding]::ASCII.GetBytes($pdf.ToString())

    $docx = [IO.MemoryStream]::new()
    $zip = [IO.Compression.ZipArchive]::new($docx, [IO.Compression.ZipArchiveMode]::Create, $true)
    $entries = @{
        '[Content_Types].xml' = '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'
        '_rels/.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'
        'word/document.xml' = '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Synthetic package resume</w:t></w:r></w:p></w:body></w:document>'
    }
    foreach ($name in $entries.Keys) {
        $stream = $zip.CreateEntry($name).Open()
        $bytes = [Text.Encoding]::UTF8.GetBytes($entries[$name]); $stream.Write($bytes); $stream.Dispose()
    }
    $zip.Dispose()
    $documents = @{ 'synthetic.pdf' = $pdfBytes; 'synthetic.docx' = $docx.ToArray() }
    $docx.Dispose()
    foreach ($name in $documents.Keys) {
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, '/api/workspace/import')
        $request.Headers.Add('X-File-Name', $name)
        $request.Content = [Net.Http.ByteArrayContent]::new($documents[$name])
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if (-not $response.IsSuccessStatusCode -or $body.document.status -ne 'ReadyForReview' -or
            $body.document.text -notmatch 'Synthetic package resume') { throw "Packaged $name import failed." }
        $request.Dispose(); $response.Dispose()
    }
    Write-Host 'Packaged app passed: all manifest hashes, authenticated local UI API, PDF and DOCX through packaged worker.'
} finally {
    if ($client) { $client.Dispose() }
    if ($process) { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'jobagent-package-test-*') { throw 'Unsafe test cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
