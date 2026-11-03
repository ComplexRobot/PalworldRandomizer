namespace PalworldRandomizer.Models;

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