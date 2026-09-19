# Candidate foundation evidence — W02, W03 text slice, W04, W05

Date: 2026-09-18
Environment: Windows, .NET SDK 10.0.401 via `scripts/use-toolchain.ps1`
Data: synthetic fixtures only; no personal document, credential, account, paid call, or live platform request was used.

## Implemented contracts

- `JobAgent.Core.Profiles`: versioned `CandidateProfile`, evidence, professional/personal experience separation, salary units and privacy fields, scoped answer memory, consent records, profile patches, local field confirmations, and verified/current fact filtering.
- `JobAgent.Core.Answers`: deterministic `AnswerResolver.Resolve(FormQuestion, CandidateProfile, JobPosting, DateTimeOffset)` for locally confirmed name/email/salary and verified professional C# experience; scoped answer memory; safe abstention and manual-only outcomes.
- `JobAgent.Core.Jobs`: typed postings and requirements, permission-bound text import, duplicate key, and evidence-based evaluation with mandatory/preferred distinction.
- `JobAgent.Core.Permissions`: allowed action set with expiry and default-blocked LinkedIn permission.
- `JobAgent.Core.SyntheticData`: fixed, marked synthetic profile and job. The fixture email is `candidate@example.invalid`; salary target is 100000 TRY/month/net and private minimum is 85000; C# professional evidence covers 2023-01-01 through 2026-01-01.
- `JobAgent.Infrastructure.Storage`: EF Core SQLite context/factory, immutable profile revision rows, protected pending patches, stale version rejection, export/delete, checkout-path rejection, Windows DPAPI current-user protection, and explicit synthetic-only plaintext support on non-Windows tests.
- `JobAgent.Infrastructure.Documents`: stream-only TXT import with a 2 MiB cap, strict UTF-8, SHA-256 document identity, source line spans, and proposed facts. It does not read arbitrary paths or execute active content.

## RED evidence

Raw logs are ignored under `artifacts/foundation/`.

| Command | Exit | Expected evidence |
|---|---:|---|
| `dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore --nologo` | 1 | Contract type absent: 1 failed assertion (`core-contract-red.log`). |
| same Core project after behavior test stubs | 1 | 9 failed, 9 passed; salary, experience, scope, job evaluation and import assertions failed (`core-behavior-red.log`). |
| Core filter `CurrentVerifiedFact_IsUsable` | 1 | 1 failed assertion; verified fact filter returned empty (`profile-policy-red.log`). |
| Core filter `OverlappingProfessionalPeriods_DoNotDoubleCountCalendarTime` | 1 | 1 failed assertion; overlapping periods incorrectly yielded Eligible (`job-overlap-red.log`). |
| `dotnet test tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj --no-restore --nologo` | 1 | Contract type absent: 1 failed assertion (`infrastructure-contract-red.log`). |
| same Infrastructure project after behavior skeleton | 1 | 12 failed, 2 passed; persistence, encryption and TXT import assertions failed (`infrastructure-behavior-red.log`). |
| Infrastructure filter `EditedFactWithSameId_DoesNotInheritVerification` | 1 | 1 failed assertion; changed value incorrectly remained Verified (`review-boundaries-infra-red.log`). |

The review-boundary Core run initially produced a compile failure because the newly specified `ConfirmLocally` contract did not exist (`review-boundaries-core-red.log`). After adding the contract, the full Core run still failed one behavioral assertion: a future end date was counted past `now`. The corrected run is recorded below.

## GREEN evidence

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore --nologo` | 0 | 24 passed, 0 failed (`review-boundaries-core-green.log`). |
| `dotnet test tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj --no-restore --nologo` | 0 | 15 passed, 0 failed (`review-boundaries-infra-green.log`). Includes a real SQLite close/reopen, stale patch rejection, DPAPI round-trip, and ciphertext privacy assertion on Windows. |
| `dotnet build src/JobAgent.Infrastructure/JobAgent.Infrastructure.csproj --no-restore --nologo` | 0 | 0 warnings, 0 errors after platform guards were made analyzer-visible. |
| Final sequential rerun of both owned test projects | 0 | Core 24/24 and Infrastructure 15/15 (`final-core.log`, `final-infrastructure.log`). |
| `dotnet build JobAgent.slnx --no-restore --nologo` | 0 | Shared solution build completed with 0 warnings and 0 errors (`final-solution-build.log`). |

## Package status and limitations

- **W02: tested foundation slice, partial package.** Versioned encrypted persistence, restart, optimistic rejection, pending patch protection, exact-fact verification preservation, export/delete and field confirmation exist. `EnsureCreated` establishes the initial SQLite schema; a formal upgrade migration chain is not yet present. Trusted UI approval provenance/audit is completed by the separate application approval package, so this repository layer is not claimed as that UI boundary.
- **W03: tested partial package.** TXT import is implemented. PDF, DOCX, OCR, archive expansion controls, parser timeout, original-file storage and a general real-document review UI are not implemented. DOCM and all non-TXT inputs fail closed.
- **W04: tested core slice, partial package.** Manual provided text is stored without fetching its URL; LinkedIn actions block without permission; typed requirements can be evaluated. Automatic requirement extraction, canonical URL normalization and durable permission evidence storage are not implemented.
- **W05: tested deterministic slice, partial package.** Known fields and scoped memory resolve without model claims; unknown fields abstain. A model proposal/validation layer and the UI for selecting new answer scope are not implemented.

No project was added to the shared solution file in this slice. Add `tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj` and `tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj` to the solution-owned test folder.
