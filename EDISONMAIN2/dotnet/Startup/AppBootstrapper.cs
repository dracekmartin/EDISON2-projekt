using System;
using System.Collections.Generic;
using System.Windows;
using ALVA.EDISONAPI;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserFonts;
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
///
/// Testovatelnost: každý krok je <c>protected virtual</c>, testovatelný subclass přepíše
/// jen ty které potřebuje (typicky <see cref="InitializeServices"/>, <see cref="PrepareLogin"/>,
/// <see cref="TryLogin"/>, <see cref="EstablishSession"/>, <see cref="AcquireSession"/>,
/// <see cref="WireActivityFailedHandler"/>, <see cref="BuildShellWindow"/>, <see cref="Shutdown"/>).
/// </summary>
public class AppBootstrapper
{
    private readonly Application _application;

    public AppBootstrapper(Application application)
    {
        _application = application;
    }

    /// <summary>Pro test-only subclassy — neinjektuje <see cref="Application"/>, takže
    /// derived class MUSÍ přepsat všechny metody co by ho použily (Shutdown, BuildShellWindow,
    /// WireActivityFailedHandler, Release branch TryLogin).</summary>
    protected AppBootstrapper()
    {
        _application = null!;
    }

    /// <summary>
    /// Provede celý bootstrap. Vrátí okno připravené k Show, nebo null pokud workflow selhal
    /// (uživatel zrušil login / překročené licence). Při selhání <see cref="Shutdown"/> už
    /// proběhl uvnitř, caller jen vrátí.
    /// </summary>
    public EdisonMainWindow? Run()
    {
        var (nav, popup, services) = InitializeServices();
        var prefs = LoadLocalPrefs();

        var prep = PrepareLogin(prefs);

        if (!TryLogin(prep.LoginRest, out var login))
        {
            Shutdown();
            return null;
        }

        var session = EstablishSession(prep, login, services);

        var (outcome, licence) = AcquireSession(session, services);
        if (outcome != LicenceAcquireOutcome.Accepted)
        {
            if (outcome == LicenceAcquireOutcome.Refused)
                services.PopupDisplayer.Info("Nelze se přihlásit, byl překročen počet licencí.");
            Shutdown();
            return null;
        }

        SavePostLoginState(prefs, session.MLRest.Idfa);
        WireActivityFailedHandler(session, services);

        return BuildShellWindow(prep, session, licence, services, nav, popup, prefs);
    }

    /// <summary>Sdílený předlogin kontext (config + companies + pre-login MLRest).</summary>
    protected record PreLoginContext(
        MLRest LoginRest,
        IReadOnlyList<CompanyStruct> Companies,
        CompanyStruct StartCompany,
        string Server,
        int Port);

    /// <summary>Sdílený postlogin kontext (real MLRest + licence loader factory).</summary>
    protected record SessionContext(IMLRest MLRest, LoginResult Login, Func<Lic?> LicenceLoader);

    // ----- Virtual hooks: production defaults, test overrides -----

    protected virtual (WindowNavigator Nav, PopupDisplayer Popup, EDServices Services) InitializeServices()
    {
        var nav = new WindowNavigator();
        RegisterShellWindows(nav.Registry);
        var popup = new PopupDisplayer();
        var services = new EDServices(null, nav, popup);
        EDServices.Initialize(services);
        return (nav, popup, services);
    }

    protected virtual ILocalPrefs LoadLocalPrefs() => LocalPrefs.Load();

    protected virtual PreLoginContext PrepareLogin(ILocalPrefs prefs)
    {
        var config = SettingsLoader.Load();
        string server = config.Server.Host;
        int port = config.Server.Port;

        // Pre-login MLRest — potřebné aby GetCompanyList šla zavolat (vlastní fallback Idfa="").
        var loginRest = new MLRest(server, port, "", "");

        string errState = "";
        var companies = GetCompanyList(ref errState, loginRest, false);
        if (!string.IsNullOrEmpty(errState))
            throw new InvalidOperationException(errState);

        var startCompany = CompanySwitcher.PickInitial(companies, prefs.LastIdfa);
        // Idfa potřebné aby ALVA.SYS.Login.Login uměl resolvovat user's startupForm.
        loginRest.Idfa = startCompany.Id;

        return new PreLoginContext(loginRest, companies, startCompany, server, port);
    }

