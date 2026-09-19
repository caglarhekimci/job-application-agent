# Privacy boundaries

The demo contains only authored synthetic data. No real CV was imported from the
parent directory. It does not send telemetry or make model API calls. Setup and
dependency auditing contact package registries/Microsoft, not candidate services.

Profile JSON and pending patches use Windows DPAPI CurrentUser before SQLite
storage. This is payload protection, not whole-database encryption. Metadata,
file names and sizes remain visible. Another process running as the same Windows
user is outside this protection boundary. The SQLite application journal accepts
synthetic records only; real applications require a separately reviewed protected
storage path before support can be enabled.

The personal workspace stores resume bytes, source text, profile versions and pasted
job text together in a DPAPI-protected SQLite payload outside the checkout. Its UI
supports export and explicit deletion. SQLite deletion is logical deletion, not guaranteed forensic erasure
from WAL files/backups/storage devices. Exports contain sensitive data and must
remain private. Use the separate personal workspace for personal information;
the fixture screen retains its authored synthetic data.

No persistent browser profile, trace or screenshot is captured by the runtime.
E2E tests deliberately capture synthetic UI screenshots under ignored artifacts.
CSRF and authentication material are excluded from state responses and MCP.
The opt-in synthetic command bridge stores its separate credential with DPAPI
outside the repository. Personal workspace documents and answer memory are not
exposed by its model-callable tools. Opt-in host and model tests send only authored
synthetic data through the existing ChatGPT account and use included quota.
Private salary minimums are excluded from answers, fixture state and MCP summaries.
The authenticated personal profile editor displays them so users can review/delete
their own data. Personal exports include them and the original CV.

Public evidence may include only reviewed synthetic reports and aggregate facts.
The default is no public upload. There are no independent user records or usage
analytics. Future pilot participation and data collection require explicit consent.
