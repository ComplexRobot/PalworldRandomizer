using System.Reflection;
using Newtonsoft.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;

// Real-seam checks for the first Randomizer category. Each check uses the production catalog, session and codec.
var catalog = SettingsCatalog.LoadEmbedded();
string tempRoot = Path.Combine(Path.GetTempPath(), "randomizer-tests-" + Guid.NewGuid());
Directory.CreateDirectory(tempRoot);
int failures = 0;
int total = 0;
try {
    await Check("Default template exposes the original 11 Area Group Type defaults", Defaults);
    await Check("User method selection remains exclusive across the five methods", MethodExclusivity);
    await Check("Conflicting imported methods are displayed without normalizing FormData", ImportedMethodConflict);
    await Check("Original conditional visibility follows No Randomization and vanilla toggles", ConditionalVisibility);
    await Check("Area Group Type and Area Selections are functional while other categories remain unavailable", CategoryAvailability);
    await AreaSelectionsTests.RunAsync(catalog, Check);
    await Check("Catalog numeric bounds and the 11 English labels remain exact", CatalogEvidence);
    await Check("YAML preserves every FormData member, array and decimal through a round trip", YamlRoundTrip);
    await Check("Unsupported YAML members are rejected instead of silently defaulted", YamlUnknownMember);
    await Check("Legacy Default.json loads through TemplateJson and remains untouched", LegacyTemplate);
    await Check("A corrupt Default.yaml stays untouched and editing stays unavailable", CorruptTemplate);
    await Check("A failed save stays visible and retains live work", SaveFailure);
    await Check("Rapid edits coalesce into a bounded number of ordered saves", CoalescedSave);
    await Check("Recovery capture is detached and includes raw drafts", RecoveryCapture);
    await Check("Restoring a checkpoint never auto-saves and later edits still save", RestoreWithoutAutosave);
    await Check("A pending save safely refuses recovery capture", PendingSaveRefusesCapture);
    await Check("RandomizerState rejects malformed checkpoints and round-trips through JSON", RecoveryStateShape);
    await Check("Randomizer navigation uses the per-tab category section route", TabSectionRoute);
} finally {
    if (Directory.Exists(tempRoot)) {
        Directory.Delete(tempRoot, true);
    }
}

Console.WriteLine($"{total - failures}/{total} Randomizer checks passed.");
return failures == 0 ? 0 : 1;

async Task Check(string name, Func<Task> run) {
    total++;
    try {
        await run();
        Console.WriteLine("PASS " + name);
    } catch (Exception exception) {
        failures++;
        Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
    }
}

static void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
    }
}

static void RequireThrows<T>(Action action, string message) where T : Exception {
    try {
        action();
    } catch (T) {
        return;
    } catch (Exception exception) {
        throw new InvalidOperationException(message + " (saw " + exception.GetType().Name + ")", exception);
    }

    throw new InvalidOperationException(message);
}

static async Task WaitUntil(Func<bool> condition, string message) {
    for (int attempt = 0; attempt < 100 && !condition(); attempt++) {
        await Task.Delay(20);
    }

    Require(condition(), message);
}

async Task Defaults() {
    var store = new MemoryStore();
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    Require(session.IsEditingEnabled, "An absent template must allow editing.");
    Require(session.TemplateName == "Default.yaml", "The truthful template name changed.");
    Require(session.SaveStatus == "No saved Default template yet", "The absent save state changed.");
    Require(store.Commits == 0, "Loading an absent template must not save.");
    Require(session.MethodFull && !session.MethodCustomSize && !session.MethodLocalSwap
        && !session.MethodGlobalSwap && !session.MethodNone, "The method defaults changed.");
    Require(session.SpawnListSize == 50 && session.VanillaPlusChance == 50
        && !session.VanillaPlus && !session.VanillaMerge && session.VanillaPlusFilter
        && session.VanillaMergeFilter, "The vanilla or custom-size defaults changed.");
    Require(session.SelectedCategory == "AreaGroupType", "The initial category changed.");
    Require(session.AreaSelections.Count == 13
        && session.AreaSelections.All(selection => selection.IsSelected
            == (selection.Name != "RandomizeQuests" && selection.Name != "RandomizeMimics")),
        "The Area Selections defaults changed.");
    Require(!session.HasError && !session.HasPendingSave, "A fresh session must be clean.");
}

