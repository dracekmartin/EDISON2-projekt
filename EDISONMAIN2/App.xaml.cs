using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            EdisonMainWindow wnd = new EdisonMainWindow();
            wnd.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("An unhandled exception has happened: " + e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}
