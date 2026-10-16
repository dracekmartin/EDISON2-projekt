using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Automation;
using ALVA.EDISONAPI;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserFonts;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.LegacyMiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.LoginWindow;
using EDISONMAIN2.Windows.SetupWindow;
using static ALVA.EDISONAPI.Database;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;

namespace EDISONMAIN2;

/// <summary>
/// Pojmenované kroky startup workflow s jasnými error/cancel exit pointy (login zrušený,
/// licence Refused/Cancelled, connection error); null z <see cref="Run"/> = Shutdown už proběhl.
/// Po návratu bootstrapper dožil - co běží dál, dostane vlastní objekt
/// (<see cref="SessionSwitcher"/>, <see cref="ShellShutdown"/>). WPF stranu (okna před shellem,
/// dispečer, konec aplikace) dělá <see cref="IStartupHost"/>; <c>protected virtual</c> zůstávají
/// jen hranice ven (server, soubory, registr) a stavba shellu.
/// </summary>
public class AppBootstrapper
{
    private readonly IStartupHost _host;
    private readonly ShellShutdown _shutdown;
    private readonly ConnectionOverride? _connectionOverride;
    private readonly StartupOptions _options;

    public AppBootstrapper(Application application, ConnectionOverride? connectionOverride,
                           StartupOptions options)
        : this(new ApplicationStartupHost(application), connectionOverride, options)
    {
    }

    internal AppBootstrapper(IStartupHost host, ConnectionOverride? connectionOverride, StartupOptions options)
    {
        _host = host;
        _shutdown = new ShellShutdown(host.Shutdown);
        _connectionOverride = connectionOverride;
        _options = options;
    }

    /// <summary>
    /// Provede celý bootstrap. Vrátí okno připravené k Show, nebo null pokud workflow selhal
    /// (uživatel zrušil login / překročené licence / výjimka). Při selhání <see cref="Shutdown"/>
    /// už proběhl uvnitř, caller jen vrátí.
    /// </summary>
    public EdisonMainWindow? Run()
    {
        try
        {
            return RunCore();
        }
        catch (Exception ex)
        {
            // Bez explicitního Shutdown by výjimka skončila v DispatcherUnhandledException (Handled)
            // a aplikace žila dál s OnExplicitShutdown a login oknem, které během progress fáze
            // každé zavření ruší - proces by šel ukončit jen Task Managerem.
            _host.ReportFailure(ex);
            Shutdown();
            return null;
        }
    }

    private EdisonMainWindow? RunCore()
    {
        // Výchozí OnLastWindowClose by aplikaci ukončil zavřením jediného okna před shellem:
        // wizardu, login okna nebo popupu při auto-loginu.
        _host.HoldShutdown();
        var (nav, popup, services) = InitializeServices();
        var prefs = LoadLocalPrefs();

        // Před login dialogem, ať ho téma taky pokryje.
        _host.InitializeAppearance();

        // First-launch setup: EDISONini.xml bez připojení → wizard. Zrušení = konec.
        if (!EnsureConfigured(services))
        {
            Shutdown();
            return null;
        }

        var prep = PrepareLogin(prefs);

        // Brány legacy EDLOADERu (zákaz přihlašování, běžící instalace na serveru): start, který App
        // nepřesměruje přes EDLOADER (značka reason=2, argumenty připojení), je obchází.
        if (!PassesPreLoginGates(prep.LoginRest, services))
        {
            Shutdown();
            return null;
        }

        // Auto-login (Windows SSO) má přednost; když neprojde, fallback na dialog. E2E běh ho vynechá:
        // úspěšný Login pod Windows jménem nuluje uživatelské flagy a identitu testu dává DevLogin.
        var login = (_options.TestRun is null ? TryAutoLogin(prep.LoginRest) : null)
                    ?? TryLogin(prep.LoginRest, prefs, services);
        if (login is null)
        {
            Shutdown();
            return null;
        }

        // Předloginový seznam je úplný (server bez uživatele nefiltruje); povolené firmy uživatele vybere opener.
        var opener = CreateSessionOpener(services);
        var opened = opener.Open(login,
            new ConnectionInfo(prep.Server, prep.Port, prep.StartCompany.Database, prep.StartCompany.Id),
            PreferredIdfa(prefs, prep.Companies, prep.Server, prep.Port),
            new SessionOpenSteps
            {
                ChangePassword = l => _host.ShowChangePassword(prep.LoginRest, l.UserName),
                BeforeEstablish = () =>
                {
                    ShowStartupProgress("Ověřuji licenci…");
                    return true;
                },
                AfterAccepted = session =>
                {
                    // Před update checkem - firma serveru musí být uložená i když teď shell skončí updatem.
                    SavePostLoginState(prefs, prep.Server, prep.Port, session.MLRest.Idfa, login.UserName);
                    // Od Accepted existuje ACTIVITY zámek - logout musí proběhnout při každém ukončení odteď,
                    // včetně exitu kvůli updatu a výjimky níž, ne až od vzniku shellu.
                    WireSessionLogout(services);
                    // Popup před shellem jako hlášky licence; nastavuje ho jen E2E přepínač.
                    if (_options.PreShellNotice is { } notice) services.PopupDisplayer.Info(notice);
                    ShowStartupProgress("Kontroluji aktualizace…");
                },
                StartsReleaseNotice = true,
            });
        if (opened.Outcome != SessionOpenOutcome.Opened)
        {
            if (opened.Outcome == SessionOpenOutcome.LicenceRefused)
                services.PopupDisplayer.Info("Nelze se přihlásit, byl překročen počet licencí.");
            Shutdown();
            return null;
        }
        prep = prep with { Companies = opened.Companies! };

        ShowStartupProgress("Spouštím aplikaci…");
        // Force logout nebo version gate během startu už aplikaci ukončuje. Postavený shell by WPF při ukončení
        // zavíral s potvrzením, které nejde ukázat, a odhlášení by neproběhlo.
        if (_host.ShutdownRequested) return null;
        var wnd = BuildShellWindow(prep, services, nav, popup, prefs, opener, opened.ReleaseNotice);
        return _host.HandOff(wnd) ? wnd : null;
    }