async Task MethodExclusivity() {
    var store = new MemoryStore();
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    session.MethodLocalSwap = true;
    Require(!session.MethodFull && session.MethodLocalSwap && !session.MethodCustomSize
        && !session.MethodGlobalSwap && !session.MethodNone, "Selecting local swap did not clear others.");
    session.MethodNone = true;
    Require(session.MethodNone && !session.MethodLocalSwap && !session.MethodFull
        && !session.MethodCustomSize && !session.MethodGlobalSwap, "Selecting no randomization failed.");
    session.MethodFull = true;
    Require(session.MethodFull && !session.MethodNone && !session.MethodLocalSwap
        && !session.MethodCustomSize && !session.MethodGlobalSwap, "Selecting full randomization failed.");
    await session.WaitForSaveAsync();
}

async Task ImportedMethodConflict() {
    FormData conflict = new() { MethodFull = true, MethodLocalSwap = true };
    var store = new MemoryStore { Yaml = TemplateYaml.Serialize(conflict) };
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    Require(session.MethodFull && session.MethodLocalSwap,
        "Loading must display conflicting imported methods without normalizing FormData.");
    session.MethodGlobalSwap = true;
    Require(session.MethodGlobalSwap && !session.MethodFull && !session.MethodLocalSwap,
        "A user method selection must clear every other imported method.");
    await session.WaitForSaveAsync();
}

async Task ConditionalVisibility() {
    var session = await RandomizerViewModel.CreateAsync(catalog, new MemoryStore().Create());
    Require(session.ShowVanillaPlus && session.ShowVanillaPlusChance && session.ShowVanillaMerge,
        "Vanilla rows must be visible before No Randomization.");
    session.MethodNone = true;
    Require(!session.ShowVanillaPlus && !session.ShowVanillaPlusChance && !session.ShowVanillaMerge
        && !session.ShowVanillaPlusFilter && !session.ShowVanillaMergeFilter,
        "No Randomization must collapse every vanilla option.");
    session.MethodNone = false;
    session.VanillaPlus = true;
    session.VanillaMerge = true;
    Require(session.ShowVanillaPlusFilter && session.ShowVanillaMergeFilter,
        "Vanilla filters must appear only when their parent toggle is enabled.");
    await session.WaitForSaveAsync();
}

async Task CategoryAvailability() {
    var session = await RandomizerViewModel.CreateAsync(catalog, new MemoryStore().Create());
    Require(session.Categories.Count(category => category.IsAvailable) == 2,
        "Area Group Type and Area Selections must be the only functional categories.");
    var area = session.Categories.Single(category => category.Key == "AreaGroupType");
    Require(area.IsAvailable && area.Title == "Area Group Type", "The Area Group Type category changed.");
    var selections = session.Categories.Single(category => category.Key == "AreaSelections");
    Require(selections.IsAvailable && selections.Title == "Area Selections",
        "The Area Selections category changed.");
    session.SelectedCategory = "AreaSelections";
    Require(session.SelectedCategory == "AreaSelections" && session.IsAreaSelectionsSelected,
        "The Area Selections category could not be selected.");
    session.SelectedCategory = "Randomize";
    Require(session.SelectedCategory == "AreaSelections", "An unavailable category must be ignored.");
}

