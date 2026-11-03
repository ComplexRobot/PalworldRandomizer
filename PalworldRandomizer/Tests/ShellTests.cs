using System.ComponentModel;
using System.Text.Json;
using PalworldRandomizer.ViewModels;

// Exercises real UI-independent shell behavior without a desktop runtime or game assets.
(string Name, Action Run)[] tests = [
    ("Initial shell selects Settings only on first run", InitialShell),
    ("New-tab opens and selects a stable independently identified launcher", OpenLauncher),
    ("Opening a destination creates a selected tab without replacing another", OpenDestination),
    ("Settings gear reuses a tab currently showing Settings", ReuseSettings),
    ("Within-tab navigation records independent page and section histories", IndependentNavigation),
    ("Tab history back forward and jump leave other tabs unchanged", HistoryTraversal),
    ("Duplicate navigation is a no-op and a new branch truncates forward history", HistoryBranch),
    ("Close retains an in-place placeholder with a thirty-second deadline", ClosePlaceholder),
    ("Undo restores the same tab identity history and slot before expiry", UndoClose),
    ("Undo cannot resurrect a tab at or after its exact deadline", UndoExpiry),
    ("Existing shell interactions prune expired placeholders before acting", InteractionExpiry),
    ("Closing the last live tab creates a usable launcher without removing its placeholder", CloseLast),
    ("Reorder moves a member to the exact index without changing selection or histories", ReorderTabs),
    ("Overflow promotion selects the exact requested live tab and moves it first", OverflowPromotion),
    ("Selection and layout changes notify bindings while rejecting non-live selections", ShellBindings),
    ("Page titles are locale keys rather than embedded visible labels", LocaleKeys),
    ("Settings reuse ignores placeholders and historical Settings entries", LiveSettingsOnly),
    ("Captured state records order selection histories mode and close deadline", CaptureSnapshot),
    ("Restore recreates tab order identities histories selection and layout", RestoreSnapshot),
    ("Snapshot and restore own detached arrays in both directions", DetachedSnapshots),
    ("Restore normalizes unknown persisted enum values without losing valid history", CorruptEnums),
    ("Restoring an empty or entirely closed state always provides a live launcher", RestoreUsability),
    ("Restore prunes expired placeholders without extending unexpired deadlines", RestoredExpiry),
    ("Restore repairs empty histories and out-of-range positions", CorruptHistory),
    ("Closed placeholders cannot traverse history or advertise traversal capabilities", ClosedHistory),
    ("All later shell interaction paths prune expired placeholders before work", AllInteractionExpiry),
    ("Restore repairs duplicate or missing tab identities", CorruptIdentity),
    ("Restore tolerates null collections and entries from corrupt persisted data", NullPersistedData),
    ("Snapshot data round-trips through JSON and remains independently usable", JsonRoundTrip),
    ("Null restore is rejected before changing the current shell", RejectNullRestore),
    ("Prune reports only expired slots and leaves unexpired Undo usable", PruneCounts),
    ("Navigation and placeholder changes notify dependent binding properties", DependentNotifications),
];
int failures = 0;
foreach (var (name, run) in tests) {
    try {
        run();
        Console.WriteLine($"PASS {name}");
    } catch (Exception exception) {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.GetBaseException().Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} shell checks passed.");
return failures == 0 ? 0 : 1;

// First-run policy selects Settings once; later sessions start at the launcher.
static void InitialShell() {
    Require(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(ShellViewModel)),
        "Shell must notify bound properties.");
    foreach (bool firstRun in new[] { true, false }) {
        var shell = new ShellViewModel(firstRun: firstRun);
        Require(shell.SelectedTab.Page == (firstRun ? AppPage.Settings : AppPage.NewTab),
            "Initial page differs from first-run policy.");
        Require(shell.Tabs.Count == 1 && shell.Tabs[0] == shell.SelectedTab,
            "Initial selection must belong to tabs.");
    }
}

// Verifies the plus-button contract without requiring a rendered tab strip.
static void OpenLauncher() {
    var shell = new ShellViewModel(firstRun: false);
    var original = shell.SelectedTab;
    var opened = shell.OpenTab();
    Require(shell.Tabs.Count == 2 && shell.SelectedTab == opened, "Plus must add and select a tab.");
    Require(opened.Page == AppPage.NewTab, "Plus must open the launcher.");
    var id = opened.Id;
    Require(id != Guid.Empty && id != original.Id, "Tab IDs must be unique.");
    Require(id == opened.Id, "Reading identity must not generate a new ID.");
}

