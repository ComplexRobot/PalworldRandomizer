using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.Window;

/// <summary>Names of the supported methods for combining character weights within a group.</summary>
public static class GroupWeightMode {
    /// <summary>The selector name for the Weight Sum mode.</summary>
    public static readonly string WeightSum = "Weight Sum";

    /// <summary>The selector name for the Weight Average mode.</summary>
    public static readonly string WeightAverage = "Weight Average";

    /// <summary>The selector name for the Rarity Average (Rounded) mode.</summary>
    public static readonly string RarityAverageRounded = "Rarity Average (Rounded)";

    /// <summary>The selector name for the Rarity Average (Blend Fractions) mode.</summary>
    public static readonly string RarityAverageBlend = "Rarity Average (Blend Fractions)";

    /// <summary>The selector name for the Rarity Average (Blend 10 to 20) mode.</summary>
    public static readonly string RarityAverageBlend10To20 = "Rarity Average (Blend 10 to 20)";

    /// <summary>The selector name for the Rarity Minimum mode.</summary>
    public static readonly string RarityMinimum = "Rarity Minimum";

    /// <summary>The selector name for the Rarity Maximum mode.</summary>
    public static readonly string RarityMaximum = "Rarity Maximum";

    /// <summary>The selector name for the Weight Minimum mode.</summary>
    public static readonly string WeightMinimum = "Weight Minimum";

    /// <summary>The selector name for the Weight Maximum mode.</summary>
    public static readonly string WeightMaximum = "Weight Maximum";
}

/// <summary>Names of the supported strategies for preventing integer spawn-weight overflow.</summary>
public static class OverflowFixMode {
    /// <summary>The selector name for the Scale All mode.</summary>
    public static readonly string ScaleAll = "Scale All";

    /// <summary>The selector name for the Scale Night-Only mode.</summary>
    public static readonly string ScaleNightOnly = "Scale Night-Only";

    /// <summary>The selector name for the Dynamic (Multi-Pass) mode.</summary>
    public static readonly string Dynamic = "Dynamic (Multi-Pass)";
}

/// <summary>Names of the supported methods for scaling a spawn level range.</summary>
public static class LevelScaleMode {
    /// <summary>The selector name for the Average Level mode.</summary>
    public static readonly string Average = "Average Level";

    /// <summary>The selector name for the Full mode.</summary>
    public static readonly string BothLevels = "Full";

    /// <summary>The selector name for the Max. Level mode.</summary>
    public static readonly string MaxLevel = "Max. Level";

    /// <summary>The selector name for the Extend Min./Max. mode.</summary>
    public static readonly string LockExtreme = "Extend Min./Max.";

    /// <summary>The selector name for the Maximize Range mode.</summary>
    public static readonly string MaxRange = "Maximize Range";

    /// <summary>The selector name for the Min. Level mode.</summary>
    public static readonly string MinLevel = "Min. Level";

    /// <summary>The selector name for the Random mode.</summary>
    public static readonly string Random = "Random";
}

/// <summary>A snapshot of the randomizer settings used for template persistence and generation.</summary>
/// <param name="window">The randomizer page whose current settings are captured.</param>
public class FormData(MainPage window) {
    /// <summary>Captures the current randomizer page settings.</summary>
    public FormData()
        : this(MainPage.Instance) { }

    /// <summary>The seed used for deterministic randomization.</summary>
    public int RandomSeed { get; set; } = int.Parse(window.randomSeed.Text);

    /// <summary>Whether generation reuses the current seed instead of choosing a new one.</summary>
    public bool RandomSeedLocked { get; set; } = window.randomSeedLocked.IsChecked == true;

    /// <summary>Whether overworld Pal areas are randomized.</summary>
    public bool RandomizeField { get; set; } = window.randomizeField.IsChecked == true;

    /// <summary>Whether regular dungeon Pal areas are randomized.</summary>
    public bool RandomizeDungeons { get; set; } = window.randomizeDungeons.IsChecked == true;

