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
`.build/Tests/Regression/<Configuration>/`.

The UI-independent shell runner links the real view-model sources and can run without building or starting WPF:

```sh
dotnet run --project PalworldRandomizer/Tests/Shell.csproj -c Release -p:TreatWarningsAsErrors=true
```

Its 32 checks cover independent histories, Settings-tab reuse, the 30-second close/Undo deadline, last-tab recovery,
reordering, overflow promotion, binding notifications, detached snapshots and persisted-state recovery. Outputs
are isolated under `.build/Tests/Shell/<Configuration>/`. These checks do not exercise rendered layout, dragging,
docking, translation resources, timers or persistence file I/O; those are separate integration gates.

## Coverage

- The eight persisted configuration members use properties rather than public fields.
- The UI-independent `Models.FormData` and `Models.ConfigData` construct without `MainPage.Instance`.
- All 137 form property names, CLR types and startup defaults match the pinned original control fixture.
- The eight configuration defaults and all 19 persisted mode labels remain compatible.
- Every nondefault setting round-trips; omitted members retain defaults in standalone DTO deserialization.
- The production template loader retains captured UI values, replaces arrays and preserves decimal precision.
- Unknown numeric members outside decimal range are ignored, including nested values; null templates do not capture controls.
- Explicit zero, false, fractional and null JSON values survive; default arrays are independent.
- Egg timer arithmetic is unchanged; actual PalSchema JSON export uses an explicitly supplied timer.
- Generation's public boundary accepts the portable form model.
- Spawn groups and areas make independent deep copies, preserving their metadata.
- The observable editor view stays synchronized after insertion, removal, resizing and clearing.

The configuration-property check was observed failing against the original assembly, then passing after the fix.
All 25 checks pass in Debug and Release. The model-construction, configuration-extraction and explicit-export
checks were observed failing before their implementations. Decimal-population coverage prevented a precision
regression; independent review caught an ignored-future-number case, now tested against the same production loader.
These are model/JSON regression checks, not GUI tests, archive-parsing
tests, benchmarks or proof of trimming/AOT compatibility. The Windows/WPF GUI cannot run natively on the Linux
host. Capture/restore mappings and unchanged calculations are additionally checked by the milestone source audit.

See [Fixtures/README.md](Fixtures/README.md) for the original settings fixture's provenance. The export check
references the application's already-built CUE4Parse dependency because Newtonsoft inspects `GameStruct`
constructor metadata; it does not load game archives or introduce a new application package.

## Formatting checks

```sh
dotnet format whitespace PalworldRandomizer.slnx --verify-no-changes --no-restore
dotnet format whitespace PalworldRandomizer/Tests/Regression.csproj --verify-no-changes --no-restore
git -c core.whitespace=trailing-space,space-before-tab,cr-at-eol diff --check
```

The application build runs the existing `.editorconfig` analyzers with `EnforceCodeStyleInBuild` still enabled.
The Git whitespace setting above recognizes the CRLF line endings required by that configuration.

See [WarningAssessment.md](WarningAssessment.md) for the remaining diagnostics and dependency decision.