// Explicit page opens create a tab; within-tab navigation is a different action.
static void OpenDestination() {
    var shell = new ShellViewModel(firstRun: false);
    var original = shell.SelectedTab;
    var tab = shell.OpenPage(AppPage.Randomizer);
    Require(tab.Page == AppPage.Randomizer && shell.SelectedTab == tab, "Destination must open selected.");
    Require(shell.Tabs.Count == 2 && original.Page == AppPage.NewTab, "Opening must not replace another tab.");
}

// Gear activation searches current destinations, not historical Settings visits.
static void ReuseSettings() {
    var shell = new ShellViewModel();
    var settings = shell.SelectedTab;
    shell.OpenPage(AppPage.Randomizer);
    Require(ReferenceEquals(shell.OpenSettings(), settings), "Gear must reuse current Settings tab.");
    Require(shell.SelectedTab == settings && shell.Tabs.Count == 2, "Gear must select without duplication.");
    var fresh = new ShellViewModel(firstRun: false);
    var opened = fresh.OpenSettings();
    Require(opened.Page == AppPage.Settings && fresh.Tabs.Count == 2, "Gear must open missing Settings.");
    fresh.OpenSettings();
    Require(fresh.Tabs.Count == 2, "Repeated gear activation must be idempotent.");
    var selectedSettings = fresh.OpenPage(AppPage.Settings);
    Require(fresh.OpenSettings() == selectedSettings, "Gear must keep an already selected Settings tab.");
}

// Both tab history and property notifications must follow the selected tab only.
static void IndependentNavigation() {
    var shell = new ShellViewModel(firstRun: false);
    var first = shell.SelectedTab;
    List<string?> changes = [];
    first.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    Require(first.Page == AppPage.Randomizer, "Selected tab must navigate.");
    Require(first.Section == "Randomizer.Category.Levels", "Section must be retained.");
    var second = shell.OpenPage(AppPage.Generate);
    shell.NavigateSelected(AppPage.SpawnEditor);
    Require(first.Page == AppPage.Randomizer && second.Page == AppPage.SpawnEditor, "Histories must be independent.");
    Require(first.History.Count == 2, "First history must be intact.");
    Require(second.History.Count == 2, "Second history must be separate.");
    Require(changes.Contains("Page") && changes.Contains("Section"), "Navigation must notify bindings.");
}

// Traversal reports boundaries, restores sections, and never leaks to another tab.
static void HistoryTraversal() {
    var shell = new ShellViewModel(firstRun: false);
    var tab = shell.SelectedTab;
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    shell.NavigateSelected(AppPage.Generate);
    var other = shell.OpenPage(AppPage.SpawnEditor);
    Require(tab.Back() && tab.Page == AppPage.Randomizer, "Back must visit previous entry.");
    Require(tab.Section == "Randomizer.Category.Levels", "Back must restore category.");
    Require(tab.Forward() && tab.Page == AppPage.Generate, "Forward must restore entry.");
    Require(!tab.Forward(), "Forward at end must be a no-op.");
    Require(tab.JumpHistory(0) && tab.Page == AppPage.NewTab, "Jump must use absolute history index.");
    Require(!tab.Back() && !tab.JumpHistory(-1), "Invalid traversal must be a no-op.");
    Require(!tab.JumpHistory(99) && other.Page == AppPage.SpawnEditor, "Other history must remain intact.");
    Require(tab.CanGoForward, "Forward capability must follow index.");
    Require(!tab.CanGoBack, "Back capability must follow index.");
}

// Revisiting the current address does not destroy forward history; new navigation does.
static void HistoryBranch() {
    var shell = new ShellViewModel(firstRun: false);
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    shell.NavigateSelected(AppPage.Generate);
    var tab = shell.SelectedTab;
    tab.Back();
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    Require(tab.History.Count == 3 && tab.CanGoForward, "Duplicate navigation must preserve the forward branch.");
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Stats");
    Require(tab.History.Count == 3 && !tab.CanGoForward, "Different category must replace the forward branch.");
    Require(tab.History[^1] == new HistoryEntry(AppPage.Randomizer, "Randomizer.Category.Stats"),
        "The new address must be appended after truncation.");
    Require(((System.Collections.IList)tab.History).IsReadOnly, "Public history must not permit edits.");
}

