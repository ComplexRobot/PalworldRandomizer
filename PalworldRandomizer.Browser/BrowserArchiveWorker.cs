using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Text;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Readers;
using IniParser.Parser;
using Newtonsoft.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Serializer;
using SpawnDev.SpawnJS;
using BrowserFile = SpawnDev.SpawnJS.JSObjects.File;

namespace PalworldRandomizer.Browser;

/// <summary>The actual platform RPC boundary; only a small summary and the finished ZIP cross back to the UI.</summary>
public interface IBrowserArchiveWorker {
    /// <summary>Checks the selected browser File references without copying the PAK.</summary>
    Task<string> ValidateAsync(BrowserFile pak, BrowserFile mapping, string previousVersion);

    /// <summary>Runs real data loading and PalSchema generation over bounded browser File reads.</summary>
    /// <param name="pak">The selected PAK File; only bounded slices are requested.</param>
    /// <param name="mapping">The selected USMAP File; its metadata bound is enforced before parsing.</param>
    /// <param name="previousVersion">The cached game version used only for cache invalidation.</param>
    /// <param name="settingsJson">The serialized <see cref="FormData"/> snapshot for this run.</param>
    /// <param name="cancellationToken">Cancels waiting for the generation gate or the worker result.</param>
    /// <returns>A small summary and the complete generated ZIP bytes, or a stage-keyed failure.</returns>
    Task<BrowserGenerationReport> GenerateAsync(BrowserFile pak, BrowserFile mapping, string previousVersion,
        string settingsJson, CancellationToken cancellationToken = default);
}

/// <summary>Runs synchronous CUE reads only inside the approved package's dedicated worker.</summary>
/// <remarks>
/// The original engine keeps process-wide tables with no reset boundary. Generation calls are therefore serialized
/// and the first mounted selection is pinned: a later call that presents different files fails with a reselect key
/// instead of mixing tables from two archives. Compatibility checks may run before generation because they only read.
/// </remarks>
public sealed class BrowserArchiveWorker : IBrowserArchiveWorker {
    private static readonly SemaphoreSlim GenerationGate = new(1, 1);
    private static string? _loadedPak;
    private static long _loadedPakLength;
    private static double _loadedPakLastModified;
    private static long _loadedMappingLength;
    private static double _loadedMappingLastModified;
    private static bool _initializationFailed;

    /// <summary>Owns the provider and both source streams until all serial compatibility work finishes.</summary>
    public async Task<string> ValidateAsync(BrowserFile pak, BrowserFile mapping, string previousVersion) {
        if (!SpawnJSRuntime.Instance.IsDedicatedWorkerGlobalScope) {
            throw new InvalidOperationException("Compatibility requires a dedicated browser worker.");
        }

        using var pakStream = new BrowserFileStream(pak);
        using var mappingStream = new BrowserFileStream(mapping);
        ArchiveCompatibility? summary = null;
        string? errorKey = null;
        string? errorDetail = null;
        string? detectedGameVersion = null;
        string stage = "setup.mappingIncompatible";
        try {
            if (mappingStream.Length is <= 0 or > 16 * 1024 * 1024) {
                throw new InvalidDataException("USMAP size exceeds the supported metadata bound.");
            }

            byte[] bytes = new byte[checked((int)mappingStream.Length)];
            mappingStream.ReadExactly(bytes);
            await using var provider = new BrowserProvider();
            provider.MappingsContainer = new SelectedMappings(bytes);
            stage = "setup.openFailed";
            using var archive = new FStreamArchive(pak.Name, (Stream)pakStream.Clone(), provider.Versions);
            await provider.OpenAsync(archive);
            string version = previousVersion;
            if (provider.Files.ContainsKey("Pal/Config/DefaultGame.ini")) {
                var ini = new IniDataParser(new() { AllowDuplicateKeys = true }).Parse(
                    Encoding.ASCII.GetString(provider.SaveAsset("Pal/Config/DefaultGame.ini")));
                version = ini["/Script/EngineSettings.GeneralProjectSettings"]["ProjectVersion"];
                detectedGameVersion = version;
            }

            stage = "setup.mappingIncompatible";
            var rows = provider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
            foreach (var row in rows.Values) {
                _ = new CharacterData(row.Properties);
            }

            summary = new(provider.Files.Count, rows.Count, version);
        } catch (Exception exception) {
            errorKey = stage;
            errorDetail = exception.Message;
        } finally {
            pakStream.Dispose();
            mappingStream.Dispose();
        }

        return JsonConvert.SerializeObject(new {
            Summary = summary,
            ErrorKey = errorKey,
            ErrorDetail = errorDetail,
            DetectedGameVersion = detectedGameVersion,
            Pak = pakStream.Stats,
            Mapping = mappingStream.Stats,
        });
    }

