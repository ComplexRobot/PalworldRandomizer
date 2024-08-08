using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PalworldRandomizer
{
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
        {
            try
            {
                File.AppendAllText(UAssetData.AppDataPath("error-log.txt"), $"{DateTime.Now}\n{eventArgs.Exception}\n\n\n");
            }
            catch
            {
            }
            string message = eventArgs.Exception.ToString();
            int newLine = message.IndexOf('\n');
            message = message[..(newLine == -1 ? message.Length : newLine)];
            Console.WriteLine(message);
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            eventArgs.Handled = true;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            DispatcherOperation dataOperation = Dispatcher.BeginInvoke(() =>
            {
                ResourceManager resourceManager = new(Assembly.GetExecutingAssembly().GetName().Name + ".g", Assembly.GetExecutingAssembly());
                UAssetData.Initialize();
                Data.Initialize(resourceManager);
                Randomize.Initialize();
                Dispatcher.BeginInvoke(() => resourceManager.ReleaseAllResources());
                PalSpawnPage palSpawnpage = new();
                AppWindow palSpawnWindow = new(() => palSpawnpage) { Title = "Pal Spawn Editor" };
                palSpawnpage.ParentWindow = palSpawnWindow;
                palSpawnWindow.Closing += (sender, e) =>
                {
                    palSpawnWindow.HideClean();
                    e.Cancel = true;
                };
            });
            Dispatcher.BeginInvoke(() =>
            {
                MainWindow = new AppWindow(() => new MainPage(dataOperation)) { Title = "Palworld Randomizer", Width = 1280, Height = 720 };
                MainWindow.Closed += (sender, e) => Shutdown();
                ((AppWindow) MainWindow).ShowClean();
            });
            InputManager.Current.PreNotifyInput += SharedWindow.PreNotifyInput;
            InputManager.Current.PostNotifyInput += SharedWindow.PostNotifyInput;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Randomize.SaveBackup();
            if (MainPage.Instance == null)
            {
                return;
            }
            ConfigData config = SharedWindow.GetConfig();
            config.AutoRestoreTemplate = MainPage.Instance.autoSaveTemplate.IsChecked == true;
            SharedWindow.SaveConfig(config);
            if (config.AutoRestoreTemplate)
            {
                MainPage.Instance.ValidateFormData();
                MainPage.SaveTemplate(new FormData(MainPage.Instance), UAssetData.AppDataPath(MainPage.AUTO_TEMPLATE_FILENAME));
            }
            else
            {
                try
                {
                    File.Delete(UAssetData.AppDataPath(MainPage.AUTO_TEMPLATE_FILENAME));
                }
                catch
                {
                }
            }
        }
    }

}
