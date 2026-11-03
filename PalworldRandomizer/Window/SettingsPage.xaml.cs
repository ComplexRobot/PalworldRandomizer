using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.Window;

public partial class SettingsPage : Grid {
    /// <summary>The active installation settings page.</summary>
    public static SettingsPage Instance { get; private set; } = null!;

    /// <summary>Creates the installation settings page.</summary>
    public SettingsPage() {
        Instance = this;
        InitializeComponent();
    }

    private void OpenInstallationFolder() {
        OpenFolderDialog openDialog = new() { InitialDirectory = UAssetData.InstallationDirectory };
        if (openDialog.ShowDialog() == true && openDialog.FolderName != string.Empty) {
            installationFolderTextbox.Text = UAssetData.InstallationDirectory = openDialog.FolderName;
            ConfigData config = SharedWindow.GetConfig();
            config.InstallationDirectory = UAssetData.InstallationDirectory;
            SharedWindow.SaveConfig(config);
            ((App)Application.Current).VerifyInstallationFolder((AppWindow)Parent);
        }
    }

    private void InstallationFolderButton_Click(object sender, RoutedEventArgs e) => OpenInstallationFolder();

    private void InstallationFolderTextBox_GotFocus(object sender, RoutedEventArgs e) {
        Keyboard.ClearFocus();
        OpenInstallationFolder();
    }
}