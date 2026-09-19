# Install and run (Windows x64)

Prerequisites: Windows x64, PowerShell 7, Git, Node.js 22 with npm (verified in
GitHub CI), and internet for the first setup. PowerShell 7 is separate from the
older Windows PowerShell included with Windows. Run these commands in PowerShell 7.
No Chrome extension, desktop-control plugin, API key or ChatGPT subscription is
required for the local application. The installer provides its own Chromium.

Clone the repository and run the setup/start commands in [README](../../README.md).
`bootstrap.ps1` installs
pinned SDK 10.0.401 per-user if absent, verifies its SHA512 against Microsoft release
metadata, restores locked packages, builds the UI and solution, and installs the
matching Playwright Chromium. It does not install Git, Node or PowerShell, change
system PATH, modify Codex configuration, or request administrator permissions.
`doctor.ps1` checks prerequisites; optional `verify.ps1` actually launches Chromium
in tests. `run-demo.ps1` starts the dashboard and synthetic career site. These
scripts do not use paid model APIs. Codex integration is optional; see
[CODEX_SETUP.md](CODEX_SETUP.md) for the separate host requirements and usage limits.

The launcher serves the dashboard at `http://127.0.0.1:5178` and fixture site on
port 5179. Use its private `#token=...` link; the page removes the fragment before
opening a cookie-backed session. Do not publish the private link. Only literal
127.0.0.1 is supported; localhost aliases and other hosts are rejected.

Runtime state: `%LOCALAPPDATA%/JobApplicationAgent/demo`. Profile payloads use
current-user Windows DPAPI. The separate application journal is synthetic-only.
Stop the launcher before changing runtime files. For a fresh demo, rename that
exact `demo` directory to a dated backup; restarting creates a new one. Do not
delete the whole JobApplicationAgent directory: it also holds the SDK.

Run `scripts/package.ps1` to create a local framework-dependent package. Its
manifest lists every packaged file/hash. The package needs the .NET 10 ASP.NET
runtime (the pinned SDK includes it), PowerShell and compatible Chromium. It
does not bundle Chromium, user databases, session state or secrets. Packaging
is separate from publication. Public prerelease downloads are available on
[GitHub Releases](https://github.com/caglarhekimci/job-application-agent/releases).
For a first installation, the source/bootstrap route above handles dependencies;
the release ZIP alone is not a self-contained installer.

Windows integration is the current support boundary. Windows GitHub-hosted CI and
a separate clean checkout have passed; see [verification](../VERIFICATION.md).
A new user's interactive setup and Linux/macOS have not been verified.
