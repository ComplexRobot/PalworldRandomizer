using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PalworldRandomizer.Localization;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Views;

/// <summary>The actual shared Setup category; other Settings workflows are not represented as implemented.</summary>
public partial class SettingsView : UserControl {
    private SetupViewModel? _model;
    private bool _refreshing;
    private bool _filenameDirty;
    private readonly Avalonia.Threading.DispatcherTimer _toastTimer = new() {
        Interval = TimeSpan.FromMilliseconds(250),
    };
    private readonly string[] _dates = ["", "M/d/yyyy", "dd/MM/yyyy", "yyyy/MM/dd", "dd MMM yyyy"];
    private readonly string[] _times = ["", "h:mm tt", "HH:mm", "HH:mm:ss"];

    private ComboBox LanguageControl => this.FindControl<ComboBox>("Language")!;
    private ComboBox RenderingControl => this.FindControl<ComboBox>("Rendering")!;
    private ComboBox DateControl => this.FindControl<ComboBox>("Date")!;
    private ComboBox TimeControl => this.FindControl<ComboBox>("Time")!;
    private CheckBox HyphensControl => this.FindControl<CheckBox>("Hyphens")!;
    private TextBox FilenameControl => this.FindControl<TextBox>("Filename")!;
    private TextBlock PreviewControl => this.FindControl<TextBlock>("Preview")!;
    private TextBlock PakHintControl => this.FindControl<TextBlock>("PakHint")!;
    private TextBlock MappingHintControl => this.FindControl<TextBlock>("MappingHint")!;
    private Border PakAreaControl => this.FindControl<Border>("PakArea")!;
    private Border MappingAreaControl => this.FindControl<Border>("MappingArea")!;

    /// <summary>Allows the browser to provide its public native File picker instead of an OS-path stream.</summary>
    public Func<string, Task<string?>>? PickFileAsync { get; set; }

    /// <summary>Builds the actual localized control tree and native drop targets.</summary>
    public SettingsView() {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        DateControl.ItemsSource = _dates.Select(FormatLabel).ToArray();
        TimeControl.ItemsSource = _times.Select(FormatLabel).ToArray();
        DataContextChanged += ContextChanged;
        PakAreaControl.AddHandler(DragDrop.DropEvent, Dropped);
        MappingAreaControl.AddHandler(DragDrop.DropEvent, Dropped);
        PakAreaControl.AddHandler(DragDrop.DragOverEvent, DragOver);
        MappingAreaControl.AddHandler(DragDrop.DragOverEvent, DragOver);
        _toastTimer.Tick += (_, _) => {
            if (_model is not null) {
                var expired = _model.Toasts.Where(toast => toast.ExpiresAt <= DateTimeOffset.UtcNow).ToArray();
                foreach (var toast in expired) {
                    _model.Dismiss(toast);
                }
            }
        };
        AttachedToVisualTree += (_, _) => _toastTimer.Start();
        DetachedFromVisualTree += (_, _) => _toastTimer.Stop();
        DataContext = new SetupViewModel();
    }

    private static string FormatLabel(string pattern) => pattern.Length == 0
        ? SetupText.Get("setup.localeDefault") : pattern;

    private void ContextChanged(object? sender, EventArgs args) {
        _model?.PropertyChanged -= ModelChanged;

        _filenameDirty = false;
        _model = DataContext as SetupViewModel;
        if (_model is not null) {
            _model.PropertyChanged += ModelChanged;
            Refresh();
        }
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args) {
        if (args.PropertyName == nameof(SetupViewModel.Preferences)) {
            Refresh();
        }
    }

    private void Refresh() {
        if (_model is null) {
            return;
        }

        _refreshing = true;
        var preferences = _model.Preferences;
        LanguageControl.SelectedIndex = 0;
        RenderingControl.SelectedIndex = preferences.Renderer == "software" ? 1 : 0;
        DateControl.SelectedIndex = Math.Max(0, Array.IndexOf(_dates, preferences.DatePattern ?? ""));
        TimeControl.SelectedIndex = Math.Max(0, Array.IndexOf(_times, preferences.TimePattern ?? ""));
        HyphensControl.IsChecked = preferences.UseHyphens;
        if (!_filenameDirty || Serializer.AppPreferencesStore.IsValidFilenamePattern(FilenameControl.Text)) {
            _filenameDirty = false;
            FilenameControl.Text = preferences.FilenamePattern;
        }

        PakHintControl.Text = MappingHintControl.Text
            = SetupText.Get(_model.CanTypePaths ? "setup.drop" : "setup.browserFiles");
        UpdatePreview();
        _refreshing = false;
    }

    private async void PreferenceChanged(object? sender, SelectionChangedEventArgs args) => await SaveControlsAsync();