// A closed tab stays in the strip and a different live tab receives selection.
static void ClosePlaceholder() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var shell = new ShellViewModel(clock, firstRun: false);
    var tab = shell.OpenPage(AppPage.Randomizer);
    shell.NavigateSelected(AppPage.Generate);
    int index = shell.Tabs.IndexOf(tab);
    Require(shell.CloseTab(tab), "Closing a live member must succeed.");
    Require(tab.IsClosed && shell.Tabs[index] == tab, "Close must retain the same slot.");
    Require(tab.UndoUntil == clock.UtcNow.AddSeconds(30), "Undo deadline must be 30s.");
    Require(shell.SelectedTab != tab && shell.SelectedTab.Page == AppPage.NewTab, "Closed tab cannot stay selected.");
    Require(tab.History.Count == 2 && !shell.CloseTab(tab), "Closing twice must be a no-op.");
    Require(!shell.CloseTab(new ShellTab(AppPage.Settings)), "Foreign tabs must be ignored.");
}

// Undo selects the original object, including its current history position.
static void UndoClose() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var shell = new ShellViewModel(clock, firstRun: false);
    var tab = shell.OpenPage(AppPage.Randomizer);
    shell.NavigateSelected(AppPage.Generate);
    tab.Back();
    var id = tab.Id;
    shell.CloseTab(tab);
    clock.Advance(TimeSpan.FromSeconds(29));
    Require(shell.UndoClose(tab), "Undo must succeed before expiry.");
    Require(!tab.IsClosed && tab.UndoUntil is null && tab.Id == id, "Undo must preserve identity and clear deadline.");
    Require(shell.SelectedTab == tab && shell.Tabs[1] == tab, "Undo must restore selection and slot.");
    Require(tab.Page == AppPage.Randomizer && tab.CanGoForward, "Undo must restore full history position.");
    Require(!shell.UndoClose(tab), "Undo of a live tab must be a no-op.");
}

// Expiry uses an exclusive boundary and does not depend on a UI timer firing.
static void UndoExpiry() {
    foreach (int elapsedSeconds in new[] { 30, 31 }) {
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var shell = new ShellViewModel(clock, firstRun: false);
        var tab = shell.OpenPage(AppPage.Randomizer);
        shell.CloseTab(tab);
        clock.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        Require(!shell.UndoClose(tab), "Expired Undo must never resurrect a tab.");
        Require(!shell.Tabs.Contains(tab), "Expired placeholders must be removed before Undo.");
    }
}

// No particular UI timer or operation is needed to remove an expired placeholder.
static void InteractionExpiry() {
    Action<ShellViewModel>[] interactions = [
        shell => shell.OpenTab(),
        shell => shell.OpenPage(AppPage.Generate),
        shell => shell.OpenSettings(),
        shell => shell.NavigateSelected(AppPage.SpawnEditor),
        shell => shell.CloseTab(shell.Tabs.First(tab => !tab.IsClosed && tab != shell.SelectedTab)),
        shell => shell.UndoClose(new ShellTab(AppPage.Settings)),
        shell => shell.PruneClosedTabs(),
    ];
    foreach (var interact in interactions) {
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var shell = new ShellViewModel(clock, firstRun: false);
        var expired = shell.OpenPage(AppPage.Settings);
        shell.CloseTab(expired);
        shell.OpenTab();
        clock.Advance(TimeSpan.FromSeconds(30));
        interact(shell);
        Require(!shell.Tabs.Contains(expired), "Every interaction must prune expired placeholders first.");
        Require(!shell.SelectedTab.IsClosed && shell.Tabs.Contains(shell.SelectedTab),
            "Selection must remain usable.");
    }
}

