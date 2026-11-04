using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PalworldRandomizer.Desktop;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Views;
using Ursa.Controls;

// Exercises the actual production App, launcher row and compiled Randomizer page without a QA fixture page.
internal static class RandomizerAppTests {
    internal static int Main() {
        try {
            Run();
            Console.WriteLine("PASS Actual App launcher opens the shared Randomizer page and edits controls");
            Console.WriteLine("1/1 Randomizer app checks passed.");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine("FAIL Actual App launcher opens the shared Randomizer page and edits controls: "
                + exception.GetBaseException().Message);
            Console.WriteLine("0/1 Randomizer app checks passed.");
            return 1;
        }
    }

    private static void Run() {
        var catalog = SettingsCatalog.LoadEmbedded();
        var store = new MemoryStore();
        var randomizer = RandomizerViewModel.CreateAsync(catalog, store.Create()).GetAwaiter().GetResult();
        var preferences = new AppPreferences {
            SetupCompleted = true,
            PakSelection = "selected.pak",
            MappingSelection = "Mappings.usmap",
        };
        var setup = new SetupViewModel(preferences, _ => Task.CompletedTask,
            (_, _, _) => Task.FromResult(new ArchiveCompatibility(1, 1, "0.6.8")),
            (_, value) => !string.IsNullOrWhiteSpace(value), false);
        var app = new PalworldRandomizer.App {
            Setup = setup, Randomizer = randomizer, PageAvailable = DesktopPagePolicy.IsAvailable,
        };
        AppBuilder.Configure(() => app).UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
        Window window = app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } main }
            ? main : new Window { Width = 1280, Height = 940, Content = app.Shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button launcher = LauncherButton(app);
        Require(launcher.IsEnabled, "The real launcher Randomizer row is disabled.");
        launcher.Command?.Execute(launcher.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        RandomizerView page = app.Shell.GetVisualDescendants().OfType<RandomizerView>().Single();
        Require(ReferenceEquals(page.DataContext, randomizer), "The page did not use the shared session.");
        Require(app.ShellState.SelectedTab.Section == "category.AreaGroupType",
            "The Randomizer tab did not record its category route.");
        Require(page.GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.Text == "Not available in this build"),
            "Other categories are not visibly unavailable.");

        NumericIntUpDown spawn = page.FindControl<NumericIntUpDown>("SpawnListSizeBox")!;
        NumericIntUpDown chance = page.FindControl<NumericIntUpDown>("VanillaPlusChanceBox")!;
        Require(spawn.AllowDrag && spawn.Minimum == 1, "The Ursa numeric drag or minimum changed.");
        Require(chance.Minimum == 1 && chance.Maximum == 99, "The vanilla probability range changed.");

        RadioButton custom = page.FindControl<RadioButton>("MethodCustomSizeRadio")!;
        custom.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Require(randomizer.MethodCustomSize && !randomizer.MethodFull,
            "Selecting the custom method in the actual page did not update the shared session.");

        ToggleSwitch vanillaPlus = page.FindControl<ToggleSwitch>("VanillaPlusToggle")!;
        vanillaPlus.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Require(randomizer.VanillaPlus, "The actual vanilla toggle did not update the shared session.");
        Require(page.FindControl<Border>("VanillaPlusFilterRow")!.IsVisible,
            "The vanilla filter row did not appear for the enabled toggle.");

        RadioButton none = page.FindControl<RadioButton>("MethodNoneRadio")!;
        none.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Require(randomizer.MethodNone, "Selecting No Randomization failed in the actual page.");
        Require(!page.FindControl<Border>("VanillaPlusRow")!.IsVisible
            && !page.FindControl<Border>("VanillaPlusChanceRow")!.IsVisible
            && !page.FindControl<Border>("VanillaMergeRow")!.IsVisible,
            "No Randomization did not collapse the vanilla rows in the actual page.");

        spawn.Value = 77;
        Dispatcher.UIThread.RunJobs();
        Require(randomizer.SpawnListSize == 77, "The actual Ursa numeric did not update the session.");

        bool groupNone = randomizer.MethodNone;
        bool groupFull = randomizer.MethodFull;
        bool groupCustom = randomizer.MethodCustomSize;
        Button areaSelections = page.GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button)
                == "Randomizer.Category.AreaSelections");
        areaSelections.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Require(randomizer.SelectedCategory == "AreaSelections",
            "The actual sidebar did not switch to Area Selections.");
        Require(page.FindControl<StackPanel>("AreaSelectionsPanel")!.IsVisible,
            "The Area Selections panel did not appear.");
        Require(!page.FindControl<StackPanel>("AreaGroupTypePanel")!.IsVisible,
            "The Area Group Type panel did not hide.");
        ToggleSwitch fieldToggle = page.GetVisualDescendants().OfType<ToggleSwitch>()
            .Single(toggle => (toggle.Tag as string) == "RandomizeField");
        ToggleSwitch dungeonToggle = page.GetVisualDescendants().OfType<ToggleSwitch>()
            .Single(toggle => (toggle.Tag as string) == "RandomizeDungeons");
        fieldToggle.IsChecked = false;
        dungeonToggle.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Require(!randomizer.Values.RandomizeField && !randomizer.Values.RandomizeDungeons,
            "The actual Area Selections toggles did not update the shared FormData.");
        Require(randomizer.MethodNone == groupNone && randomizer.MethodFull == groupFull
            && randomizer.MethodCustomSize == groupCustom,
            "Area Selections changed Area Group Type values.");
        randomizer.WaitForSaveAsync().GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        Require(store.Yaml is not null, "The actual Area Selections edit was not persisted.");
        FormData reloaded = TemplateYaml.Deserialize(store.Yaml!);
        Require(!reloaded.RandomizeField && !reloaded.RandomizeDungeons,
            "The actual page save lost the Area Selections edit.");
        Require(reloaded.MethodNone == groupNone && reloaded.MethodFull == groupFull
            && reloaded.MethodCustomSize == groupCustom,
            "The actual page save changed Area Group Type values.");
        Require(app.ShellState.SelectedTab.Section == "category.AreaGroupType",
            "The category switch unexpectedly changed the tab route.");

        var second = app.ShellState.OpenTab();
        Dispatcher.UIThread.RunJobs();
        launcher = LauncherButton(app);
        launcher.Command?.Execute(launcher.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        RandomizerView secondPage = app.Shell.GetVisualDescendants().OfType<RandomizerView>().Single();
        Require(ReferenceEquals(secondPage.DataContext, randomizer),
            "A second tab did not reuse the shared Default session.");
        Require(second.Section == "category.AreaGroupType", "The second tab lost its category route.");
        Require(secondPage.FindControl<StackPanel>("AreaSelectionsPanel")!.IsVisible,
            "The second tab did not reuse the shared Area Selections category.");
        Require(randomizer.SpawnListSize == 77, "The shared session value diverged between tabs.");
        Require(!randomizer.Values.RandomizeField && !randomizer.Values.RandomizeDungeons,
            "Shared Area Selections values diverged between tabs.");
        randomizer.WaitForSaveAsync().GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        Require(store.Yaml is not null, "The actual page edits were not persisted.");
        CorruptTemplateDisablesControls(catalog);

        Button plus = app.Shell.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetAutomationId(button) == "Shell.AddTab");
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, plus));
        Dispatcher.UIThread.RunJobs();
        Button generate = app.Shell.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetAutomationId(button) == "Launcher.Generate");
        Require(generate.IsEnabled, "The real Desktop Generate launcher is disabled.");
        generate.Command?.Execute(generate.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        Require(app.Shell.GetVisualDescendants().OfType<GenerateView>().Any(),
            "The actual launcher did not open the real Generate page.");
    }

    private static void CorruptTemplateDisablesControls(SettingsCatalog catalog) {
        int writes = 0;
        var corruptStore = new TemplateStore(
            () => Task.FromResult<string?>("unsupportedSetting: true"),
            () => Task.FromResult<string?>(null),
            _ => {
                writes++;
                return Task.CompletedTask;
            });
        var model = RandomizerViewModel.CreateAsync(catalog, corruptStore).GetAwaiter().GetResult();
        Require(!model.IsEditingEnabled && model.HasError, "The corrupt template fixture did not fail loading.");
        var page = new RandomizerView(model);
        var window = new Window { Width = 1280, Height = 940, Content = page };
        try {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Require(!page.FindControl<RadioButton>("MethodFullRadio")!.IsEffectivelyEnabled,
                "The actual method control remains editable after template load failed.");
            model.SelectedCategory = "AreaSelections";
            Dispatcher.UIThread.RunJobs();
            ToggleSwitch field = page.GetVisualDescendants().OfType<ToggleSwitch>()
                .Single(toggle => (toggle.Tag as string) == "RandomizeField");
            Require(!field.IsEffectivelyEnabled, "The actual area toggle remains editable after template load failed.");
            Require(writes == 0, "Opening an invalid template triggered a save.");
        } finally {
            window.Close();
        }
    }

    private static Button LauncherButton(PalworldRandomizer.App app) => app.Shell.GetVisualDescendants()
        .OfType<Button>()
        .First(button => AutomationProperties.GetAutomationId(button) == "Launcher.Randomizer");

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MemoryStore {
        public string? Yaml { get; private set; }

        public TemplateStore Create() => new(
            () => Task.FromResult(Yaml),
            () => Task.FromResult<string?>(null),
            text => {
                Yaml = text;
                return Task.CompletedTask;
            });
    }
}