    /// <summary>Cesta po loginu sdílená s přihlášením jiného uživatele; tatáž instance jde do <see cref="SessionSwitcher"/>.</summary>
    protected virtual SessionOpener CreateSessionOpener(EDServices services) =>
        new(services, _options, BuildActivityFailedWire(services));

    private void ShowStartupProgress(string message)
    {
        if (_startupLogin is null) return;
        _startupLogin.ShowProgress(message);
        // Další krok startupu zablokuje UI thread - progress login okna (text, zašedlá pole) vykreslit hned.
        _host.RenderNow();
    }

    // Jen pro brány před loginem; update check po loginu má SessionOpener.
    protected virtual Updater CreateUpdater() => new();

    /// <summary>VM login okna dialog cesty (ne DevLogin): progress post-login kroků.</summary>
    private LoginViewModel? _startupLogin;

    /// <summary>Sdílený předlogin kontext (config + companies + pre-login MLRest). Po loginu
    /// <c>Companies</c> nahradí povolené firmy uživatele.</summary>
    protected record PreLoginContext(
        IMLRest LoginRest,
        IReadOnlyList<CompanyStruct> Companies,
        CompanyStruct StartCompany,
        string Server,
        int Port);

    protected virtual (WindowNavigator Nav, PopupDisplayer Popup, EDServices Services) InitializeServices()
    {
        // Navigator vzniká před službami, které ho dostávají konstruktorem; delegát se volá až při spuštění modulu.
        EDServices? services = null;
        var nav = new WindowNavigator(() => services!);
        var popup = new PopupDisplayer();
        services = CreateServices(nav, popup, VerzeInf.Current);
        EDServices.Initialize(services);

        // EDInputForm pole Color - jako picker registrujeme prostý WinForms ColorDialog.
        EDControls.EDInputForm.EDInputForm.PickColorImpl = WinFormsColorPicker.Pick;

        return (nav, popup, services);
    }

    /// <summary>Služby shellu s identitou aplikace (hlášení chyb, „Informace o verzi"). <paramref name="installedVersion"/>
    /// je obsah VERZE.INF, "" = soubor chybí.</summary>
    internal static EDServices CreateServices(WindowNavigator nav, PopupDisplayer popup, string installedVersion) =>
        new(null, nav, popup)
        {
            // Součást uložených formID (ALVA.LIBRARY.Form): jiný prefix by uživatelům zahodil uložený stav oken.
            FormIdPrefix = "EDISON2",
            AppDisplayName = "M-line EDISON 2",
            // Bez VERZE.INF vezme AppIdentity verzi exe (s hashem buildu), řekne víc než pevný text.
            AppVersion = installedVersion == "" ? null : installedVersion,
        };

