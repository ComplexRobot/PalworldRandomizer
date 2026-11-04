using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Serializer;

(string Name, Func<Task> Run)[] tests = [
    ("Existing files cannot become output", ExistingOutputRefused),
    ("Generation does not change the configured application data root", DataRootPreserved),
    ("Generation service boundary is host-neutral", HostNeutralBoundary),
    ("Selected real game data produces nonempty randomized PalSchema ZIP", RealGameDataToPalSchema),
    ("Concurrent runs serialize the shared engine and remain independently randomized", ConcurrentRuns),
];
if (args.Contains("--failed-init", StringComparer.Ordinal)) {
    tests = [("Failed engine initialization requires a restart", FailedInitializationRequiresRestart)];
} else if (args.Contains("--real-only", StringComparer.Ordinal)) {
    tests = [("Selected real game data produces nonempty randomized PalSchema ZIP", RealGameDataToPalSchema)];
} else if (args.Contains("--quick", StringComparer.Ordinal)) {
    tests = tests[..2];
}

int failures = 0;
foreach (var (name, run) in tests) {
    try {
        await run();
        Console.WriteLine($"PASS {name}");
    } catch (Exception exception) {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} generation checks passed.");
return failures == 0 ? 0 : 1;

static async Task FailedInitializationRequiresRestart() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    Require(File.Exists(pak), "The real PAK is required for the failed-initialization regression.");
    string root = Path.Combine(Path.GetTempPath(), "generation-init-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
        string mapping = Path.Combine(root, "empty.usmap");
        using (var writer = new BinaryWriter(File.Create(mapping))) {
            writer.Write((ushort)0x30c4);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((uint)12);
            writer.Write((uint)12);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
        }

        var request = new GenerationRequest {
            PakPath = pak, MappingPath = mapping, DataRoot = Path.Combine(root, "data"),
            OutputZipPath = Path.Combine(root, "first.zip"),
        };
        Exception? first = null;
        try {
            await PalSpawnGenerationService.GenerateAsync(request);
        } catch (Exception exception) {
            first = exception;
        }

        Require(first is not null && first.ToString().Contains("Data.Initialize", StringComparison.Ordinal),
            "The fixture did not reach the real Data.Initialize failure path: " + first);
        string otherMapping = Path.Combine(root, "other-empty.usmap");
        File.Copy(mapping, otherMapping);
        Exception? retry = null;
        try {
            await PalSpawnGenerationService.GenerateAsync(new() {
                PakPath = pak, MappingPath = otherMapping, DataRoot = Path.Combine(root, "data"),
                OutputZipPath = Path.Combine(root, "retry.zip"),
            });
        } catch (Exception exception) {
            retry = exception;
        }

        Require(retry is InvalidOperationException
            && retry.Message.Contains("restart", StringComparison.OrdinalIgnoreCase),
            "A retry after failed initialization must be refused with a restart-required error; saw " + retry);
    } finally {
        Directory.Delete(root, true);
    }
}

static async Task ExistingOutputRefused() {
    string root = Path.Combine(Path.GetTempPath(), "generation-output-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
        string pak = Path.Combine(root, "selected.pak");
        string mapping = Path.Combine(root, "mapping.usmap");
        string output = Path.Combine(root, "existing.zip");
        await File.WriteAllTextAsync(pak, "selected game input");
        await File.WriteAllTextAsync(mapping, "selected mapping input");
        await File.WriteAllTextAsync(output, "existing output");
        var request = new GenerationRequest {
            PakPath = pak, MappingPath = mapping, DataRoot = Path.Combine(root, "data"), OutputZipPath = output,
        };
        Exception? refusal = null;
        try {
            await PalSpawnGenerationService.GenerateAsync(request);
        } catch (Exception exception) {
            refusal = exception;
        }

        Require(refusal is ArgumentException,
            $"An existing output should be refused before opening the input; saw {refusal?.GetType().Name ?? "none"}.");
        Require(await File.ReadAllTextAsync(pak) == "selected game input", "The selected PAK changed.");
        Require(await File.ReadAllTextAsync(mapping) == "selected mapping input", "The selected mapping changed.");
        Require(await File.ReadAllTextAsync(output) == "existing output", "The existing output changed.");
        Require(!Directory.Exists(request.DataRoot), "The refusal should happen before creating the data root.");

        if (!OperatingSystem.IsWindows()) {
            string alias = Path.Combine(root, "selected-link.pak");
            File.CreateSymbolicLink(alias, pak);
            var aliased = new GenerationRequest {
                PakPath = alias, MappingPath = mapping, DataRoot = Path.Combine(root, "alias-data"),
                OutputZipPath = pak,
            };
            refusal = null;
            try {
                await PalSpawnGenerationService.GenerateAsync(aliased);
            } catch (Exception exception) {
                refusal = exception;
            }

            Require(refusal is ArgumentException, "A path alias must not allow a selected PAK to become output.");
            Require(await File.ReadAllTextAsync(pak) == "selected game input", "The aliased PAK changed.");
        }
    } finally {
        Directory.Delete(root, true);
    }
}

