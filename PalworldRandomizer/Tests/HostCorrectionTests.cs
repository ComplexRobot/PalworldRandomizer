using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PalworldRandomizer.Desktop;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Views;

// Exercises the actual Desktop host and shared App; no cloned model or QA fixture page.
internal static class HostCorrectionTests {
    internal static int Main() {
        int failures = 0;
        failures += Run("Desktop Main is synchronous STA and software preference reaches public options",
            NativeStartupContract);
        failures += Run("Setup ownership follows SurfaceReady-before-Loaded and later replacement",
            SetupOwnership);
        failures += Run("Generate command runs an injected generator and publishes its actual ZIP",
            GenerateCommandWithFake);
        failures += Run("Generate progress is marshalled to the UI thread without stale completion",
            GenerateProgressUsesUiDispatcher);
        failures += Run("Generate captures settings on the UI thread and runs the delegate on a worker",
            GenerateRunsOffUiThread);
        failures += Run("A tab switch detaches the Generate view without deleting its result",
            TabSwitchPreservesResult);
        failures += Run("Generate is discoverable before readiness and its command gates dynamically",
            GenerateAvailabilityBeforeReadiness);
        failures += Run("Saving refuses game inputs and existing files and copies a new ZIP",
            SaveRefusesAliasesAndExistingFiles);
        failures += Run("A browser download result completes without native save state",
            BrowserDownloadResult);
        failures += Run("Disposing the Generate model deletes its temporary output ZIP",
            DisposeDeletesTemporaryZip);
        failures += Run("Repeated Generate deletes the prior temporary ZIP",
            RepeatedGenerateDeletesPreviousTemporaryZip);
        failures += Run("Disposing cancels an in-flight generation",
            DisposeCancelsInFlightGeneration);
        Console.WriteLine($"{12 - failures}/12 host correction checks passed.");
        return failures == 0 ? 0 : 1;
    }

    private static int Run(string name, Action run) {
        try {
            run();
            Console.WriteLine($"PASS {name}");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine($"FAIL {name}: {exception.GetBaseException().Message}");
            return 1;
        }
    }