    protected virtual ILocalPrefs LoadLocalPrefs() =>
        _options.TestRun is not null ? new TransientLocalPrefs() : LocalPrefs.Load();

    /// <summary>
    /// First-launch setup: když <c>EDISONini.xml</c> nemá vyplněné připojení, zobrazí
    /// <see cref="Windows.SetupWindow.SetupWindow"/> (jen připojení - server + REST port).
    /// Vrací false = uživatel zrušil → shutdown. DEBUG má config v repu → wizard se přeskočí.
    /// Greenfield server-init (firma/licence/admin) je mimo scope - zůstává na legacy frmSetup.
    /// </summary>
    private bool EnsureConfigured(EDServices services) => IsConfigured() || _host.ShowSetup(new SetupWizardViewModel(
        services, SettingsLoader.Load(_options.UseDevDefaults, SettingsFilePath).Server,
        PreLoginConnection.Probe, PreLoginConnection.LoadDatabases,
        settings => new SettingsLoader { Server = settings }.Save(SettingsFilePath)));

    /// <summary><c>EDISONini.xml</c> stanice; testy podstrčí vlastní, bin adresář sdílí běžící exe.</summary>
    protected virtual string SettingsFilePath => SettingsLoader.DefaultFilePath;

    // Připojení z příkazové řádky (Připojit k databázi) - EDISONini.xml stanice se nepoužije.
    protected virtual bool IsConfigured() => _connectionOverride is not null || SettingsLoader.IsConfigured(SettingsFilePath);

    private PreLoginContext PrepareLogin(ILocalPrefs prefs)
    {
        var (server, port) = ResolveConnection(_connectionOverride,
                                               () => SettingsLoader.Load(_options.UseDevDefaults, SettingsFilePath).Server);

        var loginRest = OpenLoginConnection(server, port);
        // Bez uživatele úplný seznam - slouží jen k Idfa pro Login, povolené firmy načte Run po přihlášení.
        var companies = LoadServerCompanies(loginRest);

        var startCompany = CompanySwitcher.PickInitial(companies, PreferredIdfa(prefs, companies, server, port));
        // Idfa potřebné aby ALVA.SYS.Login.Login uměl resolvovat user's startupForm.
        loginRest.Idfa = startCompany.Id;

        return new PreLoginContext(loginRest, companies, startCompany, server, port);
    }

    /// <summary>Pre-login MLRest - bez uživatele a firmy (vlastní fallback Idfa="").</summary>
    protected virtual IMLRest OpenLoginConnection(string server, int port) => PreLoginConnection.Open(server, port);

    protected virtual IReadOnlyList<CompanyStruct> LoadServerCompanies(IMLRest loginRest) =>
        LegacyApi.CompanyList(loginRest, checkAccess: false);

    // Vlastní poslední firma serveru, když na něm je. Jinak (první start nového shellu, firma mezitím zrušená)
    // navazuje na starý shell, než by začal ve výchozí firmě serveru. E2E běh startuje v zadané firmě.
    private string? PreferredIdfa(ILocalPrefs prefs, IReadOnlyList<CompanyStruct> companies, string server, int port)
    {
        if (_options.TestRun?.StartIdfa is { } start) return start;
        var last = prefs.LastIdfaOrLegacyFor(server, port);
        if (!string.IsNullOrEmpty(last) && companies.Any(c => c.Id == last)) return last;
        return ReadLegacyLastCompany(server, port) ?? last;
    }

    // E2E běh startuje v zadané firmě, registr vývojářské stanice by mu ji nesměl přebít.
    protected virtual string? ReadLegacyLastCompany(string server, int port) =>
        _options.TestRun is null ? LegacyLastCompany.Read(server, port) : null;

    /// <summary>
    /// Parita s EDLOADER <c>CheckLogin</c> + <c>UpdateIsRunning</c>: zákaz přihlašování (kdo ho zadal,
    /// smí ho zrušit) a <c>pending</c> serverové instalace. False = shell se má ukončit.
    /// </summary>
    private bool PassesPreLoginGates(IMLRest rest, EDServices services)
    {
        string ip = LoginGate.StationIp(), user = Environment.UserName;
        var permission = LoginGate.Query(rest, ip, user);
        if (!permission.Allowed)
        {
            if (permission.CanAlter
                && services.PopupDisplayer.Confirm("Je zakázáno přihlašování uživatelů, přejete si jej povolit?"))
            {
                LoginGate.Allow(rest, ip, user);
            }
            else
            {
                services.PopupDisplayer.Info("Je zakázáno přihlašování uživatelů, aplikace bude ukončena.");
                return false;
            }
        }

        if (CreateUpdater().LoadSettings(rest).Pending)
        {
            services.PopupDisplayer.Info("Probíhá aktualizace systému, aplikace bude ukončena.");
            return false;
        }
        return true;
    }

