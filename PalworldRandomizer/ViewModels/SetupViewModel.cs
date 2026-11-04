using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PalworldRandomizer.Models;

namespace PalworldRandomizer.ViewModels;

/// <summary>A contextual notification whose label is resolved by the active locale.</summary>
/// <param name="Key">Stable locale key.</param>
/// <param name="Detail">Original diagnostic context.</param>
/// <param name="Important">Requires explicit dismissal.</param>
public sealed record SetupToast(string Key, string Detail = "", bool Important = true) {
    /// <summary>Only unimportant messages have a short automatic dismissal deadline.</summary>
    public DateTimeOffset? ExpiresAt { get; } = Important ? null : DateTimeOffset.UtcNow.AddSeconds(6);
}

/// <summary>Shared Setup state; the owning UI context resumes after platform I/O.</summary>
public sealed class SetupViewModel : ObservableObject {
    private readonly Func<AppPreferences, Task> _save;
    private readonly Func<string, string, string, Task<ArchiveCompatibility>> _validate;
    private readonly Func<string, string, bool> _usable;
    private readonly SemaphoreSlim _validation = new(1, 1);
    private long _revision;

    /// <summary>Creates unconnected design-time state; no validation or persistence occurs implicitly.</summary>
    public SetupViewModel() : this(new(), _ => Task.CompletedTask,
        (_, _, _) => Task.FromException<ArchiveCompatibility>(new InvalidOperationException()),
        (_, path) => File.Exists(path), false) { }

    /// <summary>Accepts only the platform operations needed by Setup.</summary>
    public SetupViewModel(AppPreferences preferences, Func<AppPreferences, Task> save,
        Func<string, string, string, Task<ArchiveCompatibility>> validate,
        Func<string, string, bool> usable, bool browser) {
        Preferences = preferences;
        _save = save;
        _validate = validate;
        _usable = usable;
        CanTypePaths = !browser;
        PakText = preferences.PakSelection;
        MappingText = preferences.MappingSelection;
        if (PakEmpty) {
            Toasts.Add(new("setup.missingPak"));
        }

        if (MappingEmpty) {
            Toasts.Add(new("setup.missingMapping"));
        }

        if (browser && (PakInvalid || MappingInvalid)) {
            Toasts.Add(new("setup.reselect"));
        }
    }

    /// <summary>Current application metadata, independent of randomizer settings.</summary>
    public AppPreferences Preferences { get; private set; }

    /// <summary>Stacked contextual notifications.</summary>
    public ObservableCollection<SetupToast> Toasts { get; } = [];

    /// <summary>Native hosts permit typed paths; browser hosts require selected File references.</summary>
    public bool CanTypePaths { get; }

    /// <summary>Editable native path or browser display name.</summary>
    public string PakText {
        get;
        set {
            if (SetProperty(ref field, value)) {
                InputsChanged();
            }
        }
    } = "";

    /// <summary>Editable native mapping path or browser display name.</summary>
    public string MappingText {
        get;
        set {
            if (SetProperty(ref field, value)) {
                InputsChanged();
            }
        }
    } = "";

    /// <summary>Empty archive fields glow until populated.</summary>
    public bool PakEmpty => string.IsNullOrWhiteSpace(PakText);

    /// <summary>Empty mapping fields glow until populated.</summary>
    public bool MappingEmpty => string.IsNullOrWhiteSpace(MappingText);

    /// <summary>Nonempty paths with no usable local file show a contextual warning.</summary>
    public bool PakInvalid => !PakEmpty && !_usable("pak", PakText);

    /// <summary>Nonempty mapping paths with no usable local file show a contextual warning.</summary>
    public bool MappingInvalid => !MappingEmpty && !_usable("mapping", MappingText);

    /// <summary>Compatibility is being checked outside the UI thread.</summary>
    public bool IsBusy { get; private set => SetProperty(ref field, value); }

