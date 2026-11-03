namespace PalworldRandomizer.Models;

/// <summary>UI-independent randomizer settings with the original desktop defaults.</summary>
public class FormData {
    /// <summary>The seed used for deterministic randomization.</summary>
    public int RandomSeed { get; set; } = 0;

    /// <summary>Whether generation reuses the current seed instead of choosing a new one.</summary>
    public bool RandomSeedLocked { get; set; } = false;

    /// <summary>Whether overworld Pal areas are randomized.</summary>
    public bool RandomizeField { get; set; } = true;

    /// <summary>Whether regular dungeon Pal areas are randomized.</summary>
    public bool RandomizeDungeons { get; set; } = true;

    /// <summary>Whether dungeon boss areas are randomized.</summary>
    public bool RandomizeDungeonBosses { get; set; } = true;

    /// <summary>Whether overworld boss areas are randomized.</summary>
    public bool RandomizeFieldBosses { get; set; } = true;

    /// <summary>Whether predator appearances are randomized.</summary>
    public bool RandomizePredators { get; set; } = true;

    /// <summary>The appearance probability used for randomized predator spawns.</summary>
    public int PredatorChance { get; set; } = 30;

    /// <summary>Whether characters held in camp cages are randomized.</summary>
    public bool RandomizeCages { get; set; } = true;

    /// <summary>Whether characters obtained from overworld eggs are randomized.</summary>
    public bool RandomizeEggs { get; set; } = true;

    /// <summary>Whether quest-related spawn areas are randomized.</summary>
    public bool RandomizeQuests { get; set; } = false;

    /// <summary>Whether mimic spawn areas are randomized.</summary>
    public bool RandomizeMimics { get; set; } = false;

    /// <summary>Whether global spawn tables are randomized.</summary>
    public bool RandomizeAllArea { get; set; } = true;

    /// <summary>Whether single-character spawn tables are randomized.</summary>
    public bool RandomizeSingleSpawns { get; set; } = true;

    /// <summary>Whether human boss spawn areas are randomized.</summary>
    public bool RandomizeHumanBosses { get; set; } = true;

    /// <summary>Whether the Pal companions in human boss areas are randomized.</summary>
    public bool RandomizeHumanBossesPals { get; set; } = true;

    /// <summary>Whether species rarity is equalized across randomized areas.</summary>
    public bool EqualizeAreaRarity { get; set; } = true;

    /// <summary>Whether each area can contain every permitted species.</summary>
    public bool MethodFull { get; set; } = true;

    /// <summary>Whether each area uses a custom number of spawn groups.</summary>
    public bool MethodCustomSize { get; set; } = false;

    /// <summary>The number of groups generated per area in custom-size mode.</summary>
    public int SpawnListSize { get; set; } = 50;

    /// <summary>Whether generated areas retain their vanilla number of spawn groups.</summary>
    public bool MethodLocalSwap { get; set; } = false;

    /// <summary>Whether species replacements are shared consistently across areas.</summary>
    public bool MethodGlobalSwap { get; set; } = false;

    /// <summary>Whether vanilla groups are retained instead of randomizing species.</summary>
    public bool MethodNone { get; set; } = false;

    /// <summary>Whether vanilla groups are added alongside randomized groups.</summary>
    public bool VanillaPlus { get; set; } = false;

    /// <summary>The weight percentage reserved for added vanilla groups.</summary>
    public int VanillaPlusChance { get; set; } = 50;

    /// <summary>Whether matching vanilla and randomized entries are merged.</summary>
    public bool VanillaMerge { get; set; } = false;

    /// <summary>Whether added vanilla groups are filtered by the permitted character choices.</summary>
    public bool VanillaPlusFilter { get; set; } = true;

    /// <summary>Whether merged vanilla groups are filtered by the permitted character choices.</summary>
    public bool VanillaMergeFilter { get; set; } = true;

    /// <summary>Whether character groups are derived from vanilla group compositions.</summary>
    public bool GroupVanilla { get; set; } = false;

    /// <summary>Whether randomized character groups are generated.</summary>
    public bool GroupRandom { get; set; } = true;

    /// <summary>Whether overworld boss areas can contain multiple boss groups.</summary>
    public bool FieldBossExtended { get; set; } = true;

    /// <summary>The minimum size of a regular randomized group.</summary>
    public int GroupMin { get; set; } = 1;

    /// <summary>The maximum size of a regular randomized group.</summary>
    public int GroupMax { get; set; } = 3;

    /// <summary>The minimum size of a randomized boss group.</summary>
    public int GroupMinBoss { get; set; } = 3;

    /// <summary>The maximum size of a randomized boss group.</summary>
    public int GroupMaxBoss { get; set; } = 3;