// Even repeated closes retain Undo slots while ensuring a usable live tab.
static void CloseLast() {
    var shell = new ShellViewModel(firstRun: false);
    for (int count = 0; count < 3; count++) {
        var closed = shell.SelectedTab;
        int index = shell.Tabs.IndexOf(closed);
        bool succeeded;
        try {
            succeeded = shell.CloseTab(closed);
        } catch (InvalidOperationException) {
            succeeded = false;
        }

        Require(succeeded, "Closing the last tab must succeed without an exception.");
        Require(shell.Tabs[index] == closed && closed.IsClosed, "Last tab must still have an Undo slot.");
        Require(shell.SelectedTab.Page == AppPage.NewTab && !shell.SelectedTab.IsClosed,
            "Last close needs a launcher.");
        Require(shell.Tabs.Count(tab => !tab.IsClosed) == 1, "There must be exactly one replacement live tab.");
    }
}

// Drag semantics operate on strip slots, including unexpired placeholders.
static void ReorderTabs() {
    var shell = new ShellViewModel(firstRun: false);
    var first = shell.SelectedTab;
    var second = shell.OpenPage(AppPage.Randomizer);
    var third = shell.OpenPage(AppPage.Generate);
    shell.CloseTab(second);
    Require(shell.ReorderTab(third, 0), "Reorder must move an existing tab.");
    Require(shell.Tabs.SequenceEqual([third, first, second]), "Move must use exact target index.");
    Require(shell.SelectedTab == third && second.IsClosed, "Reorder must preserve selection and placeholders.");
    Require(shell.ReorderTab(second, 1) && shell.Tabs[1] == second, "Placeholder slot can be moved.");
    Require(!shell.ReorderTab(third, -1) && !shell.ReorderTab(third, 99),
        "Invalid destination indices must be no-ops.");
    Require(!shell.ReorderTab(new ShellTab(AppPage.Settings), 0), "Foreign tab must be a no-op.");
    Require(third.Page == AppPage.Generate && second.Page == AppPage.Randomizer, "Move must preserve destinations.");
}

// Overflow geometry belongs to the view; state owns the exact move-and-select action.
static void OverflowPromotion() {
    var shell = new ShellViewModel(firstRun: false);
    var first = shell.SelectedTab;
    var second = shell.OpenPage(AppPage.Randomizer);
    var third = shell.OpenPage(AppPage.Generate);
    Require(shell.TabMode == TabStripMode.Wrap, "The initial tab mode must be Wrap.");
    shell.TabMode = TabStripMode.Shrink;
    Require(!shell.PromoteOverflow(second), "Promotion outside overflow mode must be a no-op.");
    shell.TabMode = TabStripMode.Overflow;
    Require(shell.PromoteOverflow(second), "Overflow member must be promoted.");
    Require(shell.SelectedTab == second && shell.Tabs.SequenceEqual([second, first, third]),
        "Exactly the requested tab must move first without disturbing relative order.");
    shell.CloseTab(first);
    Require(!shell.PromoteOverflow(first), "A placeholder cannot be promoted as a live tab.");
    Require(!shell.PromoteOverflow(new ShellTab(AppPage.Settings)), "Foreign tabs cannot be promoted.");
}

// The view can select a live tab directly without replacing its navigation state.
static void ShellBindings() {
    var shell = new ShellViewModel(firstRun: false);
    var first = shell.SelectedTab;
    var second = shell.OpenPage(AppPage.Generate);
    List<string?> changes = [];
    shell.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
    shell.SelectedTab = first;
    shell.TabMode = TabStripMode.Shrink;
    Require(shell.SelectedTab == first && changes.Contains("SelectedTab") && changes.Contains("TabMode"),
        "Shell selection and layout must notify bindings.");
    shell.CloseTab(second);
    shell.SelectedTab = second;
    Require(shell.SelectedTab == first, "Closed tabs cannot be selected.");
    shell.SelectedTab = new ShellTab(AppPage.Settings);
    shell.SelectedTab = null;
    Require(shell.SelectedTab == first, "Foreign or null selection cannot break the shell.");
}

// UI code resolves keys; navigation never owns translated display strings.
static void LocaleKeys() {
    var shell = new ShellViewModel(firstRun: false);
    foreach (var page in Enum.GetValues<AppPage>()) {
        shell.NavigateSelected(page);
        Require(shell.SelectedTab.TitleKey == $"Shell.Page.{page}",
            "Every page needs a stable locale key.");
    }
}

