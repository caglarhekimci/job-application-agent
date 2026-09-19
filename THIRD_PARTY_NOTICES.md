# Third-party components

Original code uses the unmodified MIT license text from
[OSI](https://opensource.org/license/mit), checked 2026-09-18. Third-party software
keeps its own license. This project does not relicense dependency code.

NuGet versions and integrity hashes are in per-project `packages.lock.json`;
frontend versions/integrity/license metadata are in `web/package-lock.json`.
Installed NuGet `.nuspec` files are authoritative for declared expressions/URLs.
The primary runtime packages are Microsoft EF Core/SQLite, Playwright .NET,
System.Security.Cryptography.ProtectedData, ModelContextProtocol, PdfPig and React.

PdfPig 0.1.16 declares Apache-2.0. The full upstream LICENSE, including inherited
component notices, is preserved in `third-party-notices/PdfPig-0.1.16-LICENSE` and
copied into distributable packages. Source commit from the installed NuGet manifest:
`a7bb35662bbbf405efddad50aedc9bcdcf515afc` at https://github.com/UglyToad/PdfPig.
The downloaded notice SHA-256 is
`4C510E162F896EA43B4B1CF1B743641D7C756F1004D0E5EAF0615B28B7DED409`.

`scripts/package.ps1` preserves Playwright driver notices and copies the installed
React, React DOM and scheduler LICENSE files. It does not redistribute the .NET
SDK or Chromium browser. Chromium is installed separately through Playwright.

An inventory is saved at `docs/evidence/DEPENDENCIES.json` after local restore.
That inventory is not a legal audit. Before publication review all runtime,
transitive/native components and redistributed notices, and re-run vulnerability
checks. An unresolved dependency/license issue blocks a public release; no license
compliance badge or independent audit is claimed.
