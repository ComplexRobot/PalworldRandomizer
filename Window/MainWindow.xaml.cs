using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PalworldRandomizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static MainWindow Instance { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UAssetData.Initialize();
            Data.Initialize();
            Randomize.Initialize();
            SharedWindow.EnableDarkMode(this);
            new PalSpawnWindow();
            testImage.Source = new BitmapImage(new Uri(Data.palIcon[Randomize.GetRandomPal()], UriKind.Relative));
        }

        private void SavePak_Click(object sender, RoutedEventArgs e)
        {
            if (!Randomize.GeneratePalSpawns())
            {
                MessageBox.Show(this, "Error: No area changes to save.", "Failed To Save Pak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PositiveIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SharedWindow.PositiveIntSize2_PreviewTextInput(sender, e);
        }

        private void PositiveIntSize2_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            SharedWindow.PositiveIntSize2_Pasting(sender, e);
        }

        private void PositiveIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SharedWindow.PositiveIntSize3_PreviewTextInput(sender, e);
        }

        private void PositiveIntSize3_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            SharedWindow.PositiveIntSize3_Pasting(sender, e);
        }

        private void NonNegIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SharedWindow.NonNegIntSize3_PreviewTextInput(sender, e);
        }

        private void NonNegIntSize3_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            SharedWindow.NonNegIntSize3_Pasting(sender, e);
        }

        private void groupType_Click(object sender, RoutedEventArgs e)
        {
            if (groupRandom.IsChecked == true)
            {
                groupMinMaxPanel.Visibility = Visibility.Visible;
            }
            else
            {
                groupMinMaxPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void spawnListSize_GotFocus(object sender, RoutedEventArgs e)
        {
            methodCustomSize.IsChecked = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ViewSpawns_Click(object sender, RoutedEventArgs e)
        {
            PalSpawnWindow.Instance.Show();
            PalSpawnWindow.Instance.Focus();
        }

        private void methodNone_Checked(object sender, RoutedEventArgs e)
        {
            spawnGroupSettings.Visibility = Visibility.Collapsed;
        }

        private void methodNone_Unchecked(object sender, RoutedEventArgs e)
        {
            spawnGroupSettings.Visibility = Visibility.Visible;
        }
    }
}