    /// <summary>Whether dungeon boss areas are randomized.</summary>
    public bool RandomizeDungeonBosses { get; set; } = window.randomizeDungeonBosses.IsChecked == true;

    /// <summary>Whether overworld boss areas are randomized.</summary>
    public bool RandomizeFieldBosses { get; set; } = window.randomizeFieldBosses.IsChecked == true;

    /// <summary>Whether predator appearances are randomized.</summary>
    public bool RandomizePredators { get; set; } = window.randomizePredators.IsChecked == true;

    /// <summary>The appearance probability used for randomized predator spawns.</summary>
    public int PredatorChance { get; set; } = int.Parse(window.predatorChance.Text);

    /// <summary>Whether characters held in camp cages are randomized.</summary>
    public bool RandomizeCages { get; set; } = window.randomizeCages.IsChecked == true;

    /// <summary>Whether characters obtained from overworld eggs are randomized.</summary>
    public bool RandomizeEggs { get; set; } = window.randomizeEggs.IsChecked == true;

    /// <summary>Whether quest-related spawn areas are randomized.</summary>
    public bool RandomizeQuests { get; set; } = window.randomizeQuests.IsChecked == true;

    /// <summary>Whether mimic spawn areas are randomized.</summary>
    public bool RandomizeMimics { get; set; } = window.randomizeMimics.IsChecked == true;

    /// <summary>Whether global spawn tables are randomized.</summary>
    public bool RandomizeAllArea { get; set; } = window.randomizeAllArea.IsChecked == true;

    /// <summary>Whether single-character spawn tables are randomized.</summary>
    public bool RandomizeSingleSpawns { get; set; } = window.randomizeSingleSpawns.IsChecked == true;

    /// <summary>Whether human boss spawn areas are randomized.</summary>
    public bool RandomizeHumanBosses { get; set; } = window.randomizeHumanBosses.IsChecked == true;

    /// <summary>Whether the Pal companions in human boss areas are randomized.</summary>
    public bool RandomizeHumanBossesPals { get; set; } = window.randomizeHumanBossesPals.IsChecked == true;

    /// <summary>Whether species rarity is equalized across randomized areas.</summary>
    public bool EqualizeAreaRarity { get; set; } = window.equalizeAreaRarity.IsChecked == true;

    /// <summary>Whether each area can contain every permitted species.</summary>
    public bool MethodFull { get; set; } = window.methodFull.IsChecked == true;

    /// <summary>Whether each area uses a custom number of spawn groups.</summary>
    public bool MethodCustomSize { get; set; } = window.methodCustomSize.IsChecked == true;

    /// <summary>The number of groups generated per area in custom-size mode.</summary>
    public int SpawnListSize { get; set; } = int.Parse(window.spawnListSize.Text);

    /// <summary>Whether generated areas retain their vanilla number of spawn groups.</summary>
    public bool MethodLocalSwap { get; set; } = window.methodLocalSwap.IsChecked == true;

    /// <summary>Whether species replacements are shared consistently across areas.</summary>
    public bool MethodGlobalSwap { get; set; } = window.methodGlobalSwap.IsChecked == true;

    /// <summary>Whether vanilla groups are retained instead of randomizing species.</summary>
    public bool MethodNone { get; set; } = window.methodNone.IsChecked == true;

    /// <summary>Whether vanilla groups are added alongside randomized groups.</summary>
    public bool VanillaPlus { get; set; } = window.vanillaPlus.IsChecked == true;

    /// <summary>The weight percentage reserved for added vanilla groups.</summary>
    public int VanillaPlusChance { get; set; } = int.Parse(window.vanillaPlusChance.Text);

    /// <summary>Whether matching vanilla and randomized entries are merged.</summary>
    public bool VanillaMerge { get; set; } = window.vanillaMerge.IsChecked == true;

