# Troubleshooting

| Symptom | Action |
|---|---|
| SDK mismatch | Run bootstrap in PowerShell 7; use-toolchain selects the per-user pinned SDK. |
| Node/npm not found | Install a supported Node runtime yourself, reopen PowerShell, rerun bootstrap. |
| Chromium missing | Run bootstrap; browser revision must match Playwright 1.62.0 (1234). |
| Port 5178/5179 in use | Stop the previous demo with Ctrl+C. Do not terminate unrelated processes. |
| Session unauthorized after restart | Open the new private link printed by the current launcher. |
| Session changed before submit | Cancel the unsubmitted flow and restart with a fresh synthetic runtime backup. |
| BrowserSessionLost | Restart discarded the old browser. Review and give fresh sharing consent. |
| SubmittedUnverified | A request may have been accepted. No retry is permitted; inspect the fixture receipt privately. |
| SubmittedVerified after restart | Expected: durable receipt survives even though the fake server's memory resets. |
| PackageChanged/FormChanged | A file, answer or form changed. Old approval is invalid. |
| LinkedIn blocked | Expected. User consent is not platform authorization; no bypass is provided. |
| MCP profile not found | Run/confirm the demo first and point MCP at the same runtime directory. |

Raw verification logs and screenshots are under ignored `artifacts/`. They may
contain local paths; never publish the whole directory. Share synthetic summaries
after reviewing them. A successful doctor check is not an E2E test.
