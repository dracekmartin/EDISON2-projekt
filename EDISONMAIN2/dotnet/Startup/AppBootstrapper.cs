using System;
using System.Collections.Generic;
using System.Windows;
using ALVA.EDISONAPI;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserFonts;
using EDControls.UserPrefs;
using EDISONMAIN2.LegacyMiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.LoginWindow;
using static ALVA.EDISONAPI.Database;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using Application = System.Windows.Application;

namespace EDISONMAIN2;

/// <summary>
/// Výsledek pokusu o přepnutí uživatele. <see cref="Outcome"/> rozlišuje úspěch, zrušení
/// v login dialogu a selhání licence pro nového uživatele.
/// </summary>
public record SwitchUserOutcome(SwitchUserResult Outcome, Lic? Licence);

public enum SwitchUserResult
{
    /// <summary>Uživatel zrušil login dialog. Shell pokračuje s původním session beze změny.</summary>
    Cancelled,
    /// <summary>Licence pro nového uživatele přesáhla limit (admin retry / non-admin). Caller obvykle ukončí app.</summary>
    LicenceRefused,
    /// <summary>Admin v LicenceVerifier retry loop zvolil Storno. Caller obvykle ukončí app.</summary>
    LicenceCancelled,
    /// <summary>Nový uživatel přihlášen, MLRest swapnut, licence platná.</summary>
    Success,
}

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

        // First-launch setup (feature #5): chybí appsettings.json → wizard. Zrušení = konec.
        if (!EnsureConfigured())
        {
            Shutdown();
            return null;
        }

        var prep = PrepareLogin(prefs);

        // Auto-login (Windows SSO) má přednost; když není zapnutý / neprojde, fallback na dialog.
        if (!TryAutoLogin(prep.LoginRest, prefs, out var login) && !TryLogin(prep.LoginRest, prefs.LastLoginName, out login))
        {
            Shutdown();
            return null;
        }

        var session = EstablishSession(
            new ConnectionInfo(prep.Server, prep.Port, prep.StartCompany.Database, prep.StartCompany.Id),
            login, services);

        var (outcome, licence) = AcquireSession(session, services);
        if (outcome != LicenceAcquireOutcome.Accepted)
        {
            if (outcome == LicenceAcquireOutcome.Refused)
                services.PopupDisplayer.Info("Nelze se přihlásit, byl překročen počet licencí.");
            Shutdown();
            return null;
        }

        SavePostLoginState(prefs, session.MLRest.Idfa, login.UserName);
        WireActivityFailedHandler(session, services);

        // Až po SavePostLoginState — LastIdfa musí být uložená i když teď shell skončí updatem.
        if (CheckForUpdate(session, services))
        {
            Shutdown();
            return null;
        }

        return BuildShellWindow(prep, session, licence, services, nav, popup, prefs);
    }

    /// <summary>
    /// Klientský update check — mandatory, bez user volby (stanice nemá co rozhodovat).
    /// Vrací true = shell se má ukončit (spawnutý EDUPDATE / rozbitý stav). Viz docs/UPDATE_FLOW.md §7.
    /// </summary>
    protected virtual bool CheckForUpdate(SessionContext session, EDServices services)
    {
        var updater = new Updater();
        var settings = updater.LoadSettings(session.MLRest);
        var result = updater.Check(settings);
        switch (result.Outcome)
        {
            case UpdateOutcome.UpdateRequired:
                services.PopupDisplayer.Info(
                    $"K dispozici je nová verze programu [{result.Version}]. Aplikace se nyní zaktualizuje a bude ukončena.");
                try
                {
                    updater.CopyUpdater(settings.SourceDir);
                    updater.LaunchUpdate(settings.SourceDir, result.Version);
                }
                catch (Exception ex)
                {
                    services.PopupDisplayer.Info(
                        $"Aktualizaci nelze spustit ({ex.Message}). Kontaktujte Vašeho správce IS.");
                }
                return true;
            case UpdateOutcome.Broken:
                services.PopupDisplayer.Info(result.Message);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Sdílený předlogin kontext (config + companies + pre-login MLRest).</summary>
    protected record PreLoginContext(
        MLRest LoginRest,
        IReadOnlyList<CompanyStruct> Companies,
        CompanyStruct StartCompany,
        string Server,
        int Port);

    /// <summary>Connection info — vstup do <see cref="EstablishSession"/>. Sdílí initial bootstrap a S2 user switch.</summary>
    protected record ConnectionInfo(string Server, int Port, string Database, string Idfa);

    /// <summary>Sdílený postlogin kontext (real MLRest + licence loader factory).</summary>
    protected record SessionContext(IMLRest MLRest, LoginResult Login, Func<Lic?> LicenceLoader);

    // ----- Virtual hooks: production defaults, test overrides -----

    protected virtual (WindowNavigator Nav, PopupDisplayer Popup, EDServices Services) InitializeServices()
    {
        var nav = new WindowNavigator();
        var popup = new PopupDisplayer();
        var services = new EDServices(null, nav, popup);
        EDServices.Initialize(services);

        // EDInputForm Color field — register simple WinForms ColorDialog as picker.
        // Pro EDInputFormTestWindow + budoucí EDInputForm use cases v EDISONMAIN2 modulech.
        EDControls.EDInputForm.EDInputForm.PickColorImpl = PickColorWithWinFormsDialog;

        return (nav, popup, services);
    }

    /// <summary>
    /// Simple WinForms ColorDialog wrapper pro EDInputForm Color field.
    /// EDISONMAIN2 nemá recent colors infrastrukturu (na rozdíl od TD) — baseline dialog.
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
                    System.Convert.ToByte(h.Substring(0, 2), 16),
                    System.Convert.ToByte(h.Substring(2, 2), 16),
                    System.Convert.ToByte(h.Substring(4, 2), 16));
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

    protected virtual ILocalPrefs LoadLocalPrefs() => LocalPrefs.Load();

    /// <summary>
    /// First-launch setup (feature #5): když chybí <c>appsettings.json</c>, zobrazí
    /// <see cref="Windows.SetupWindow.SetupWindow"/> (jen připojení — server + REST port).
    /// Vrací false = uživatel zrušil → shutdown. DEBUG má config v repu → wizard se přeskočí.
    /// Greenfield server-init (firma/licence/admin) je mimo scope, viz PLAN.md #5.
    /// </summary>
    protected virtual bool EnsureConfigured()
    {
        if (SettingsLoader.Exists()) return true;

        SettingsLoader.Load();   // naplní Current dev defaulty pro předvyplnění polí
        _application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            return new Windows.SetupWindow.SetupWindow().ShowDialog() == true;
        }
        finally
        {
            _application.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
    }

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
    protected virtual bool TryLogin(MLRest loginRest, string? lastLoginName, out LoginResult login)
    {
#if DEBUG
        login = new LoginResult(201, "martin.hubata", "martin.hubata", true, "", false, true, "");
        return true;
#else
        _application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var loginWindow = new Windows.LoginWindow.LoginWindow(loginRest, lastLoginName);
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

    /// <summary>
    /// Windows SSO auto-login (feature #7, legacy-style): jen když je na stanici zapnutý
    /// <see cref="ILocalPrefs.TryWindowsAutoLogin"/>, zkusí login na <c>Environment.UserName</c>
    /// s prázdným heslem — server přihlásí jen LDAP-AUTO uživatele. Neúspěch (jiný mód / nepovoleno)
    /// → false → fallback na <see cref="TryLogin"/> dialog. Vrací true = přihlášeno bez dialogu.
    /// </summary>
    protected virtual bool TryAutoLogin(MLRest loginRest, ILocalPrefs prefs, out LoginResult login)
    {
        login = null!;
        if (!prefs.TryWindowsAutoLogin) return false;
        try
        {
            login = Windows.LoginWindow.UserAuthenticator.Login(new MLRestAdapter(loginRest), Environment.UserName, "");
            return true;
        }
        catch (Windows.LoginWindow.LoginException)
        {
            return false;   // ne LDAP-AUTO / nepovoleno → dialog
        }
    }

    protected virtual SessionContext EstablishSession(ConnectionInfo conn, LoginResult login, EDServices services)
    {
        var inner = new MLRest(conn.Server, conn.Port, conn.Database, conn.Idfa)
        {
            LoginName = login.UserName,
            FullName = login.FullName,
            Mlp = login.Mlp.ToString(),
            // Aktivuje server-side per-request version gate (prázdná verze = gate neběží).
            EdisonVersion = VerzeInf.Current
        };
#if DEBUG
        // Deterministický GUID v dev: Stop Debugging (hard-kill) ACTIVITY zámek neuvolní, ale
        // příští spuštění reusne stejný klíč (idfa~ip~guid) → zámek se přepíše, neakumuluje se
        // (a neroste falešně licenční počet). V produkci náhodný GUID per session (default).
        inner.GUID = $"DEV-{Environment.MachineName}-{Environment.UserName}";
#endif
        var mlRest = new MLRestAdapter(inner);

        services.IsAdmin = login.IsAdmin;
        // services.MLRest se musí nastavit už tady — admin override v LicenceVerifier spouští
        // USERS.exe přes WindowNavigator.ShowLegacyAndWait, ten čte connection z EDServices.Current.MLRest.
        services.MLRest = mlRest;
        services.UserPrefs = new CacheUserPrefsService(mlRest);

        var licenceService = new LicenceService(mlRest.Inner);
        services.LicenceLoader = licenceService.GetActiveLicence;
        return new SessionContext(mlRest, login, licenceService.GetActiveLicence);
    }

    protected virtual (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session, EDServices services)
        => LicenceVerifier.TryAcquireSession(
            session.MLRest, session.LicenceLoader, services.IsAdmin,
            services.PopupDisplayer, services.WindowNavigator);

    protected virtual void SavePostLoginState(ILocalPrefs prefs, string idfa, string? loginName)
    {
        prefs.LastIdfa = idfa;
        prefs.LastLoginName = loginName;
        prefs.Save();
    }

    /// <summary>
    /// Wire-up server force-logout — když server odstraní ACTIVITY zámek (timeout, výpadek,
    /// admin disconnect), graceful shutdown s informační hláškou.
    /// </summary>
    protected virtual void WireActivityFailedHandler(SessionContext session, EDServices services)
        => BuildActivityFailedWire(services)(session.MLRest);

    /// <summary>
    /// Factory pro delegát který napojí EdisonActivityFailed handler na daný MLRest. Použito
    /// pro initial bootstrap (<see cref="WireActivityFailedHandler"/>) i pro S2 user switch
    /// (<see cref="SwitchUser"/> swapuje MLRest a musí napojit handler znovu).
    /// </summary>
    private Action<IMLRest> BuildActivityFailedWire(EDServices services) => mlRest =>
    {
        if (mlRest is not MLRestAdapter adapter) return;
        adapter.Inner.EdisonActivityFailed += (_, _) =>
            _application.Dispatcher.Invoke(() =>
            {
                services.PopupDisplayer.Info("Spojení se serverem bylo přerušeno. Aplikace bude ukončena.");
                _application.Shutdown();
            });
        // Server per-request version gate (date+full segmenty) — viz docs/UPDATE_FLOW.md §5.
        adapter.Inner.EdisonVersionFailed += (_, _) =>
            _application.Dispatcher.Invoke(() =>
            {
                services.PopupDisplayer.Info("Rozdílná verze Caché a verze Edison, aplikace bude ukončena.");
                _application.Shutdown();
            });
    };

    /// <summary>Login dialog pro S2 user switch — owner je shell okno, prázdné pole (uživatel zadá od začátku).</summary>
    protected virtual LoginResult? PromptLoginForReauth(MLRest loginRest)
    {
        var loginWindow = new Windows.LoginWindow.LoginWindow(loginRest)
        {
            Owner = _application.MainWindow,
        };
        return loginWindow.ShowDialog() == true ? loginWindow.Result : null;
    }

    /// <summary>
    /// Reauth flow ("Přihlásit jiného uživatele"): prompt → close all → reuse <see cref="EstablishSession"/>
    /// (drží UserPrefs cache + LicenceService synchronně s MLRest) → wire activity → acquire licence.
    /// Při zrušení v login dialogu nic neudělá; při selhání licence MLRest už je swapnut, caller
    /// obvykle volí shutdown (rollback nelze: closeAllWindows už proběhlo).
    /// </summary>
    public SwitchUserOutcome SwitchUser(EDServices services, Action closeAllWindows)
    {
        var current = ((MLRestAdapter)services.MLRest!).Inner;
        var login = PromptLoginForReauth(current);
        if (login == null) return new(SwitchUserResult.Cancelled, null);

        closeAllWindows();

        // Odhlásit starého uživatele — jinak jeho ACTIVITY zámek (starý GUID) drží licenční slot
        // do server-side timeoutu. Až po confirmu (login != null), před swapem session.
        SessionLogout.Logout(services.MLRest);

        var session = EstablishSession(
            new ConnectionInfo(current.Server, current.Port, current.DataNamespace, current.Idfa),
            login, services);
        WireActivityFailedHandler(session, services);

        var (outcome, licence) = AcquireSession(session, services);
        return outcome switch
        {
            LicenceAcquireOutcome.Accepted => new(SwitchUserResult.Success, licence),
            LicenceAcquireOutcome.Refused => new(SwitchUserResult.LicenceRefused, null),
            _ => new(SwitchUserResult.LicenceCancelled, null),
        };
    }

    protected virtual EdisonMainWindow BuildShellWindow(
        PreLoginContext prep, SessionContext session, Lic? licence,
        EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs)
    {
        var treeGetter = new ModuleTreeGetter();
        var switcher = new CompanySwitcher(services, prefs, nav.CloseAll);
        var miniAppCatalog = MiniAppCatalog.Build([new CoreMiniApps(), new LegacyMiniAppProvider(), new LegacyDashboardMiniAppProvider(), new DevSampleMiniApps()]);
        var viewModel = new EdisonMainViewModel(prep.Companies, session.MLRest, treeGetter,
                                                licence, switcher, () => SwitchUser(services, nav.CloseAll),
                                                prefs, nav.GetActiveModules, nav.CloseAll, miniAppCatalog);

        var wnd = new EdisonMainWindow { DataContext = viewModel };
        popup.DefaultOwner = wnd;
        viewModel.AttachToWindow(wnd);
        // User font preference: load shared shell+TD storage, apply na EDTheme tokeny PŘED Show
        // (eliminuje flicker), spustit FileSystemWatcher pro cross-process propagaci změny.
        FontService.Initialize(_application);

        // Cooperative logout — poll běží přes services.MLRest (následuje swap při switch user).
        var logoutWatcher = new LogoutWatcher(() => services.MLRest, ShowLogoutCountdown);
        logoutWatcher.Start();
        wnd.Closed += (_, _) => logoutWatcher.Stop();

        // Graceful close → uvolnit ACTIVITY zámek (jinak visí do server-side idle timeoutu, jelikož
        // je v temp globálu, ne nativním Caché locku). services.MLRest = aktuální (po switch user).
        _application.Exit += (_, _) => SessionLogout.Logout(services.MLRest);
        return wnd;
    }

    private void ShowLogoutCountdown(int seconds) =>
        new Windows.LogoutCountdownWindow.LogoutCountdownWindow(seconds, _application.Shutdown).Show();

    protected virtual void Shutdown() => _application.Shutdown();
}