    /// <summary>Whether added vanilla groups are filtered by the permitted character choices.</summary>
    public bool VanillaPlusFilter { get; set; } = window.vanillaPlusFilter.IsChecked == true;

    /// <summary>Whether merged vanilla groups are filtered by the permitted character choices.</summary>
    public bool VanillaMergeFilter { get; set; } = window.vanillaMergeFilter.IsChecked == true;

    /// <summary>Whether character groups are derived from vanilla group compositions.</summary>
    public bool GroupVanilla { get; set; } = window.groupVanilla.IsChecked == true;

    /// <summary>Whether randomized character groups are generated.</summary>
    public bool GroupRandom { get; set; } = window.groupRandom.IsChecked == true;

    /// <summary>Whether overworld boss areas can contain multiple boss groups.</summary>
    public bool FieldBossExtended { get; set; } = window.fieldBossExtended.IsChecked == true;

    /// <summary>The minimum size of a regular randomized group.</summary>
    public int GroupMin { get; set; } = int.Parse(window.groupMin.Text);

    /// <summary>The maximum size of a regular randomized group.</summary>
    public int GroupMax { get; set; } = int.Parse(window.groupMax.Text);

    /// <summary>The minimum size of a randomized boss group.</summary>
    public int GroupMinBoss { get; set; } = int.Parse(window.groupMinBoss.Text);

    /// <summary>The maximum size of a randomized boss group.</summary>
    public int GroupMaxBoss { get; set; } = int.Parse(window.groupMaxBoss.Text);

    /// <summary>Whether every character in a boss group is itself a boss.</summary>
    public bool MultiBoss { get; set; } = window.multiBoss.IsChecked == true;

    /// <summary>Whether generated groups are restricted to vanilla group compositions.</summary>
    public bool VanillaRestrict { get; set; } = window.vanillaRestrict.IsChecked == true;

    /// <summary>The minimum human count in a regular human boss area.</summary>
    public int HumanBossMin { get; set; } = int.Parse(window.humanBossMin.Text);

    /// <summary>The maximum human count in a regular human boss area.</summary>
    public int HumanBossMax { get; set; } = int.Parse(window.humanBossMax.Text);

    /// <summary>The minimum human count in a boss-group variant of a human boss area.</summary>
    public int HumanBossBossMin { get; set; } = int.Parse(window.humanBossBossMin.Text);

    /// <summary>The maximum human count in a boss-group variant of a human boss area.</summary>
    public int HumanBossBossMax { get; set; } = int.Parse(window.humanBossBossMax.Text);

    /// <summary>The minimum number of Pal companions in a regular human boss area.</summary>
    public int HumanBossPalMin { get; set; } = int.Parse(window.humanBossPalMin.Text);

    /// <summary>The maximum number of Pal companions in a regular human boss area.</summary>
    public int HumanBossPalMax { get; set; } = int.Parse(window.humanBossPalMax.Text);

    /// <summary>The minimum number of Pal companions in a boss-group variant of a human boss area.</summary>
    public int HumanBossPalBossMin { get; set; } = int.Parse(window.humanBossPalBossMin.Text);

    /// <summary>The maximum number of Pal companions in a boss-group variant of a human boss area.</summary>
    public int HumanBossPalBossMax { get; set; } = int.Parse(window.humanBossPalBossMax.Text);

    /// <summary>Whether rare Pals receive a minimum-level safeguard.</summary>
    public bool RarityLevelBoost { get; set; } = window.rarityLevelBoost.IsChecked == true;

    /// <summary>The minimum level used for Pals with rarity six or seven.</summary>
    public int Rarity67MinLevel { get; set; } = int.Parse(window.rarity67MinLevel.Text);

    /// <summary>The minimum level used for Pals with rarity eight or higher.</summary>
    public int Rarity8UpMinLevel { get; set; } = int.Parse(window.rarity8UpMinLevel.Text);

