using System.ComponentModel;
using System.IO.Compression;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;

namespace PalworldRandomizer.ViewModels;

/// <summary>Runs the real native PalSpawn generation and exposes its progress, errors and save state.</summary>
/// <remarks>
/// The ViewModel owns a unique temporary output path so multiple runs never collide. The host supplies
/// a narrow <see cref="Func{FormData}"/> that captures the user's shared Randomizer session; the isolated
/// base supplies a default <see cref="FormData"/>. Generation is serialized by the service itself and
/// this ViewModel additionally rejects simultaneous runs through its command's concurrency contract.
/// A tab switch detaches the view but keeps the produced ZIP and this model intact, so an unsaved result
/// survives re-entry. Saving only ever writes a new local file and never touches the selected game inputs.
/// </remarks>
public sealed partial class GenerateViewModel : ObservableObject, IDisposable {
    private readonly SetupViewModel _setup;
    private readonly Func<FormData> _captureSettings;
    private readonly string _dataRoot;
    private readonly Func<GenerationRequest, Action<double?>?, CancellationToken, Task<GenerationResult>> _generate;
    private string? _outputZipPath;
    private long _runId;
    private bool _subscribed;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _status = "Ready to generate.";

    [ObservableProperty]
    private GenerationResult? _result;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _hasOutput;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Creates a generation workspace bound to the current Setup inputs and settings capture.</summary>
    /// <param name="setup">The real Setup model; its current PakText/MappingText/IsReady gate generation.</param>
    /// <param name="captureSettings">The host's narrow settings snapshot; called once per generation run.</param>
    /// <param name="dataRoot">An isolated filesystem root owned by the caller; null uses the app cache.</param>
    /// <param name="generate">The generation seam; tests inject a fake, production uses the real service.</param>
    public GenerateViewModel(SetupViewModel setup, Func<FormData> captureSettings, string? dataRoot = null,
        Func<GenerationRequest, Action<double?>?, CancellationToken, Task<GenerationResult>>? generate = null) {
        _setup = setup;
        _captureSettings = captureSettings;
        _dataRoot = dataRoot ?? Path.Combine(Path.GetTempPath(), "PalworldRandomizer", "Generate");
        _generate = generate ?? PalSpawnGenerationService.GenerateAsync;
        Subscribe();
    }