    /// <summary>Připojení z příkazové řádky má přednost před <c>EDISONini.xml</c>.</summary>
    internal static (string Server, int Port) ResolveConnection(ConnectionOverride? connectionOverride,
                                                                 Func<ServerSettings> loadSettings)
    {
        if (connectionOverride is { } overridden) return (overridden.Server, overridden.RestPort);
        var settings = loadSettings();
        return (settings.Host, settings.Port);
    }

    /// <summary>
    /// Zobrazí login okno, nebo vrátí <see cref="StartupOptions.DevLogin"/>; null = uživatel zavřel okno.
    /// Okno po úspěchu zůstává otevřené jako progress podklad pod dobíhajícím bootstrapem.
    /// </summary>
    private LoginResult? TryLogin(IMLRest loginRest, ILocalPrefs prefs, EDServices services)
    {
        if (_options.DevLogin is { } devLogin) return devLogin;

        // Zapamatované heslo (předvyplnění i uložení) řeší VM nad prefs.
        var vm = LoginViewModel.ForStartup(services, loginRest, prefs);
        if (_host.ShowLogin(vm, new ShellWindowStateStore(prefs, loginRest.Server, loginRest.Port)) is not { } result)
        {
            // Zrušeno po odmítnutí zapamatovaného hesla → zapomeň ho (jinak příště zase auto-selže).
            if (vm.RememberedPasswordRejected)
            {
                vm.ApplyRememberedPassword();
                prefs.Save();
            }
            return null;
        }
        _startupLogin = vm;
        // Až po uložení geometrie (ta ukládá celé prefs) - persist proběhne v SavePostLoginState.
        vm.ApplyRememberedPassword();
        return result;
    }

    /// <summary>
    /// Windows SSO auto-login (legacy-style): login na <c>Environment.UserName</c> s prázdným
    /// heslem - server tak přihlásí jen LDAP-AUTO uživatele. Neúspěch (jiný mód / nepovoleno)
    /// → null → fallback na <see cref="TryLogin"/> dialog.
    /// </summary>
/// <remarks>Zkouší se vždy - server při neprázdném heslu překlopí LDAP-AUTO účet na plný LDAP
/// bind (<c>ALVA.SYS.Login:1138</c>), takže dialogem se takový účet přihlásit nedá.</remarks>
    protected virtual LoginResult? TryAutoLogin(IMLRest loginRest)
    {
        try
        {
            return UserAuthenticator.Login(loginRest, Environment.UserName, "");
        }
        catch (LoginException)
        {
            return null;   // ne LDAP-AUTO / nepovoleno → dialog
        }
    }

    private void SavePostLoginState(ILocalPrefs prefs, string server, int port, string idfa, string? loginName)
    {
        prefs.SetLastIdfa(server, port, idfa);
        // DevLogin je syntetický - nesmí přepsat předvyplnění jména (a tím zneplatnit
        // zapamatované heslo) pro ostrý exe na témže stroji.
        if (_options.DevLogin is null) prefs.SetLoginName(server, port, loginName);
        prefs.Save();
    }

    /// <summary>
    /// Delegát, který napojí force-logout na MLRest každé nové session (start i přihlášení jiného
    /// uživatele, <see cref="SessionOpener"/>): když server odstraní ACTIVITY zámek (timeout, výpadek,
    /// admin disconnect) nebo nesedí verze, aplikace skončí s informační hláškou.
    /// </summary>
    private Action<IMLRest> BuildActivityFailedWire(EDServices services)
    {
        // Lokální kopie polí: delegát žije po celou session, closure na bootstrapper by ho držela taky.
        var host = _host;
        var shutdown = _shutdown;
        void End(string message) => host.Dispatch(() =>
        {
            services.PopupDisplayer.Info(message);
            shutdown.Force();
        });
        return mlRest => LegacyApi.OnSessionEnd(mlRest,
            activityLost: () => End("Spojení se serverem bylo přerušeno. Aplikace bude ukončena."),
            versionMismatch: () => End("Rozdílná verze Caché a verze Edison, aplikace bude ukončena."));
    }

