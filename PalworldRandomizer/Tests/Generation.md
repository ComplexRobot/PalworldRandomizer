# Real game-data generation boundary

`PalworldRandomizer.Randomizer.PalSpawnGenerationService.GenerateAsync` connects explicitly selected game files to
the original archive, character/spawn data, randomization and PalSchema writer. It is host-neutral: it returns
`Task<GenerationResult>`, performs all engine work on a worker, and never references a window or dispatcher.

```csharp
GenerationResult result = await PalSpawnGenerationService.GenerateAsync(
    new() {
        PakPath = selectedPak,
        MappingPath = selectedUsmap,
        DataRoot = isolatedDataRoot,
        OutputZipPath = uniqueOutputZip,
        Settings = capturedFormData,
    },
    progress // optional; raised on the generation worker
);
```

The first call opens the selected PAK read-only, reads the real game version, initializes `Data` and `Randomize`, and
writes the ZIP. Later calls with the same PAK/USMAP reuse the process-wide tables and only regenerate from the new
settings. A different selection is rejected with an explicit restart requirement because the original engine has no
reset boundary. A failed data initialization similarly requires restart, preventing reuse of partially populated
static tables. One `SemaphoreSlim` covers archive ownership, initialization, generation and export, so concurrent
callers cannot interleave the original static engine. The service snapshots all settings before generation, since the
original algorithm normalizes its working rarity-weight array; it does not change the caller's saved template.

The output is written to a unique sibling file and moved into place only after the ZIP is complete and summarized.
Existing output paths are refused, and the final move does not overwrite a file created during generation. This also
protects selected inputs when paths are aliases. The service does not change the process-wide application-data root.
Selected PAK/USMAP files are never written or copied.

## Icon-codec seam

Running the real flow first exposed the concrete blocker: `Data.Initialize` populated the real tables and generation
completed, but `VfsFileProvider.DisposeAsync` awaited queued icon saves and failed with
`DllNotFoundException: libSkiaSharp` in `CUE4Parse_Conversion.Textures.TextureEncoder.Encode`. The PNG codec is only
needed by optional UI icons.

`Data.Initialize(fileProvider, extractIcons: false)` skips table/icon lookups and texture decoding while loading every
character table and spawn asset. `PalIcon` stays empty; no palette, spawn or schema value is synthesized or skipped.
The existing one-argument call still extracts icons exactly as before.

## Observed real-data proof

Linux x64 run against `/root/project-data/Pal-Windows.pak` (39,254,065,482 bytes) and
`/root/project-data/Mappings.usmap`:

- Game version `1.0.5.102999`; 185,141 mounted entries.
- 288 eligible pals, 433 vanilla areas, 433 generated areas, 432 changed areas.
- 5 PalSchema documents, 111,007,467 uncompressed bytes, 3,951,936-byte ZIP.
- `blueprints/PalSpawns.json`: 371 areas, 216,324 spawn groups, 461,708 character entries; all keys resolve to real
  `Data.PalData` rows.
- 14 egg areas with nonempty lotteries.
- Isolated data root retained 0 bytes; the PAK length/mtime and USMAP bytes were unchanged.
- Two concurrent calls with seeds 111 and 222 both completed and produced different nonempty spawn schemas.

Evidence ZIPs and `summary.json` remain under
`.build/Tests/Generation/Debug/evidence-*` and `evidence-concurrent-*` for review.

## Running only this gate

Build the engine/test project, not the Desktop or Browser hosts:

```sh
/root/.dotnet/dotnet build PalworldRandomizer/Tests/Generation.csproj -c Debug -p:EnableWindowsTargeting=true
/root/.dotnet/dotnet run --project PalworldRandomizer/Tests/Generation.csproj -c Debug \
    -p:EnableWindowsTargeting=true --no-build
```

Override inputs with `PALWORLD_TEST_PAK` and `PALWORLD_TEST_USMAP`. Exit code is nonzero on any failed check. The run
needs roughly 45 seconds for the first initialization and 20-30 seconds for each later generation, plus bounded
temporary disk for the ZIP. It does not build the UI hosts and does not materialize the 37 GiB PAK.

## Scope

This proves the data-to-Palaschema engine seam and its asynchronous/serialized boundary. It does not claim desktop
or browser UI integration, icon rendering, localization, recovery, Firefox support or publication.
