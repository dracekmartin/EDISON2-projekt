using EDControls.EDViewModel;
using EDControls.ErrorReporting;

namespace EDISONMAIN2
{
    /// <summary>
    /// Tenký wrapper - celý startup workflow je v
    /// <see cref="AppBootstrapper"/>, App.xaml.cs jen dispatchne a ukáže okno.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            // Chyby mimo dispatcher by jinak zmizely beze stopy - do telemetrie, bez dialogu.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex) ErrorReports.ReportFatal(ex);
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                ErrorReports.ReportBackground(args.Exception);
                args.SetObserved();
            };

            var window = new AppBootstrapper(this, ConnectionOverride.Parse(e.Args)).Run();
            window?.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Dispatcher výjimka žádné okno nenese - aktivní okno dá reportu identitu okna
            // (WindowNamespace/Title) a chybovému dialogu ownera.
            var owner = Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive) ?? MainWindow;
            new PopupDisplayer(owner).Error(e.Exception);
            e.Handled = true;
        }
    }
}