    protected virtual EdisonMainWindow BuildShellWindow(
        PreLoginContext prep,
        EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs,
        SessionOpener opener, Task<ReleaseNoticeResult>? releaseNotice)
    {
        // Owner login dialogu: shell okno vzniká až pod VM, který switcher potřebuje.
        EdisonMainWindow? shell = null;
        var sessionSwitcher = new SessionSwitcher(services, () => shell, opener,
                                                  nav.TryCloseAll, new ShellWindowStateStore(prefs, prep.Server, prep.Port),
                                                  _shutdown.Force);
        bool testRun = _options.TestRun is not null;
        var viewModel = CreateShellViewModel(prep.Companies, services, nav, prefs, sessionSwitcher.Switch);
        _shutdown.AttachShell(viewModel.SuppressCloseConfirm);

        var wnd = shell = new EdisonMainWindow { DataContext = viewModel };
        if (testRun) AutomationProperties.SetItemStatus(wnd, TestRunOptions.WindowMarker);
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

        return wnd;
    }

    /// <summary>VM shellu podle voleb startu; bez okna, aby šlo zapojení voleb otestovat.</summary>
    internal EdisonMainViewModel CreateShellViewModel(IReadOnlyList<CompanyStruct> companies, EDServices services,
        WindowNavigator nav, ILocalPrefs prefs, Func<SwitchUserOutcome> switchUser)
    {
        var switcher = new CompanySwitcher(services, prefs, nav.TryCloseShellWindows, nav.CloseLegacyModules);
        // Sdílená služba zástupců - strom (VM) i miniaplikace „Plocha" (provider) drží tutéž instanci.
        var shortcuts = new ModuleShortcutsService();
        var miniAppCatalog = MiniAppCatalog.Build(MiniAppProviders(shortcuts, _options.DevMiniApps));
        bool testRun = _options.TestRun is not null;
        return new EdisonMainViewModel(services, companies, CompanySwitcher.LoadAllowed, new ModuleTreeLoader(),
                                       switcher, switchUser,
                                       prefs, nav.GetActiveModules, nav.CloseAll, _shutdown.Force,
                                       miniAppCatalog, shortcuts,
                                       testRun ? null : new InfChangelogSource(),
                                       _options.InternalDiagnosticsEnabled,
                                       LicenceSyncFor(_options))
        {
            OpensStartupWindow = !testRun
        };
    }

    /// <summary>
    /// E2E běh licenci z M-line nestahuje: sync zapisuje do serveru a při chybě spojení ukáže legacy
    /// okno ERROR, na kterém harness skončí.
    /// </summary>
    internal static Func<IMLRest, LicenceService.MlineSyncResult> LicenceSyncFor(StartupOptions options) =>
        options.TestRun is null ? LegacyApi.SyncFromMline : _ => new LicenceService.MlineSyncResult(false, "");

    /// <summary>Providery miniaplikací shellu. Pořadí registrace řídí pořadí připnutých miniaplikací; výběr je řadí abecedně.</summary>
    internal static IReadOnlyList<IMiniAppProvider> MiniAppProviders(ModuleShortcutsService shortcuts, bool devMiniApps)
    {
        List<IMiniAppProvider> providers =
        [
            new DesktopMiniAppProvider(shortcuts),
            new CoreMiniApps(), new LegacyMiniAppProvider(), new LegacyDashboardMiniAppProvider(),
        ];
        if (devMiniApps) providers.Add(new DevSampleMiniApps());
        return providers;
    }

    /// <summary>
    /// Graceful close → uvolnit ACTIVITY zámek (jinak visí do server-side idle timeoutu, jelikož
    /// je v temp globálu, ne nativním Caché locku). services.MLRest = aktuální (po switch user).
    /// Před logoutem flush usage telemetrie - pojistka na záznamy po posledním zavření okna
    /// (typicky legacy launche); potřebuje živou session kvůli userID.
    /// </summary>
    private void WireSessionLogout(EDServices services) =>
        _host.OnExit(() =>
        {
            EDUsageTelemetry.Flush(services.MLRest);
            SessionLogout.Logout(services.MLRest);
        });

    private void Shutdown() => _shutdown.Force();
}
