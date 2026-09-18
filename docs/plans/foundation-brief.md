# Candidate foundation — W02, W03 text slice, W04, W05

Read the corresponding sections 5–7 and packages W02–W05 in the master plan.
The user approved this architecture and explicitly requests implementation, tests,
and execution without another plan/permission round.

Own only Core/Profiles, Core/Answers, Core/Jobs, Core/Permissions/SourcePermission*,
Infrastructure/Storage, Infrastructure/Documents, Core.Tests and Infrastructure.Tests,
samples/synthetic-* and docs/evidence/foundation.md. Parent owns applications,
browser, web, solution file and shared status docs. No commits or subagents.
Use scripts/use-toolchain.ps1 for .NET10. Package versions already central.
Notify parent early when contracts compile. Do not modify Application files.

Create public record types with init properties and usable defaults in namespaces
JobAgent.Core.Profiles, JobAgent.Core.Answers, JobAgent.Core.Jobs:

- CandidateProfile: Guid Id, int Version, bool Synthetic, string FullName, Email,
  Locale; DateTimeOffset? VerifiedAt; List<EvidenceFact> Facts;
  List<ExperiencePeriod> Experience; SalaryPreference Salary;
  List<AnswerMemory> Answers. Add preferences as needed.
- Money: decimal Amount, string Currency, enum SalaryPeriod {Month,Year},
  enum TaxBasis {Net,Gross}. SalaryPreference: Money Target, Money PrivateMinimum.
- EvidenceFact: string Id, Kind, Value, SourceDocumentId, SourceSpan;
  enum VerificationStatus {Proposed,Verified,Rejected}, optional validity.
- Profile revisions are immutable history; proposals never make verified facts.
  FullName/Email/Salary require explicit local confirmation before usage.
- JobPosting: string Id, Employer, Title, Text, SourceUrl; bool Synthetic;
  list of typed requirements and source permission. Import never fetches URL.
- FormQuestion: string Key, Label, Language; int? MaxLength; add typed semantics
  needed for salary/experience. Unknown keys must abstain, no free-form model claims.
- AnswerResolution: enum AnswerStatus {Resolved,NeedsInput,RequiresReview,ManualOnly,Blocked};
  string? Value; string Reason; List<string> EvidenceIds.
- AnswerResolver.Resolve(FormQuestion, CandidateProfile, JobPosting, DateTimeOffset now)
  synchronous deterministic method. Recognize keys contact.name, contact.email,
  salary.expected.monthly.net.TRY, experience.professional.csharp.years;
  honor language, verification, privacy, expiry and company/application scope.
- JobEvaluator.Evaluate(JobPosting, CandidateProfile, DateTimeOffset now).
- SyntheticData.Profile() and SyntheticData.Job() static factories in Core
  create clearly marked synthetic fixtures: candidate@example.invalid; salary
  100000 TRY/month/net, private minimum 85000; source-backed verified professional
  C# 3 years ending in 2026 (fixed snapshot, no false live claim).

Storage: EF SQLite context/factory, persisted versioned profiles and pending patches,
optimistic version rejection, Windows DPAPI payload protection fail closed. Allow
explicitly synthetic plaintext protection for Linux tests only. Repository takes
path outside checkout. No arbitrary model file read. Export/delete capability
if feasible within this foundation, accurately mark partial work.

CV import: start TXT with 2MB cap, strict UTF8, no macros/active execution; return
reviewable proposed facts with source spans, never unverified work as professional.
If time permits add safe PDF/DOCX, otherwise clearly mark W03 partial, do not fake it.

TDD required: meaningful failing assertion before implementation, then green.
Cover plan's W02/W04/W05 named cases and text import failures; real SQLite restart
test and ciphertext privacy assertions on Windows. Keep initial test-only stubs
so RED failure is an assertion, not merely missing-type compilation.

Report real commands, expected RED and GREEN exit codes and counts under
docs/evidence/foundation.md with raw logs under ignored artifacts/foundation/.
Do not edit shared solution; tell parent projects to add. Report exact APIs,
all limitations and whether each package is complete or a tested partial slice.
