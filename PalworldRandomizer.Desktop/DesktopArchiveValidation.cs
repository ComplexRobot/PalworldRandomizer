using CUE4Parse.MappingsProvider.Usmap;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.Desktop;

/// <summary>Checks native files off the UI thread without initializing application resources or icons.</summary>
public static class DesktopArchiveValidation {
    /// <summary>Serially reads a representative table and awaits provider disposal before returning.</summary>
    public static Task<ArchiveCompatibility> ValidateAsync(string pak, string mapping, string previousVersion) =>
        Task.Run(async () => {
            // Bound metadata work before the unchanged loader initializes mappings ahead of mounting the PAK.
            try {
                if (new FileInfo(mapping).Length is <= 0 or > 16 * 1024 * 1024) {
                    throw new InvalidDataException("USMAP size exceeds the supported metadata bound.");
                }

                _ = new FileUsmapTypeMappingsProvider(mapping);
            } catch (Exception exception) {
                throw new ArchiveCompatibilityException("setup.mappingIncompatible", exception);
            }

            VfsFileProvider provider;
            try {
                provider = await UAssetData.OpenArchiveAsync(pak, mapping, previousVersion).ConfigureAwait(false);
            } catch (Exception exception) {
                throw new ArchiveCompatibilityException("setup.openFailed", exception);
            }

            await using (provider.ConfigureAwait(false)) {
                try {
                    var rows = provider.LoadDataTable(
                        "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
                    foreach (var row in rows.Values) {
                        _ = new CharacterData(row.Properties);
                    }

                    return new ArchiveCompatibility(provider.Files.Count, rows.Count, provider.GameVersion);
                } catch (Exception exception) {
                    throw new ArchiveCompatibilityException(
                        "setup.mappingIncompatible", exception, provider.GameVersion);
                }
            }
        });
}