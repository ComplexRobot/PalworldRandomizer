using PalworldRandomizer.Serializer;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace PalworldRandomizer.Browser;

/// <summary>Stores the small Default template documents in this browser origin, never a game File or Blob.</summary>
/// <remarks>
/// The key is derived from the actual Templates/Default.yaml name rather than a display-name indirection. The legacy
/// Default.json key is separate so an existing JSON document is never overwritten by YAML saves.
/// </remarks>
public static class BrowserTemplateStore {
    /// <summary>The origin-private YAML document key for Templates/Default.yaml.</summary>
    public const string YamlStorageKey = "Palworld-Randomizer.Templates/Default.yaml";

    /// <summary>The origin-private legacy JSON document key for Templates/Default.json.</summary>
    public const string LegacyStorageKey = "Palworld-Randomizer.Templates/Default.json";

    /// <summary>Builds the actual shared template store with localStorage replacement and readback.</summary>
    public static TemplateStore CreateStore() => new(
        () => Read(YamlStorageKey),
        () => Read(LegacyStorageKey),
        text => Write(YamlStorageKey, text));

    private static Task<string?> Read(string key) {
        using var storage = SpawnJSRuntime.Instance.Get<Storage>("localStorage");
        return Task.FromResult(storage.GetItem(key));
    }

    private static Task Write(string key, string text) {
        using var storage = SpawnJSRuntime.Instance.Get<Storage>("localStorage");
        storage.SetItem(key, text);
        if (storage.GetItem(key) != text) {
            throw new IOException("Template storage readback failed.");
        }

        return Task.CompletedTask;
    }
}