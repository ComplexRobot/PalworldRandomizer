using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.ExceptionServices;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Numerics;

namespace PalworldRandomizer
{
    public static class GroupWeightMode
    {
        public static readonly string WeightSum = "Weight Sum";
        public static readonly string WeightAverage = "Weight Average";
        public static readonly string RarityAverageRounded = "Rarity Average (Rounded)";
        public static readonly string RarityAverageBlend = "Rarity Average (Blend Fractions)";
        public static readonly string RarityAverageBlend10To20 = "Rarity Average (Blend 10 to 20)";
        public static readonly string WeightMinimum = "Weight Minimum";
        public static readonly string WeightMaximum = "Weight Maximum";
    }

    public static class OverflowFixMode
    {
        public static readonly string ScaleAll = "Scale All";
        public static readonly string ScaleNightOnly = "Scale Night-Only";
        public static readonly string Dynamic = "Dynamic (Multi-Pass)";
    }

    public static class LevelScaleMode
    {
        public static readonly string Average = "Average Level";
        public static readonly string BothLevels = "Full";
        public static readonly string MaxLevel = "Max. Level";
        public static readonly string LockExtreme = "Extend Min./Max.";
        public static readonly string MaxRange = "Maximize Range";
        public static readonly string MinLevel = "Min. Level";
        public static readonly string Random = "Random";
    }

    public class FormData(MainPage window)
    {
        public FormData() : this(MainPage.Instance) { }

