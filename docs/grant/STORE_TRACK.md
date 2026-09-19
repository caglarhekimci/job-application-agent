# ChatGPT and Codex plugin distribution track

**Status date:** 2026-09-19.
**User intent:** Public plugin publication is explicitly requested, alongside the
public repository and Codex for Open Source application. Paid services remain
unauthorized. These are separate outcomes; publication does not award Pro access.

## Implemented local package

`plugins/job-application-agent/` contains a portable Agent Plugins 1.0.0 manifest,
a supported Codex compatibility manifest, matching local STDIO declarations,
contained launcher, package builder, privacy disclosure, and reviewer scenarios.
The package identifies itself as **Job Application Agent — Local Inspector**.
It exposes the existing three read-only tools; it does not add approval, browser,
submission or live-platform tools. No skills-only substitute for the application
has been presented as completion of the requested product.

The package builder produces a ZIP with runtime binaries from reviewed MCP output,
dependency notices, file hashes and a ZIP checksum. Offline manifest validators
passed. A relocated ZIP passed a real STDIO handshake, exact three-tool listing,
capability call, invalid-path rejection and empty-runtime check. Missing runtime
and forbidden private-file input fail closed. See
[package evidence](../evidence/plugin-package.md).

No marketplace entry was installed into the user's configuration. The plugin has
not been installed through the host or submitted to the public directory. The
separate [Codex host evidence](../evidence/codex-host.md) proves a direct capability
tool call, not plugin installation or all reviewer prompts.

## Current public submission rules

The official flow accepts skills-only, remote MCP-only, or combined submissions.
MCP submission currently uses a stable public HTTPS endpoint; local MCP publishers
who cannot provide one are directed to their OpenAI contact for local support.
Local packaging and local marketplaces are supported separately from public
directory publication. [Packaging documentation](https://developers.openai.com/plugins/build/plugins),
[submission flow](https://developers.openai.com/plugins/deploy/submission).

The portal also requires Apps Management write access, a verified publisher
identity, public support/privacy/terms material, and five positive plus three
negative review cases. Submission, approval and publication are distinct steps.
These requirements were checked on 2026-09-19; actual account eligibility has not
been inspected. [Submission requirements](https://developers.openai.com/plugins/deploy/submission).

The published-plugin quality policy excludes trial/demo products. The initial
fixture inspector is therefore not labeled ready for public review.
[Plugin guidelines](https://developers.openai.com/plugins/app-guidelines).

## Remaining gates and actual blockers

| Gate | State | Concrete next requirement |
|---|---|---|
| Local distribution package | VerifiedLocal | Built/tested from publish output `20260919T210019Z`; rebuild if release inputs change |
| Plugin host installation | NotRun | Install/test the prepared package when the parent task schedules that step |
| Public source and release | Repository created; release pending | Publish reviewed source and supported release; see project ledger |
| Public MCP access route | BlockedExternal | Approved production HTTPS architecture or OpenAI local-MCP support; no tunnel or hosted service was enabled |
| Publisher identity and portal permission | Unknown | User-controlled Platform identity verification and Apps Management access |
| Public listing material | Incomplete | Publish matching support, privacy and terms URLs plus production branding |
| Reviewer scenarios | Specified, not all run | Execute the eight actual host workflows using synthetic fixtures |
| Product completeness | InProgress | Complete the intended useful application and required security/installation gates |
| Policy attestations | Not completed | Review the actual final package and current portal statements |
| Review submission | Not submitted | Satisfy the above gates and submit the actual package |
| Review decision / publication | Unknown / not published | External review, then separate publication action after acceptance |

The current account's Pro subscription is not treated as authorization to buy
hosting, API credits, extra usage or another service. Local tests and ZIP packaging
make no model calls. Codex host/model tests use only the user's expressly authorized
existing plan allowance. No award, store acceptance or independent adoption is
claimed. Codex for OSS status remains in [readiness](READINESS.md).
