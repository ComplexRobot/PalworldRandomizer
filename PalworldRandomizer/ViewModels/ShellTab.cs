using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PalworldRandomizer.ViewModels;

/// <summary>Identifies a shell destination without referencing a UI type.</summary>
public enum AppPage {
    /// <summary>The new-tab launcher.</summary>
    NewTab,
    /// <summary>The randomization workspace.</summary>
    Randomizer,
    /// <summary>The output generation workspace.</summary>
    Generate,
    /// <summary>The application settings.</summary>
    Settings,
    /// <summary>The spawn editor workspace.</summary>
    SpawnEditor,
}

/// <summary>An immutable page and optional within-page category in a tab's history.</summary>
/// <param name="Page">The destination.</param>
/// <param name="Section">An optional locale key identifying a category within the page.</param>
public sealed record HistoryEntry(AppPage Page, string? Section = null);

/// <summary>Represents one independent navigation tab.</summary>
public sealed class ShellTab : ObservableObject {
    private readonly List<HistoryEntry> _history;

    /// <summary>Creates a tab with a single initial destination.</summary>
    public ShellTab(AppPage page) {
        _history = [new(page)];
        History = _history.AsReadOnly();
    }

    /// <summary>Copies persisted history, recovering invalid addresses and history positions.</summary>
    internal ShellTab(ShellTabState state) {
        Id = state.Id;
        _history = [.. (state.History ?? []).Select(entry => entry is not null && Enum.IsDefined(entry.Page)
            ? entry : new HistoryEntry(AppPage.NewTab))];
        if (_history.Count == 0) {
            _history.Add(new(AppPage.NewTab));
        }

        History = _history.AsReadOnly();
        HistoryIndex = Math.Clamp(state.HistoryIndex, 0, _history.Count - 1);
        SetClosed(state.IsClosed ? state.UndoUntil : null);
    }

    /// <summary>Gets the stable tab identity.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Gets the current destination.</summary>
    public AppPage Page => _history[HistoryIndex].Page;

    /// <summary>Gets the locale key for the current tab title.</summary>
    public string TitleKey => $"Shell.Page.{Page}";

    /// <summary>Gets the current optional within-page category locale key.</summary>
    public string? Section => _history[HistoryIndex].Section;

    /// <summary>Gets history entries without exposing a mutable list.</summary>
    public ReadOnlyCollection<HistoryEntry> History { get; }

    /// <summary>Gets the zero-based current history position.</summary>
    public int HistoryIndex { get; private set; }

    /// <summary>Gets whether a previous history entry exists.</summary>
    public bool CanGoBack => !IsClosed && HistoryIndex > 0;

    /// <summary>Gets whether a later history entry exists.</summary>
    public bool CanGoForward => !IsClosed && HistoryIndex < _history.Count - 1;

    /// <summary>Moves back one entry, returning false at the beginning.</summary>
    public bool Back() => JumpHistory(HistoryIndex - 1);

    /// <summary>Moves forward one entry, returning false at the end.</summary>
    public bool Forward() => JumpHistory(HistoryIndex + 1);

    /// <summary>Moves to an absolute history index; invalid or unchanged positions return false.</summary>
    public bool JumpHistory(int index) {
        BeforeInteraction?.Invoke();
        if (IsClosed || index < 0 || index >= _history.Count || index == HistoryIndex) {
            return false;
        }

        HistoryIndex = index;
        NotifyNavigation();
        return true;
    }

    /// <summary>Gets whether this tab is an Undo placeholder rather than a live destination.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Gets the exclusive UTC Undo deadline, or null for a live tab.</summary>
    public DateTimeOffset? UndoUntil { get; private set; }

    /// <summary>Runs the owning shell's expiry gate before navigation, when the tab belongs to a shell.</summary>
    internal Action? BeforeInteraction { get; set; }

    /// <summary>Changes placeholder state and notifies dependent navigation capabilities.</summary>
    internal void SetClosed(DateTimeOffset? deadline) {
        IsClosed = deadline is not null;
        UndoUntil = deadline;
        OnPropertyChanged(nameof(IsClosed));
        OnPropertyChanged(nameof(UndoUntil));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    /// <summary>Appends a different address, discarding only the forward branch.</summary>
    internal void Navigate(AppPage page, string? section) {
        var entry = new HistoryEntry(page, section);
        if (_history[HistoryIndex] == entry) {
            return;
        }

        _history.RemoveRange(HistoryIndex + 1, _history.Count - HistoryIndex - 1);
        _history.Add(entry);
        HistoryIndex = _history.Count - 1;
        NotifyNavigation();
    }

    /// <summary>Refreshes every binding derived from the current history address and position.</summary>
    private void NotifyNavigation() {
        OnPropertyChanged(nameof(HistoryIndex));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(TitleKey));
        OnPropertyChanged(nameof(Section));
        OnPropertyChanged(nameof(History));
    }
}