        public int RandomSeed = int.Parse(window.randomSeed.Text);
        public bool RandomSeedLocked = window.randomSeedLocked.IsChecked == true;
        public bool RandomizeField = window.randomizeField.IsChecked == true;
        public bool RandomizeDungeons = window.randomizeDungeons.IsChecked == true;
        public bool RandomizeDungeonBosses = window.randomizeDungeonBosses.IsChecked == true;
        public bool RandomizeFieldBosses = window.randomizeFieldBosses.IsChecked == true;
        public bool RandomizePredators = window.randomizePredators.IsChecked == true;
        public int PredatorChance = int.Parse(window.predatorChance.Text);
        public bool EqualizeAreaRarity = window.equalizeAreaRarity.IsChecked == true;
        public bool MethodFull = window.methodFull.IsChecked == true;
        public bool MethodCustomSize = window.methodCustomSize.IsChecked == true;
        public int SpawnListSize = int.Parse(window.spawnListSize.Text);
        public bool MethodLocalSwap = window.methodLocalSwap.IsChecked == true;
        public bool MethodGlobalSwap = window.methodGlobalSwap.IsChecked == true;
        public bool MethodNone = window.methodNone.IsChecked == true;
        public bool VanillaPlus = window.vanillaPlus.IsChecked == true;
        public int VanillaPlusChance = int.Parse(window.vanillaPlusChance.Text);
        public bool VanillaMerge = window.vanillaMerge.IsChecked == true;
        public bool GroupVanilla = window.groupVanilla.IsChecked == true;
        public bool GroupRandom = window.groupRandom.IsChecked == true;
        public bool FieldBossExtended = window.fieldBossExtended.IsChecked == true;
        public int GroupMin = int.Parse(window.groupMin.Text);
        public int GroupMax = int.Parse(window.groupMax.Text);
        public int GroupMinBoss = int.Parse(window.groupMinBoss.Text);
        public int GroupMaxBoss = int.Parse(window.groupMaxBoss.Text);
        public bool MultiBoss = window.multiBoss.IsChecked == true;
        public bool VanillaRestrict = window.vanillaRestrict.IsChecked == true;
        public bool RarityLevelBoost = window.rarityLevelBoost.IsChecked == true;
        public int Rarity67MinLevel = int.Parse(window.rarity67MinLevel.Text);
        public int Rarity8UpMinLevel = int.Parse(window.rarity8UpMinLevel.Text);
        public bool Rarity9UpBossOnly = window.rarity9UpBossOnly.IsChecked == true;
        public bool Rarity8UpSolo = window.rarity8UpSolo.IsChecked == true;
        public bool MixHumanAndPal = window.mixHumanAndPal.IsChecked == true;
        public bool SeparateAggroHumans = window.separateAggroHumans.IsChecked == true;
        public bool BossesEverywhere = window.bossesEverywhere.IsChecked == true;
        public int BossesEverywhereChance = int.Parse(window.bossesEverywhereChance.Text);
        public bool SeparateFlying = window.separateFlying.IsChecked == true;
        public bool PredatorConstraint = window.predatorConstraint.IsChecked == true;
        public bool WeightTypeUniform = window.weightTypeUniform.IsChecked == true;
        public bool WeightTypeCustom = window.weightTypeCustom.IsChecked == true;
        public int WeightUniformMin = int.Parse(window.weightUniformMin.Text);
        public int WeightUniformMax = int.Parse(window.weightUniformMax.Text);
        public int[] WeightCustom = ((Func<int[]>) (() =>
        {
            int[] weightCustom = new int[21];
            weightCustom[1] = int.Parse(window.weightCustom1.Text);
            weightCustom[2] = int.Parse(window.weightCustom2.Text);
            weightCustom[3] = int.Parse(window.weightCustom3.Text);
            weightCustom[4] = int.Parse(window.weightCustom4.Text);
            weightCustom[5] = int.Parse(window.weightCustom5.Text);
            weightCustom[6] = int.Parse(window.weightCustom6.Text);
            weightCustom[7] = int.Parse(window.weightCustom7.Text);
            weightCustom[8] = int.Parse(window.weightCustom8.Text);
            weightCustom[9] = int.Parse(window.weightCustom9.Text);
            weightCustom[10] = int.Parse(window.weightCustom10.Text);
            weightCustom[20] = int.Parse(window.weightCustom20.Text);
            return weightCustom;
        }))();
        public string WeightCustomMode = window.weightCustomMode.Text;
        public int HumanRarity = int.Parse(window.humanRarity.Text);
        public int HumanBossRarity = int.Parse(window.humanBossRarity.Text);
        public bool WeightAdjustProbability = window.weightAdjustProbability.IsChecked == true;
        public int HumanWeight = int.Parse(window.humanWeight.Text);
        public int HumanWeightAggro = int.Parse(window.humanWeightAggro.Text);
        public decimal WeightNightOnly = decimal.Parse(window.weightNightOnly.Text);
        public string OverflowFixMode = window.overflowFixMode.Text;
        public bool SpawnPals = window.spawnPals.IsChecked == true;
        public bool SpawnHumans = window.spawnHumans.IsChecked == true;
        public bool SpawnPolice = window.spawnPolice.IsChecked == true;
        public bool SpawnGuards = window.spawnGuards.IsChecked == true;
        public bool SpawnTraders = window.spawnTraders.IsChecked == true;
        public bool SpawnPalTraders = window.spawnPalTraders.IsChecked == true;
        public bool SpawnTowerBosses = window.spawnTowerBosses.IsChecked == true;
        public bool SpawnAlphas = window.spawnAlphas.IsChecked == true;
        public bool SpawnRaidBosses = window.spawnRaidBosses.IsChecked == true;
        public bool SpawnPredators = window.spawnPredators.IsChecked == true;
        public bool SpawnHumanBosses = window.spawnHumanBosses.IsChecked == true;
        public bool SpawnSpecial = window.spawnSpecial.IsChecked == true;
        public int FieldLevel = int.Parse(window.fieldLevel.Text);
        public int DungeonLevel = int.Parse(window.dungeonLevel.Text);
        public int FieldBossLevel = int.Parse(window.fieldBossLevel.Text);
        public int DungeonBossLevel = int.Parse(window.dungeonBossLevel.Text);
        public int PredatorLevel = int.Parse(window.predatorLevel.Text);
        public int LevelCap = int.Parse(window.levelCap.Text);
        public int BossAddLevel = int.Parse(window.bossAddLevel.Text);
        public bool ForceAddLevel = window.forceAddLevel.IsChecked == true;
        public string LevelScaleMode = window.levelScaleMode.Text;
        public int RandomLevelMin = int.Parse(window.randomLevelMin.Text);
        public int RandomLevelMax = int.Parse(window.randomLevelMax.Text);
        public int BaseCountMin = int.Parse(window.baseCountMin.Text);
        public int BaseCountMax = int.Parse(window.baseCountMax.Text);
        public int FieldCount = int.Parse(window.fieldCount.Text);
        public int DungeonCount = int.Parse(window.dungeonCount.Text);
        public int FieldBossCount = int.Parse(window.fieldBossCount.Text);
        public int DungeonBossCount = int.Parse(window.dungeonBossCount.Text);
        public int PredatorCount = int.Parse(window.predatorCount.Text);
        public int CountClampMin = int.Parse(window.countClampMin.Text);
        public int CountClampMax = int.Parse(window.countClampMax.Text);
        public int CountClampFirstMin = int.Parse(window.countClampFirstMin.Text);
        public int CountClampFirstMax = int.Parse(window.countClampFirstMax.Text);
        public bool NightOnly = window.nightOnly.IsChecked == true;
        public bool NightOnlyDungeons = window.nightOnlyDungeons.IsChecked == true;
        public bool NightOnlyDungeonBosses = window.nightOnlyDungeonBosses.IsChecked == true;
        public bool NightOnlyBosses = window.nightOnlyBosses.IsChecked == true;
        public bool NightOnlyPredators = window.nightOnlyPredators.IsChecked == true;

