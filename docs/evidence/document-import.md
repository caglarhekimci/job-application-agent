# W03 document import evidence

**Verified:** 2026-09-19
**Scope:** deterministic, stream-only TXT/PDF/DOCX import with synthetic inputs. No personal document was opened.

## Dependency decision

PDF extraction uses **PdfPig 0.1.16**, the latest stable release visible on the official NuGet package page on the verification date. The newer 0.1.17 builds are prereleases and were not selected. NuGet identifies 0.1.16 as Apache-2.0, supports .NET 6 and later, and lists no dependencies for its net6/net8/net9 assets. The upstream repository also publishes an Apache 2.0 license; its license file contains additional notices for inherited PDFBox/font/CMap components, which must remain available when distribution terms require them.

- Official package and version: https://www.nuget.org/packages/PdfPig/0.1.16
- Official source and usage documentation: https://github.com/UglyToad/PdfPig
- Upstream license and component notices: https://github.com/UglyToad/PdfPig/blob/master/LICENSE

The worker calls `ContentOrderTextExtractor` rather than relying on PDF internal content order. It does not request annotations, hyperlinks, images, forms, or embedded files. PdfPig is kept outside the main process because PDF parsing is CPU work over untrusted bytes.

DOCX extraction has no added package. It uses .NET `ZipArchive` metadata and bounded entry streams, plus `XmlReader` with DTD processing prohibited, a null resolver, and a character limit. Microsoft documents `ZipArchiveEntry.Length` as the uncompressed entry size, recommends XML size limits for untrusted documents, and documents `Process.Kill(true)` as terminating the selected process and descendants.

- ZIP entry length: https://learn.microsoft.com/dotnet/api/system.io.compression.ziparchiveentry.length?view=net-10.0
- XML reader security settings: https://learn.microsoft.com/dotnet/api/system.xml.xmlreadersettings?view=net-10.0
- DTD prohibition: https://learn.microsoft.com/dotnet/api/system.xml.xmlreadersettings.dtdprocessing?view=net-10.0
- Process-tree termination: https://learn.microsoft.com/dotnet/api/system.diagnostics.process.kill?view=net-10.0

## Implemented boundary

`ResumeImporter.ImportAsync(Stream, fileName, cancellationToken)` is the only general import entry point. It never accepts a source path and never writes the original. It reads at most 2 MiB, computes a lowercase SHA-256 of the original bytes, checks a leaf-only filename, allowlisted extension, and PDF/ZIP magic, then handles strict UTF-8 text locally or sends the bounded bytes to `JobAgent.DocumentWorker` over redirected standard input.

The result contains:

- `DocumentId` and `FileHash`: SHA-256 of the exact input bytes;
- `TextHash`: SHA-256 of normalized extracted UTF-8 text;
- `Text` and `EvidenceSegments`, with `line N`, `page N`, or `paragraph N` spans;
- `ReadyForReview` or `NeedsOcr` status;
- `ProposedFacts`, always carrying `VerificationStatus.Proposed`.

The worker returns one bounded JSON envelope over standard output. The parent consumes stdout and stderr with limits while the process is running; it does not call an unbounded `ReadToEndAsync`. Cancellation and the eight-second default timeout terminate the process tree and await process exit. A 256 MiB working-set check is defense in depth: polling can react after an allocation and is **not** an operating-system-enforced memory quota.

This process boundary is **not an OS sandbox**. It gives the parent a killable CPU boundary and prevents parser state from living in the application process. It does not provide kernel-enforced filesystem, network, syscall, or hard memory isolation.

## Enforced limits and rejection rules

| Boundary | Enforced behavior |
| --- | --- |
| Input | 2 MiB maximum; works with non-seekable streams; `.txt`, `.pdf`, and `.docx` only |
| PDF | 100 pages, 500,000 extracted characters, killable timeout/cancellation; blank image-only pages produce `NeedsOcr` with no invented facts |
| DOCX archive | 128 entries, 8 MiB declared total expansion, high-ratio large entry rejection, duplicate and traversal-like path rejection, encrypted entry rejection |
| DOCX active content | Rejects VBA/`.bin`, ActiveX, OLE/embeddings, custom UI, macro/active content types, and every relationship marked `TargetMode="External"` |
| XML | DTD prohibited, resolver disabled, processing instructions ignored, character count bounded |
| Worker protocol | 4 MiB stdout and 64 KiB stderr maximum, fixed error codes, malformed responses rejected |