    /// <summary>Re-evaluates the generate command when Setup readiness changes.</summary>
    private void SetupChanged(object? sender, PropertyChangedEventArgs args) {
        if (args.PropertyName == nameof(SetupViewModel.IsReady)) {
            GenerateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Resubscribes the cached view's model and refreshes readiness when its tab is selected again.</summary>
    /// <remarks>
    /// Readiness may have changed while the cached tab was detached, so re-entry must re-evaluate the command
    /// instead of waiting for another Setup notification.
    /// </remarks>
    public void Attach() {
        Subscribe();
        GenerateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Detaches from the shared Setup model without discarding a generated result.</summary>
    public void Detach() {
        if (!_subscribed) {
            return;
        }

        _setup.PropertyChanged -= SetupChanged;
        _subscribed = false;
    }

    /// <summary>Removes this model's temporary ZIP when the host permanently discards it.</summary>
    /// <remarks>A tab switch only detaches; an unsaved result survives until host or model disposal.</remarks>
    public void Dispose() {
        Detach();
        if (_outputZipPath is not null) {
            TryDelete(_outputZipPath);
        }
    }

    private void Subscribe() {
        if (_subscribed) {
            return;
        }

        _setup.PropertyChanged += SetupChanged;
        _subscribed = true;
    }

    /// <summary>Runs the native generation asynchronously with visible progress and errors.</summary>
    /// <remarks>
    /// The service reports progress on its worker. Every snapshot is marshalled to the UI dispatcher and
    /// tagged with the run that produced it, so a late callback can never overwrite completion state.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanGenerate), AllowConcurrentExecutions = false)]
    private async Task GenerateAsync() {
        long run = Interlocked.Increment(ref _runId);
        IsGenerating = true;
        Progress = 0;
        Status = "Generating...";
        Result = null;
        Error = null;
        _outputZipPath = null;
        HasOutput = false;
        HasError = false;
        GenerateCommand.NotifyCanExecuteChanged();

        try {
            var request = new GenerationRequest {
                PakPath = _setup.PakText,
                MappingPath = _setup.MappingText,
                DataRoot = _dataRoot,
                OutputZipPath = Path.Combine(_dataRoot, $"generated-{Guid.NewGuid():N}.zip"),
                Settings = _captureSettings(),
                PreviousGameVersion = _setup.Preferences.LastGameVersion,
            };

            // The service resolves and opens archive inputs before its first incomplete await when the
            // engine gate is free. Run the entire delegate on a worker; only the request snapshot and
            // the UI-bound completion stay on the caller's thread.
            var generationResult = await Task.Run(() => _generate(
                request,
                value => {
                    if (!value.HasValue) {
                        return;
                    }

                    double reported = Math.Clamp(value.Value, 0, 100);
                    Dispatcher.UIThread.Post(() => {
                        if (run != Volatile.Read(ref _runId) || !IsGenerating) {
                            return;
                        }

                        Progress = reported;
                        Status = $"Generating... {reported:F0}%";
                    }, DispatcherPriority.Background);
                },
                CancellationToken.None));

            Result = generationResult;
            bool browserDownload = !Path.IsPathFullyQualified(generationResult.OutputZipPath)
                && generationResult.ZipBytes > 0
                && generationResult.SchemaDocumentCount > 0;
            if (browserDownload) {
                // The browser host has no writable user path: its service starts a real download
                // and returns only the download name. Never expose the native save picker for it.
                _outputZipPath = null;
                HasOutput = false;
                Progress = 100;
                Status = "Generation complete. ZIP downloaded.";
            } else if (IsProducedZip(generationResult, request.OutputZipPath)) {
                _outputZipPath = generationResult.OutputZipPath;
                HasOutput = true;
                Progress = 100;
                Status = "Generation complete.";
            } else {
                HasOutput = false;
                _outputZipPath = null;
                Error = "Generation completed without producing a readable ZIP.";
                HasError = true;
                Status = "Generation failed.";
            }
        } catch (Exception exception) {
            Error = exception.Message;
            Status = "Generation failed.";
            HasError = true;
        } finally {
            IsGenerating = false;
            GenerateCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanGenerate() => _setup.IsReady && !IsGenerating;

    /// <summary>The generated ZIP path for display, or null before a successful run.</summary>
    public string? ResultPath => Result?.OutputZipPath;

    /// <summary>Keeps the bound result path current when a run publishes its result.</summary>
    partial void OnResultChanged(GenerationResult? value) => OnPropertyChanged(nameof(ResultPath));

    /// <summary>Copies the generated ZIP to a new local destination without overwriting anything.</summary>
    /// <remarks>
    /// The destination must be an absolute local path that is neither the temporary output nor one of the
    /// selected game inputs. A uniquely named sibling is created exclusively and moved without overwrite only
    /// after the copy succeeds. A failed save removes only its own partial file and keeps the result available.
    /// </remarks>
    public async Task SaveAsync(string destinationPath) {
        if (_outputZipPath is null || !File.Exists(_outputZipPath)) {
            throw new InvalidOperationException("No generated output is available to save.");
        }

        if (string.IsNullOrWhiteSpace(destinationPath) || !Path.IsPathFullyQualified(destinationPath)) {
            throw new ArgumentException("Choose an absolute local destination path.", nameof(destinationPath));
        }

        string destination = Path.GetFullPath(destinationPath);
        if (AliasesInput(destination) || SamePath(destination, _outputZipPath)) {
            throw new IOException("The destination must not replace a selected game file or the generated source.");
        }

        if (File.Exists(destination)) {
            throw new IOException("The destination already exists; choose a new file name.");
        }

        await using var source = new FileStream(_outputZipPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, useAsync: true);
        string partial = Path.Combine(Path.GetDirectoryName(destination)!, $".palworld-{Guid.NewGuid():N}.partial");
        bool partialCreated = false;
        try {
            await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, useAsync: true)) {
                partialCreated = true;
                await source.CopyToAsync(target);
                await target.FlushAsync();
            }

            File.Move(partial, destination);
            partialCreated = false;
            Error = null;
            HasError = false;
            Status = "Saved successfully.";
        } finally {
            if (partialCreated) {
                TryDelete(partial);
            }
        }
    }

    private bool AliasesInput(string destination) {
        foreach (string candidate in new[] { _setup.PakText, _setup.MappingText }) {
            if (!string.IsNullOrWhiteSpace(candidate) && SamePath(destination, candidate)) {
                return true;
            }
        }

        return false;
    }

    private static bool SamePath(string left, string right) {
        try {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        } catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or PathTooLongException) {
            return false;
        }
    }

    private static bool IsProducedZip(GenerationResult result, string requestedPath) {
        if (string.IsNullOrWhiteSpace(result.OutputZipPath)
            || !SamePath(result.OutputZipPath, requestedPath)
            || !File.Exists(result.OutputZipPath)
            || new FileInfo(result.OutputZipPath).Length == 0) {
            return false;
        }

        try {
            using ZipArchive archive = ZipFile.OpenRead(result.OutputZipPath);
            return archive.Entries.Count > 0;
        } catch (InvalidDataException) {
            return false;
        } catch (IOException) {
            return false;
        }
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
            // The save failure is more useful than a cleanup failure.
        } catch (UnauthorizedAccessException) {
            // The save failure is more useful than a cleanup failure.
        }
    }
}