// Gear lookup considers only live tabs whose current address is Settings.
static void LiveSettingsOnly() {
    var shell = new ShellViewModel();
    var oldSettings = shell.SelectedTab;
    shell.NavigateSelected(AppPage.Randomizer);
    var currentSettings = shell.OpenSettings();
    Require(currentSettings != oldSettings, "A historical Settings visit is not a current Settings tab.");
    shell.CloseTab(currentSettings);
    var replacement = shell.OpenSettings();
    Require(replacement != currentSettings && !replacement.IsClosed, "Gear must not reuse a closed placeholder.");
    Require(shell.SelectedTab == replacement && shell.Tabs.Contains(currentSettings), "Gear must preserve Undo slot.");
}

// Capture produces a data-only snapshot with no live tab references.
static void CaptureSnapshot() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var shell = new ShellViewModel(clock, firstRun: false);
    var first = shell.SelectedTab;
    var tab = shell.OpenPage(AppPage.Randomizer);
    shell.NavigateSelected(AppPage.Generate, "Generate.Category.Output");
    tab.Back();
    shell.CloseTab(first);
    shell.ReorderTab(tab, 0);
    shell.TabMode = TabStripMode.Overflow;
    var state = shell.CaptureState();
    Require(state.SelectedTabId == tab.Id && state.TabMode == TabStripMode.Overflow,
        "Snapshot needs selection identity and layout mode.");
    Require(state.Tabs.Length == 2 && state.Tabs[0].Id == tab.Id, "Snapshot must retain strip order.");
    Require(state.Tabs[0].HistoryIndex == 0, "Snapshot must retain history position.");
    Require(state.Tabs[1].IsClosed && state.Tabs[1].UndoUntil == first.UndoUntil,
        "Snapshot must retain closed expiry.");
    Require(state.Tabs[0].History.Length == 2 && state.Tabs[0].History[1].Section == "Generate.Category.Output",
        "Snapshot needs full history.");
}

// A new shell receives every state field without sharing mutable history storage.
static void RestoreSnapshot() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var original = new ShellViewModel(clock, firstRun: false);
    var first = original.SelectedTab;
    var tab = original.OpenPage(AppPage.Randomizer);
    original.NavigateSelected(AppPage.Generate, "Generate.Category.Output");
    tab.Back();
    original.CloseTab(first);
    original.ReorderTab(tab, 0);
    original.TabMode = TabStripMode.Overflow;
    var state = original.CaptureState();
    var restored = new ShellViewModel(clock);
    restored.RestoreState(state);
    Require(restored.Tabs.Select(item => item.Id).SequenceEqual(original.Tabs.Select(item => item.Id)),
        "Restored strip order and identities must match.");
    Require(restored.SelectedTab.Id == tab.Id && restored.TabMode == TabStripMode.Overflow,
        "Restored selection and mode must match.");
    Require(!ReferenceEquals(restored.SelectedTab, tab), "Restore must create new live tab objects.");
    Require(restored.SelectedTab.History.SequenceEqual(tab.History) && restored.SelectedTab.HistoryIndex == 0,
        "Restored history and index must match.");
    Require(restored.Tabs[1].IsClosed && restored.Tabs[1].UndoUntil == first.UndoUntil,
        "Restored placeholder deadline must match.");
    Require(restored.SelectedTab.Forward() && restored.SelectedTab.Section == "Generate.Category.Output",
        "Restored forward history must be usable.");
}

// Changing a mutable DTO array cannot edit either live shell, and live edits cannot edit the DTO.
static void DetachedSnapshots() {
    var shell = new ShellViewModel(firstRun: false);
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    var snapshot = shell.CaptureState();
    var restored = new ShellViewModel();
    restored.RestoreState(snapshot);
    snapshot.Tabs[0].History[1] = new HistoryEntry(AppPage.Settings);
    Require(shell.SelectedTab.Page == AppPage.Randomizer && restored.SelectedTab.Page == AppPage.Randomizer,
        "DTO history mutation cannot change a live shell.");
    snapshot.Tabs[0] = new ShellTabState(Guid.NewGuid(), [new(AppPage.SpawnEditor)]);
    Require(restored.SelectedTab.Id == shell.SelectedTab.Id, "DTO tab replacement cannot change restored identity.");
    var fresh = shell.CaptureState();
    shell.NavigateSelected(AppPage.Generate);
    restored.NavigateSelected(AppPage.SpawnEditor);
    Require(fresh.Tabs[0].History.Length == 2 && fresh.Tabs[0].History[1].Page == AppPage.Randomizer,
        "Live navigation cannot mutate a captured DTO.");
    Require(!ReferenceEquals(shell.CaptureState().Tabs, shell.CaptureState().Tabs),
        "Each capture owns a new tab array.");
}

