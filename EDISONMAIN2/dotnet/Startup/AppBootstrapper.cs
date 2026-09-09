using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ALVA.EDISONAPI;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserFonts;
using EDISONMAIN2.LegacyMiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.LoginWindow;
using static ALVA.EDISONAPI.Database;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;

namespace EDISONMAIN2;

/// <summary>
/// Pojmenované kroky startup workflow s jasnými error/cancel exit pointy (login zrušený,
/// licence Refused/Cancelled, connection error); null z <see cref="Run"/> = Shutdown už proběhl.
/// Po návratu bootstrapper dožil - co běží dál, dostane vlastní objekt
/// (<see cref="SessionSwitcher"/>, <see cref="ShellShutdown"/>). Kroky jsou
/// <c>protected virtual</c> kvůli testovatelným subclassům.
/// </summary>
public class AppBootstrapper
{
    private readonly Application _application;
    private readonly ShellShutdown _shutdown;

    public AppBootstrapper(Application application)
    {
        _application = application;
        _shutdown = new ShellShutdown(application);
    }

    /// <summary>Pro test-only subclassy - neinjektuje <see cref="Application"/>, takže
    /// derived class MUSÍ přepsat všechny metody co by ho použily (Shutdown, BuildShellWindow,
    /// WireActivityFailedHandler, Release branch TryLogin).</summary>
    protected AppBootstrapper() : this(null!)
    {
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

        // Sdílený vzhled (font + světlý/tmavý režim) - jedna registrace, čte %APPDATA%\m-line\common
        // a propaguje mezi procesy (TemplateDesigner). Před login dialogem, ať ho téma taky pokryje.
        EDControls.EDAppearance.Initialize(_application);

        // First-launch setup: EDISONini.xml bez připojení → wizard. Zrušení = konec.
        if (!EnsureConfigured())
        {
            Shutdown();
            return null;
        }

        var prep = PrepareLogin(prefs);

        // Auto-login (Windows SSO) má přednost; když neprojde, fallback na dialog.
        if (!TryAutoLogin(prep.LoginRest, prefs, out var login) && !TryLogin(prep.LoginRest, prefs, out login))
        {
            Shutdown();
            return null;
        }

        // Feature #17 (vynucená změna hesla): server přihlásí, ale s příznakem změnit heslo.
        // Nezměněné → zrušení loginu (uživatel nemá jak dovnitř bez změny).
        if (login.ChangePasswordNeeded && !ForcePasswordChange(prep.LoginRest, login.UserName))
        {
            Shutdown();
            return null;
        }

        _startupLoginWindow?.ShowProgress("Ověřuji licenci…");
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

        // Až po SavePostLoginState - LastIdfa musí být uložená i když teď shell skončí updatem.
        _startupLoginWindow?.ShowProgress("Kontroluji aktualizace…");
        var update = CheckForUpdate(session, services);
        if (update.ShouldExit)
        {
            Shutdown();
            return null;
        }

        _startupLoginWindow?.ShowProgress("Spouštím aplikaci…");
        var wnd = BuildShellWindow(prep, session, licence, services, nav, popup, prefs, update.Notice);
        HandOffLoginWindow(wnd);
        return wnd;
    }

    /// <summary>
    /// Předání role hlavního okna z login okna na shell. MainWindow se musí přiřadit explicitně -
    /// implicitní WPF pravidlo ho dalo login oknu (první zkonstruované) a dokud je otevřené,
    /// shell by roli nezískal; pořadí (MainWindow → OnMainWindowClose) chrání před shutdownem
    /// při pozdějším zavření login okna. To se zavře až po prvním vykreslení shellu, aby
    /// v přechodu nebyla vidět plocha.
    /// </summary>
    protected virtual void HandOffLoginWindow(EdisonMainWindow wnd)
    {
        if (_startupLoginWindow is not { } loginWindow) return;
        _application.MainWindow = wnd;
        _application.ShutdownMode = ShutdownMode.OnMainWindowClose;
        wnd.ContentRendered += (_, _) => loginWindow.CloseAfterHandoff();
    }

