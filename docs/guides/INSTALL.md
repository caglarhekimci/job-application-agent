# Install and run (Windows x64)

Prerequisites: PowerShell 7, Git, Node.js with npm, internet for dependency restore.
The development machine used Node 20.14.0/npm 10.7.0. This is an observed version,
not a claim that an old Node release remains supported. Use a supported Node
release compatible with the locked Vite toolchain for a new installation.

From the checkout, run the four commands in README. `bootstrap.ps1` installs
pinned SDK 10.0.401 per-user if absent, verifies its SHA512 against Microsoft release
metadata, restores locked packages, builds the UI and solution, and installs the
matching Playwright Chromium. It does not install Git, Node or PowerShell, change
system PATH, modify Codex configuration, or request administrator permissions.
`doctor.ps1` checks prerequisites; `verify.ps1` actually launches Chromium in tests.

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
is not publication; there is no download URL or release yet.

Windows integration is the current support boundary. Linux/macOS, a fresh OS/user
profile and GitHub-hosted CI must be reported separately from local checks.
