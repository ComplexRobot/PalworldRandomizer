using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using PalworldRandomizer.Serializer;

if (args.Contains("--startup-only")) {
    return await StartupTests.RunAsync() == 0 ? 0 : 1;
}

(string Name, Func<Task> Run)[] tests = [
    ("Default application paths are portable before startup", DefaultPaths),
    ("Explicit application roots isolate every filesystem helper", ExplicitRoot),
    ("Async disposal closes registered archive handles", DisposeClosesArchive),
    ("Explicit read-only async opening returns a live caller-owned provider", OpenOwnedArchive),
    ("Archive startup exposes no WPF or parameterless UI initialization boundary", PortableBoundary),
    ("Cached texture paths use the actual filesystem directory", CachedIconPath),
    ("Image saves can drain without surrendering archive ownership", DrainWithoutClosing),
    ("Mount failures propagate the original parser error and release handles", MountFailure),
    ("Missing and malformed selected inputs fail without writes or leaked handles", InvalidInputs),
    ("Asset loading propagates the parser or codec exception", AssetFailure),
    ("Texture extraction cannot silently skip parser or codec failures", TextureFailure),
    ("Lazy texture reads cannot corrupt subsequent asset loads or leak archive handles", LazyTextureReads),
];
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

Console.WriteLine($"{tests.Length - failures}/{tests.Length} archive checks passed.");
failures += await StartupTests.RunAsync();
return failures == 0 ? 0 : 1;

static Task DefaultPaths() {
    string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Palworld-Randomizer");
    Require(UAssetData.AppDataPath() == root, "App-data root is uninitialized or not platform-correct.");
    Require(UAssetData.AppDataPath(null!) == root, "Null must select the application root.");
    Require(UAssetData.AppDataPath(@"Config\GlobalConfig.json") == Path.Combine(root, "Config", "GlobalConfig.json"),
        "Legacy relative filesystem separators must be portable.");
    Require(UAssetData.PalIconPath(@"Human\test.png") == Path.Combine(root, "Images", "PalIcon", "Human", "test.png"),
        "Nested icon paths must use filesystem separators.");
    return Task.CompletedTask;
}

static Task ExplicitRoot() {
    var configure = typeof(UAssetData).GetMethod("ConfigureDataRoot", [typeof(string)]);
    Require(configure != null, "An explicit application-data root is required for isolated callers/tests.");
    string root = Path.Combine(Path.GetTempPath(), "archive-paths-" + Guid.NewGuid());
    try {
        configure!.Invoke(null, [root]);
        (Func<string?, string> Helper, string Folder)[] helpers = [
            (UAssetData.AppDataPath, ""), (UAssetData.AssetsPath, "Assets"),
            (UAssetData.PalSpawnerPath, "Assets"), (UAssetData.PalEggPath, "Assets/PalEgg"),
            (UAssetData.DataPath, "Data"), (UAssetData.ImagesPath, "Images"),
            (UAssetData.PalIconPath, "Images/PalIcon"), (UAssetData.NpcIconPath, "Images/NPC"),
            (UAssetData.WeaponIconPath, "Images/InventoryItemIcon"), (UAssetData.ImportsPath, "Imports"),
        ];
        foreach (var (helper, folder) in helpers) {
            string expected = folder.Length == 0
                ? root : Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar));
            Require(helper(null) == expected && helper("") == expected, "Empty/null root changed: " + folder);
            Require(helper(@"nested\file.bin") == Path.Combine(expected, "nested", "file.bin"),
                "Legacy filesystem path escaped its folder: " + folder);
        }

        string absolute = Path.Combine(Path.GetTempPath(), "absolute.bin");
        Require(UAssetData.AppDataPath(absolute) == absolute, "Rooted paths must follow Path.Combine semantics.");
        Require(!Directory.Exists(root), "Path construction must not create directories.");
        Require(VfsFileProvider.SoftPathToHardPath("/Game/Pal/Test.Test") == "Pal/Content/Pal/Test.uasset",
            "CUE asset identifiers must not be converted to filesystem paths.");
    } finally {
        configure!.Invoke(null, [null]);
    }

    return Task.CompletedTask;
}

static async Task DisposeClosesArchive() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    using VfsFileProvider provider = new();
    int before = Handles(pak);
    provider.RegisterVfs(pak);
    Require(Handles(pak) > before, "RegisterVfs must hold the archive open for this lifetime test.");
    await provider.DisposeAsync();
    Require(Handles(pak) == before, "DisposeAsync drained image tasks but leaked the archive file handle.");
}