        public void RestoreToWindow(MainPage window)
        {
            foreach (FieldInfo fieldInfo in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (fieldInfo.FieldType == typeof(bool))
                {
                    AssignIsChecked(window.GetType().GetField(fieldInfo.Name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                        ?.GetValue(window), (bool) fieldInfo.GetValue(this)!);
                }
                else if (fieldInfo.FieldType.IsArray)
                {
                    Array array = (Array) fieldInfo.GetValue(this)!;
                    for (int i = 0; i < array.Length; ++i)
                    {
                        AssignText(window.GetType().GetField($"{fieldInfo.Name}{i}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                            ?.GetValue(window), array.GetValue(i));
                    }
                }
                else
                {
                    AssignText(window.GetType().GetField(fieldInfo.Name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                        ?.GetValue(window), fieldInfo.GetValue(this));
                }
                void AssignIsChecked(object? control, bool value)
                {
                    if (control != null && (value || control is not RadioButton))
                    {
                        ((ToggleButton) control).IsChecked = value;
                    }
                }
                void AssignText(object? control, object? value)
                {
                    if (control != null)
                    {
                        if (control is TextBox textBox)
                        {
                            textBox.Text = $"{value}";
                        }
                        else if (control is ComboBox comboBox)
                        {
                            comboBox.Text = $"{value}";
                        }
                        else
                        {
                            throw new Exception("Unknown control type being restored from template.");
                        }
                    }
                }
            }
            window.ValidateFormData();
        }
    }

    // Needed because the default json writer always adds a decimal point with a trailing zero otherwise
    public class JsonWriterDecimal : JsonConverter<decimal>
    {
        public override decimal ReadJson(JsonReader reader, Type objectType, decimal existingValue, bool hasExistingValue, JsonSerializer serializer) => throw new NotImplementedException();
        public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer) => writer.WriteRawValue(value.ToString());
    }

    public partial class MainPage : Grid
    {
        public static readonly string AUTO_TEMPLATE_FILENAME = @"Config\AutoSavedTemplate.json";
        public static MainPage Instance { get; private set; } = null!;
        public static readonly string[] GroupWeightModes = [.. typeof(GroupWeightMode).GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => (string) x.GetValue(null)!)];
        public static readonly string[] OverflowFixModes = [.. typeof(OverflowFixMode).GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => (string) x.GetValue(null)!)];
        public static readonly string[] LevelScaleModes = [.. typeof(LevelScaleMode).GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => (string) x.GetValue(null)!)];
        public MainPage(DispatcherOperation dataOperation)
        {
            Instance = this;
            InitializeComponent();
            form.Visibility = Visibility.Collapsed;
            statusBar.Text = "⌛ Initializing...";
            testImage.Source = new BitmapImage(new Uri(Data.PalIcon[Randomize.GetRandomPal()], UriKind.Relative));
            Dispatcher.BeginInvoke(() =>
            {
                dataOperation.Wait();
                Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version!;
                version.Text = $"v{assemblyVersion.Major}.{assemblyVersion.Minor}{(assemblyVersion.Build != 0 ? $".{assemblyVersion.Build}" : "")}";
                weightCustomMode.SelectedItem = GroupWeightMode.WeightMinimum;
                overflowFixMode.SelectedItem = OverflowFixMode.Dynamic;
                levelScaleMode.SelectedItem = LevelScaleMode.BothLevels;
                ConfigData config = SharedWindow.GetConfig();
                autoSaveTemplate.IsChecked = config.AutoRestoreTemplate;
                outputLog.IsChecked = config.OutputLog;
                Randomize.AutoSaveRestoreBackups = config.AutoSaveRestoreBackups;
                Randomize.AutoSaveGenerationData = config.AutoSaveGenerationData;
                if (config.AutoRestoreTemplate)
                {
                    LoadTemplate(UAssetData.AppDataPath(AUTO_TEMPLATE_FILENAME))?.RestoreToWindow(this);
                }
                form.Visibility = Visibility.Visible;
                statusBar.Text = "";
            });
        }

