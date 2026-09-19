# Remaining deliverables before publication and application

**Audit date:** 2026-09-19
**Scope:** read-only comparison of the current working tree with W00-W18 in the accepted master plan. No build, test, package, push, release, live application, or private-data operation was performed for this audit. Other agents were still integrating and testing the workspace, so the final verification ledger is authoritative when it is updated.

## Honest release claim available after the gates below

The present work can support an initial **local-first, synthetic job-application prototype**. Its strongest demonstrated parts are deterministic profile/job/answer rules, approval-bound single submission, a constrained Playwright flow against the local synthetic career site, local encrypted/versioned storage, TXT/PDF/DOCX parsing with proposed evidence, an offline fixture evaluation, and a narrow STDIO MCP capability. LinkedIn and other live career sites remain disabled without verified permission.

The actual Codex-host evidence proves one read-only `runtime_get_capabilities` call. It does not prove a host-mediated application flow, general model answer quality, or all MCP operations. PDF/DOCX distribution should not be claimed until the worker is present and runnable in the packaged applications. No independent adoption, public release, program submission, acceptance, or benefit has been demonstrated.

## Release-blocking local work

Complete these items in order. They are smaller and higher value than adding more product features before the first publication.

1. **Integrate one reviewable commit.** The audit observed a large modified/untracked working tree on `work/local-synthetic`; the latest visible commits predate most current features. Resolve concurrent edits, review the final diff, and make the public source state reproducible. Do not attach verification from an earlier tree to the release commit.
2. **Synchronize public documentation with measured behavior.** README, capability/state/verification documents, changelog, MCP/setup evidence, and grant readiness contain stale statements such as TXT-only import, unverified host execution, old test totals, no repository authorization, and submission not authorized. Update them from the final evidence once. Keep the distinction between one host capability smoke and a complete W09 flow. Do not publish a test total until the final run produces it.
3. **Run release verification on the exact commit.** Run the repository verification command, the document suite, source/history privacy scan, package creation, manifest review, and packaged CLI/MCP launch. Add a packaged PDF and DOCX smoke so the worker boundary is exercised from distribution output. Then test README installation from a clean checkout/profile and let public CI run on the pushed commit. Record commands, dates, exit codes, commit SHA, and report paths in `docs/VERIFICATION.md`.
4. **Complete basic repository health files.** `CONTRIBUTING.md` and `SECURITY.md` exist, but no code of conduct, issue templates, or pull-request template were observed. Add the small community files required by W14. Configure a usable private security-reporting path after publication; do not invent an email address.
5. **Publish from the verified state.** Publication and OSS submission are authorized, but a successful public push, public URL, default branch, tag, release, and release artifact were not verified in this audit. Verify the public URL without authentication, then tag/release only the exact tested commit with scope-accurate notes.

### Package audit details

`scripts/package.ps1` publishes CLI and MCP outputs, copies top-level license/readme/docs files, copies React-family license files, rejects several sensitive filename patterns, and writes a hash manifest. During this audit it was updated to copy the vendored full PdfPig license and to reject publish outputs missing `JobAgent.DocumentWorker.dll`, its deps/runtime configuration, or `UglyToad.PdfPig.dll`. The normal Infrastructure project reference was also observed to copy the complete worker runtime into the Web output. These fixes are implemented; the currently running full verification and a packaged document-import exercise still need to establish release evidence. Filename scanning also does not replace the source/history privacy scan or a human review of generated documentation and archives.

Recommended package acceptance checks:

- required PdfPig license and inherited notices are present and hashed;
- required worker files and `UglyToad.PdfPig.dll` are present in both runnable outputs that expose import;
- packaged CLI and MCP start without a checkout-relative dependency;
- a synthetic PDF and DOCX import completes through the packaged parent/worker boundary;
- no database, browser state, authentication material, private artifacts, PDB, or personal document appears in the archives;
- `SECURITY.md`, support/install guidance, and exact release scope remain reachable from the source release or package.

## W00-W18 disposition

