using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobAgent.Core.Profiles;

namespace JobAgent.Infrastructure.Documents;

public enum ResumeImportStatus { ReadyForReview, NeedsOcr }

public sealed record ResumeEvidenceSegment(string SourceSpan, string Text);

public sealed record ResumeImportResult
{
    public string DocumentId { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public string TextHash { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public ResumeImportStatus Status { get; init; }
    public IReadOnlyList<ResumeEvidenceSegment> EvidenceSegments { get; init; } = [];
    public List<EvidenceFact> ProposedFacts { get; init; } = [];
}

public sealed record ResumeImportOptions
{
    public string? WorkerPath { get; init; }
    public IReadOnlyList<string> WorkerArguments { get; init; } = [];
    public TimeSpan ParserTimeout { get; init; } = TimeSpan.FromSeconds(8);
    public long WorkerWorkingSetLimitBytes { get; init; } = 256L * 1024 * 1024;
}

public static class DocumentImportLimits
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    public const int MaximumExpandedBytes = 8 * 1024 * 1024;
    public const int MaximumEntries = 128;
    public const int MaximumPages = 100;
    public const int MaximumExtractedCharacters = 500_000;
}

public sealed class DocumentImportException(string code, Exception? innerException = null)
    : IOException(code, innerException)
{
    public string Code { get; } = code;
}

public sealed class ResumeImporter
{
    public const int MaximumBytes = DocumentImportLimits.MaximumBytes;
    private const int MaximumWorkerOutputBytes = 4 * 1024 * 1024;
    private const int MaximumWorkerErrorBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ResumeImportOptions options;

    public ResumeImporter() : this(new()) { }

    public ResumeImporter(ResumeImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ParserTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.WorkerWorkingSetLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        this.options = options;
    }

    public async Task<ResumeImportResult> ImportTextAsync(Stream input, string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("This method accepts UTF-8 plain text resumes only.");
        try { return await ImportAsync(input, fileName, cancellationToken); }
        catch (DocumentImportException e) when (e.Code is "FileSizeLimitExceeded" or "InvalidUtf8")
        { throw new InvalidDataException(e.Code, e); }
    }

    public async Task<ResumeImportResult> ImportAsync(Stream input, string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateLeafName(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".txt" or ".pdf" or ".docx"))
            throw new NotSupportedException("Supported resume formats are .txt, .pdf, and .docx.");

        var bytes = await ReadBoundedAsync(input, cancellationToken);
        var fileHash = Hash(bytes);
        IReadOnlyList<ResumeEvidenceSegment> segments;
        var status = ResumeImportStatus.ReadyForReview;

        if (extension == ".txt")
        {
            var text = DecodeText(bytes);
            segments = TextSegments(text);
        }
        else
        {
            ValidateMagic(extension, bytes);
            var response = await RunWorkerAsync(extension[1..], bytes, cancellationToken);
            status = response.Status switch
            {
                "ReadyForReview" => ResumeImportStatus.ReadyForReview,
                "NeedsOcr" => ResumeImportStatus.NeedsOcr,
                _ => throw new DocumentImportException("WorkerProtocolInvalid")
            };
            segments = response.Segments.Select(s => new ResumeEvidenceSegment(s.SourceSpan, Normalize(s.Text))).ToArray();
        }

        var normalizedText = status == ResumeImportStatus.NeedsOcr
            ? string.Empty
            : string.Join('\n', segments.Select(s => s.Text).Where(s => !string.IsNullOrWhiteSpace(s)));
        if (normalizedText.Length > DocumentImportLimits.MaximumExtractedCharacters)
            throw new DocumentImportException("ExtractedTextLimitExceeded");
        var facts = ExtractFacts(segments, fileHash);
        return new()
        {
            DocumentId = fileHash,
            FileHash = fileHash,
            TextHash = Hash(Encoding.UTF8.GetBytes(normalizedText)),
            Text = normalizedText,
            Status = status,
            EvidenceSegments = segments,
            ProposedFacts = facts
        };
    }