    /// <summary>Whether Pals with rarity nine or higher are restricted to boss groups.</summary>
    public bool Rarity9UpBossOnly { get; set; } = window.rarity9UpBossOnly.IsChecked == true;

    /// <summary>Whether Pals with rarity eight or higher spawn without other characters.</summary>
    public bool Rarity8UpSolo { get; set; } = window.rarity8UpSolo.IsChecked == true;

    /// <summary>Whether additional rare-Pal group restrictions are applied.</summary>
    public bool Rarity8UpSanity { get; set; } = window.rarity8UpSanity.IsChecked == true;

    /// <summary>Whether a randomized group may contain both humans and Pals.</summary>
    public bool MixHumanAndPal { get; set; } = window.mixHumanAndPal.IsChecked == true;

    /// <summary>Whether non-aggressive humans are kept separate from aggressive characters.</summary>
    public bool SeparateAggroHumans { get; set; } = window.separateAggroHumans.IsChecked == true;

    /// <summary>Whether boss groups may be added to regular overworld areas.</summary>
    public bool BossesEverywhere { get; set; } = window.bossesEverywhere.IsChecked == true;

    /// <summary>The weight percentage assigned to bosses added to regular overworld areas.</summary>
    public int BossesEverywhereChance { get; set; } = int.Parse(window.bossesEverywhereChance.Text);

    /// <summary>Whether boss groups may be added to regular dungeon areas.</summary>
    public bool BossesEverywhereDungeons { get; set; } = window.bossesEverywhereDungeons.IsChecked == true;

    /// <summary>The weight percentage assigned to bosses added to regular dungeon areas.</summary>
    public int BossesEverywhereDungeonsChance { get; set; } = int.Parse(window.bossesEverywhereDungeonsChance.Text);

    /// <summary>Whether overworld eggs may produce boss Pals.</summary>
    public bool BossEggs { get; set; } = window.bossEggs.IsChecked == true;

    /// <summary>The weight percentage assigned to boss Pals in overworld eggs.</summary>
    public int BossEggsChance { get; set; } = int.Parse(window.bossEggsChance.Text);

    /// <summary>Whether flying Pals are kept in compatible groups.</summary>
    public bool SeparateFlying { get; set; } = window.separateFlying.IsChecked == true;

    /// <summary>Whether predator group-composition restrictions are applied.</summary>
    public bool PredatorConstraint { get; set; } = window.predatorConstraint.IsChecked == true;

    /// <summary>Whether humans may be selected for camp cages.</summary>
    public bool AllowCagedHumans { get; set; } = window.allowCagedHumans.IsChecked == true;

    /// <summary>Whether the starting area retains a Lamball spawn.</summary>
    public bool StartSheepBall { get; set; } = window.startSheepBall.IsChecked == true;

    /// <summary>Whether solo spawn groups must contain at least one character.</summary>
    public bool MinOneCountSolo { get; set; } = window.minOneCountSolo.IsChecked == true;

    /// <summary>The allowed outgoing-damage multiplier for regular human spawns.</summary>
    public decimal HumanDamageCap { get; set; } = decimal.Parse(window.humanDamageCap.Text);

    /// <summary>The allowed outgoing-damage multiplier for human boss spawns.</summary>
    public decimal HumanBossDamageCap { get; set; } = decimal.Parse(window.humanBossDamageCap.Text);

    /// <summary>The allowed outgoing-damage multiplier for predator spawns.</summary>
    public decimal PredatorDamageCap { get; set; } = decimal.Parse(window.predatorDamageCap.Text);

    /// <summary>The allowed outgoing-damage multiplier for raid boss spawns.</summary>
    public decimal RaidDamageCap { get; set; } = decimal.Parse(window.raidDamageCap.Text);

    /// <summary>Whether group weights are drawn from a uniform numeric range.</summary>
    public bool WeightTypeUniform { get; set; } = window.weightTypeUniform.IsChecked == true;