    /// <summary>Whether every character in a boss group is itself a boss.</summary>
    public bool MultiBoss { get; set; } = false;

    /// <summary>Whether generated groups are restricted to vanilla group compositions.</summary>
    public bool VanillaRestrict { get; set; } = false;

    /// <summary>The minimum human count in a regular human boss area.</summary>
    public int HumanBossMin { get; set; } = 3;

    /// <summary>The maximum human count in a regular human boss area.</summary>
    public int HumanBossMax { get; set; } = 3;

    /// <summary>The minimum human count in a boss-group variant of a human boss area.</summary>
    public int HumanBossBossMin { get; set; } = 1;

    /// <summary>The maximum human count in a boss-group variant of a human boss area.</summary>
    public int HumanBossBossMax { get; set; } = 1;

    /// <summary>The minimum number of Pal companions in a regular human boss area.</summary>
    public int HumanBossPalMin { get; set; } = 0;

    /// <summary>The maximum number of Pal companions in a regular human boss area.</summary>
    public int HumanBossPalMax { get; set; } = 3;

    /// <summary>The minimum number of Pal companions in a boss-group variant of a human boss area.</summary>
    public int HumanBossPalBossMin { get; set; } = 0;

    /// <summary>The maximum number of Pal companions in a boss-group variant of a human boss area.</summary>
    public int HumanBossPalBossMax { get; set; } = 0;

    /// <summary>Whether rare Pals receive a minimum-level safeguard.</summary>
    public bool RarityLevelBoost { get; set; } = true;

    /// <summary>The minimum level used for Pals with rarity six or seven.</summary>
    public int Rarity67MinLevel { get; set; } = 18;

    /// <summary>The minimum level used for Pals with rarity eight or higher.</summary>
    public int Rarity8UpMinLevel { get; set; } = 30;

    /// <summary>Whether Pals with rarity nine or higher are restricted to boss groups.</summary>
    public bool Rarity9UpBossOnly { get; set; } = false;

    /// <summary>Whether Pals with rarity eight or higher spawn without other characters.</summary>
    public bool Rarity8UpSolo { get; set; } = true;

    /// <summary>Whether additional rare-Pal group restrictions are applied.</summary>
    public bool Rarity8UpSanity { get; set; } = false;

    /// <summary>Whether a randomized group may contain both humans and Pals.</summary>
    public bool MixHumanAndPal { get; set; } = true;

    /// <summary>Whether non-aggressive humans are kept separate from aggressive characters.</summary>
    public bool SeparateAggroHumans { get; set; } = true;

    /// <summary>Whether boss groups may be added to regular overworld areas.</summary>
    public bool BossesEverywhere { get; set; } = true;

    /// <summary>The weight percentage assigned to bosses added to regular overworld areas.</summary>
    public int BossesEverywhereChance { get; set; } = 5;

    /// <summary>Whether boss groups may be added to regular dungeon areas.</summary>
    public bool BossesEverywhereDungeons { get; set; } = true;

    /// <summary>The weight percentage assigned to bosses added to regular dungeon areas.</summary>
    public int BossesEverywhereDungeonsChance { get; set; } = 5;

    /// <summary>Whether overworld eggs may produce boss Pals.</summary>
    public bool BossEggs { get; set; } = true;

    /// <summary>The weight percentage assigned to boss Pals in overworld eggs.</summary>
    public int BossEggsChance { get; set; } = 5;

    /// <summary>Whether flying Pals are kept in compatible groups.</summary>
    public bool SeparateFlying { get; set; } = true;

    /// <summary>Whether predator group-composition restrictions are applied.</summary>
    public bool PredatorConstraint { get; set; } = true;

    /// <summary>Whether humans may be selected for camp cages.</summary>
    public bool AllowCagedHumans { get; set; } = false;

    /// <summary>Whether the starting area retains a Lamball spawn.</summary>
    public bool StartSheepBall { get; set; } = true;

    /// <summary>Whether solo spawn groups must contain at least one character.</summary>
    public bool MinOneCountSolo { get; set; } = false;

    /// <summary>The allowed outgoing-damage multiplier for regular human spawns.</summary>
    public decimal HumanDamageCap { get; set; } = 1m;

    /// <summary>The allowed outgoing-damage multiplier for human boss spawns.</summary>
    public decimal HumanBossDamageCap { get; set; } = 2m;

    /// <summary>The allowed outgoing-damage multiplier for predator spawns.</summary>
    public decimal PredatorDamageCap { get; set; } = 2m;

    /// <summary>The allowed outgoing-damage multiplier for raid boss spawns.</summary>
    public decimal RaidDamageCap { get; set; } = 9999m;

    /// <summary>Whether group weights are drawn from a uniform numeric range.</summary>
    public bool WeightTypeUniform { get; set; } = false;

