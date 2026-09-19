# W05 local answer-memory review

Implemented 2026-09-19 UTC. The authenticated local UI saves reviewed answers with
application, company or default scope, language and optional expiry. Scope IDs
come from the stored job, never from a model or arbitrary UI scope-ID field.
Memory updates and revocation use expected workspace revision and protected atomic
storage, increment profile versions, and preserve disclosed history.
New CV/profile review invalidates current memory. Revocation stops current use;
full workspace deletion clears the retained history.

Known question types use canonical motivation/availability keys. Custom questions
are explicitly exact-text matches, not semantic paraphrase recognition.
No network, paid API or model call occurs in this UI flow.

## Actual tests and review

- Initial Workspace tests failed compilation because review/revoke contracts did
  not exist (exit 1). Implementation initially passed 8/9; the history assertion
  compared raw JSON with an apostrophe escaped by the serializer. The assertion
  was corrected to inspect the parsed JSON value. All 9 passed afterward.
- UI RED: missing memory controls caused timeout (exit 1).
- First UI implementation exposed a select accessible-name issue; explicit
  accessible labels fixed it. The browser memory save/reload/revoke test passed.
- Independent review found confirmation drift after a job change, unstable
  same-posting identity, and free-text/canonical question mismatch.
- Identity regression RED failed with differing GUIDs. Confirmation regression
  initially observed transient busy state and incorrectly passed; it was corrected
  to await completion and inspect the checkbox, then failed as intended.
- The fix binds confirmation to the exact workspace revision. Canonical question
  selection now produces a resolved answer in the real browser test.
- Same-job identity is retained only for the same employer and same nonempty
  canonical URL, or identical title/text when both URLs are empty. Changed
  recipient or ambiguous changed no-URL posting gets a new ID.
- Final focused Release results: Workspace **10/10**, personal UI **1/1**, exits 0.
  Reports: `artifacts/verification/w05-reviewed-green/workspace.trx` and `ui.trx`.
  RED reports: `w05-review-red/identity-red.trx` and
  `w05-review-red-fixed/confirmation-red.trx`.
- The browser screenshot `artifacts/screenshots/personal-workspace.png` was
  visually inspected; no unexpected external request occurred.

These are local synthetic tests, not evidence of model answer quality or adoption.
