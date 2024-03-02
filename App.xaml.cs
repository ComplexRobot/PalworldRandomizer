using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Threading;

namespace PalworldRandomizer
{
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
        {
            Console.WriteLine(eventArgs.Exception.ToString());
            MessageBox.Show(eventArgs.Exception.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MainWindow = new AppWindow(() => new MainPage(dataOperation)) { Title = "Palworld Randomizer" };
                MainWindow.Closed += (sender, e) => Shutdown();
                ((AppWindow) MainWindow).ShowClean();
            });
        }
    }

}
