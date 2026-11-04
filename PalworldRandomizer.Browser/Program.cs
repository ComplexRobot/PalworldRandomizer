using System.Text.Json;
using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.DependencyInjection;
using PalworldRandomizer.Browser;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.WebWorkers;

var builder = SpawnJSAppBuilder.CreateDefault(args, out var js);
builder.Services.AddWebWorkerService();
builder.Services.AddSingleton<IBrowserArchiveWorker, BrowserArchiveWorker>();
await builder.Build().RunAsync(async app => {
    if (!js.IsWindow) {
        return;
    }

    var store = BrowserPreferences.CreateStore();
    AppPreferences preferences;
    string? storageError = null;
    try {
        preferences = await store.LoadAsync();
    } catch (Exception exception) {
        preferences = new();
        storageError = exception.Message;
    }

    var files = new BrowserSetupFiles(app.Services.GetRequiredService<WebWorkerService>());
    var picker = new RecoveryPicker(files);
    SetupViewModel setup = new(preferences, store.SaveAsync, files.ValidateAsync, files.IsUsable, true);
    if (storageError is not null) {
        setup.Toasts.Add(new("setup.persistenceFailed", storageError));
    }

    TemplateStore templates = BrowserTemplateStore.CreateStore();
    RandomizerViewModel randomizer = await RandomizerViewModel.CreateAsync(
        SettingsCatalog.LoadEmbedded(), templates);
    TaskCompletionSource<string> bootRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<string> bootDecision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    js.Set("recoveryBoot", (Func<string, Task<string>>)(json => {
        bootRequest.TrySetResult(json);
        return bootDecision.Task;
    }));

    string bootJson;
    try {
        bootJson = await bootRequest.Task.WaitAsync(TimeSpan.FromSeconds(20));
    } catch (TimeoutException) {
        // JavaScript owns its visible refusal; a second boot-timeout status must not replace it.
        return;
    }

    string status;
    string mode = "normal";
    string? pendingId = null;
    string? pendingReason = null;
    string? pendingLocale = null;
    string? pendingJson = null;
    using (var boot = JsonDocument.Parse(bootJson)) {
        var root = boot.RootElement;
        status = root.TryGetProperty("status", out var statusValue)
            ? statusValue.GetString() ?? "refused" : "refused";
        if (root.TryGetProperty("mode", out var modeValue)) {
            mode = modeValue.GetString() ?? "normal";
        }

        if (root.TryGetProperty("pending", out var pending) && pending.ValueKind == JsonValueKind.Object) {
            pendingId = Text(pending, "id");
            pendingReason = Text(pending, "reason");
            pendingLocale = Text(pending, "locale");
            pendingJson = Text(pending, "json");
        }
    }

    if (status == "refused") {
        bootDecision.TrySetResult(DecisionRefused("recovery.markerInvalid", "the boot refusal was already decided"));
        return;
    }

    if (status == "pending") {
        string? preflight = Preflight(pendingJson, pendingReason, pendingLocale);
        if (preflight is not null) {
            bootDecision.TrySetResult(preflight);
            return;
        }
    }

    bool software = mode == "software" || preferences.Renderer == "software";
    RecoveryHostBridge? bridge = null;
    PalworldRandomizer.App shared = new() {
        Setup = setup, PickFileAsync = picker.PickAsync, Randomizer = randomizer,
        Generate = files.GenerateAsync,
        PageAvailable = page => page is AppPage.Settings or AppPage.Generate,
    };
    shared.SurfaceReady += () => {
        try {
            bridge = new RecoveryHostBridge(shared.ShellState, shared.Shell, shared.Settings,
                shared.Setup, shared.Randomizer, store.SaveAsync, files.ValidateAsync, files.IsUsable, picker);
        } catch (Exception exception) {
            bootDecision.TrySetResult(DecisionRefused("recovery.pendingInvalid", exception.Message));
            return;
        }

        if (status == "pending") {
            var attached = bridge.Attach(pendingJson, pendingReason, pendingLocale);
            shared.Setup = bridge.Setup;
            if (!attached.Accepted) {
                bootDecision.TrySetResult(DecisionRefused(attached.MessageKey!, attached.Detail));
                return;
            }
        }

        bootDecision.TrySetResult("{\"status\":\"accepted\"}");
    };

    BrowserRenderingMode[] rendering = software
        ? [BrowserRenderingMode.Software2D]
        : [BrowserRenderingMode.WebGL2, BrowserRenderingMode.WebGL1, BrowserRenderingMode.Software2D];
    await AppBuilder.Configure(() => shared).WithInterFont()
        .StartBrowserAppAsync("out", new BrowserPlatformOptions { RenderingMode = rendering });

    if (bridge is null) {
        bootDecision.TrySetResult(DecisionRefused("recovery.pendingInvalid", "managed bridge was not created"));
        return;
    }

    bridge.MarkSoftwareSession(software);
    bridge.SetDropFactory(() => new BrowserSetupDrop(shared.Settings, files));
    bridge.EnsureDrop();
    js.Set("manualRecoveryCapture", (Func<Task<string>>)bridge.CaptureAsync);
    js.Set("manualRecoveryFailure", (Func<string, Task<string>>)bridge.StoreFailedAsync);
    js.Set("manualRecoveryMarkerFailure", (Func<string, Task<string>>)bridge.MarkerFailedAsync);
    js.Set("manualRecoveryWaitRestore", (Func<Task<string>>)bridge.WaitRestoreAsync);
    js.Set("manualRecoveryAcknowledge", (Func<string, Task<string>>)bridge.AcknowledgeAsync);

    using var window = js.Get<Window>("window");
    window.AddEventListener<Event>("pagehide", _ => {
        bridge.Dispose();
        files.Dispose();
    });

    static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    static string? Preflight(string? json, string? reason, string? locale) {
        if (json is null) {
            return DecisionRefused("recovery.pendingMissing", "the pending recovery document is missing");
        }

        if (!string.Equals(reason, RecoveryHostBridge.ManualReason, StringComparison.Ordinal)) {
            return DecisionRefused("recovery.markerInvalid", "unsupported recovery reason");
        }

        RecoveryState state;
        try {
            state = WorkspaceRecovery.Deserialize(json);
        } catch (Exception exception) {
            return DecisionRefused("recovery.pendingInvalid", exception.Message);
        }

        return string.Equals(state.Preferences.Locale, locale, StringComparison.Ordinal)
            ? null
            : DecisionRefused("recovery.markerInvalid", "captured locale does not match the marker");
    }

    static string DecisionRefused(string key, string? detail) => JsonSerializer.Serialize(new {
        status = "refused",
        messageKey = key,
        detail = detail ?? "",
    });
});