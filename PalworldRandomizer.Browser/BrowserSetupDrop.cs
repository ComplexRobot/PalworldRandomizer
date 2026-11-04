using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Views;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace PalworldRandomizer.Browser;

/// <summary>Targets real Avalonia file areas while extracting raw Files through the public DOM API.</summary>
public sealed class BrowserSetupDrop : IDisposable {
    private readonly SettingsView _view;
    private readonly BrowserSetupFiles _files;
    private readonly Document _document = SpawnJSRuntime.Instance.Get<Document>("document");

    /// <summary>Registers targeted capture handlers; unrelated drops retain their original handlers.</summary>
    public BrowserSetupDrop(SettingsView view, BrowserSetupFiles files) {
        _view = view;
        _files = files;
        _document.AddEventListener<DragEvent>("dragover", Over, true);
        _document.AddEventListener<DragEvent>("drop", Drop, true);
    }

    private string? Hit(double x, double y) {
        var top = TopLevel.GetTopLevel(_view);
        using var canvas = _document.QuerySelector<HTMLElement>("canvas");
        if (top is null || canvas is null || !_view.IsEffectivelyVisible) {
            return null;
        }

        using var rectangle = canvas.GetBoundingClientRect();
        if (rectangle.Width <= 0 || rectangle.Height <= 0) {
            return null;
        }

        Point point = new((x - rectangle.Left) * top.ClientSize.Width / rectangle.Width,
            (y - rectangle.Top) * top.ClientSize.Height / rectangle.Height);
        var target = top.InputHitTest(point);
        foreach (string name in new[] { "PakArea", "MappingArea" }) {
            var area = _view.FindControl<Border>(name)!;
            if (target is Visual visual && (visual == area || area.IsVisualAncestorOf(visual))) {
                return name == "PakArea" ? "pak" : "mapping";
            }
        }

        return null;
    }

    private void Over(DragEvent args) {
        if (Hit(args.ClientX, args.ClientY) is not null) {
            args.PreventDefault();
            args.StopImmediatePropagation();
            using var transfer = args.DataTransfer;
            transfer?.DropEffect = "copy";
        }
    }

    private async void Drop(DragEvent args) {
        string? role = Hit(args.ClientX, args.ClientY);
        if (role is null) {
            return;
        }

        args.PreventDefault();
        args.StopImmediatePropagation();
        try {
            using var transfer = args.DataTransfer;
            using var items = transfer?.Items;
            if (items is null) {
                return;
            }

            for (int index = 0; index < items.Length; index++) {
                using var item = items.Item(index);
                var file = item.GetAsFile();
                if (file is not null) {
                    await _view.SelectAsync(role, _files.Adopt(role, file));
                    break;
                }
            }
        } catch (Exception exception) {
            if (_view.DataContext is SetupViewModel model) {
                model.Toasts.Add(new("setup.pickFailed", exception.Message));
            }
        }
    }

    /// <summary>Removes the exact handlers before releasing the DOM wrapper.</summary>
    public void Dispose() {
        _document.RemoveEventListener<DragEvent>("dragover", Over, true);
        _document.RemoveEventListener<DragEvent>("drop", Drop, true);
        _document.Dispose();
    }
}