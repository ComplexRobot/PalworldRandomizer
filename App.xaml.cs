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
            LogException(eventArgs.Exception);
            try
            {
#if DEBUG
                string message = eventArgs.Exception.ToString();
                Console.WriteLine(message[..message.TakeWhile(x => x != '\n').Count()]);
#endif
                MessageBox.Show(eventArgs.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception e)
            {
                LogException(e);
            }
            eventArgs.Handled = true;
        }

        public static void LogException(Exception e)
        {
            try { File.AppendAllText(UAssetData.AppDataPath("error-log.txt"), $"{DateTime.Now}\n{e}\n\n\n"); } catch { }
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
                Randomize.RestoreBackup();
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
            try
            {
                Randomize.SaveBackup();
                if (MainPage.Instance == null)
                {
                    return;
                }
                ConfigData config = SharedWindow.GetConfig();
                config.AutoRestoreTemplate = MainPage.Instance.autoSaveTemplate.IsChecked == true;
                config.OutputLog = MainPage.Instance.outputLog.IsChecked == true;
                SharedWindow.SaveConfig(config);
                if (config.AutoRestoreTemplate)
                {
                    MainPage.Instance.ValidateFormData();
                    MainPage.SaveTemplate(new FormData(MainPage.Instance), UAssetData.AppDataPath(MainPage.AUTO_TEMPLATE_FILENAME));
                }
                else
                {
                    File.Delete(UAssetData.AppDataPath(MainPage.AUTO_TEMPLATE_FILENAME));
                }
            }
            catch (Exception exception)
            {
                LogException(exception);
            }
        }
    }

}