static async Task DataRootPreserved() {
    string root = Path.Combine(Path.GetTempPath(), "generation-root-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    string originalRoot = UAssetData.AppDataPath();
    string configuredRoot = Path.Combine(root, "configured");
    try {
        UAssetData.ConfigureDataRoot(configuredRoot);
        string expectedRoot = UAssetData.AppDataPath();
        string pak = Path.Combine(root, "selected.pak");
        string mapping = Path.Combine(root, "mapping.usmap");
        await File.WriteAllTextAsync(pak, "invalid test PAK");
        await File.WriteAllTextAsync(mapping, "invalid test mapping");
        var request = new GenerationRequest {
            PakPath = pak, MappingPath = mapping, DataRoot = Path.Combine(root, "other"),
            OutputZipPath = Path.Combine(root, "new-output.zip"),
        };
        bool failed = false;
        try {
            await PalSpawnGenerationService.GenerateAsync(request);
        } catch (Exception) {
            failed = true;
        }

        Require(failed, "Invalid game inputs must fail instead of generating fake results.");
        Require(UAssetData.AppDataPath() == expectedRoot,
            "Generation changed the application data root after a failed open.");
    } finally {
        UAssetData.ConfigureDataRoot(originalRoot);
        Directory.Delete(root, true);
    }
}

static Task HostNeutralBoundary() {
    MethodInfo? method = typeof(PalSpawnGenerationService).GetMethod("GenerateAsync");
    Require(method != null, "There is no explicit asynchronous generation boundary.");
    Require(
        method!.ReturnType == typeof(Task<GenerationResult>),
        "Generation must return an awaitable result instead of blocking the caller."
    );
    Require(
        method.GetParameters()[0].ParameterType == typeof(GenerationRequest),
        "Generation must accept explicit selected inputs and settings."
    );
    Require(
        method.GetParameters().All(parameter => parameter.ParameterType.Namespace != "PalworldRandomizer.Window"),
        "The generation boundary still requires a window type."
    );
    foreach (
        string property in (string[])
            ["PakPath", "MappingPath", "DataRoot", "OutputZipPath", "Settings", "PreviousGameVersion"]
    ) {
        Require(
            typeof(GenerationRequest).GetProperty(property) != null,
            $"GenerationRequest is missing the selected input '{property}'."
        );
    }

    return Task.CompletedTask;
}

static async Task RealGameDataToPalSchema() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    string mapping = Environment.GetEnvironmentVariable("PALWORLD_TEST_USMAP")
        ?? "/root/project-data/Mappings.usmap";
    Require(File.Exists(pak), $"Real PAK is missing: {pak}");
    Require(File.Exists(mapping), $"Real USMAP is missing: {mapping}");

    string evidence = Path.Combine(AppContext.BaseDirectory, "evidence-" + Guid.NewGuid().ToString("N"));
    string dataRoot = Path.Combine(evidence, "data");
    string output = Path.Combine(evidence, "pal-spawns.zip");
    Directory.CreateDirectory(evidence);

    long pakLength = new FileInfo(pak).Length;
    DateTime pakWrite = File.GetLastWriteTimeUtc(pak);
    byte[] mappingBytes = File.ReadAllBytes(mapping);
    FormData settings = new() { RandomSeed = 12345, RandomSeedLocked = true };
    string capturedSettings = JsonConvert.SerializeObject(settings);

    GenerationResult result;
    try {
        result = await PalSpawnGenerationService.GenerateAsync(
            new() {
                PakPath = pak,
                MappingPath = mapping,
                DataRoot = dataRoot,
                OutputZipPath = output,
                Settings = settings,
            }
        );
    } catch (Exception exception) {
        throw new Exception(
            $"Real generation failed. Evidence root: {evidence}. "
                + $"Data root bytes: {DirectorySize(dataRoot)}. {exception}"
        );
    }

    Require(result.ArchiveEntries > 100_000, $"Unexpected archive entry count: {result.ArchiveEntries}.");
    Require(JsonConvert.SerializeObject(settings) == capturedSettings,
        "Generating with the original engine changed the caller's saved settings.");
    Require(result.PalCount > 100, $"Real character data yielded too few pals: {result.PalCount}.");
    Require(result.VanillaAreaCount > 100, $"Real spawn tables yielded too few areas: {result.VanillaAreaCount}.");
    Require(result.GeneratedAreaCount > 100, $"Generation produced too few areas: {result.GeneratedAreaCount}.");
    Require(result.ChangedAreaCount > 0, "Generation produced no changed areas; output may be vanilla/empty.");
    Require(result.SchemaDocumentCount >= 3, $"Too few PalSchema documents: {result.SchemaDocumentCount}.");
    Require(result.SchemaBytes > 10_000, $"PalSchema documents are suspiciously small: {result.SchemaBytes} bytes.");
    Require(File.Exists(output), "Generation reported success without a ZIP.");
    Require(result.ZipBytes == new FileInfo(output).Length && result.ZipBytes > 0, "ZIP size mismatch.");
    Require(Data.PalIcon.Count == 0, "Generation unexpectedly attempted optional icon extraction.");
    Require(new FileInfo(pak).Length == pakLength && File.GetLastWriteTimeUtc(pak) == pakWrite,
        "Generation modified the selected PAK.");
    Require(File.ReadAllBytes(mapping).AsSpan().SequenceEqual(mappingBytes),
        "Generation modified the selected USMAP.");
    Require(DirectorySize(dataRoot) < 1_000_000_000, "Generation materialized too much extracted data.");
    Require(Directory.GetFiles(evidence, "*.partial-*").Length == 0, "Generation left a partial ZIP beside the output.");

    using ZipArchive zip = ZipFile.OpenRead(output);
    Require(zip.Entries.Count == result.SchemaDocumentCount, "ZIP entry count does not match the result.");
    Require(zip.Entries.All(entry => entry.FullName.StartsWith("CustomPalSpawns/", StringComparison.Ordinal)),
        "ZIP contains an unexpected layout.");

    JObject palSpawns = ReadJson(zip, "CustomPalSpawns/blueprints/PalSpawns.json");
    Require(palSpawns.Count > 100, $"PalSpawns.json has too few areas: {palSpawns.Count}.");
    int groupCount = 0;
    int spawnCount = 0;
    foreach (JProperty area in palSpawns.Properties()) {
        JArray groups = area.Value["SpawnGroupList"] as JArray
            ?? throw new Exception($"'{area.Name}' has no SpawnGroupList.");
        Require(groups.Count > 0, $"'{area.Name}' has an empty SpawnGroupList.");
        foreach (JObject group in groups.Cast<JObject>()) {
            JArray pals = group["PalList"] as JArray ?? throw new Exception($"'{area.Name}' has a group without PalList.");
            Require(pals.Count > 0, $"'{area.Name}' has an empty PalList.");
            foreach (JObject spawn in pals.Cast<JObject>()) {
                string? name = (spawn["PalId"] as JObject)?["Key"]?.ToString()
                    ?? (spawn["NPCID"] as JObject)?["Key"]?.ToString();
                Require(!string.IsNullOrWhiteSpace(name), $"'{area.Name}' has a spawn without a character key.");
                if (name != "None") {
                    Require(Data.PalData.ContainsKey(name!), $"'{area.Name}' references unknown character '{name}'.");
                }

                ++spawnCount;
            }

            ++groupCount;
        }
    }

    Require(groupCount > 100 && spawnCount > 100, $"Generated spawn content is too small: {groupCount}/{spawnCount}.");

    JObject eggs = ReadJson(zip, "CustomPalSpawns/blueprints/EggSpawns.json");
    Require(eggs.Count > 0, "EggSpawns.json is empty.");
    foreach (JProperty egg in eggs.Properties()) {
        JArray lottery = egg.Value["SpawnPalEggLotteryDataArray"] as JArray
            ?? throw new Exception($"'{egg.Name}' has no egg lottery.");
        Require(lottery.Count > 0, $"'{egg.Name}' has an empty egg lottery.");
    }

    string summary = JsonSerializer.Serialize(
        new {
            result,
            PalSpawnsAreas = palSpawns.Count,
            groups = groupCount,
            spawns = spawnCount,
            eggAreas = eggs.Count,
            dataRootBytes = DirectorySize(dataRoot),
            evidence,
        },
        new JsonSerializerOptions { WriteIndented = true }
    );
    File.WriteAllText(Path.Combine(evidence, "summary.json"), summary);
    Console.WriteLine($"REAL GENERATION {summary}");
}