| Work package | Current disposition for first public release | Remaining evidence or work |
| --- | --- | --- |
| W00 environment/sources | Locally evidenced; public facts need refresh | Reconcile the final remote/public URL and current account/tool observations without exposing credentials. |
| W01 executable skeleton | Suitable for synthetic release | Re-run health and synthetic-site checks on the release commit and in CI. |
| W02 profile/storage | Substantial local implementation | Cite final migration, restart, encryption, conflict, and deletion results. Clean-profile verification remains part of W12. |
| W03 document import | Parser and runtime-output integration implemented; final verification pending | Final workspace review flow, encrypted original persistence, package execution, and integrated tests must pass. OCR remains intentionally absent and returns `NeedsOcr`. |
| W04 job import/evaluation | Substantial deterministic local implementation | Keep manual text import and permission boundaries explicit; richer extraction is later work. |
| W05 answer memory | Deterministic core is useful but partial | The schema-validated model-proposal layer and a user flow for selecting new-answer scope remain feasible local follow-ups. Do not block the synthetic release if the limitation is explicit. |
| W06 approvals/idempotency | Strong synthetic evidence | Carry the final approval, concurrency, crash-recovery, and uncertain-result tests into the exact-commit report. |
| W07 managed browser | Synthetic path and network boundary are substantial but partial | General multi-step/conditional controls and explicit CAPTCHA/MFA manual takeover remain. Release only the tested local fixture scope. |
| W08 local UI/vertical slice | Synthetic application flow exists; workspace integration is being finalized | Final UI/workspace E2E, accessibility/manual observations, and status display evidence must be recorded. Scoped memory for a newly encountered question remains later work. |
| W09 MCP/model host | STDIO contract plus one real read-only host call | A host-mediated approved synthetic application and broader common-service tool surface remain incomplete. Do not describe the single capability call as full model integration. |
| W10 evaluation | Reproducible synthetic fixture evaluation | Broader cases, independent splits, and model comparison remain. Real-model quality is `NotRun`; no further paid/API run is required for the first release. |
| W11 security/privacy | Broad adversarial coverage; final ledger required | Add or cite explicit log-redaction and consent-revocation regressions, packaged-export checks, and any unresolved severity/mitigation. Avoid claims outside the loopback fixture threat boundary. |
| W12 install/package/CI | Release blocker | PdfPig notices and required-worker checks are implemented. Verify the clean install/package, packaged imports, and CI results for the public commit. |
| W13 personal/live use | External/private gate | A real CV and live job must remain outside the repository. LinkedIn automation stays `BlockedExternal`; another live adapter needs documented authorization and user-approved submission. |
| W14 public repo/release | Authorized, not yet verified complete | Public push/URL, community templates/reporting path, tag, release, and artifact checks remain. |
| W15 adoption/maintenance | External time/community gate | Independent users are 0 and published releases are 0 at the recorded snapshot. Invite and measure real pilots only after publication; never manufacture activity. |
| W16 OSS application | Draft prepared; submission authorized but externally gated | Recheck the live form/terms, insert the real public URL, privately obtain the ChatGPT email and any required organization ID, verify character counts, submit, and retain the confirmation privately. Selection remains OpenAI's decision. |
| W17 store package | Optional | Local plugin material may be kept as experimental; store publication is not a Codex for OSS prerequisite. |
| W18 desktop/fine-tuning | Optional and unjustified at present | No release work is needed unless a measured problem later passes the plan's justification, permission, data, and cost gates. |

## External gates that code cannot close

- LinkedIn or another career site's automation/data-use authorization, plus any CAPTCHA/MFA/user takeover in a live flow.
- The user's private CV/profile review and any real-employer approval/receipt. These records must stay outside the public repository.
- A verified public repository URL and GitHub-side settings after the authorized push, including private vulnerability reporting if selected.
- Real independent users, feedback, maintenance history, downloads, stars, issues, and downstream reuse. The current honest count remains zero until observed.
- The private ChatGPT account email and, only if the current form requires it, the OpenAI organization ID.
- OpenAI's review, selection, activation, duration, or benefits. Submission cannot establish any of these facts.

## Publication decision

The first public push does not need W05, W07, W09, or W10 expanded into their full long-term designs. PdfPig notices and worker-file package checks are now implemented. Publication still needs a coherent commit, accurate scope, a successful package/document smoke, final exact-commit tests, privacy review, clean-install evidence, and passing public CI. After those gates pass, publish the synthetic/local prototype and keep live adapters, adoption, and program outcome visibly open.
