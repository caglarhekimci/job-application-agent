# Common protected application review and personal UI

Date: 2026-09-20 Istanbul. This evidence covers local synthetic test data only.

The personal UI and opt-in local MCP tools use the same `LocalWorkspace` protected
payload. Applications now persist question snapshots before reporting `NeedsInput`.
The question engine is shared with the synthetic browser adapter. It grants no
permission and does not navigate any website.

Reviewed answers use the real application GUID, company, explicit stable role
group, or default scope. Application questions are included in the package hash.
Changes to the source CV, profile, questions, or answer package invalidate the
review marker. Rebinding a replaced CV requires an explicit UI action after source
review. Personal execution has no authorized live adapter and returns
`BlockedPermission`; no tool can approve a submission.

Host profile/job/answer proposals are kept separately from verified facts and
answers inside the same protected payload. Host summaries expose professional
skill labels and usable evidence references, excluding raw CV text, contact data
and salary. Personal drafts and pending proposals are included in local export
and removed by workspace deletion.

## Test-first evidence

Core tests were written first. The initial run exited 1 for missing question/scope
contracts (`artifacts/verification/w08-questions/core-red.log`). The first runtime
run had 62 pass/1 fail because the test fixture's name was not locally confirmed;
the test now explicitly uses the confirmed fixture. Final Core **63/63** passed,
exit 0, `core-green-2.trx` in that directory. Five new tests exercise scoped resume,
role precedence, question hash binding, manual-only protection and invalid states.

To avoid simultaneous writes to shared build outputs, parent service/UI work used
an isolated checkout at `%TEMP%/jobagent-workspace-service-60f3dd83`, based on public
`b7f020d` plus the new Core/host contracts. Source changes were copied back by an
explicit file allowlist; the parallel storage implementation was not overwritten.

`WorkspaceHostTests` initially failed **6/6**, while the existing **10/10** workspace
tests passed: the workspace did not implement the shared host service. The report
is `artifacts/verification/workspace-host/host-red.trx`. After implementation, all
**16/16** passed (`host-green-3.trx`). An intermediate failed build was a lambda
discard typo; another test failure exposed the difference between professional
experience evidence and the separate synthetic fixture's fact kind. The corrected
host projection uses reviewed experience skills and verified fact references, not
raw fact text.

Four UI-service tests then began RED on the missing review APIs (`review-red.log`).
After implementation, **20/20** workspace tests passed (`review-green.trx`):
same-application persistence and resume, stale package rejection after question
changes, explicit source rebinding, and user review of an imported job proposal.

The actual Chromium personal UI test first failed because the create-draft button
did not exist (`ui-red.trx`). A later failed run exposed ambiguous implicit select
labels; explicit accessible labels fixed the issue. Final **1/1** passed, exit 0,
`ui-green-2.trx`. It creates a draft, reviews an application-scoped answer, reaches
ready state, reloads the same application, adds a new question and returns to
`NeedsInput`. Every observed page request stays on the local dashboard origin.
The synthetic profile was seeded by the trusted test driver; this is not a real
candidate or real employer application.

The actual full-page screenshot was visually inspected and copied to ignored
`artifacts/screenshots/personal-application.png`. Logs/TRX files were copied back
under the same `artifacts/verification/workspace-host/` path. These isolated runs
predate final integrated storage/security review corrections; final full-solution
verification must be recorded separately rather than inferred from these counts.