    /// <summary>
    /// Zobrazí login dialog (release) nebo vrátí hardcoded dev login (DEBUG). Po dobu
    /// dialogu přepne <c>ShutdownMode</c> na <c>OnExplicitShutdown</c> aby zavření dialogu
    /// nevypnulo aplikaci (žádné jiné okno ještě není otevřené).
    /// </summary>
    protected virtual bool TryLogin(MLRest loginRest, out LoginResult login)
    {
#if DEBUG
        login = new LoginResult(201, "martin.hubata", "martin.hubata", true, "", false, true, "");
        return true;
#else
        _application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
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
            _application.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
#endif
    }

    protected virtual SessionContext EstablishSession(PreLoginContext prep, LoginResult login, EDServices services)
    {
        var mlRest = new MLRestAdapter(new MLRest(prep.Server, prep.Port, prep.StartCompany.Database, prep.StartCompany.Id)
        {
            LoginName = login.UserName,
            FullName = login.FullName,
            Mlp = login.Mlp.ToString()
        });

        services.IsAdmin = login.IsAdmin;
        // services.MLRest se musí nastavit už tady — admin override v LicenceVerifier spouští
        // USERS.exe přes WindowNavigator.ShowLegacyAndWait, ten čte connection z EDServices.Current.MLRest.
        services.MLRest = mlRest;
        services.UserPrefs = new CacheUserPrefsService(mlRest);

        var licenceService = new LicenceService(mlRest.Inner);
        return new SessionContext(mlRest, login, licenceService.GetActiveLicence);
    }

    protected virtual (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session, EDServices services)
        => LicenceVerifier.TryAcquireSession(
            session.MLRest, session.LicenceLoader, services.IsAdmin,
            services.PopupDisplayer, services.WindowNavigator);

    protected virtual void SavePostLoginState(ILocalPrefs prefs, string idfa)
    {
        prefs.LastIdfa = idfa;
        prefs.Save();
    }

    /// <summary>
    /// Wire-up server force-logout — když server odstraní ACTIVITY zámek (timeout, výpadek,
    /// admin disconnect), graceful shutdown s informační hláškou.
    /// </summary>
    protected virtual void WireActivityFailedHandler(SessionContext session, EDServices services)
    {
        // Cast je safe v produkci — EstablishSession vždy vrací MLRestAdapter. Test override
        // metody přeskočí, takže mock IMLRest se sem nedostane.
        var adapter = (MLRestAdapter)session.MLRest;
        adapter.Inner.EdisonActivityFailed += (_, _) =>
            _application.Dispatcher.Invoke(() =>
            {
                services.PopupDisplayer.Info("Spojení se serverem bylo přerušeno. Aplikace bude ukončena.");
                _application.Shutdown();
            });
    }

    protected virtual EdisonMainWindow BuildShellWindow(
        PreLoginContext prep, SessionContext session, Lic? licence,
        EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs)
    {
        var treeGetter = new ModuleTreeGetter();
        var switcher = new CompanySwitcher(session.MLRest, session.LicenceLoader, services.IsAdmin,
                                           services.PopupDisplayer, nav, prefs, nav.CloseAll);
        var viewModel = new EdisonMainViewModel(prep.Companies, session.MLRest, treeGetter, session.LicenceLoader,
                                                licence, switcher, prefs, nav.CloseAll);

        var wnd = new EdisonMainWindow { DataContext = viewModel };
        popup.DefaultOwner = wnd;
        viewModel.AttachToWindow(wnd);
        // User font preference: load shared shell+TD storage, apply na EDTheme tokeny PŘED Show
        // (eliminuje flicker), spustit FileSystemWatcher pro cross-process propagaci změny.
        FontService.Initialize(_application);
        return wnd;
    }

    protected virtual void Shutdown() => _application.Shutdown();

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
