# W04 job import, review, permission, and storage evidence

Date: 2026-09-19
Scope: deterministic import from text supplied by the user, explicit requirement review, source-permission policy, job eligibility, and protected local persistence.

## Implemented behavior

- `JobPostingImporter.ProposeProvidedText` accepts already supplied text and never fetches its URL. It normalizes the URL, hashes the supplied text, and returns conservative typed suggestions with line spans and a rule rationale.
- The initial extractor recognizes only an explicit professional-experience sentence shape. Ambiguous prose produces no requirement rather than a fabricated fact.
- `ConfirmRequirements` accepts selected parser suggestions. `ConfirmReviewedRequirements` also accepts manually typed requirements after validating their typed shape, unique ID, and exact presence of their requirement text in the supplied posting. Both paths mark requirements confirmed and record the review time.
- Canonical URLs discard fragments and known tracking parameters while retaining and sorting identity-bearing query parameters.
- Duplicate identity combines source, normalized employer, and external job ID, with canonical URL and text hash as fallbacks. Distinct external IDs at the same employer remain distinct.
- `JobEvaluator` returns `Closed` for closed jobs. Empty or unconfirmed requirement sets return `InsufficientInformation`; they cannot become eligible by omission.
- Source permissions include evidence ID, scope, verification/expiry/review times, allowed actions, and recipient origin. Expiry or review due at the evaluation instant is denied. External actions require an exact normalized recipient origin.
- `LinkedInRestricted` is always denied by the runtime policy. The durable repository also rejects a caller-constructed LinkedIn permission that claims allowed status or actions.
- `JobRepository` uses a separate SQLite `jobs.db` outside the source checkout. The complete job and permission document is protected with `IPayloadProtector`; the visible duplicate key is SHA-256. Save is an identity-keyed upsert.

## Test-driven evidence

The first focused Core run failed to compile because the new import, canonicalization, review, and permission APIs did not exist (`artifacts/job-import/core-red.log`). The first focused Infrastructure run failed because the repository API did not exist (`artifacts/job-import/infrastructure-red.log`).

An additional behavior test then demonstrated that an imported job with no reviewed requirements was incorrectly eligible: 1 failed and 15 passed (`artifacts/job-import/core-empty-requirements-red.log`). After the evaluator fix, the focused Core set passed 16/16 (`artifacts/job-import/core-green-filtered.log`).

The first repository implementation run exposed that an interface-typed permission set could not be deserialized. Changing the persisted contract to a concrete `HashSet<SourceAction>` made the focused repository set pass 5/5 (`artifacts/job-import/infrastructure-green-filtered.log`).

The manual-review API test first failed to compile because `ConfirmReviewedRequirements` did not exist (`artifacts/job-import/core-manual-review-red.log`), then passed with all six job-import tests (`artifacts/job-import/core-manual-review-green.log`).

Final project verification:

- Core: 42 passed, 0 failed, 0 skipped (`artifacts/job-import/core-full-final.log`).
- Infrastructure: 25 passed, 0 failed, 0 skipped (`artifacts/job-import/infrastructure-full.log`).

Tests use a fixed 2026 timestamp and cover URL tracking/identity handling, duplicate IDs, conservative extraction, manual review, invalid typed requirements, closed and unknown evaluation, permission expiry and recipient/action scope, LinkedIn elevation rejection, protected restart, identity upsert, and checkout-path rejection.

## Boundaries and remaining W04 gaps

- This slice makes no network requests and has no site scraper, feed client, browser automation, autofill, or submission implementation.
- LinkedIn automation remains blocked because there is no verified authorization evidence.
- The rule extractor is intentionally small; other requirement types must be entered and reviewed by the user or added through separately tested deterministic rules.
- `jobs.db` is an initial `EnsureCreated` schema. It does not yet have the versioned upgrade path implemented for the profile database.
- Windows deployment relies on the existing DPAPI protector. Plaintext protection is permitted only for explicitly enabled synthetic, non-Windows tests.
- The repository stores no plaintext profile or posting payload, but SQLite operational metadata, timestamps, and the hashed duplicate identity remain visible.