        public AppWindow GetWindow() => (AppWindow) Parent;

        public static FormData? LoadTemplate(string templatePath)
        {
            if (File.Exists(templatePath))
            {
                return JsonConvert.DeserializeObject<FormData>(File.ReadAllText(templatePath));
            }
            return null;
        }

        public static string? SaveTemplate(FormData formData, string templatePath)
        {
            try
            {
                File.WriteAllText(templatePath, JsonConvert.SerializeObject(formData, Formatting.Indented, new JsonSerializerSettings{ Converters = [new JsonWriterDecimal()] }));
            }
            catch (Exception e)
            {
                App.LogException(e);
                return e.Message;
            }
            return null;
        }

        public void ValidateFormData(int seed = 0)
        {
            ValidateNumericText(predatorChance, 0, 30, 100);
            ValidateNumericText(randomSeed, 0, seed);
            ValidateNumericText(groupMin, 1);
            ValidateNumericText(groupMax, int.Parse(groupMin.Text));
            ValidateNumericText(groupMinBoss, 1);
            ValidateNumericText(groupMaxBoss, int.Parse(groupMinBoss.Text));
            ValidateNumericText(spawnListSize, 1, 50);
            ValidateNumericText(vanillaPlusChance, 1, 50, 99);
            ValidateNumericText(fieldLevel, 0, 100);
            ValidateNumericText(dungeonLevel, 0, 100);
            ValidateNumericText(fieldBossLevel, 0, 100);
            ValidateNumericText(dungeonBossLevel, 0, 100);
            ValidateNumericText(predatorLevel, 0, 100);
            ValidateNumericText(levelCap, 1, 60);
            ValidateNumericText(bossAddLevel, 0, 85);
            ValidateNumericText(randomLevelMin, 1, 1, int.Parse(levelCap.Text));
            ValidateNumericText(randomLevelMax, Math.Max(1, int.Parse(randomLevelMin.Text)), int.Parse(levelCap.Text), int.Parse(levelCap.Text));
            ValidateNumericText(rarity67MinLevel, 1, Math.Min(18, int.Parse(levelCap.Text)), int.Parse(levelCap.Text));
            ValidateNumericText(rarity8UpMinLevel, 1, Math.Min(30, int.Parse(levelCap.Text)), int.Parse(levelCap.Text));
            ValidateNumericText(bossesEverywhereChance, 1, 5, 100);
            ValidateNumericText(weightUniformMin, 1, 10);
            ValidateNumericText(weightUniformMax, int.Parse(weightUniformMin.Text));
            ValidateNumericText(weightCustom1, 0, 60);
            ValidateNumericText(weightCustom2, 0, 60);
            ValidateNumericText(weightCustom3, 0, 60);
            ValidateNumericText(weightCustom4, 0, 25);
            ValidateNumericText(weightCustom5, 0, 25);
            ValidateNumericText(weightCustom6, 0, 10);
            ValidateNumericText(weightCustom7, 0, 10);
            ValidateNumericText(weightCustom8, 0, 5);
            ValidateNumericText(weightCustom9, 0, 1);
            ValidateNumericText(weightCustom10, 0, 1);
            ValidateNumericText(weightCustom20, 0, 1);
            ValidateNumericText(humanRarity, 1, 4, 20);
            ValidateNumericText(humanBossRarity, 1, 5, 20);
            ValidateNumericText(humanWeight, 0, 100);
            ValidateNumericText(humanWeightAggro, 0, 50);
            ValidateNumericText(weightNightOnly, 0m, 10000);
            ValidateNumericText(baseCountMin, 0, 1);
            ValidateNumericText(baseCountMax, Math.Max(1, int.Parse(baseCountMin.Text)));
            ValidateNumericText(fieldCount, 0, 100);
            ValidateNumericText(dungeonCount, 0, 100);
            ValidateNumericText(fieldBossCount, 0, 100);
            ValidateNumericText(dungeonBossCount, 0, 100);
            ValidateNumericText(predatorCount, 0, 100);
            ValidateNumericText(countClampMin, 0, 0);
            ValidateNumericText(countClampMax, Math.Max(1, int.Parse(countClampMin.Text)));
            ValidateNumericText(countClampFirstMin, 0, 0);
            ValidateNumericText(countClampFirstMax, Math.Max(1, int.Parse(countClampFirstMin.Text)));
            if (!GroupWeightModes.Contains(weightCustomMode.Text))
            {
                weightCustomMode.SelectedItem = GroupWeightMode.WeightMinimum;
            }
            if (!OverflowFixModes.Contains(overflowFixMode.Text))
            {
                overflowFixMode.SelectedItem = OverflowFixMode.Dynamic;
            }
            if (!LevelScaleModes.Contains(levelScaleMode.Text))
            {
                levelScaleMode.SelectedItem = LevelScaleMode.BothLevels;
            }
            static void ValidateNumericText<T>(TextBox textBox, T minValue, T? defaultValue = null, T? maxValue = null) where T : struct, INumber<T>
            {
                defaultValue ??= minValue;
                try
                {
                    if (textBox.Text.Length == 0)
                    {
                        textBox.Text = $"{defaultValue}";
                    }
                    else if (T.Parse(textBox.Text, null) < minValue)
                    {
                        textBox.Text = $"{minValue}";
                    }
                    else if (maxValue != null && T.Parse(textBox.Text, null) > maxValue)
                    {
                        textBox.Text = $"{maxValue}";
                    }
                }
                catch // FormatException, OverflowException
                {
                    textBox.Text = $"{defaultValue}";
                }
            }
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", ["https://www.nexusmods.com/palworld/mods/1706"]) { CreateNoWindow = true });
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
            int seed = 0;
            if (methodNone.IsChecked != true)
            {
                seed = new Random().Next(1000000000);
                if (randomSeedLocked.IsChecked != true)
                {
                    randomSeed.Text = $"{seed}";
                }
            }
            ValidateFormData(seed);
            progressBar.Visibility = Visibility.Visible;
            progressBar.Value = 0;
            new Thread(formData =>
            {
                try
                {
                    if (!Randomize.GeneratePalSpawns((FormData) formData!))
                    {
                        Dispatcher.Invoke(() => MessageBox.Show(GetWindow(), "Error: No area changes to save.", "Failed To Save Pak", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                }
                catch (Exception e)
                {
                    Dispatcher.BeginInvoke(() => ExceptionDispatchInfo.Capture(e).Throw());
                }
                Dispatcher.Invoke(() => statusBar.Text = "✔️ Generation complete.");
                generating = false;
                Dispatcher.Invoke(() => savePak.IsEnabled = true);
            }).Start(new FormData(this));
        }

        private void ViewSpawns_Click(object sender, RoutedEventArgs e)
        {
            PalSpawnPage.Instance.GetWindow().ShowClean();
            PalSpawnPage.Instance.GetWindow().Focus();
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new()
            {
                FileName = $"Pal-Spawn-Randomizer-Template-{DateTime.Now:MM-dd-yy-HH-mm-ss}",
                DefaultExt = ".json",
                Filter = "JavaScript Object Notation|*.json|All files|*.*"
            };
            if (saveDialog.ShowDialog() == true)
            {
                ValidateFormData();
                string? message = SaveTemplate(new FormData(this)/* { RandomSeedLocked = true }*/, saveDialog.FileName);
                if (message != null)
                {
                    MessageBox.Show(GetWindow(), message, "Failed to Save Template", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadTemplate_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new()
            {
                DefaultExt = ".json",
                Filter = "JavaScript Object Notation|*.json|All files|*.*"
            };
            if (openDialog.ShowDialog() == true && openDialog.FileName != string.Empty)
            {
                ValidateFormData();
                FormData? formData = LoadTemplate(openDialog.FileName);
                if (formData != null)
                {
                    formData.RestoreToWindow(this);
                    ValidateFormData();
                    MessageBox.Show(GetWindow(), "Successfully applied template.", "Template Applied", MessageBoxButton.OK, MessageBoxImage.None);
                }
                else
                {
                    MessageBox.Show(GetWindow(), "Failed to apply template!", "Apply Template Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SpawnListSize_GotFocus(object sender, RoutedEventArgs e)
        {
            methodCustomSize.IsChecked = true;
        }

        private void WeightTypeUniform_GotFocus(object sender, RoutedEventArgs e)
        {
            weightTypeUniform.IsChecked = true;
        }

        private void RarityMinLevel_GotFocus(object sender, RoutedEventArgs e)
        {
            rarityLevelBoost.IsChecked = true;
        }

        private void WeightTypeCustom_Unchecked(object sender, RoutedEventArgs e)
        {
            weightCustomAdvanced.IsChecked = false;
        }

        private void WeightCustomAdvanced_Checked(object sender, RoutedEventArgs e)
        {
            weightTypeCustom.IsChecked = true;
        }

        private void VanillaPlusChance_GotFocus(object sender, RoutedEventArgs e)
        {
            vanillaPlus.IsChecked = true;
        }

        private void BossesEverywhereChance_GotFocus(object sender, RoutedEventArgs e)
        {
            bossesEverywhere.IsChecked = true;
        }

        private void PositiveIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.PositiveIntSize2_PreviewTextInput(sender, e);
        private void PositiveIntSize2_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.PositiveIntSize2_Pasting(sender, e);
        private void PositiveIntPercent_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.PositiveIntPercent_PreviewTextInput(sender, e);
        private void PositiveIntPercent_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.PositiveIntPercent_Pasting(sender, e);
        private void PositiveIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.PositiveIntSize3_PreviewTextInput(sender, e);
        private void PositiveIntSize3_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.PositiveIntSize3_Pasting(sender, e);
        private void PositiveIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.PositiveIntSize4_PreviewTextInput(sender, e);
        private void PositiveIntSize4_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.PositiveIntSize4_Pasting(sender, e);
        private void NonNegIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntSize2_PreviewTextInput(sender, e);
        private void NonNegIntSize2_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntSize2_Pasting(sender, e);
        private void NonNegIntPercent_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntPercent_PreviewTextInput(sender, e);
        private void NonNegIntPercent_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntPercent_Pasting(sender, e);
        private void NonNegIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntSize3_PreviewTextInput(sender, e);
        private void NonNegIntSize3_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntSize3_Pasting(sender, e);
        private void NonNegIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntSize4_PreviewTextInput(sender, e);
        private void NonNegIntSize4_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntSize4_Pasting(sender, e);
        private void NonNegIntSize5_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntSize5_PreviewTextInput(sender, e);
        private void NonNegIntSize5_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntSize5_Pasting(sender, e);
        private void NonNegIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegIntSize9_PreviewTextInput(sender, e);
        private void NonNegIntSize9_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegIntSize9_Pasting(sender, e);
        private void NonNegDecSize5_PreviewTextInput(object sender, TextCompositionEventArgs e) => SharedWindow.NonNegDecSize5_PreviewTextInput(sender, e);
        private void NonNegDecSize5_Pasting(object sender, DataObjectPastingEventArgs e) => SharedWindow.NonNegDecSize5_Pasting(sender, e);
    }
}