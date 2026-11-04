using System.Text.Json;
using Avalonia.Threading;
using PalworldRandomizer.Models;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Views;

namespace PalworldRandomizer.Browser;

/// <summary>Resolves the installed English recovery messages without claiming translations.</summary>
public static class RecoveryText {
    private static readonly Dictionary<string, string> _strings = Load();

    /// <summary>Resolves one installed recovery message key.</summary>
    public static string Get(string key) => _strings[key];

    private static Dictionary<string, string> Load() {
        using var stream = typeof(RecoveryText).Assembly
            .GetManifestResourceStream("PalworldRandomizer.Metadata.Locales.Recovery.en-US.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}

/// <summary>Tracks the actual host picker delegate so recovery never races an open chooser.</summary>
public sealed class RecoveryPicker(BrowserSetupFiles files) {
    private int _open;

    /// <summary>Whether the host-supplied picker is awaiting a user decision.</summary>
    public bool IsOpen => Volatile.Read(ref _open) > 0;

    /// <summary>Invokes the real browser File picker and tracks its exact completion.</summary>
    public async Task<string?> PickAsync(string role) {
        Interlocked.Increment(ref _open);
        try {
            return await files.PickAsync(role);
        } finally {
            Interlocked.Decrement(ref _open);
        }
    }
}

/// <summary>The managed decision returned to the boot handshake before JavaScript may continue.</summary>
/// <param name="Accepted">Whether the pending snapshot was actually restored into the live host.</param>
/// <param name="MessageKey">Localized refusal/failure key, if any.</param>
/// <param name="Detail">Original diagnostic detail, if any.</param>
public sealed record RecoveryAttachResult(bool Accepted, string? MessageKey, string? Detail) {
    /// <summary>A successful attach into the fresh live objects.</summary>
    public static RecoveryAttachResult Restored { get; } = new(true, null, null);

    /// <summary>A refusal that leaves the existing checkpoint and gated host untouched.</summary>
    public static RecoveryAttachResult Refused(string key, string? detail) => new(false, key, detail);
}

/// <summary>Owns safe capture gating, verified restoration and explicit acknowledgment for one host.</summary>
public sealed class RecoveryHostBridge(
    ShellViewModel shell,
    ShellView shellView,
    SettingsView view,
    SetupViewModel setup,
    Func<AppPreferences, Task> save,
    Func<string, string, string, Task<ArchiveCompatibility>> validate,
    Func<string, string, bool> usable,
    RecoveryPicker picker) {
    /// <summary>The only reason accepted by this manual increment.</summary>
    public const string ManualReason = "ManualRecovery";

    private readonly ShellViewModel _shell = shell;
    private readonly ShellView _shellView = shellView;
    private readonly SettingsView _view = view;
    private readonly Func<AppPreferences, Task> _save = save;
    private readonly Func<string, string, string, Task<ArchiveCompatibility>> _validate = validate;
    private readonly Func<string, string, bool> _usable = usable;
    private readonly RecoveryPicker _picker = picker;
    private readonly TaskCompletionSource _restoreReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<BrowserSetupDrop>? _dropFactory;
    private BrowserSetupDrop? _drop;
    private bool _preparing;
    private bool _restorePending;
    private bool _blocked;
    private bool _softwareSession;
    private bool _disposed;
    private bool _viewWasEnabled = true;
    private bool _shellWasEnabled = true;
    private bool _dropWasAttached;

    /// <summary>The actual Setup model, replaced by the restored model after a managed restore.</summary>
    public SetupViewModel Setup { get; private set; } = setup;

    /// <summary>Whether the owned drop handler is currently attached.</summary>
    public bool IsDropAttached => _drop is not null;

    /// <summary>Whether live input and drop handling must stay closed.</summary>
    public bool IsGated => _preparing || _restorePending || _blocked;

    /// <summary>Records that this tab booted with the temporary software override.</summary>
    public void MarkSoftwareSession(bool active) => _softwareSession = active;

    /// <summary>Supplies the owned drop attachment factory; recovery detaches and reattaches it.</summary>
    public void SetDropFactory(Func<BrowserSetupDrop> factory) => _dropFactory = factory;

    /// <summary>Attaches the owned drop handling only when the host is not gated.</summary>
    public void EnsureDrop() {
        if (_disposed || IsGated || _drop is not null || _dropFactory is null) {
            return;
        }

        _drop = _dropFactory();
    }

    /// <summary>Re-validates marker metadata and restores a verified checkpoint into the fresh objects.</summary>
    public RecoveryAttachResult Attach(string? pendingJson, string? reason, string? locale) {
        if (pendingJson is null) {
            return RecoveryAttachResult.Restored;
        }

        if (!string.Equals(reason, ManualReason, StringComparison.Ordinal)) {
            Gate();
            _blocked = true;
            return RecoveryAttachResult.Refused("recovery.markerInvalid", "unsupported recovery reason");
        }

        RecoveryState state;
        try {
            state = WorkspaceRecovery.Deserialize(pendingJson);
        } catch (Exception exception) {
            Gate();
            _blocked = true;
            return RecoveryAttachResult.Refused("recovery.pendingInvalid", exception.Message);
        }

        if (!string.Equals(state.Preferences.Locale, locale, StringComparison.Ordinal)) {
            Gate();
            _blocked = true;
            return RecoveryAttachResult.Refused("recovery.markerInvalid", "captured locale does not match the marker");
        }

        try {
            Gate();
            Setup = WorkspaceRecovery.Restore(state, _shell, _view, _save, _validate, _usable, true);
            _restorePending = true;
            _shellView.Loaded += ShellLoaded;
            return RecoveryAttachResult.Restored;
        } catch (Exception exception) {
            _blocked = true;
            return RecoveryAttachResult.Refused("recovery.pendingInvalid", exception.Message);
        }
    }

    /// <summary>Captures the actual managed draft after gating and a real dispatcher barrier.</summary>
    public async Task<string> CaptureAsync() {
        string? refusal = await Dispatcher.UIThread.InvokeAsync(PrepareCapture);
        if (refusal is not null) {
            return refusal;
        }

        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        return await Dispatcher.UIThread.InvokeAsync(CompleteCapture);
    }

    /// <summary>Rolls back preparation after an actual checkpoint store failure.</summary>
    public async Task<string> StoreFailedAsync(string detail) =>
        await Dispatcher.UIThread.InvokeAsync(() => FailCore("recovery.storeFailed", detail));

    /// <summary>Rolls back preparation after session marker verification fails.</summary>
    public async Task<string> MarkerFailedAsync(string detail) =>
        await Dispatcher.UIThread.InvokeAsync(() => FailCore("recovery.markerFailed", detail));

    /// <summary>Waits for the restored view to finish startup before the pending marker is consumed.</summary>
    public async Task<string> WaitRestoreAsync() {
        if (!_restorePending || _blocked) {
            return Json(new { status = "none" });
        }

        try {
            await _restoreReady.Task.WaitAsync(TimeSpan.FromSeconds(20));
            return Json(new { status = "restored" });
        } catch (TimeoutException exception) {
            return Json(new { status = "failed", messageKey = "recovery.ackFailed", detail = exception.Message });
        }
    }

    /// <summary>Enables live input only after a completed restore and startup barrier.</summary>
    public async Task<string> AcknowledgeAsync(string json) =>
        await Dispatcher.UIThread.InvokeAsync(() => AcknowledgeCore(json));

    /// <summary>Releases the owned drop handlers without touching accepted stage sources.</summary>
    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        DetachDrop();
    }

    private string? PrepareCapture() {
        if (_preparing || _restorePending || _blocked) {
            return Refused("recovery.refusedDuplicate");
        }

        if (_picker.IsOpen) {
            return Refused("recovery.refusedPicker");
        }

        if (Setup.IsBusy) {
            return Refused("recovery.refusedBusy");
        }

        if (_softwareSession || Setup.Preferences.Renderer == "software") {
            return Refused("recovery.refusedAlreadySoftware");
        }

        Gate();
        return null;
    }

    private string CompleteCapture() {
        if (Setup.IsBusy) {
            Rollback();
            return Refused("recovery.refusedBusy");
        }

        if (_picker.IsOpen) {
            Rollback();
            return Refused("recovery.refusedPicker");
        }

        try {
            var state = WorkspaceRecovery.Capture(_shell, Setup, _view);
            string json = WorkspaceRecovery.Serialize(state);
            return Json(new {
                status = "captured",
                json,
                reason = ManualReason,
                locale = state.Preferences.Locale,
            });
        } catch (Exception exception) {
            Rollback();
            return Json(new { status = "failed", messageKey = "recovery.captureFailed", detail = exception.Message });
        }
    }

    private string FailCore(string key, string detail) {
        if (_restorePending || _blocked) {
            return Json(new { status = "failed", messageKey = "recovery.ackFailed", detail });
        }

        Rollback();
        return Json(new { status = "rolledBack", messageKey = key, detail });
    }

    private string AcknowledgeCore(string json) {
        bool ok = false;
        string detail = "";
        try {
            using var document = JsonDocument.Parse(json);
            ok = document.RootElement.TryGetProperty("ok", out var value) && value.GetBoolean();
            if (document.RootElement.TryGetProperty("detail", out var text)) {
                detail = text.GetString() ?? "";
            }
        } catch (JsonException exception) {
            detail = exception.Message;
        }

        if (!ok) {
            return Json(new { status = "failed", messageKey = "recovery.ackFailed", detail });
        }

        if (!_restorePending || _blocked || !_restoreReady.Task.IsCompletedSuccessfully) {
            return Json(new {
                status = "failed",
                messageKey = "recovery.ackFailed",
                detail = "the restore startup barrier has not completed",
            });
        }

        _restorePending = false;
        _preparing = false;
        _view.IsEnabled = _viewWasEnabled;
        _shellView.IsEnabled = _shellWasEnabled;
        if (_dropFactory is not null) {
            EnsureDrop();
        }

        return Json(new { status = "active", messageKey = "recovery.active", detail = "" });
    }

    private void Gate() {
        if (!_preparing && !_restorePending && !_blocked) {
            _viewWasEnabled = _view.IsEnabled;
            _shellWasEnabled = _shellView.IsEnabled;
            _dropWasAttached = _drop is not null;
        }

        _preparing = true;
        _view.IsEnabled = false;
        _shellView.IsEnabled = false;
        DetachDrop();
    }

    private void Rollback() {
        if (_restorePending || _blocked) {
            return;
        }

        _preparing = false;
        _view.IsEnabled = _viewWasEnabled;
        _shellView.IsEnabled = _shellWasEnabled;
        if (_dropWasAttached) {
            EnsureDrop();
        }
    }

    private void DetachDrop() {
        _drop?.Dispose();
        _drop = null;
    }

    private void ShellLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args) {
        _shellView.Loaded -= ShellLoaded;
        Dispatcher.UIThread.Post(() => _restoreReady.TrySetResult(), DispatcherPriority.Background);
    }

    private static string Refused(string key) => Json(new { status = "refused", messageKey = key });

    private static string Json(object value) => JsonSerializer.Serialize(value);
}