# First public prerelease

[v0.1.0-alpha.1](https://github.com/caglarhekimci/job-application-agent/releases/tag/v0.1.0-alpha.1)
was published on 2026-09-19 at 21:25:34 UTC (20 September in Istanbul).
It is a prerelease, not a completed general job-application product.
Tag target: `b0687232553a6c0ec7fc92b5bfb9ec727fa326f1`.

The exact commit passed all **137 tests** in the separate clean checkout,
`artifacts/verification/20260919T212118Z/`, exit 0.
[Public CI run 35469984283](https://github.com/caglarhekimci/job-application-agent/actions/runs/35469984283)
also completed successfully for that commit.

The source was packaged in the clean checkout, not the concurrently edited working
tree. Package creation, manifest verification, packaged PDF/DOCX imports and
relocated plugin STDIO checks all exited 0.

| Public asset | Bytes | SHA-256 |
|---|---:|---|
| job-application-agent-20260919T211813Z.zip | 122639255 | 6C2267ED5C812A0A6A7CAD742FC29EB6A695738FE23A1E18355E0B92920098E5 |
| job-application-agent-local-20260919T211845926Z.zip | 61499182 | 1B2C2FB9FF4209AC513DA5E0664A16A1ED4ABE086B9560E365C24D11369041F9 |

Unauthenticated GitHub REST confirmed release ID 392218492, draft=false,
prerelease=true and both uploaded asset digests equal to the local hashes.
The app checksum is also attached. Archives contain no personal runtime data.

The next actual installed-plugin test discovered a multiple-dotnet PATH selection
bug in the plugin launcher when the per-user SDK path is absent. Its RED/GREEN
regression and correction are being recorded for the next release; alpha.1 assets
are not silently replaced. The normal source bootstrap creates the pinned per-user
SDK used by the already-passing alpha.1 package checks.

The release establishes public distribution, not independent adoption, plugin-store
approval, Codex for OSS submission or a program award.
