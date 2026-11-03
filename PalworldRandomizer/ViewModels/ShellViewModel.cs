using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PalworldRandomizer.ViewModels;

/// <summary>The layout policy requested from the tab-strip view.</summary>
public enum TabStripMode {
    /// <summary>Wrap tabs onto multiple rows.</summary>
    Wrap,
    /// <summary>Shrink tab headers to fit.</summary>
    Shrink,
    /// <summary>Expose excess tabs through an overflow menu.</summary>
    Overflow,
}

/// <summary>Owns UI-independent tab selection and navigation.</summary>
/// <remarks>
/// Use shell operations to mutate tabs on one owning UI thread. Layout measurement, dragging input,
/// timers, localization lookup, and file I/O belong to the host. No background expiry timer is created.
/// </remarks>
public sealed class ShellViewModel : ObservableObject {
    private readonly TimeProvider _clock;
    private ShellTab _selectedTab;

    /// <summary>Creates the initial Settings or launcher tab; the clock controls close deadlines.</summary>
    public ShellViewModel(TimeProvider? clock = null, bool firstRun = true) {
        _clock = clock ?? TimeProvider.System;
        _selectedTab = new ShellTab(firstRun ? AppPage.Settings : AppPage.NewTab);
        AddTab(_selectedTab);
    }

    /// <summary>Adds and selects a launcher tab, returning that tab.</summary>
    public ShellTab OpenTab() => OpenPage(AppPage.NewTab);

    /// <summary>Adds and selects a tab for a destination, returning the new tab.</summary>
    public ShellTab OpenPage(AppPage page) {
        PruneClosedTabs();
        var tab = new ShellTab(page);
        AddTab(tab);
        SelectedTab = tab;
        return tab;
    }

    /// <summary>Keeps selected Settings, otherwise reuses a live Settings tab or opens one, returning it.</summary>
    public ShellTab OpenSettings() {
        PruneClosedTabs();
        var tab = SelectedTab.Page == AppPage.Settings ? SelectedTab
            : Tabs.FirstOrDefault(tab => !tab.IsClosed && tab.Page == AppPage.Settings);
        if (tab is null) {
            return OpenPage(AppPage.Settings);
        }

        SelectedTab = tab;
        return tab;
    }

    /// <summary>Navigates the selected tab to a page and optional category locale key.</summary>
    public void NavigateSelected(AppPage page, string? section = null) {
        PruneClosedTabs();
        SelectedTab.Navigate(page, section);
    }

    /// <summary>Retains a live tab as a 30-second Undo placeholder; returns false for a non-live member.</summary>
    public bool CloseTab(ShellTab tab) {
        PruneClosedTabs();
        if (!Tabs.Contains(tab) || tab.IsClosed) {
            return false;
        }

        tab.SetClosed(_clock.GetUtcNow().AddSeconds(30));
        if (SelectedTab == tab) {
            SelectedTab = Tabs.FirstOrDefault(candidate => !candidate.IsClosed) ?? OpenTab();
        }

        return true;
    }

    /// <summary>Restores and selects a closed member, returning false if it cannot be restored.</summary>
    public bool UndoClose(ShellTab tab) {
        PruneClosedTabs();
        if (!Tabs.Contains(tab) || !tab.IsClosed) {
            return false;
        }

        tab.SetClosed(null);
        SelectedTab = tab;
        return true;
    }

    /// <summary>Removes expired placeholders and returns the number removed.</summary>
    public int PruneClosedTabs() {
        var now = _clock.GetUtcNow();
        var expired = Tabs.Where(tab => tab.IsClosed && tab.UndoUntil <= now).ToArray();
        foreach (var tab in expired) {
            Tabs.Remove(tab);
        }

        return expired.Length;
    }

    /// <summary>Moves a member to an exact strip index; returns false for invalid or unchanged moves.</summary>
    public bool ReorderTab(ShellTab tab, int targetIndex) {
        PruneClosedTabs();
        int sourceIndex = Tabs.IndexOf(tab);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= Tabs.Count || sourceIndex == targetIndex) {
            return false;
        }

        Tabs.Move(sourceIndex, targetIndex);
        return true;
    }

    /// <summary>Selects an overflow-menu tab and moves it first; returns false outside Overflow mode.</summary>
    public bool PromoteOverflow(ShellTab tab) {
        PruneClosedTabs();
        if (TabMode != TabStripMode.Overflow || !Tabs.Contains(tab) || tab.IsClosed) {
            return false;
        }

        ReorderTab(tab, 0);
        SelectedTab = tab;
        return true;
    }

    /// <summary>Captures detached tabs and immutable histories after pruning expired placeholders.</summary>
    public ShellState CaptureState() {
        PruneClosedTabs();
        return new ShellState(
            [.. Tabs.Select(tab => new ShellTabState(
                tab.Id, [.. tab.History], tab.HistoryIndex, tab.IsClosed, tab.UndoUntil))],
            SelectedTab.Id,
            TabMode);
    }

    /// <summary>Replaces contents from detached data, recovering invalid persisted values.</summary>
    /// <exception cref="ArgumentNullException">The top-level state is null.</exception>
    public void RestoreState(ShellState state) {
        ArgumentNullException.ThrowIfNull(state);
        Tabs.Clear();
        var now = _clock.GetUtcNow();
        HashSet<Guid> identities = [];
        foreach (var tab in state.Tabs ?? []) {
            if (tab is null || (tab.IsClosed && (tab.UndoUntil is null || tab.UndoUntil <= now))) {
                continue;
            }

            var id = tab.Id;
            while (id == Guid.Empty || !identities.Add(id)) {
                id = Guid.NewGuid();
            }

            AddTab(new ShellTab(tab with { Id = id }));
        }

        SelectedTab = Tabs.FirstOrDefault(tab => tab.Id == state.SelectedTabId && !tab.IsClosed)
            ?? Tabs.FirstOrDefault(tab => !tab.IsClosed) ?? OpenTab();
        TabMode = Enum.IsDefined(state.TabMode) ? state.TabMode : TabStripMode.Wrap;
    }

    /// <summary>Registers the owning shell's expiry gate before exposing a tab to bindings.</summary>
    private void AddTab(ShellTab tab) {
        tab.BeforeInteraction = () => PruneClosedTabs();
        Tabs.Add(tab);
    }

    /// <summary>Gets or sets the requested tab layout policy.</summary>
    public TabStripMode TabMode {
        get;
        set {
            PruneClosedTabs();
            SetProperty(ref field, value);
        }
    }

    /// <summary>Gets the ordered binding collection; mutate it only through shell methods.</summary>
    public ObservableCollection<ShellTab> Tabs { get; } = [];

    /// <summary>Gets or selects a live member; null, foreign, or closed assignments are ignored.</summary>
    [AllowNull]
    public ShellTab SelectedTab {
        get => _selectedTab;
        set {
            PruneClosedTabs();
            if (value is not null && Tabs.Contains(value) && !value.IsClosed) {
                SetProperty(ref _selectedTab, value);
            }
        }
    }
}