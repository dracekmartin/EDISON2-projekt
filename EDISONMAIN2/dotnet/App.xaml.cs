using System.Windows;
using EDControls.EDViewModel;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Processes;
using EDISONMAIN2.Windows.EdisonMainWindow;

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
            var viewModel = new EdisonMainViewModel(dataService, processService, connParams);

            var wnd = new EdisonMainWindow { DataContext = viewModel };
            wnd.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            new PopupDisplayer().Error(e.Exception);
            e.Handled = true;
        }
    }

}
