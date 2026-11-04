using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PalworldRandomizer.Localization;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Controls;

/// <summary>Displays one stable shell identity with a typed, localized navigation title.</summary>
public partial class ShellTabHeader : UserControl {
    /// <summary>Creates the compiled header markup.</summary>
    public ShellTabHeader() {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, _) => {
            bool compact = Bounds.Width < 88;
            this.FindControl<Grid>("HeaderLayout")!.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 32);
            this.FindControl<Button>("SelectTab")!.Padding = new Avalonia.Thickness(compact ? 2 : 12, 8);
            this.FindControl<Button>("CloseTab")!.IsHitTestVisible = !compact;
        };
    }

    /// <summary>Creates the header and routes selection through the actual shell state.</summary>
    public ShellTabHeader(ShellTab tab, ShellViewModel shell) : this() {
        Tab = tab;
        DataContext = tab;
        AutomationProperties.SetAutomationId(this, "Shell.Tab." + tab.Id);
        var select = this.FindControl<Button>("SelectTab")!;
        AutomationProperties.SetAutomationId(select, "Shell.Select." + tab.Id);
        select.Click += (_, _) => shell.SelectedTab = tab;
        AddHandler(PointerReleasedEvent, (_, args) => {
            if (args.InitialPressMouseButton == MouseButton.Middle && !tab.IsClosed) {
                shell.CloseTab(tab);
                args.Handled = true;
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        var close = this.FindControl<Button>("CloseTab")!;
        close.Content = EnglishMetadata.Get("Shell.CloseTab");
        AutomationProperties.SetAutomationId(close, "Shell.Close." + tab.Id);
        close.Click += (_, _) => shell.CloseTab(tab);
        var undo = this.FindControl<Button>("UndoClose")!;
        undo.Content = CreateUndoContent();
        AutomationProperties.SetName(undo, EnglishMetadata.Get("Shell.Undo"));
        AutomationProperties.SetAutomationId(undo, "Shell.Undo." + tab.Id);
        undo.Click += (_, _) => shell.UndoClose(tab);
    }

    /// <summary>Pairs a small return arrow with its localized label for a closed identity.</summary>
    internal static StackPanel CreateUndoContent() {
        var content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new Avalonia.Controls.Shapes.Path {
            Width = 14,
            Height = 14,
            Stretch = Avalonia.Media.Stretch.Uniform,
            Stroke = Avalonia.Media.Brush.Parse("#C4CEDD"),
            StrokeThickness = 1.6,
            Data = Avalonia.Media.PathGeometry.Parse("M 6,1 L 1,6 6,11 M 1,6 H 9 A 4,4 0 0 1 9,14 H 7"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsHitTestVisible = false,
        });
        content.Children.Add(new TextBlock { Text = EnglishMetadata.Get("Shell.Undo") });
        return content;
    }

    /// <summary>Gets the stable tab represented by this header, when configured.</summary>
    public ShellTab? Tab { get; }
}