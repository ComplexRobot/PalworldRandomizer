using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalworldRandomizer.Localization;

namespace PalworldRandomizer.ViewModels;

/// <summary>One localized, searchable launcher destination backed by an application page enum.</summary>
/// <param name="Page">The stable navigation destination.</param>
public sealed record LauncherDestination(AppPage Page) {
    /// <summary>Whether this build provides a real workflow for the destination.</summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>Gets the localized title.</summary>
    public string Title => EnglishMetadata.Get($"Shell.Page.{Page}");

    /// <summary>Gets the localized explanatory subtitle.</summary>
    public string Description => EnglishMetadata.Get($"Launcher.Description.{Page}");
}

/// <summary>Filters the four destinations and navigates within the owning shell's selected tab.</summary>
/// <remarks>Creates a launcher attached to its owning shell, without implementing destination workflows.</remarks>
/// <param name="shell">The owning shell whose selected tab receives navigation.</param>
/// <param name="available">Host availability seam; omitted keeps every destination selectable.</param>
public sealed class LauncherViewModel(ShellViewModel shell, Func<AppPage, bool>? available = null)
    : ObservableObject {

    /// <summary>Gets the complete stable set; hidden entries retain their layout identity.</summary>
    public IReadOnlyList<LauncherDestination> Destinations { get; } =
    [
        new(AppPage.Randomizer) { IsAvailable = available?.Invoke(AppPage.Randomizer) ?? true },
        new(AppPage.Generate) { IsAvailable = available?.Invoke(AppPage.Generate) ?? true },
        new(AppPage.Settings) { IsAvailable = available?.Invoke(AppPage.Settings) ?? true },
        new(AppPage.SpawnEditor) { IsAvailable = available?.Invoke(AppPage.SpawnEditor) ?? true },
    ];

    /// <summary>Gets or changes the text used to filter the localized labels.</summary>
    public string SearchText {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Gets the command navigating within the selected tab.</summary>
    public IRelayCommand<LauncherDestination> NavigateCommand { get; } =
        new RelayCommand<LauncherDestination>(destination => {
            if (destination is { IsAvailable: true }) {
                shell.NavigateSelected(destination.Page);
            }
        });
}