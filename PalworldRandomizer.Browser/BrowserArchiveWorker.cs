using System.Runtime.ExceptionServices;
using System.Text;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Readers;
using IniParser.Parser;
using Newtonsoft.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using SpawnDev.SpawnJS;
using BrowserFile = SpawnDev.SpawnJS.JSObjects.File;

namespace PalworldRandomizer.Browser;

/// <summary>The actual platform RPC boundary; only a small summary crosses back to the UI.</summary>
public interface IBrowserArchiveWorker {
    /// <summary>Checks the selected browser File references without copying the PAK.</summary>
    Task<string> ValidateAsync(BrowserFile pak, BrowserFile mapping, string previousVersion);
}

/// <summary>Runs synchronous CUE reads only inside the approved package's dedicated worker.</summary>
public sealed class BrowserArchiveWorker : IBrowserArchiveWorker {
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