using System;
using System.Linq;
using System.Windows;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow.Processes;
using EDISONMAIN2.Windows.LoginWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var services = new EDServices(null, new ReflectionWindowNavigator(), new PopupDisplayer());
            EDServices.Initialize(services);

            LocalPrefs.Load();

            var config = SettingsLoader.Load();
            string server = config.Server.Host;
            int port = config.Server.Port;

            var loginRest = new MLRest(server, port, "", "");

            // Fetch companies first — Caché Login method needs non-empty Idfa
            string errState = "";
            var companies = GetCompanyList(ref errState, loginRest, false);
            if (!string.IsNullOrEmpty(errState))
                throw new InvalidOperationException(errState);

            // Pre-fill firmy: priorita 1) lastIdfa z LocalPrefs (uživatel naposled přihlášen),
            // 2) první s IsDefault, 3) fallback companies[0].
            var startCompany = companies.FirstOrDefault(c => c.Id == LocalPrefs.Current.LastIdfa);
            if (startCompany.Id == null) startCompany = companies.FirstOrDefault(c => c.IsDefault);
            if (startCompany.Id == null) startCompany = companies[0];

            // Set Idfa so ALVA.SYS.Login.Login can resolve user's startupForm
            loginRest.Idfa = startCompany.Id;

            LoginResult login;
#if DEBUG
            login = new LoginResult(201, "martin.hubata", "martin.hubata", true, "", false, true, "");
#else
            // Login — prevent auto-shutdown when dialog closes (no other window is open yet)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var loginWindow = new Windows.LoginWindow.LoginWindow(loginRest);
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            login = loginWindow.Result!;
#endif

            // Create the real MLRest connection with company context
            var mlRest = new MLRestAdapter(new MLRest(server, port, startCompany.Database, startCompany.Id)
            {
                LoginName = login.UserName,
                FullName  = login.FullName,
                Mlp       = login.Mlp.ToString()
            });

            services.IsAdmin = login.IsAdmin;

            // VerifyLic vytvoří ACTIVITY zámek pro tuto session. Při překročení licencí + admin
            // dostane retry s oknem aktivity uživatelů. Stejná logika i při přepnutí firmy ve VM.
            var licenceService = new LicenceService(mlRest.Inner);
            var (outcome, activeLicence) = LicenceVerifier.TryAcquireSession(
                mlRest, licenceService.GetActiveLicence, services.IsAdmin, services.PopupDisplayer);
            if (outcome != LicenceAcquireOutcome.Accepted)
            {
                if (outcome == LicenceAcquireOutcome.Refused)
                    services.PopupDisplayer.Info("Nelze se přihlásit, byl překročen počet licencí.");
                Shutdown();
                return;
            }
            services.ActiveLicence = activeLicence;

            LocalPrefs.Current.LastIdfa = mlRest.Idfa;
            LocalPrefs.Current.Save();

            // Server odstraní ACTIVITY zámek (force-logout, výpadek, expirace) → graceful shutdown.
            mlRest.Inner.EdisonActivityFailed += (_, _) =>
                Dispatcher.Invoke(() =>
                {
                    services.PopupDisplayer.Info("Spojení se serverem bylo přerušeno. Aplikace bude ukončena.");
                    Shutdown();
                });

            // Moduly (.exe) sdílejí shellův GUID + CacheLocks=1, aby zánik shell
            // ACTIVITY zámku shodil i moduly. Wiring viz FormToRunGetter v EDISONMAIN2API.
            var treeGetter = new ModuleTreeGetter();
            var processLauncher = new ProcessLauncher(mlRest);
            // licenceService.GetActiveLicence čte aktuální stav MLRest, takže po přepnutí firmy
            // dostane VM licenci cílové firmy (potřebné pro VerifyLic se správným CountsLOGIN).
            var viewModel = new EdisonMainViewModel(companies, mlRest, treeGetter, processLauncher, licenceService.GetActiveLicence);

            var wnd = new EdisonMainWindow { DataContext = viewModel };
            // AttachToWindow před Show — hookne SourceInitialized pro persistence předtím než fire
            // (jinak by okno blikalo z XAML defaults na restored velikost).
            viewModel.AttachToWindow(wnd);
            wnd.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            new PopupDisplayer().Error(e.Exception);
            e.Handled = true;
        }
    }

}
