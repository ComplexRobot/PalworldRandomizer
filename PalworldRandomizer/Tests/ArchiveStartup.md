# Explicit archive startup

`UAssetData.OpenArchiveAsync(string archivePath, string mappingPath, string previousGameVersion = "0.0.0.0")`
opens only the selected PAK and USMAP, read-only. It asynchronously mounts the PAK and reads
`Pal/Config/DefaultGame.ini`. The returned `VfsFileProvider.GameVersion` and `GameVersionUpdated` describe that
opening; the operation does not mutate the legacy static configuration, initialize Windows DLLs, prepare bundled
resources, encode PNGs, or persist settings.

```csharp
await using VfsFileProvider provider = await UAssetData.OpenArchiveAsync(pakPath, usmapPath, savedVersion);
GameStruct table = new(provider, "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
// Consume the mapped assets before leaving the provider's ownership scope.
```

## Ownership and errors

- The caller owns the successful result. Keep public asset requests serialized and await `DisposeAsync` after the
  last use. Internally scheduled lazy texture reads use positional I/O, as the original `RegisterVfs(string)` did;
  they must not share a seek/read cursor with subsequent asset loads.
- Failure during mapping, registration, mounting, or version reading releases all opened archive handles.
- `DisposeAsync` waits for scheduled image saves, then calls the base provider's `Dispose` in `finally`, including
  when an image save fails. Synchronous `Dispose` alone does not drain scheduled image saves.
- `WaitForTextureSavesAsync()` drains scheduled image saves without closing the provider. Legacy Windows startup
  uses it before returning the provider for `Data.Initialize`, preserving the old initial-weapon-icon readiness.
- CUE4Parse's public registration/mount convenience methods catch errors. A small private `PakFileReader` subclass
  records the original mount exception and rethrows it after the real `MountAsync` finishes. Registration is owned
  explicitly, including constructor failures. Encrypted archives that cannot mount fail explicitly.
- Metadata, asset and texture loads use the throwing library APIs so parser/codec failures are not silently treated
  as missing assets. Cached PNGs still skip decoding; failed synchronous texture loads are not marked as saved.
- No stream/browser-worker API, new package, codec workaround, or whole-PAK copy was introduced.

## Filesystem paths versus Unreal identifiers

The default root is `Path.Combine(LocalApplicationData, "Palworld-Randomizer")`, available before startup.
`ConfigureDataRoot(root)` selects an explicit process-wide filesystem root for legacy extraction/persistence;
`ConfigureDataRoot()` restores the default. Configure it before loading data. Merely constructing paths does not
create directories. Tests use isolated explicit roots, never the user's real application-data directory.

Filesystem helpers accept null/empty paths and legacy relative backslashes, using `Path.Combine` and the native
separator. Rooted inputs follow `Path.Combine` semantics. Windows output locations remain the same; the root no
longer relies on a trailing backslash. These helpers must not be used to normalize CUE asset identifiers such as
`Pal/Content/...`, which retain their forward-slash spelling.

## Legacy Windows adapter

`App` retains installation-textbox updates, missing-installation setup, configuration reads/saves, asset-version
upgrade policy, bundled resource extraction/replacement, the original icon regex and icon-directory preparation,
locked weapon-icon extraction, and existing dispatcher/error/restore/editor/exit actions. Bundled preparation is a
separate explicit step before opening the bundled mapping; it is never called by the read-only opener and cannot
replace its explicitly selected mapping. Oodle/Zlib Windows DLL initialization runs only under `IsWindows()`.

The original `Initialize` used `await using` before returning a provider whose `DisposeAsync` merely drained images
and leaked the archive reader. Correcting disposal without removing that early ownership scope would instead
return a closed provider. Both ends of this contract are covered by real-handle lifetime checks.

## Windows startup completion

`App` schedules its existing UI-affine loading callback with the typed WPF `InvokeAsync(Func<Task>)` API.
`StartupCompletion.Completion` unwraps that operation's `Task<Task>`: completion includes mounting, data and
randomizer initialization, post-mount/backup restoration/editor setup, and the entire `await using` disposal,
including every queued Pal/NPC/weapon image save. The window still appears without synchronously waiting.