// Newer or corrupt persisted enum values fall back to a usable launcher and Wrap mode.
static void CorruptEnums() {
    var id = Guid.NewGuid();
    var state = new ShellState([
        new(id, [new(AppPage.Randomizer, "Randomizer.Category.Levels"), new((AppPage)999, "unknown")], 1),
    ], Guid.NewGuid(), (TabStripMode)999);
    var shell = new ShellViewModel();
    shell.RestoreState(state);
    Require(shell.TabMode == TabStripMode.Wrap, "Unknown persisted strip mode must fall back to Wrap.");
    Require(shell.SelectedTab.Id == id && shell.SelectedTab.Page == AppPage.NewTab
        && shell.SelectedTab.Section is null,
        "Unknown persisted page must fall back to a launcher without an invalid category.");
    Require(shell.SelectedTab.Back() && shell.SelectedTab.Page == AppPage.Randomizer,
        "Valid history entries must survive enum recovery.");
}

// Invalid selection IDs and empty/all-closed sessions cannot leave the shell unusable.
static void RestoreUsability() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var closedId = Guid.NewGuid();
    ShellState[] states = [
        new([]),
        new([new(closedId, [new(AppPage.Settings)], IsClosed: true, UndoUntil: clock.UtcNow.AddSeconds(20))], closedId),
    ];
    foreach (var state in states) {
        var shell = new ShellViewModel(clock);
        Exception? failure = null;
        try {
            shell.RestoreState(state);
        } catch (InvalidOperationException exception) {
            failure = exception;
        }

        Require(failure is null, "Restoring an empty/all-closed state must not throw.");
        Require(shell.SelectedTab.Page == AppPage.NewTab && !shell.SelectedTab.IsClosed,
            "Restoring without a live tab must create a launcher.");
        Require(shell.Tabs.Contains(shell.SelectedTab), "Fallback selection must belong to the strip.");
        if (state.Tabs.Length > 0) {
            Require(shell.Tabs[0].Id == closedId && shell.Tabs[0].IsClosed,
                "Unexpired placeholder must retain order.");
        }
    }
}

// Absolute UTC deadlines survive restart; downtime does not restart the Undo window.
static void RestoredExpiry() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var firstId = Guid.NewGuid();
    var secondId = Guid.NewGuid();
    var liveId = Guid.NewGuid();
    var state = new ShellState([
        new(firstId, [new(AppPage.Settings)], IsClosed: true, UndoUntil: clock.UtcNow),
        new(secondId, [new(AppPage.Randomizer)], IsClosed: true, UndoUntil: clock.UtcNow.AddSeconds(5)),
        new(liveId, [new(AppPage.Generate)]),
        new(Guid.NewGuid(), [new(AppPage.Settings)], IsClosed: true),
    ], firstId);
    var shell = new ShellViewModel(clock);
    shell.RestoreState(state);
    Require(shell.Tabs.Count == 2 && shell.Tabs[0].Id == secondId && shell.SelectedTab.Id == liveId,
        "Restore must drop expired or undated placeholders before choosing a live selection.");
    clock.Advance(TimeSpan.FromSeconds(5));
    Require(!shell.UndoClose(shell.Tabs[0]), "Restored deadline must not restart the Undo window.");
    Require(shell.Tabs.Count == 1 && shell.SelectedTab.Id == liveId, "Expired restored slot must be pruned.");
}

// Persisted history positions are clamped and empty histories become launchers.
static void CorruptHistory() {
    ShellState[] states = [
        new([new(Guid.NewGuid(), [], 99)]),
        new([new(Guid.NewGuid(), [new(AppPage.Randomizer), new(AppPage.Generate)], 99)]),
        new([new(Guid.NewGuid(), [new(AppPage.Randomizer)], -5)]),
    ];
    foreach (var state in states) {
        var shell = new ShellViewModel();
        shell.RestoreState(state);
        var tab = shell.SelectedTab;
        Require(tab.History.Count > 0 && tab.HistoryIndex >= 0 && tab.HistoryIndex < tab.History.Count,
            "Every restored tab must have a valid history position.");
        Require(Enum.IsDefined(tab.Page), "A recovered history must have a valid destination.");
        if (state.Tabs[0].History.Length == 0) {
            Require(tab.Page == AppPage.NewTab, "An empty history must become a launcher.");
        }
    }
}

