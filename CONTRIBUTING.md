# Contributing

This repository is currently local and unpublished; no issue or pull-request URL
is claimed. Work locally on a branch and run `scripts/verify.ps1` before review.
Start with a meaningful failing behavior test and preserve its actual output.
Use only clearly marked synthetic profiles, resumes and jobs in tests.

Keep changes small. Explain the user-visible behavior, relevant threat boundary,
test command/results and limitations. Do not claim a real model, host, platform,
or operating system was tested when only a fixture or another OS was exercised.
Never add a model-callable approval tool, arbitrary browser URL/file tool, retries
after uncertain submission, real candidate data, tokens or browser profiles.

Read AGENTS.md, both supplied project specifications and docs/PROJECT_STATE.md.
Update state, decisions, capabilities and verification for each completed slice.
Existing work must be preserved. Public hosting and external submissions are
separate approval gates. A usable public contribution channel will be configured
only when the owner approves publication.
