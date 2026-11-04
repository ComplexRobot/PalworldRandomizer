using Newtonsoft.Json.Linq;
using PalworldRandomizer.Models;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.WebWorkers;
using BrowserFile = SpawnDev.SpawnJS.JSObjects.File;

namespace PalworldRandomizer.Browser;

/// <summary>Owns real selected browser File references and the package worker boundary.</summary>
/// <param name="workers">The approved package worker manager.</param>
public sealed class BrowserSetupFiles(WebWorkerService workers) : IDisposable {
    private BrowserFile? _pak;
    private BrowserFile? _mapping;
    private bool _disposed;

    /// <summary>Last small RPC report, available for diagnostics without exposing a game-data catalog.</summary>
    public string LastReport { get; private set; } = "{}";

    /// <summary>Tests availability of the held File, not the presence of an arbitrary OS path.</summary>
    public bool IsUsable(string role, string name) => !_disposed
        && (role == "pak" ? _pak : _mapping) is { } file && file.Name == name;

    /// <summary>Opens a real native file input from the caller's user gesture; all names remain acceptable.</summary>
    public async Task<string?> PickAsync(string role) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var document = SpawnJSRuntime.Instance.Get<Document>("document");
        using var input = document.CreateElement<HTMLInputElement>("input");
        input.Type = "file";
        input.Accept = role == "pak" ? ".pak,*/*" : ".usmap,*/*";
        input.SetAttribute("style", "display:none");
        input.SetAttribute("data-setup-file", role);
        TaskCompletionSource<string?> completion = new();
        void Changed(Event args) {
            using var files = input.Files;
            var file = files?.Length > 0 ? files.Item(0) : null;
            completion.TrySetResult(file is null ? null : Adopt(role, file));
        }

        void Canceled(Event args) => completion.TrySetResult(null);
        input.OnChange += Changed;
        input.OnCancel += Canceled;
        using var body = document.Body!;
        body.AppendChild(input);
        try {
            input.Click();
            return await completion.Task.WaitAsync(TimeSpan.FromMinutes(5));
        } finally {
            input.OnChange -= Changed;
            input.OnCancel -= Canceled;
            input.Remove();
        }
    }

    /// <summary>Takes ownership of a public picker/drop File wrapper, replacing only that input.</summary>
    public string Adopt(string role, BrowserFile file) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (role == "pak") {
            _pak?.Dispose();
            _pak = file;
        } else {
            _mapping?.Dispose();
            _mapping = file;
        }

        return file.Name;
    }

    /// <summary>Holds independent references until bounded RPC completion, timeout or worker termination.</summary>
    public async Task<ArchiveCompatibility> ValidateAsync(string pakName, string mappingName, string previousVersion) {
        if (!IsUsable("pak", pakName) || !IsUsable("mapping", mappingName)) {
            throw new ArchiveCompatibilityException("setup.reselect",
                new IOException("Local File access unavailable."));
        }

        using var pak = _pak!.JSRefCopy<BrowserFile>();
        using var mapping = _mapping!.JSRefCopy<BrowserFile>();
        using var worker = workers.GetWebWorkerSync()
            ?? throw new PlatformNotSupportedException("Dedicated browser worker unavailable.");
        await worker.WhenReady.WaitAsync(TimeSpan.FromSeconds(30));
        LastReport = await worker.GetService<IBrowserArchiveWorker>()
            .ValidateAsync(pak, mapping, previousVersion).WaitAsync(TimeSpan.FromMinutes(2));
        var report = JObject.Parse(LastReport);
        if (report.Value<string>("ErrorKey") is { } key) {
            throw new ArchiveCompatibilityException(key, new InvalidDataException(report.Value<string>("ErrorDetail")),
                report.Value<string>("DetectedGameVersion"));
        }

        return report["Summary"]!.ToObject<ArchiveCompatibility>()
            ?? throw new InvalidDataException("Worker returned no compatibility summary.");
    }

    /// <summary>Releases held File references; no PAK File or Blob is ever persisted.</summary>
    public void Dispose() {
        if (!_disposed) {
            _disposed = true;
            _pak?.Dispose();
            _mapping?.Dispose();
        }
    }
}