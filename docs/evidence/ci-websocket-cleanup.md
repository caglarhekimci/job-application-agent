# CI driver-cleanup regression

The first alpha.2 candidate `f3f38dd` failed public Windows
[CI35476840378](https://github.com/caglarhekimci/job-application-agent/actions/runs/35476840378).
E2E94/95 passed. `WebSocketEgress_IsBlockedBeforeHandshake` had already asserted
policy denial and zero trap requests, but disposal threw `InvalidOperationException`
from `StdIOTransport.Close` while waiting on an already-disposed driver process.
This is retained as a failure, not dismissed as a passing security test.

The cleanup correction makes the Playwright handle single-consumption and tolerates
only that transport-close exception after a WebSocket was denied. Other disposal
exceptions still propagate; request policy and zero-handshake assertions are unchanged.
The [pinned upstream transport source](https://github.com/microsoft/playwright-dotnet/blob/v1.62.0/src/Playwright/Transport/StdIOTransport.cs)
shows the close callback waits on a process also disposed by transport cleanup.

The failed candidate's prepared archives are not approved for public release.
The focused `AdversarialFormTests` run passed **10/10**, no skips; Release build
passed. The formatter initially requested a catch-body line break; its correction
and `git diff --check` then passed. Report: `artifacts/test-results/ci-websocket-cleanup/green.trx`.
Exact-source [CI 35477129404](https://github.com/caglarhekimci/job-application-agent/actions/runs/35477129404)
then passed all 293 tests, including 95 E2E tests, at 4d475bb. Rebuilt archives passed
their package checks and were published in [alpha.2](release-alpha2.md).
No model call was made for this correction.
