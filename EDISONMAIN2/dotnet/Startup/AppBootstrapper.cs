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
using static ALVA.EDISONAPI.Database;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using Application = System.Windows.Application;

namespace EDISONMAIN2;

/// <summary>
/// Pojmenované kroky startup workflow vytažené z procedurálního <c>Application_Startup</c>.
/// Cíl: čitelnost a jasné error/cancel exit pointy (login zrušený, licence Refused/Cancelled,
/// connection error) místo skrytých <c>return;</c> uprostřed 100-řádkové metody. App.xaml.cs
/// po refactoru jen vytvoří bootstrapper, zavolá <see cref="Run"/> a ukáže okno (nebo nic
/// pokud Run vrátil null — Shutdown už proběhl uvnitř).
/// </summary>
public class AppBootstrapper(Application application)
{
    /// <summary>
    /// Provede celý bootstrap: load config + LocalPrefs → fetch companies → login →
    /// connect MLRest → acquire licence → vytvoří VM a okno. Vrátí okno připravené k Show,
    /// nebo null pokud workflow selhal (uživatel zrušil login / překročené licence). Při
    /// selhání <see cref="Application.Shutdown()"/> už proběhl uvnitř, caller jen vrátí.
    /// </summary>
    public EdisonMainWindow? Run()
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

        // Pre-login MLRest — potřebné aby GetCompanyList šla zavolat (vlastní fallback Idfa="").
        var loginRest = new MLRest(server, port, "", "");

        string errState = "";
        var companies = GetCompanyList(ref errState, loginRest, false);
        if (!string.IsNullOrEmpty(errState))
            throw new InvalidOperationException(errState);

        var startCompany = CompanySwitcher.PickInitial(companies, LocalPrefs.Current.LastIdfa);
        // Idfa potřebné aby ALVA.SYS.Login.Login uměl resolvovat user's startupForm.
        loginRest.Idfa = startCompany.Id;

        if (!TryLogin(loginRest, out var login))
        {
            application.Shutdown();
            return null;
        }

        var mlRest = new MLRestAdapter(new MLRest(server, port, startCompany.Database, startCompany.Id)
        {
            LoginName = login.UserName,
            FullName  = login.FullName,
            Mlp       = login.Mlp.ToString()
        });

        services.IsAdmin = login.IsAdmin;
        // services.MLRest se musí nastavit už tady — admin override v LicenceVerifier spouští
        // USERS.exe přes WindowNavigator.ShowLegacyAndWait, ten čte connection z EDServices.Current.MLRest.
        services.MLRest = mlRest;
        services.UserPrefs = new CacheUserPrefsService(mlRest);

        var licenceService = new LicenceService(mlRest.Inner);
        var (outcome, activeLicence) = LicenceVerifier.TryAcquireSession(
            mlRest, licenceService.GetActiveLicence, services.IsAdmin, services.PopupDisplayer, services.WindowNavigator);
        if (outcome != LicenceAcquireOutcome.Accepted)
        {
            if (outcome == LicenceAcquireOutcome.Refused)
                services.PopupDisplayer.Info("Nelze se přihlásit, byl překročen počet licencí.");
            application.Shutdown();
            return null;
        }

        LocalPrefs.Current.LastIdfa = mlRest.Idfa;
        LocalPrefs.Current.Save();

        // Server odstraní ACTIVITY zámek (force-logout, výpadek, expirace) → graceful shutdown.
        mlRest.Inner.EdisonActivityFailed += (_, _) =>
            application.Dispatcher.Invoke(() =>
            {
                services.PopupDisplayer.Info("Spojení se serverem bylo přerušeno. Aplikace bude ukončena.");
                application.Shutdown();
            });

        var treeGetter = new ModuleTreeGetter();
        var switcher = new CompanySwitcher(mlRest, licenceService.GetActiveLicence, services.IsAdmin,
                                           services.PopupDisplayer, nav, nav.CloseAll);
        var viewModel = new EdisonMainViewModel(companies, mlRest, treeGetter, licenceService.GetActiveLicence,
                                                activeLicence, switcher, nav.CloseAll);

        var wnd = new EdisonMainWindow { DataContext = viewModel };
        popupDisplayer.DefaultOwner = wnd;
        viewModel.AttachToWindow(wnd);
        // Apply user font preference PŘED Show — overridne EDTheme tokeny v Application.Resources,
        // DynamicResource v EDThemeStyles okamžitě propaguje na všechny ovládací prvky.
        FontService.Apply(LocalPrefs.Current);
        return wnd;
    }

    /// <summary>
    /// Zobrazí login dialog (release) nebo vrátí hardcoded dev login (DEBUG). Po dobu
    /// dialogu přepne <c>ShutdownMode</c> na <c>OnExplicitShutdown</c> aby zavření dialogu
    /// nevypnulo aplikaci (žádné jiné okno ještě není otevřené).
    /// </summary>
    private bool TryLogin(MLRest loginRest, out LoginResult login)
    {
#if DEBUG
        login = new LoginResult(201, "martin.hubata", "martin.hubata", true, "", false, true, "");
        return true;
#else
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var loginWindow = new Windows.LoginWindow.LoginWindow(loginRest);
            if (loginWindow.ShowDialog() != true)
            {
                login = null!;
                return false;
            }
            login = loginWindow.Result!;
            return true;
        }
        finally
        {
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
#endif
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
        r.Register("EDComponentAndViewModelTestWindow", _ => new Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow());
    }
}