static async Task ConcurrentRuns() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    string mapping = Environment.GetEnvironmentVariable("PALWORLD_TEST_USMAP")
        ?? "/root/project-data/Mappings.usmap";
    string evidence = Path.Combine(AppContext.BaseDirectory, "evidence-concurrent-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(evidence);

    Task<GenerationResult> first = PalSpawnGenerationService.GenerateAsync(
        new() {
            PakPath = pak,
            MappingPath = mapping,
            DataRoot = Path.Combine(evidence, "data-a"),
            OutputZipPath = Path.Combine(evidence, "seed-111.zip"),
            Settings = new() { RandomSeed = 111, RandomSeedLocked = true },
        }
    );
    Task<GenerationResult> second = PalSpawnGenerationService.GenerateAsync(
        new() {
            PakPath = pak,
            MappingPath = mapping,
            DataRoot = Path.Combine(evidence, "data-b"),
            OutputZipPath = Path.Combine(evidence, "seed-222.zip"),
            Settings = new() { RandomSeed = 222, RandomSeedLocked = true },
        }
    );

    GenerationResult[] results = await Task.WhenAll(first, second);
    Require(results.All(result => result.ChangedAreaCount > 0), "A concurrent run produced no changed areas.");
    Require(results[0].SchemaDocumentCount == results[1].SchemaDocumentCount,
        "Concurrent runs produced different document counts.");
    byte[] firstSchema = ReadEntry(results[0].OutputZipPath, "CustomPalSpawns/blueprints/PalSpawns.json");
    byte[] secondSchema = ReadEntry(results[1].OutputZipPath, "CustomPalSpawns/blueprints/PalSpawns.json");
    Require(firstSchema.Length > 10_000 && secondSchema.Length > 10_000, "Concurrent schemas are too small.");
    Require(
        !System.Security.Cryptography.SHA256.HashData(firstSchema)
            .SequenceEqual(System.Security.Cryptography.SHA256.HashData(secondSchema)),
        "Different seeds produced identical spawn schemas."
    );
    File.WriteAllText(
        Path.Combine(evidence, "summary.json"),
        JsonSerializer.Serialize(new { results, evidence }, new JsonSerializerOptions { WriteIndented = true })
    );
    Console.WriteLine($"CONCURRENT GENERATION seeds=111/222 outputs={results.Length} evidence={evidence}");
}

static byte[] ReadEntry(string zipPath, string entryName) {
    using ZipArchive zip = ZipFile.OpenRead(zipPath);
    ZipArchiveEntry entry = zip.GetEntry(entryName)
        ?? throw new Exception($"Missing PalSchema document '{entryName}'.");
    using Stream stream = entry.Open();
    using MemoryStream buffer = new();
    stream.CopyTo(buffer);
    return buffer.ToArray();
}

static JObject ReadJson(ZipArchive zip, string entryName) {
    ZipArchiveEntry entry = zip.GetEntry(entryName)
        ?? throw new Exception($"Missing PalSchema document '{entryName}'.");
    using Stream stream = entry.Open();
    using StreamReader reader = new(stream);
    return JObject.Parse(reader.ReadToEnd());
}

static long DirectorySize(string path) => Directory.Exists(path)
    ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
    : 0;

static void Require(bool condition, string message) {
    if (!condition) {
        throw new Exception(message);
    }
}