static async Task OpenOwnedArchive() {
    var open = typeof(UAssetData).GetMethod("OpenArchiveAsync", [typeof(string), typeof(string), typeof(string)]);
    Require(open != null, "Archive opening must accept explicit PAK/USMAP paths and previous version, without UI.");
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    string mapping = Environment.GetEnvironmentVariable("PALWORLD_TEST_USMAP") ?? "/root/project-data/Mappings.usmap";
    string root = Path.Combine(Path.GetTempPath(), "archive-open-" + Guid.NewGuid());
    long pakSize = new FileInfo(pak).Length;
    DateTime pakWrite = File.GetLastWriteTimeUtc(pak);
    byte[] mappingBytes = File.ReadAllBytes(mapping);
    DateTime mappingWrite = File.GetLastWriteTimeUtc(mapping);
    int before = Handles(pak);
    string globalVersion = UAssetData.GameVersion;
    string expectedVersion = "";
    UAssetData.ConfigureDataRoot(root);
    try {
        VfsFileProvider provider = await (Task<VfsFileProvider>)open!.Invoke(null, [pak, mapping, "older-version"])!;
        await using (provider) {
            Require(Handles(pak) > before, "The returned provider was disposed before ownership reached its caller.");
            Require(provider.Files.Count == 185141, "Real archive entry count changed.");
            Require(provider.TrySaveAsset("Pal/Config/DefaultGame.ini", out byte[]? ini) && ini.Length > 0,
                "Returned provider cannot read DefaultGame.ini.");
            var rows = provider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
            Require(rows.Count > 0, "Explicit mappings did not deserialize the representative data table.");
            GameStruct table = new(provider, "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
            Require(table.DataTable.Count == rows.Count, "Existing GameStruct could not project the mapped table.");
            var row = rows.First();
            CharacterData character = new(row.Value.Properties);
            Require(character.IsPal && character.Hp > 0, "Existing GameStruct character projection failed.");
            expectedVersion = System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.ASCII.GetString(ini!),
                @"(?m)^ProjectVersion=([^\r\n]+)").Groups[1].Value;
            Require(expectedVersion.Length > 0 && provider.GameVersion == expectedVersion,
                "Version was not read from the real archive's DefaultGame.ini.");
            Require(provider.GameVersionUpdated, "Version-change cache flag was lost.");
            Require(UAssetData.GameVersion == globalVersion, "Read-only opening changed legacy global configuration.");
            Require(CUE4Parse.Compression.OodleHelper.Instance == null
                && CUE4Parse.Compression.ZlibHelper.Instance == null,
                "Portable opening initialized platform-specific native helpers.");
            Console.WriteLine($"REAL PAK bytes={pakSize}; entries={provider.Files.Count}; ini={ini!.Length}; "
                + $"version={provider.GameVersion}; tableRows={rows.Count}; first={row.Key.Text}; hp={character.Hp}");
            Require(!Directory.Exists(root), "Read-only opening prepared resources or wrote configuration.");
            Require(new FileInfo(pak).Length == pakSize && File.GetLastWriteTimeUtc(pak) == pakWrite,
                "Read-only opening modified the selected PAK.");
            Require(File.ReadAllBytes(mapping).AsSpan().SequenceEqual(mappingBytes)
                && File.GetLastWriteTimeUtc(mapping) == mappingWrite, "Selected USMAP was overwritten.");
        }

        Require(Handles(pak) == before, "Caller disposal did not release the returned archive.");
        await using (var unchanged = await UAssetData.OpenArchiveAsync(pak, mapping, expectedVersion)) {
            Require(!unchanged.GameVersionUpdated && unchanged.GameVersion == expectedVersion,
                "An unchanged game version incorrectly invalidated the icon/asset cache.");
        }

        Require(Handles(pak) == before, "Repeated opening leaked an archive handle.");
    } finally {
        UAssetData.ConfigureDataRoot();
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }
}

static Task PortableBoundary() {
    Require(typeof(UAssetData).GetMethod("Initialize", Type.EmptyTypes) == null,
        "Parameterless archive startup still captures UI singletons/configuration.");
    Require(typeof(UAssetData).GetMethods().SelectMany(method => method.GetParameters())
        .All(parameter => parameter.ParameterType.Namespace != "PalworldRandomizer.Window"),
        "The archive boundary still requires a Window type.");
    return Task.CompletedTask;
}

static async Task CachedIconPath() {
    string root = Path.Combine(Path.GetTempPath(), "archive-icon-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try {
        string asset = Path.Combine(root, "CachedTexture.uasset");
        string image = Path.Combine(root, "CachedTexture.png");
        File.WriteAllBytes(asset, []);
        File.WriteAllBytes(image, [1, 2, 3]);
        await using VfsFileProvider provider = new();
        provider.AddFile(asset, "Pal/Content/");
        string key = provider.Files.Keys.Single();
        Require(provider.SaveTexturePng(key, root, false) == image,
            "An existing cached image was not found at its platform-correct filesystem path.");
        Require(File.ReadAllBytes(image).SequenceEqual(new byte[] { 1, 2, 3 }), "Cached image was overwritten.");
    } finally {
        Directory.Delete(root, true);
    }
}

