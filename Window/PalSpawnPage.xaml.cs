using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PalworldRandomizer
{
    public partial class PalSpawnPage : Grid
    {
        public static PalSpawnPage Instance { get; private set; } = null!;
        public AppWindow ParentWindow { get; set; } = null!;
        public AppWindow GetWindow() => ((AppWindow) Parent) ?? ParentWindow;
        public PalSpawnPage()
        {
            Instance = this;
            InitializeComponent();
            CompositionTarget.Rendering += Window_Rendering;
            spawnEntries.Tag = this;
        }

        private void UpdateSourceFocusedElement()
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(GetWindow());
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
                MessageBox.Show(GetWindow(), "Error: No spawn group changes detected.", "Failed To Save Pak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(GetWindow(), "Are you sure you want to revert back to vanilla spawns?", "Revert All Spawns", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
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
                    MessageBox.Show(GetWindow(), "Error: Invalid or incorrect PAK file.\n" + status, "Failed To Load Pak", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(GetWindow(), "Successfully loaded PAK file.", "Pak Loaded", MessageBoxButton.OK, MessageBoxImage.None);
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
                    MessageBox.Show(GetWindow(), "Error: Invalid or corrupt CSV file.\n" + status, "Failed To Load CSV", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(GetWindow(), "Successfully loaded CSV file.", "CSV Loaded", MessageBoxButton.OK, MessageBoxImage.None);
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
                area.VirtualCapacity = area.Count;
            }
            if (e.RemovedItems.Count > 0 && ((AreaData) e.RemovedItems[0]!).modified == true)
            {
                Data.AreaForEachIfDiff([(AreaData) e.RemovedItems[0]!], null, area => { area.modified = false; areaList.Items.Refresh(); });
            }
        }

        private void Window_Rendering(object? sender, EventArgs e)
        {
            if (loadingEntries)
            {
                AreaData area = (AreaData) areaList.SelectedItem;
                if (area != null && area.EntriesToShow < area.Count)
                {
                    int countToAdd = 0;
                    while (addedSpawns < SpawnLoadSpeed * spawnAddLoops && area.EntriesToShow + countToAdd < area.Count)
                    {
                        addedSpawns += area.SpawnEntries[area.EntriesToShow + countToAdd++].SpawnList.Count;
                    }
                    area.EntriesToShow += countToAdd;
                    ++spawnAddLoops;
                }
                else
                {
                    loadingEntries = false;
                }
            }
        }

        private void PositiveIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.PositiveIntSize9_PreviewTextInput(sender, e);
        private void PositiveIntSize9_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.PositiveIntSize9_Pasting(sender, e);
        private void NonNegIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntSize9_PreviewTextInput(sender, e);
        private void NonNegIntSize9_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntSize9_Pasting(sender, e);

        private void NameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox) sender;
            textBox.Visibility = Visibility.Collapsed;
            ComboBox comboBox = (ComboBox) ((Grid) textBox.Parent).Children[2];
            comboBox.Visibility = Visibility.Visible;
            bool templateApplied = comboBox.ApplyTemplate();
            TextBox newTextBox = (TextBox) comboBox.Template.FindName("PART_EditableTextBox", comboBox);
            if (templateApplied)
            {
                newTextBox.LostFocus += (sender, e) =>
                {
                    comboBox.Visibility = Visibility.Collapsed;
                    textBox.Visibility = Visibility.Visible;
                };
            }
            newTextBox.Focus();
            comboBox.IsDropDownOpen = true;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox) sender;
            Image image = (Image) ((Grid) comboBox.Parent).Children[0];
            TextBox textBox = (TextBox) ((Grid) comboBox.Parent).Children[1];
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            image.GetBindingExpression(Image.SourceProperty)?.UpdateTarget();
            AreaProperty_SourceUpdated(this, e);
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox) sender;
            TextBox newTextBox = (TextBox) comboBox.Template.FindName("PART_EditableTextBox", comboBox);
            if (newTextBox?.IsFocused == true)
            {
                FocusManager.SetFocusedElement(GetWindow(), null);
                Keyboard.ClearFocus();
            }
        }

        private void Boss_CheckChanged(object sender, RoutedEventArgs e)
        {
            ((SpawnData?) ((MenuItem) sender).GetBindingExpression(MenuItem.IsCheckedProperty)?.DataItem)?.NotifyPropertyChanged(nameof(SpawnData.IsBoss));
        }

        private void AreaProperty_SourceUpdated(object sender, EventArgs e)
        {
            if (areaList.SelectedItem != null && ((AreaData) areaList.SelectedItem).modified == false)
            {
                ((AreaData) areaList.SelectedItem).modified = true;
                areaList.Items.Refresh();
            }
        }

        // TODO: Change to object-oriented design? Very unreadable with this functional design
        private bool CollectionAction<T>(object sender, EventArgs e,
            Func<object, List<SpawnData>> spawnListFunc,
            Func<List<SpawnData>, AreaData, int, T> valueFunc,
            Action<int, T, List<SpawnData>, AreaData> completionAction,
            Func<int, T, List<SpawnData>, AreaData, bool>? condition,
            Action<int, T, List<SpawnData>, AreaData> action)
        {
            int index = (int) (((FrameworkElement) ((FrameworkElement) sender).Parent).Tag ?? 0);
            List<SpawnData> spawnList = spawnListFunc(sender);
            AreaData areaData = (AreaData) areaList.SelectedItem;
            T valueData = valueFunc(spawnList, areaData, index);
            if (condition == null || condition(index, valueData, spawnList, areaData))
            {
                action(index, valueData, spawnList, areaData);
                completionAction(index, valueData, spawnList, areaData);
                return true;
            }
            return false;
        }

        private bool PalAction(object sender, EventArgs e,
            Func<int, SpawnData, List<SpawnData>, bool>? condition,
            Action<int, SpawnData, List<SpawnData>> action, bool refresh = true)
        {
            ItemsControl itemsControl = GetPalItemsControl(sender);
            return CollectionAction(sender, e, (sender) => (List<SpawnData>) itemsControl.ItemsSource,
                (spawnList, areaData, index) => spawnList[index],
                (index, spawnData, spawnList, areaData) =>
                {
                    if (refresh)
                    {
                        itemsControl.Items.Refresh();
                        AreaProperty_SourceUpdated(sender, e);
                    }
                },
                (index, spawnData, spawnList, areaData) => condition == null || condition(index, spawnData, spawnList),
                (index, spawnData, spawnList, areaData) => action(index, spawnData, spawnList));
        }

        private void PalAction(object sender, EventArgs e, Action<int, SpawnData, List<SpawnData>> action, bool refresh = true)
        {
            PalAction(sender, e, null, action, refresh);
        }

        private static ItemsControl GetPalItemsControl(object sender)
        {
            return (((FrameworkElement) sender).Parent is ContextMenu) ?
                (ItemsControl) ((FrameworkElement) ((ContextMenu) ((FrameworkElement) sender).Parent).PlacementTarget).Tag
                : (ItemsControl) ((FrameworkElement) ((FrameworkElement) ((FrameworkElement) sender).Parent).Parent).Tag;
        }

        private static ItemsControl GetGroupPalItemsControl(object sender)
        {
            return (((FrameworkElement) sender).Parent is ContextMenu) ?
                (ItemsControl) ((Panel) ((Decorator) ((ContextMenu) ((FrameworkElement) sender).Parent).PlacementTarget).Child).Children[1]
                : (ItemsControl) ((Panel) ((FrameworkElement) ((FrameworkElement) ((FrameworkElement) sender).Parent).Parent).Parent).Children[1];
        }

        private bool GroupAction(object sender, EventArgs e,
            Func<int, SpawnEntry, List<SpawnData>, AreaData, bool>? condition,
            Action<int, SpawnEntry, List<SpawnData>, AreaData> action, bool refresh = false, bool changed = true)
        {
            ItemsControl itemsControl = GetGroupPalItemsControl(sender);
            return CollectionAction(sender, e, (sender) => (List<SpawnData>) itemsControl.ItemsSource,
                (spawnList, areaData, index) => areaData.SpawnEntries[index],
                (index, spawnEntry, spawnList, areaData) =>
                {
                    if (refresh)
                        itemsControl.Items.Refresh();
                    if (changed)
                        AreaProperty_SourceUpdated(sender, e);
                },
                condition, action);
        }

        private void GroupAction(object sender, EventArgs e, Action<int, SpawnEntry, List<SpawnData>, AreaData> action, bool refresh = false, bool changed = true)
        {
            GroupAction(sender, e, null, action, refresh, changed);
        }

        private bool GroupToolBarAction(object sender, EventArgs e,
            Func<AreaData, bool>? condition,
            Action<AreaData> action)
        {
            return CollectionAction(sender, e, (sender) => null!,
                (Func<List<SpawnData>, AreaData, int, SpawnEntry>) ((spawnList, areaData, index) => null!),
                (index, spawnEntry, spawnList, areaData) =>
                {
                    AreaProperty_SourceUpdated(sender, e);
                },
                (index, spawnEntry, spawnList, areaData) => condition == null || condition(areaData),
                (index, spawnEntry, spawnList, areaData) => action(areaData));
        }

        private void GroupToolBarAction(object sender, EventArgs e, Action<AreaData> action)
        {
            GroupToolBarAction(sender, e, null, action);
        }

        private void GroupToolBarNew_Click(object sender, RoutedEventArgs e)
        {
            GroupToolBarAction(sender, e,
            (areaData) =>
            areaData?.Insert(areaData.Count, new()
            {
                SpawnList = [new() { Name = "SheepBall" }]
            }));
        }

        private void GroupToolBarDelete_Click(object sender, RoutedEventArgs e)
        {
            GroupToolBarAction(sender, e,
            (areaData) => areaData != null && areaData.Count > 0,
            (areaData) => areaData.RemoveAt(areaData.Count - 1));
        }

        private void GroupToolBarDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            GroupToolBarAction(sender, e,
            (areaData) => areaData != null &&
                MessageBox.Show(GetWindow(), "Delete all groups of this area?", "Delete All", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK,
            (areaData) => areaData?.Clear());
        }

        private void PalDuplicateThis_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e, (index, spawnData, spawnList) => spawnList.Insert(index, spawnData.Clone()));
        }

        private void PalDeleteThis_Click(object sender, RoutedEventArgs e)
        {
            if (!PalAction(sender, e,
                (index, spawnData, spawnList) => spawnList.Count > 1,
                (index, spawnData, spawnList) => spawnList.RemoveAt(index)))
            {
                MessageBox.Show(GetWindow(), "Can't delete the last Pal!", "Delete Pal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PalMoveUp_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e,
            (index, spawnData, spawnList) => index > 0,
            (index, spawnData, spawnList) =>
            {
                spawnList.RemoveAt(index);
                spawnList.Insert(index - 1, spawnData);
            });
        }

        private void PalMoveDown_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e,
            (index, spawnData, spawnList) => index < spawnList.Count - 1,
            (index, spawnData, spawnList) =>
            {
                spawnList.RemoveAt(index);
                spawnList.Insert(index + 1, spawnData);
            });
        }

        public SpawnData? PalClipboard { get; private set; }

        private void PalCut_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e,
            (index, spawnData, spawnList) => spawnList.Count > 1,
            (index, spawnData, spawnList) =>
            {
                PalClipboard = spawnData;
                spawnList.RemoveAt(index);
            });
        }

        private void PalCopy_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e, (index, spawnData, spawnList) => PalClipboard = spawnData.Clone(), false);
        }

        private void PalPaste_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e,
            (index, spawnData, spawnList) => PalClipboard != null,
            (index, spawnData, spawnList) => spawnList[index] = PalClipboard!.Clone());
        }

        private void PalPasteBefore_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e,
            (index, spawnData, spawnList) => PalClipboard != null,
            (index, spawnData, spawnList) => spawnList.Insert(index, PalClipboard!.Clone()));
        }

        private void PalPasteAfter_Click(object sender, RoutedEventArgs e)
        {
            PalAction(sender, e,
            (index, spawnData, spawnList) => PalClipboard != null,
            (index, spawnData, spawnList) => spawnList.Insert(index + 1, PalClipboard!.Clone()));
        }

        private void GroupDuplicateThis_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e, (index, spawnEntry, spawnList, areaData) => areaData.Insert(index, spawnEntry.Clone()));
        }

        private void GroupDeleteThis_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e, (index, spawnEntry, spawnList, areaData) => areaData.RemoveAt(index));
        }

        private void GroupPalDelete_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) => spawnList.Count > 1,
            (index, spawnEntry, spawnList, areaData) => spawnList.RemoveAt(spawnList.Count - 1), true);
        }

        private void GroupPalNew_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e, (index, spawnEntry, spawnList, areaData) => spawnList.Insert(spawnList.Count, new() { Name = "SheepBall" }), true);
        }

        private void GroupPalDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e, (index, spawnEntry, spawnList, areaData) =>
            {
                spawnList.Clear();
                spawnList.Add(new() { Name = "SheepBall" });
            }, true);
        }

        public SpawnEntry? ListClipboard { get; private set; }

        private void GroupCut_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e, (index, spawnEntry, spawnList, areaData) =>
            {
                ListClipboard = spawnEntry;
                areaData.RemoveAt(index);
            });
        }

        private void GroupCopy_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e, (index, spawnEntry, spawnList, areaData) =>
            {
                ListClipboard = spawnEntry.Clone();
            }, false, false);
        }

        private void GroupPaste_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) => ListClipboard != null,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.RemoveAt(index);
                areaData.Insert(index, ListClipboard!.Clone());
            });
        }

        private void GroupPasteBefore_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) => ListClipboard != null,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.Insert(index, ListClipboard!.Clone());
            });
        }

        private void GroupPasteAfter_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) => ListClipboard != null,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.Insert(index + 1, ListClipboard!.Clone());
            });
        }

        private void GroupMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) => index > 0,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.RemoveAt(index);
                areaData.Insert(index - 1, spawnEntry);
            });
        }

        private void GroupMoveRight_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) => index < areaData.Count - 1,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.RemoveAt(index);
                areaData.Insert(index + 1, spawnEntry);
            });
        }

        private void GroupMoveToStart_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.RemoveAt(index);
                areaData.Insert(0, spawnEntry);
            });
        }

        private void GroupMoveToEnd_Click(object sender, RoutedEventArgs e)
        {
            GroupAction(sender, e,
            (index, spawnEntry, spawnList, areaData) =>
            {
                areaData.RemoveAt(index);
                areaData.Insert(areaData.Count, spawnEntry);
            });
        }
    }
}
