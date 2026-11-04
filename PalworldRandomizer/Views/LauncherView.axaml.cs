using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PalworldRandomizer.Localization;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Views;

/// <summary>Displays the real searchable launcher; destination workflows belong to the injected page factory.</summary>
public partial class LauncherView : UserControl {
    /// <summary>Associates actual command buttons with their stable enum destinations.</summary>
    private readonly Dictionary<Button, LauncherDestination> _destinations = [];
    /// <summary>The current model whose search changes update the destination controls.</summary>
    private LauncherViewModel? _subscribedModel;

    /// <summary>Creates the localized launcher markup.</summary>
    public LauncherView() {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<TextBlock>("Heading")!.Text = EnglishMetadata.Get("Launcher.Heading");
        this.FindControl<TextBlock>("Introduction")!.Text = EnglishMetadata.Get("Launcher.Introduction");
        this.FindControl<TextBlock>("NoResults")!.Text = EnglishMetadata.Get("Launcher.NoResults");
        this.FindControl<TextBox>("Search")!.PlaceholderText = EnglishMetadata.Get("Launcher.Search");
        DataContextChanged += (_, _) => Populate();
    }

    /// <summary>Builds the four command-bound destination controls from typed enum metadata.</summary>
    private void Populate() {
        _subscribedModel?.PropertyChanged -= OnFilterChanged;
        _subscribedModel = null;

        var panel = this.FindControl<StackPanel>("Destinations")!;
        panel.Children.Clear();
        _destinations.Clear();
        if (DataContext is not LauncherViewModel model) {
            return;
        }

        foreach (var destination in model.Destinations) {
            var text = new StackPanel { Spacing = 4 };
            text.Children.Add(new TextBlock {
                Text = destination.Title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
            });
            text.Children.Add(new TextBlock {
                Text = destination.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush.Parse("#A5B0C0"),
            });
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*") };
            content.Children.Add(CreateIcon(destination.Page));
            Grid.SetColumn(text, 1);
            content.Children.Add(text);
            var button = new Button
            {
                Content = content,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Padding = new Avalonia.Thickness(16, 12),
                Command = model.NavigateCommand,
                CommandParameter = destination,
                IsEnabled = destination.IsAvailable,
                Opacity = destination.IsAvailable ? 1 : 0.55,
            };
            if (!destination.IsAvailable) {
                ToolTip.SetTip(button, EnglishMetadata.Get("Shell.Unavailable"));
            }

            AutomationProperties.SetAutomationId(button, "Launcher." + destination.Page);
            _destinations.Add(button, destination);
            panel.Children.Add(button);
        }

        _subscribedModel = model;
        model.PropertyChanged += OnFilterChanged;
        UpdateFilter(model);
    }

    /// <summary>Draws shuffle arrows, output, a gear, and editable spawn nodes without font dependencies.</summary>
    private static Avalonia.Controls.Shapes.Path CreateIcon(AppPage page) {
        string geometry = page switch
        {
            AppPage.Randomizer => "M 2,5 H 5 C 11,5 13,19 19,19 H 22 M 18,15 L 22,19 18,23 "
                + "M 2,19 H 5 C 11,19 13,5 19,5 H 22 M 18,1 L 22,5 18,9",
            AppPage.Generate => "M 4,2 H 15 L 21,8 V 22 H 4 Z M 15,2 V 8 H 21 "
                + "M 12,10 V 18 M 8,14 L 12,18 16,14",
            AppPage.Settings => "M 8,1 H 14 L 15,5 19,4 22,9 19,12 22,15 19,20 15,19 "
                + "14,23 H 8 L 7,19 3,20 0,15 3,12 0,9 3,4 7,5 Z "
                + "M 11,8 A 4,4 0 1 0 11,16 A 4,4 0 1 0 11,8 Z",
            AppPage.SpawnEditor => "M 5,5 H 18 M 5,5 V 18 H 10 M 2,2 H 8 V 8 H 2 Z "
                + "M 15,2 H 21 V 8 H 15 Z M 2,15 H 8 V 21 H 2 Z "
                + "M 12,19 L 19,12 22,15 15,22 11,23 Z M 17,14 L 20,17",
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };
        return new Avalonia.Controls.Shapes.Path {
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform,
            Stroke = Brush.Parse("#A9BFDA"),
            StrokeThickness = 1.5,
            Data = PathGeometry.Parse(geometry),
            IsHitTestVisible = false,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        };
    }

    /// <summary>Refreshes the visible destination set after a bound search edit.</summary>
    private void OnFilterChanged(object? sender, PropertyChangedEventArgs args) {
        if (sender is LauncherViewModel model && args.PropertyName == nameof(model.SearchText)) {
            UpdateFilter(model);
        }
    }

    /// <summary>Matches localized titles and descriptions without changing stable destination keys.</summary>
    private void UpdateFilter(LauncherViewModel model) {
        string query = model.SearchText.Trim();
        foreach (var (button, destination) in _destinations) {
            button.IsVisible = destination.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || destination.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        this.FindControl<TextBlock>("NoResults")!.IsVisible = !_destinations.Keys.Any(button => button.IsVisible);
    }
}