static async Task DrainWithoutClosing() {
    var drain = typeof(VfsFileProvider).GetMethod("WaitForTextureSavesAsync", Type.EmptyTypes);
    Require(drain != null, "Legacy startup must finish initial icon saves without disposing the returned provider.");
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    using VfsFileProvider provider = new();
    int before = Handles(pak);
    provider.RegisterVfs(pak);
    var tasks = (List<Task>)typeof(VfsFileProvider).GetField("_fileSaveTasks",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(provider)!;
    TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    tasks.Add(pending.Task);
    var completion = (Task)drain!.Invoke(provider, null)!;
    Require(!completion.IsCompleted, "Startup skipped a pending icon save.");
    pending.SetResult();
    await completion;
    Require(Handles(pak) > before, "Draining initial images prematurely closed the provider.");
    TaskCompletionSource failed = new();
    InvalidOperationException expected = new("image encoding failed");
    tasks.Add(failed.Task);
    failed.SetException(expected);
    Exception? actual = null;
    try {
        await provider.DisposeAsync();
    } catch (Exception exception) {
        actual = exception;
    }

    Require(ReferenceEquals(actual, expected), "Asynchronous image failures must propagate unchanged.");
    Require(Handles(pak) == before, "A failed image save leaked the archive during final disposal.");
}

static async Task MountFailure() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    string mapping = Environment.GetEnvironmentVariable("PALWORLD_TEST_USMAP") ?? "/root/project-data/Mappings.usmap";
    string root = Path.Combine(Path.GetTempPath(), "archive-failure-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    string damaged = Path.Combine(root, "truncated.pak");
    try {
        // A bounded real footer, not a replacement parser: valid registration, missing archive index.
        byte[] footer = new byte[4096];
        using (FileStream input = File.OpenRead(pak)) {
            input.Seek(-footer.Length, SeekOrigin.End);
            input.ReadExactly(footer);
        }

        File.WriteAllBytes(damaged, footer);
        Exception? expected = null;
        using (var archive = new CUE4Parse.UE4.Readers.FStreamArchive(damaged, File.OpenRead(damaged),
            new CUE4Parse.UE4.Versions.VersionContainer(CUE4Parse.UE4.Versions.EGame.GAME_UE5_1))) {
            using CUE4Parse.UE4.Pak.PakFileReader reader = new(archive);
            try {
                reader.Mount(StringComparer.OrdinalIgnoreCase);
            } catch (Exception exception) {
                expected = exception;
            }
        }

        Require(expected != null, "Damaged index did not exercise the mount failure path.");
        Exception? actual = null;
        int before = Handles(damaged);
        try {
            await using var unexpected = await UAssetData.OpenArchiveAsync(damaged, mapping);
        } catch (Exception exception) {
            actual = exception;
        }

        Require(Handles(damaged) == before, "Failed mount leaked its archive handle.");
        Require(actual?.GetType() == expected!.GetType(),
            $"Mount error was replaced: expected {expected.GetType().Name}, actual {actual?.GetType().Name}.");
    } finally {
        Directory.Delete(root, true);
    }
}

static async Task InvalidInputs() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    string mapping = Environment.GetEnvironmentVariable("PALWORLD_TEST_USMAP") ?? "/root/project-data/Mappings.usmap";
    string root = Path.Combine(Path.GetTempPath(), "archive-invalid-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    string malformedPak = Path.Combine(root, "invalid.pak");
    string malformedMapping = Path.Combine(root, "invalid.usmap");
    File.WriteAllBytes(malformedPak, [0, 1, 2]);
    File.WriteAllBytes(malformedMapping, [0, 1, 2]);
    int before = Handles(pak);
    try {
        foreach (var (archivePath, mappingPath) in new[] {
            (Path.Combine(root, "missing.pak"), mapping), (pak, Path.Combine(root, "missing.usmap")),
            (malformedPak, mapping), (pak, malformedMapping),
        }) {
            Exception? actual = null;
            try {
                await using var unexpected = await UAssetData.OpenArchiveAsync(archivePath, mappingPath);
            } catch (Exception exception) {
                actual = exception;
            }

            Require(actual != null, "Invalid selected inputs must not return an apparently usable provider.");
            Require(Handles(pak) == before && Handles(malformedPak) == 0,
                "An opening failure leaked its selected archive.");
        }

        Require(Directory.GetFileSystemEntries(root).Length == 2, "Opening wrote to the selected input directory.");
        Require(File.ReadAllBytes(malformedMapping).SequenceEqual(new byte[] { 0, 1, 2 }),
            "Malformed selected mappings were replaced with bundled mappings.");
    } finally {
        Directory.Delete(root, true);
    }
}

static Task AssetFailure() {
    string root = Path.Combine(Path.GetTempPath(), "asset-error-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try {
        string asset = Path.Combine(root, "Broken.uasset");
        File.WriteAllBytes(asset, [0, 1, 2]);
        using VfsFileProvider provider = new();
        provider.AddFile(asset, "Pal/Content/");
        string key = provider.Files.Keys.Single();
        Exception expected = Catch(() => provider.LoadPackage(key));
        Exception actual = Catch(() => provider.LoadAsset(key).ToList());
        Require(actual.GetType() == expected.GetType() && actual.Message == expected.Message,
            "Asset loading replaced the original parser/codec error with a generic failure.");
    } finally {
        Directory.Delete(root, true);
    }

    return Task.CompletedTask;
}

static Task TextureFailure() {
    string root = Path.Combine(Path.GetTempPath(), "texture-error-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try {
        string asset = Path.Combine(root, "Broken.uasset");
        File.WriteAllBytes(asset, [0, 1, 2]);
        using VfsFileProvider provider = new();
        provider.AddFile(asset, "Pal/Content/");
        string key = provider.Files.Keys.Single();
        Exception expected = Catch(() => provider.LoadPackage(key));
        Exception actual = Catch(() => provider.SaveTexturePng(key, root, false));
        Require(actual.GetType() == expected.GetType() && actual.Message == expected.Message,
            "Texture extraction swallowed or replaced the original parser/codec error.");
        Exception retry = Catch(() => provider.SaveTexturePng(key, root, false));
        Require(retry.GetType() == expected.GetType(), "A failed texture was incorrectly marked as saved.");
    } finally {
        Directory.Delete(root, true);
    }

    return Task.CompletedTask;
}

static async Task LazyTextureReads() {
    string pak = Environment.GetEnvironmentVariable("PALWORLD_TEST_PAK") ?? "/root/project-data/Pal-Windows.pak";
    string mapping = Environment.GetEnvironmentVariable("PALWORLD_TEST_USMAP") ?? "/root/project-data/Mappings.usmap";
    int before = Handles(pak);
    await using (var provider = await UAssetData.OpenArchiveAsync(pak, mapping)) {
        var reader = (CUE4Parse.UE4.Pak.PakFileReader)provider.MountedVfs.Single();
        long position = reader.Ar.Position;
        byte[] bytes = new byte[16];
        Require(reader.Ar.ReadAt(0, bytes, 0, bytes.Length) == bytes.Length, "Positional PAK read was incomplete.");
        Require(reader.Ar.Position == position,
            "Positional reads mutate a shared cursor; lazy background texture reads can corrupt asset loading.");

        var tasks = new List<Task>();
        string[] icons = [.. provider.Files.Keys.Where(path =>
            path.StartsWith("Pal/Content/Pal/Texture/PalIcon/Normal/", StringComparison.Ordinal)
            && path.EndsWith(".uasset", StringComparison.Ordinal)).Take(160)];
        Require(icons.Length == 160, "Real lazy-bulk texture fixture changed.");
        for (int pass = 0; pass < 4; pass++) {
            try {
                // Keep public loads serialized, as SaveTexturePng does; only lazy bulk decoding runs in the background.
                foreach (string path in icons) {
                    var texture = provider.LoadAsset(path).OfType<UTexture>().Single();
                    tasks.Add(Task.Run(() => {
                        var decoded = texture.Decode();
                        Require(decoded?.Data.Length > 0, "Lazy texture decode returned no pixels.");
                    }));
                }
            } finally {
                await Task.WhenAll(tasks);
            }
        }

        Require(Handles(pak) == before + 1, "Lazy extraction opened unowned archive clones.");
        Console.WriteLine($"REAL lazy-bulk texture loads/decodes={tasks.Count}; "
            + $"live PAK handles={Handles(pak) - before}");
    }

    Require(Handles(pak) == before, "Lazy texture extraction leaked the caller-owned PAK handle.");
}

static Exception Catch(Action action) {
    try {
        action();
    } catch (Exception exception) {
        return exception;
    }

    throw new InvalidOperationException("Expected an exception from this failure fixture.");
}

static int Handles(string path) => Directory.EnumerateFiles("/proc/self/fd").Count(
    file => new FileInfo(file).LinkTarget == path
);

static void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
    }
}