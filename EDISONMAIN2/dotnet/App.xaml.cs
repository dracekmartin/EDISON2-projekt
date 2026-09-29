using System.Diagnostics;
using System.IO;
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
        // Značka EDLOADERu, parita s legacy EDISONMAIN (reason=2 = EdisonLaunchReasonEnum.RunEdison).
        internal const string LoaderMarker = "reason=2";
        // Nastaví ji přesměrování pro EDISON.exe: EDLOADER bez značky (build před ní) by vedl do smyčky.
        internal const string RedirectedVariable = "EDISONMAIN2_REDIRECTED";
        private const string LoaderExe = "EDISON.exe";

        private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            TestRunOptions? testRun = null;
#if DEBUG
            // E2E test spouští DEBUG build; Release skrytý vypínač hlášení chyb nedostane.
            testRun = TestRunOptions.Parse(e.Args);
            testRun?.ApplyProcessIsolation();
#endif

            bool redirected = TakeRedirectedFlag();
            // Start mimo EDLOADER (připnutí na lištu, zástupce na exe) by obešel uzel NewShell i rozbalení runtime.
            if (testRun is null && RedirectToLoader(e.Args, redirected))
            {
                Shutdown();
                return;
            }

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

            var window = new AppBootstrapper(this, ConnectionOverride.Parse(e.Args), testRun).Run();
            window?.Show();
        }

        /// <summary>Start přes <c>EDISON.exe</c> místo tohoto procesu. Instance s argumenty připojení
        /// spouští běžící shell, <c>bin\</c> vývojáře EDLOADER nemá.</summary>
        internal static bool ShouldRedirectToLoader(IReadOnlyList<string> args, bool loaderPresent, bool alreadyRedirected) =>
            loaderPresent
            && !alreadyRedirected
            && ConnectionOverride.Parse(args) is null
            && !args.Any(arg => string.Equals(arg.Trim(), LoaderMarker, StringComparison.OrdinalIgnoreCase));

        // Dědily by ji agendy i další instance shellu, pro ty už nic neznamená.
        private static bool TakeRedirectedFlag()
        {
            bool redirected = Environment.GetEnvironmentVariable(RedirectedVariable) is not null;
            Environment.SetEnvironmentVariable(RedirectedVariable, null);
            return redirected;
        }

        private static bool RedirectToLoader(IReadOnlyList<string> args, bool alreadyRedirected)
        {
            var loader = Path.Combine(AppContext.BaseDirectory, LoaderExe);
            if (!ShouldRedirectToLoader(args, File.Exists(loader), alreadyRedirected)) return false;
            try
            {
                var info = new ProcessStartInfo(loader) { WorkingDirectory = AppContext.BaseDirectory };
                info.Environment[RedirectedVariable] = "1";
                Process.Start(info);
                return true;
            }
            catch (Exception)
            {
                // EDLOADER nejde spustit: start jako bez něj, brány před loginem dělá shell sám.
                return false;
            }
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