    private static void NativeStartupContract() {
        Assembly desktop = typeof(PalworldRandomizer.Desktop.DesktopArchiveValidation).Assembly;
        Type? program = desktop.GetTypes().FirstOrDefault(type => type.Name == "Program"
            && type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is not null);
        Require(program is not null, "The Desktop host has no Main entry point.");
        MethodInfo main = program!.GetMethod("Main",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        Require(main.GetCustomAttribute<STAThreadAttribute>() is not null,
            "Native startup must keep the UI on an STA entry thread; Main lacks [STAThread].");
        Require(main.ReturnType == typeof(int) || main.ReturnType == typeof(void),
            "Native startup must be synchronous; the top-level await leaves an async Task entry point.");

        Type? native = desktop.GetType("PalworldRandomizer.Desktop.NativeRendering", throwOnError: false);
        Require(native is not null, "The Desktop host exposes no public typed native rendering selector.");
        MethodInfo? optionsFor = native!.GetMethod("OptionsFor", BindingFlags.Public | BindingFlags.Static,
            binder: null, types: [typeof(string)], modifiers: null);
        Require(optionsFor is not null, "NativeRendering.OptionsFor(string) is missing.");

        RequireModes(optionsFor!.Invoke(null, ["software"]), "software",
            ["Software"], ["Software"], ["Software"]);
        RequireModes(optionsFor.Invoke(null, ["gpu-preferred"]), "gpu-preferred",
            ["AngleEgl", "Software"], ["Glx", "Software"], ["OpenGl", "Software"]);
    }

    private static void RequireModes(object? options, string renderer, string[] win32, string[] x11, string[] native) {
        Require(options is not null, $"NativeRendering.OptionsFor({renderer}) returned null.");
        foreach (var (property, expected) in new[] {
            ("Win32", win32), ("X11", x11), ("AvaloniaNative", native),
        }) {
            object? platform = options!.GetType().GetProperty(property)?.GetValue(options);
            Require(platform is not null, $"{renderer} has no {property} options.");
            object? modes = platform!.GetType().GetProperty("RenderingMode")?.GetValue(platform);
            if (modes is not IEnumerable values) {
                throw new InvalidOperationException($"{renderer} {property} has no public rendering mode list.");
            }

            string[] actual = [.. values.Cast<object>().Select(mode => mode.ToString() ?? "")];
            Require(actual.SequenceEqual(expected),
                $"{renderer} {property} modes were [{string.Join(", ", actual)}], "
                + $"expected [{string.Join(", ", expected)}].");
        }
    }

    private static void SetupOwnership() {
        int[] aCalls = [0];
        int[] bCalls = [0];
        int[] cCalls = [0];
        SetupViewModel a = CreateSetup("a.pak", "a.usmap", aCalls);
        a.CommitInputsAsync().GetAwaiter().GetResult();
        Require(a.HighlightNewTab, "Fixture A must start highlighted.");
        SetupViewModel b = CreateSetup("b.pak", "b.usmap", bCalls);
        SetupViewModel c = CreateSetup("c.pak", "c.usmap", cCalls);

        PalworldRandomizer.App app = new() { Setup = a };
        app.SurfaceReady += () => app.Setup = b;
        AppBuilder.Configure(() => app).UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();

        Require(ReferenceEquals(app.Setup, b), "SurfaceReady replacement did not reach the live App.Setup.");
        Require(ReferenceEquals(app.Settings.DataContext, b),
            "SurfaceReady replacement did not update the actual Settings view.");

        Window window = app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } main }
            ? main : new Window { Width = 1100, Height = 900, Content = app.Shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        WaitFor(() => bCalls[0] == 1, "Loaded compatibility did not run for the current restored Setup.");
        Require(aCalls[0] == 1, "The obsolete Setup ran compatibility after replacement.");
        Require(b.HighlightNewTab, "The restored Setup did not publish its highlight.");
        Button plus = app.Shell.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetAutomationId(button) == "Shell.AddTab");
        Require(plus.BorderThickness.Left == 2, "The plus highlight did not follow the restored Setup.");

