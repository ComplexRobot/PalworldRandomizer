using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Views;

/// <summary>The minimal real native Generate workspace; host StorageProvider owns destination selection.</summary>
public partial class GenerateView : UserControl {
    /// <summary>Builds the real Generate control tree and wires the host-owned save action.</summary>
    public GenerateView() {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("SaveButton")!.Click += SaveClicked;
        AttachedToVisualTree += (_, _) => {
            if (DataContext is GenerateViewModel model) {
                model.Attach();
            }
        };
        DetachedFromVisualTree += (_, _) => {
            // A tab switch only detaches the cached page. The result must survive until the tab is gone.
            if (DataContext is GenerateViewModel model) {
                model.Detach();
            }
        };
    }

    /// <summary>Prompts for a real local destination and stream-copies the generated ZIP.</summary>
    /// <remarks>
    /// The picker's file is never opened for writing: a pre-created placeholder must not truncate a real
    /// file, and non-local storage files cannot offer the model's atomic no-overwrite move. Unsupported
    /// destinations are refused visibly.
    /// </remarks>
    private async void SaveClicked(object? sender, RoutedEventArgs args) {
        if (DataContext is not GenerateViewModel model || !model.HasOutput) {
            return;
        }

        try {
            var top = TopLevel.GetTopLevel(this);
            if (top is null) {
                return;
            }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                Title = "Save PalSchema",
                DefaultExtension = "zip",
                SuggestedFileName = "pal-schema.zip",
                FileTypeChoices = [new FilePickerFileType("ZIP Archive") { Patterns = ["*.zip"] }],
            });

            if (file is null) {
                return;
            }

            string? destination = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destination)) {
                throw new InvalidOperationException("Choose a destination on the local filesystem.");
            }

            await model.SaveAsync(destination);
        } catch (Exception exception) {
            model.Status = $"Save failed: {exception.Message}";
            model.HasError = true;
            model.Error = exception.Message;
        }
    }
}