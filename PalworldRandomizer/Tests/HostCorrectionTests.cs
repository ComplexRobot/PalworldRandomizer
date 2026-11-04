using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PalworldRandomizer.Models;
using PalworldRandomizer.ViewModels;

// Exercises the actual Desktop host and shared App; no cloned model or QA fixture page.
internal static class HostCorrectionTests {
    internal static int Main() {
        int failures = 0;
        failures += Run("Desktop Main is synchronous STA and software preference reaches public options",
            NativeStartupContract);
        failures += Run("Setup ownership follows SurfaceReady-before-Loaded and later replacement",
            SetupOwnership);
        Console.WriteLine($"{2 - failures}/2 host correction checks passed.");
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
        MethodInfo main = program!.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
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
                $"{renderer} {property} modes were [{string.Join(", ", actual)}], expected [{string.Join(", ", expected)}].");
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