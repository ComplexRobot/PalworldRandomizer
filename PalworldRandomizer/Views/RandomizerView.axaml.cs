using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
    private ComboBox? _templateSelector;
    private bool _syncingTemplateSelector;
    private bool _templateDropDownOpen;
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
        HookTemplateControls();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args) {
        _model?.PropertyChanged -= ModelChanged;

        UnhookNumericControls();
        UnhookTemplateControls();
        _model = null;
        _draftsApplied = false;
        base.OnDetachedFromVisualTree(args);
    }

    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) {
        if (args.PropertyName == nameof(RandomizerViewModel.SelectedCategory)) {
            BuildCategories();
        } else if (args.PropertyName == nameof(RandomizerViewModel.TemplateName)
                   || args.PropertyName == nameof(RandomizerViewModel.SelectedTemplateName)) {
            SyncTemplateSelector();
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

    private void HookTemplateControls() {
        _templateSelector = this.FindControl<ComboBox>("TemplateSelector");
        if (_templateSelector is null) {
            return;
        }

        _templateSelector.AddHandler(KeyDownEvent, TemplateSelectorKeyDown, RoutingStrategies.Tunnel);
        _templateSelector.LostFocus += TemplateSelectorLostFocus;
        _templateSelector.SelectionChanged += TemplateSelectorSelectionChanged;
        _templateSelector.DropDownOpened += TemplateSelectorDropDownOpened;
        _templateSelector.DropDownClosed += TemplateSelectorDropDownClosed;
        if (_model is not null) {
            _model.TemplateNames.CollectionChanged += TemplateNamesChanged;
        }

        SyncTemplateSelector();
    }

    private void UnhookTemplateControls() {
        if (_templateSelector is not null) {
            _templateSelector.RemoveHandler(KeyDownEvent, TemplateSelectorKeyDown);
            _templateSelector.LostFocus -= TemplateSelectorLostFocus;
            _templateSelector.SelectionChanged -= TemplateSelectorSelectionChanged;
            _templateSelector.DropDownOpened -= TemplateSelectorDropDownOpened;
            _templateSelector.DropDownClosed -= TemplateSelectorDropDownClosed;
        }

        if (_model is not null) {
            _model.TemplateNames.CollectionChanged -= TemplateNamesChanged;
        }

        _templateSelector = null;
        _templateDropDownOpen = false;
    }

    private void TemplateNamesChanged(object? sender, NotifyCollectionChangedEventArgs args) {
        if (_model is not null && _model.IsTemplateBusy) {
            return;
        }

        SyncTemplateSelector();
    }

    private void SyncTemplateSelector() {
        if (_model is null || _templateSelector is null) {
            return;
        }

        _syncingTemplateSelector = true;
        try {
            _templateSelector.SelectedItem = _model.TemplateName;
            _templateSelector.Text = _model.TemplateName;
        } finally {
            _syncingTemplateSelector = false;
        }
    }

    private void TemplateSelectorDropDownOpened(object? sender, EventArgs args) =>
        _templateDropDownOpen = true;

    private void TemplateSelectorDropDownClosed(object? sender, EventArgs args) =>
        _templateDropDownOpen = false;

    private void TemplateSelectorSelectionChanged(object? sender, SelectionChangedEventArgs args) {
        if (_syncingTemplateSelector || _model is null || _model.IsTemplateBusy || !_templateDropDownOpen) {
            return;
        }

        if (_templateSelector?.SelectedItem is string name
            && !string.Equals(name, _model.TemplateName, StringComparison.Ordinal)) {
            _ = _model.SelectTemplateAsync(name);
        }
    }

    private void TemplateSelectorKeyDown(object? sender, KeyEventArgs args) {
        if (_model is null || _templateSelector is null || _model.IsTemplateBusy
            || _templateDropDownOpen || args.Key != Key.Enter) {
            return;
        }

        args.Handled = true;
        _ = CommitTemplateTextAsync(_templateSelector.Text);
    }

    private void TemplateSelectorLostFocus(object? sender, FocusChangedEventArgs args) {
        if (_syncingTemplateSelector || _templateDropDownOpen || _model is null
            || _templateSelector is null || _model.IsTemplateBusy) {
            return;
        }

        if (!string.Equals(_templateSelector.Text, _model.TemplateName, StringComparison.Ordinal)) {
            // Enter is the explicit rename gesture; an uncommitted typed name is discarded on blur.
            _templateSelector.SetCurrentValue(ComboBox.TextProperty, _model.TemplateName);
        }
    }

    private async Task CommitTemplateTextAsync(string? text) {
        if (_model is null || string.Equals(text, _model.TemplateName, StringComparison.Ordinal)) {
            return;
        }

        bool committed = await _model.CommitTemplateNameAsync(text).ConfigureAwait(true);
        if (!committed) {
            _templateSelector?.SetCurrentValue(ComboBox.TextProperty, _model.TemplateName);
        }

        SyncTemplateSelector();
    }

    private async void NewTemplateClicked(object? sender, RoutedEventArgs args) {
        if (_model is null) {
            return;
        }

        await _model.CreateTemplateAsync().ConfigureAwait(true);
        SyncTemplateSelector();
    }

    private async void DeleteTemplateClicked(object? sender, RoutedEventArgs args) {
        if (_model is null) {
            return;
        }

        await _model.DeleteTemplateAsync().ConfigureAwait(true);
        SyncTemplateSelector();
    }

    private async void UndoTemplateClicked(object? sender, RoutedEventArgs args) {
        if (_model is null) {
            return;
        }

        await _model.UndoDeleteAsync().ConfigureAwait(true);
        SyncTemplateSelector();
    }

    private async void ImportTemplateClicked(object? sender, RoutedEventArgs args) {
        if (_model is null) {
            return;
        }

        TopLevel? top = TopLevel.GetTopLevel(this);
        if (top is null) {
            _model.ReportTemplateError("The file picker is unavailable in this host.");
            return;
        }

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions {
                Title = "Import Template",
                AllowMultiple = false,
                FileTypeFilter = [
                    new FilePickerFileType("Template files") { Patterns = ["*.yaml", "*.yml", "*.json"] },
                    FilePickerFileTypes.All,
                ],
            }).ConfigureAwait(true);
        if (files.Count == 0) {
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) {
            _model.ReportTemplateError("The selected file has no local path in this host.");
            return;
        }

        await _model.ImportTemplateAsync(path).ConfigureAwait(true);
        SyncTemplateSelector();
    }

    private async void ExportYamlClicked(object? sender, RoutedEventArgs args) =>
        await ExportTemplateAsync(json: false).ConfigureAwait(true);

    private async void ExportJsonClicked(object? sender, RoutedEventArgs args) =>
        await ExportTemplateAsync(json: true).ConfigureAwait(true);

    private async Task ExportTemplateAsync(bool json) {
        if (_model is null) {
            return;
        }

        TopLevel? top = TopLevel.GetTopLevel(this);
        if (top is null) {
            _model.ReportTemplateError("The file picker is unavailable in this host.");
            return;
        }

        string extension = json ? ".json" : ".yaml";
        IStorageFile? file = await top.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions {
                Title = json ? "Export Template as JSON" : "Export Template as YAML",
                SuggestedFileName = _model.TemplateName + extension,
                DefaultExtension = extension.TrimStart('.'),
                ShowOverwritePrompt = true,
                FileTypeChoices = json
                    ? [new FilePickerFileType("JSON template") { Patterns = ["*.json"] }]
                    : [new FilePickerFileType("YAML template") { Patterns = ["*.yaml", "*.yml"] }],
            }).ConfigureAwait(true);
        if (file is null) {
            return;
        }

        string? path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) {
            _model.ReportTemplateError("The selected destination has no local path in this host.");
            return;
        }

        if (!Path.HasExtension(path)) {
            path += extension;
        }

        await _model.ExportTemplateAsync(path, json).ConfigureAwait(true);
    }
}