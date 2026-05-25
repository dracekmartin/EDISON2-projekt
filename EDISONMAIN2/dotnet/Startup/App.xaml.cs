using System;
using System.Linq;
using System.Windows;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.LoginWindow;
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
            var services = new EDServices(null, nav, new PopupDisplayer());
            EDServices.Initialize(services);

            // EDInputForm Color field — register simple WinForms ColorDialog as picker.
            // Pro EDInputFormTestWindow + budoucí EDInputForm use cases v EDISONMAIN2 modulech.
            EDControls.EDInputForm.EDInputForm.PickColorImpl = PickColorWithWinFormsDialog;

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
            // licenceService.GetActiveLicence čte aktuální stav MLRest, takže po přepnutí firmy
            // dostane VM licenci cílové firmy (potřebné pro VerifyLic se správným CountsLOGIN).
            var viewModel = new EdisonMainViewModel(companies, mlRest, treeGetter, licenceService.GetActiveLicence, nav.CloseAll);

            var wnd = new EdisonMainWindow { DataContext = viewModel };
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

        /// <summary>
        /// Simple WinForms ColorDialog wrapper pro EDInputForm Color field.
        /// EDISONMAIN2 nemá recent colors infrastrukturu (na rozdíl od TD) — používáme baseline dialog.
        /// </summary>
        private static string? PickColorWithWinFormsDialog(System.Windows.Window owner, string currentHex)
        {
            var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };

            // Parse #AARRGGBB / #RRGGBB → R/G/B
            string h = (currentHex ?? "").TrimStart('#');
            try
            {
                if (h.Length == 8) h = h.Substring(2);
                if (h.Length == 6)
                {
                    dlg.Color = System.Drawing.Color.FromArgb(
                        Convert.ToByte(h.Substring(0, 2), 16),
                        Convert.ToByte(h.Substring(2, 2), 16),
                        Convert.ToByte(h.Substring(4, 2), 16));
                }
            }
            catch { /* parse error → použij default barvu dialogu */ }

            var handle = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
            if (dlg.ShowDialog(new Win32Window(handle)) != System.Windows.Forms.DialogResult.OK)
                return null;

            var c = dlg.Color;
            return $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private class Win32Window(nint handle) : System.Windows.Forms.IWin32Window
        {
            public nint Handle { get; } = handle;
        }
    }

}