    private async Task<WorkerResponse> RunWorkerAsync(string format, byte[] bytes, CancellationToken cancellationToken)
    {
        var workerPath = options.WorkerPath ?? Path.Combine(AppContext.BaseDirectory, "JobAgent.DocumentWorker.dll");
        if (!File.Exists(workerPath)) throw new DocumentImportException("DocumentWorkerUnavailable");
        var info = new ProcessStartInfo
        {
            FileName = workerPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : workerPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (workerPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(workerPath);
        foreach (var argument in options.WorkerArguments) info.ArgumentList.Add(argument);
        info.ArgumentList.Add("--format"); info.ArgumentList.Add(format);
        info.ArgumentList.Add("--max-pages"); info.ArgumentList.Add(DocumentImportLimits.MaximumPages.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add("--max-chars"); info.ArgumentList.Add(DocumentImportLimits.MaximumExtractedCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add("--max-entries"); info.ArgumentList.Add(DocumentImportLimits.MaximumEntries.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add("--max-expanded-bytes"); info.ArgumentList.Add(DocumentImportLimits.MaximumExpandedBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(info) ?? throw new DocumentImportException("DocumentWorkerUnavailable");
        using var pipeCancellation = new CancellationTokenSource();
        var outputTask = ReadPipeBoundedAsync(process.StandardOutput.BaseStream, MaximumWorkerOutputBytes,
            pipeCancellation.Token);
        var errorTask = ReadPipeBoundedAsync(process.StandardError.BaseStream, MaximumWorkerErrorBytes,
            pipeCancellation.Token);
        var inputTask = WriteInputAsync(process, bytes);
        var clock = Stopwatch.StartNew();
        try
        {
            while (!process.HasExited)
            {
                if (outputTask.IsFaulted || errorTask.IsFaulted)
                {
                    await KillAsync(process);
                    if (outputTask.IsFaulted) await outputTask;
                    await errorTask;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    await KillAsync(process);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (clock.Elapsed >= options.ParserTimeout)
                {
                    await KillAsync(process);
                    throw new DocumentImportException("ParsingTimedOut");
                }
                try
                {
                    process.Refresh();
                    if (process.WorkingSet64 > options.WorkerWorkingSetLimitBytes)
                    {
                        await KillAsync(process);
                        throw new DocumentImportException("WorkerMemoryLimitExceeded");
                    }
                }
                catch (InvalidOperationException) when (process.HasExited) { }
                await Task.Delay(25, CancellationToken.None);
            }
            try { await inputTask; }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            { throw new DocumentImportException("DocumentWorkerFailed", e); }
            var output = StrictUtf8.GetString(await outputTask);
            _ = await errorTask;
            WorkerResponse? response;
            try { response = JsonSerializer.Deserialize<WorkerResponse>(output, WorkerJson.Options); }
            catch (JsonException e) { throw new DocumentImportException("WorkerProtocolInvalid", e); }
            if (response is null) throw new DocumentImportException("WorkerProtocolInvalid");
            if (process.ExitCode != 0 || response.Error is not null)
                throw new DocumentImportException(response.Error ?? "DocumentWorkerFailed");
            if (response.Segments.Count > DocumentImportLimits.MaximumPages + DocumentImportLimits.MaximumEntries)
                throw new DocumentImportException("WorkerProtocolInvalid");
            return response;
        }
        finally
        {
            if (!process.HasExited) await KillAsync(process);
            await pipeCancellation.CancelAsync();
            await ObserveAsync(inputTask, outputTask, errorTask);
        }
    }

    private static async Task<byte[]> ReadPipeBoundedAsync(Stream input, int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0) return output.ToArray();
            if (output.Length + read > maximumBytes)
                throw new DocumentImportException("WorkerOutputLimitExceeded");
            await output.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task WriteInputAsync(Process process, byte[] bytes)
    {
        try { await process.StandardInput.BaseStream.WriteAsync(bytes); }
        finally { process.StandardInput.Close(); }
    }

    private static async Task KillAsync(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        try { await process.WaitForExitAsync(); }
        catch (InvalidOperationException) { }
    }

    private static async Task ObserveAsync(params Task[] tasks)
    {
        foreach (var task in tasks)
        {
            try { await task; }
            catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException) { }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > MaximumBytes)
                throw new DocumentImportException("FileSizeLimitExceeded");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return buffer.ToArray();
    }

    private static string DecodeText(byte[] bytes)
    {
        try { return Normalize(StrictUtf8.GetString(bytes).TrimStart('\uFEFF')); }
        catch (DecoderFallbackException e) { throw new DocumentImportException("InvalidUtf8", e); }
    }

    private static IReadOnlyList<ResumeEvidenceSegment> TextSegments(string text) => text.Split('\n')
        .Select((line, index) => new ResumeEvidenceSegment($"line {index + 1}", line.Trim()))
        .Where(segment => segment.Text.Length > 0).ToArray();

    private static List<EvidenceFact> ExtractFacts(IEnumerable<ResumeEvidenceSegment> segments, string documentId)
    {
        var facts = new List<EvidenceFact>();
        var ordinal = 0;
        foreach (var segment in segments)
        {
            foreach (var rawLine in segment.Text.Split('\n'))
            {
                var line = rawLine.Trim();
                string? kind = null;
                if (line.StartsWith("Professional Experience:", StringComparison.OrdinalIgnoreCase))
                    kind = "professional-experience";
                else if (line.StartsWith("Personal Project:", StringComparison.OrdinalIgnoreCase))
                    kind = "personal-project";
                if (kind is null) continue;
                facts.Add(new()
                {
                    Id = $"{documentId}:fact-{++ordinal}",
                    Kind = kind,
                    Value = line[(line.IndexOf(':') + 1)..].Trim(),
                    SourceDocumentId = documentId,
                    SourceSpan = segment.SourceSpan,
                    VerificationStatus = VerificationStatus.Proposed
                });
            }
        }
        return facts;
    }

    private static void ValidateLeafName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(['/', '\\', '\0']) >= 0
            || fileName is "." or "..") throw new DocumentImportException("InvalidFileName");
    }

    private static void ValidateMagic(string extension, byte[] bytes)
    {
        var valid = extension == ".pdf" ? bytes.AsSpan().StartsWith("%PDF-"u8)
            : bytes.AsSpan().StartsWith("PK\u0003\u0004"u8) || bytes.AsSpan().StartsWith("PK\u0005\u0006"u8);
        if (!valid) throw new DocumentImportException("ContentTypeMismatch");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n').Replace('\0', '\uFFFD').Trim();
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record WorkerResponse(string Status, List<WorkerSegment> Segments, string? Error);
internal sealed record WorkerSegment(string SourceSpan, string Text);
internal static class WorkerJson
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
