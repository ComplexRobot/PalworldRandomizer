using Newtonsoft.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;

internal static class AreaSelectionsTests {
    internal static async Task RunAsync(SettingsCatalog catalog, Func<string, Func<Task>, Task> check) {
        await check("Area Selections exposes the 13 original Booleans with catalog text", () => CategoryAsync(catalog));
        await check("Area Selections edits persist exact YAML and preserve Area Group Type values",
            () => PersistenceAsync(catalog));
    }

    private static async Task CategoryAsync(SettingsCatalog catalog) {
        var session = await RandomizerViewModel.CreateAsync(catalog, new MemoryStore().Create());
        Require(session.AreaSelections.Count == 13,
            "Area Selections did not expose the 13 original FormData Booleans.");
        string[] expected = [
            "RandomizeField", "RandomizeDungeons", "RandomizeDungeonBosses", "RandomizeFieldBosses",
            "RandomizePredators", "RandomizeCages", "RandomizeEggs", "RandomizeQuests", "RandomizeMimics",
            "RandomizeAllArea", "RandomizeSingleSpawns", "RandomizeHumanBosses", "RandomizeHumanBossesPals",
        ];
        Require(session.AreaSelections.Select(selection => selection.Name).SequenceEqual(expected),
            "The Area Selections names or original order changed.");
        AreaSelectionViewModel field = session.AreaSelections.Single(selection => selection.Name == "RandomizeField");
        Require(field.Label == "Overworld Pals"
            && field.Description == "Affects all spawns outside of dungeons except for bosses.",
            "The Overworld Pals catalog label or description changed.");
        AreaSelectionViewModel dungeons = session.AreaSelections
            .Single(selection => selection.Name == "RandomizeDungeons");
        Require(dungeons.Label == "Dungeon Pals"
            && dungeons.Description == "Affects all spawns inside dungeons except for bosses.",
            "The Dungeon Pals catalog label or description changed.");
        await Task.CompletedTask;
    }

    private static async Task PersistenceAsync(SettingsCatalog catalog) {
        var store = new MemoryStore();
        var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());

        // Change Area Group Type first so the Area Selections edit can prove it leaves those values alone.
        session.MethodFull = false;
        session.MethodLocalSwap = true;
        await session.WaitForSaveAsync();

        session.SelectedCategory = "AreaSelections";
        Require(session.SelectedCategory == "AreaSelections" && session.IsAreaSelectionsSelected
            && !session.IsAreaGroupTypeSelected, "The category switch did not take effect.");
        Require(session.CategoryTitle == "Area Selections", "The category title did not follow the switch.");

        AreaSelectionViewModel field = session.AreaSelections.Single(selection => selection.Name == "RandomizeField");
        AreaSelectionViewModel dungeons = session.AreaSelections
            .Single(selection => selection.Name == "RandomizeDungeons");
        field.IsSelected = false;
        dungeons.IsSelected = false;
        Require(!session.Values.RandomizeField && !session.Values.RandomizeDungeons,
            "The actual Area Selections rows did not update the shared FormData.");
        Require(!field.IsSelected && !dungeons.IsSelected,
            "The Area Selections row bindings did not reflect the shared FormData.");
        Require(session.MethodLocalSwap && !session.MethodFull && !session.MethodCustomSize
            && !session.MethodGlobalSwap && !session.MethodNone,
            "The Area Selections edit changed Area Group Type values.");

        await session.WaitForSaveAsync();
        Require(store.LastYaml is not null, "The Area Selections edit did not save Default.yaml.");
        FormData reloaded = TemplateYaml.Deserialize(store.LastYaml!);
        Require(!reloaded.RandomizeField && !reloaded.RandomizeDungeons,
            "The YAML reload lost the Area Selections edit.");
        Require(reloaded.MethodLocalSwap && !reloaded.MethodFull && !reloaded.MethodCustomSize
            && !reloaded.MethodGlobalSwap && !reloaded.MethodNone,
            "The YAML reload changed Area Group Type values.");
        Require(JsonConvert.SerializeObject(reloaded) == JsonConvert.SerializeObject(session.Values),
            "The YAML reload was not exact across all FormData members.");

        RandomizerState captured = session.CaptureState();
        Require(captured.SelectedCategory == "AreaSelections",
            "The recovery checkpoint did not keep the Area Selections category.");
        RandomizerState.Validate(captured);
        var reopened = await RandomizerViewModel.CreateAsync(catalog, store.Create());
        Require(!reopened.Values.RandomizeField && !reopened.Values.RandomizeDungeons
            && reopened.Values.MethodLocalSwap,
            "Reopening Default.yaml did not restore the edited values.");
        reopened.RestoreState(captured);
        Require(reopened.SelectedCategory == "AreaSelections" && reopened.IsAreaSelectionsSelected,
            "Recovery did not restore the Area Selections category.");
        Require(!reopened.Values.RandomizeField && !reopened.Values.RandomizeDungeons,
            "Recovery did not restore Area Selections values.");
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}