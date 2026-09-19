# Job Application Agent — Local Inspector

This Windows plugin exposes three read-only tools from the local Job Application
Agent companion. It is an initial local distribution package, not a public
directory listing or an autonomous application sender.

| Tool | What it returns |
|---|---|
| `runtime_get_capabilities` | Current fixture mode and restrictions |
| `profile_get_summary` | Confirmed profile version and currently verified professional skill names, using a canonical GUID |
| `application_get_status` | Synthetic application state and receipt identifiers when present, using a canonical GUID |

There is no approval, write, browser, download, arbitrary-file, shell, or submission
tool. LinkedIn remains blocked. The plugin does not call a paid API. A model using
these tools through Codex can consume the user's normal plan allowance.

## Prepare a local package

Prerequisites: Windows, PowerShell 7, the .NET 10 runtime, and a compatible local
plugin host. The project bootstrap installs a per-user .NET toolchain. The plugin
launcher uses that toolchain when present, otherwise the `dotnet` executable on
PATH. It never downloads or installs software on startup.

From a prepared repository checkout, first produce the regular MCP publish output
with the project packaging workflow. Then run:

```powershell
./plugins/job-application-agent/scripts/build-package.ps1 -McpDirectory <published-mcp-directory>
./scripts/test-plugin-package.ps1 -McpDirectory <published-mcp-directory>
```

The builder creates an ignored `artifacts/plugin-packages/` ZIP with the plugin
folder, compiled runtime dependencies, licenses, a per-file hash manifest, and a
ZIP checksum. A Release build directory is also supported for local verification;
published releases should use the reviewed publish output. No runtime data is
packaged. Source-only installation fails with a setup message until runtime files
are present.

Extract the archive and select its `job-application-agent` folder when configuring
a local plugin marketplace with the host's plugin-creator workflow. This repository
does not install or register a marketplace automatically. The portable root
manifest and compatibility manifest carry the same identity. Their MCP launch
entry uses `cwd: "./"` and a contained relative script path; it does not rely on
legacy placeholder interpolation.

For direct STDIO setup before plugin installation, configure `pwsh` with arguments
`-NoLogo`, `-NoProfile`, `-File`, and the absolute path to the extracted
`scripts/start-mcp.ps1`. A direct connection is not a plugin-installation test.

## Data and everyday use

The default data directory is `%LOCALAPPDATA%\JobApplicationAgent\demo`, shared
with the synthetic companion. A trusted local operator can set
`JOBAGENT_RUNTIME_DIR` in the MCP process configuration. This setting is never a
tool parameter. Profile payloads require the same Windows user who stored them.
The plugin does not discover or enumerate records; provide an existing reference.
An empty runtime supports capability checks and returns errors for missing records.

Prepare and confirm profiles in the companion application. If a record is missing,
unconfirmed, or unavailable, report that result instead of guessing. Receipt
evidence describes the synthetic local site; it is not evidence of an employer
receiving an application. [Data disclosure](PRIVACY.md) explains what reaches the
model host. [Review cases](REVIEW_CASES.md) distinguish proposed tests from results.

Local packaging, public plugin review, and Codex for Open Source support are
separate processes. The current public MCP submission route requires a stable
public HTTPS service or separate OpenAI coordination for local MCP support. This
package does not supply that service. See the repository's
`docs/grant/STORE_TRACK.md` for actual submission blockers.
