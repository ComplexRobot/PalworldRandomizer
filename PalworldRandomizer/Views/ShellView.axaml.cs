using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PalworldRandomizer.Controls;
using PalworldRandomizer.Localization;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Views;

/// <summary>Hosts tab navigation and the launcher; actual workspace controls are supplied by its owner.</summary>
public partial class ShellView : UserControl {
    /// <summary>Keeps stable header controls for the current real tab collection.</summary>
    private readonly Dictionary<ShellTab, ShellTabHeader> _headers = [];
    /// <summary>Caches the actual factory content for each current tab address.</summary>
    private readonly Dictionary<Guid, (HistoryEntry Address, Control Content)> _pages = [];
    /// <summary>The final strip control, kept outside tab templates.</summary>
    private readonly Button _addTab;
    /// <summary>The injected UI-independent state, owned by the host.</summary>
    private readonly ShellViewModel? _state;
    /// <summary>The owner-provided real destination factory.</summary>
    private readonly Func<ShellTab, Control>? _pageFactory;
    /// <summary>The host's one narrow availability seam for launcher destinations.</summary>
    private readonly Func<AppPage, bool>? _pageAvailable;
    /// <summary>Prevents duplicate state subscriptions across visual-tree reattachment.</summary>
    private bool _subscribed;
    /// <summary>Polls the real state clock only while attached to a host.</summary>
    private readonly Avalonia.Threading.DispatcherTimer _expiryTimer;
    /// <summary>The current bounded, non-scrolling overflow-menu page.</summary>
    private int _overflowPage;

    /// <summary>Creates unconfigured markup for Avalonia's XAML infrastructure; no fake pages are supplied.</summary>
    public ShellView() {
        AvaloniaXamlLoader.Load(this);
        _expiryTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _expiryTimer.Tick += (_, _) => _state?.PruneClosedTabs();
        _addTab = new Button { Content = EnglishMetadata.Get("Shell.AddTab"), Width = 40, Height = 40 };
        AutomationProperties.SetAutomationId(_addTab, "Shell.AddTab");
        _addTab.Click += (_, _) => _state?.OpenTab();
        var settings = this.FindControl<Button>("Settings")!;
        var settingsLabel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        settingsLabel.Children.Add(new Avalonia.Controls.Shapes.Path {
            Width = 16,
            Height = 16,
            Stretch = Avalonia.Media.Stretch.Uniform,
            Fill = Avalonia.Media.Brush.Parse("#C4CEDD"),
            Data = Avalonia.Media.Geometry.Parse("M 8,0 L 12,0 13,4 16,3 19,6 17,9 20,10 "
                + "20,14 16,15 17,18 14,21 11,19 10,22 6,22 5,18 2,19 -1,16 1,13 -2,12 "
                + "-2,8 2,7 1,4 4,1 7,3 Z M 9,6 A 5,5 0 1 0 9,16 A 5,5 0 1 0 9,6 Z"),
        });
        settings.Content = settingsLabel;
        AutomationProperties.SetName(settings, EnglishMetadata.Get("Shell.Settings"));
        ToolTip.SetTip(settings, EnglishMetadata.Get("Shell.Settings"));
        settings.Click += (_, _) => _state?.OpenSettings();
        var back = this.FindControl<Button>("Back")!;
        var forward = this.FindControl<Button>("Forward")!;
        back.Content = EnglishMetadata.Get("Shell.Back");
        forward.Content = EnglishMetadata.Get("Shell.Forward");
        back.Click += (_, _) => _state?.SelectedTab.Back();
        forward.Click += (_, _) => _state?.SelectedTab.Forward();
        AddHandler(PointerPressedEvent, OnMouseNavigation, RoutingStrategies.Tunnel, handledEventsToo: true);
        var overflow = this.FindControl<Button>("Overflow")!;
        overflow.Content = new Avalonia.Controls.Shapes.Path {
            Width = 18,
            Height = 4,
            Stretch = Avalonia.Media.Stretch.Uniform,
            Fill = Avalonia.Media.Brush.Parse("#DEE4ED"),
            IsHitTestVisible = false,
            Data = Avalonia.Media.PathGeometry.Parse("M 2,0 A 2,2 0 1 0 2,4 A 2,2 0 1 0 2,0 Z "
                + "M 9,0 A 2,2 0 1 0 9,4 A 2,2 0 1 0 9,0 Z "
                + "M 16,0 A 2,2 0 1 0 16,4 A 2,2 0 1 0 16,0 Z"),
        };
        AutomationProperties.SetName(overflow, EnglishMetadata.Get("Shell.Overflow"));
        ToolTip.SetTip(overflow, EnglishMetadata.Get("Shell.Overflow"));
        overflow.Click += (_, _) => {
            _overflowPage = 0;
            ShowOverflow();
        };
        this.FindControl<Grid>("OverflowOverlay")!.PointerPressed += (_, args) => {
            if (args.Source == this.FindControl<Grid>("OverflowOverlay")) {
                HideOverflow();
            }
        };
        KeyDown += (_, args) => {
            if (args.Key == Key.Escape) {
                HideOverflow();
            }
        };
        this.FindControl<TabStripPanel>("Strip")!.SizeChanged += (_, args) => {
            if (args.WidthChanged) {
                HideOverflow();
            }
        };
        SizeChanged += (_, args) => {
            if (args.HeightChanged) {
                HideOverflow();
            }
        };
        LayoutUpdated += (_, _) => EnsureSelectedVisible();
        this.FindControl<TabStripPanel>("Strip")!.ReorderRequested += (tab, index) => _state?.ReorderTab(tab, index);
    }

