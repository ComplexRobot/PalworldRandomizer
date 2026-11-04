using System.Text.Json;
using Avalonia.Controls;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Views;

namespace PalworldRandomizer.Browser;

/// <summary>Captures and restores actual managed shell and Setup state.</summary>
public static class WorkspaceRecovery {
    /// <summary>Captures detached actual shell state and the exact current Setup drafts.</summary>
    public static RecoveryState Capture(ShellViewModel shell, SetupViewModel setup, SettingsView view) {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(view);
        if (setup.IsBusy) {
            throw new InvalidOperationException("Setup validation is busy; recovery state was not captured.");
        }

        TextBox filename = FilenameControl(view);
        ValidateViewFormats(setup.Preferences, view);
        return new RecoveryState(
            RecoveryState.CurrentSchema,
            setup.Preferences,
            shell.CaptureState(),
            setup.PakText,
            setup.MappingText,
            filename.Text ?? "");
    }

    /// <summary>Serializes and validates a detached recovery document.</summary>
    public static string Serialize(RecoveryState state) {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);
        return JsonSerializer.Serialize(state);
    }

    /// <summary>Deserializes and validates a recovery document without changing live state.</summary>
    public static RecoveryState Deserialize(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new InvalidDataException("The recovery document is empty.");
        }

        RecoveryState state;
        try {
            state = JsonSerializer.Deserialize<RecoveryState>(json)
                ?? throw new InvalidDataException("The recovery document is empty.");
        } catch (JsonException exception) {
            throw new InvalidDataException("The recovery document is malformed.", exception);
        }

        Validate(state);
        return state;
    }

    /// <summary>Validates, then restores actual shell and Setup state through host callbacks.</summary>
    public static SetupViewModel Restore(RecoveryState state, ShellViewModel shell, SettingsView view,
        Func<AppPreferences, Task> save,
        Func<string, string, string, Task<ArchiveCompatibility>> validate,
        Func<string, string, bool> usable,
        bool browser) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(usable);
        Validate(state);
        TextBox filename = FilenameControl(view);
        ValidateViewFormats(state.Preferences, view);
        var setup = new SetupViewModel(state.Preferences, save, validate, usable, browser) {
            PakText = state.PakDraft,
            MappingText = state.MappingDraft,
        };
        shell.RestoreState(state.Shell);
        view.DataContext = setup;
        filename.Text = state.FilenameDraft;
        return setup;
    }

    /// <summary>Rejects unsupported, malformed, or inconsistent recovery data before any live mutation.</summary>
    private static void Validate(RecoveryState state) {
        if (state.Schema != RecoveryState.CurrentSchema) {
            throw new InvalidDataException("The recovery schema is not supported.");
        }

        if (state.Preferences is null) {
            throw new InvalidDataException("Recovery preferences are missing.");
        }

        if (state.Shell?.Tabs is null) {
            throw new InvalidDataException("Recovery shell tabs are missing.");
        }

        if (state.PakDraft is null || state.MappingDraft is null || state.FilenameDraft is null) {
            throw new InvalidDataException("Recovery input drafts are incomplete.");
        }

        AppPreferencesStore.Validate(state.Preferences);
        ValidateShell(state.Shell);

        // A valid unsaved filename draft cannot come from the actual view without an earlier durable save.
        if (AppPreferencesStore.IsValidFilenamePattern(state.FilenameDraft)
            && state.FilenameDraft != state.Preferences.FilenamePattern) {
            throw new InvalidDataException("A valid filename draft is not the saved preference.");
        }
    }

    /// <summary>Rejects tab values the actual shell would silently normalize during restoration.</summary>
    private static void ValidateShell(ShellState shell) {
        if (shell.Tabs.Length == 0) {
            throw new InvalidDataException("The recovery shell has no tabs.");
        }

        if (!Enum.IsDefined(shell.TabMode)) {
            throw new InvalidDataException("The recovery tab layout mode is not defined.");
        }

        HashSet<Guid> identities = [];
        foreach (var tab in shell.Tabs) {
            if (tab is null) {
                throw new InvalidDataException("A recovery tab is missing.");
            }

            if (tab.Id == Guid.Empty || !identities.Add(tab.Id)) {
                throw new InvalidDataException("A recovery tab identity is empty or duplicated.");
            }

            if (tab.History is null || tab.History.Length == 0) {
                throw new InvalidDataException("A recovery tab history is missing or empty.");
            }

            if (tab.HistoryIndex < 0 || tab.HistoryIndex >= tab.History.Length) {
                throw new InvalidDataException("A recovery history position is out of range.");
            }

            foreach (var entry in tab.History) {
                if (entry is null || !Enum.IsDefined(entry.Page)) {
                    throw new InvalidDataException("A recovery history entry address is not defined.");
                }
            }

            bool hasDeadline = tab.UndoUntil is not null;
            if (tab.IsClosed != hasDeadline) {
                throw new InvalidDataException("A recovery tab close deadline is inconsistent.");
            }
        }

        if (shell.SelectedTabId is not Guid selectedId) {
            throw new InvalidDataException("The recovery selected tab is missing.");
        }

        var selected = shell.Tabs.FirstOrDefault(tab => tab.Id == selectedId);
        if (selected is null || selected.IsClosed) {
            throw new InvalidDataException("The recovery selected tab is not a live tab.");
        }
    }

    /// <summary>Rejects saved format values the actual Settings controls cannot represent.</summary>
    private static void ValidateViewFormats(AppPreferences preferences, SettingsView view) {
        ComboBox date = view.FindControl<ComboBox>("Date")
            ?? throw new InvalidOperationException("The actual Settings date field is unavailable.");
        ComboBox time = view.FindControl<ComboBox>("Time")
            ?? throw new InvalidOperationException("The actual Settings time field is unavailable.");
        if (!CanRepresent(date, preferences.DatePattern)) {
            throw new InvalidDataException("The recovery date pattern is not supported by the actual view.");
        }

        if (!CanRepresent(time, preferences.TimePattern)) {
            throw new InvalidDataException("The recovery time pattern is not supported by the actual view.");
        }
    }

    /// <summary>Matches a saved pattern against the actual choices, whose first item is the locale default.</summary>
    private static bool CanRepresent(ComboBox control, string? pattern) {
        if (pattern is null) {
            return true;
        }

        var items = control.ItemsSource;
        if (items is null) {
            return false;
        }

        int index = 0;
        foreach (object? item in items) {
            if (index++ > 0 && item is string value && value == pattern) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the actual named filename input used for detached raw draft capture and restore.</summary>
    private static TextBox FilenameControl(SettingsView view) => view.FindControl<TextBox>("Filename")
        ?? throw new InvalidOperationException("The actual Settings filename field is unavailable.");
}