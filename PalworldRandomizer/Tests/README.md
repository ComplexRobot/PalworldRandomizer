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
- Repeated edits signal invalidation without requiring a WPF page.
- Actual PalSchema archives use explicit settings and no native dialog.
- Backup capture stays deferred until dirty/enabled, with capture and I/O failures returned to the caller.
- Generation returns areas and optional log text, with ordered capture, progress hiding and publication.
- Restore selects the newest valid backup/log and preserves prior areas after a failed conversion.
- The production save adapter captures settings only after confirmation, never on cancellation.

The configuration-property check was observed failing against the original assembly, then passing after the fix.
The regression runner now passes 31 checks in Debug and Release, retaining its original 25 model/JSON checks.
New engine-boundary behaviors were observed failing before implementation, including deferred backup capture.
Decimal-population coverage prevented a precision regression; independent review caught an ignored-future-number
case, now tested against the same production loader.

These are model/JSON, file-I/O and boundary checks, not GUI tests, archive-parsing benchmarks or trimming/AOT proof.
The committed generation check runs the actual compiled engine against empty startup tables; it verifies transport
and ordering, not nonempty gameplay generation. The Windows/WPF GUI cannot run natively on the Linux host.
Capture/restore, calculation tokens, dialogs, conversions and desktop adapter ordering are additionally source-audited.
UAssetData startup, native codecs and platform path behavior remain separate portability work.

See [Fixtures/README.md](Fixtures/README.md) for the original settings fixture's provenance. The export check
references the application's already-built CUE4Parse dependency because Newtonsoft inspects GameStruct metadata.
The generation check also references the already-built Stfu dependency. Neither reference loads archives or adds
an application package.

## Embedded settings catalog

Build the application in the same configuration, then run:

```sh
dotnet run --project PalworldRandomizer/Tests/Metadata.csproj -c Debug -p:TreatWarningsAsErrors=true
```

Repeat in Release. Its 12 checks load the actual production assembly and embedded YAML through
`SettingsCatalog.LoadEmbedded()`: 137 original setting contracts, exact Nexus text and implementation provenance,
147 controls, three radio groups, all 457 English keys, read-only nested collections and independent defaults.
The additional metadata fixture does not replace `Fixtures/FormData.original.json`. No UI bindings, validators,
editable YAML templates or persistence are executed by this runner. See [the catalog notes](../Metadata/README.md)
for the API, provenance and scope. Outputs are isolated under `.build/Tests/Metadata/<Configuration>/`.

## Formatting checks

```sh
dotnet format whitespace PalworldRandomizer.slnx --verify-no-changes --no-restore
dotnet format whitespace PalworldRandomizer/Tests/Regression.csproj --verify-no-changes --no-restore
git -c core.whitespace=trailing-space,space-before-tab,cr-at-eol diff --check
```

The application build runs the existing `.editorconfig` analyzers with `EnforceCodeStyleInBuild` still enabled.
The Git whitespace setting above recognizes the CRLF line endings required by that configuration.

See [WarningAssessment.md](WarningAssessment.md) for the remaining diagnostics and dependency decision.