`MainPage` asynchronously awaits the production gate before its first game-data/RNG/image access or control
initialization. The existing version text, mode selections, config defaults and template restore sequence remain
unchanged on the UI context. Only successful initialization reveals the form/image and marks `IsReady` true.
Failure leaves both hidden, displays `Initialization failed.`, and forwards the original exception once using the
existing dispatcher `ExceptionDispatchInfo.Throw` route and unchanged global handler. The completion task stays
faulted; there is no second App-level error forward. Exit skips backups/config/template capture until `IsReady`.

The archive runner also runs six startup regressions against the actual application `StartupCompletion` and
`VfsFileProvider` types, not a copied dispatcher implementation. Separate pending-open and pending-image-save
checks hold the consumer hidden; image checks exercise the real provider's save-task drain/async disposal.
Open/save failures retain the original fault and report once; UI restoration failure and cached synchronous
startup are covered too. Run only this portable gate suite without game files:

```sh
/root/.dotnet/dotnet run --project PalworldRandomizer/Tests/Archive.csproj -c Debug -- --startup-only
```

These are task/lifetime and consumer-gate tests, not Windows dispatcher/GUI execution. Real Windows scheduling,
image-inclusive native startup and UI affinity still require Windows runtime validation; Linux source audits
verify unchanged UI payloads and the absence of context escapes/blocking waits, not GUI behavior.

## Running the checks on Linux

Build the legacy application first, then run the independent console runner without the Windows Desktop runtime:

```sh
/root/.dotnet/dotnet build PalworldRandomizer.slnx -c Debug -p:EnableWindowsTargeting=true
/root/.dotnet/dotnet run --project PalworldRandomizer/Tests/Archive.csproj -c Debug
/root/.dotnet/dotnet run --project PalworldRandomizer/Tests/Regression.csproj -c Debug
/root/.dotnet/dotnet run --project PalworldRandomizer/Tests/Shell.csproj -c Debug
```

Repeat with `-c Release`. `Archive` has explicit compile items; it is excluded from the other entry points.
Its twelve checks require Linux `/proc/self/fd` and the supplied real test archive. Defaults are
`/root/project-data/Pal-Windows.pak` and `/root/project-data/Mappings.usmap`; override with `PALWORLD_TEST_PAK` and
`PALWORLD_TEST_USMAP`. The mounted-entry assertion intentionally pins this supplied archive (185,141 entries).
The ordinary model/shell runners do not require these game files. Existing 31 model-boundary and 32 shell checks
remain separate.

The real input was opened at 39,254,065,482 bytes with a 2,706,032-byte USMAP. Version reading returned
`1.0.5.102999` from the 41,219-byte INI. The mapped character table contained 753 rows and loaded through both
`GameStruct` and the existing `CharacterData` projection without requiring an image encoder. Input sizes,
modification times, USMAP bytes and live/closed handle counts are asserted.

## Explicit remaining gates

This is an archive/path boundary, not a converted desktop/browser host. The application still targets WPF.
Windows GUI/resource execution, persistence architecture and WASM-worker integration are not proven here.

A differential Linux probe established a read-transport regression in the first explicit opener: `FStreamArchive`
used a shared cursor while `SaveTexturePng` decoded lazy bulk data in background tasks. Serialized decoding passed
640 loads; scheduled decoding produced invalid magic and an Oodle decompression error. The original registration
path and the checked reader with `FRandomAccessFileStreamArchive` both passed 640 loads/decodes. The opener now
uses that original positional-I/O transport with an explicitly read-only owned handle, not per-extraction clones.
The regression check first failed deterministically on shared-cursor mutation, then passed that assertion plus 640
actual texture loads/decodes, with one live PAK handle and none after disposal. No decoding was moved to the UI
thread. The old claim that the spawner table itself had invalid magic was incorrect: its extracted header is valid.

The post-fix probe completed `Data.Initialize` and reached its subsequent image-save wait without invalid magic.
A representative `PF_DXT5` texture decoded into 128 × 128 / 65,536 raw pixel bytes. PNG encoding and scheduled icon
saves still fail because `libSkiaSharp` is absent; failed image-save disposal releases all PAK handles. Complete
startup including images is therefore not claimed. The before/after probes are retained outside the repository in
milestone evidence. No vendor patch, new package, suppression or fallback hides the remaining codec gate.
Existing IL/AOT/trimming diagnostics and NuGet advisories remain unsuppressed; neither ordinary builds nor these
native Linux tests prove WASM or AOT publishing.
