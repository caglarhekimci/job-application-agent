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
and formatting checks passed. Report: `artifacts/test-results/ci-websocket-cleanup/green.trx`.
A new exact-source CI run and rebuilt archives remain the publication gates.
No model call was made for this correction.