// A retained Undo placeholder is not an active navigation surface.
static void ClosedHistory() {
    var shell = new ShellViewModel(firstRun: false);
    var tab = shell.OpenPage(AppPage.Randomizer);
    shell.NavigateSelected(AppPage.Generate);
    shell.CloseTab(tab);
    Require(!tab.CanGoBack && !tab.CanGoForward, "Closed placeholders must not advertise navigation.");
    Require(!tab.Back() && !tab.JumpHistory(0), "Closed placeholders must not navigate.");
    Require(tab.HistoryIndex == 1 && tab.Page == AppPage.Generate, "Closed history position must remain intact.");
    shell.UndoClose(tab);
    Require(tab.CanGoBack && tab.Back(), "Undo must restore history navigation capabilities.");
}

// Selection, tab mode, reorder, overflow and capture obey the same expiry gate.
static void AllInteractionExpiry() {
    Action<ShellViewModel>[] interactions = [
        shell => shell.SelectedTab = shell.SelectedTab,
        shell => shell.TabMode = TabStripMode.Wrap,
        shell => shell.ReorderTab(shell.SelectedTab, 0),
        shell => shell.PromoteOverflow(shell.SelectedTab),
        shell => shell.CaptureState(),
        shell => shell.SelectedTab.Back(),
        shell => shell.SelectedTab.Forward(),
        shell => shell.SelectedTab.JumpHistory(0),
    ];
    foreach (var interact in interactions) {
        var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var shell = new ShellViewModel(clock, firstRun: false);
        var expired = shell.OpenPage(AppPage.Settings);
        shell.CloseTab(expired);
        shell.NavigateSelected(AppPage.Randomizer);
        clock.Advance(TimeSpan.FromSeconds(30));
        interact(shell);
        Require(!shell.Tabs.Contains(expired), "Every shell or tab navigation interaction must prune expiry first.");
    }
}

// Persisted identities must stay unambiguous for selection and subsequent snapshots.
static void CorruptIdentity() {
    var id = Guid.NewGuid();
    var shell = new ShellViewModel();
    shell.RestoreState(new ShellState([
        new(id, [new(AppPage.Randomizer)]),
        new(id, [new(AppPage.Generate)]),
        new(Guid.Empty, [new(AppPage.SpawnEditor)]),
    ], id));
    Require(shell.Tabs.Select(tab => tab.Id).Distinct().Count() == 3 && shell.Tabs.All(tab => tab.Id != Guid.Empty),
        "Duplicate or empty persisted tab identities must be repaired.");
    Require(shell.SelectedTab == shell.Tabs[0] && shell.SelectedTab.Id == id,
        "The first valid occurrence retains identity and selection.");
}

// Converts unexpected restore exceptions into explicit malformed-input assertions.
static void RequireRestore(ShellViewModel shell, ShellState state) {
    Exception? failure = null;
    try {
        shell.RestoreState(state);
    } catch (Exception exception) {
        failure = exception;
    }

    Require(failure is null, $"Malformed persisted data must recover, not throw: {failure?.Message}");
}

// Nullable annotations do not prevent JSON files from containing explicit null values.
static void NullPersistedData() {
    ShellState[] states = [
        new(null!),
        new([null!]),
        new([new(Guid.NewGuid(), null!)]),
        new([new(Guid.NewGuid(), [null!])]),
    ];
    foreach (var state in states) {
        var shell = new ShellViewModel();
        RequireRestore(shell, state);
        Require(shell.SelectedTab.Page == AppPage.NewTab && shell.SelectedTab.History.Count == 1,
            "Null persisted collections or entries must recover to a launcher.");
    }
}

