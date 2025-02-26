using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Input;

namespace PalworldRandomizer
{
    public partial class SettingsPage : Grid
    {
        public static SettingsPage Instance { get; private set; } = null!;

        public SettingsPage()
        {
            Instance = this;
            InitializeComponent();
        }

        private void OpenInstallationFolder()
        {
            OpenFolderDialog openDialog = new()
            {
                InitialDirectory = UAssetData.InstallationDirectory
            };
            if (openDialog.ShowDialog() == true && openDialog.FolderName != string.Empty)
            {
                installationFolderTextbox.Text = UAssetData.InstallationDirectory = openDialog.FolderName;
                ConfigData config = SharedWindow.GetConfig();
                config.InstallationDirectory = UAssetData.InstallationDirectory;
                SharedWindow.SaveConfig(config);
                ((App)Application.Current).VerifyInstallationFolder((AppWindow)Parent);
            }
        }

        private void InstallationFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenInstallationFolder();
        }

        private void InstallationFolderTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            OpenInstallationFolder();
        }
    }
}
