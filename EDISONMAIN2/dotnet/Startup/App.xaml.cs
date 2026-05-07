using System;
using System.Diagnostics;
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

            var config = SettingsLoader.Load();
            string server = config.Server.Host;
            int port = config.Server.Port;

            var loginRest = new MLRest(server, port, "", "");

            // Fetch companies first — Caché Login method needs non-empty Idfa
            string errState = "";
            var companies = GetCompanyList(ref errState, loginRest, false);
            if (!string.IsNullOrEmpty(errState))
                throw new InvalidOperationException(errState);

            var defaultCompany = companies.FirstOrDefault(c => c.IsDefault);
            if (defaultCompany.Id == null) defaultCompany = companies[0];

            // Set Idfa so ALVA.SYS.Login.Login can resolve user's startupForm
            loginRest.Idfa = defaultCompany.Id;

            LoginResult login;
#if DEBUG
            login = new LoginResult(167, "martin.hubata", "martin.hubata", true, "", false, true, "");
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
            var mlRest = new MLRestAdapter(new MLRest(server, port, defaultCompany.Database, defaultCompany.Id)
            {
                LoginName = login.UserName,
                FullName  = login.FullName,
                Mlp       = login.Mlp.ToString()
            });

            // VerifyLic also creates the ACTIVITY lock for this session.
            var activeLicence = new LicenceService(mlRest.Inner).GetActiveLicence();
            services.ActiveLicence = activeLicence;
            int countOfLicenses = activeLicence?.CountsLOGIN ?? 0;
            var verifyOutput = mlRest.Inner.Query("ALVA.SYS.Login", "VerifyLic", new object[] { countOfLicenses });
            mlRest.Inner.RestStatus.ThrowIfError();
            int verifyResult = int.Parse(verifyOutput[1]?.ToString() ?? "0");
            if (verifyResult != 1)
            {
                services.PopupDisplayer.Info(verifyResult == -1
                    ? "Nelze se přihlásit, byl překročen počet licencí."
                    : "Při ověření licence nastala neznámá chyba.");
                Shutdown();
                return;
            }

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
            var viewModel = new EdisonMainViewModel(companies, mlRest, treeGetter, processLauncher);

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
