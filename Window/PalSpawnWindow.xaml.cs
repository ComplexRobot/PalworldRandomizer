using System.Windows;

namespace PalworldRandomizer
{
    /// <summary>
    /// Interaction logic for PalSpawnWindow.xaml
    /// </summary>
    public partial class PalSpawnWindow : Window
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static PalSpawnWindow Instance { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public PalSpawnWindow()
        {
            Instance = this;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SharedWindow.EnableDarkMode(this);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        private void savePak_Click(object sender, RoutedEventArgs e)
        {
            if (!Randomize.SaveAreaList((List<AreaData>) areaList.ItemsSource) || !Randomize.GenerateAndSavePak())
            {
                MessageBox.Show(this, "Error: No spawn group changes detected.", "Failed To Save Pak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Are you sure you want to revert back to vanilla spawns?", "Revert All Spawns", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                == MessageBoxResult.OK)
            {
                areaList.ItemsSource = Data.AreaDataCopy();
            }
        }

        private void loadPak_Click(object sender, RoutedEventArgs e)
        {
            if (!Randomize.LoadPak())
            {
                MessageBox.Show(this, "Error: Invalid or incorrect PAK file.", "Failed To Load Pak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(this, "Successfully loaded PAK file.", "Pak Loaded", MessageBoxButton.OK, MessageBoxImage.None);
            }
        }

        private void saveCsv_Click(object sender, RoutedEventArgs e)
        {
            Randomize.SaveCSV((List<AreaData>) areaList.ItemsSource);
        }

        private void loadCsv_Click(object sender, RoutedEventArgs e)
        {
            Exception? exception = Randomize.LoadCSV();
            if (exception != null)
            {
                MessageBox.Show(this, "Error: Invalid or corrupt CSV file.\n" + exception, "Failed To Load CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(this, "Successfully loaded CSV file.", "CSV Loaded", MessageBoxButton.OK, MessageBoxImage.None);
            }
        }
    }
}