    /// <summary>
    /// Klientský update check - mandatory, bez user volby (stanice nemá co rozhodovat).
    /// <c>ShouldExit</c> = shell se má ukončit (spawnutý EDUPDATE / rozbitý stav);
    /// <c>Notice</c> = běžící notice-only kontrola release serveru pro status bar.
    /// </summary>
    protected virtual (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) CheckForUpdate(
        SessionContext session, EDServices services)
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
                    updater.LaunchUpdate(settings.SourceDir, result.Version, result.Forced, settings.ForcedStamp);
                }
                catch (Exception ex)
                {
                    services.PopupDisplayer.Info(
                        $"Aktualizaci nelze spustit ({ex.Message}). Kontaktujte Vašeho správce IS.");
                }
                return (true, null);
            case UpdateOutcome.Broken:
                services.PopupDisplayer.Info(result.Message);
                return (true, null);
            default:
                // Notice-only kontrola release serveru M-line běží paralelně se stavbou shellu;
                // výsledek napojí BuildShellWindow do status baru.
                return (false, Task.Run(() => updater.GetReleaseNotice(settings, VerzeInf.Current)));
        }
    }

    /// <summary>Login okno držené otevřené přes post-login kroky (jen Release dialog path);
    /// zavře ho <see cref="HandOffLoginWindow"/> po vykreslení shellu.</summary>
    private Windows.LoginWindow.LoginWindow? _startupLoginWindow = null;   // = null kvůli CS0649 v DEBUG (přiřazení je v #else větvi)

    /// <summary>Sdílený předlogin kontext (config + companies + pre-login MLRest).</summary>
    protected record PreLoginContext(
        MLRest LoginRest,
        IReadOnlyList<CompanyStruct> Companies,
        CompanyStruct StartCompany,
        string Server,
        int Port);

    protected virtual (WindowNavigator Nav, PopupDisplayer Popup, EDServices Services) InitializeServices()
    {
        var nav = new WindowNavigator();
        var popup = new PopupDisplayer();
        var services = new EDServices(null, nav, popup)
        {
            // Pro „Informace o verzi" (panel Podpora, menu Nápověda) - verze assembly je
            // 1.0.0.0, jediná pravdivá je VERZE.INF vedle exe.
            AppDisplayName = "M-line EDISON 2",
            AppVersion = VerzeInf.Current == "" ? "nezjištěna" : VerzeInf.Current
        };
        EDServices.Initialize(services);

        // EDInputForm pole Color - jako picker registrujeme prostý WinForms ColorDialog.
        EDControls.EDInputForm.EDInputForm.PickColorImpl = WinFormsColorPicker.Pick;

        return (nav, popup, services);
    }

    protected virtual ILocalPrefs LoadLocalPrefs() => LocalPrefs.Load();

    /// <summary>
    /// First-launch setup: když <c>EDISONini.xml</c> nemá vyplněné připojení, zobrazí
    /// <see cref="Windows.SetupWindow.SetupWindow"/> (jen připojení - server + REST port).
    /// Vrací false = uživatel zrušil → shutdown. DEBUG má config v repu → wizard se přeskočí.
    /// Greenfield server-init (firma/licence/admin) je mimo scope - zůstává na legacy frmSetup.
    /// </summary>
    protected virtual bool EnsureConfigured()
    {
        if (SettingsLoader.IsConfigured()) return true;

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

        // Pre-login MLRest - potřebné aby GetCompanyList šla zavolat (vlastní fallback Idfa="").
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
    /// Zobrazí login okno (release) nebo vrátí hardcoded dev login (DEBUG). Přepne
    /// <c>ShutdownMode</c> na <c>OnExplicitShutdown</c> aby zavření okna nevypnulo aplikaci;
    /// restore dělá až <see cref="HandOffLoginWindow"/>. Okno po úspěchu zůstává otevřené
    /// (progress podklad pod dobíhajícím bootstrapem) - čekání řeší
    /// <see cref="System.Windows.Threading.DispatcherFrame"/> místo ShowDialog, aby návrat
    /// nevyžadoval zavření okna.
    /// </summary>
    protected virtual bool TryLogin(MLRest loginRest, ILocalPrefs prefs, out LoginResult login)
    {
#if DEBUG
        login = new LoginResult(201, "martin.hubata", "martin.hubata", true, "", false, true, "");
        return true;
#else
        _application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Zapamatované heslo předvyplní jen když patří k naposledy přihlášenému jménu (DPAPI).
        string initialPassword = PasswordProtector.Unprotect(prefs.RememberedPassword, prefs.LastLoginName ?? "") ?? "";

        var loginWindow = new Windows.LoginWindow.LoginWindow(loginRest, prefs.LastLoginName, initialPassword,
                                                              keepOpenOnSuccess: true);
        var frame = new System.Windows.Threading.DispatcherFrame();
        loginWindow.LoginSucceeded += (_, _) => frame.Continue = false;
        loginWindow.Closed += (_, _) => frame.Continue = false;
        loginWindow.Show();
        System.Windows.Threading.Dispatcher.PushFrame(frame);

        if (loginWindow.Result == null)
        {
            // Zrušeno po odmítnutí zapamatovaného hesla → zapomeň ho (jinak příště zase auto-selže).
            if (loginWindow.RememberedPasswordRejected)
            {
                prefs.RememberedPassword = null;
                prefs.Save();
            }
            login = null!;
            // ShutdownMode se nerestoruje - Run() hned volá explicitní Shutdown().
            return false;
        }
        login = loginWindow.Result;
        _startupLoginWindow = loginWindow;

        // Ulož / zapomeň. Při vynucené změně hesla (feature #17) heslo neukládej - za chvíli
        // se změní a zapamatované by bylo neplatné. Persist proběhne v SavePostLoginState.
        prefs.RememberedPassword = loginWindow.RememberPassword && !login.ChangePasswordNeeded
            ? PasswordProtector.Protect(login.UserName, loginWindow.EnteredPassword)
            : null;
        return true;
#endif
    }

    /// <summary>
    /// Vynucená změna hesla (feature #17). Dialog si sám ověří změnu proti serveru a zavře se
    /// jen při úspěchu; vrací true = změněno, false = uživatel zrušil (→ abort loginu).
    /// ShutdownMode toggle jako u <see cref="TryLogin"/> - dialog běží před existencí shell okna.
    /// </summary>
    protected virtual bool ForcePasswordChange(MLRest loginRest, string username)
    {
        // Restore na předchozí mode, ne hardcoded - po dialog loginu je OnExplicitShutdown v platnosti až do handoffu.
        var previousMode = _application.ShutdownMode;
        _application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            return new Windows.LoginWindow.ChangePasswordWindow(new MLRestAdapter(loginRest), username)
                { Owner = _startupLoginWindow }
                .ShowDialog() == true;
        }
        finally
        {
            _application.ShutdownMode = previousMode;
        }
    }

    /// <summary>
    /// Windows SSO auto-login (legacy-style): login na <c>Environment.UserName</c> s prázdným
    /// heslem - server tak přihlásí jen LDAP-AUTO uživatele. Neúspěch (jiný mód / nepovoleno)
    /// → false → fallback na <see cref="TryLogin"/> dialog. Vrací true = přihlášeno bez dialogu.
    /// </summary>
