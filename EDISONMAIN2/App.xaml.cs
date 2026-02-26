using System.Windows;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Processes;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // TODO (Priority 5): load these from config rather than hardcoding defaults
            var connParams = new ConnectionParams("192.168.16.1", 57770, "martin.hubata", "martin.hubata", 167);
            var dataService = new EdisonMainDataService(connParams);
            var processService = new ProcessLaunchService(connParams);
            var viewModel = new MainViewModel(dataService, processService, connParams);

            var wnd = new EdisonMainWindow { DataContext = viewModel };
            wnd.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("An unhandled exception has happened: " + e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}
