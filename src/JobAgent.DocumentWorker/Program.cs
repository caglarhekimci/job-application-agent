using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

var result = await Worker.RunAsync(args, Console.OpenStandardInput());
await Console.Out.WriteAsync(JsonSerializer.Serialize(result.Response, Worker.JsonOptions));
return result.ExitCode;

internal static class Worker
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumInputBytes = 2 * 1024 * 1024;

    internal static async Task<WorkerRun> RunAsync(string[] args, Stream input)
    {
        try
        {
            var options = WorkerOptions.Parse(args);
            var bytes = await ReadBoundedAsync(input, MaximumInputBytes);
            var segments = options.Format switch
            {
                "pdf" => PdfParser.Parse(bytes, options),
                "docx" => DocxParser.Parse(bytes, options),
                _ => throw new WorkerFailure("UnsupportedFormat")
            };
            var status = options.Format == "pdf" && segments.All(s => string.IsNullOrWhiteSpace(s.Text))
                ? "NeedsOcr" : "ReadyForReview";
            if (status == "NeedsOcr") segments.Clear();
            return new(0, new(status, segments, null));
        }
        catch (WorkerFailure e) { return new(2, new("Failed", [], e.Code)); }
        catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException
                                     and not AccessViolationException)
        { return new(2, new("Failed", [], "CorruptDocument")); }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, int maximum)
    {
        using var output = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(chunk);
            if (read == 0) return output.ToArray();
            if (output.Length + read > maximum) throw new WorkerFailure("FileSizeLimitExceeded");
            await output.WriteAsync(chunk.AsMemory(0, read));
        }
    }
}

internal static class PdfParser
{
    internal static List<WorkerSegment> Parse(byte[] bytes, WorkerOptions options)
    {
        using var document = PdfDocument.Open(bytes);
        if (document.NumberOfPages <= 0) throw new WorkerFailure("CorruptDocument");
        if (document.NumberOfPages > options.MaximumPages) throw new WorkerFailure("PageLimitExceeded");
        var segments = new List<WorkerSegment>();
        var characters = 0;
        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var text = Normalize(ContentOrderTextExtractor.GetText(document.GetPage(pageNumber)));
            characters = checked(characters + text.Length);
            if (characters > options.MaximumCharacters) throw new WorkerFailure("ExtractedTextLimitExceeded");
            segments.Add(new($"page {pageNumber}", text));
        }
        return segments;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n').Replace('\0', '\uFFFD').Trim();
}