    /// <summary>Creates a shell using the approved state and a real host-supplied destination factory.</summary>
    public ShellView(ShellViewModel state, Func<ShellTab, Control> pageFactory,
        Func<AppPage, bool>? pageAvailable = null) : this() {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pageFactory);
        _state = state;
        _pageFactory = pageFactory;
        _pageAvailable = pageAvailable;
        DataContext = state;
        SynchronizeTabs();
        RefreshSelection();
    }

    /// <summary>Reattaches state listeners after the control is returned to a visual tree.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args) {
        base.OnAttachedToVisualTree(args);
        BuildWindowControls();
        _state?.PruneClosedTabs();
        _expiryTimer.Start();
        Subscribe();
        SynchronizeTabs();
        RefreshSelection();
    }

    /// <summary>Releases state listeners while the shell is not hosted.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args) {
        base.OnDetachedFromVisualTree(args);
        _expiryTimer.Stop();
        if (_state is not null && _subscribed) {
            _state.PropertyChanged -= OnShellChanged;
            _state.Tabs.CollectionChanged -= OnTabsChanged;
            foreach (var tab in _headers.Keys) {
                tab.PropertyChanged -= OnTabChanged;
            }

            _subscribed = false;
        }
    }

    /// <summary>Creates native Window actions only for a desktop Window host, never for Browser.</summary>
    private void BuildWindowControls() {
        var panel = this.FindControl<StackPanel>("WindowControls")!;
        panel.Children.Clear();
        if (OperatingSystem.IsBrowser() || TopLevel.GetTopLevel(this) is not Avalonia.Controls.Window window) {
            return;
        }

        foreach (string action in new[] { "Move", "Minimize", "Maximize", "Close" }) {
            var button = new Button
            {
                Content = EnglishMetadata.Get("Shell.Window." + action),
                Width = 36,
                Height = 40,
                Padding = default,
            };
            AutomationProperties.SetAutomationId(button, "Shell.Window." + action);
            AutomationProperties.SetName(button, EnglishMetadata.Get("Shell.Window.Name." + action));
            ToolTip.SetTip(button, EnglishMetadata.Get("Shell.Window.Name." + action));
            if (action == "Move") {
                button.AddHandler(PointerPressedEvent, (_, args) => {
                    if (args.GetCurrentPoint(button).Properties.IsLeftButtonPressed) {
                        window.BeginMoveDrag(args);
                        args.Handled = true;
                    }
                }, RoutingStrategies.Tunnel, handledEventsToo: true);
            } else {
                button.Click += (_, _) => {
                    switch (action) {
                        case "Minimize":
                            window.WindowState = WindowState.Minimized;
                            break;
                        case "Maximize":
                            window.WindowState = window.WindowState == WindowState.Maximized
                                ? WindowState.Normal : WindowState.Maximized;
                            break;
                        case "Close":
                            window.Close();
                            break;
                    }
                };
            }

            panel.Children.Add(button);
        }
    }

    /// <summary>Attaches exactly one set of listeners to the injected state.</summary>
    private void Subscribe() {
        if (_state is null || _subscribed) {
            return;
        }

        _subscribed = true;
        _state.PropertyChanged += OnShellChanged;
        _state.Tabs.CollectionChanged += OnTabsChanged;
        foreach (var tab in _headers.Keys) {
            tab.PropertyChanged += OnTabChanged;
        }
    }

    /// <summary>Reflects selection changes without replacing unchanged destination content.</summary>
    private void OnShellChanged(object? sender, PropertyChangedEventArgs args) => RefreshSelection();

    /// <summary>Synchronizes stable headers when the actual ordered collection changes.</summary>
    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs args) {
        SynchronizeTabs();
        if (this.FindControl<Grid>("OverflowOverlay")!.IsVisible) {
            ShowOverflow();
        }
    }

    /// <summary>Reflects the current address after navigation in the selected tab.</summary>
    private void OnTabChanged(object? sender, PropertyChangedEventArgs args) {
        if (sender == _state?.SelectedTab && args.PropertyName == nameof(ShellTab.HistoryIndex)) {
            RefreshSelection();
        }
    }

    /// <summary>Preserves control identity while applying membership and order from the real state.</summary>
    private void SynchronizeTabs() {
        if (_state is null) {
            return;
        }

        foreach (var stale in _headers.Keys.Except(_state.Tabs).ToArray()) {
            stale.PropertyChanged -= OnTabChanged;
            _headers.Remove(stale);
            _pages.Remove(stale.Id);
        }

        var strip = this.FindControl<TabStripPanel>("Strip")!;
        strip.Children.Clear();
        foreach (var tab in _state.Tabs) {
            if (!_headers.TryGetValue(tab, out var header)) {
                header = new ShellTabHeader(tab, _state);
                _headers.Add(tab, header);
                if (_subscribed) {
                    tab.PropertyChanged += OnTabChanged;
                }
            }

            strip.Children.Add(header);
        }

        strip.Children.Add(_addTab);
    }

    /// <summary>Handles hardware back and forward buttons without synthesizing unsupported browser input.</summary>
    private void OnMouseNavigation(object? sender, PointerPressedEventArgs args) {
        var kind = args.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (kind is PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton2Pressed && _state is not null) {
            if (kind == PointerUpdateKind.XButton1Pressed) {
                _state.SelectedTab.Back();
            } else {
                _state.SelectedTab.Forward();
            }

            args.Handled = true;
        }
    }

    /// <summary>Offers only applicable history entries, each bound to the exact tab and absolute index.</summary>
    private static void UpdateHistoryMenu(Button button, ShellTab tab, bool backwards) {
        button.ContextMenu?.Close();
        var indices = backwards ? Enumerable.Range(0, tab.HistoryIndex).Reverse()
            : Enumerable.Range(tab.HistoryIndex + 1, tab.History.Count - tab.HistoryIndex - 1);
        var menu = new ContextMenu();
        foreach (int index in indices) {
            var entry = tab.History[index];
            var item = new MenuItem
            {
                Header = EnglishMetadata.Get($"Shell.Page.{entry.Page}")
                    + (entry.Section is null ? string.Empty : " · " + EnglishMetadata.Get(entry.Section)),
            };
            AutomationProperties.SetAutomationId(item, $"Shell.History.{index}");
            item.Click += (_, _) => tab.JumpHistory(index);
            menu.Items.Add(item);
        }

        button.ContextMenu = menu.Items.Count == 0 ? null : menu;
    }

    /// <summary>Hides the non-scrolling overflow overlay.</summary>
    private void HideOverflow() {
        this.FindControl<Grid>("OverflowOverlay")!.IsVisible = false;
        this.FindControl<StackPanel>("OverflowItems")!.Children.Clear();
    }

    /// <summary>Builds an actual menu from hidden state identities, without a scrolling tab template.</summary>
    private void ShowOverflow() {
        if (_state is null) {
            return;
        }

        var strip = this.FindControl<TabStripPanel>("Strip")!;
        var items = this.FindControl<StackPanel>("OverflowItems")!;
        items.Children.Clear();
        var hidden = _state.Tabs.Skip(strip.VisibleHeaderCount(strip.Bounds.Width)).ToArray();
        int capacity = Math.Max(1, (int)((Bounds.Height - strip.Bounds.Height - 100) / 40));
        int pageCount = Math.Max(1, (hidden.Length + capacity - 1) / capacity);
        _overflowPage = Math.Clamp(_overflowPage, 0, pageCount - 1);
        foreach (var tab in hidden.Skip(_overflowPage * capacity).Take(capacity)) {
            var button = new Button
            {
                Content = tab.IsClosed ? ShellTabHeader.CreateUndoContent() : EnglishMetadata.Get(tab.TitleKey),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Height = 36,
            };
            AutomationProperties.SetAutomationId(button, "Shell.Overflow.Tab." + tab.Id);
            button.Click += (_, _) => {
                HideOverflow();
                if (tab.IsClosed) {
                    _state.UndoClose(tab);
                }

                _state.PromoteOverflow(tab);
            };
            items.Children.Add(button);
        }

        if (pageCount > 1) {
            var paging = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            var previous = new Button
            {
                Content = EnglishMetadata.Get("Shell.Overflow.Previous"),
                IsEnabled = _overflowPage > 0,
            };
            var next = new Button
            {
                Content = EnglishMetadata.Get("Shell.Overflow.Next"),
                IsEnabled = _overflowPage < pageCount - 1,
            };
            AutomationProperties.SetAutomationId(previous, "Shell.Overflow.Previous");
            AutomationProperties.SetAutomationId(next, "Shell.Overflow.Next");
            previous.Click += (_, _) => {
                _overflowPage--;
                ShowOverflow();
            };
            next.Click += (_, _) => {
                _overflowPage++;
                ShowOverflow();
            };
            var caption = new TextBlock
            {
                Text = string.Format(EnglishMetadata.Get("Shell.Overflow.Page"), _overflowPage + 1, pageCount),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            paging.Children.Add(previous);
            Grid.SetColumn(caption, 1);
            paging.Children.Add(caption);
            Grid.SetColumn(next, 2);
            paging.Children.Add(next);
            items.Children.Add(paging);
        }

        this.FindControl<Grid>("OverflowOverlay")!.IsVisible = hidden.Length > 0;
    }

    /// <summary>Uses the approved exact promotion operation when selection would otherwise be hidden.</summary>
    private void EnsureSelectedVisible() {
        if (_state?.TabMode != TabStripMode.Overflow) {
            return;
        }

        var strip = this.FindControl<TabStripPanel>("Strip")!;
        int visibleCount = strip.VisibleHeaderCount(strip.Bounds.Width);
        this.FindControl<Button>("Overflow")!.IsEnabled = _state.Tabs.Count > visibleCount;
        if (strip.Bounds.Width > 0 && _state.Tabs.IndexOf(_state.SelectedTab) >= visibleCount) {
            _state.PromoteOverflow(_state.SelectedTab);
        }
    }

    /// <summary>Shows a real launcher or a cached host control for the selected identity and address.</summary>
    private void RefreshSelection() {
        if (_state is null || _pageFactory is null || _state.SelectedTab.IsClosed) {
            return;
        }

        var overflow = this.FindControl<Button>("Overflow")!;
        overflow.IsVisible = _state.TabMode == TabStripMode.Overflow;
        HideOverflow();
        EnsureSelectedVisible();
        var tab = _state.SelectedTab;
        foreach (var (candidate, header) in _headers) {
            header.Classes.Set("selected", candidate == tab);
        }

        var address = tab.History[tab.HistoryIndex];
        if (!_pages.TryGetValue(tab.Id, out var cached) || cached.Address != address) {
            var content = tab.Page == AppPage.NewTab
                ? new LauncherView { DataContext = new LauncherViewModel(_state, _pageAvailable) }
                : _pageFactory(tab);
            cached = (address, content);
            _pages[tab.Id] = cached;
        }

        this.FindControl<ContentControl>("PageHost")!.Content = cached.Content;
        UpdateHistoryMenu(this.FindControl<Button>("Back")!, tab, backwards: true);
        UpdateHistoryMenu(this.FindControl<Button>("Forward")!, tab, backwards: false);
    }
}