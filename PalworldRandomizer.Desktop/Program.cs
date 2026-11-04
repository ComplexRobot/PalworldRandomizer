using Avalonia;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Desktop;

/// <summary>The native host entry point; the actual UI stays on this STA thread.</summary>
internal static class Program {
    /// <summary>Loads saved metadata synchronously before the window exists; archive work stays deferred.</summary>
    [STAThread]
    private static int Main(string[] args) {
        // The native host loads its own metadata document; the original ConfigData file is never touched.
        AppPreferencesStore store = new(AppPreferencesStore.DefaultMetadataRoot);
        AppPreferences preferences;
        string? storageError = null;
        try {
            // Bounded preference I/O only; PAK/archive loading remains deferred until the UI exists.
            preferences = store.LoadAsync().GetAwaiter().GetResult();
        } catch (Exception exception) {
            preferences = new();
            storageError = exception.Message;
        }

        SetupViewModel setup = new(preferences, store.SaveAsync, DesktopArchiveValidation.ValidateAsync,
            (_, path) => File.Exists(path), false);
        if (storageError is not null) {
            setup.Toasts.Add(new("setup.persistenceFailed", storageError));
        }

        RandomizerViewModel randomizer = RandomizerViewModel.CreateAsync(
            SettingsCatalog.LoadEmbedded(), TemplateStore.CreateNative()).GetAwaiter().GetResult();
        PalworldRandomizer.App app = new() {
            Setup = setup, Randomizer = randomizer, PageAvailable = DesktopPagePolicy.IsAvailable,
        };
        NativeRenderingOptions rendering = NativeRendering.OptionsFor(preferences.Renderer);
        return AppBuilder.Configure(() => app)
            .UsePlatformDetect()
            .WithInterFont()
            .With(rendering.Win32)
            .With(rendering.X11)
            .With(rendering.AvaloniaNative)
            .StartWithClassicDesktopLifetime(args);
    }
}