    /// <summary>Whether group weights are calculated from character rarity.</summary>
    public bool WeightTypeCustom { get; set; } = window.weightTypeCustom.IsChecked == true;

    /// <summary>The minimum weight drawn in uniform-weight mode.</summary>
    public int WeightUniformMin { get; set; } = int.Parse(window.weightUniformMin.Text);

    /// <summary>The maximum weight drawn in uniform-weight mode.</summary>
    public int WeightUniformMax { get; set; } = int.Parse(window.weightUniformMax.Text);

    /// <summary>Rarity-based weights indexed by the game rarity value.</summary>
    public int[] WeightCustom { get; set; } =
        (
            (Func<int[]>)(
                () => {
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
                }
            )
        )();

    /// <summary>The method used to combine rarity-based character weights within a group.</summary>
    public string WeightCustomMode { get; set; } = window.weightCustomMode.Text;

    /// <summary>The rarity used for regular humans when calculating group weights.</summary>
    public int HumanRarity { get; set; } = int.Parse(window.humanRarity.Text);

    /// <summary>The rarity used for human bosses when calculating group weights.</summary>
    public int HumanBossRarity { get; set; } = int.Parse(window.humanBossRarity.Text);

    /// <summary>The highest Pal rarity permitted in camp cages.</summary>
    public int MaxCageRarity { get; set; } = int.Parse(window.maxCageRarity.Text);

    /// <summary>Whether calculated group weights are converted to percentages.</summary>
    public bool WeightAdjustProbability { get; set; } = window.weightAdjustProbability.IsChecked == true;

    /// <summary>Whether boss rarity is prioritized when calculating a mixed group weight.</summary>
    public bool WeightPrioritizeBoss { get; set; } = window.weightPrioritizeBoss.IsChecked == true;

    /// <summary>The percentage multiplier applied to human group weights.</summary>
    public int HumanWeight { get; set; } = int.Parse(window.humanWeight.Text);

    /// <summary>The percentage multiplier applied to aggressive human group weights.</summary>
    public int HumanWeightAggro { get; set; } = int.Parse(window.humanWeightAggro.Text);

    /// <summary>The weight multiplier applied to night-only groups.</summary>
    public decimal WeightNightOnly { get; set; } = decimal.Parse(window.weightNightOnly.Text);

    /// <summary>The strategy used to keep spawn weights within the game integer limits.</summary>
    public string OverflowFixMode { get; set; } = window.overflowFixMode.Text;

    /// <summary>Whether normal Pals are permitted in generated groups.</summary>
    public bool SpawnPals { get; set; } = window.spawnPals.IsChecked == true;

    /// <summary>Whether aggressive humans are permitted in generated groups.</summary>
    public bool SpawnHumans { get; set; } = window.spawnHumans.IsChecked == true;

    /// <summary>Whether regular police characters are permitted in generated groups.</summary>
    public bool SpawnPolice { get; set; } = window.spawnPolice.IsChecked == true;

    /// <summary>Whether guards are permitted in generated groups.</summary>
    public bool SpawnGuards { get; set; } = window.spawnGuards.IsChecked == true;

    /// <summary>Whether item merchants are permitted in generated groups.</summary>
    public bool SpawnTraders { get; set; } = window.spawnTraders.IsChecked == true;

    /// <summary>Whether Pal merchants are permitted in generated groups.</summary>
    public bool SpawnPalTraders { get; set; } = window.spawnPalTraders.IsChecked == true;

    /// <summary>Whether tower bosses are permitted in generated groups.</summary>
    public bool SpawnTowerBosses { get; set; } = window.spawnTowerBosses.IsChecked == true;

    /// <summary>Whether normal boss variants are permitted in generated groups.</summary>
    public bool SpawnAlphas { get; set; } = window.spawnAlphas.IsChecked == true;

