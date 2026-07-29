using System.Windows;
using System.Windows.Interop;

namespace PalworldRandomizer
{
    public partial class AppWindow : Window
    {
        public AppWindow(Func<object> contentSetter)
        {
            Loaded += (sender, e) =>
            {
                Left = initialX;
                Top = initialY;
                SharedWindow.EnableDarkMode(this);
                Dispatcher.BeginInvoke(() => Content = contentSetter());
            };
            InitializeComponent();
        }

        private double initialX = 0;
        private double initialY = 0;
        public void ShowClean()
        {
            if (!IsLoaded)
            {
                new WindowInteropHelper(this).EnsureHandle();
                initialX = Left;
                initialY = Top;
                Left = -20000;
                Top = -20000;
                Show();
            }
            else
            {
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
            }
        }

        public void HideClean()
        {
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
        }
    }
}