internal static class DocxParser
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypeNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string MainDocumentContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    internal static List<WorkerSegment> Parse(byte[] bytes, WorkerOptions options)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count > options.MaximumEntries) throw new WorkerFailure("ArchiveLimitExceeded");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            ValidateEntryName(entry.FullName);
            if (!names.Add(entry.FullName)) throw new WorkerFailure("ArchivePathRejected");
            if (entry.IsEncrypted) throw new WorkerFailure("EncryptedDocumentRejected");
            expanded = checked(expanded + entry.Length);
            if (expanded > options.MaximumExpandedBytes) throw new WorkerFailure("ArchiveLimitExceeded");
            if (entry.Length > 1_048_576 && (entry.CompressedLength == 0 || entry.Length / entry.CompressedLength > 100))
                throw new WorkerFailure("ArchiveLimitExceeded");
            if (IsActiveContent(entry.FullName)) throw new WorkerFailure("ActiveContentRejected");
        }

        ValidateContentTypes(ReadXml(Required(archive, "[Content_Types].xml"), options.MaximumExpandedBytes));
        foreach (var relationship in archive.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
            ValidateRelationships(ReadXml(relationship, options.MaximumExpandedBytes));
        return ReadParagraphs(ReadXml(Required(archive, "word/document.xml"), options.MaximumExpandedBytes),
            options.MaximumCharacters);
    }

    private static void ValidateEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains('\\') || name.Contains(':'))
            throw new WorkerFailure("ArchivePathRejected");
        var trimmed = name.EndsWith('/') ? name[..^1] : name;
        if (trimmed.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
            throw new WorkerFailure("ArchivePathRejected");
    }

    private static bool IsActiveContent(string name)
    {
        var value = name.ToLowerInvariant();
        return value.EndsWith(".bin", StringComparison.Ordinal)
               || value.StartsWith("word/activex/", StringComparison.Ordinal)
               || value.StartsWith("word/embeddings/", StringComparison.Ordinal)
               || value.StartsWith("customui/", StringComparison.Ordinal)
               || value.Contains("vbaproject", StringComparison.Ordinal);
    }

    private static ZipArchiveEntry Required(ZipArchive archive, string name) =>
        archive.Entries.SingleOrDefault(e => e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new WorkerFailure("CorruptDocument");

    private static XDocument ReadXml(ZipArchiveEntry entry, long maximumCharacters)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maximumCharacters,
            MaxCharactersFromEntities = 0,
            IgnoreProcessingInstructions = true,
            IgnoreComments = true
        };
        using var input = entry.Open();
        using var reader = XmlReader.Create(input, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static void ValidateContentTypes(XDocument document)
    {
        XNamespace content = ContentTypeNamespace;
        var overrides = document.Root?.Elements(content + "Override").ToArray()
            ?? throw new WorkerFailure("CorruptDocument");
        if (!overrides.Any(e => (string?)e.Attribute("PartName") == "/word/document.xml"
                                && (string?)e.Attribute("ContentType") == MainDocumentContentType))
            throw new WorkerFailure("CorruptDocument");
        if (document.Descendants().Attributes("ContentType").Any(a =>
                a.Value.Contains("macro", StringComparison.OrdinalIgnoreCase)
                || a.Value.Contains("vba", StringComparison.OrdinalIgnoreCase)
                || a.Value.Contains("activex", StringComparison.OrdinalIgnoreCase)
                || a.Value.Contains("oleObject", StringComparison.OrdinalIgnoreCase)))
            throw new WorkerFailure("ActiveContentRejected");
    }

    private static void ValidateRelationships(XDocument document)
    {
        XNamespace relationships = RelationshipNamespace;
        if (document.Descendants(relationships + "Relationship").Any(e =>
                string.Equals((string?)e.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)))
            throw new WorkerFailure("ActiveContentRejected");
    }

    private static List<WorkerSegment> ReadParagraphs(XDocument document, int maximumCharacters)
    {
        XNamespace word = WordNamespace;
        var result = new List<WorkerSegment>();
        var total = 0;
        var paragraphNumber = 0;
        foreach (var paragraph in document.Descendants(word + "p"))
        {
            paragraphNumber++;
            var builder = new StringBuilder();
            foreach (var element in paragraph.Descendants())
            {
                if (element.Name == word + "t") builder.Append(element.Value);
                else if (element.Name == word + "tab") builder.Append('\t');
                else if (element.Name == word + "br") builder.Append('\n');
            }
            var text = builder.ToString().Trim();
            total = checked(total + text.Length);
            if (total > maximumCharacters) throw new WorkerFailure("ExtractedTextLimitExceeded");
            if (text.Length > 0) result.Add(new($"paragraph {paragraphNumber}", text));
        }
        return result;
    }
}

internal sealed record WorkerOptions(string Format, int MaximumPages, int MaximumCharacters,
    int MaximumEntries, int MaximumExpandedBytes)
{
    internal static WorkerOptions Parse(string[] args) => new(
        Value(args, "--format"), Positive(args, "--max-pages"), Positive(args, "--max-chars"),
        Positive(args, "--max-entries"), Positive(args, "--max-expanded-bytes"));

    private static int Positive(string[] args, string name) => int.TryParse(Value(args, name), out var value) && value > 0
        ? value : throw new WorkerFailure("WorkerArgumentsInvalid");
    private static string Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new WorkerFailure("WorkerArgumentsInvalid");
    }
}

internal sealed record WorkerSegment(string SourceSpan, string Text);
internal sealed record WorkerResponse(string Status, List<WorkerSegment> Segments, string? Error);
internal sealed record WorkerRun(int ExitCode, WorkerResponse Response);
internal sealed class WorkerFailure(string code) : Exception(code) { internal string Code { get; } = code; }
