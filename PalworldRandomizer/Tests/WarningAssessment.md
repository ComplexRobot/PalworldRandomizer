# Warning reassessment

## Verified cleanup

Both Debug and Release builds succeed with zero errors and no remaining C# compiler, documentation, code-style
or code-quality warnings. All seven model regression checks pass in both configurations with warnings treated
as errors in the runner. Whitespace checks pass, and handwritten C# lines are no longer than 120 characters.

The source audit also confirms preservation of all 137 `FormData` setting names/types, all eight configuration
member names/types/defaults, and all 22 generated-regex patterns. The existing randomization algorithms and WPF
interface are retained. No new production warning suppressions were added.

## Remaining IL diagnostics — deliberately unchanged

The unchanged Debug baseline and the cleaned Debug build have the same diagnostic identities and messages
apart from source line positions. Debug and Release each have these distinct diagnostic locations:

| Code | Locations | Cause |
| --- | ---: | --- |
| IL2026 | 16 | Reflection-dependent Newtonsoft.Json calls and `DataTable.Compute` are not guaranteed trim-safe. |
| IL3050 | 15 | Newtonsoft.Json paths may require runtime-generated code unavailable under native AOT. |
| IL2075 | 5 | Template restoration and WPF-generated code reflect over types without preservation guarantees. |
| IL2067 | 1 | WPF-generated constructor activation lacks the required preservation annotation. |

These are 37 distinct IL diagnostics, not 37 demonstrated runtime failures. WPF compiles source twice, so the
build summary repeats diagnostics. The remaining NuGet warnings are also repeated during restore and build.

`Directory.Build.props` still declares `IsAotCompatible` and `IsTrimmable`; neither flag was disabled. Ordinary
successful builds do not establish that trimming or AOT publishing is safe. Single-file publishing is a separate
feature and does not by itself require trimming or native AOT.

Recommended follow-up: reassess the final Avalonia desktop/browser publishing requirements before changing
these guarantees. The WPF-generated sites will disappear with the WPF migration; the reflection-based template
restoration and layout-expression converter are also scheduled for replacement. Serialization needs a deliberate
trim/AOT-compatible design and compatibility tests, rather than blanket suppression.

## Remaining dependency warnings — ImageSharp

The Microsoft.Bcl.Memory advisory is resolved by pinning the first patched release on the existing 9.x line,
`9.0.14`. The application and regression runner now share centrally managed package versions.

NuGet still reports six advisories for the transitive `SixLabors.ImageSharp 3.1.12` dependency: four high-severity
and two moderate-severity advisories. `CUE4Parse-Conversion 1.2.2.202607` pins that exact version.

A trial override to the patched `ImageSharp 3.2.0` was reverted because it introduced:

- NU1608: the new version violates CUE4Parse-Conversion's exact dependency constraint.
- A Six Labors licensing check: Debug can continue without a key, but Release requires a valid license.

The current code does not contain that override, a license-check bypass, or a new vulnerability suppression.
Choosing a compatible dependency upgrade and obtaining an appropriate license, or replacing the conversion
path, is a separate development/licensing decision. The original specification permits NuGet warnings, but
these advisories remain real and should not be mistaken for resolved vulnerabilities.

Reproduce the dependency assessment from the repository root:

```sh
dotnet list PalworldRandomizer/PalworldRandomizer.csproj package --vulnerable --include-transitive
```

Advisories:

- https://github.com/advisories/GHSA-v76p-62qx-wwq2
- https://github.com/advisories/GHSA-gwg2-r3hj-4w44
- https://github.com/advisories/GHSA-j3p4-wp97-rph4
- https://github.com/advisories/GHSA-j9gm-c75j-xc9q
- https://github.com/advisories/GHSA-wmxv-xphr-5c9g
- https://github.com/advisories/GHSA-jjfr-hcj7-qf5w

## Verification limits

The current Windows/WPF GUI cannot run natively on this Linux host. These results cover cross-compilation and
real execution of the model regression runner, not interactive Windows UI tests, browser tests or game-data
compatibility. No visual changes or new application screenshots are claimed for this maintenance pass.