    /// <summary>Whether group weights are calculated from character rarity.</summary>
    public bool WeightTypeCustom { get; set; } = true;

    /// <summary>The minimum weight drawn in uniform-weight mode.</summary>
    public int WeightUniformMin { get; set; } = 10;

    /// <summary>The maximum weight drawn in uniform-weight mode.</summary>
    public int WeightUniformMax { get; set; } = 10;

    /// <summary>Rarity-based weights indexed by the game rarity value.</summary>
    public int[] WeightCustom { get; set; } =
        [0, 60, 60, 60, 25, 25, 10, 10, 5, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];

    /// <summary>The method used to combine rarity-based character weights within a group.</summary>
    public string WeightCustomMode { get; set; } = "Weight Minimum";

    /// <summary>The rarity used for regular humans when calculating group weights.</summary>
    public int HumanRarity { get; set; } = 4;

    /// <summary>The rarity used for human bosses when calculating group weights.</summary>
    public int HumanBossRarity { get; set; } = 5;

    /// <summary>The highest Pal rarity permitted in camp cages.</summary>
    public int MaxCageRarity { get; set; } = 9;

    /// <summary>Whether calculated group weights are converted to percentages.</summary>
    public bool WeightAdjustProbability { get; set; } = true;

    /// <summary>Whether boss rarity is prioritized when calculating a mixed group weight.</summary>
    public bool WeightPrioritizeBoss { get; set; } = true;

    /// <summary>The percentage multiplier applied to human group weights.</summary>
    public int HumanWeight { get; set; } = 100;

    /// <summary>The percentage multiplier applied to aggressive human group weights.</summary>
    public int HumanWeightAggro { get; set; } = 25;

    /// <summary>The weight multiplier applied to night-only groups.</summary>
    public decimal WeightNightOnly { get; set; } = 1000m;

    /// <summary>The strategy used to keep spawn weights within the game integer limits.</summary>
    public string OverflowFixMode { get; set; } = "Dynamic (Multi-Pass)";

    /// <summary>Whether normal Pals are permitted in generated groups.</summary>
    public bool SpawnPals { get; set; } = true;

    /// <summary>Whether aggressive humans are permitted in generated groups.</summary>
    public bool SpawnHumans { get; set; } = true;

    /// <summary>Whether regular police characters are permitted in generated groups.</summary>
    public bool SpawnPolice { get; set; } = true;

    /// <summary>Whether guards are permitted in generated groups.</summary>
    public bool SpawnGuards { get; set; } = true;

    /// <summary>Whether item merchants are permitted in generated groups.</summary>
    public bool SpawnTraders { get; set; } = true;

    /// <summary>Whether Pal merchants are permitted in generated groups.</summary>
    public bool SpawnPalTraders { get; set; } = true;

    /// <summary>Whether tower bosses are permitted in generated groups.</summary>
    public bool SpawnTowerBosses { get; set; } = true;

    /// <summary>Whether normal boss variants are permitted in generated groups.</summary>
    public bool SpawnAlphas { get; set; } = true;

    /// <summary>Whether raid bosses are permitted in generated groups.</summary>
    public bool SpawnRaidBosses { get; set; } = true;

    /// <summary>Whether predators are permitted in generated groups.</summary>
    public bool SpawnPredators { get; set; } = true;

    /// <summary>Whether human bosses are permitted in generated groups.</summary>
    public bool SpawnHumanBosses { get; set; } = true;

    /// <summary>Whether humans with special effects are permitted in generated groups.</summary>
    public bool SpawnSpecial { get; set; } = true;

    /// <summary>Whether regular Terraria monsters are permitted in generated groups.</summary>
    public bool SpawnTerraria { get; set; } = true;

    /// <summary>Whether Terraria bosses are permitted in generated groups.</summary>
    public bool SpawnTerrariaBosses { get; set; } = true;

    /// <summary>Whether non-boss tower humans are permitted in generated groups.</summary>
    public bool SpawnTowerHumans { get; set; } = true;

    /// <summary>Whether police-ridden Pal variants are permitted in generated groups.</summary>
    public bool SpawnPolicePals { get; set; } = true;

    /// <summary>Whether additional ordinary humans are permitted in generated groups.</summary>
    public bool SpawnUselessHumans { get; set; } = true;

    /// <summary>Whether generic villagers are permitted in generated groups.</summary>
    public bool SpawnVillagers { get; set; } = true;

    /// <summary>The percentage level multiplier for regular overworld spawns.</summary>
    public int FieldLevel { get; set; } = 100;

    /// <summary>The percentage level multiplier for regular dungeon spawns.</summary>
    public int DungeonLevel { get; set; } = 100;