async Task CatalogEvidence() {
    var session = await RandomizerViewModel.CreateAsync(catalog, new MemoryStore().Create());
    Require(session.SpawnListSizeMinimum == 1 && session.SpawnListSizeFallback == 50,
        "The custom group count minimum or fallback changed.");
    Require(session.VanillaPlusChanceMinimum == 1 && session.VanillaPlusChanceMaximum == 99
        && session.VanillaPlusChanceFallback == 50, "The vanilla probability range changed.");
    Require(session.MethodFullLabel == "All Species Everywhere", "The full method label changed.");
    Require(session.MethodFullDescription == "All possible spawns will have a chance to appear in all areas. "
        + "Random groups will be added until all species are in every spawn table.",
        "The full method description changed.");
    Require(session.MethodCustomSizeLabel == "Custom Groups Count (Per Area):"
        && session.SpawnListSizeLabel == "Custom Groups Count (Per Area):", "The custom count labels changed.");
    Require(session.MethodLocalSwapLabel == "Match Vanilla Groups Count (Per Area)"
        && session.MethodGlobalSwapLabel == "Global Species Swap"
        && session.MethodNoneLabel == "No Randomization", "A method label changed.");
    Require(session.VanillaPlusLabel == "Add To Vanilla With Probability:"
        && session.VanillaPlusChanceLabel == "Add To Vanilla With Probability:"
        && session.VanillaMergeLabel == "Merge Vanilla"
        && session.VanillaPlusFilterLabel == "Filter Vanilla Before Adding"
        && session.VanillaMergeFilterLabel == "Filter Vanilla Before Merging",
        "A vanilla label changed.");
}

async Task YamlRoundTrip() {
    FormData original = NonDefaultFormData();
    int propertyCount = typeof(FormData).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length;
    Require(propertyCount == 137, "The FormData contract is no longer 137 properties.");
    string yaml = TemplateYaml.Serialize(original);
    var loaded = TemplateYaml.Deserialize(yaml);
    Require(JsonConvert.SerializeObject(loaded) == JsonConvert.SerializeObject(original),
        "The YAML round trip lost a FormData value, array or decimal.");
    await Task.CompletedTask;
}

async Task YamlUnknownMember() {
    string yaml = TemplateYaml.Serialize(new FormData()) + "\r\nUnknownMember: true";
    RequireThrows<InvalidDataException>(() => TemplateYaml.Deserialize(yaml),
        "An unknown YAML member must be rejected.");
    await Task.CompletedTask;
}

async Task LegacyTemplate() {
    string directory = Path.Combine(tempRoot, "legacy");
    Directory.CreateDirectory(directory);
    string jsonPath = Path.Combine(directory, "Default.json");
    FormData legacy = new() {
        MethodFull = false,
        MethodLocalSwap = true,
        SpawnListSize = 7,
        RandomizeField = false,
        HumanDamageCap = 2.5m,
        WeightCustom = [3, 2, 1],
    };
    File.WriteAllText(jsonPath, JsonConvert.SerializeObject(legacy, Formatting.Indented));
    byte[] before = File.ReadAllBytes(jsonPath);
    var native = TemplateStore.CreateNative(directory);
    TemplateSnapshot snapshot = await native.LoadAsync();
    Require(snapshot.Source == TemplateSource.LegacyJson, "The legacy source was not reported.");
    Require(!snapshot.Values.MethodFull && snapshot.Values.MethodLocalSwap && snapshot.Values.SpawnListSize == 7
        && !snapshot.Values.RandomizeField && snapshot.Values.HumanDamageCap == 2.5m
        && snapshot.Values.WeightCustom.SequenceEqual([3, 2, 1]),
        "The legacy JSON values were not loaded through the compatible loader.");
    Require(!File.Exists(Path.Combine(directory, "Default.yaml")), "Loading legacy JSON wrote YAML.");
    await native.SaveAsync(snapshot.Values);
    Require(File.Exists(Path.Combine(directory, "Default.yaml")), "An edit did not create Default.yaml.");
    Require(File.ReadAllBytes(jsonPath).SequenceEqual(before), "The legacy JSON original was modified.");
    TemplateSnapshot reopened = await TemplateStore.CreateNative(directory).LoadAsync();
    Require(reopened.Source == TemplateSource.Yaml && reopened.Values.SpawnListSize == 7
        && reopened.Values.HumanDamageCap == 2.5m, "The saved YAML did not reopen losslessly.");
}

