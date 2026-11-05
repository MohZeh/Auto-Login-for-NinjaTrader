# Development

Use Windows x64, the stable .NET 10 SDK and the .NET Framework 4.8 runtime.
The SDK restores the pinned Microsoft reference assembly package; no NinjaTrader install
or credential is needed to compile. Package lock files are committed and restore uses
locked mode. The SDK selection is in `global.json`; product version is owned by
`Directory.Build.props`.

- `./build.ps1`: restore and build the solution into `artifacts/`.
- `./scripts/test.ps1 -NoBuild`: essential checks, fake-process supervision tests,
  isolated embedded-font check and mathematical layout audit.
- `./scripts/check-repository.ps1`: limited publication hygiene and font-hash checks.
- `dotnet format NinjaTraderLogin.sln --no-restore`: apply the
  configured code format. Add `--verify-no-changes` to verify it.
- `./scripts/package.ps1 -NoBuild`: create a versioned local ZIP and SHA-256 file.
- `./scripts/test-packaging.ps1`: exercise packaging in isolated fixtures, including
  configuration/path/hash mismatches, private content and preservation of an existing archive.
- `./scripts/test-portability.ps1`: create, move, inspect and remove shortcuts through
  the Unicode Shell API in workspace fixtures; the Desktop and registry are untouched.

Build never changes a deployed copy or overwrites its acceptance records. Existing
runtime data stays local. In this checkout, `.local/` is ignored private maintenance
space; it is not an input to a clean clone or CI. No local SDK, migration backup or
historical test journal belongs in publication history.

Do not normalize upstream font/license bytes. Follow `.editorconfig` for authored source.
The repository preserves file bytes; deterministic compilation and source path mapping
avoid recording the checkout's machine-specific source directory in debug paths.
