using SpawnDev.SpawnJS;

namespace PalworldRandomizer.Browser;

/// <summary>Small worker-to-host summary of one bounded game-data generation run.</summary>
/// <remarks>
/// The ZIP bytes are the only large member. Every other member is a count, byte total or diagnostic string so the
/// UI never receives the multi-hundred-megabyte PalSchema document set itself.
/// </remarks>
[SpawnJSPoco]
public sealed class BrowserGenerationReport {
    /// <summary>Whether generation completed and produced a readable ZIP.</summary>
    public bool Success { get; set; }

    /// <summary>A stable stage key when generation failed.</summary>
    public string? ErrorKey { get; set; }

    /// <summary>The original failure message when generation failed.</summary>
    public string? ErrorDetail { get; set; }

    /// <summary>The game version read from the PAK's INI, or the caller's fallback.</summary>
    public string GameVersion { get; set; } = "0.0.0.0";

    /// <summary>Mounted PAK entries.</summary>
    public int ArchiveEntries { get; set; }

    /// <summary>Eligible character rows loaded by <c>Data.Initialize</c>.</summary>
    public int PalCount { get; set; }

    /// <summary>Vanilla spawn areas loaded from the PAK.</summary>
    public int VanillaAreaCount { get; set; }

    /// <summary>Generated spawn areas after randomization.</summary>
    public int GeneratedAreaCount { get; set; }

    /// <summary>Areas whose generated content differs from vanilla.</summary>
    public int ChangedAreaCount { get; set; }

    /// <summary>PalSchema documents inside the ZIP.</summary>
    public int SchemaDocumentCount { get; set; }

    /// <summary>Uncompressed bytes of all PalSchema documents.</summary>
    public long SchemaBytes { get; set; }

    /// <summary>Compressed ZIP length.</summary>
    public long ZipBytes { get; set; }

    /// <summary>Total bytes requested from the browser PAK source.</summary>
    public long PakBytesRead { get; set; }

    /// <summary>Number of bounded PAK reads.</summary>
    public int PakReads { get; set; }

    /// <summary>Largest single PAK read request, never above the bounded block size.</summary>
    public int MaxPakReadRequest { get; set; }

    /// <summary>Total bytes requested from the browser USMAP source.</summary>
    public long MappingBytesRead { get; set; }

    /// <summary>Number of bounded USMAP reads.</summary>
    public int MappingReads { get; set; }

    /// <summary>Largest single USMAP read request.</summary>
    public int MaxMappingReadRequest { get; set; }

    /// <summary>Wall-clock duration of the worker call in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>The complete ZIP bytes; empty on failure.</summary>
    public byte[] ZipData { get; set; } = [];
}
