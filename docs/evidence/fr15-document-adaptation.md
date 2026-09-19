# FR-15 extractive CV and cover-letter adaptation

**Focused verification date:** 2026-09-20

## Delivered behavior

The personal workspace can assemble a job-specific CV and short cover-letter text without a model or external API. This is an extractive assembly feature, not a stylistic AI rewrite. Candidate statements are copied exactly from currently verified imported-CV source spans. Fixed headings and the reviewed employer/title are the only generated text.

Professional work, internships, part-time work and personal projects remain separate. Confirmed job skills affect ordering only. The generator never derives experience duration, proficiency, achievements, work authorization, salary or motivation. Proposed, expired, missing, source-mismatched, unlinked or conflicting-kind evidence is excluded or fails closed.

Each proposal binds the application and payload hash, profile id/version, original resume reference/hash, reviewed job content and requirements, and the current usable source-fact set. Approval binds the exact bundle and content hashes. Reads, approval and export recompute currentness, including validity expiry. A changed profile, CV, job, application package or evidence set makes the proposal stale.

The original protected CV bytes and the application's original resume reference/hash are unchanged. Approved output is available only as fixed-name UTF-8 `adapted-cv.txt` and `cover-letter.txt` downloads. It is never selected as a submission attachment.

## Local UI boundary

Dedicated `/api/workspace/applications/{id}/document-adaptation` routes inherit the dashboard's loopback Host/IP, UI cookie, same-origin and CSRF checks. They are not exposed through MCP or the host interface and accept no path, URL or model-created approval value.

The application card shows source and proposed CV text side by side, the cover-letter citations, and Included/Moved/Omitted/Structural changes. Its approval checkbox starts unchecked and binds workspace revision plus bundle hash. Regenerating or refreshing the proposal clears confirmation. Download links appear only for a current approved bundle.

## TDD and verification evidence

- Core RED: missing adaptation namespace/API — `artifacts/verification/fr15-red/fr15-core-red.trx`.
- Workspace RED: missing protected-workspace methods — `artifacts/verification/fr15-red/fr15-workspace-red.trx`.
- UI RED: real browser reached the personal application, then timed out on the absent proposal control — `artifacts/verification/fr15-red/fr15-ui-red.trx`.
- Review RED: contradictory fact kind and stale source-preview pairing both failed before their fixes — `fr15-kind-red.trx` and `fr15-source-snapshot-red.trx` in the same RED directory.
- Core focused GREEN: **6/6** — `artifacts/verification/fr15-green/fr15-core-final-green.trx`.
- Workspace focused GREEN: **4/4** — `artifacts/verification/fr15-green/fr15-workspace-final-green.trx`.
- Real browser GREEN: **1/1** — `artifacts/verification/fr15-green/fr15-ui-green.trx`.
- Frontend production build: exit **0**, 32 modules transformed.
- Web build: exit **0**, 0 warnings and 0 errors.

The browser test creates the protected personal workspace, proposes and regenerates the documents, verifies confirmation reset, approves the current bundle, downloads and inspects the CV, and observes zero requests outside the local app. The captured page was visually inspected at `artifacts/fr15-document-adaptation.png`.

## Limits

- Plain-text export intentionally preserves exact source wording and basic structure; it does not reproduce the original PDF/DOCX layout.
- Relevance is deterministic skill matching against reviewed requirements. It does not claim semantic or stylistic optimization.
- The adapted files remain separate local downloads. Selecting one as a future application attachment requires a separate explicit feature and approval design.
