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
    }

}