async Task CorruptTemplate() {
    string directory = Path.Combine(tempRoot, "corrupt");
    Directory.CreateDirectory(directory);
    string yamlPath = Path.Combine(directory, "Default.yaml");
    File.WriteAllText(yamlPath, "This: [is not valid settings YAML");
    string before = File.ReadAllText(yamlPath);
    var session = await RandomizerViewModel.CreateAsync(catalog, TemplateStore.CreateNative(directory));
    Require(!session.IsEditingEnabled && session.HasError && session.SaveStatus == "Unavailable",
        "A corrupt template did not disable editing with a visible error.");
    session.MethodNone = true;
    await Task.Delay(50);
    Require(File.ReadAllText(yamlPath) == before, "A corrupt template was silently overwritten.");
}

async Task SaveFailure() {
    var store = new MemoryStore { BeforeCommit = _ => throw new IOException("quota") };
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    session.MethodLocalSwap = true;
    await WaitUntil(() => session.SaveStatus == "Save failed", "A save failure was not surfaced.");
    Require(session.HasError && session.ErrorMessage!.Contains("quota"), "The save failure detail was lost.");
    Require(session.MethodLocalSwap && session.HasPendingSave, "A failed save discarded live work.");
    store.BeforeCommit = null;
    session.MethodFull = true;
    await session.WaitForSaveAsync();
    Require(session.SaveStatus == "Saved" && !session.HasError, "A later save did not recover.");
    Require(store.LastYaml is not null && TemplateYaml.Deserialize(store.LastYaml).MethodFull,
        "The recovered save did not contain the latest work.");
}

async Task CoalescedSave() {
    var store = new MemoryStore { BeforeCommit = async _ => await Task.Delay(80) };
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    session.MethodLocalSwap = true;
    session.MethodGlobalSwap = true;
    session.MethodNone = true;
    await session.WaitForSaveAsync();
    Require(store.Attempts <= 2, "Rapid edits produced an unbounded write queue.");
    FormData last = TemplateYaml.Deserialize(store.LastYaml!);
    Require(last.MethodNone && !last.MethodLocalSwap && !last.MethodGlobalSwap,
        "The final coalesced save did not contain the latest values.");
}

async Task RecoveryCapture() {
    var store = new MemoryStore();
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    session.MethodLocalSwap = true;
    session.SpawnListSize = 77;
    session.VanillaPlusChance = 33;
    session.Values.HumanDamageCap = 1.234567890123456789m;
    session.SetSpawnListSizeText("77");
    session.SetVanillaPlusChanceText("abc");
    await session.WaitForSaveAsync();
    RandomizerState captured = session.CaptureState();
    Require(captured.SpawnListSizeText == "77" && captured.VanillaPlusChanceText == "abc",
        "The raw numeric drafts were not captured.");
    Require(captured.Values.HumanDamageCap == 1.234567890123456789m,
        "The recovery capture lost decimal precision.");
    captured.Values.MethodFull = true;
    Require(!session.MethodFull, "The recovery capture was not detached from the live session.");
}

async Task RestoreWithoutAutosave() {
    var store = new MemoryStore();
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    session.MethodLocalSwap = true;
    session.SpawnListSize = 77;
    session.VanillaPlusChance = 33;
    session.Values.HumanDamageCap = 1.234567890123456789m;
    session.SetSpawnListSizeText("77");
    session.SetVanillaPlusChanceText("abc");
    await session.WaitForSaveAsync();
    int commits = store.Commits;
    RandomizerState captured = session.CaptureState();
    var fresh = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    fresh.RestoreState(captured);
    Require(fresh.MethodLocalSwap && fresh.SpawnListSize == 77 && fresh.VanillaPlusChance == 33,
        "Recovery did not restore the committed values.");
    Require(fresh.SpawnListSizeText == "77" && fresh.VanillaPlusChanceText == "abc",
        "Recovery did not restore the raw drafts.");
    Require(store.Commits == commits, "Recovery restore triggered an automatic save.");
    Require(fresh.SaveStatus == "Restored", "Recovery did not report its restored state.");
    fresh.MethodFull = true;
    await fresh.WaitForSaveAsync();
    Require(store.Commits > commits, "A user edit after recovery did not save.");
}

