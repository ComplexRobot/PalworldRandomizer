using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PalworldRandomizer.ViewModels;
using Ursa.Controls;

namespace PalworldRandomizer.Views;

/// <summary>The actual first Randomizer category; unavailable categories are shown but never simulated.</summary>
public partial class RandomizerView : UserControl {
    private RandomizerViewModel? _model;
    private NumericIntUpDown? _spawnListSize;
    private NumericIntUpDown? _vanillaPlusChance;
    private TextBox? _spawnListSizeText;
    private TextBox? _vanillaPlusChanceText;
    private bool _draftsApplied;

    /// <summary>Creates the compiled category view; the host supplies the shared Default session.</summary>
    public RandomizerView() => AvaloniaXamlLoader.Load(this);

    /// <summary>Creates the view over the shared Default session used by every tab.</summary>
    public RandomizerView(RandomizerViewModel model) : this() {
        ArgumentNullException.ThrowIfNull(model);
        DataContext = model;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args) {
        base.OnAttachedToVisualTree(args);
        _model = DataContext as RandomizerViewModel;
        _model?.PropertyChanged += ModelChanged;

        BuildCategories();
        HookNumericControls();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args) {
        _model?.PropertyChanged -= ModelChanged;

        UnhookNumericControls();
        _model = null;
        _draftsApplied = false;
        base.OnDetachedFromVisualTree(args);
    }

    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) {
        if (args.PropertyName == nameof(RandomizerViewModel.SelectedCategory)) {
            BuildCategories();
        }
    }

    private void BuildCategories() {
        var panel = this.FindControl<StackPanel>("CategoriesPanel");
        if (panel is null) {
            return;
        }

        panel.Children.Clear();
        if (_model is null) {
            return;
        }

        foreach (RandomizerCategory category in _model.Categories) {
            bool selected = string.Equals(category.Key, _model.SelectedCategory, StringComparison.Ordinal);
            var title = new TextBlock {
                Text = category.Title,
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontWeight = selected ? Avalonia.Media.FontWeight.SemiBold
                    : Avalonia.Media.FontWeight.Normal,
            };
            var stack = new StackPanel { Spacing = 2 };
            stack.Children.Add(title);
            if (!category.IsAvailable) {
                stack.Children.Add(new TextBlock {
                    Text = "Not available in this build",
                    FontSize = 11,
                    Foreground = Avalonia.Media.Brush.Parse("#6F7F94"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                });
            }

            var button = new Button {
                Tag = category.Key,
                Content = stack,
                IsEnabled = category.IsAvailable,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Avalonia.Thickness(12, 9),
                CornerRadius = new Avalonia.CornerRadius(5),
                BorderThickness = new Avalonia.Thickness(0),
                Background = selected ? Avalonia.Media.Brush.Parse("#34465E")
                    : Avalonia.Media.Brushes.Transparent,
                Opacity = category.IsAvailable ? 1 : 0.55,
            };
            AutomationProperties.SetAutomationId(button, "Randomizer.Category." + category.Key);
            if (category.IsAvailable) {
                button.Click += CategoryClicked;
            }

            panel.Children.Add(button);
        }
    }

    private void CategoryClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) {
        if (sender is Button { Tag: string key } && _model is not null) {
            _model.SelectedCategory = key;
        }
    }

    private void HookNumericControls() {
        _spawnListSize = this.FindControl<NumericIntUpDown>("SpawnListSizeBox");
        _vanillaPlusChance = this.FindControl<NumericIntUpDown>("VanillaPlusChanceBox");
        if (_spawnListSize is null || _vanillaPlusChance is null) {
            return;
        }

        _spawnListSize.GotFocus += SpawnListSizeFocused;
        _vanillaPlusChance.GotFocus += VanillaPlusChanceFocused;
        _spawnListSize.ApplyTemplate();
        _vanillaPlusChance.ApplyTemplate();
        _spawnListSizeText = _spawnListSize.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        _vanillaPlusChanceText = _vanillaPlusChance.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault();
        _spawnListSizeText?.TextChanged += SpawnListSizeTextChanged;
        _vanillaPlusChanceText?.TextChanged += VanillaPlusChanceTextChanged;

        ApplyRawDrafts();
    }

    private void UnhookNumericControls() {
        _spawnListSize?.GotFocus -= SpawnListSizeFocused;
        _vanillaPlusChance?.GotFocus -= VanillaPlusChanceFocused;
        _spawnListSizeText?.TextChanged -= SpawnListSizeTextChanged;
        _vanillaPlusChanceText?.TextChanged -= VanillaPlusChanceTextChanged;

        _spawnListSize = null;
        _vanillaPlusChance = null;
        _spawnListSizeText = null;
        _vanillaPlusChanceText = null;
    }

    private void ApplyRawDrafts() {
        if (_model is null || _draftsApplied) {
            return;
        }

        _draftsApplied = true;
        if (_spawnListSizeText is not null
            && !string.Equals(_spawnListSizeText.Text, _model.SpawnListSizeText,
                StringComparison.Ordinal)) {
            _spawnListSizeText.Text = _model.SpawnListSizeText;
        }

        if (_vanillaPlusChanceText is not null
            && !string.Equals(_vanillaPlusChanceText.Text, _model.VanillaPlusChanceText,
                StringComparison.Ordinal)) {
            _vanillaPlusChanceText.Text = _model.VanillaPlusChanceText;
        }
    }

    private void SpawnListSizeFocused(object? sender, FocusChangedEventArgs args) =>
        _model?.FocusSpawnListSize();

    private void VanillaPlusChanceFocused(object? sender, FocusChangedEventArgs args) =>
        _model?.FocusVanillaPlusChance();

    private void SpawnListSizeTextChanged(object? sender, TextChangedEventArgs args) =>
        _model?.SetSpawnListSizeText(_spawnListSizeText?.Text);

    private void VanillaPlusChanceTextChanged(object? sender, TextChangedEventArgs args) =>
        _model?.SetVanillaPlusChanceText(_vanillaPlusChanceText?.Text);
}