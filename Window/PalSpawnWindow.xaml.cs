using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PalworldRandomizer
{
    public partial class PalSpawnWindow : Window
    {
        public static PalSpawnWindow Instance { get; private set; } = null!;
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

        private void SavePak_Click(object sender, RoutedEventArgs e)
        {
            UpdateSourceFocusedElement();
            if (!FileModify.SaveAreaList((List<AreaData>) areaList.ItemsSource) || !FileModify.GenerateAndSavePak())
            {
                MessageBox.Show(this, "Error: No spawn group changes detected.", "Failed To Save Pak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "Are you sure you want to revert back to vanilla spawns?", "Revert All Spawns", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                == MessageBoxResult.OK)
            {
                areaList.ItemsSource = Data.AreaDataCopy();
            }
        }

        private void LoadPak_Click(object sender, RoutedEventArgs e)
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

        private void SaveCsv_Click(object sender, RoutedEventArgs e)
        {
            UpdateSourceFocusedElement();
            FileModify.SaveCSV((List<AreaData>) areaList.ItemsSource);
        }

        private void LoadCsv_Click(object sender, RoutedEventArgs e)
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
        public static float SpawnLoadSpeed { get; set; } = 1.5f;
        private void AreaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                AreaData area = (AreaData) e.AddedItems[0]!;
                area.EntriesToShow = 0;
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
                if (area != null && area.EntriesToShow < area.SpawnEntries.Count)
                {
                    while (addedSpawns < SpawnLoadSpeed * spawnAddLoops && area.EntriesToShow < area.SpawnEntries.Count)
                    {
                        addedSpawns += area.SpawnEntries[area.EntriesToShow++].SpawnList.Count;
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
            comboBox.IsDropDownOpen = true;
            newTextBox.LostFocus += (object sender, RoutedEventArgs e) =>
            {
                comboBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
            };
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox) sender;
            Image image = (Image) ((Grid) comboBox.Parent).Children[0];
            TextBox textBox = (TextBox) ((Grid) comboBox.Parent).Children[1];
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            image.GetBindingExpression(Image.SourceProperty)?.UpdateTarget();
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox) sender;
            TextBox newTextBox = (TextBox) comboBox.Template.FindName("PART_EditableTextBox", comboBox);
            if (newTextBox?.IsFocused == true)
            {
                FocusManager.SetFocusedElement(this, null);
                Keyboard.ClearFocus();
            }
        }

        private void Boss_CheckChanged(object sender, RoutedEventArgs e) // Why is this needed?
        {
            ((SpawnData?) ((MenuItem) sender).GetBindingExpression(MenuItem.IsCheckedProperty)?.DataItem)?.OnPropertyChanged(nameof(SpawnData.IsBoss));
        }
    }
}