    /// <summary>Whether raid bosses are permitted in generated groups.</summary>
    public bool SpawnRaidBosses { get; set; } = window.spawnRaidBosses.IsChecked == true;

    /// <summary>Whether predators are permitted in generated groups.</summary>
    public bool SpawnPredators { get; set; } = window.spawnPredators.IsChecked == true;

    /// <summary>Whether human bosses are permitted in generated groups.</summary>
    public bool SpawnHumanBosses { get; set; } = window.spawnHumanBosses.IsChecked == true;

    /// <summary>Whether humans with special effects are permitted in generated groups.</summary>
    public bool SpawnSpecial { get; set; } = window.spawnSpecial.IsChecked == true;

    /// <summary>Whether regular Terraria monsters are permitted in generated groups.</summary>
    public bool SpawnTerraria { get; set; } = window.spawnTerraria.IsChecked == true;

    /// <summary>Whether Terraria bosses are permitted in generated groups.</summary>
    public bool SpawnTerrariaBosses { get; set; } = window.spawnTerrariaBosses.IsChecked == true;

    /// <summary>Whether non-boss tower humans are permitted in generated groups.</summary>
    public bool SpawnTowerHumans { get; set; } = window.spawnTowerHumans.IsChecked == true;

    /// <summary>Whether police-ridden Pal variants are permitted in generated groups.</summary>
    public bool SpawnPolicePals { get; set; } = window.spawnPolicePals.IsChecked == true;

    /// <summary>Whether additional ordinary humans are permitted in generated groups.</summary>
    public bool SpawnUselessHumans { get; set; } = window.spawnUselessHumans.IsChecked == true;

    /// <summary>Whether generic villagers are permitted in generated groups.</summary>
    public bool SpawnVillagers { get; set; } = window.spawnVillagers.IsChecked == true;

    /// <summary>The percentage level multiplier for regular overworld spawns.</summary>
    public int FieldLevel { get; set; } = int.Parse(window.fieldLevel.Text);

    /// <summary>The percentage level multiplier for regular dungeon spawns.</summary>
    public int DungeonLevel { get; set; } = int.Parse(window.dungeonLevel.Text);

    /// <summary>The percentage level multiplier for overworld boss spawns.</summary>
    public int FieldBossLevel { get; set; } = int.Parse(window.fieldBossLevel.Text);

    /// <summary>The percentage level multiplier for dungeon boss spawns.</summary>
    public int DungeonBossLevel { get; set; } = int.Parse(window.dungeonBossLevel.Text);

    /// <summary>The percentage level multiplier for predator spawns.</summary>
    public int PredatorLevel { get; set; } = int.Parse(window.predatorLevel.Text);

    /// <summary>The percentage level multiplier for caged characters.</summary>
    public int CageLevel { get; set; } = int.Parse(window.cageLevel.Text);

    /// <summary>Whether generated character levels are capped.</summary>
    public bool EnableLevelCap { get; set; } = window.enableLevelCap.IsChecked == true;

    /// <summary>The highest permitted generated character level when the cap is enabled.</summary>
    public int LevelCap { get; set; } = int.Parse(window.levelCap.Text);

    /// <summary>The relative level scale used for additional characters in boss groups.</summary>
    public int BossAddLevel { get; set; } = int.Parse(window.bossAddLevel.Text);

    /// <summary>Whether the additional-character level scale is forced on the affected groups.</summary>
    public bool ForceAddLevel { get; set; } = window.forceAddLevel.IsChecked == true;

    /// <summary>The method used to transform minimum and maximum spawn levels.</summary>
    public string LevelScaleMode { get; set; } = window.levelScaleMode.Text;

    /// <summary>The minimum level in random-level scaling mode.</summary>
    public int RandomLevelMin { get; set; } = int.Parse(window.randomLevelMin.Text);

    /// <summary>The maximum level in random-level scaling mode.</summary>
    public int RandomLevelMax { get; set; } = int.Parse(window.randomLevelMax.Text);