// Serializer compatibility is verified without adding persistence I/O to the shell.
static void JsonRoundTrip() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var original = new ShellViewModel(clock, firstRun: false);
    var first = original.SelectedTab;
    var tab = original.OpenPage(AppPage.Randomizer);
    original.NavigateSelected(AppPage.Generate, "Generate.Category.Output");
    original.CloseTab(first);
    original.TabMode = TabStripMode.Shrink;
    var state = original.CaptureState();
    string json = JsonSerializer.Serialize(state);
    var parsed = JsonSerializer.Deserialize<ShellState>(json)!;
    var restored = new ShellViewModel(clock);
    restored.RestoreState(parsed);
    Require(restored.SelectedTab.Id == tab.Id && restored.TabMode == TabStripMode.Shrink,
        "JSON round-trip must preserve selection and layout.");
    Require(restored.SelectedTab.History.SequenceEqual(tab.History) && restored.Tabs[0].UndoUntil == first.UndoUntil,
        "JSON round-trip must preserve full history and deadlines.");
    Require(restored.UndoClose(restored.Tabs[0]), "Restored JSON placeholder must still support Undo.");
    parsed.Tabs[1].History[1] = new HistoryEntry(AppPage.Settings);
    Require(restored.Tabs[1].Page == AppPage.Generate, "Deserialized arrays must detach during restore.");
}

// A missing top-level DTO is a caller error, unlike corrupt fields in a non-null DTO.
static void RejectNullRestore() {
    var shell = new ShellViewModel(firstRun: false);
    var original = shell.SelectedTab;
    Exception? failure = null;
    try {
        shell.RestoreState(null!);
    } catch (Exception exception) {
        failure = exception;
    }

    Require(failure is ArgumentNullException, "Null restore must report its argument contract.");
    Require(shell.Tabs.Count == 1 && shell.SelectedTab == original && shell.Tabs[0] == original,
        "Rejected restore must not change the current shell.");
}

// Count and boundary behavior let a UI host refresh its placeholder presentation.
static void PruneCounts() {
    var clock = new ManualClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var shell = new ShellViewModel(clock, firstRun: false);
    var first = shell.OpenPage(AppPage.Randomizer);
    shell.CloseTab(first);
    clock.Advance(TimeSpan.FromSeconds(5));
    var second = shell.OpenPage(AppPage.Generate);
    shell.CloseTab(second);
    clock.Advance(TimeSpan.FromSeconds(25));
    Require(shell.PruneClosedTabs() == 1 && !shell.Tabs.Contains(first) && shell.Tabs.Contains(second),
        "Prune must remove and report only the expired slot.");
    Require(shell.PruneClosedTabs() == 0 && shell.UndoClose(second), "Unexpired placeholder must remain restorable.");
}

// History menus and Undo controls can refresh from ordinary property-change events.
static void DependentNotifications() {
    var shell = new ShellViewModel(firstRun: false);
    var tab = shell.SelectedTab;
    List<string?> changes = [];
    tab.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    string[] navigation = ["Page", "TitleKey", "Section", "History", "HistoryIndex", "CanGoBack", "CanGoForward"];
    Require(navigation.All(changes.Contains), "Navigation must refresh every derived binding property.");
    changes.Clear();
    shell.NavigateSelected(AppPage.Randomizer, "Randomizer.Category.Levels");
    Require(changes.Count == 0, "Duplicate navigation must not emit spurious notifications.");
    shell.CloseTab(tab);
    string[] placeholder = ["IsClosed", "UndoUntil", "CanGoBack", "CanGoForward"];
    Require(placeholder.All(changes.Contains), "Close must refresh placeholder and navigation bindings.");
    changes.Clear();
    shell.UndoClose(tab);
    Require(placeholder.All(changes.Contains), "Undo must refresh placeholder and navigation bindings.");
}

// Fails with an explicit behavioral diagnostic instead of a framework dependency.
static void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
    }
}

/// <summary>Provides deterministic UTC deadlines without an external fake-time package.</summary>
internal sealed class ManualClock(DateTimeOffset now) : TimeProvider {
    /// <summary>Gets the controlled UTC instant.</summary>
    public DateTimeOffset UtcNow { get; private set; } = now;

    /// <summary>Returns the controlled UTC instant.</summary>
    public override DateTimeOffset GetUtcNow() => UtcNow;

    /// <summary>Advances time without sleeping.</summary>
    public void Advance(TimeSpan amount) => UtcNow += amount;
}