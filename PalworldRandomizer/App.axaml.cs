using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PalworldRandomizer.Localization;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Views;

namespace PalworldRandomizer;

/// <summary>Shared application composition: the real shell and the implemented Setup workflow.</summary>
/// <remarks>
/// A platform entry point supplies the actual Setup model, an optional browser File picker and the
/// availability seam for workflows that are not implemented yet. No QA page is constructed here.
/// A verified recovery restore may assign a replacement model; this type rebinds the live UI to it.
/// </remarks>
public partial class App : Application {
    /// <summary>The workflows with a real page in this bounded build.</summary>
    private static readonly Func<AppPage, bool> _implemented = page => page == AppPage.Settings;

    private SetupViewModel? _boundSetup;
    private Button? _addTab;
    private bool _surfaceLoaded;

    /// <summary>Creates the shared composition; platform hosts set the model and seams before startup.</summary>
    public App() {
    }

    /// <summary>The live Setup model; a verified recovery restore may replace it.</summary>
    /// <remarks>
    /// Assignment rebinds the actual Settings page and the plus cue to the current model so a
    /// replaced model owns later validation, highlight and acknowledgement. The obsolete model's
    /// events no longer reach the UI.
    /// </remarks>
    public SetupViewModel Setup {
        get;
        set {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(field, value)) {
                return;
            }

            field = value;
            Settings?.DataContext = value;

            if (_surfaceLoaded) {
                BindSetup();
            }
        }
    } = null!;

    /// <summary>The browser's public File picker; null uses the native StorageProvider.</summary>
    public Func<string, Task<string?>>? PickFileAsync { get; init; }

    /// <summary>One narrow seam for unimplemented launcher destinations.</summary>
    public Func<AppPage, bool> PageAvailable { get; init; } = _implemented;

    /// <summary>The actual UI-independent shell state shared with recovery.</summary>
    public ShellViewModel ShellState { get; private set; } = null!;

    /// <summary>The actual shell view after framework initialization.</summary>
    public ShellView Shell { get; private set; } = null!;

    /// <summary>The actual Setup view hosted by the Settings tab.</summary>
    public SettingsView Settings { get; private set; } = null!;

    /// <summary>Raised once the real visual tree and platform lifetime members exist.</summary>
    public event Action? SurfaceReady;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted() {
        SetupViewModel setup = Setup ?? throw new InvalidOperationException(
            "The platform host must supply the Setup model before startup.");
        Settings = new SettingsView { DataContext = setup, PickFileAsync = PickFileAsync };
        ShellState = new ShellViewModel(firstRun: !setup.Preferences.SetupCompleted
            || setup.PakEmpty || setup.MappingEmpty || setup.PakInvalid || setup.MappingInvalid);
        Shell = new ShellView(ShellState, CreatePage, PageAvailable);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new Avalonia.Controls.Window {
                Title = "Palworld Randomizer",
                Width = 1280,
                Height = 940,
                MinWidth = 720,
                MinHeight = 560,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = Shell,
            };
        } else if (ApplicationLifetime is ISingleViewApplicationLifetime single) {
            single.MainView = Shell;
        }

        Shell.Loaded += (_, _) => {
            // A recovered host may replace Setup between SurfaceReady and this first Loaded event.
            if (_surfaceLoaded) {
                return;
            }

            _surfaceLoaded = true;
            _addTab = Shell.GetVisualDescendants().OfType<Button>()
                .First(button => AutomationProperties.GetAutomationId(button) == "Shell.AddTab");
            _addTab.Click += AddTabClicked;
            BindSetup();
        };

        // SurfaceReady fires before Loaded so a recovery host can supply the current model first.
        SurfaceReady?.Invoke();
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Moves the one plus subscription and deferred validation to the current Setup model.</summary>
    private void BindSetup() {
        _boundSetup?.PropertyChanged -= SetupChanged;

        _boundSetup = Setup;
        _boundSetup.PropertyChanged += SetupChanged;
        Settings?.DataContext = _boundSetup;

        ApplyHighlight();
        SetupViewModel current = _boundSetup;
        // Compatibility runs only after the first frame; constructor startup never waits for a PAK.
        Dispatcher.UIThread.Post(async () => {
            if (ReferenceEquals(Setup, current)) {
                await current.CommitInputsAsync();
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>A real plus click is acknowledged by whichever Setup model currently owns the UI.</summary>
    private void AddTabClicked(object? sender, RoutedEventArgs args) => Setup.AcknowledgeNewTab();

    /// <summary>Reflects the current model's highlight without touching the frozen shell layout.</summary>
    private void SetupChanged(object? sender, PropertyChangedEventArgs args) {
        if (args.PropertyName == nameof(SetupViewModel.HighlightNewTab)) {
            ApplyHighlight();
        }
    }

    private void ApplyHighlight() {
        if (_addTab is null) {
            return;
        }

        _addTab.BorderBrush = Setup.HighlightNewTab ? Brush.Parse("#8CB5DC") : null;
        _addTab.BorderThickness = new Thickness(Setup.HighlightNewTab ? 2 : 0);
    }

    /// <summary>Returns the real Settings page or an explicit unavailable notice for this build.</summary>
    private Control CreatePage(ShellTab tab) => tab.Page == AppPage.Settings ? Settings : new TextBlock {
        Text = EnglishMetadata.Get("Shell.Unavailable"),
        Margin = new Thickness(30),
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brush.Parse("#A5B0C0"),
    };
}