    /// <summary>The minimum base count used before area-specific count multipliers.</summary>
    public int BaseCountMin { get; set; } = int.Parse(window.baseCountMin.Text);

    /// <summary>The maximum base count used before area-specific count multipliers.</summary>
    public int BaseCountMax { get; set; } = int.Parse(window.baseCountMax.Text);

    /// <summary>The percentage count multiplier for regular overworld spawns.</summary>
    public int FieldCount { get; set; } = int.Parse(window.fieldCount.Text);

    /// <summary>The percentage count multiplier for regular dungeon spawns.</summary>
    public int DungeonCount { get; set; } = int.Parse(window.dungeonCount.Text);

    /// <summary>The percentage count multiplier for overworld boss spawns.</summary>
    public int FieldBossCount { get; set; } = int.Parse(window.fieldBossCount.Text);

    /// <summary>The percentage count multiplier for dungeon boss spawns.</summary>
    public int DungeonBossCount { get; set; } = int.Parse(window.dungeonBossCount.Text);

    /// <summary>The percentage count multiplier for predator spawns.</summary>
    public int PredatorCount { get; set; } = int.Parse(window.predatorCount.Text);

    /// <summary>The lower count limit applied to regular character entries.</summary>
    public int CountClampMin { get; set; } = int.Parse(window.countClampMin.Text);

    /// <summary>The upper count limit applied to regular character entries.</summary>
    public int CountClampMax { get; set; } = int.Parse(window.countClampMax.Text);

    /// <summary>The lower count limit applied to the first character in a regular group.</summary>
    public int CountClampFirstMin { get; set; } = int.Parse(window.countClampFirstMin.Text);

    /// <summary>The upper count limit applied to the first character in a regular group.</summary>
    public int CountClampFirstMax { get; set; } = int.Parse(window.countClampFirstMax.Text);

    /// <summary>The lower count limit applied to character entries in boss groups.</summary>
    public int CountClampBossMin { get; set; } = int.Parse(window.countClampBossMin.Text);

    /// <summary>The upper count limit applied to character entries in boss groups.</summary>
    public int CountClampBossMax { get; set; } = int.Parse(window.countClampBossMax.Text);

    /// <summary>The lower count limit applied to the first character in a boss group.</summary>
    public int CountClampFirstBossMin { get; set; } = int.Parse(window.countClampFirstBossMin.Text);

    /// <summary>The upper count limit applied to the first character in a boss group.</summary>
    public int CountClampFirstBossMax { get; set; } = int.Parse(window.countClampFirstBossMax.Text);

    /// <summary>Whether nocturnal overworld Pals are restricted to nighttime.</summary>
    public bool NightOnly { get; set; } = window.nightOnly.IsChecked == true;

    /// <summary>Whether nocturnal dungeon Pals are restricted to nighttime.</summary>
    public bool NightOnlyDungeons { get; set; } = window.nightOnlyDungeons.IsChecked == true;

    /// <summary>Whether nocturnal dungeon bosses are restricted to nighttime.</summary>
    public bool NightOnlyDungeonBosses { get; set; } = window.nightOnlyDungeonBosses.IsChecked == true;

    /// <summary>Whether nocturnal overworld bosses are restricted to nighttime.</summary>
    public bool NightOnlyBosses { get; set; } = window.nightOnlyBosses.IsChecked == true;

    /// <summary>Whether nocturnal predators are restricted to nighttime.</summary>
    public bool NightOnlyPredators { get; set; } = window.nightOnlyPredators.IsChecked == true;

    /// <summary>The hour component of the overworld egg respawn interval.</summary>
    public int EggRespawnHours { get; set; } = int.Parse(window.eggRespawnHours.Text);

    /// <summary>The minute component of the overworld egg respawn interval.</summary>
    public int EggRespawnMinutes { get; set; } = int.Parse(window.eggRespawnMinutes.Text);

