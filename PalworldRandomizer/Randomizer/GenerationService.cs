using System.IO.Compression;
using Newtonsoft.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer.PalSpawn;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.Randomizer;

/// <summary>Explicit inputs for a host-neutral Pal spawn generation run.</summary>
public sealed class GenerationRequest {
    /// <summary>The selected Palworld PAK, opened read-only and never copied.</summary>
    public required string PakPath { get; init; }

    /// <summary>The selected USMAP that describes the PAK's serialized types.</summary>
    public required string MappingPath { get; init; }

    /// <summary>An isolated filesystem root owned by the caller for any extracted application data.</summary>
    public required string DataRoot { get; init; }

    /// <summary>The unique ZIP path that receives the PalSchema documents.</summary>
    public required string OutputZipPath { get; init; }

    /// <summary>The captured settings; every default remains the original application default.</summary>
    public FormData Settings { get; init; } = new();

    /// <summary>The previously cached game version, used only for asset-cache invalidation.</summary>
    public string PreviousGameVersion { get; init; } = "0.0.0.0";
}

/// <summary>Small summary of a completed real-data generation run.</summary>
public sealed record GenerationResult(
    string GameVersion,
    int ArchiveEntries,
    int PalCount,
    int VanillaAreaCount,
    int GeneratedAreaCount,
    int ChangedAreaCount,
    int SchemaDocumentCount,
    long SchemaBytes,
    long ZipBytes,
    string OutputZipPath
);

/// <summary>
/// Runs the original archive, data, randomization and PalSchema engine away from the host's UI thread.
/// </summary>
/// <remarks>
/// The engine keeps process-wide tables, so every run is serialized through one gate that covers archive ownership,
/// initialization, generation and export. The first run initializes the original
/// tables from the selected files; later runs with those same files reuse them and regenerate from the supplied
/// settings. Selecting different files in one process is rejected explicitly because the original engine has no
/// reset boundary. A failed initialization requires a restart to avoid reusing partially populated tables.
/// The PAK is opened read-only on demand; no archive content is copied. Settings are copied before generation so
/// engine normalization cannot mutate the caller's live template. The caller must marshal <c>reportProgress</c>
/// callbacks to its own UI context. Optional UI icons are not decoded, so a host without a PNG codec still receives
/// complete gameplay and PalSchema data.
/// </remarks>
public static class PalSpawnGenerationService {
    private static readonly SemaphoreSlim EngineGate = new(1, 1);
    private static LoadedInputs? _loaded;
    private static bool _initializationFailed;

    private sealed record LoadedInputs(
        string PakPath,
        string MappingPath,
        int ArchiveEntries,
        string GameVersion
    );

    /// <summary>Generates and writes a PalSchema ZIP from the selected game files.</summary>
    /// <param name="request">Explicit selected paths, isolated data root, output path and captured settings.</param>
    /// <param name="reportProgress">Optional engine progress snapshots, raised on the generation worker.</param>
    /// <param name="cancellationToken">Cancels waiting/opening before engine work starts; generated work is atomic.</param>
    public static async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        Action<double?>? reportProgress = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);
        FormData settings = TemplateJson.Load(JsonConvert.SerializeObject(request.Settings), static () => new FormData())
            ?? throw new InvalidDataException("The generation settings could not be captured.");
        string pakPath = Path.GetFullPath(request.PakPath);
        string mappingPath = Path.GetFullPath(request.MappingPath);
        string dataRoot = Path.GetFullPath(request.DataRoot);
        string outputPath = Path.GetFullPath(request.OutputZipPath);

        if (!File.Exists(pakPath)) {
            throw new FileNotFoundException("The selected PAK does not exist.", pakPath);
        }

        if (!File.Exists(mappingPath)) {
            throw new FileNotFoundException("The selected USMAP does not exist.", mappingPath);
        }

        if (
            string.Equals(outputPath, pakPath, StringComparison.Ordinal)
            || string.Equals(outputPath, mappingPath, StringComparison.Ordinal)
        ) {
            throw new ArgumentException("The output ZIP must not overwrite a selected game input.", nameof(request));
        }

        if (File.Exists(outputPath) || Directory.Exists(outputPath)) {
            throw new ArgumentException("The output ZIP must be a new file.", nameof(request));
        }

        await EngineGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_initializationFailed) {
                throw new InvalidOperationException("Game data initialization failed; restart the app before retrying.");
            }

            Directory.CreateDirectory(dataRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            GenerationResult result;
            if (_loaded is { } loaded) {
                if (
                    !string.Equals(loaded.PakPath, pakPath, StringComparison.Ordinal)
                    || !string.Equals(loaded.MappingPath, mappingPath, StringComparison.Ordinal)
                ) {
                    throw new InvalidOperationException(
                        "The original engine is already initialized for different game files; restart the host."
                    );
                }

                result = await Task.Run(
                        () => GenerateAndWrite(
                            settings,
                            outputPath,
                            loaded.ArchiveEntries,
                            loaded.GameVersion,
                            reportProgress
                        ),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            } else {
                await using VfsFileProvider provider = await UAssetData
                    .OpenArchiveAsync(pakPath, mappingPath, request.PreviousGameVersion)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                result = await Task.Run(
                        () => {
                            try {
                                Data.Initialize(provider, extractIcons: false);
                            } catch {
                                _initializationFailed = true;
                                throw;
                            }

                            _loaded = new(pakPath, mappingPath, provider.Files.Count, provider.GameVersion);
                            return GenerateAndWrite(
                                settings,
                                outputPath,
                                provider.Files.Count,
                                provider.GameVersion,
                                reportProgress
                            );
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            return result;
        } finally {
            EngineGate.Release();
        }
    }

    private static GenerationResult GenerateAndWrite(
        FormData settings,
        string outputPath,
        int archiveEntries,
        string gameVersion,
        Action<double?>? reportProgress
    ) {
        Randomize.Initialize();

        var (areas, _) = Randomize.GeneratePalSpawns(settings, () => false, reportProgress);

        int changedAreas = 0;
        Data.AreaForEachIfDiff(areas, _ => ++changedAreas);

        // Build beside the destination so a failed write never truncates an existing export.
        string temporary = outputPath + ".partial-" + Guid.NewGuid().ToString("N");
        try {
            FileModify.WritePalSchema(temporary, areas, settings);
            GenerationResult result = Summarize(
                temporary,
                outputPath,
                archiveEntries,
                gameVersion,
                areas,
                changedAreas
            );
            File.Move(temporary, outputPath);
            return result;
        } finally {
            TryDelete(temporary);
        }
    }

    private static GenerationResult Summarize(
        string writtenZipPath,
        string outputPath,
        int archiveEntries,
        string gameVersion,
        List<AreaData> areas,
        int changedAreas
    ) {
        using ZipArchive archive = ZipFile.OpenRead(writtenZipPath);
        long schemaBytes = archive.Entries.Sum(entry => entry.Length);
        return new(
            gameVersion,
            archiveEntries,
            Data.PalList.Count,
            Data.AreaData.Count,
            areas.Count,
            changedAreas,
            archive.Entries.Count,
            schemaBytes,
            new FileInfo(writtenZipPath).Length,
            outputPath
        );
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
            // The original failure is more useful than a cleanup failure.
        } catch (UnauthorizedAccessException) {
            // The original failure is more useful than a cleanup failure.
        }
    }
}