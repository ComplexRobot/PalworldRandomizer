# Regression checks

This small console runner exercises the real application model types without opening a WPF window or loading
Palworld game data. It uses the application's existing Newtonsoft.Json dependency; no test framework is required.

Run from the repository root with .NET 10:

```sh
dotnet build PalworldRandomizer.slnx -c Debug -p:EnableWindowsTargeting=true
dotnet run --project PalworldRandomizer/Tests/Regression.csproj -c Debug -p:TreatWarningsAsErrors=true
```

Repeat both commands with `-c Release` to check the Release assembly. Build the application first in the same
configuration: the runner deliberately references that compiled assembly rather than inheriting the Windows
Desktop framework requirement through a project reference. Its output and intermediates are isolated under
`.build/Tests/<Configuration>/`.

## Coverage

- The eight persisted configuration members use properties rather than public fields.
- Default JSON and all persisted setting names remain compatible.
- Existing JSON files round-trip without losing values; omitted members retain their defaults.
- Spawn groups and areas make independent deep copies, preserving their metadata.
- The observable editor view stays synchronized after insertion, removal, resizing and clearing.

The configuration-property check was observed failing against the original assembly, then passing after the fix.
All seven checks pass in Debug and Release. These are model regression checks, not GUI tests, archive-parsing tests,
benchmarks or proof of trimming/AOT compatibility. The Windows/WPF GUI cannot run natively on the Linux test host.

## Formatting checks

```sh
dotnet format whitespace PalworldRandomizer.slnx --verify-no-changes --no-restore
dotnet format whitespace PalworldRandomizer/Tests/Regression.csproj --verify-no-changes --no-restore
git -c core.whitespace=trailing-space,space-before-tab,cr-at-eol diff --check
```

The application build runs the existing `.editorconfig` analyzers with `EnforceCodeStyleInBuild` still enabled.
The Git whitespace setting above recognizes the CRLF line endings required by that configuration.

See [WarningAssessment.md](WarningAssessment.md) for the remaining diagnostics and dependency decision.