    /// <summary>The current input pair, not a previous pair, passed compatibility.</summary>
    public bool IsReady { get; private set => SetProperty(ref field, value); }

    /// <summary>Host signal for a plus-button overlay/style without modifying the frozen shell.</summary>
    public bool HighlightNewTab { get; private set => SetProperty(ref field, value); }

    /// <summary>Invalidates in-flight results when either input is edited.</summary>
    private void InputsChanged() {
        _revision++;
        ClearCompatibilitySuccess();
        IsReady = false;
        HighlightNewTab = false;
        OnPropertyChanged(nameof(PakEmpty));
        OnPropertyChanged(nameof(MappingEmpty));
        OnPropertyChanged(nameof(PakInvalid));
        OnPropertyChanged(nameof(MappingInvalid));
    }

    /// <summary>Invalidates compatibility on file replacement, even when the display name is unchanged.</summary>
    public void SelectionChanged() => InputsChanged();

    /// <summary>Runs only on Enter, focus leave, successful selection, or drop; partial edits never parse.</summary>
    public async Task CommitInputsAsync() {
        long revision = _revision;
        if (PakEmpty || MappingEmpty || PakInvalid || MappingInvalid) {
            InputsChanged();
            return;
        }

        await _validation.WaitAsync();
        try {
            if (revision != _revision) {
                return;
            }

            IsReady = false;
            HighlightNewTab = false;
            ClearCompatibilitySuccess();
            IsBusy = true;
            var result = await _validate(PakText, MappingText, Preferences.LastGameVersion);
            if (revision != _revision) {
                return;
            }

            if (PakInvalid || MappingInvalid) {
                InputsChanged();
                return;
            }

            IsReady = true;
            HighlightNewTab = true;
            Toasts.Add(new("setup.pakValid", PakText, false));
            Toasts.Add(new("setup.mappingValid", MappingText, false));
            ReportVersionChange(result.GameVersion);

            Preferences = Preferences with {
                PakSelection = PakText,
                MappingSelection = MappingText,
                SetupCompleted = true,
                LastGameVersion = result.GameVersion,
            };
            await PersistAsync();
        } catch (Exception exception) {
            if (revision == _revision) {
                if (exception is ArchiveCompatibilityException compatibility) {
                    ReportVersionChange(compatibility.DetectedGameVersion);
                    Toasts.Add(new(compatibility.Key, exception.Message));
                } else {
                    Toasts.Add(new("setup.openFailed", exception.Message));
                }
            }
        } finally {
            IsBusy = false;
            _validation.Release();
        }
    }

    private void ClearCompatibilitySuccess() {
        foreach (var toast in Toasts.Where(toast => toast.Key is "setup.pakValid" or "setup.mappingValid").ToArray()) {
            Toasts.Remove(toast);
        }
    }

    private void ReportVersionChange(string? version) {
        if (!string.IsNullOrWhiteSpace(version) && Preferences.LastGameVersion != "0.0.0.0"
            && Preferences.LastGameVersion != version) {
            Toasts.Add(new("setup.versionChanged", version));
        }
    }

    /// <summary>Applies a real Setup preference edit and awaits its storage commit.</summary>
    public async Task UpdatePreferencesAsync(AppPreferences preferences) {
        Preferences = preferences;
        OnPropertyChanged(nameof(Preferences));
        await PersistAsync();
    }

    /// <summary>Reports metadata storage failures without deleting the existing document.</summary>
    private async Task PersistAsync() {
        try {
            await _save(Preferences);
        } catch (Exception exception) {
            Toasts.Add(new("setup.persistenceFailed", exception.Message));
        }
    }

    /// <summary>Closes exactly the selected message.</summary>
    public void Dismiss(SetupToast toast) => Toasts.Remove(toast);

    /// <summary>Stops the plus cue after the host observes a real plus click.</summary>
    public void AcknowledgeNewTab() => HighlightNewTab = false;
}