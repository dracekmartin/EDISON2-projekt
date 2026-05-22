using System;
using System.Windows;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.LoginWindow;
using ComponentTestWindow = EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var nav = new WindowNavigator();
            RegisterShellWindows(nav.Registry);
            var popupDisplayer = new PopupDisplayer();
            var services = new EDServices(null, nav, popupDisplayer);
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
            var startCompany = CompanySwitcher.PickInitial(companies, LocalPrefs.Current.LastIdfa);

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
            // services.MLRest musí být nastaveno už tady — admin override v LicenceVerifier
            // spouští USERS.exe přes WindowNavigator.ShowLegacyAndWait, ten čte connection
            // kontext z EDServices.Current.MLRest.
            services.MLRest = mlRest;
            services.UserPrefs = new CacheUserPrefsService(mlRest);

            // VerifyLic vytvoří ACTIVITY zámek pro tuto session. Při překročení licencí + admin
            // dostane retry s oknem aktivity uživatelů. Stejná logika i při přepnutí firmy ve VM.
            var licenceService = new LicenceService(mlRest.Inner);
            var (outcome, activeLicence) = LicenceVerifier.TryAcquireSession(
                mlRest, licenceService.GetActiveLicence, services.IsAdmin, services.PopupDisplayer, services.WindowNavigator);
            if (outcome != LicenceAcquireOutcome.Accepted)
            {
                if (outcome == LicenceAcquireOutcome.Refused)
                    services.PopupDisplayer.Info("Nelze se přihlásit, byl překročen počet licencí.");
                Shutdown();
                return;
            }
            // services.ActiveLicence + services.AccessTable nastaví VM v RebuildSessionState
            // (jediný bod přebuildu session-scoped state — initial / switch firmy / manual refresh).

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
            // licenceService.GetActiveLicence čte aktuální stav MLRest, takže po přepnutí firmy
            // dostane VM licenci cílové firmy (potřebné pro VerifyLic se správným CountsLOGIN).
            var switcher = new CompanySwitcher(mlRest, licenceService.GetActiveLicence, services.IsAdmin,
                                               services.PopupDisplayer, nav, nav.CloseAll);
            var viewModel = new EdisonMainViewModel(companies, mlRest, treeGetter, licenceService.GetActiveLicence, activeLicence, switcher, nav.CloseAll);

            var wnd = new EdisonMainWindow { DataContext = viewModel };
            // Shell okno existuje → PopupDisplayer použije ho jako default owner pro popupy.
            popupDisplayer.DefaultOwner = wnd;
            // AttachToWindow před Show — hookne SourceInitialized pro persistence předtím než fire
            // (jinak by okno blikalo z XAML defaults na restored velikost).
            viewModel.AttachToWindow(wnd);
            wnd.Show();
            // Aplikace uloženého fontu (Styl → Použít z LocalPrefs) — Application.MainWindow už je set.
            EdisonMainViewModel.ApplyFontToMainWindow();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            new PopupDisplayer().Error(e.Exception);
            e.Handled = true;
        }

        // Klíče jsou ZÁMĚRNĚ literal stringy, ne nameof(). Pointa registry je decoupling
        // klíče (veřejný kontrakt) od jména třídy (implementační detail) — když se třída
        // přejmenuje, IDE updatuje `new XxxWindow()` ale callsity `Show("...")` ne; literal
        // string v registraci proto zůstane stabilní s callsity. Při budoucím renamu lze
        // dočasně registrovat oba klíče (starý i nový) než se callsity migrují.
        // Konvence: pro shell-internal okna jméno třídy; pro cross-agenda preferovat
        // namespacovaný klíč (např. "MZDY.ZamestnanecEdit") proti kolizím mezi agendami.
        private static void RegisterShellWindows(WindowRegistry r)
        {
            r.Register("EDInputFormTestWindow", _ => new EDInputFormTestWindow());
            r.Register("EDListTestWindow", _ => new EDListTestWindow());
            r.Register("EDComponentAndViewModelTestWindow", _ => new ComponentTestWindow());
        }
    }

}
