using System.Diagnostics;
using System.Text;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Documents;

namespace JobAgent.Document.Tests;

public sealed class DocumentImportTests
{
    [Fact]
    public async Task NonSeekableTextStream_ProducesOnlyProposedFactsWithLineSpan()
    {
        await using var input = new NonSeekableStream(Encoding.UTF8.GetBytes(
            "Synthetic Resume\nProfessional Experience: C# - 3 years"));

        var result = await new ResumeImporter().ImportAsync(input, "resume.txt");

        var fact = Assert.Single(result.ProposedFacts);
        Assert.Equal(ResumeImportStatus.ReadyForReview, result.Status);
        Assert.Equal(VerificationStatus.Proposed, fact.VerificationStatus);
        Assert.Equal("professional-experience", fact.Kind);
        Assert.Equal("line 2", fact.SourceSpan);
    }

    [Theory]
    [InlineData("resume.pdf", "not a PDF")]
    [InlineData("resume.docx", "not a ZIP")]
    [InlineData("../resume.txt", "text")]
    [InlineData("resume.docm", "PK")]
    public async Task ExtensionPathOrMagicMismatch_IsRejected(string fileName, string content)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(content));

        await Assert.ThrowsAnyAsync<Exception>(() => new ResumeImporter().ImportAsync(input, fileName));
    }

    [Fact]
    public async Task TextPdf_ProducesProposedFactWithPageSpan()
    {
        await using var input = new NonSeekableStream(SyntheticDocuments.TextPdf("Professional Experience: C# - 4 years"));

        var result = await Importer().ImportAsync(input, "resume.pdf");

        var fact = Assert.Single(result.ProposedFacts);
        Assert.Equal(ResumeImportStatus.ReadyForReview, result.Status);
        Assert.Equal(VerificationStatus.Proposed, fact.VerificationStatus);
        Assert.Equal("page 1", fact.SourceSpan);
    }

    [Fact]
    public async Task ScanOnlyPdf_ReturnsNeedsOcrWithoutFacts()
    {
        await using var input = new MemoryStream(SyntheticDocuments.BlankPdf());

        var result = await Importer().ImportAsync(input, "resume.pdf");

        Assert.Equal(ResumeImportStatus.NeedsOcr, result.Status);
        Assert.Empty(result.Text);
        Assert.Empty(result.ProposedFacts);
    }

    [Fact]
    public async Task CorruptPdfAfterValidMagic_ReturnsStableSafeError()
    {
        await using var input = new MemoryStream("%PDF-1.7\nmalformed"u8.ToArray());

        var error = await Assert.ThrowsAsync<DocumentImportException>(() => Importer().ImportAsync(input, "resume.pdf"));

        Assert.Equal("CorruptDocument", error.Code);
    }

    [Fact]
    public async Task Docx_ProducesProposedFactWithParagraphSpan()
    {
        var bytes = SyntheticDocuments.Docx(["Synthetic Resume", "Personal Project: Java game"]);
        await using var input = new NonSeekableStream(bytes);

        var result = await Importer().ImportAsync(input, "resume.docx");

        var fact = Assert.Single(result.ProposedFacts);
        Assert.Equal("personal-project", fact.Kind);
        Assert.Equal("paragraph 2", fact.SourceSpan);
        Assert.Equal(VerificationStatus.Proposed, fact.VerificationStatus);
    }

    [Fact]
    public async Task DocxWithExternalRelationship_IsRejected()
    {
        var relationship = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.invalid" TargetMode="External"/>
            </Relationships>
            """;
        await using var input = new MemoryStream(SyntheticDocuments.Docx(["Resume"], relationship));

        var error = await Assert.ThrowsAsync<DocumentImportException>(() => Importer().ImportAsync(input, "resume.docx"));

        Assert.Equal("ActiveContentRejected", error.Code);
    }

    [Fact]
    public async Task DocxWithMacroOrEmbeddedObject_IsRejected()
    {
        await using var input = new MemoryStream(SyntheticDocuments.Docx(["Resume"], null,
            ("word/vbaProject.bin", [1, 2, 3])));

        var error = await Assert.ThrowsAsync<DocumentImportException>(() => Importer().ImportAsync(input, "resume.docx"));

        Assert.Equal("ActiveContentRejected", error.Code);
    }

    [Fact]
    public async Task DocxExpansionBeyondLimit_IsRejected()
    {
        var expanded = new byte[DocumentImportLimits.MaximumExpandedBytes + 1];
        await using var input = new MemoryStream(SyntheticDocuments.Docx(["Resume"], null, ("word/styles.xml", expanded)));

        var error = await Assert.ThrowsAsync<DocumentImportException>(() => Importer().ImportAsync(input, "resume.docx"));

        Assert.Equal("ArchiveLimitExceeded", error.Code);
    }

    [Fact]
    public async Task ParserTimeout_KillsWorkerProcess()
    {
        var marker = Path.Combine(Path.GetTempPath(), $"jobagent-worker-{Guid.NewGuid():N}.pid");
        try
        {
            var worker = OutputPath("tests", "JobAgent.Document.Tests", "Fixtures", "HangingWorker", "bin",
                Configuration(), "net10.0", "JobAgent.HangingDocumentWorker.dll");
            var importer = new ResumeImporter(new ResumeImportOptions
            {
                WorkerPath = worker,
                WorkerArguments = ["--pid-file", marker],
                ParserTimeout = TimeSpan.FromMilliseconds(500)
            });
            await using var input = new MemoryStream(SyntheticDocuments.BlankPdf());

            var error = await Assert.ThrowsAsync<DocumentImportException>(() => importer.ImportAsync(input, "resume.pdf"));

            Assert.Equal("ParsingTimedOut", error.Code);
            var pid = int.Parse(await File.ReadAllTextAsync(marker), System.Globalization.CultureInfo.InvariantCulture);
            Assert.False(IsRunning(pid));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact]
    public async Task Cancellation_KillsWorkerProcessAndRemainsCancellation()
    {
        var marker = Path.Combine(Path.GetTempPath(), $"jobagent-worker-{Guid.NewGuid():N}.pid");
        try
        {
            var importer = HangingImporter(marker, TimeSpan.FromSeconds(10));
            await using var input = new MemoryStream(SyntheticDocuments.BlankPdf());
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                importer.ImportAsync(input, "resume.pdf", cancellation.Token));

            var pid = int.Parse(await File.ReadAllTextAsync(marker), System.Globalization.CultureInfo.InvariantCulture);
            Assert.False(IsRunning(pid));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact]
    public async Task WorkerOutputBeyondLimit_KillsWorkerBeforeTimeout()
    {
        var marker = Path.Combine(Path.GetTempPath(), $"jobagent-worker-{Guid.NewGuid():N}.pid");
        try
        {
            var importer = HangingImporter(marker, TimeSpan.FromSeconds(10), "--stdout-bytes", "4300000");
            await using var input = new MemoryStream(SyntheticDocuments.BlankPdf());
            var clock = Stopwatch.StartNew();

            var error = await Assert.ThrowsAsync<DocumentImportException>(() => importer.ImportAsync(input, "resume.pdf"));

            Assert.Equal("WorkerOutputLimitExceeded", error.Code);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5));
            var pid = int.Parse(await File.ReadAllTextAsync(marker), System.Globalization.CultureInfo.InvariantCulture);
            Assert.False(IsRunning(pid));
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    private static ResumeImporter Importer() => new(new ResumeImportOptions
    {
        WorkerPath = OutputPath("src", "JobAgent.DocumentWorker", "bin", Configuration(), "net10.0",
            "JobAgent.DocumentWorker.dll")
    });

    private static ResumeImporter HangingImporter(string marker, TimeSpan timeout, params string[] arguments) =>
        new(new ResumeImportOptions
        {
            WorkerPath = OutputPath("tests", "JobAgent.Document.Tests", "Fixtures", "HangingWorker", "bin",
                Configuration(), "net10.0", "JobAgent.HangingDocumentWorker.dll"),
            WorkerArguments = ["--pid-file", marker, .. arguments],
            ParserTimeout = timeout
        });

    private static string Configuration() => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    private static bool IsRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static string OutputPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobAgent.slnx")))
            directory = directory.Parent;
        return Path.Combine([directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), .. parts]);
    }
}