/// <remarks>Zkouší se vždy - server při neprázdném heslu překlopí LDAP-AUTO účet na plný LDAP
/// bind (<c>ALVA.SYS.Login:1138</c>), takže dialogem se takový účet přihlásit nedá.</remarks>
    protected virtual bool TryAutoLogin(MLRest loginRest, ILocalPrefs prefs, out LoginResult login)
    {
        login = null!;
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
        => SessionFactory.Establish(conn, login, services);

    protected virtual (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session, EDServices services)
        => LicenceVerifier.TryAcquireSession(
            session.MLRest, session.LicenceLoader, services.IsAdmin,
            services.PopupDisplayer, services.WindowNavigator);

    protected virtual void SavePostLoginState(ILocalPrefs prefs, string idfa, string? loginName)
    {
        prefs.LastIdfa = idfa;
#if !DEBUG
        // Debug login je syntetický (TryLogin bez dialogu) - nesmí přepsat předvyplnění
        // jména (a tím zneplatnit RememberedPassword) pro ostrý exe na témže stroji.
        prefs.LastLoginName = loginName;
#endif
        prefs.Save();
    }

    /// <summary>
    /// Wire-up server force-logout - když server odstraní ACTIVITY zámek (timeout, výpadek,
    /// admin disconnect), graceful shutdown s informační hláškou.
    /// </summary>
    protected virtual void WireActivityFailedHandler(SessionContext session, EDServices services)
        => BuildActivityFailedWire(services)(session.MLRest);

    /// <summary>
    /// Factory pro delegát který napojí EdisonActivityFailed handler na daný MLRest. Použito
    /// pro initial bootstrap (<see cref="WireActivityFailedHandler"/>) i pro user switch
    /// (<see cref="SessionSwitcher"/> swapuje MLRest a musí napojit handler znovu).
    /// </summary>
    private Action<IMLRest> BuildActivityFailedWire(EDServices services)
    {
        // Lokální kopie polí: delegát žije po celou session, closure na bootstrapper by ho držela taky.
        var app = _application;
        var shutdown = _shutdown;
        return mlRest =>
        {
            if (mlRest is not MLRestAdapter adapter) return;
            adapter.Inner.EdisonActivityFailed += (_, _) =>
                app.Dispatcher.Invoke(() =>
                {
                    services.PopupDisplayer.Info("Spojení se serverem bylo přerušeno. Aplikace bude ukončena.");
                    shutdown.Force();
                });
            // Server per-request version gate (date+full segmenty).
            adapter.Inner.EdisonVersionFailed += (_, _) =>
                app.Dispatcher.Invoke(() =>
                {
                    services.PopupDisplayer.Info("Rozdílná verze Caché a verze Edison, aplikace bude ukončena.");
                    shutdown.Force();
                });
        };
    }

    protected virtual EdisonMainWindow BuildShellWindow(
        PreLoginContext prep, SessionContext session, Lic? licence,
        EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs,
        Task<ReleaseNoticeResult>? releaseNotice)
    {
        var treeLoader = new ModuleTreeLoader();
        var switcher = new CompanySwitcher(services, prefs, nav.TryCloseShellWindows, nav.CloseLegacyModules);
        // Sdílená služba zástupců - strom (VM) i miniaplikace „Plocha" (provider) drží tutéž instanci.
        var shortcuts = new ModuleShortcutsService();
        var miniAppCatalog = MiniAppCatalog.Build([
            new DesktopMiniAppProvider(shortcuts),
            new CoreMiniApps(), new LegacyMiniAppProvider(), new LegacyDashboardMiniAppProvider(),
#if DEBUG
            new DevSampleMiniApps(),
#endif
        ]);
        // Owner login dialogu se čte lazily - MainWindow ukazuje na shell až po HandOffLoginWindow.
        var sessionSwitcher = new SessionSwitcher(services, () => _application.MainWindow,
                                                  BuildActivityFailedWire(services), nav.TryCloseAll);
        var viewModel = new EdisonMainViewModel(prep.Companies, session.MLRest, treeLoader,
                                                licence, switcher, sessionSwitcher.Switch,
                                                prefs, nav.GetActiveModules, nav.CloseAll, _shutdown.Force,
                                                miniAppCatalog, shortcuts, new InfChangelogSource());
        _shutdown.AttachShell(viewModel.SuppressCloseConfirm);

        var wnd = new EdisonMainWindow { DataContext = viewModel };
        popup.DefaultOwner = wnd;
        viewModel.AttachToWindow(wnd);

        // Notice z release serveru (nová verze / potvrzení aktuálnosti) - doběhne typicky
        // až po zobrazení okna.
        releaseNotice?.ContinueWith(t =>
        {
            if (t.Result.Outcome != ReleaseNoticeOutcome.Unknown)
                wnd.Dispatcher.BeginInvoke(() => viewModel.ReleaseNotice = t.Result);
        }, TaskContinuationOptions.OnlyOnRanToCompletion);

        // Cooperative logout - poll běží přes services.MLRest (následuje swap při switch user).
        var shutdown = _shutdown;
        var logoutWatcher = new LogoutWatcher(
            () => services.MLRest,
            seconds => new Windows.LogoutCountdownWindow.LogoutCountdownWindow(seconds, shutdown.Force).Show());
        logoutWatcher.Start();
        wnd.Closed += (_, _) => logoutWatcher.Stop();

        // Graceful close → uvolnit ACTIVITY zámek (jinak visí do server-side idle timeoutu, jelikož
        // je v temp globálu, ne nativním Caché locku). services.MLRest = aktuální (po switch user).
        // Před logoutem flush usage telemetrie - pojistka na záznamy po posledním zavření okna
        // (typicky legacy launche); potřebuje živou session kvůli userID.
        _application.Exit += (_, _) =>
        {
            EDUsageTelemetry.Flush(services.MLRest);
            SessionLogout.Logout(services.MLRest);
        };
        return wnd;
    }

    protected virtual void Shutdown() => _shutdown.Force();
}
