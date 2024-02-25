using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PalworldRandomizer
{
    public partial class PalSpawnWindow : Window
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public static PalSpawnWindow Instance { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public PalSpawnWindow()
        {
            Instance = this;
            InitializeComponent();
            CompositionTarget.Rendering += Window_Rendering;
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

        private void UpdateSourceFocusedElement()
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        private void savePak_Click(object sender, RoutedEventArgs e)
        {
            UpdateSourceFocusedElement();
            if (!FileModify.SaveAreaList((List<AreaData>) areaList.ItemsSource) || !FileModify.GenerateAndSavePak())
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
            string? status = FileModify.LoadPak();
            if (status != null)
            {
                if (status != "Cancel")
                {
                    MessageBox.Show(this, "Error: Invalid or incorrect PAK file.\n" + status, "Failed To Load Pak", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(this, "Successfully loaded PAK file.", "Pak Loaded", MessageBoxButton.OK, MessageBoxImage.None);
            }
        }

        private void saveCsv_Click(object sender, RoutedEventArgs e)
        {
            UpdateSourceFocusedElement();
            FileModify.SaveCSV((List<AreaData>) areaList.ItemsSource);
        }

        private void loadCsv_Click(object sender, RoutedEventArgs e)
        {
            string? status = FileModify.LoadCSV();
            if (status != null)
            {
                if (status != "Cancel")
                {
                    MessageBox.Show(this, "Error: Invalid or corrupt CSV file.\n" + status, "Failed To Load CSV", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(this, "Successfully loaded CSV file.", "CSV Loaded", MessageBoxButton.OK, MessageBoxImage.None);
            }
        }

        private bool loadingEntries = false;
        private int addedSpawns = 0;
        private int spawnAddLoops = 1;
        public static float spawnLoadSpeed = 1.5f;
        private void areaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                AreaData area = (AreaData) e.AddedItems[0]!;
                area.entriesToShow = 0;
                loadingEntries = true;
                addedSpawns = 0;
                spawnAddLoops = 1;
            }
        }

        private void Window_Rendering(object? sender, EventArgs e)
        {
            if (loadingEntries)
            {
                AreaData area = (AreaData) areaList.SelectedItem;
                if (area != null && area.entriesToShow < area.spawnEntries.Count)
                {
                    while (addedSpawns < spawnLoadSpeed * spawnAddLoops && area.entriesToShow < area.spawnEntries.Count)
                    {
                        addedSpawns += area.spawnEntries[area.entriesToShow++].spawnList.Count;
                    }
                    ++spawnAddLoops;
                }
                else
                {
                    loadingEntries = false;
                }
            }
        }

        private void PositiveIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SharedWindow.PositiveIntSize9_PreviewTextInput(sender, e);
        }

        private void PositiveIntSize9_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            SharedWindow.PositiveIntSize9_Pasting(sender, e);
        }

        private void NonNegIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SharedWindow.NonNegIntSize9_PreviewTextInput(sender, e);
        }

        private void NonNegIntSize9_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            SharedWindow.NonNegIntSize9_Pasting(sender, e);
        }

        private void NameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox) sender;
            textBox.Visibility = Visibility.Collapsed;
            ComboBox comboBox = (ComboBox) ((Grid) textBox.Parent).Children[2];
            comboBox.Visibility = Visibility.Visible;
            comboBox.ApplyTemplate();
            TextBox newTextBox = (TextBox) comboBox.Template.FindName("PART_EditableTextBox", comboBox);
            newTextBox.Focus();
            newTextBox.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                comboBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            };
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox) sender;
            Image image = (Image) ((Grid) comboBox.Parent).Children[0];
            image.GetBindingExpression(Image.SourceProperty)?.UpdateTarget();
        }
    }
}
