# W05 answer proposal and scoped-memory evidence

Date: 2026-09-20
Scope: strict validation of model-shaped answer proposals and common Core methods for reviewed scoped-answer updates and revocation.

## Contracts and behavior

`ModelAnswerProposalValidator.ValidateJson(json, question, profile, suppliedEvidenceIds, now)` accepts a local JSON string with exactly these v1 fields:

- `schemaVersion`
- `semanticKey`
- `proposedValue`
- `language`
- `evidenceIds`
- `rationale`

The validator caps raw UTF-8 JSON at 32 KiB and rejects malformed JSON, duplicate/unknown/missing fields, unsupported versions, question/language mismatches, empty values, proposed values over the smaller of the field limit and 4,000 characters, sensitive or attestation questions, duplicate evidence, evidence outside the caller-supplied model context, and evidence that is missing, expired, rejected, or unverified in the current profile.

A valid output returns `AnswerProposalDisposition.RequiresReview` and an `AnswerProposal` whose `ReviewStatus` is forced to `Proposed`. It never returns `AnswerStatus.Resolved`, mutates the candidate profile, or writes answer memory. Invalid output returns `Abstained` with no proposal value.

`AnswerMemoryService.UpsertReviewed(profile, update, now)` is the common trusted-service mutation for a separately reviewed answer. It validates semantic key, language, answer length, scope shape, future expiry, and any referenced current evidence. Identity is the exact combination of semantic key, language, scope type, and scope ID; an update replaces only that identity and increments the profile version.

`AnswerMemoryService.Revoke(profile, key)` removes only the exact scoped identity and increments the profile version when a value was removed. A missing identity is a no-op. Repository profile history remains the audit source for the removed prior value.

## Test-driven evidence

The focused RED failed during compilation because the proposal validator, proposal status types, memory service, update type, and revocation key did not exist. Exit code was 1 (`artifacts/w05-answer-proposals/red.log`).

After the first implementation, the focused proposal and memory tests passed 7/7 (`artifacts/w05-answer-proposals/green-focused.log`). Review tests then reproduced missing raw-size/value caps, generic duplicate-key handling, and unbounded/null memory evidence handling as 3/3 failures (`artifacts/w05-answer-proposals/review-red.log`). After the boundary fixes, those tests passed 3/3 (`artifacts/w05-answer-proposals/review-green.log`).

Final verification passed the focused set 10/10 (`artifacts/w05-answer-proposals/green-focused-final.log`) and the complete Core test project 52/52, with 0 failures and 0 skips (`artifacts/w05-answer-proposals/core-full-final.log`).

The tests cover a grounded proposal remaining unverified, abstention for unverified evidence, abstention when cited evidence was not supplied to the model context, strict schema drift and duplicate-key rejection, raw JSON and value caps, sensitive-question abstention, exact scoped replacement, exact scoped revocation, invalid scope rejection, and missing/null/unbounded evidence rejection. Test time is fixed at 2026-09-18.

## Boundaries

- No external network, paid API, provider SDK, model invocation, or model-host claim is part of this slice.
- Test JSON is a deterministic fixture for validator behavior. It is not evidence of model accuracy or quality.
- Evidence validation proves that cited IDs were allowed and currently verified. It does not prove that free-form wording logically follows from the evidence; the proposal therefore remains proposed until explicit user review.
- The UI for reviewing a proposal, selecting its storage scope, and invoking `UpsertReviewed` is not implemented in this slice.
- Revocation removes the current answer. A separate revocation tombstone/event is not added to `AnswerMemory`; durable profile revision history is required for audit.
- The service does not grant data-sharing consent or authorize browser submission.
