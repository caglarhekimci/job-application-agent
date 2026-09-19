# W17 local plugin package evidence

Date: 2026-09-19. Scope: distributable Windows read-only companion package.
No installation into user configuration, public store submission, model call,
paid service, or live job application occurred during this package verification.

## Package shape

`plugins/job-application-agent/` includes portable `plugin.json` and `mcp.json`,
Codex compatibility `.codex-plugin/plugin.json` and `.mcp.json`, a contained
PowerShell STDIO launcher, a ZIP builder, data disclosure and reviewer scenarios.
There are no lifecycle hooks or skills implying additional runtime powers.
Both manifests use the same name/version and both MCP declarations invoke the
same relative script under `cwd: "./"`.

The official plugin-creator scaffold was used without `--with-marketplace`, so it
did not change personal or repository marketplace configuration. The generated
compatibility manifest was then given accurate project metadata. The portable
manifest follows current documentation, with OpenAI display metadata under
`extensions.com.openai`. The launcher requires prepared runtime binaries and
does not download, build, initialize a profile, or run a browser on startup.

The ZIP builder copies a reviewed MCP build/publish directory into the archive;
it preserves upstream dependency files/notices, excludes debug symbols, rejects
private-file extensions and links, and records per-file SHA256 values. That
extension check is not claimed to detect every possible secret or personal datum;
the full public-source and release privacy review remains a separate gate.

## Executed checks

| Phase/check | Actual command | Exit/result |
|---|---|---|
| RED before package implementation | `pwsh -NoProfile -File scripts/test-plugin-package.ps1` | 1; missing portable manifest; `artifacts/logs/plugin-package-red.log` |
| Compatibility contract | plugin-creator `scripts/validate_plugin.py plugins/job-application-agent` | 0; validation passed |
| Portable schemas | PowerShell `Test-Json -SchemaFile` for both root manifests | Both true; exact Agent Plugins 1.0.0 schemas fetched and cached locally |
| GREEN package/transport | `pwsh -NoProfile -File scripts/test-plugin-package.ps1` | 0; `artifacts/logs/plugin-package-green.log` |
| RED publish-output license regression | same script with `-McpDirectory artifacts/packages/job-application-agent-20260919T210019Z/mcp` | 1; exact PdfPig license absent; `artifacts/logs/plugin-package-license-red.log` |
| GREEN publish-output regression | same publish-output command after builder fix | 0; `artifacts/logs/plugin-package-release-green.log` |

The first package attempt also caught an overbroad extension filter matching
the legitimate `Microsoft.Data.Sqlite.dll` assembly. The filter was narrowed to
actual database suffixes; the private `.pem` rejection test still passed. That
intermediate failure was not relabeled as a passing run.

The final script contains seven reported check groups:

1. Portable and compatibility identities and contained launcher agree.
2. The extracted ZIP contains the exact vendored PdfPig license, worker DLL,
   dependency manifest, runtime configuration and PdfPig assembly.
3. A ZIP extracted to a different path with a space starts the actual runtime,
   performs MCP initialize and lists exactly three read-only, closed-world tools.
4. An actual capability call returns fixture restrictions; a path supplied as a
   profile reference is rejected; the empty runtime stays empty.
5. A launcher copied without runtime binaries exits nonzero with a setup message.
6. A package source containing a synthetic `.pem` file is rejected before packaging.
7. A package source missing the document worker's runtime configuration is rejected.

This is a process/protocol test. The initial run used existing Release binaries;
the final run used the actual published MCP output identified in the table. It did not build
.NET projects or invoke a model. It is not an installed-plugin host verification.
All eight proposed review prompts are honestly marked by their coverage in
`plugins/job-application-agent/REVIEW_CASES.md`.

The earlier archive at `20260919T205815834Z` is superseded because it did not carry
the newly required PdfPig license. Successful current archive:
`artifacts/plugin-packages/job-application-agent-local-20260919T210452237Z.zip`.
SHA256: `895ddddee618dbed1c9dc627dee076e9d55bd70387570b54a1017ba4d02ffdc8`.
The adjacent `.sha256` file and archive `MANIFEST.json` also contain checksums.
Binary artifacts are ignored by Git and must be rebuilt from the final release
output before publication if implementation files change.

## Validator environment

The supplied validator requires PyYAML. The existing Python 3.14 and bundled
Python did not have it; `PyYAML==6.0.3` was installed from PyPI into the isolated
per-user `JobApplicationAgent/tools/plugin-validator-python` directory, then made
available only through the validator process environment. No global Python package
or Codex configuration was edited.

For repeat validation in that environment:

```powershell
$env:PYTHONPATH = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent/tools/plugin-validator-python'
python "$env:USERPROFILE/.codex/skills/.system/plugin-creator/scripts/validate_plugin.py" plugins/job-application-agent
```

Portable schema cache: `artifacts/plugin-validation/plugin.schema.json` and
`mcp.schema.json`; retrieved from their declared Agent Plugins 1.0.0 schema URLs.
These schema checks validate shape, not installation, operating-system support,
publication eligibility or model behavior.

## Sources and external gates

Current [OpenAI packaging documentation](https://developers.openai.com/plugins/build/plugins)
supports the portable root format and compatibility fallback. The
[official Codex MCP loader](https://github.com/openai/codex/blob/main/codex-rs/codex-mcp/src/plugin_config.rs)
resolves a relative legacy `cwd` against the plugin root, while the
[portable loader](https://github.com/openai/codex/blob/main/codex-rs/codex-mcp/src/agent_plugin_config.rs)
supports contained portable paths. The package avoids relying on old placeholder
substitution behavior.

The actual [public submission flow](https://developers.openai.com/plugins/deploy/submission)
and current account/deployment blockers are recorded in
[STORE_TRACK.md](../grant/STORE_TRACK.md). Public listing acceptance and Pro support
are external decisions; this successful local package test does not establish either.
