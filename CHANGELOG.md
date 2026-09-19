# Changelog

## v0.1.0-alpha.2 — 2026-09-20

- Persist application questions and resume the same draft after reviewed answers.
  Answer memory supports application, company, role-group and default scope.
- Support typed select, checkbox, radio and conditional fields on the synthetic
  career site, with separate sharing and submission approval.
- Expose twelve opt-in local MCP tools over the protected personal workspace.
  Proposals remain pending; personal execution has no live adapter.
- Add protected pre-upgrade snapshots, explicit offline recovery and metadata
  audits across writable stores. Reject unsupported future schemas.
- Add source-bound CV/letter comparison and local text export after user review.
  Original CV attachments remain unchanged; this is extractive, not AI rewriting.
- Add local proposal-operation limits and a Fixture/HostMediated choice. Paid API
  mode remains disabled; limits do not control the host's tokens or account quota.
- Fix launcher PATH selection and keep the packaged inspector read-only even when
  command flags are inherited. Add English-first, Turkish-second setup guidance.
- Recheck approval at outbound transmission, cap its lifetime by answer expiry,
  and preserve document-review confirmation through unchanged background refreshes.
- Record regression fixes and their actual failing/passing tests in the
  [verification ledger](docs/VERIFICATION.md).

No employer or LinkedIn automation is enabled. Local packaging and Codex for OSS
submission do not mean ChatGPT store approval or a program award.

## v0.1.0-alpha.1 — 2026-09-19 UTC

First public Windows prototype: local CV/job review, synthetic browser submission,
read-only local plugin and source installation. The exact release passed137 tests
and Windows CI. See [release evidence](docs/evidence/release-alpha1.md).
