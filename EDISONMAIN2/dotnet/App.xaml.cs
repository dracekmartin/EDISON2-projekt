using EDControls.EDViewModel;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for App.xaml. Tenký wrapper — celý startup workflow je v
    /// <see cref="AppBootstrapper"/>, App.xaml.cs jen dispatchne a ukáže okno.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            var window = new AppBootstrapper(this).Run();
            window?.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            new PopupDisplayer().Error(e.Exception);
            e.Handled = true;
        }
    }
}
