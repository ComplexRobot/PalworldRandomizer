namespace PalworldRandomizer.ViewModels;

/// <summary>Detached persistence data; arrays are owned by the snapshot, never by the live shell.</summary>
/// <param name="Tabs">Tab snapshots in strip order.</param>
/// <param name="SelectedTabId">The selected live tab's identity, if available.</param>
/// <param name="TabMode">The requested strip layout policy.</param>
public sealed record ShellState(
    ShellTabState[] Tabs,
    Guid? SelectedTabId = null,
    TabStripMode TabMode = TabStripMode.Wrap);

/// <summary>Detached persistence data for one tab, including its complete navigation history.</summary>
/// <param name="Id">The stable tab identity.</param>
/// <param name="History">Independent history entries in navigation order.</param>
/// <param name="HistoryIndex">The current zero-based history position.</param>
/// <param name="IsClosed">Whether the tab is an Undo placeholder.</param>
/// <param name="UndoUntil">The exclusive UTC Undo deadline, or null for a live tab.</param>
public sealed record ShellTabState(
    Guid Id,
    HistoryEntry[] History,
    int HistoryIndex = 0,
    bool IsClosed = false,
    DateTimeOffset? UndoUntil = null);