async Task PendingSaveRefusesCapture() {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var store = new MemoryStore { BeforeCommit = _ => gate.Task };
    var session = await RandomizerViewModel.CreateAsync(catalog, store.Create());
    session.MethodLocalSwap = true;
    await WaitUntil(() => session.HasPendingSave, "The test edit did not start a save.");
    RequireThrows<InvalidOperationException>(() => session.CaptureState(),
        "A pending save must refuse recovery capture instead of claiming quiescence.");
    gate.SetResult();
    await session.WaitForSaveAsync();
}

async Task RecoveryStateShape() {
    RandomizerState valid = new(RandomizerState.CurrentSchema, new FormData(), "50", "50", "AreaGroupType");
    RandomizerState.Validate(valid);
    RandomizerState.Validate(valid with { SelectedCategory = "AreaSelections" });
    RequireThrows<InvalidDataException>(() => RandomizerState.Validate(null),
        "A missing checkpoint must be refused.");
    RequireThrows<InvalidDataException>(() => RandomizerState.Validate(valid with { Schema = 99 }),
        "An unsupported checkpoint schema must be refused.");
    RequireThrows<InvalidDataException>(() => RandomizerState.Validate(valid with { SelectedCategory = "Randomize" }),
        "An unsupported checkpoint category must be refused.");
    RequireThrows<InvalidDataException>(() => RandomizerState.Validate(valid with { SpawnListSizeText = null! }),
        "A checkpoint without raw drafts must be refused.");
    string json = System.Text.Json.JsonSerializer.Serialize(valid);
    RandomizerState? roundTrip = System.Text.Json.JsonSerializer.Deserialize<RandomizerState>(json);
    RandomizerState.Validate(roundTrip);
    Require(roundTrip!.Values.MethodFull
        && roundTrip.SelectedCategory == "AreaGroupType",
        "The recovery checkpoint did not round-trip through JSON.");
    await Task.CompletedTask;
}

async Task TabSectionRoute() {
    var shell = new ShellViewModel(firstRun: false);
    var launcher = new LauncherViewModel(shell);
    LauncherDestination randomizer = launcher.Destinations
        .Single(destination => destination.Page == AppPage.Randomizer);
    launcher.NavigateCommand.Execute(randomizer);
    Require(shell.SelectedTab.Page == AppPage.Randomizer
        && shell.SelectedTab.Section == "category.AreaGroupType",
        "Randomizer navigation did not use the per-tab section route.");
    await Task.CompletedTask;
}

static FormData NonDefaultFormData() {
    var values = new FormData();
    foreach (PropertyInfo property in typeof(FormData).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
        if (!property.CanWrite) {
            continue;
        }

        object? current = property.GetValue(values);
        object? next = current switch {
            bool flag => !flag,
            int number => number + 17,
            decimal number => number + 0.125m,
            string text => text + " changed",
            int[] numbers => numbers.Select(number => number + 3).ToArray(),
            _ => throw new InvalidOperationException("Unexpected FormData member " + property.Name),
        };
        property.SetValue(values, next);
    }

    return values;
}

internal sealed class MemoryStore {
    public string? Yaml { get; set; }
    public string? Legacy { get; set; }
    public int Attempts { get; private set; }
    public int Commits { get; private set; }
    public string? LastYaml { get; private set; }
    public Func<string, Task>? BeforeCommit { get; set; }

    public TemplateStore Create() => new(
        () => Task.FromResult(Yaml),
        () => Task.FromResult(Legacy),
        async text => {
            Attempts++;
            if (BeforeCommit is not null) {
                await BeforeCommit(text);
            }

            Yaml = text;
            LastYaml = text;
            Commits++;
        });
}