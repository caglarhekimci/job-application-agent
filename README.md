# Job Application Agent

[Türkçe](README.tr.md) · **Early local prototype · synthetic browser submissions only**

A local application assistant that keeps answers tied to verified profile facts
and separates permission to share data from permission to submit an application.
The first working flow uses a fictional CV and a local career site. A real Chromium
browser fills the two-step form, uploads the approved file and verifies the server
receipt. No employer receives this demo.

## Try the local demo

Windows x64, PowerShell 7, Git and Node.js/npm are prerequisites. The exact SDK and
dependency versions are pinned. From this repository in PowerShell 7:

```powershell
./scripts/bootstrap.ps1
./scripts/doctor.ps1
./scripts/verify.ps1
./scripts/run-demo.ps1
```

Open the private session link printed by the launcher. Import the synthetic CV,
confirm the profile, review the job and answers, approve data sharing, then approve
the synthetic submission. Ctrl+C stops both local hosts. Setup downloads the .NET
SDK when absent, dependencies and Chromium; the workflow itself makes no paid or
model API calls. [Installation and reset instructions](docs/guides/INSTALL.md).

## What works now

- Versioned profile storage with current-user Windows DPAPI-protected payloads;
  proposed facts require confirmation, and edits do not inherit verification.
- Bounded TXT/PDF/DOCX import, typed job requirements, deterministic answer rules and
  scoped answer memory. Private salary minimums are excluded from answers.
- A Turkish React dashboard with separate sharing and final submission approvals.
- A separate personal workspace: upload a CV, review source-backed experience,
  paste/review job requirements, preview answers, export and delete local data.
- A synthetic-only managed browser, exact file/answer checks, one-shot submission
  request, durable attempt claim, and explicit uncertain outcomes without retry.
- Three read-only STDIO MCP tools by default, plus three opt-in synthetic workflow
  commands. A real browser UI approves the package; model tools cannot grant consent.
- Reviewed answer memory with application/company/global scope, language, expiry,
  revision checks and revocation; schema-checked model suggestions stay unapproved.
- A reproducible fixture evaluation covering 12 profiles, 240 question outcomes
  and 60 job outcomes. See the [dataset card](evals/EXPANDED_DATASET_CARD.md).
- CAPTCHA/MFA fixture detection stops the managed browser for manual attention.

This is **not a completed general job-application product**. General form adapters,
broader host services and authorized live adapters remain unfinished. LinkedIn automation is
blocked without platform authorization. The [capability matrix](docs/CAPABILITY_MATRIX.md)
and [verification ledger](docs/VERIFICATION.md) distinguish tested slices from gaps.
Fixture tests are not language-model accuracy or live-site success measurements.

## Design and evidence

The solution uses .NET 10, ASP.NET Core, EF Core/SQLite, React/TypeScript,
Playwright .NET and the official C# MCP SDK. Runtime data lives outside the checkout.
The application journal currently accepts only synthetic applications.

- [Architecture and data flow](docs/DATA_FLOW.md)
- [Threat model](docs/THREAT_MODEL.md) and [privacy](docs/PRIVACY.md)
- [MCP setup and host verification boundary](docs/guides/CODEX_SETUP.md)
- [Project state and next work](docs/PROJECT_STATE.md)
- [Contributor guide](CONTRIBUTING.md) and [security reporting status](SECURITY.md)
- [Codex for OSS readiness](docs/grant/READINESS.md)

The [first prerelease](https://github.com/caglarhekimci/job-application-agent/releases/tag/v0.1.0-alpha.1)
is public. The Codex for OSS application was submitted and its confirmation observed;
selection and any benefit remain unknown. There is no independent adoption or grant award yet. Real applications and paid
calls require separate user approval. No paid API is required for local workflows.
Original project code and synthetic fixtures use the [MIT license](LICENSE).
Dependency notices are tracked separately in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
