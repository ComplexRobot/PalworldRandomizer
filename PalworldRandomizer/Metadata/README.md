# Embedded settings catalog

`Serializer.SettingsCatalog.LoadEmbedded()` loads the two compiled YAML resources with SharpYaml 3.16.0
source-generated type metadata. It has no WPF, file-dialog or runtime-settings dependency. It returns:

- `Definitions`: 137 ordered `Models.SettingDefinition` objects with original names, value kinds and cold defaults.
- `English`: 457 strings addressed by stable label, description, option, category and control-label keys.
- `RadioGroups`: the three original exclusive-selection groups and their cold-default member.

```csharp
var catalog = SettingsCatalog.LoadEmbedded();
var setting = catalog.Definitions.Single(x => x.Name == "RandomLevelMax");
string label = catalog.English[setting.LabelKey];
object coldDefault = setting.DefaultValue; // System.Int32: 65, not the invalid-input fallback of 80.
```

Definitions and nested collections are detached read-only snapshots. Defaults retain the original CLR scalar
kinds; array defaults are independent `IReadOnlyList<int>` values, not writable `int[]` aliases. Loading does not
read or mutate a caller's `FormData`, repair conflicting imported radio booleans, normalize values, or apply defaults
to an existing model. The original model and JSON template contracts are unchanged.

## Resources and provenance

- `RandomizerSettings.yaml` contains identities, typed default representations, relationships, localization keys
  and the original 147 controls' source evidence. No localized display prose belongs in that file.
- `Locales/en-US.Randomizer.yaml` contains English display text, separate from the existing UI JSON resources.
- `DescriptionOrigin`, `DescriptionReferences` and `OriginalDescriptionStatus` distinguish source quotations from
  implementation-authored explanations. The original status counts remain 67 verified, 44 shared-range,
  20 missing and six ambiguous; adding an explanation does not relabel an original source gap as verified.
- The 111 verified/shared setting descriptions retain exact audited reference wording. Additional source
  paragraphs are retained where relevant. There are 87 distinct `reference.*` paragraphs because multiple settings
  share text. Paragraph IDs refer to the frozen description audit, not live line numbers.
- The 26 missing/ambiguous primary explanations are implementation-authored and code-referenced. The undocumented
  Rarity Minimum/Maximum option explanations are also implementation-authored, not reference quotations.

The original source contract is revision `72a836172e7778b5d2c003c81418172c57126e24`. The review evidence is under
`milestone-evidence/007-settings-metadata/` and the frozen source/provenance audit is under
`research/007-settings-metadata/` in the parent project workspace. Detailed source citations remain in that audit,
not in application identifiers or resources. The retained description text matches its reviewed SHA-256.
Source text is retained for the identified owner's requested implementation; this is not a separate license grant.

`Tests/Fixtures/FormData.original.json` remains the canonical original model/default/mode fixture and is unchanged.
`Tests/Fixtures/SettingsMetadata.original.json` is an additional source-audit-derived fixture for original labels,
paragraph hashes, categories, relationships and control evidence. It does not replace the canonical fixture and
was not obtained from an original executable. Runtime checks use the actual built application loader and its
embedded resources, not a second test parser.

## Deliberately not executable UI metadata

`SourceNumericConstraint`, `SourceFocusEffect` and `SourceVisibility*` preserve original C#/WPF evidence verbatim.
For example, `Binding`, regex literals, normalizer calls, source control names and source locations are provenance,
not a new cross-platform binding/validation language. Cold defaults and invalid-input fallbacks are separate facts.
Hidden legacy flags remain serialized; active hidden controls are not assumed inert. Human/boss subcounts remain
subsets within their totals, and the unchecked level cap is 80 rather than unlimited.

This slice does **not** render controls, evaluate visibility/triggers, wire focus effects, enforce numeric input,
change randomization, select locale at runtime, or add an editable YAML/template parser. Embedded resources are
trusted shipped inputs; there is no public arbitrary-YAML loading API or user-input validation contract. The
source-audit script rejects duplicate source keys, but this is not a promise of runtime editable-document validation.
A future generic renderer must first translate the source-only constraints into a reviewed portable behavioral
contract, including trigger precedence and conditional relationships. Treating retained WPF binding text as already
renderer-ready would be incorrect. Schema redundancy/provenance tradeoffs are review topics, not a redesign here.

## Verification

From the worktree root with .NET 10:

```sh
dotnet build PalworldRandomizer.slnx -c Debug -p:EnableWindowsTargeting=true
dotnet run --project PalworldRandomizer/Tests/Regression.csproj -c Debug -p:TreatWarningsAsErrors=true
dotnet run --project PalworldRandomizer/Tests/Shell.csproj -c Debug -p:TreatWarningsAsErrors=true
dotnet run --project PalworldRandomizer/Tests/Metadata.csproj -c Debug -p:TreatWarningsAsErrors=true
```

Repeat with `-c Release`, building the application before its compiled-assembly runners. Outputs are isolated by
runner and configuration. Debug and Release pass 31 regression, 32 shell and 13 metadata checks each. The metadata
checks include all original types/defaults, exact source text, nested localization, original controls, radio defaults,
read-only nested collections, independent array defaults and caller nonmutation. Earlier vertical RED/GREEN logs
are retained in the milestone evidence; recovery added characterization coverage, not new production behavior.

Formatting checks must actually load the root `.editorconfig`:

```sh
Configuration=Debug EnableWindowsTargeting=true dotnet format whitespace PalworldRandomizer.slnx --verify-no-changes
Configuration=Debug EnableWindowsTargeting=true dotnet format whitespace PalworldRandomizer/Tests/Metadata.csproj --verify-no-changes
git -c core.whitespace=trailing-space,space-before-tab,cr-at-eol diff --check
```

The full builds retain the baseline's IL trimming/AOT and NuGet vulnerability diagnostics (94 reported warning
occurrences per build); no suppressions or compatibility flags were added. There are no new compiler,
documentation, code-style or code-quality diagnostics. The metadata runner treats warnings as errors.
These Linux runs prove cross-compilation and execution of the production catalog seam, not Windows GUI behavior,
nonempty gameplay generation, Avalonia/browser integration, persistent editing, native codecs, trimming or AOT.