Only `word/document.xml` text is read. Relationships are inspected only to reject external targets; they are never resolved. Macro code, embedded objects, external templates, hyperlinks, and other package parts are not executed or followed.

## TDD evidence

Initial RED after adding the new tests failed because `JobAgent.DocumentWorker`, `ImportAsync`, `NeedsOcr`, limits, and the public result types did not exist. After implementation, the first focused suite passed **12/12**.

A separate output-bound RED used a synthetic worker that emitted more than the protocol limit and then waited. Before the bounded drain, the observed result was `ParsingTimedOut` after ten seconds instead of `WorkerOutputLimitExceeded`. After the fix, the focused regression passed in 254 ms. A corrupt PDF with valid `%PDF-` magic then reproduced `WorkerProtocolInvalid`; the worker now converts every nonfatal parser exception to the fixed `CorruptDocument` result without printing document content. Its focused regression passed in 362 ms, and the final document suite passed **15/15** in approximately three seconds.

Commands used:

```powershell
. .\scripts\use-toolchain.ps1
dotnet test tests/JobAgent.Document.Tests/JobAgent.Document.Tests.csproj --no-restore --verbosity minimal
```

The tests create all TXT, PDF, DOCX, ZIP, XML, and hanging-worker inputs in memory or under the test temporary directory. They cover non-seekable streams, proposed facts and source spans, extension/magic mismatch, PDF text, corrupt PDF error normalization, scan-only PDF, DOCX paragraphs, external relationships, macros, archive expansion, timeout, cancellation, process termination, and bounded worker output.

## Build and runtime integration

The repository centrally pins `PdfPig` 0.1.16 and `src/JobAgent.DocumentWorker/packages.lock.json` resolves exactly 0.1.16. `JobAgent.DocumentWorker` and `JobAgent.Document.Tests` are in the solution, and Infrastructure has a project reference to the worker. The normal reference was retained after the integration build verified that it copies the complete worker runtime into the Web output. Packaging now treats those files as required rather than relying silently on that behavior.

At runtime the default importer looks for `JobAgent.DocumentWorker.dll` in `AppContext.BaseDirectory` and launches it with the inherited `dotnet` host. Each runnable application output that imports PDF/DOCX must therefore receive these worker build outputs in the same directory:

- `JobAgent.DocumentWorker.dll`
- `JobAgent.DocumentWorker.deps.json`
- `JobAgent.DocumentWorker.runtimeconfig.json`
- `UglyToad.PdfPig.dll`

The Windows apphost executable and PDB are optional for the default DLL launch. An application may instead set `ResumeImportOptions.WorkerPath` once from trusted startup configuration to the worker DLL or apphost; this setting must not be accepted from an HTTP, MCP, document, or UI payload. The test project uses an explicit worker build path and has a build-only project reference.

The repository vendors the complete upstream PdfPig license and inherited component notices as `third-party-notices/PdfPig-0.1.16-LICENSE` (SHA-256 `4C510E162F896EA43B4B1CF1B743641D7C756F1004D0E5EAF0615B28B7DED409`). `scripts/package.ps1` copies that file and rejects a CLI or MCP publish missing the worker DLL, deps/runtime configuration, or `UglyToad.PdfPig.dll`. A successful final package execution and packaged import smoke remain part of the release verification ledger rather than this focused parser test.

## W03 work still unsatisfied

- The profile review UI and the user's accept/correct flow are outside this slice and remain required before proposed facts can become verified facts.
- Encrypted persistence of the unchanged original document is outside the importer. The caller must store the bounded original bytes in the private profile workspace; no original is stored in this implementation or the repository.
- OCR is deliberately absent and disabled. `NeedsOcr` asks for another representation or future explicit OCR work.
- The monitored working-set ceiling is not a hard memory quota, and the worker is not an OS sandbox.
- Real-world corpus, fuzz, and parser-vulnerability testing remain future hardening. Current evidence uses synthetic documents only.