    /// <summary>The percentage level multiplier for overworld boss spawns.</summary>
    public int FieldBossLevel { get; set; } = 100;

    /// <summary>The percentage level multiplier for dungeon boss spawns.</summary>
    public int DungeonBossLevel { get; set; } = 100;

    /// <summary>The percentage level multiplier for predator spawns.</summary>
    public int PredatorLevel { get; set; } = 100;

    /// <summary>The percentage level multiplier for caged characters.</summary>
    public int CageLevel { get; set; } = 100;

    /// <summary>Whether generated character levels are capped.</summary>
    public bool EnableLevelCap { get; set; } = false;

    /// <summary>The highest permitted generated character level when the cap is enabled.</summary>
    public int LevelCap { get; set; } = 80;

    /// <summary>The relative level scale used for additional characters in boss groups.</summary>
    public int BossAddLevel { get; set; } = 85;

    /// <summary>Whether the additional-character level scale is forced on the affected groups.</summary>
    public bool ForceAddLevel { get; set; } = false;

    /// <summary>The method used to transform minimum and maximum spawn levels.</summary>
    public string LevelScaleMode { get; set; } = "Full";

    /// <summary>The minimum level in random-level scaling mode.</summary>
    public int RandomLevelMin { get; set; } = 1;

    /// <summary>The maximum level in random-level scaling mode.</summary>
    public int RandomLevelMax { get; set; } = 65;

    /// <summary>The minimum base count used before area-specific count multipliers.</summary>
    public int BaseCountMin { get; set; } = 1;

    /// <summary>The maximum base count used before area-specific count multipliers.</summary>
    public int BaseCountMax { get; set; } = 1;

    /// <summary>The percentage count multiplier for regular overworld spawns.</summary>
    public int FieldCount { get; set; } = 100;

    /// <summary>The percentage count multiplier for regular dungeon spawns.</summary>
    public int DungeonCount { get; set; } = 100;

    /// <summary>The percentage count multiplier for overworld boss spawns.</summary>
    public int FieldBossCount { get; set; } = 100;

    /// <summary>The percentage count multiplier for dungeon boss spawns.</summary>
    public int DungeonBossCount { get; set; } = 100;

    /// <summary>The percentage count multiplier for predator spawns.</summary>
    public int PredatorCount { get; set; } = 100;

    /// <summary>The lower count limit applied to regular character entries.</summary>
    public int CountClampMin { get; set; } = 0;

    /// <summary>The upper count limit applied to regular character entries.</summary>
    public int CountClampMax { get; set; } = 5;

    /// <summary>The lower count limit applied to the first character in a regular group.</summary>
    public int CountClampFirstMin { get; set; } = 1;

    /// <summary>The upper count limit applied to the first character in a regular group.</summary>
    public int CountClampFirstMax { get; set; } = 5;

    /// <summary>The lower count limit applied to character entries in boss groups.</summary>
    public int CountClampBossMin { get; set; } = 0;

    /// <summary>The upper count limit applied to character entries in boss groups.</summary>
    public int CountClampBossMax { get; set; } = 5;

    /// <summary>The lower count limit applied to the first character in a boss group.</summary>
    public int CountClampFirstBossMin { get; set; } = 1;

    /// <summary>The upper count limit applied to the first character in a boss group.</summary>
    public int CountClampFirstBossMax { get; set; } = 5;

    /// <summary>Whether nocturnal overworld Pals are restricted to nighttime.</summary>
    public bool NightOnly { get; set; } = true;

    /// <summary>Whether nocturnal dungeon Pals are restricted to nighttime.</summary>
    public bool NightOnlyDungeons { get; set; } = false;

    /// <summary>Whether nocturnal dungeon bosses are restricted to nighttime.</summary>
    public bool NightOnlyDungeonBosses { get; set; } = false;

    /// <summary>Whether nocturnal overworld bosses are restricted to nighttime.</summary>
    public bool NightOnlyBosses { get; set; } = false;

    /// <summary>Whether nocturnal predators are restricted to nighttime.</summary>
    public bool NightOnlyPredators { get; set; } = true;

    /// <summary>The hour component of the overworld egg respawn interval.</summary>
    public int EggRespawnHours { get; set; } = 3;

    /// <summary>The minute component of the overworld egg respawn interval.</summary>
    public int EggRespawnMinutes { get; set; } = 0;

    /// <summary>The second component of the overworld egg respawn interval.</summary>
    public int EggRespawnSeconds { get; set; } = 0;

    /// <summary>The egg respawn time in minutes.</summary>
    public float EggRespawnTime() =>
        Math.Max(0, EggRespawnHours) * 60 + Math.Max(0, EggRespawnMinutes) + Math.Max(0, EggRespawnSeconds) / 60.0f;
}