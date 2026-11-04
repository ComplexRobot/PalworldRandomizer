using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using CUE4Parse.Compression;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Resources;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.ViewModels;
using PalworldRandomizer.Window;

namespace PalworldRandomizer;

public partial class App : Application {
    /// <summary>Whether startup has verified the configured installation directory.</summary>
    public static bool InstallationFolderVerified { get; private set; } = false;

    private void Application_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs
    ) {
        LogException(eventArgs.Exception);
        try {
            Exception exception = eventArgs.Exception is TargetInvocationException invocationException
                ? invocationException.InnerException ?? eventArgs.Exception
                : eventArgs.Exception;
#if DEBUG
            string message = exception.ToString();
            Console.WriteLine(message[..message.TakeWhile(x => x != '\n').Count()]);
#endif
            MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        } catch (Exception e) {
            LogException(e);
        }

        eventArgs.Handled = true;
    }

    /// <summary>Appends an exception to the application error log when possible.</summary>
    public static void LogException(Exception e) {
        try {
            File.AppendAllText(UAssetData.AppDataPath("error-log.txt"), $"{DateTime.Now}\n{e}\n\n\n");
        } catch { }
    }

    private void Application_Startup(object sender, StartupEventArgs e) =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            () => {
                SettingsPage settingsPage = new();
                AppWindow settingsWindow = new(() => settingsPage)
                {
                    Title = "Settings",
                    Topmost = true,
                    Width = 800,
                    Height = 200,
                };
                settingsWindow.Closing += (sender, e) => {
                    if (InstallationFolderVerified) {
                        settingsWindow.HideClean();
                        e.Cancel = true;
                    } else {
                        Shutdown();
                    }
                };
                VerifyInstallationFolder(settingsWindow);
            }
        );

    /// <summary>Verifies the installation directory and starts loading game data on success.</summary>
    public void VerifyInstallationFolder(AppWindow settingsWindow) {
        if (InstallationFolderVerified) {
            return;
        }

        Directory.CreateDirectory(UAssetData.AppDataPath());
        ApplyInstallationPath(SharedWindow.GetConfig());
        if (!File.Exists(UAssetData.ArchivePath)) {
            settingsWindow.ShowClean();
            return;
        }

        InstallationFolderVerified = true;
        ((AppWindow)SettingsPage.Instance.Parent)?.HideClean();
        Initialize();
    }

    private void Initialize() {
        StartupCompletion startup = new(Dispatcher.InvokeAsync(
            async () => {
                await using var fileProvider = await InitializeArchiveAsync();
                Data.Initialize(fileProvider);
                Randomize.Initialize();

                fileProvider.PostMount();

                if (Randomize.RestoreBackup() is { } restoreError) {
                    LogException(restoreError);
                }

                PalSpawnPage palSpawnpage = new();
                AppWindow palSpawnWindow = new(() => palSpawnpage) { Title = "Pal Spawn Editor" };
                palSpawnpage.ParentWindow = palSpawnWindow;
                palSpawnWindow.Closing += (sender, e) => {
                    palSpawnWindow.HideClean();
                    e.Cancel = true;
                };
            },
            DispatcherPriority.Background
        ).Task);
        Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            () => {
                MainWindow = new AppWindow(() => new MainPage(startup)) {
                    Title = "Palworld Randomizer",
                    Width = 1280,
                    Height = 800,
                };
                MainWindow.Closed += (sender, e) => Shutdown();
                ((AppWindow)MainWindow).ShowClean();
            }
        );
        InputManager.Current.PreNotifyInput += SharedWindow.PreNotifyInput;
        InputManager.Current.PostNotifyInput += SharedWindow.PostNotifyInput;
    }

    private static void ApplyInstallationPath(ConfigData config) {
        SettingsPage.Instance.installationFolderTextbox.Text = UAssetData.InstallationDirectory =
            config.InstallationDirectory;
        UAssetData.ArchivePath = Path.Combine(
            config.InstallationDirectory, "Pal", "Content", "Paks", "Pal-Windows.pak"
        );
    }

    // Bundled-file preparation is deliberately separate from read-only opening of user-selected archives/mappings.
    private static void PrepareBundledResources(bool replaceAssets) {
        XmlDocument xmlDoc = new();
        xmlDoc.LoadXml(Resource.Resource_resx);
        foreach (XmlNode resource in xmlDoc.DocumentElement!.SelectNodes("data")!.Cast<XmlNode>()) {
            string name = resource.Attributes!["name"]!.InnerText;
            if (name == "Resource.resx") {
                continue;
            }

            string value = resource.SelectSingleNode("value")!.InnerText;
            string filename = UAssetData.AppDataPath(value[..value.IndexOf(';')]);
            Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
            if (replaceAssets || !File.Exists(filename)) {
                File.WriteAllBytes(filename, (byte[])Resource.ResourceManager.GetObject(name)!);
            }
        }

        if (OperatingSystem.IsWindows()) {
            OodleHelper.Initialize(UAssetData.AppDataPath("oo2core_9_win64.dll"));
            ZlibHelper.Initialize(UAssetData.AppDataPath("zlib-ng2.dll"));
        }
    }

    [GeneratedRegex(
        @"^Pal/Content/Others/InventoryItemIcon/Texture/T_itemicon_Weapon_"
            + @"(AssaultRifle_Default1|HandGun_Default|PumpActionShotgun|Launcher_Default|Bat|FragGrenade"
            + @"|FlameThrower_Default|GatlingGun|BowGun|LaserRifle|GuidedMissileLauncher"
            + @"|GrenadeLauncher|Katana)\.uasset$",
        RegexOptions.ExplicitCapture
    )]
    private static partial Regex WeaponIconRegex();

    [GeneratedRegex("skyisland")]
    private static partial Regex TestRegex();

    [GeneratedRegex(@"^Pal/Content/Pal/(Blueprint|DataTable)/.+?\.uasset$", RegexOptions.ExplicitCapture)]
    private static partial Regex TestBlueprintRegex();

    private static async Task<VfsFileProvider> InitializeArchiveAsync() {
        ConfigData config = SharedWindow.GetConfig();
        ApplyInstallationPath(config);
        UAssetData.GameVersion = config.GameVersion;
        bool replaceAssets = config.AssetVersion < UAssetData.ASSET_VERSION && config.AutoReplaceOldFiles;
        config.AssetVersion = UAssetData.ASSET_VERSION;
        PrepareBundledResources(replaceAssets);
        VfsFileProvider fileProvider = await UAssetData.OpenArchiveAsync(
            UAssetData.ArchivePath, UAssetData.AppDataPath("Mappings.usmap"), config.GameVersion
        );
        try {
            config.GameVersion = UAssetData.GameVersion = fileProvider.GameVersion;
            Directory.CreateDirectory(UAssetData.PalIconPath());
            Directory.CreateDirectory(UAssetData.PalIconPath("Human"));
            Directory.CreateDirectory(UAssetData.NpcIconPath());
            Directory.CreateDirectory(UAssetData.WeaponIconPath());
            var threadLock = new Lock();
            Parallel.ForEach(fileProvider.Files, keyValuePair => {
                if (WeaponIconRegex().IsMatch(keyValuePair.Key)) {
                    lock (threadLock) {
                        fileProvider.SaveTexturePng(
                            keyValuePair.Key, UAssetData.WeaponIconPath(), fileProvider.GameVersionUpdated
                        );
                    }
                }
            });
            SharedWindow.SaveConfig(config);
            await fileProvider.WaitForTextureSavesAsync();
            return fileProvider;
        } catch {
            await fileProvider.DisposeAsync();
            throw;
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e) {
        try {
            if (MainPage.Instance?.IsReady != true) {
                return;
            }

            PalSpawnPage.SaveBackup();
            ConfigData config = SharedWindow.GetConfig();
            config.AutoRestoreTemplate = MainPage.Instance.autoSaveTemplate.IsChecked == true;
            config.OutputLog = MainPage.Instance.outputLog.IsChecked == true;
            SharedWindow.SaveConfig(config);
            if (config.AutoRestoreTemplate) {
                MainPage.Instance.ValidateFormData();
                MainPage.SaveTemplate(
                    MainPage.Instance.CaptureFormData(),
                    UAssetData.AppDataPath(MainPage.AUTO_TEMPLATE_FILENAME)
                );
            } else {
                File.Delete(UAssetData.AppDataPath(MainPage.AUTO_TEMPLATE_FILENAME));
            }
        } catch (Exception exception) {
            LogException(exception);
        }
    }
}