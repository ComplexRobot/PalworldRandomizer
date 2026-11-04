using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Controls;

/// <summary>Arranges outer tabs without any scrolling surface; the final child is the add-tab button.</summary>
public sealed class TabStripPanel : Panel {
    /// <summary>The preferred readable tab width.</summary>
    private const double _tabWidth = 200;
    /// <summary>The reserved plus-button width.</summary>
    private const double _buttonWidth = 40;
    /// <summary>The comfortable actual row height.</summary>
    private const double _rowHeight = 40;
    /// <summary>The standard inter-control and inter-row gap.</summary>
    private const double _gap = 4;
    /// <summary>The stable header pressed before a possible captured drag.</summary>
    private ShellTabHeader? _dragCandidate;
    /// <summary>The initial local pointer position used for the drag threshold.</summary>
    private Point _pressPoint;
    /// <summary>Whether this panel currently owns a captured reorder gesture.</summary>
    private bool _dragging;

    /// <summary>Creates routed pointer-capture handling; no native or HTML drag/drop is used.</summary>
    public TabStripPanel() {
        AddHandler(PointerPressedEvent, OnDragPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnDragMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnDragReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        PointerCaptureLost += (_, args) => {
            if (args.Pointer.Captured != this) {
                _dragCandidate = null;
                _dragging = false;
            }
        };
    }

    /// <summary>Requests an exact strip-index reorder from the host's actual view model.</summary>
    public event Action<ShellTab, int>? ReorderRequested;

    /// <summary>Records selection-header presses without stealing ordinary button clicks.</summary>
    private void OnDragPressed(object? sender, PointerPressedEventArgs args) {
        _dragCandidate = null;
        _dragging = false;
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed || args.Source is not Visual source) {
            return;
        }

        var ancestors = source.GetVisualAncestors().Prepend(source).ToArray();
        var button = ancestors.OfType<Button>().FirstOrDefault();
        if (button?.Name != "SelectTab") {
            return;
        }

        _dragCandidate = ancestors.OfType<ShellTabHeader>().FirstOrDefault();
        _pressPoint = args.GetPosition(this);
    }

    /// <summary>Captures the pointer after a movement threshold and cancels the original button press.</summary>
    private void OnDragMoved(object? sender, PointerEventArgs args) {
        if (_dragCandidate is null || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
            return;
        }

        if (!_dragging && ((Vector)(args.GetPosition(this) - _pressPoint)).Length >= 6) {
            args.Pointer.Capture(this);
            _dragging = true;
        }

        args.Handled = _dragging;
    }

    /// <summary>Drops at the nearest visible header, including another wrap row, preserving state selection.</summary>
    private void OnDragReleased(object? sender, PointerReleasedEventArgs args) {
        var candidate = _dragCandidate;
        _dragCandidate = null;
        if (!_dragging || candidate?.Tab is null) {
            return;
        }

        _dragging = false;
        var point = args.GetPosition(this);
        var target = Children.OfType<ShellTabHeader>().Where(header => header.IsVisible)
            .MinBy(header => ((Vector)(header.Bounds.Center - point)).Length);
        args.Pointer.Capture(null);
        args.Handled = true;
        if (target is not null) {
            ReorderRequested?.Invoke(candidate.Tab, Children.IndexOf(target));
        }
    }

    /// <summary>Identifies the requested layout policy.</summary>
    public static readonly StyledProperty<TabStripMode> ModeProperty =
        AvaloniaProperty.Register<TabStripPanel, TabStripMode>(nameof(Mode));

    /// <summary>Registers layout invalidation when the host changes policy.</summary>
    static TabStripPanel() => AffectsMeasure<TabStripPanel>(ModeProperty);

    /// <summary>Gets or sets the layout policy bound to the real shell state.</summary>
    public TabStripMode Mode {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Measures actual tab rows against the constrained available width.</summary>
    protected override Size MeasureOverride(Size availableSize) {
        double width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : _tabWidth;
        double x = 0;
        double y = 0;
        int visibleCount = Mode == TabStripMode.Overflow ? VisibleHeaderCount(width) : int.MaxValue;
        int index = 0;
        foreach (var header in Children.OfType<ShellTabHeader>()) {
            header.IsVisible = index++ < visibleCount;
        }

        double headerWidth = GetHeaderWidth(width);
        foreach (var child in Children.Where(child => child.IsVisible)) {
            double itemWidth = Math.Min(width, child is ShellTabHeader ? headerWidth : _buttonWidth);
            child.Measure(new Size(itemWidth, _rowHeight));
            if (Mode == TabStripMode.Wrap && x > 0 && x + itemWidth > width) {
                x = 0;
                y += _rowHeight + _gap;
            }

            x += itemWidth + GetGap(width);
        }

        return new Size(width, Children.Count == 0 ? 0 : y + _rowHeight);
    }

    /// <summary>Gets the maximum complete visible prefix while reserving the final plus button.</summary>
    public int VisibleHeaderCount(double width)
        => Math.Min(Children.OfType<ShellTabHeader>().Count(),
            Math.Max(1, (int)Math.Floor(Math.Max(0, width - _buttonWidth) / (_tabWidth + _gap))));

    /// <summary>Preserves positive header width even when many single-row tabs need tighter spacing.</summary>
    private double GetGap(double width) {
        int count = Children.OfType<ShellTabHeader>().Count();
        return Mode == TabStripMode.Shrink && count > 0
            ? Math.Min(_gap, Math.Max(0, width - _buttonWidth) / (2 * count)) : _gap;
    }

    /// <summary>Allocates constrained width evenly to headers in the single-row shrink policy.</summary>
    private double GetHeaderWidth(double width) {
        int count = Children.OfType<ShellTabHeader>().Count();
        if (Mode == TabStripMode.Shrink && count > 0) {
            return Math.Min(_tabWidth, Math.Max(0, width - _buttonWidth - GetGap(width) * count) / count);
        }

        return Mode == TabStripMode.Overflow
            ? Math.Min(_tabWidth, Math.Max(0, width - _buttonWidth - _gap)) : _tabWidth;
    }

    /// <summary>Places tabs across real rows with the plus control last in reading order.</summary>
    protected override Size ArrangeOverride(Size finalSize) {
        double x = 0;
        double y = 0;
        double headerWidth = GetHeaderWidth(finalSize.Width);
        foreach (var child in Children.Where(child => child.IsVisible)) {
            double width = Math.Min(finalSize.Width, child is ShellTabHeader ? headerWidth : _buttonWidth);
            if (Mode == TabStripMode.Wrap && x > 0 && x + width > finalSize.Width) {
                x = 0;
                y += _rowHeight + _gap;
            }

            child.Arrange(new Rect(x, y, width, _rowHeight));
            x += width + GetGap(finalSize.Width);
        }

        return finalSize;
    }
}