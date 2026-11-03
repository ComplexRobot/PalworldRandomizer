using System.Windows;
using System.Windows.Interop;

namespace PalworldRandomizer.Window;

public partial class AppWindow : System.Windows.Window {
    /// <summary>Creates a styled desktop host and defers its content until the window is loaded.</summary>
    public AppWindow(Func<object> contentSetter) {
        Loaded += (sender, e) => {
            Left = initialX;
            Top = initialY;
            SharedWindow.EnableDarkMode(this);
            Dispatcher.BeginInvoke(() => Content = contentSetter());
        };
        InitializeComponent();
    }

    private double initialX = 0;
    private double initialY = 0;

    /// <summary>Shows the desktop window while avoiding its initial unstyled appearance.</summary>
    public void ShowClean() {
        if (!IsLoaded) {
            new WindowInteropHelper(this).EnsureHandle();
            initialX = Left;
            initialY = Top;
            Left = -20000;
            Top = -20000;
            Show();
        } else {
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
        }
    }

    /// <summary>Minimizes the desktop window and hides its taskbar entry.</summary>
    public void HideClean() {
        WindowState = WindowState.Minimized;
        ShowInTaskbar = false;
    }
}