    private async void SeparatorChanged(object? sender, RoutedEventArgs args) => await SaveControlsAsync();

    private async void FilenameChanged(object? sender, TextChangedEventArgs args) {
        if (!_refreshing) {
            _filenameDirty = true;
        }

        await SaveControlsAsync();
    }

    private async Task SaveControlsAsync() {
        if (_refreshing || _model is null || DateControl.SelectedIndex < 0 || TimeControl.SelectedIndex < 0) {
            return;
        }

        bool validFilename = Serializer.AppPreferencesStore.IsValidFilenamePattern(FilenameControl.Text);
        var edited = _model.Preferences with {
            Locale = "en-US",
            Renderer = RenderingControl.SelectedIndex == 1 ? "software" : "gpu-preferred",
            DatePattern = DateControl.SelectedIndex == 0 ? null : _dates[DateControl.SelectedIndex],
            TimePattern = TimeControl.SelectedIndex == 0 ? null : _times[TimeControl.SelectedIndex],
            UseHyphens = HyphensControl.IsChecked == true,
            FilenamePattern = validFilename ? FilenameControl.Text! : _model.Preferences.FilenamePattern,
        };
        if (edited != _model.Preferences) {
            await _model.UpdatePreferencesAsync(edited);
        }

        UpdatePreview();
    }

    private void UpdatePreview() {
        if (_model is null) {
            return;
        }

        if (_filenameDirty && !Serializer.AppPreferencesStore.IsValidFilenamePattern(FilenameControl.Text)) {
            PreviewControl.Text = SetupText.Get("setup.invalidFormat");
            return;
        }

        var value = _model.Preferences;
        var now = DateTime.Now;
        try {
            PreviewControl.Text = SetupText.Get("setup.preview") + ": "
                + now.ToString(value.EffectiveDatePattern, CultureInfo.GetCultureInfo(value.Locale)) + " · "
                + now.ToString(value.EffectiveTimePattern, CultureInfo.GetCultureInfo(value.Locale)) + " · "
                + now.ToString(value.FilenamePattern, CultureInfo.InvariantCulture);
        } catch (FormatException) {
            PreviewControl.Text = SetupText.Get("setup.invalidFormat");
        }
    }

    private async void InputKeyDown(object? sender, KeyEventArgs args) {
        if (args.Key == Key.Enter && _model is not null) {
            await _model.CommitInputsAsync();
        }
    }

    private async void InputLostFocus(object? sender, RoutedEventArgs args) {
        if (_model is not null) {
            await _model.CommitInputsAsync();
        }
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs args) {
        if (sender is not Button { Tag: string role } || _model is null) {
            return;
        }

        try {
            string? selected;
            if (PickFileAsync is not null) {
                selected = await PickFileAsync(role);
            } else {
                var top = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException();
                var files = await top.StorageProvider.OpenFilePickerAsync(new() {
                    AllowMultiple = false,
                    FileTypeFilter = [new(role == "pak" ? SetupText.Get("setup.pak") : SetupText.Get("setup.mapping")) {
                        Patterns = [role == "pak" ? "*.pak" : "*.usmap"],
                    }, new(SetupText.Get("setup.allFiles")) { Patterns = ["*.*"] }],
                });
                selected = files.FirstOrDefault()?.TryGetLocalPath();
                foreach (var file in files) {
                    file.Dispose();
                }
            }

            if (selected is not null) {
                await SelectAsync(role, selected);
            }
        } catch (Exception exception) {
            _model.Toasts.Add(new("setup.pickFailed", exception.Message));
        }
    }

    /// <summary>Validates a platform picker/drop selection using the same path as typed inputs.</summary>
    public async Task SelectAsync(string role, string value) {
        if (_model is null) {
            return;
        }

        _model.SelectionChanged();
        if (role == "pak") {
            _model.PakText = value;
        } else {
            _model.MappingText = value;
        }

        await _model.CommitInputsAsync();
    }

    private void DragOver(object? sender, DragEventArgs args) {
        args.DragEffects = _model?.CanTypePaths == true ? DragDropEffects.Copy : DragDropEffects.None;
        args.Handled = true;
    }

    private async void Dropped(object? sender, DragEventArgs args) {
        if (_model?.CanTypePaths != true) {
            return;
        }

        string? path = args.DataTransfer.Items.Select(item => item.TryGetFile()?.TryGetLocalPath())
            .FirstOrDefault(path => path is not null);
        if (path is not null) {
            await SelectAsync(ReferenceEquals(sender, PakAreaControl) ? "pak" : "mapping", path);
        }

        args.Handled = true;
    }

    private void CloseToastClicked(object? sender, RoutedEventArgs args) {
        if (sender is Button { Tag: SetupToast toast }) {
            _model?.Dismiss(toast);
        }
    }
}