        a.AcknowledgeNewTab();
        Dispatcher.UIThread.RunJobs();
        Require(plus.BorderThickness.Left == 2, "The obsolete Setup changed the UI after replacement.");
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, plus));
        Dispatcher.UIThread.RunJobs();
        Require(!b.HighlightNewTab, "The plus acknowledgement did not reach the restored Setup.");
        Require(plus.BorderThickness.Left == 0, "The plus highlight did not clear for the restored Setup.");

        app.Setup = c;
        Dispatcher.UIThread.RunJobs();
        Require(ReferenceEquals(app.Settings.DataContext, c),
            "Post-Loaded replacement did not update the actual Settings view.");
        WaitFor(() => cCalls[0] == 1, "Post-Loaded replacement did not run the current compatibility.");
        Require(c.HighlightNewTab, "Post-Loaded replacement did not publish its highlight.");
        Require(plus.BorderThickness.Left == 2, "The plus highlight did not follow the later Setup.");

        b.CommitInputsAsync().GetAwaiter().GetResult();
        b.AcknowledgeNewTab();
        Dispatcher.UIThread.RunJobs();
        Require(plus.BorderThickness.Left == 2, "The previous Setup changed the UI after later replacement.");
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, plus));
        Dispatcher.UIThread.RunJobs();
        Require(!c.HighlightNewTab, "The plus acknowledgement did not reach the later Setup.");
        Require(plus.BorderThickness.Left == 0, "The plus highlight did not clear for the later Setup.");
        window.Close();
    }

    private static void GenerateCommandWithFake() {
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup("pak", "mapping", validationCalls);
        setup.CommitInputsAsync().GetAwaiter().GetResult();
        Require(setup.IsReady, "Setup did not become ready after successful compatibility.");

        bool generatorCalled = false;
        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            (request, progress, token) => {
                generatorCalled = true;
                return Task.FromResult(WriteTinyZip(request));
            });
        bool resultPathNotified = false;
        model.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(GenerateViewModel.ResultPath)) {
                resultPathNotified = true;
            }
        };

        RunGenerate(model);
        Require(generatorCalled, "The injected generator was not invoked.");
        Require(resultPathNotified, "Changing the result did not notify the bound ResultPath.");
        Require(model.HasOutput && model.Result is not null, "The generated result was not published.");
        Require(model.Result!.OutputZipPath == model.ResultPath && File.Exists(model.ResultPath),
            "The published result is not the ZIP the generator wrote.");
        Require(model.Progress == 100 && model.Status == "Generation complete.",
            "Completion did not settle progress at 100 percent.");
        model.Detach();
    }

    private static void GenerateProgressUsesUiDispatcher() {
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup("pak", "mapping", validationCalls);
        setup.CommitInputsAsync().GetAwaiter().GetResult();
        Require(setup.IsReady, "Setup did not become ready after successful compatibility.");

        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool offUiThread = false;
        Action<double?>? lateReport = null;
        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            async (request, progress, token) => {
                await Task.Run(() => progress?.Invoke(42));
                await release.Task;
                progress?.Invoke(99);
                lateReport = progress;
                return WriteTinyZip(request);
            });
        model.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(GenerateViewModel.Progress)
                && !Dispatcher.UIThread.CheckAccess()) {
                offUiThread = true;
            }
        };

        Task run = model.GenerateCommand.ExecuteAsync(null);
        WaitFor(() => model.Progress >= 42, "Worker progress never reached the UI thread.");
        Require(model.Status.StartsWith("Generating...", StringComparison.Ordinal),
            "Worker progress did not update the visible status.");
        Require(!offUiThread, "Progress changed the observable from the service worker thread.");

        release.TrySetResult();
        WaitFor(() => run.IsCompleted, "Generation did not complete after releasing the worker.");
        run.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        Require(model.Progress == 100, "Final progress was not settled at 100 percent.");

        Task.Run(() => lateReport?.Invoke(77)).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        Require(model.Progress == 100 && model.Status == "Generation complete.",
            "A late progress callback overwrote the completed state.");
        model.Detach();
    }

    private static void GenerateRunsOffUiThread() {
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup("pak", "mapping", validationCalls);
        setup.CommitInputsAsync().GetAwaiter().GetResult();
        Require(setup.IsReady, "Setup did not become ready after successful compatibility.");

        bool capturedOnUi = false;
        bool workerOnUi = true;
        int workerThread = Environment.CurrentManagedThreadId;
        GenerateViewModel model = new(setup, () => {
            capturedOnUi = Dispatcher.UIThread.CheckAccess();
            return new FormData();
        }, NewRoot(), (request, progress, token) => {
            workerOnUi = Dispatcher.UIThread.CheckAccess();
            workerThread = Environment.CurrentManagedThreadId;
            return Task.FromResult(WriteTinyZip(request));
        });

        RunGenerate(model);
        Require(capturedOnUi, "The settings snapshot was not captured on the UI thread.");
        Require(!workerOnUi && workerThread != Environment.CurrentManagedThreadId,
            "Archive setup and generation ran on the UI thread.");
        Require(model.HasOutput && File.Exists(model.ResultPath),
            "The worker run did not publish its actual ZIP.");
        model.Detach();
    }

    private static void TabSwitchPreservesResult() {
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup("pak", "mapping", validationCalls);
        setup.CommitInputsAsync().GetAwaiter().GetResult();
        Require(setup.IsReady, "Setup did not become ready after successful compatibility.");

        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            (request, progress, token) => Task.FromResult(WriteTinyZip(request)));
        GenerateView view = new() { DataContext = model };
        Window window = new() { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        RunGenerate(model);
        Require(model.HasOutput && model.ResultPath is not null, "The fake run produced no result.");
        string output = model.ResultPath!;
        Require(File.Exists(output), "The generated ZIP was not written.");

        // A normal shell tab switch replaces the page host content and detaches the cached view.
        window.Content = new TextBlock();
        Dispatcher.UIThread.RunJobs();
        Require(model.HasOutput && File.Exists(output),
            "Detaching the Generate view deleted the user's unsaved result.");

        window.Content = view;
        Dispatcher.UIThread.RunJobs();
        Require(model.HasOutput && File.Exists(output), "Re-entering the tab lost the result.");
        setup.PakText = "different.pak";
        Dispatcher.UIThread.RunJobs();
        Require(!model.GenerateCommand.CanExecute(null),
            "The cached model did not resubscribe to Setup readiness after re-entry.");
        window.Close();
    }

    private static void GenerateAvailabilityBeforeReadiness() {
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup("pak", "mapping", validationCalls);
        Require(!setup.IsReady, "Fixture Setup unexpectedly started ready.");

        ShellViewModel shell = new(firstRun: false);
        LauncherViewModel launcher = new(shell, DesktopPagePolicy.IsAvailable);
        LauncherDestination generate = launcher.Destinations.Single(item => item.Page == AppPage.Generate);
        Require(generate.IsAvailable,
            "A native launcher created before validation hid the real Generate page.");

        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            (request, progress, token) => Task.FromResult(WriteTinyZip(request)));
        Require(!model.GenerateCommand.CanExecute(null),
            "Generate must stay disabled until the current inputs validate.");

        setup.CommitInputsAsync().GetAwaiter().GetResult();
        Require(setup.IsReady, "Setup did not become ready after successful compatibility.");
        Require(model.GenerateCommand.CanExecute(null),
            "Generate did not re-evaluate when Setup became ready.");
        model.Detach();
    }

    private static void SaveRefusesAliasesAndExistingFiles() {
        string root = NewRoot();
        string pak = Path.Combine(root, "game.pak");
        string mapping = Path.Combine(root, "mappings.usmap");
        File.WriteAllText(pak, "original pak");
        File.WriteAllText(mapping, "original mapping");
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup(pak, mapping, validationCalls);
        setup.CommitInputsAsync().GetAwaiter().GetResult();

        GenerateViewModel model = new(setup, () => new FormData(), Path.Combine(root, "data"),
            (request, progress, token) => Task.FromResult(WriteTinyZip(request)));
        RunGenerate(model);
        Require(model.HasOutput && model.ResultPath is not null, "The fake run produced no result.");
        string output = model.ResultPath!;
        byte[] expected = File.ReadAllBytes(output);

        RequireThrows<IOException>(() => model.SaveAsync(pak), "Saving over the selected PAK must fail.");
        RequireThrows<IOException>(() => model.SaveAsync(mapping),
            "Saving over the selected USMAP must fail.");
        Require(File.ReadAllText(pak) == "original pak" && File.ReadAllText(mapping) == "original mapping",
            "A refused save modified a selected game input.");

        string existing = Path.Combine(root, "existing.zip");
        File.WriteAllText(existing, "keep me");
        RequireThrows<IOException>(() => model.SaveAsync(existing),
            "Saving over an existing file must fail instead of truncating it.");
        Require(File.ReadAllText(existing) == "keep me", "A refused save truncated an existing file.");
        Require(File.Exists(output) && model.HasOutput, "A refused save discarded the unsaved result.");

        if (OperatingSystem.IsLinux()) {
            SaveRacePreservesDestination(model, root, output);
        }

        RequireThrows<ArgumentException>(() => model.SaveAsync("relative.zip"),
            "A non-absolute destination must be refused visibly.");

        string destination = Path.Combine(root, "saved.zip");
        Task save = model.SaveAsync(destination);
        WaitFor(() => save.IsCompleted, "Saving the produced ZIP did not complete.");
        save.GetAwaiter().GetResult();
        Require(File.ReadAllBytes(destination).SequenceEqual(expected),
            "The saved file was not a byte-for-byte copy of the generated ZIP.");
        using (ZipArchive archive = ZipFile.OpenRead(destination)) {
            Require(archive.Entries.Count > 0, "The saved file is not a readable ZIP.");
        }

        Require(File.Exists(output) && model.HasOutput,
            "A successful save removed the still-owned generated output.");
        model.Detach();
    }

    private static void BrowserDownloadResult() {
        int[] validationCalls = [0];
        SetupViewModel setup = CreateSetup("pak", "mapping", validationCalls);
        setup.CommitInputsAsync().GetAwaiter().GetResult();
        Require(setup.IsReady, "Fixture Setup did not become ready.");
        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            (request, progress, token) => {
                progress?.Invoke(100);
                return Task.FromResult(new GenerationResult("1.0.0.0", 1, 1, 1, 1, 1, 5, 1000, 500,
                    "pal-schema.zip"));
            });

        RunGenerate(model);
        Require(!model.HasError, "A browser download was reported as a failed generation: " + model.Error);
        Require(!model.HasOutput, "A browser download must not expose a native Save action.");
        Require(model.Progress == 100 && model.Status.Contains("downloaded", StringComparison.OrdinalIgnoreCase),
            "The browser download did not reach a visible completed state.");
        Require(!File.Exists("pal-schema.zip"), "The browser contract must not be treated as a native file.");
        model.Detach();
    }

    private static void SaveRacePreservesDestination(GenerateViewModel model, string root, string output) {
        string backup = output + ".backup";
        string raced = Path.Combine(root, "race.zip");
        File.Move(output, backup);
        try {
            var fifo = new System.Diagnostics.ProcessStartInfo("mkfifo") { UseShellExecute = false };
            fifo.ArgumentList.Add(output);
            using var created = System.Diagnostics.Process.Start(fifo)!;
            Require(created.WaitForExit(5000) && created.ExitCode == 0, "Could not create the FIFO test fixture.");

            var race = Task.Run(() => model.SaveAsync(raced));
            WaitFor(HasFifoReaderWaiting, "Save did not reach the blocked source-open fixture.");
            File.WriteAllText(raced, "created by another process");

            var release = new System.Diagnostics.ProcessStartInfo("python3") { UseShellExecute = false };
            release.ArgumentList.Add("-c");
            release.ArgumentList.Add("import os,sys; fd=os.open(sys.argv[1],os.O_WRONLY); os.close(fd)");
            release.ArgumentList.Add(output);
            using var writer = System.Diagnostics.Process.Start(release)!;
            Require(writer.WaitForExit(5000) && writer.ExitCode == 0, "Could not release the blocked FIFO reader.");
            WaitFor(() => race.IsCompleted, "The raced save did not finish.");
            RequireThrows<IOException>(() => race, "The raced save must refuse an existing destination.");
            Require(File.Exists(raced) && File.ReadAllText(raced) == "created by another process",
                "A refused raced save deleted or changed an existing destination.");
        } finally {
            File.Delete(output);
            File.Move(backup, output);
        }
    }

    private static bool HasFifoReaderWaiting() {
        foreach (string task in Directory.EnumerateDirectories("/proc/self/task")) {
            try {
                if (File.ReadAllText(Path.Combine(task, "wchan")).Trim() == "wait_for_partner") {
                    return true;
                }
            } catch (IOException) {
                // A separate short-lived thread can exit while /proc is being scanned.
            }
        }

        return false;
    }

    private static void DisposeDeletesTemporaryZip() {
        SetupViewModel setup = CreateSetup("C:\\game\\Pal-Windows.pak", "C:\\game\\Mappings.usmap", new int[1]);
        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            (request, _, _) => Task.FromResult(WriteTinyZip(request)));
        RunGenerate(model);
        string? path = model.ResultPath;
        Require(path is not null && File.Exists(path), "The generated temporary ZIP was not published.");
        model.Dispose();
        Require(!File.Exists(path), "Disposing the Generate model left its temporary ZIP on disk.");
    }

    private static void RepeatedGenerateDeletesPreviousTemporaryZip() {
        SetupViewModel setup = CreateSetup("C:\\game\\Pal-Windows.pak", "C:\\game\\Mappings.usmap", new int[1]);
        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            (request, _, _) => Task.FromResult(WriteTinyZip(request)));
        RunGenerate(model);
        string? first = model.ResultPath;
        Require(first is not null && File.Exists(first), "The first generated temporary ZIP was not published.");
        RunGenerate(model);
        string? second = model.ResultPath;
        Require(second is not null && File.Exists(second), "The second generated temporary ZIP was not published.");
        Require(!string.Equals(first, second, StringComparison.Ordinal), "The second run reused the first path.");
        Require(!File.Exists(first), "Starting a second run left the prior temporary ZIP on disk.");
        model.Dispose();
    }

    private static void DisposeCancelsInFlightGeneration() {
        SetupViewModel setup = CreateSetup("C:\\game\\Pal-Windows.pak", "C:\\game\\Mappings.usmap", new int[1]);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        GenerateViewModel model = new(setup, () => new FormData(), NewRoot(),
            async (request, _, token) => {
                entered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return WriteTinyZip(request);
            });
        Task run = model.GenerateCommand.ExecuteAsync(null);
        WaitFor(() => entered.Task.IsCompleted, "Generation did not start.");
        model.Dispose();
        WaitFor(() => run.IsCompleted, "Disposing did not end the in-flight generation.");
        Dispatcher.UIThread.RunJobs();
        Require(run.IsCompleted, "The in-flight generation did not complete after disposal.");
    }

    private static void RunGenerate(GenerateViewModel model) {
        Task task = model.GenerateCommand.ExecuteAsync(null);
        WaitFor(() => task.IsCompleted, "Generation did not complete.");
        task.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
    }

    private static GenerationResult WriteTinyZip(GenerationRequest request) {
        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputZipPath)!);
        using (ZipArchive archive = ZipFile.Open(request.OutputZipPath, ZipArchiveMode.Create)) {
            ZipArchiveEntry entry = archive.CreateEntry("pal-schema.json");
            using StreamWriter writer = new(entry.Open());
            writer.Write("{}");
        }

        FileInfo info = new(request.OutputZipPath);
        return new("1.0.0.0", 1, 1, 1, 1, 1, 1, info.Length, info.Length, request.OutputZipPath);
    }

    private static string NewRoot() {
        string root = Path.Combine(Path.GetTempPath(), "palworld-randomizer-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void RequireThrows<T>(Func<Task> action, string message) where T : Exception {
        try {
            action().GetAwaiter().GetResult();
        } catch (T) {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static SetupViewModel CreateSetup(string pak, string mapping, int[] calls) => new(
        new AppPreferences { PakSelection = pak, MappingSelection = mapping, SetupCompleted = true },
        _ => Task.CompletedTask,
        (_, _, _) => {
            calls[0]++;
            return Task.FromResult(new ArchiveCompatibility(1, 1, "1.0.0.0"));
        },
        (_, _) => true,
        false) {
        PakText = pak,
        MappingText = mapping,
    };

    private static void WaitFor(Func<bool> condition, string message) {
        for (int attempt = 0; attempt < 400; attempt++) {
            Dispatcher.UIThread.RunJobs();
            if (condition()) {
                return;
            }

            Thread.Sleep(5);
        }

        Require(condition(), message);
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}