    /// <summary>The second component of the overworld egg respawn interval.</summary>
    public int EggRespawnSeconds { get; set; } = int.Parse(window.eggRespawnSeconds.Text);

    /// <summary>The egg respawn time in minutes.</summary>
    public float EggRespawnTime() =>
        Math.Max(0, EggRespawnHours) * 60 + Math.Max(0, EggRespawnMinutes) + Math.Max(0, EggRespawnSeconds) / 60.0f;

    /// <summary>Restores the saved setting values to the randomizer controls and validates them.</summary>
    public void RestoreToWindow(MainPage window) {
        foreach (var fieldInfo in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (fieldInfo.PropertyType == typeof(bool)) {
                AssignIsChecked(
                    window
                        .GetType()
                        .GetField(
                            fieldInfo.Name,
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase
                        )
                        ?.GetValue(window),
                    (bool)fieldInfo.GetValue(this)!
                );
            } else if (fieldInfo.PropertyType.IsArray) {
                var array = (Array)fieldInfo.GetValue(this)!;
                for (int i = 0; i < array.Length; ++i) {
                    AssignText(
                        window
                            .GetType()
                            .GetField(
                                $"{fieldInfo.Name}{i}",
                                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase
                            )
                            ?.GetValue(window),
                        array.GetValue(i)
                    );
                }
            } else {
                AssignText(
                    window
                        .GetType()
                        .GetField(
                            fieldInfo.Name,
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase
                        )
                        ?.GetValue(window),
                    fieldInfo.GetValue(this)
                );
            }

            void AssignIsChecked(object? control, bool value) {
                if (control != null && (value || control is not RadioButton)) {
                    ((ToggleButton)control).IsChecked = value;
                }
            }

            void AssignText(object? control, object? value) {
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

        window.ValidateFormData();
    }
}

// Needed because the default json writer always adds a decimal point with a trailing zero otherwise
/// <summary>Writes template decimal values without an unnecessary trailing decimal zero.</summary>
public class JsonWriterDecimal : JsonConverter<decimal> {
    /// <summary>Decimal reading is not supported by this write-only template converter.</summary>
    public override decimal ReadJson(
        JsonReader reader,
        Type objectType,
        decimal existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    ) => throw new NotImplementedException();

    /// <summary>Writes a decimal directly without adding a trailing decimal zero.</summary>
    public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer) =>
        writer.WriteRawValue(value.ToString());
}

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
                    LoadTemplate(UAssetData.AppDataPath(AUTO_TEMPLATE_FILENAME))?.RestoreToWindow(this);
                }

                testImage.Visibility = Visibility.Visible;
                form.Visibility = Visibility.Visible;
                statusBar.Text = "";
            }
        );
    }

    /// <summary>Returns the desktop window hosting this randomizer page.</summary>
    public AppWindow GetWindow() => (AppWindow)Parent;

    /// <summary>Loads randomizer settings from a JSON template file.</summary>
    public static FormData? LoadTemplate(string templatePath) {
        if (File.Exists(templatePath)) {
            return JsonConvert.DeserializeObject<FormData>(File.ReadAllText(templatePath));
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
                Randomize.GeneratePalSpawns((FormData)formData!);
            } catch (Exception e) {
                Dispatcher.BeginInvoke(() => ExceptionDispatchInfo.Capture(e).Throw());
            }

            Dispatcher.Invoke(() => statusBar.Text = "✔️ Generation complete.");
            generating = false;
            Dispatcher.Invoke(() => {
                generateButton.IsEnabled = true;
                savePalSchema.IsEnabled = true;
            });
        }).Start(new FormData(this));
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
            string? message = SaveTemplate(
                new FormData(
                    this
                ) /* { RandomSeedLocked = true }*/
                ,
                saveDialog.FileName
            );
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
                formData.RestoreToWindow(this);
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