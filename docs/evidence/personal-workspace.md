# Personal workspace evidence

Date: 2026-09-19 UTC. All test documents and identities are synthetic.

The authenticated loopback UI accepts a bounded uploaded stream through the common
TXT/PDF/DOCX importer. Original bytes, extracted text, profile versions and pasted
job data are stored atomically in one DPAPI-protected SQLite payload outside the
checkout. Updates use an expected revision and database compare-and-swap. This
separate initial workspace schema has no upgrade chain yet; the profile repository's
tested migration chain is a different storage component.

Review requires a name/email and validates salary/date bounds, experience type and
an actual document source span. Selecting a source is the user's confirmation, not
proof that a deterministic parser understood every claim. No model invents dates,
skills or answers. The UI intentionally limits automatic salary answers to monthly
net TRY and automatic experience answers to the implemented semantic rules.

RED: Workspace.Tests failed on absent LocalWorkspace/ProfileReview contracts;
`artifacts/workspace/red.log`, exit 1. Initial GREEN: 6/6. Browser first failed on
an ambiguous select label; explicit accessible label corrected it. Browser then
passed upload, review, job evaluation, answer, export, reload and deletion.

Review reproduced unsaved-name loss on tab switch (empty actual value vs expected
synthetic name): `artifacts/workspace/tab-red/`, exit 1. Keeping the component
mounted preserves draft fields across tabs. A nullable private-minimum view contract
now distinguishes an unprovided minimum from zero and hydrates it independently
from a salary target; its RED and GREEN are recorded in workspace logs.

Integrated GREEN: 7 workspace tests plus the personal browser test within the
36 E2E tests, at `artifacts/verification/20260919T205804Z/`; all passed. The browser
asserts zero requests outside the local app origin. Screenshot was captured and
visually inspected at `artifacts/screenshots/personal-workspace.png`.

Export is explicitly plaintext and includes the original CV and version history.
Deletion clears the protected current/history payload with SQLite secure_delete;
it is not a forensic erasure guarantee for backups or storage devices. No external
application adapter or user-provided URL is invoked from this workspace.
