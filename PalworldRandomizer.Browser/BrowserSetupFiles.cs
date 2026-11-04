using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.WebWorkers;
using BrowserFile = SpawnDev.SpawnJS.JSObjects.File;

namespace PalworldRandomizer.Browser;

/// <summary>Owns real selected browser File references and the package worker boundary.</summary>
/// <param name="workers">The approved package worker manager.</param>
public sealed class BrowserSetupFiles(WebWorkerService workers) : IBrowserGenerationService, IDisposable {
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

    /// <summary>Runs real generation in the dedicated worker and writes the completed ZIP to the requested path.</summary>
    /// <remarks>
    /// The PAK and USMAP File references stay in this host and cross only as live worker handles. The worker reads
    /// bounded slices, so the full PAK never enters .NET memory. The returned ZIP is about four megabytes even
    /// though the uncompressed PalSchema documents exceed one hundred megabytes; only that compressed result is
    /// copied into the host's writable filesystem. The request settings are serialized once into an immutable
    /// snapshot before the worker call.
    /// </remarks>
    /// <param name="request">The selected file names, destination ZIP path and captured settings.</param>
    /// <param name="reportProgress">Optional coarse progress snapshots; generation itself reports on completion.</param>
    /// <param name="cancellationToken">Cancels waiting for worker readiness or completion; worker work is atomic.</param>
    /// <returns>The same summary contract as the native generation service.</returns>
    /// <exception cref="InvalidOperationException">The selected File references are no longer available.</exception>
    /// <exception cref="InvalidDataException">The worker failed or produced an unreadable ZIP.</exception>
    public async Task<GenerationResult> GenerateAsync(GenerationRequest request,
        Action<double?>? reportProgress = null, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        if (!IsUsable("pak", request.PakPath) || !IsUsable("mapping", request.MappingPath)) {
            throw new InvalidOperationException(
                "The selected game files are no longer available; reselect the PAK and USMAP.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        reportProgress?.Invoke(0);
        using var pak = _pak!.JSRefCopy<BrowserFile>();
        using var mapping = _mapping!.JSRefCopy<BrowserFile>();
        using var worker = workers.GetWebWorkerSync()
            ?? throw new PlatformNotSupportedException("Dedicated browser worker unavailable.");
        await worker.WhenReady.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        string settingsJson = JsonConvert.SerializeObject(request.Settings);
        BrowserGenerationReport report = await worker.GetService<IBrowserArchiveWorker>()
            .GenerateAsync(pak, mapping, request.PreviousGameVersion, settingsJson)
            .WaitAsync(TimeSpan.FromMinutes(10), cancellationToken);
        LastReport = JsonConvert.SerializeObject(new {
            report.Success,
            report.ErrorKey,
            report.ErrorDetail,
            report.GameVersion,
            report.ArchiveEntries,
            report.PalCount,
            report.VanillaAreaCount,
            report.GeneratedAreaCount,
            report.ChangedAreaCount,
            report.SchemaDocumentCount,
            report.SchemaBytes,
            report.ZipBytes,
            report.PakBytesRead,
            report.PakReads,
            report.MaxPakReadRequest,
            report.MappingBytesRead,
            report.MappingReads,
            report.MaxMappingReadRequest,
            report.ElapsedMilliseconds,
        });
        if (!report.Success) {
            throw new InvalidDataException($"{report.ErrorKey}: {report.ErrorDetail}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using (ZipArchive archive = new(new MemoryStream(report.ZipData, writable: false), ZipArchiveMode.Read)) {
            if (archive.Entries.Count != report.SchemaDocumentCount) {
                throw new InvalidDataException("The generated ZIP does not contain the reported documents.");
            }
        }

        string downloadName = Path.GetFileName(request.OutputZipPath);
        if (string.IsNullOrWhiteSpace(downloadName)) {
            downloadName = "PalSchema.zip";
        }

        // A browser has no user-visible writable path: the finished ZIP leaves as a real download.
        using (var document = SpawnJSRuntime.Instance.Get<Document>("document")) {
            using var body = document.Body!;
            using var blob = new Blob([report.ZipData], new BlobOptions { Type = "application/zip" });
            string url = URL.CreateObjectURL(blob);
            try {
                using var anchor = document.CreateElement<HTMLAnchorElement>("a");
                anchor.Href = url;
                anchor.Download = downloadName;
                anchor.SetAttribute("style", "display:none");
                body.AppendChild(anchor);
                anchor.Click();
                anchor.Remove();
            } finally {
                URL.RevokeObjectURL(url);
            }
        }

        reportProgress?.Invoke(100);
        return new GenerationResult(
            report.GameVersion,
            report.ArchiveEntries,
            report.PalCount,
            report.VanillaAreaCount,
            report.GeneratedAreaCount,
            report.ChangedAreaCount,
            report.SchemaDocumentCount,
            report.SchemaBytes,
            report.ZipBytes,
            downloadName
        );
    }

    /// <summary>Implements the host-neutral boundary with a bounded waiting cancellation.</summary>
    Task<GenerationResult> IBrowserGenerationService.GenerateAsync(GenerationRequest request,
        CancellationToken cancellationToken) => GenerateAsync(request, null, cancellationToken);

    /// <summary>Releases held File references; no PAK File or Blob is ever persisted.</summary>
    public void Dispose() {
        if (!_disposed) {
            _disposed = true;
            _pak?.Dispose();
            _mapping?.Dispose();
        }
    }
}