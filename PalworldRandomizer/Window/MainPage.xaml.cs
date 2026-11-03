using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.Window;

public partial class MainPage : Grid {
    /// <summary>The relative path of the automatically restored randomizer template.</summary>
    public static readonly string AUTO_TEMPLATE_FILENAME = @"Config\AutoSavedTemplate.json";

    /// <summary>The active randomizer settings page.</summary>
    public static MainPage Instance { get; private set; } = null!;

    /// <summary>The supported group-weight method names shown in the settings selector.</summary>
    public static readonly string[] GroupWeightModes =
    [
        .. typeof(GroupWeightMode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (string)x.GetValue(null)!),
    ];

    /// <summary>The supported overflow-fix method names shown in the settings selector.</summary>
    public static readonly string[] OverflowFixModes =
    [
        .. typeof(OverflowFixMode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (string)x.GetValue(null)!),
    ];

    /// <summary>The supported level-scaling method names shown in the settings selector.</summary>
    public static readonly string[] LevelScaleModes =
    [
        .. typeof(LevelScaleMode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (string)x.GetValue(null)!),
    ];

    /// <summary>Creates the randomizer page and restores preferences after game data is loaded.</summary>
    public MainPage(DispatcherOperation dataOperation) {
        Instance = this;
        InitializeComponent();
        Randomize.GenerationInvalidated += (_, _) => savePalSchema.IsEnabled = false;
        form.Visibility = Visibility.Collapsed;
        testImage.Visibility = Visibility.Collapsed;
        statusBar.Text = "⌛ Initializing...";
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () => {
                dataOperation.Wait();
                testImage.Source = new BitmapImage(
                    new Uri(Data.PalIcon[Randomize.GetRandomPal()], UriKind.Absolute)
                );
                Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version!;
                version.Text =
                    $"v{assemblyVersion.Major}.{assemblyVersion.Minor}"
                    + (assemblyVersion.Build != 0 ? $".{assemblyVersion.Build}" : "");
                weightCustomMode.SelectedItem = GroupWeightMode.WeightMinimum;
                overflowFixMode.SelectedItem = OverflowFixMode.Dynamic;
                levelScaleMode.SelectedItem = LevelScaleMode.BothLevels;
                ConfigData config = SharedWindow.GetConfig();
                autoSaveTemplate.IsChecked = config.AutoRestoreTemplate;
                outputLog.IsChecked = config.OutputLog;
                Randomize.AutoSaveRestoreBackups = config.AutoSaveRestoreBackups;
                Randomize.AutoSaveGenerationData = config.AutoSaveGenerationData;
                if (config.AutoRestoreTemplate) {
                    if (LoadTemplate(UAssetData.AppDataPath(AUTO_TEMPLATE_FILENAME)) is { } savedSettings) {
                        RestoreFormData(savedSettings);
                    }
                }

                testImage.Visibility = Visibility.Visible;
                form.Visibility = Visibility.Visible;
                statusBar.Text = "";
            }
        );
    }

    /// <summary>Returns the desktop window hosting this randomizer page.</summary>
    public AppWindow GetWindow() => (AppWindow)Parent;

    /// <summary>Captures every setting, including hidden controls, on the WPF UI thread.</summary>
    public FormData CaptureFormData() {
        int[] weightCustom = new int[21];
        weightCustom[1] = int.Parse(weightCustom1.Text);
        weightCustom[2] = int.Parse(weightCustom2.Text);
        weightCustom[3] = int.Parse(weightCustom3.Text);
        weightCustom[4] = int.Parse(weightCustom4.Text);
        weightCustom[5] = int.Parse(weightCustom5.Text);
        weightCustom[6] = int.Parse(weightCustom6.Text);
        weightCustom[7] = int.Parse(weightCustom7.Text);
        weightCustom[8] = int.Parse(weightCustom8.Text);
        weightCustom[9] = int.Parse(weightCustom9.Text);
        weightCustom[10] = int.Parse(weightCustom10.Text);
        weightCustom[20] = int.Parse(weightCustom20.Text);
        return new() {
            RandomSeed = int.Parse(randomSeed.Text),
            RandomSeedLocked = randomSeedLocked.IsChecked == true,
            RandomizeField = randomizeField.IsChecked == true,
            RandomizeDungeons = randomizeDungeons.IsChecked == true,
            RandomizeDungeonBosses = randomizeDungeonBosses.IsChecked == true,
            RandomizeFieldBosses = randomizeFieldBosses.IsChecked == true,
            RandomizePredators = randomizePredators.IsChecked == true,
            PredatorChance = int.Parse(predatorChance.Text),
            RandomizeCages = randomizeCages.IsChecked == true,
            RandomizeEggs = randomizeEggs.IsChecked == true,
            RandomizeQuests = randomizeQuests.IsChecked == true,
            RandomizeMimics = randomizeMimics.IsChecked == true,
            RandomizeAllArea = randomizeAllArea.IsChecked == true,
            RandomizeSingleSpawns = randomizeSingleSpawns.IsChecked == true,
            RandomizeHumanBosses = randomizeHumanBosses.IsChecked == true,
            RandomizeHumanBossesPals = randomizeHumanBossesPals.IsChecked == true,
            EqualizeAreaRarity = equalizeAreaRarity.IsChecked == true,
            MethodFull = methodFull.IsChecked == true,
            MethodCustomSize = methodCustomSize.IsChecked == true,
            SpawnListSize = int.Parse(spawnListSize.Text),
            MethodLocalSwap = methodLocalSwap.IsChecked == true,
            MethodGlobalSwap = methodGlobalSwap.IsChecked == true,
            MethodNone = methodNone.IsChecked == true,
            VanillaPlus = vanillaPlus.IsChecked == true,
            VanillaPlusChance = int.Parse(vanillaPlusChance.Text),
            VanillaMerge = vanillaMerge.IsChecked == true,
            VanillaPlusFilter = vanillaPlusFilter.IsChecked == true,
            VanillaMergeFilter = vanillaMergeFilter.IsChecked == true,
            GroupVanilla = groupVanilla.IsChecked == true,
            GroupRandom = groupRandom.IsChecked == true,
            FieldBossExtended = fieldBossExtended.IsChecked == true,
            GroupMin = int.Parse(groupMin.Text),
            GroupMax = int.Parse(groupMax.Text),
            GroupMinBoss = int.Parse(groupMinBoss.Text),
            GroupMaxBoss = int.Parse(groupMaxBoss.Text),
            MultiBoss = multiBoss.IsChecked == true,
            VanillaRestrict = vanillaRestrict.IsChecked == true,
            HumanBossMin = int.Parse(humanBossMin.Text),
            HumanBossMax = int.Parse(humanBossMax.Text),
            HumanBossBossMin = int.Parse(humanBossBossMin.Text),
            HumanBossBossMax = int.Parse(humanBossBossMax.Text),
            HumanBossPalMin = int.Parse(humanBossPalMin.Text),
            HumanBossPalMax = int.Parse(humanBossPalMax.Text),
            HumanBossPalBossMin = int.Parse(humanBossPalBossMin.Text),
            HumanBossPalBossMax = int.Parse(humanBossPalBossMax.Text),
            RarityLevelBoost = rarityLevelBoost.IsChecked == true,
            Rarity67MinLevel = int.Parse(rarity67MinLevel.Text),
            Rarity8UpMinLevel = int.Parse(rarity8UpMinLevel.Text),
            Rarity9UpBossOnly = rarity9UpBossOnly.IsChecked == true,
            Rarity8UpSolo = rarity8UpSolo.IsChecked == true,
            Rarity8UpSanity = rarity8UpSanity.IsChecked == true,
            MixHumanAndPal = mixHumanAndPal.IsChecked == true,
            SeparateAggroHumans = separateAggroHumans.IsChecked == true,
            BossesEverywhere = bossesEverywhere.IsChecked == true,
            BossesEverywhereChance = int.Parse(bossesEverywhereChance.Text),
            BossesEverywhereDungeons = bossesEverywhereDungeons.IsChecked == true,
            BossesEverywhereDungeonsChance = int.Parse(bossesEverywhereDungeonsChance.Text),
            BossEggs = bossEggs.IsChecked == true,
            BossEggsChance = int.Parse(bossEggsChance.Text),
            SeparateFlying = separateFlying.IsChecked == true,
            PredatorConstraint = predatorConstraint.IsChecked == true,
            AllowCagedHumans = allowCagedHumans.IsChecked == true,
            StartSheepBall = startSheepBall.IsChecked == true,
            MinOneCountSolo = minOneCountSolo.IsChecked == true,
            HumanDamageCap = decimal.Parse(humanDamageCap.Text),
            HumanBossDamageCap = decimal.Parse(humanBossDamageCap.Text),
            PredatorDamageCap = decimal.Parse(predatorDamageCap.Text),
            RaidDamageCap = decimal.Parse(raidDamageCap.Text),
            WeightTypeUniform = weightTypeUniform.IsChecked == true,
            WeightTypeCustom = weightTypeCustom.IsChecked == true,
            WeightUniformMin = int.Parse(weightUniformMin.Text),
            WeightUniformMax = int.Parse(weightUniformMax.Text),
            WeightCustom = weightCustom,
            WeightCustomMode = weightCustomMode.Text,
            HumanRarity = int.Parse(humanRarity.Text),
            HumanBossRarity = int.Parse(humanBossRarity.Text),
            MaxCageRarity = int.Parse(maxCageRarity.Text),
            WeightAdjustProbability = weightAdjustProbability.IsChecked == true,
            WeightPrioritizeBoss = weightPrioritizeBoss.IsChecked == true,
            HumanWeight = int.Parse(humanWeight.Text),
            HumanWeightAggro = int.Parse(humanWeightAggro.Text),
            WeightNightOnly = decimal.Parse(weightNightOnly.Text),
            OverflowFixMode = overflowFixMode.Text,
            SpawnPals = spawnPals.IsChecked == true,
            SpawnHumans = spawnHumans.IsChecked == true,
            SpawnPolice = spawnPolice.IsChecked == true,
            SpawnGuards = spawnGuards.IsChecked == true,
            SpawnTraders = spawnTraders.IsChecked == true,
            SpawnPalTraders = spawnPalTraders.IsChecked == true,
            SpawnTowerBosses = spawnTowerBosses.IsChecked == true,
            SpawnAlphas = spawnAlphas.IsChecked == true,
            SpawnRaidBosses = spawnRaidBosses.IsChecked == true,
            SpawnPredators = spawnPredators.IsChecked == true,
            SpawnHumanBosses = spawnHumanBosses.IsChecked == true,
            SpawnSpecial = spawnSpecial.IsChecked == true,
            SpawnTerraria = spawnTerraria.IsChecked == true,
            SpawnTerrariaBosses = spawnTerrariaBosses.IsChecked == true,
            SpawnTowerHumans = spawnTowerHumans.IsChecked == true,
            SpawnPolicePals = spawnPolicePals.IsChecked == true,
            SpawnUselessHumans = spawnUselessHumans.IsChecked == true,
            SpawnVillagers = spawnVillagers.IsChecked == true,
            FieldLevel = int.Parse(fieldLevel.Text),
            DungeonLevel = int.Parse(dungeonLevel.Text),
            FieldBossLevel = int.Parse(fieldBossLevel.Text),
            DungeonBossLevel = int.Parse(dungeonBossLevel.Text),
            PredatorLevel = int.Parse(predatorLevel.Text),
            CageLevel = int.Parse(cageLevel.Text),
            EnableLevelCap = enableLevelCap.IsChecked == true,
            LevelCap = int.Parse(levelCap.Text),
            BossAddLevel = int.Parse(bossAddLevel.Text),
            ForceAddLevel = forceAddLevel.IsChecked == true,
            LevelScaleMode = levelScaleMode.Text,
            RandomLevelMin = int.Parse(randomLevelMin.Text),
            RandomLevelMax = int.Parse(randomLevelMax.Text),
            BaseCountMin = int.Parse(baseCountMin.Text),
            BaseCountMax = int.Parse(baseCountMax.Text),
            FieldCount = int.Parse(fieldCount.Text),
            DungeonCount = int.Parse(dungeonCount.Text),
            FieldBossCount = int.Parse(fieldBossCount.Text),
            DungeonBossCount = int.Parse(dungeonBossCount.Text),
            PredatorCount = int.Parse(predatorCount.Text),
            CountClampMin = int.Parse(countClampMin.Text),
            CountClampMax = int.Parse(countClampMax.Text),
            CountClampFirstMin = int.Parse(countClampFirstMin.Text),
            CountClampFirstMax = int.Parse(countClampFirstMax.Text),
            CountClampBossMin = int.Parse(countClampBossMin.Text),
            CountClampBossMax = int.Parse(countClampBossMax.Text),
            CountClampFirstBossMin = int.Parse(countClampFirstBossMin.Text),
            CountClampFirstBossMax = int.Parse(countClampFirstBossMax.Text),
            NightOnly = nightOnly.IsChecked == true,
            NightOnlyDungeons = nightOnlyDungeons.IsChecked == true,
            NightOnlyDungeonBosses = nightOnlyDungeonBosses.IsChecked == true,
            NightOnlyBosses = nightOnlyBosses.IsChecked == true,
            NightOnlyPredators = nightOnlyPredators.IsChecked == true,
            EggRespawnHours = int.Parse(eggRespawnHours.Text),
            EggRespawnMinutes = int.Parse(eggRespawnMinutes.Text),
            EggRespawnSeconds = int.Parse(eggRespawnSeconds.Text),
        };
    }

    /// <summary>Restores the saved setting values to the randomizer controls and validates them.</summary>
    public void RestoreFormData(FormData formData) {
        foreach (var fieldInfo in formData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (fieldInfo.PropertyType == typeof(bool)) {
                AssignIsChecked(
                    this
                        .GetType()
                        .GetField(
                            fieldInfo.Name,
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase
                        )
                        ?.GetValue(this),
                    (bool)fieldInfo.GetValue(formData)!
                );
            } else if (fieldInfo.PropertyType.IsArray) {
                var array = (Array)fieldInfo.GetValue(formData)!;
                for (int i = 0; i < array.Length; ++i) {
                    AssignText(
                        this
                            .GetType()
                            .GetField(
                                $"{fieldInfo.Name}{i}",
                                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase
                            )
                            ?.GetValue(this),
                        array.GetValue(i)
                    );
                }
            } else {
                AssignText(
                    this
                        .GetType()
                        .GetField(
                            fieldInfo.Name,
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase
                        )
                        ?.GetValue(this),
                    fieldInfo.GetValue(formData)
                );
            }

            // Restores true radio selections without clearing another selection in the same group.
            static void AssignIsChecked(object? control, bool value) {
                if (control != null && (value || control is not RadioButton)) {
                    ((ToggleButton)control).IsChecked = value;
                }
            }

            // Restores text and selector values, ignoring array indices without controls.
            static void AssignText(object? control, object? value) {
                if (control != null) {
                    if (control is TextBox textBox) {
                        textBox.Text = $"{value}";
                    } else if (control is ComboBox comboBox) {
                        comboBox.Text = $"{value}";
                    } else {
                        throw new Exception("Unknown control type being restored from template.");
                    }
                }
            }
        }

        ValidateFormData();
    }

    /// <summary>Loads a template, retaining the current controls for any omitted settings.</summary>
    public FormData? LoadTemplate(string templatePath) {
        if (File.Exists(templatePath)) {
            return TemplateJson.Load(File.ReadAllText(templatePath), CaptureFormData);
        }

        return null;
    }

    /// <summary>Writes the supplied randomizer settings as a JSON template file.</summary>
    public static string? SaveTemplate(FormData formData, string templatePath) {
        try {
            File.WriteAllText(
                templatePath,
                JsonConvert.SerializeObject(
                    formData,
                    Formatting.Indented,
                    new JsonSerializerSettings { Converters = [new JsonWriterDecimal()] }
                )
            );
        } catch (Exception e) {
            App.LogException(e);
            return e.Message;
        }

        return null;
    }

    /// <summary>Normalizes numeric settings to their supported limits and dependent ranges.</summary>
    public void ValidateFormData(int seed = 0) {
        ValidateNumericText(predatorChance, 0, 30, 100);
        ValidateNumericText(randomSeed, 0, seed);
        ValidateNumericText(groupMin, 1);
        ValidateNumericText(groupMax, int.Parse(groupMin.Text));
        ValidateNumericText(groupMinBoss, 1);
        ValidateNumericText(groupMaxBoss, int.Parse(groupMinBoss.Text));

        ValidateNumericText(humanBossMin, 1, 3, 3);
        ValidateNumericText(humanBossMax, int.Parse(humanBossMin.Text), 3, 3);
        ValidateNumericText(humanBossBossMin, 1, null, int.Parse(humanBossMax.Text));
        ValidateNumericText(humanBossBossMax, int.Parse(humanBossBossMin.Text), null, int.Parse(humanBossMax.Text));
        ValidateNumericText(humanBossPalMin, 0, null, 3);
        ValidateNumericText(humanBossPalMax, int.Parse(humanBossPalMin.Text), null, 3);
        ValidateNumericText(humanBossPalBossMin, 0, null, int.Parse(humanBossPalMax.Text));
        ValidateNumericText(
            humanBossPalBossMax,
            int.Parse(humanBossPalBossMin.Text),
            null,
            int.Parse(humanBossPalMax.Text)
        );

        ValidateNumericText(spawnListSize, 1, 50);
        ValidateNumericText(vanillaPlusChance, 1, 50, 99);
        ValidateNumericText(fieldLevel, 0, 100);
        ValidateNumericText(dungeonLevel, 0, 100);
        ValidateNumericText(fieldBossLevel, 0, 100);
        ValidateNumericText(dungeonBossLevel, 0, 100);
        ValidateNumericText(predatorLevel, 0, 100);
        ValidateNumericText(cageLevel, 0, 100);
        ValidateNumericText(levelCap, 1, 80);
        ValidateNumericText(bossAddLevel, 0, 85);
        ValidateNumericText(randomLevelMin, 1, 1, int.Parse(levelCap.Text));
        ValidateNumericText(
            randomLevelMax,
            Math.Max(1, int.Parse(randomLevelMin.Text)),
            int.Parse(levelCap.Text),
            int.Parse(levelCap.Text)
        );
        ValidateNumericText(rarity67MinLevel, 1, Math.Min(18, int.Parse(levelCap.Text)), int.Parse(levelCap.Text));
        ValidateNumericText(rarity8UpMinLevel, 1, Math.Min(30, int.Parse(levelCap.Text)), int.Parse(levelCap.Text));
        ValidateNumericText(bossesEverywhereChance, 1, 5, 100);
        ValidateNumericText(bossesEverywhereDungeonsChance, 1, 5, 100);
        ValidateNumericText(bossEggsChance, 1, 5, 100);

        ValidateNumericText(humanDamageCap, 0m, 1);
        ValidateNumericText(humanBossDamageCap, 0m, 2);
        ValidateNumericText(predatorDamageCap, 0m, 2);
        ValidateNumericText(raidDamageCap, 0m, 9999);

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
        ValidateNumericText(maxCageRarity, 1, 9, 20);
        ValidateNumericText(humanWeight, 0, 100);
        ValidateNumericText(humanWeightAggro, 0, 25);
        ValidateNumericText(weightNightOnly, 0m, 1000);
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
        ValidateNumericText(countClampBossMin, 0, 0);
        ValidateNumericText(countClampBossMax, Math.Max(1, int.Parse(countClampBossMin.Text)));
        ValidateNumericText(countClampFirstBossMin, 0, 0);
        ValidateNumericText(countClampFirstBossMax, Math.Max(1, int.Parse(countClampFirstBossMin.Text)));
        ValidateNumericText(eggRespawnHours, 0, 3);
        ValidateNumericText(eggRespawnMinutes, 0, 0);
        ValidateNumericText(eggRespawnSeconds, 0, 0);
        if (!GroupWeightModes.Contains(weightCustomMode.Text)) {
            weightCustomMode.SelectedItem = GroupWeightMode.WeightMinimum;
        }

        if (!OverflowFixModes.Contains(overflowFixMode.Text)) {
            overflowFixMode.SelectedItem = OverflowFixMode.Dynamic;
        }

        if (!LevelScaleModes.Contains(levelScaleMode.Text)) {
            levelScaleMode.SelectedItem = LevelScaleMode.BothLevels;
        }

        static void ValidateNumericText<T>(TextBox textBox, T minValue, T? defaultValue = null, T? maxValue = null)
            where T : struct, INumber<T> {
            defaultValue ??= minValue;
            try {
                if (textBox.Text.Length == 0) {
                    textBox.Text = $"{defaultValue}";
                } else if (T.Parse(textBox.Text, null) < minValue) {
                    textBox.Text = $"{minValue}";
                } else if (maxValue != null && T.Parse(textBox.Text, null) > maxValue) {
                    textBox.Text = $"{maxValue}";
                }
            } catch // FormatException, OverflowException
              {
                textBox.Text = $"{defaultValue}";
            }
        }
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e) =>
        Process.Start(
            new ProcessStartInfo("explorer.exe", ["https://www.nexusmods.com/palworld/mods/1706"]) {
                CreateNoWindow = true,
            }
        );

    private bool generating = false;

    private void GeneratePalSpawns(FormData formData) {
        var (areas, outputText) = Randomize.GeneratePalSpawns(
            formData,
            () => {
                PalSpawnPage.SaveBackup();
                return Dispatcher.Invoke(() => outputLog.IsChecked == true);
            },
            value => {
                if (value is { } percent) {
                    Dispatcher.BeginInvoke(() => progressBar.Value = percent);
                } else {
                    Dispatcher.Invoke(() => progressBar.Visibility = Visibility.Collapsed);
                }
            },
            generatedAreas => PalSpawnPage.Instance.Dispatcher.Invoke(() =>
                PalSpawnPage.Instance.areaList.ItemsSource = generatedAreas
            )
        );
        if (outputText != null) {
            try {
                File.WriteAllText("Palworld-Randomizer-Log.txt", outputText);
            } catch (Exception e) {
                Dispatcher.Invoke(() =>
                    MessageBox.Show(
                        GetWindow(),
                        "Error: Failed to write output log.\n\n" + e.Message,
                        "Output Log Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    )
                );
            }
        }

        try {
            if (Randomize.AutoSaveGenerationData) {
                string date = $"{DateTime.Now:MM-dd-yy-HH-mm-ss}";
                Directory.CreateDirectory(UAssetData.AppDataPath("Log"));
                File.WriteAllText(
                    UAssetData.AppDataPath($"Log\\{date}.csv"),
                    FileModify.GenerateCSV(areas),
                    Encoding.UTF8
                );
                File.WriteAllText(
                    UAssetData.AppDataPath($"Log\\{date}.json"),
                    JsonConvert.SerializeObject(
                        formData,
                        Formatting.Indented,
                        new JsonSerializerSettings { Converters = [new JsonWriterDecimal()] }
                    )
                );
            }
        } catch (Exception exception) {
            App.LogException(exception);
        }

        Dispatcher.Invoke(() => statusBar.Text = "💾 Creating...");
    }

    private void Randomize_Click(object sender, RoutedEventArgs e) {
        if (generating) {
            return;
        }

        statusBar.Text = "⏱️ Generating...";
        generating = true;
        generateButton.IsEnabled = false;
        savePalSchema.IsEnabled = false;
        int seed = 0;
        if (methodNone.IsChecked != true) {
            seed = new Random().Next(1000000000);
            if (randomSeedLocked.IsChecked != true) {
                randomSeed.Text = $"{seed}";
            }
        }

        ValidateFormData(seed);
        progressBar.Visibility = Visibility.Visible;
        progressBar.Value = 0;
        new Thread(formData => {
            try {
                GeneratePalSpawns((FormData)formData!);
            } catch (Exception e) {
                Dispatcher.BeginInvoke(() => ExceptionDispatchInfo.Capture(e).Throw());
            }

            Dispatcher.Invoke(() => statusBar.Text = "✔️ Generation complete.");
            generating = false;
            Dispatcher.Invoke(() => {
                generateButton.IsEnabled = true;
                savePalSchema.IsEnabled = true;
            });
        }).Start(CaptureFormData());
    }

    private void SavePalSchema_Click(object sender, RoutedEventArgs e) => PalSpawnPage.Instance.SavePalSchema();

    private void ViewSpawns_Click(object sender, RoutedEventArgs e) {
        PalSpawnPage.Instance.GetWindow().ShowClean();
        PalSpawnPage.Instance.GetWindow().Focus();
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e) {
        SaveFileDialog saveDialog = new()
        {
            FileName = $"Pal-Spawn-Randomizer-Template-{DateTime.Now:MM-dd-yy-HH-mm-ss}",
            DefaultExt = ".json",
            Filter = "JavaScript Object Notation|*.json|All files|*.*",
        };
        if (saveDialog.ShowDialog() == true) {
            ValidateFormData();
            string? message = SaveTemplate(CaptureFormData(), saveDialog.FileName);
            if (message != null) {
                MessageBox.Show(
                    GetWindow(),
                    message,
                    "Failed to Save Template",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }

    private void LoadTemplate_Click(object sender, RoutedEventArgs e) {
        OpenFileDialog openDialog = new()
        {
            DefaultExt = ".json",
            Filter = "JavaScript Object Notation|*.json|All files|*.*",
        };
        if (openDialog.ShowDialog() == true && openDialog.FileName != string.Empty) {
            ValidateFormData();
            FormData? formData = LoadTemplate(openDialog.FileName);
            if (formData != null) {
                RestoreFormData(formData);
                ValidateFormData();
                MessageBox.Show(
                    GetWindow(),
                    "Successfully applied template.",
                    "Template Applied",
                    MessageBoxButton.OK,
                    MessageBoxImage.None
                );
            } else {
                MessageBox.Show(
                    GetWindow(),
                    "Failed to apply template!",
                    "Apply Template Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }

    private void SpawnListSize_GotFocus(object sender, RoutedEventArgs e) => methodCustomSize.IsChecked = true;

    private void WeightTypeUniform_GotFocus(object sender, RoutedEventArgs e) => weightTypeUniform.IsChecked = true;

    private void RarityMinLevel_GotFocus(object sender, RoutedEventArgs e) => rarityLevelBoost.IsChecked = true;

    private void WeightTypeCustom_Unchecked(object sender, RoutedEventArgs e) =>
        weightCustomAdvanced.IsChecked = false;

    private void WeightCustomAdvanced_Checked(object sender, RoutedEventArgs e) =>
        weightTypeCustom.IsChecked = true;

    private void VanillaPlusChance_GotFocus(object sender, RoutedEventArgs e) => vanillaPlus.IsChecked = true;

    private void BossesEverywhereChance_GotFocus(object sender, RoutedEventArgs e) =>
        bossesEverywhere.IsChecked = true;

    private void BossesEverywhereDungeonsChance_GotFocus(object sender, RoutedEventArgs e) =>
        bossesEverywhereDungeons.IsChecked = true;

    private void BossEggsChance_GotFocus(object sender, RoutedEventArgs e) => bossEggs.IsChecked = true;

    private void PositiveIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.PositiveIntSize2_PreviewTextInput(sender, e);

    private void PositiveIntSize2_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.PositiveIntSize2_Pasting(sender, e);

    private void PositiveIntPercent_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.PositiveIntPercent_PreviewTextInput(sender, e);

    private void PositiveIntPercent_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.PositiveIntPercent_Pasting(sender, e);

    private void PositiveIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.PositiveIntSize3_PreviewTextInput(sender, e);

    private void PositiveIntSize3_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.PositiveIntSize3_Pasting(sender, e);

    private void PositiveIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.PositiveIntSize4_PreviewTextInput(sender, e);

    private void PositiveIntSize4_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.PositiveIntSize4_Pasting(sender, e);

    private void NonNegIntSize2_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegIntSize2_PreviewTextInput(sender, e);

    private void NonNegIntSize2_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegIntSize2_Pasting(sender, e);

    private void NonNegIntPercent_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegIntPercent_PreviewTextInput(sender, e);

    private void NonNegIntPercent_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegIntPercent_Pasting(sender, e);

    private void NonNegIntSize3_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegIntSize3_PreviewTextInput(sender, e);

    private void NonNegIntSize3_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegIntSize3_Pasting(sender, e);

    private void NonNegIntSize4_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegIntSize4_PreviewTextInput(sender, e);

    private void NonNegIntSize4_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegIntSize4_Pasting(sender, e);

    private void NonNegIntSize5_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegIntSize5_PreviewTextInput(sender, e);

    private void NonNegIntSize5_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegIntSize5_Pasting(sender, e);

    private void NonNegIntSize9_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegIntSize9_PreviewTextInput(sender, e);

    private void NonNegIntSize9_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegIntSize9_Pasting(sender, e);

    private void NonNegDecSize5_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        SharedWindow.NonNegDecSize5_PreviewTextInput(sender, e);

    private void NonNegDecSize5_Pasting(object sender, DataObjectPastingEventArgs e) =>
        SharedWindow.NonNegDecSize5_Pasting(sender, e);
}