using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.Win32;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Randomizer.PalSpawn;
using static PalworldRandomizer.Serializer.FileModify;

namespace PalworldRandomizer.Window;

/// <summary>Windows file dialogs and editor updates for spawn-file import and export.</summary>
public static class SpawnFileDialogs {
    /// <summary>Prompts for a destination and writes the supplied spawn areas as CSV.</summary>
    public static void SaveCSV(List<AreaData> areaList) {
        SaveFileDialog saveDialog = new()
        {
            FileName = $"PalworldSpawns-{DateTime.Now:MM-dd-yy-HH-mm-ss}",
            DefaultExt = ".csv",
            Filter = "Comma-separated values|*.csv|All files|*.*",
        };
        if (saveDialog.ShowDialog() == true) {
            File.WriteAllText(saveDialog.FileName, GenerateCSV(areaList), Encoding.UTF8);
        }
    }

    /// <summary>Prompts for a CSV file and replaces the editor areas with its contents.</summary>
    public static string? LoadCSV() {
        OpenFileDialog openDialog = new()
        {
            DefaultExt = ".csv",
            Filter = "Comma-separated values|*.csv|All files|*.*",
        };
        if (openDialog.ShowDialog() == true && openDialog.FileName != string.Empty) {
            try {
                PalSpawnPage.SaveBackup();
                PalSpawnPage.Instance.areaList.ItemsSource = ConvertCSV(openDialog.FileName);
                Randomize.AreaListChanged = true;
            } catch (Exception e) {
                if (e.Source == "FileIO") {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }

                return e.Message;
            }
        } else {
            return "Cancel";
        }

        return null;
    }

    /// <summary>Prompts for a destination and captures export settings only after confirmation.</summary>
    /// <param name="areaList">The modified areas selected for export.</param>
    /// <param name="captureSettings">Captures settings on the UI thread only when the export is confirmed.</param>
    /// <param name="selectDestination">Optional destination selection; null uses the native Windows dialog.
    /// A selector returning null cancels without capturing settings or creating an archive.</param>
    public static void SavePalSchema(
        List<AreaData> areaList,
        Func<FormData> captureSettings,
        Func<string?>? selectDestination = null
    ) {
        string? filename = (selectDestination ?? SelectPalSchemaDestination)();
        if (filename != null) {
            WritePalSchema(filename, areaList, captureSettings());
        }
    }

    private static string? SelectPalSchemaDestination() {
        SaveFileDialog saveDialog = new()
        {
            FileName = $"PalworldSpawns-PalSchema-{DateTime.Now:MM-dd-yy-HH-mm-ss}",
            DefaultExt = ".zip",
            Filter = "ZIP Archive|*.zip",
        };

        return saveDialog.ShowDialog() == true ? saveDialog.FileName : null;
    }

    /// <summary>Prompts for a PalSchema archive and loads it into the spawn editor.</summary>
    public static string? LoadPalSchema() {
        OpenFileDialog openDialog = new()
        {
            DefaultExt = ".zip",
            Filter = "Zip Archive|*.zip|JSON file|*.json|All files|*.*",
        };

        if (openDialog.ShowDialog() != true || openDialog.FileName.Length == 0) {
            return "Cancel";
        }

        try {
            List<AreaData> areaList = Data.AreaDataCopy();

            if (string.Equals(Path.GetExtension(openDialog.FileName), ".zip", StringComparison.OrdinalIgnoreCase)) {
                ConvertPalSchemaZIP(areaList, openDialog.FileName);
            } else {
                ConvertPalSchemaJSON(areaList, File.ReadAllText(openDialog.FileName, Encoding.UTF8));
            }

            Data.AreaForEachIfDiff(areaList, x => x.Modified = true);
            areaList.Sort(AreaSortFunc);

            PalSpawnPage.SaveBackup();
            PalSpawnPage.Instance.areaList.ItemsSource = areaList;
            Randomize.AreaListChanged = true;
        } catch (Exception e) {
            return e.Message;
        }

        return null;
    }
}