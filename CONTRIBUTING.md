# Contributing

The original code is licensed under MIT. Contributions are accepted under the same license.
Discuss larger changes in an issue before submitting code, and keep pull requests
focused on an agreed behavior or maintenance concern.

For an agreed change:

1. Keep it limited to one concrete behavior or maintenance concern.
2. Build with `./build.ps1` and run `./scripts/test.ps1 -NoBuild` on Windows.
3. Follow `.editorconfig`; verify formatting with the command in the README.
4. Keep generated output, credentials and local diagnostic data outside Git.
5. Preserve meaningful regression fixtures; replace captured machine metadata with the
   minimum anonymized state needed to reproduce the behavior.
6. Describe whether validation is a model calculation, headless process test, or actual
   Human observation in NinjaTrader. Do not promote Simulation results to Live acceptance.

Executable NinjaTrader changes require the maintainer's Native/Human validation before
being represented as accepted. CI drives a fake target only; it does not log in to
NinjaTrader or require an account. To support another NinjaTrader version, test it and add
it to `NinjaTraderCompatibility`.
