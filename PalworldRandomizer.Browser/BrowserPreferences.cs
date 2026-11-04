using PalworldRandomizer.Serializer;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace PalworldRandomizer.Browser;

/// <summary>Stores a small metadata document in this browser origin, never a game File or Blob.</summary>
public static class BrowserPreferences {
    /// <summary>The origin-private configuration document key.</summary>
    public const string StorageKey = "Palworld-Randomizer.Config.preferences";

    /// <summary>Builds the actual shared store with atomic localStorage replacement and readback.</summary>
    public static AppPreferencesStore CreateStore() => new(() => {
        using var storage = SpawnJSRuntime.Instance.Get<Storage>("localStorage");
        return Task.FromResult(storage.GetItem(StorageKey));
    }, json => {
        using var storage = SpawnJSRuntime.Instance.Get<Storage>("localStorage");
        storage.SetItem(StorageKey, json);
        if (storage.GetItem(StorageKey) != json) {
            throw new IOException("Metadata storage readback failed.");
        }

        return Task.CompletedTask;
    });
}