    /// <summary>Loads real tables and generates a PalSchema ZIP from bounded browser File reads.</summary>
    /// <remarks>
    /// The PAK is never copied or uploaded. CUE asks for ranges; each range is served from a shared 256 KiB
    /// <c>FileReaderSync</c> cache and every source request stays at or below that block size. The full
    /// PalSchema document set never crosses the worker boundary: only the compressed ZIP and small counts do.
    /// The worker instance is disposable because the original engine keeps process-wide tables with no reset.
    /// </remarks>
    public async Task<BrowserGenerationReport> GenerateAsync(BrowserFile pak, BrowserFile mapping,
        string previousVersion, string settingsJson, CancellationToken cancellationToken = default) {
        if (!SpawnJSRuntime.Instance.IsDedicatedWorkerGlobalScope) {
            throw new InvalidOperationException("Generation requires a dedicated browser worker.");
        }

        BrowserGenerationReport report = new();
        var clock = Stopwatch.StartNew();
        var pakStream = (BrowserFileStream?)null;
        var mappingStream = (BrowserFileStream?)null;
        string stage = "generation.mappingIncompatible";
        await GenerationGate.WaitAsync(cancellationToken);
        try {
            if (_initializationFailed) {
                throw new InvalidOperationException(
                    "Game data initialization failed; reload the page before retrying.");
            }

            pakStream = new BrowserFileStream(pak);
            mappingStream = new BrowserFileStream(mapping);
            if (mappingStream.Length is <= 0 or > 16 * 1024 * 1024) {
                throw new InvalidDataException("USMAP size exceeds the supported metadata bound.");
            }

            stage = "generation.reselect";
            if (_loadedPak is not null
                && (!string.Equals(_loadedPak, pak.Name, StringComparison.Ordinal)
                    || _loadedPakLength != pak.Size
                    || _loadedPakLastModified != pak.LastModified
                    || _loadedMappingLength != mappingStream.Length
                    || _loadedMappingLastModified != mapping.LastModified)) {
                throw new InvalidOperationException(
                    "A different PAK or USMAP was selected; reload the page to reset the game-data tables.");
            }

            byte[] mappingBytes = new byte[checked((int)mappingStream.Length)];
            mappingStream.ReadExactly(mappingBytes);
            await using var provider = new BrowserProvider();
            provider.MappingsContainer = new SelectedMappings(mappingBytes);
            stage = "generation.openFailed";
            using var archive = new FStreamArchive(pak.Name, (Stream)pakStream.Clone(), provider.Versions);
            await provider.OpenAsync(archive);
            string version = previousVersion;
            if (provider.Files.ContainsKey("Pal/Config/DefaultGame.ini")) {
                var ini = new IniDataParser(new() { AllowDuplicateKeys = true }).Parse(
                    Encoding.ASCII.GetString(provider.SaveAsset("Pal/Config/DefaultGame.ini")));
                version = ini["/Script/EngineSettings.GeneralProjectSettings"]["ProjectVersion"];
            }

            report.GameVersion = version;
            report.ArchiveEntries = provider.Files.Count;
            provider.GameVersionUpdated = version != previousVersion;

            // Pin immediately after a successful mount: a later failure must not leave process-wide engine
            // tables populated without the selection that produced them.
            _loadedPak = pak.Name;
            _loadedPakLength = pak.Size;
            _loadedPakLastModified = pak.LastModified;
            _loadedMappingLength = mappingStream.Length;
            _loadedMappingLastModified = mapping.LastModified;

            FormData settings = string.IsNullOrWhiteSpace(settingsJson)
                ? new FormData()
                : JsonConvert.DeserializeObject<FormData>(settingsJson)
                    ?? throw new InvalidDataException("The serialized settings snapshot was empty.");

            stage = "generation.dataFailed";
            try {
                Data.Initialize(provider, extractIcons: false);
            } catch {
                _initializationFailed = true;
                throw;
            }

            Randomize.Initialize();
            var (areas, _) = Randomize.GeneratePalSpawns(settings, () => false);

            int changedAreas = 0;
            Data.AreaForEachIfDiff(areas, _ => ++changedAreas);

            stage = "generation.writeFailed";
            List<FileModify.PalSchemaJson> documents = FileModify.GeneratePalSchema(areas, settings);
            using MemoryStream memory = new(8 * 1024 * 1024);
            FileModify.WritePalSchema(memory, documents);
            byte[] zip = memory.ToArray();
            using (ZipArchive check = new(new MemoryStream(zip, writable: false), ZipArchiveMode.Read)) {
                if (check.Entries.Count == 0) {
                    throw new InvalidDataException("The generated ZIP contains no PalSchema documents.");
                }

                report.SchemaDocumentCount = check.Entries.Count;
                report.SchemaBytes = check.Entries.Sum(entry => entry.Length);
            }

            report.ZipBytes = zip.Length;
            report.ZipData = zip;
            report.PalCount = Data.PalList.Count;
            report.VanillaAreaCount = Data.AreaData.Count;
            report.GeneratedAreaCount = areas.Count;
            report.ChangedAreaCount = changedAreas;
            report.Success = true;
        } catch (Exception exception) {
            report.Success = false;
            report.ErrorKey = stage;
            report.ErrorDetail = exception.Message;
        } finally {
            report.ElapsedMilliseconds = clock.ElapsedMilliseconds;
            if (pakStream is not null) {
                report.PakBytesRead = pakStream.Stats.Bytes;
                report.PakReads = pakStream.Stats.Reads;
                report.MaxPakReadRequest = pakStream.Stats.MaxRequest;
            }

            if (mappingStream is not null) {
                report.MappingBytesRead = mappingStream.Stats.Bytes;
                report.MappingReads = mappingStream.Stats.Reads;
                report.MaxMappingReadRequest = mappingStream.Stats.MaxRequest;
            }

            pakStream?.Dispose();
            mappingStream?.Dispose();
            GenerationGate.Release();
        }

        return report;
    }

    private sealed class SelectedMappings : UsmapTypeMappingsProvider {
        internal SelectedMappings(byte[] bytes) => Load(bytes, StringComparer.OrdinalIgnoreCase);
        public override void Reload() => throw new NotSupportedException("Reselect the mappings file.");
    }

    private sealed class BrowserProvider : VfsFileProvider {
        internal async Task OpenAsync(FArchive archive) {
            CheckedReader reader = new(archive);
            PostLoadReader(reader, false);
            int mounted = await MountAsync();
            reader.Failure?.Throw();
            if (mounted != 1) {
                throw new InvalidDataException("The selected PAK could not be mounted.");
            }
        }
    }

    private sealed class CheckedReader(FArchive archive) : PakFileReader(archive) {
        internal ExceptionDispatchInfo? Failure { get; private set; }
        public override void Mount(StringComparer comparer) {
            try {
                base.Mount(comparer);
            } catch (Exception exception) {
                Failure = ExceptionDispatchInfo.Capture(exception);
                throw;
            }
        }
    }
}