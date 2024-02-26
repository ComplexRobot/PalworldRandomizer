using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalworldRandomizer
{
    public class FormData(MainWindow window)
    {
        public bool randomizeField = window.randomizeField.IsChecked == true;
        public bool randomizeDungeons = window.randomizeDungeons.IsChecked == true;
        public bool randomizeDungeonBosses = window.randomizeDungeonBosses.IsChecked == true;
        public bool randomizeFieldBosses = window.randomizeFieldBosses.IsChecked == true;
        public bool methodFull = window.methodFull.IsChecked == true;
        public bool methodCustomSize = window.methodCustomSize.IsChecked == true;
        public int spawnListSize = int.Parse(window.spawnListSize.Text);
        public bool methodLocalSwap = window.methodLocalSwap.IsChecked == true;
        public bool methodGlobalSwap = window.methodGlobalSwap.IsChecked == true;
        public bool methodNone = window.methodNone.IsChecked == true;
        public bool groupVanilla = window.groupVanilla.IsChecked == true;
        public bool groupRandom = window.groupRandom.IsChecked == true;
        public bool fieldBossExtended = window.fieldBossExtended.IsChecked == true;
        public int groupMin = int.Parse(window.groupMin.Text);
        public int groupMax = int.Parse(window.groupMax.Text);
        public bool multiBoss = window.multiBoss.IsChecked == true;
        public bool spawnPals = window.spawnPals.IsChecked == true;
        public bool spawnHumans = window.spawnHumans.IsChecked == true;
        public bool spawnPolice = window.spawnPolice.IsChecked == true;
        public bool spawnGuards = window.spawnGuards.IsChecked == true;
        public bool spawnTraders = window.spawnTraders.IsChecked == true;
        public bool spawnPalTraders = window.spawnPalTraders.IsChecked == true;
        public bool spawnTowerBosses = window.spawnTowerBosses.IsChecked == true;
        public int fieldLevel = int.Parse(window.fieldLevel.Text);
        public int dungeonLevel = int.Parse(window.dungeonLevel.Text);
        public int fieldBossLevel = int.Parse(window.fieldBossLevel.Text);
        public int dungeonBossLevel = int.Parse(window.dungeonBossLevel.Text);
        public int levelCap = int.Parse(window.levelCap.Text);
        public bool nightOnly = window.nightOnly.IsChecked == true;
        public bool nightOnlyDungeons = window.nightOnlyDungeons.IsChecked == true;
        public bool nightOnlyDungeonBosses = window.nightOnlyDungeonBosses.IsChecked == true;
        public bool nightOnlyBosses = window.nightOnlyBosses.IsChecked == true;
        public bool outputLog = window.outputLog.IsChecked == true;
    }

    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } = null!;
        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            CompositionTarget.Rendering += Window_Rendering;
        }

        private enum LoadStep
        {
            NoSize,
            SizeSet,
            Loaded,
            AfterLoaded
        }
        private double initialX = 0;
        private double initialY = 0;
        private LoadStep loadStep = LoadStep.NoSize;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UAssetData.Initialize();
            Data.Initialize();
            Randomize.Initialize();
            SharedWindow.EnableDarkMode(this);
            new PalSpawnWindow();
            testImage.Source = new BitmapImage(new Uri(Data.PalIcon[Randomize.GetRandomPal()], UriKind.Relative));
            loadStep = LoadStep.Loaded;
        }

        private void WindowComplete(object? sender, EventArgs e)
        {
            loadStep = LoadStep.Loaded;
        }

        private void Window_Rendering(object? sender, EventArgs e)
        {
            if (loadStep == LoadStep.NoSize)
            {
                initialX = Left;
                initialY = Top;
                Left = -10000;
                Top = -10000;
                loadStep = LoadStep.SizeSet;
            }
            else if (loadStep == LoadStep.Loaded) // It's not guaranteed to be done after Loaded, a better solution is needed
            {
                loadStep = LoadStep.AfterLoaded;
            }
            else if (loadStep == LoadStep.AfterLoaded)
            {
                Left = initialX;
                Top = initialY;
                CompositionTarget.Rendering -= Window_Rendering;
            }
        }

        private bool generating = false;
        private void SavePak_Click(object sender, RoutedEventArgs e)
        {
            if (generating)
            {
                return;
            }
            statusBar.Text = "⏱️ Generating...";
            generating = true;
            savePak.IsEnabled = false;
            static void ValidateNumericText(TextBox textBox, int minValue, int? defaultValue = null)
            {
                defaultValue ??= minValue;
                try
                {
                    if (textBox.Text.Length == 0)
                    {
                        textBox.Text = $"{defaultValue}";
                    }
                    else if (int.Parse(textBox.Text) < minValue)
                    {
                        textBox.Text = $"{minValue}";
                    }
                }
                catch (Exception)
                {
                    textBox.Text = $"{defaultValue}";
                }
            }
            ValidateNumericText(groupMin, 1);
            ValidateNumericText(groupMax, int.Parse(groupMin.Text));
            ValidateNumericText(spawnListSize, 1);
            ValidateNumericText(fieldLevel, 0, 100);
            ValidateNumericText(dungeonLevel, 0, 100);
            ValidateNumericText(fieldBossLevel, 0, 100);
            ValidateNumericText(dungeonBossLevel, 0, 100);
            ValidateNumericText(levelCap, 1, 50);
            progressBar.Visibility = Visibility.Visible;
            progressBar.Value = 0;
            new Thread((object? formData) =>
            {
                if (!Randomize.GeneratePalSpawns((FormData) formData!))
                {
                    Dispatcher.Invoke(() => MessageBox.Show(this, "Error: No area changes to save.", "Failed To Save Pak", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                generating = false;
                Dispatcher.Invoke(() => savePak.IsEnabled = true);
            }).Start(new FormData(this));
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

        private void NonNegIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SharedWindow.NonNegIntSize4_PreviewTextInput(sender, e);
        }

        private void NonNegIntSize4_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            SharedWindow.NonNegIntSize4_Pasting(sender, e);
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