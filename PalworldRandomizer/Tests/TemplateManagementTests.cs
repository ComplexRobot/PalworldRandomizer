using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;

/// <summary>Real-seam checks for the multi-template Randomizer page contract.</summary>
internal static class TemplateManagementTests {
    internal static async Task RunAsync(SettingsCatalog catalog,
        Func<string, Func<Task>, Task> check) {
        await check("Template store lists Default first and creates a virtual Default when empty",
            ListAsync);
        await check("Template store creates separate templates and saves independently",
            CreateAndSaveAsync);
        await check("Template store rejects duplicate names case-insensitively and unsafe paths",
            ValidationAsync);
        await check("Template store renames templates and updates the on-disk path",
            RenameAsync);
        await check("Template store deletes templates and recreates Default when none remain",
            DeleteAsync);
        await check("Template store imports and exports YAML and JSON",
            ImportExportAsync);
        await check("Randomizer session creates, selects, renames, deletes and undoes templates",
            () => ViewModelCrudAsync(catalog));
        await check("Randomizer session imports and exports templates",
            () => ViewModelImportExportAsync(catalog));
    }

    private static async Task ListAsync() {
        string root = NewRoot();
        try {
            var store = TemplateStore.CreateNative(root);
            IReadOnlyList<TemplateEntry> entries = await store.ListAsync();
            Require(entries.Count == 1, "An empty Templates folder must expose one Default entry.");
            Require(entries[0].Name == TemplateStore.DefaultName,
                "The first template must be named Default.");
            Require(!entries[0].FileExists,
                "The first Default template is virtual until its first save.");
            TemplateSnapshot snapshot = await store.LoadAsync();
            Require(snapshot.Source == TemplateSource.Default,
                "An empty folder must load the original FormData defaults.");
            Require(store.Name == TemplateStore.DefaultName,
                "The active template name must be Default.");
            Require(snapshot.Values.MethodFull && snapshot.Values.SpawnListSize == 50,
                "The original FormData defaults changed.");
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static async Task CreateAndSaveAsync() {
        string root = NewRoot();
        try {
            var store = TemplateStore.CreateNative(root);
            var second = new FormData {
                MethodFull = false,
                MethodNone = true,
                SpawnListSize = 77,
                VanillaPlusChance = 12,
            };
            TemplateEntry created = await store.CreateAsync("Second", second);
            Require(created.Name == "Second", "The created template name changed.");
            Require(File.Exists(created.FilePath), "The created template file is missing.");

            IReadOnlyList<TemplateEntry> entries = await store.ListAsync();
            Require(entries.Count == 2, "Expected Default and Second.");
            Require(entries[0].Name == TemplateStore.DefaultName && entries[1].Name == "Second",
                "Default must remain first in the selector list.");

            TemplateSnapshot secondSnapshot = await store.LoadAsync("Second");
            Require(!secondSnapshot.Values.MethodFull && secondSnapshot.Values.MethodNone
                && secondSnapshot.Values.SpawnListSize == 77
                && secondSnapshot.Values.VanillaPlusChance == 12,
                "Second template values did not round-trip.");

            TemplateSnapshot defaultSnapshot = await store.LoadAsync("Default");
            Require(defaultSnapshot.Values.MethodFull && defaultSnapshot.Values.SpawnListSize == 50,
                "Creating Second must not change Default.");

            var updated = new FormData {
                MethodFull = false,
                MethodCustomSize = true,
                SpawnListSize = 88,
            };
            await store.SaveAsync("Second", updated);
            TemplateSnapshot reloaded = await store.LoadAsync("Second");
            Require(reloaded.Values.MethodCustomSize && reloaded.Values.SpawnListSize == 88,
                "Saving Second did not replace its YAML.");
            Require((await store.LoadAsync("Default")).Values.SpawnListSize == 50,
                "Saving Second must not change Default.");
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static async Task ValidationAsync() {
        Require(TemplateStore.ValidateName(null) is not null,
            "A null template name must be rejected.");
        Require(TemplateStore.ValidateName("   ") is not null,
            "A whitespace template name must be rejected.");
        Require(TemplateStore.ValidateName("C:\\temp\\escape") is not null,
            "An absolute template path must be rejected.");
        Require(TemplateStore.ValidateName("..\\escape") is not null,
            "A parent-directory template name must be rejected.");
        Require(TemplateStore.ValidateName("folder/../escape") is not null,
            "An embedded parent-directory name must be rejected.");
        Require(TemplateStore.ValidateName("Nested/Template") is null,
            "A safe nested template name must be accepted.");
        Require(TemplateStore.ValidateName("CON") is not null,
            "A reserved device name must be rejected.");
        Require(TemplateStore.ValidateName("CON.yaml") is not null,
            "A reserved device name with its extension must be rejected.");

        string root = NewRoot();
        try {
            var store = TemplateStore.CreateNative(root);
            await store.CreateAsync("Alpha");
            bool rejected = false;
            try {
                await store.CreateAsync("alpha");
            } catch (InvalidDataException) {
                rejected = true;
            }

            Require(rejected, "A case-insensitive duplicate template name must be rejected.");
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static async Task RenameAsync() {
        string root = NewRoot();
        try {
            var store = TemplateStore.CreateNative(root);
            var values = new FormData {
                MethodFull = false,
                MethodCustomSize = true,
                SpawnListSize = 99,
            };
            await store.CreateAsync("Original", values);
            TemplateEntry renamed = await store.RenameAsync("Original", "Renamed", values);
            Require(renamed.Name == "Renamed", "The renamed template name changed.");
            Require(!File.Exists(Path.Combine(root, "Original.yaml")),
                "The old template file must be removed.");
            Require(File.Exists(Path.Combine(root, "Renamed.yaml")),
                "The new template file must exist.");
            Require((await store.LoadAsync("Renamed")).Values.SpawnListSize == 99,
                "Renaming must preserve the template values.");

            await store.RenameAsync("Renamed", "RENAMED", values);
            string[] files = Directory.GetFiles(root, "*.yaml");
            Require(files.Any(path => Path.GetFileName(path) == "RENAMED.yaml"),
                "A case-only rename must update the file name.");
            Require(!files.Any(path => Path.GetFileName(path) == "Renamed.yaml"),
                "A case-only rename must remove the previous file name.");

            await store.CreateAsync("Other");
            bool rejected = false;
            try {
                await store.RenameAsync("RENAMED", "Other", values);
            } catch (InvalidDataException) {
                rejected = true;
            }

            Require(rejected, "Renaming onto an existing template must be rejected.");
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static async Task DeleteAsync() {
        string root = NewRoot();
        try {
            var store = TemplateStore.CreateNative(root);
            await store.CreateAsync("Alpha", new FormData { SpawnListSize = 11 });
            await store.CreateAsync("Beta", new FormData { SpawnListSize = 22 });

            TemplateDeleteResult alpha = await store.DeleteAsync("Alpha");
            Require(!alpha.DefaultRecreated,
                "Default must remain when other templates still exist.");
            Require(!File.Exists(Path.Combine(root, "Alpha.yaml")),
                "Deleting Alpha must remove its file.");
            Require((await store.ListAsync()).Count == 2,
                "Expected Default and Beta after deleting Alpha.");

            await store.DeleteAsync("Beta");
            TemplateDeleteResult last = await store.DeleteAsync(TemplateStore.DefaultName);
            Require(last.DefaultRecreated,
                "Deleting the last template must recreate Default.");
            Require(File.Exists(Path.Combine(root, "Default.yaml")),
                "The recreated Default file is missing.");
            TemplateSnapshot recreated = await store.LoadAsync(TemplateStore.DefaultName);
            Require(recreated.Values.MethodFull && recreated.Values.SpawnListSize == 50,
                "The recreated Default must use the original FormData defaults.");
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static async Task ImportExportAsync() {
        string root = NewRoot();
        string exportRoot = NewRoot();
        try {
            var store = TemplateStore.CreateNative(root);
            var values = new FormData {
                MethodFull = false,
                MethodNone = true,
                SpawnListSize = 42,
                VanillaPlusChance = 7,
            };
            await store.CreateAsync("ExportMe", values);

            string yamlPath = Path.Combine(exportRoot, "exported.yaml");
            string jsonPath = Path.Combine(exportRoot, "exported.json");
            await store.ExportAsync("ExportMe", yamlPath, false);
            await store.ExportAsync("ExportMe", jsonPath, true);

            string yamlText = await File.ReadAllTextAsync(yamlPath);
            string jsonText = await File.ReadAllTextAsync(jsonPath);
            Require(yamlText.Contains("SpawnListSize", StringComparison.Ordinal),
                "The YAML export is missing FormData members.");
            Require(jsonText.TrimStart().StartsWith('{'),
                "The JSON export must be a JSON object.");
            Require(jsonText.Contains("\"SpawnListSize\"", StringComparison.Ordinal),
                "The JSON export is missing FormData members.");

            TemplateEntry importedYaml = await store.ImportAsync(yamlPath, "ImportedYaml");
            Require(importedYaml.Name == "ImportedYaml", "The YAML import name changed.");
            TemplateSnapshot yamlSnapshot = await store.LoadAsync("ImportedYaml");
            Require(!yamlSnapshot.Values.MethodFull && yamlSnapshot.Values.MethodNone
                && yamlSnapshot.Values.SpawnListSize == 42
                && yamlSnapshot.Values.VanillaPlusChance == 7,
                "The imported YAML values changed.");

            TemplateEntry importedJson = await store.ImportAsync(jsonPath, "ImportedJson");
            Require(importedJson.Name == "ImportedJson", "The JSON import name changed.");
            TemplateSnapshot jsonSnapshot = await store.LoadAsync("ImportedJson");
            Require(!jsonSnapshot.Values.MethodFull && jsonSnapshot.Values.MethodNone
                && jsonSnapshot.Values.SpawnListSize == 42
                && jsonSnapshot.Values.VanillaPlusChance == 7,
                "The imported JSON values changed.");

            bool duplicateRejected = false;
            try {
                await store.ImportAsync(yamlPath, "ImportedYaml");
            } catch (InvalidDataException) {
                duplicateRejected = true;
            }

            Require(duplicateRejected, "Importing a duplicate template name must be rejected.");

            string invalidPath = Path.Combine(exportRoot, "invalid.yaml");
            await File.WriteAllTextAsync(invalidPath, "UnsupportedMember: true");
            bool invalidRejected = false;
            try {
                await store.ImportAsync(invalidPath);
            } catch (InvalidDataException) {
                invalidRejected = true;
            }

            Require(invalidRejected, "Importing an unsupported YAML document must be rejected.");
        } finally {
            Directory.Delete(root, true);
            Directory.Delete(exportRoot, true);
        }
    }

    private static async Task ViewModelCrudAsync(SettingsCatalog catalog) {
        string root = NewRoot();
        try {
            var session = await RandomizerViewModel.CreateAsync(catalog, TemplateStore.CreateNative(root));
            Require(session.IsEditingEnabled && !session.HasError,
                "A fresh session over an empty Templates folder must allow editing.");
            Require(session.TemplateName == TemplateStore.DefaultName,
                "A fresh session must start on Default.");
            Require(session.TemplateNames.Count == 1
                && session.TemplateNames[0] == TemplateStore.DefaultName,
                "A fresh session must expose only Default.");

            Require(await session.CreateTemplateAsync("Alpha"),
                "Creating Alpha failed.");
            Require(session.TemplateName == "Alpha",
                "Creating Alpha must select it.");
            Require(session.TemplateNames.Contains("Alpha") && session.TemplateNames.Count == 2,
                "The selector list did not add Alpha.");
            Require(session.SaveStatus == "Saved",
                "A new template must report the saved state.");

            session.MethodFull = false;
            session.MethodNone = true;
            session.SpawnListSize = 77;
            await session.WaitForSaveAsync();
            Require(!session.HasPendingSave && session.Values.SpawnListSize == 77,
                "Alpha values did not save.");

            Require(await session.CreateTemplateAsync("Beta"),
                "Creating Beta failed.");
            Require(session.Values.SpawnListSize == 50 && session.Values.MethodFull,
                "A new template must use the original FormData defaults.");

            Require(await session.SelectTemplateAsync("Alpha"),
                "Selecting Alpha failed.");
            Require(session.Values.SpawnListSize == 77 && session.Values.MethodNone
                && !session.Values.MethodFull,
                "Selecting Alpha did not load its values.");

            Require(await session.RenameTemplateAsync("Gamma"),
                "Renaming Alpha to Gamma failed.");
            Require(session.TemplateName == "Gamma",
                "The active template name did not update after rename.");
            Require(!session.TemplateNames.Contains("Alpha")
                && session.TemplateNames.Contains("Gamma"),
                "The selector list did not update after rename.");
            Require(File.Exists(Path.Combine(root, "Gamma.yaml")),
                "The renamed Gamma file is missing.");
            Require(!File.Exists(Path.Combine(root, "Alpha.yaml")),
                "The old Alpha file must be removed after rename.");

            Require(await session.DeleteTemplateAsync(),
                "Deleting Gamma failed.");
            Require(!session.TemplateNames.Contains("Gamma"),
                "The deleted Gamma name remained in the selector list.");
            Require(session.CanUndoDelete,
                "Deleting a template must offer the temporary undo.");
            Require(session.TemplateName != "Gamma",
                "Deleting the active template must select another template.");

            Require(await session.UndoDeleteAsync(),
                "Undoing the Gamma delete failed.");
            Require(session.TemplateNames.Contains("Gamma"),
                "Undo did not restore the Gamma name.");
            Require(session.TemplateName == "Gamma",
                "Undo did not select the restored Gamma template.");
            Require(session.Values.SpawnListSize == 77 && session.Values.MethodNone,
                "Undo did not restore the deleted Gamma values.");
            Require(!session.CanUndoDelete,
                "The undo action must be consumed after a successful restore.");

            while (session.TemplateNames.Count > 1) {
                Require(await session.SelectTemplateAsync(session.TemplateNames[1]),
                    "Selecting the next template for cleanup failed.");
                Require(await session.DeleteTemplateAsync(),
                    "Deleting a template during cleanup failed.");
            }

            Require(await session.DeleteTemplateAsync(),
                "Deleting the final template failed.");
            Require(session.TemplateNames.Count == 1
                && session.TemplateNames[0] == TemplateStore.DefaultName,
                "Deleting every template must recreate Default.");
            Require(session.Values.SpawnListSize == 50 && session.Values.MethodFull,
                "The recreated Default values changed.");
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static async Task ViewModelImportExportAsync(SettingsCatalog catalog) {
        string root = NewRoot();
        string exportRoot = NewRoot();
        try {
            var session = await RandomizerViewModel.CreateAsync(catalog, TemplateStore.CreateNative(root));
            session.MethodFull = false;
            session.MethodNone = true;
            session.SpawnListSize = 63;
            await session.WaitForSaveAsync();

            string yamlPath = Path.Combine(exportRoot, "session.yaml");
            string jsonPath = Path.Combine(exportRoot, "session.json");
            Require(await session.ExportTemplateAsync(yamlPath, false),
                "Exporting the session as YAML failed.");
            Require(await session.ExportTemplateAsync(jsonPath, true),
                "Exporting the session as JSON failed.");
            Require(File.Exists(yamlPath) && File.Exists(jsonPath),
                "The exported YAML or JSON file is missing.");
            Require((await File.ReadAllTextAsync(jsonPath)).Contains("\"SpawnListSize\"",
                    StringComparison.Ordinal),
                "The JSON export is missing FormData members.");

            Require(await session.ImportTemplateAsync(yamlPath, "Imported"),
                "Importing the YAML template failed.");
            Require(session.TemplateName == "Imported",
                "Importing must select the new template.");
            Require(session.TemplateNames.Contains("Imported"),
                "The selector list did not add the imported template.");
            Require(session.Values.SpawnListSize == 63 && session.Values.MethodNone
                && !session.Values.MethodFull,
                "The imported YAML values changed.");

            Require(await session.ImportTemplateAsync(jsonPath, "ImportedJson"),
                "Importing the JSON template failed.");
            Require(session.TemplateName == "ImportedJson",
                "Importing JSON must select the new template.");
            Require(session.Values.SpawnListSize == 63 && session.Values.MethodNone,
                "The imported JSON values changed.");

            bool duplicateRejected = !await session.ImportTemplateAsync(yamlPath, "Imported");
            Require(duplicateRejected && session.HasError,
                "Importing a duplicate name must fail and set the page error.");
        } finally {
            Directory.Delete(root, true);
            Directory.Delete(exportRoot, true);
        }
    }

    private static string NewRoot() {
        string root = Path.Combine(Path.GetTempPath(), "randomizer-template-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}
