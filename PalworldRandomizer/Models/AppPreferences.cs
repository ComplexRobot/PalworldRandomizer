using System.Globalization;
using System.Text.Json.Serialization;

namespace PalworldRandomizer.Models;

/// <summary>Application preferences, separate from the original randomizer configuration.</summary>
public sealed record AppPreferences {
    /// <summary>The installed interface locale.</summary>
    public string Locale { get; init; } = "en-US";

    /// <summary>The saved choice, never a temporary rendering fallback.</summary>
    public string Renderer { get; init; } = "gpu-preferred";

    /// <summary>Last selected native path or browser display name, never the archive contents.</summary>
    public string PakSelection { get; init; } = "";

    /// <summary>Last selected native path or browser display name.</summary>
    public string MappingSelection { get; init; } = "";

    /// <summary>Whether an explicit pair passed compatibility validation.</summary>
    public bool SetupCompleted { get; init; }

    /// <summary>Last validated game version; zero means not yet known.</summary>
    public string LastGameVersion { get; init; } = "0.0.0.0";

    /// <summary>Null follows the locale until manually edited.</summary>
    public string? DatePattern { get; init; }

    /// <summary>Null follows the locale until manually edited.</summary>
    public string? TimePattern { get; init; }

    /// <summary>Uses hyphens instead of the default slashes in displayed dates.</summary>
    public bool UseHyphens { get; init; }

    /// <summary>The editable timestamp used by future filename generation consumers.</summary>
    public string FilenamePattern { get; init; } = "MM-dd-yy-HH-mm-ss";

    /// <summary>The effective display date pattern, without persisting a derived locale default.</summary>
    [JsonIgnore]
    public string EffectiveDatePattern => (DatePattern ?? CultureInfo.GetCultureInfo(Locale)
        .DateTimeFormat.ShortDatePattern).Replace("-", "/").Replace("/", UseHyphens ? "-" : "/");

    /// <summary>The effective display time pattern.</summary>
    [JsonIgnore]
    public string EffectiveTimePattern => TimePattern
        ?? CultureInfo.GetCultureInfo(Locale).DateTimeFormat.ShortTimePattern;
}