using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.LoginWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Start z pohledu <see cref="AppBootstrapper.Run"/>: kroky před loginem, kroky startu kolem
/// <see cref="SessionOpener"/> a mapování jeho výsledku na shell nebo Shutdown. Cestu po loginu
/// ověřuje <see cref="SessionOpenerTests"/>. Test subclass přepisuje virtual hooky, aby se obešel
/// MLRest/WPF window initialization (vyžaduje real server / STA thread).
/// </summary>
public class AppBootstrapperTests
{
    /// <summary>
    /// Bootstrapper nad falešným hostem a mockem předlogin spojení. Přepisuje jen hranice ven (soubory,
    /// registr, legacy seznam firem, procesové statiky) a stavbu shellu. Brány před loginem, PrepareLogin
    /// i ukládání preferencí běží skutečné. Kroky hosta i openeru jdou do jednoho <see cref="Calls"/>.
    /// </summary>
    private class TestableBootstrapper : AppBootstrapper
    {
        private TestableBootstrapper(FakeStartupHost host, StartupOptions options)
            : base(host, new ConnectionOverride("test-host", 9999), options)
        {
            Host = host;
            host.DialogLogin = () => LoginSucceeds
                ? new LoginResult(42, "test", "Test User", true, "",
                                  ChangePasswordNeeded: LoginRequiresPasswordChange, MayUpdate: true, Email: "")
                : null;
            LoginRest.Delimiter.Returns('~');
            LoginRest.Server.Returns("test-host");
            LoginRest.Port.Returns(9999);
        }

        public TestableBootstrapper(StartupOptions? options = null) : this(new FakeStartupHost(), options ?? new StartupOptions())
        {
        }

        public FakeStartupHost Host { get; }
        public List<string> Calls => Host.Calls;
        public bool ShutdownCalled => Host.ShutdownCalled;
        public Exception? ReportedFailure => Host.ReportedFailure;
        public bool TryLoginCalled => Calls.Contains("login dialog");
        public bool HandOffCalled => Calls.Contains("handoff");
        public bool ShellBuiltBeforeHandOff => Calls.IndexOf("shell") is >= 0 and var shell && shell < Calls.IndexOf("handoff");
        public bool SessionLogoutWired => Host.ExitHandlers.Count > 0;
        public bool LogoutWiredBeforeUpdateCheck =>
            Calls.IndexOf("logout hook") is >= 0 and var hook && hook < Calls.IndexOf("update");
        public bool ForcePasswordChangeCalled => Host.PasswordChange is not null;
        public bool ForcePasswordChangeResult { set => Host.PasswordChanged = value; }
        public bool SetupCompleted { set => Host.SetupCompleted = value; }

        public bool LoginSucceeds { get; set; } = true;
        public bool LoginRequiresPasswordChange { get; set; }

        public bool Configured { get; set; } = true;
        public bool EnsureConfiguredCalled { get; private set; }

        protected override bool IsConfigured()
        {
            EnsureConfiguredCalled = true;
            return Configured;
        }

        // Neexistující soubor: wizard předvyplní jen výchozí hodnoty, ne EDISONini.xml z bin adresáře testů.
        protected override string SettingsFilePath { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"EDISONini-{Guid.NewGuid():N}.xml");

        /// <summary>Předlogin spojení: brány a dialog s ním mluví skutečně, odpovědi nastavuje test.</summary>
        public IMLRest LoginRest { get; } = MLRestMocks.Create();

        /// <summary>Všechny firmy serveru (předloginový seznam) - podle něj se pozná uložená firma, která na serveru není.</summary>
        public IReadOnlyList<CompanyStruct> ServerCompanies { get; set; } = [new() { Id = "CB_DP", Database = "CB_DP" }];

        protected override IMLRest OpenLoginConnection(string server, int port) => LoginRest;

        /// <summary>Chyba načtení předloginového seznamu firem (výpadek spojení, chyba serveru).</summary>
        public Exception? CompanyListFailure { get; set; }

        protected override IReadOnlyList<CompanyStruct> LoadServerCompanies(IMLRest loginRest) =>
            CompanyListFailure is { } failure ? throw failure : ServerCompanies;

        public LicenceAcquireOutcome SessionOutcome { get; set; } = LicenceAcquireOutcome.Accepted;
        public IPopupDisplayer Popup { get; set; } = Substitute.For<IPopupDisplayer>();
        public IWindowNavigator Nav { get; set; } = Substitute.For<IWindowNavigator>();
        public IMLRest MLRest { get; set; } = MLRestMocks.Create();

        protected override (WindowNavigator Nav, PopupDisplayer Popup, EDServices Services) InitializeServices()
        {
            // EDServices.Initialize je proces-wide side effect - testy si ho navzájem přepisují.
            var nav = new WindowNavigator();
            var popup = new PopupDisplayer();
            // Wrap mock impls jako EDServices fields - base Run() je čte přes 'services' parametr.
            var svc = new EDServices(MLRest, Nav, Popup);
            return (nav, popup, svc);
        }

        public ILocalPrefs Prefs { get; set; } = Substitute.For<ILocalPrefs>();

        protected override ILocalPrefs LoadLocalPrefs()
        {
            Prefs.When(p => p.Save()).Do(_ => SavedIdfa = Prefs.LastIdfaFor("test-host", 9999));
            return Prefs;
        }

        /// <summary>Firma serveru při posledním Save - hodnota nastavená bez Save se na disk nedostane.</summary>
        public string? SavedIdfa { get; private set; }

        public string? SavedLoginName => Prefs.LoginNameFor("test-host", 9999);

        /// <summary>Poslední firma starého shellu - test nesmí číst registr stanice.</summary>
        public string? LegacyIdfa { get; set; }
        public (string Server, int Port)? LegacyQueriedFor { get; private set; }

        protected override string? ReadLegacyLastCompany(string server, int port)
        {
            LegacyQueriedFor = (server, port);
            return LegacyIdfa;
        }

        public bool AutoLoginSucceeds { get; set; }
        public bool TryAutoLoginCalled { get; private set; }

        protected override LoginResult? TryAutoLogin(IMLRest loginRest)
        {
            TryAutoLoginCalled = true;
            return AutoLoginSucceeds ? new LoginResult(7, "auto", "Auto User", false, "", false, true, "") : null;
        }

        public IReadOnlyList<CompanyStruct> AllowedCompanies { get; set; } = [new() { Id = "CB_DP", Database = "CB_DP" }];
        public Exception? AcquireSessionThrows { get; set; }
        public bool UpdateTriggered { get; set; }
        public Exception? UpdateCheckThrows { get; set; }

        /// <summary>Opener se skutečným pořadím kroků, kroky zapisuje do <see cref="Calls"/>.</summary>
        public TestableSessionOpener? Opener { get; private set; }

        protected override SessionOpener CreateSessionOpener(EDServices services, ILocalPrefs prefs) =>
            Opener = new TestableSessionOpener(services, calls: Calls)
            {
                AllowedCompanies = AllowedCompanies,
                NewSessionMLRest = MLRest,
                LicenceOutcome = SessionOutcome,
                AcquireThrows = AcquireSessionThrows,
                UpdateRequired = UpdateTriggered,
                UpdateCheckThrows = UpdateCheckThrows,
            }.WithLicenceOutcomes(FirstSessionOutcomes);

        /// <summary>Výsledky licence prvních pokusů, potom platí <see cref="SessionOutcome"/>.</summary>
        public List<LicenceAcquireOutcome> FirstSessionOutcomes { get; } = [];

        public bool AcquireSessionCalled => Calls.Contains("acquire");
        public ConnectionInfo? EstablishedConnection => Opener?.EstablishedConnection;
        public LoginResult? AllowedQueriedFor => Opener?.AllowedQuery?.Login;

        public Exception? BuildShellThrows { get; set; }
        public bool BuildShellCalled { get; private set; }
        public IReadOnlyList<CompanyStruct>? ShellCompanies { get; private set; }

        protected override EdisonMainWindow BuildShellWindow(PreLoginContext prep,
            EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs,
            SessionOpener opener, Task<ReleaseNoticeResult>? releaseNotice)
        {
            if (BuildShellThrows is not null) throw BuildShellThrows;
            BuildShellCalled = true;
            Calls.Add("shell");
            ShellCompanies = prep.Companies;
            return Shell;
        }

        /// <summary>Atrapa shellu bez konstruktoru (skutečné okno potřebuje STA a resource), host ji jen předává.</summary>
        public EdisonMainWindow Shell { get; } = UninitializedShell();

        private static EdisonMainWindow UninitializedShell()
        {
            var shell = (EdisonMainWindow)RuntimeHelpers.GetUninitializedObject(typeof(EdisonMainWindow));
            GC.SuppressFinalize(shell);
            return shell;
        }
    }

    [Fact]
    public void Run_NotConfigured_CallsShutdown_ReturnsNull()
    {
        var b = new TestableBootstrapper { Configured = false };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.False(b.TryLoginCalled);   // setup zrušen → ani login se nezkouší
        Assert.False(b.BuildShellCalled);
    }

    [Fact]
    public void Run_HoldsShutdownBeforeFirstWindow()
    {
        // Wizard je první okno, které může vzniknout; jeho zavření nesmí aplikaci ukončit dřív, než rozhodne Run.
        var b = new TestableBootstrapper { Configured = false, SetupCompleted = true };

        b.Run();

        Assert.Equal("hold", b.Calls[0]);
        Assert.Contains("setup", b.Calls);
    }

    [Theory]
    [InlineData(true, "192.168.16.1")]
    [InlineData(false, "")]
    public void Run_NotConfigured_WizardPrefillsDevServerOnlyWithDevDefaults(bool devDefaults, string server)
    {
        // U zákazníka by wizard nabídl cizí IP a mohl ji zapsat do souboru sdíleného s legacy.
        var b = new TestableBootstrapper(new StartupOptions { UseDevDefaults = devDefaults }) { Configured = false };

        b.Run();

        Assert.Equal(server, b.Host.ShownSetup?.Server);
        Assert.Equal(ServerSettings.DefaultRestPort.ToString(), b.Host.ShownSetup?.RestPort);
    }

    [Fact]
    public void Run_SetupCompleted_ProceedsToShell()
    {
        var b = new TestableBootstrapper { Configured = false, SetupCompleted = true };

        b.Run();

        Assert.True(b.BuildShellCalled);
        Assert.False(b.ShutdownCalled);
    }

    [Fact]
    public void Run_PreShellNotice_ShownOnceAfterLogoutWired_ShellStillBuilt()
    {
        // Ukončení během hlášky musí uvolnit zámek jako každé ukončení po Accepted.
        var b = new TestableBootstrapper(new StartupOptions { PreShellNotice = "hláška" });
        bool? logoutWiredAtNotice = null;
        b.Popup.When(p => p.Info("hláška")).Do(_ => logoutWiredAtNotice = b.SessionLogoutWired);

        b.Run();

        b.Popup.Received(1).Info("hláška");
        Assert.True(logoutWiredAtNotice);
        Assert.True(b.BuildShellCalled);
    }

    [Fact]
    public void Run_LicenceRefused_NoPreShellNotice()
    {
        var b = new TestableBootstrapper(new StartupOptions { PreShellNotice = "hláška" })
        {
            SessionOutcome = LicenceAcquireOutcome.Refused
        };

        b.Run();

        b.Popup.DidNotReceive().Info("hláška");
    }

    // Brány legacy EDLOADERu (parita CheckLogin + UpdateIsRunning) - konec ještě před loginem.

    [Fact]
    public void Run_ServerInstallationPending_InfoAndShutdown_BeforeLogin()
    {
        var b = new TestableBootstrapper();
        b.LoginRest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", Arg.Any<object[]>())
                   .Returns(MLRestMocks.Output("pending~1"));

        var result = b.Run();

        Assert.Null(result);
        b.Popup.Received(1).InfoSystem("Probíhá aktualizace systému, aplikace bude ukončena.");
        Assert.True(b.ShutdownCalled);
        Assert.False(b.TryLoginCalled);
        Assert.False(b.BuildShellCalled);
    }

    [Fact]
    public void Run_LoginForbiddenWithoutRight_InfoAndShutdown_BeforeLogin()
    {
        var b = new TestableBootstrapper();
        b.LoginRest.QueryOrThrow("ALVA.SYSTEM.Main", "LoginAllowed", Arg.Any<object[]>())
                   .Returns(MLRestMocks.Output("login~0~0"));

        b.Run();

        b.Popup.DidNotReceiveWithAnyArgs().ConfirmSystem(default!);
        b.Popup.Received(1).InfoSystem("Je zakázáno přihlašování uživatelů, aplikace bude ukončena.");
        Assert.True(b.ShutdownCalled);
        Assert.False(b.TryLoginCalled);
    }

    [Fact]
    public void Run_LoginForbidden_AllowedByUser_ContinuesToLogin()
    {
        // Kdo zákaz zadal, smí ho zrušit (legacy EDLOADER CheckLogin) - server dostane ChangeLoginState.
        var b = new TestableBootstrapper();
        b.LoginRest.QueryOrThrow("ALVA.SYSTEM.Main", "LoginAllowed", Arg.Any<object[]>())
                   .Returns(MLRestMocks.Output("login~0~1"));
        b.Popup.ConfirmSystem(Arg.Any<string>()).Returns(true);

        b.Run();

        b.LoginRest.Received(1).QueryOrThrow("ALVA.SYSTEM.Main", "ChangeLoginState", Arg.Any<object[]>());
        Assert.True(b.BuildShellCalled);
    }

    [Fact]
    public void Run_Configured_ProceedsToLogin()
    {
        var b = new TestableBootstrapper { Configured = true };

        b.Run();

        Assert.True(b.EnsureConfiguredCalled);
        Assert.True(b.BuildShellCalled);
    }

    [Fact]
    public void Run_AutoLoginSucceeds_SkipsDialog_BuildsShell()
    {
        // Auto-login projde → TryLogin (dialog) se vůbec nevolá, i kdyby měl selhat.
        var b = new TestableBootstrapper { AutoLoginSucceeds = true, LoginSucceeds = false };

        var result = b.Run();

        Assert.False(b.TryLoginCalled);
        Assert.True(b.BuildShellCalled);
        Assert.False(b.ShutdownCalled);
    }

    [Fact]
    public void Run_AutoLoginFails_FallsBackToDialog()
    {
        var b = new TestableBootstrapper { AutoLoginSucceeds = false, LoginSucceeds = true };

        b.Run();

        Assert.True(b.TryLoginCalled);
        Assert.True(b.BuildShellCalled);
    }

    // Vynucená změna hesla = Caché user flag #17.

    [Fact]
    public void Run_ChangePasswordNeeded_ForcesChange_ThenProceeds()
    {
        var b = new TestableBootstrapper
        {
            LoginSucceeds = true,
            LoginRequiresPasswordChange = true,
            ForcePasswordChangeResult = true,
            SessionOutcome = LicenceAcquireOutcome.Accepted,
            MLRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP"),
        };

        b.Run();

        Assert.True(b.ForcePasswordChangeCalled);
        // Dialog nad login oknem mluví s předlogin spojením, session ještě nevznikla.
        Assert.Same(b.LoginRest, b.Host.PasswordChange?.LoginRest);
        Assert.Equal("test", b.Host.PasswordChange?.UserName);
        Assert.True(b.BuildShellCalled);
    }

    [Fact]
    public void Run_ChangePasswordCancelled_ShutsDown_NoShell()
    {
        var b = new TestableBootstrapper
        {
            LoginSucceeds = true,
            LoginRequiresPasswordChange = true,
            ForcePasswordChangeResult = false,
        };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.False(b.BuildShellCalled);
    }

    [Fact]
    public void Run_LoginCancelled_CallsShutdown_ReturnsNull()
    {
        var b = new TestableBootstrapper { LoginSucceeds = false };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
    }

    [Fact]
    public void Run_LoginCancelled_DoesNotEstablishSession()
    {
        var b = new TestableBootstrapper { LoginSucceeds = false };

        b.Run();

        // EstablishSession by se nemělo dostat ke slovu - výsledkem by byl unused real MLRest.
        // Sledujeme přes AcquireSession (volá se po EstablishSession).
        Assert.False(b.AcquireSessionCalled);
        Assert.False(b.BuildShellCalled);
    }

    [Fact]
    public void Run_LicenceRefused_ShowsPopupInfo_CallsShutdown_ReturnsNull()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        b.Popup.Received(1).InfoSystem("Nelze se přihlásit, byl překročen počet licencí.");
    }

    [Fact]
    public void Run_LicenceRefused_OtherCompanyPicked_ShellStartsThere()
    {
        var b = new TestableBootstrapper
        {
            AllowedCompanies = [new() { Id = "CB_DP", Database = "CB_DP" }, new() { Id = "JI_DP", Database = "JI_DB" }],
        };
        b.FirstSessionOutcomes.Add(LicenceAcquireOutcome.Refused);
        b.Host.OtherCompany = offered => offered[0];

        b.Run();

        Assert.Contains("pick company CB_DP", b.Calls);
        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
        Assert.True(b.BuildShellCalled);
        b.Popup.DidNotReceive().InfoSystem(Arg.Any<string>());
    }

    [Fact]
    public void Run_LicenceRefused_OtherCompanyNotPicked_ShutdownWithoutPopup()
    {
        // Výběr firmy už důvod řekl, druhá hláška by ho jen opakovala.
        var b = new TestableBootstrapper
        {
            SessionOutcome = LicenceAcquireOutcome.Refused,
            AllowedCompanies = [new() { Id = "CB_DP", Database = "CB_DP" }, new() { Id = "JI_DP", Database = "JI_DB" }],
        };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.False(b.BuildShellCalled);
        b.Popup.DidNotReceive().InfoSystem(Arg.Any<string>());
    }

    [Fact]
    public void Run_LicenceRefused_DoesNotBuildShell()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };

        b.Run();

        Assert.False(b.BuildShellCalled);
        Assert.Null(b.SavedIdfa);
    }

    [Fact]
    public void Run_LicenceCancelled_CallsShutdown_NoPopup()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Cancelled };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        // Cancelled je admin volba - žádná zpráva (admin už ví že rušil sám).
        b.Popup.DidNotReceive().InfoSystem(Arg.Any<string>());
    }

    [Fact]
    public void Run_LicenceCancelled_DoesNotBuildShell()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Cancelled };

        b.Run();

        Assert.False(b.BuildShellCalled);
        Assert.Null(b.SavedIdfa);
    }

    [Fact]
    public void Run_Success_ReturnsHandedOffShell_NoShutdown()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Accepted };

        var result = b.Run();

        Assert.Same(b.Shell, result);
        Assert.Same(b.Shell, b.Host.HandedOff);
        Assert.False(b.ShutdownCalled);
    }

    [Fact]
    public void Run_RememberedPasswordRejected_DialogClosed_ForgetsIt()
    {
        // Jinak by příští start předvyplnil totéž heslo a server ho zase odmítl.
        var b = new TestableBootstrapper { LoginSucceeds = false };
        b.Prefs.SetLoginName("test-host", 9999, "jan");
        b.Prefs.SetRememberedPassword("test-host", 9999, PasswordProtector.Protect("jan", "stare"));
        b.LoginRest.QueryOrThrow("ALVA.SYS.Login", "Login", Arg.Any<object[]>()).Returns(MLRestMocks.Output("-1"));
        b.Host.InDialog = vm => vm.Login(vm.InitialPassword);

        b.Run();

        Assert.Equal("stare", b.Host.ShownLogin?.InitialPassword);
        Assert.Null(b.Prefs.RememberedPasswordFor("test-host", 9999));
        b.Prefs.Received().Save();
        Assert.True(b.ShutdownCalled);
    }

    [Fact]
    public void Run_Success_SavesIdfaFromMLRest()
    {
        var mlRest = MLRestMocks.Create(idfa: "JI_DP", dataNamespace: "JI_DP");
        var b = new TestableBootstrapper
        {
            SessionOutcome = LicenceAcquireOutcome.Accepted,
            MLRest = mlRest,
        };

        b.Run();

        Assert.Equal("JI_DP", b.SavedIdfa);
        Assert.Equal("test", b.SavedLoginName);   // jméno z úspěšného loginu, pod serverem, na který se přihlásil
        b.Prefs.Received().Save();
    }

    [Fact]
    public void Run_DevLogin_SkipsDialog_KeepsLoginName()
    {
        // DevLogin je syntetický - jméno v local.json patří poslednímu ostrému přihlášení.
        var options = StartupOptions.Development([], null);
        var b = new TestableBootstrapper(options);

        b.Run();

        Assert.False(b.TryLoginCalled);
        Assert.Same(options.DevLogin, b.AllowedQueriedFor);
        Assert.Equal("CB_DP", b.SavedIdfa);
        b.Prefs.DidNotReceive().LoginNames = Arg.Any<Dictionary<string, string>?>();
        b.Prefs.Received(1).Save();
    }

    [Fact]
    public void Run_LoginCancelled_DoesNotSavePrefs()
    {
        // Důvod: dokud nemáme úspěšný login + accepted licenci, nemá smysl ukládat firmu.
        var b = new TestableBootstrapper { LoginSucceeds = false };

        b.Run();

        Assert.Null(b.SavedIdfa);
    }

    [Fact]
    public void Run_UpdateTriggered_CallsShutdown_DoesNotBuildShell()
    {
        var b = new TestableBootstrapper { UpdateTriggered = true };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.False(b.BuildShellCalled);
    }

    [Fact]
    public void Run_UpdateTriggered_RunsAfterSavePrefs()
    {
        // Update check je až po SavePostLoginState - firma musí být uložená
        // i když shell hned skončí updatem (příští start ji načte).
        var b = new TestableBootstrapper { UpdateTriggered = true };

        b.Run();

        Assert.True(b.AcquireSessionCalled);
        Assert.NotNull(b.SavedIdfa);
    }

    /// <summary>Volá skutečné hranice ven bez přepsání - jen ty, které v E2E běhu nesahají na disk ani registr.</summary>
    private class RealStepsBootstrapper(StartupOptions options) : AppBootstrapper(new FakeStartupHost(), null, options)
    {
        public ILocalPrefs Load() => LoadLocalPrefs();

        public string? CallReadLegacy(string server, int port) => ReadLegacyLastCompany(server, port);
    }

    private static StartupOptions E2E(params string[] args) => StartupOptions.Development(["e2e=1", .. args], null);

    [Fact]
    public void Run_TestRun_SkipsAutoLogin()
    {
        var options = E2E();
        var b = new TestableBootstrapper(options) { AutoLoginSucceeds = true };

        b.Run();

        Assert.False(b.TryAutoLoginCalled);
        Assert.Same(options.DevLogin, b.AllowedQueriedFor);   // identitu E2E dává DevLogin
    }

    [Fact]
    public void LoadLocalPrefs_TestRun_KeepsPrefsInMemory()
    {
        var prefs = new RealStepsBootstrapper(E2E()).Load();

        Assert.IsType<TransientLocalPrefs>(prefs);
        Assert.Null(prefs.LastIdfaByServer);
    }

    [Fact]
    public void Run_TestRunWithIdfa_StartsInThatCompany()
    {
        // Zadaná firma vyhraje i nad uloženou firmou serveru.
        var b = new TestableBootstrapper(E2E("idfa=JI_DP")) { AllowedCompanies = TwoAllowed, ServerCompanies = TwoAllowed };
        b.Prefs.SetLastIdfa("test-host", 9999, "CB_DP");

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("260901.1", "260901.1")]
    public void CreateServices_VersionFromVerzeInf_OtherwiseLeftToAppIdentity(string installed, string? expected)
    {
        // Bez VERZE.INF rozhoduje AppIdentity (verze exe s hashem buildu), pevný text by ji přebil.
        var services = AppBootstrapper.CreateServices(new WindowNavigator(), new PopupDisplayer(), installed,
                                                      StartupOptions.Production());

        Assert.Equal(expected, services.AppVersion);
        Assert.Equal("M-line EDISON 2", services.AppDisplayName);
        Assert.Equal("EDISON2", services.FormIdPrefix);   // součást uložených formID
    }

    [Fact]
    public void CreateServices_RegularStart_KeepsWindowGeometryInCache()
    {
        // Úložiště v paměti mimo E2E by uživatelům tiše zahodilo uloženou polohu dialogů.
        foreach (var options in new[] { StartupOptions.Production(), StartupOptions.Development([], null) })
            Assert.Null(AppBootstrapper.CreateServices(new WindowNavigator(), new PopupDisplayer(), "", options)
                .WindowStateStoreFactory);
    }

    [Fact]
    public void CreateServices_TestRun_KeepsWindowGeometryInMemoryPerForm()
    {
        var services = AppBootstrapper.CreateServices(new WindowNavigator(), new PopupDisplayer(), "", E2E());
        var factory = services.WindowStateStoreFactory
                      ?? throw new InvalidOperationException("E2E běh nemá úložiště geometrie v paměti.");

        factory("EDISON2.UserProfileWindow").Save(new System.Windows.Rect(10, 20, 300, 200), maximized: false);

        Assert.Equal(new WindowGeometry(10, 20, 300, 200, false), factory("EDISON2.UserProfileWindow").Load());
        Assert.True(factory("EDISON2.EDInputFormTestWindow").Load().IsEmpty);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void MiniAppProviders_DevSamplesOnlyInDevelopment(bool devMiniApps, int devSamples)
    {
        var providers = AppBootstrapper.MiniAppProviders(new ModuleShortcutsService(), devMiniApps);

        Assert.Equal(devSamples, providers.OfType<MiniApps.DevSampleMiniApps>().Count());
        Assert.IsType<DesktopMiniAppProvider>(providers[0]);   // pořadí = pořadí skupin v menu
    }

    [Fact]
    public void LicenceSyncFor_TestRun_NeverContactsMline()
    {
        // Sync zapisuje licenci do serveru a při výpadku M-line ukáže legacy okno ERROR, na kterém E2E skončí.
        var sync = AppBootstrapper.LicenceSyncFor(E2E());
        var rest = Substitute.For<IMLRest>();

        Assert.NotNull(sync);
        Assert.Equal(new LicenceService.MlineSyncResult(false, ""), sync(rest));
        Assert.Empty(rest.ReceivedCalls());
    }

    [Fact]
    public void LicenceSyncFor_RegularStart_SyncsThroughLegacyApi()
    {
        Assert.Equal(nameof(LegacyApi.SyncFromMline), AppBootstrapper.LicenceSyncFor(StartupOptions.Production()).Method.Name);
        Assert.Equal(nameof(LegacyApi.SyncFromMline),
                     AppBootstrapper.LicenceSyncFor(StartupOptions.Development([], null)).Method.Name);
    }

    private static EdisonMainViewModel ShellViewModelFor(StartupOptions options) =>
        new TestableBootstrapper(options).CreateShellViewModel(
            [new() { Id = "CB_DP", Database = "CB_DP" }],
            new EDServices(MLRestMocks.Create(), Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            new WindowNavigator(), Substitute.For<ILocalPrefs>(), () => new(SwitchUserResult.Cancelled));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateShellViewModel_TestMenuFollowsStartupOptions(bool diagnostics)
    {
        // U zákazníka by „Vyvolat chybu" z menu Test funkcí založilo tiket.
        var vm = ShellViewModelFor(StartupOptions.Production() with { InternalDiagnosticsEnabled = diagnostics });

        Assert.Equal(diagnostics, vm.IsTestMenuVisible);
    }

    [Fact]
    public void CreateShellViewModel_TestRun_DoesNotOpenStartupWindow()
    {
        Assert.True(ShellViewModelFor(StartupOptions.Production()).OpensStartupWindow);
        Assert.False(ShellViewModelFor(E2E()).OpensStartupWindow);
    }

    [Fact]
    public void Run_Success_CallsHandOffAfterBuildShell()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Accepted };

        b.Run();

        Assert.True(b.HandOffCalled);
        Assert.True(b.ShellBuiltBeforeHandOff);   // handoff váže ContentRendered na hotové okno
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_AutoLoginAndDialog_HoldShutdownFirst_HandOffShellOnce(bool autoLogin)
    {
        // Při auto-loginu chybělo předání i držení ukončení: popup před shellem byl jediné okno a jeho zavření
        // aplikaci ukončilo. Obě cesty musí jít stejně.
        var b = new TestableBootstrapper { AutoLoginSucceeds = autoLogin };

        b.Run();

        Assert.Equal(autoLogin, !b.TryLoginCalled);
        Assert.Equal("hold", b.Calls[0]);
        Assert.Equal(["shell", "handoff"], b.Calls[^2..]);
        Assert.Single(b.Calls, c => c == "handoff");
    }

    [Fact]
    public void Run_ShutdownDuringStartup_NoShellNoFailureReport()
    {
        // Force logout během startu (tady při hlášce před shellem) už aplikaci ukončuje. Postavený shell by WPF
        // při ukončení zavíral s potvrzením, které nejde ukázat: odhlášení by neproběhlo a odešlo by hlášení chyby.
        var b = new TestableBootstrapper(new StartupOptions { PreShellNotice = "hláška" });
        b.Popup.When(p => p.Info("hláška")).Do(_ => b.Host.Shutdown());

        var result = b.Run();

        Assert.Null(result);
        Assert.False(b.BuildShellCalled);
        Assert.False(b.HandOffCalled);
        Assert.Null(b.ReportedFailure);
        Assert.Single(b.Calls, c => c == "shutdown");   // start ho neopakuje
        Assert.True(b.SessionLogoutWired);
    }

    [Fact]
    public void Run_LoginCancelled_DoesNotHandOff()
    {
        var b = new TestableBootstrapper { LoginSucceeds = false };

        b.Run();

        Assert.False(b.HandOffCalled);
    }

    [Fact]
    public void Run_LicenceRefused_DoesNotHandOff()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };

        b.Run();

        Assert.False(b.HandOffCalled);
    }

    [Fact]
    public void Run_UpdateTriggered_DoesNotHandOff()
    {
        var b = new TestableBootstrapper { UpdateTriggered = true };

        b.Run();

        Assert.False(b.HandOffCalled);
    }

    // Chybová hranice startupu: každá výjimka po loginu končí explicitním Shutdown, jinak by
    // aplikace žila dál s neuzavíratelným login oknem (Closing během progress fáze ruší).

    [Fact]
    public void Run_UpdateCheckThrows_ReportsAndShutsDown_NoShell()
    {
        var ex = new InvalidOperationException("LoadSettings selhal");
        var b = new TestableBootstrapper { UpdateCheckThrows = ex };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.Same(ex, b.ReportedFailure);
        Assert.False(b.BuildShellCalled);
        Assert.False(b.HandOffCalled);
    }

    [Fact]
    public void Run_ServerUnreachableBeforeLogin_MessageAndShutdownWithoutErrorDialog()
    {
        var b = new TestableBootstrapper { CompanyListFailure = new InvalidOperationException(MLRestErrors.LegacyWrapped(MLRestErrors.Transport())) };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.Contains("unreachable test-host:9999", b.Calls);
        Assert.Same(b.CompanyListFailure, b.Host.UnreachableFailure);
        Assert.Null(b.ReportedFailure);
        Assert.False(b.TryLoginCalled);
    }

    [Fact]
    public void Run_CompanyListServerError_StaysTechnicalFailure()
    {
        var ex = new InvalidOperationException(MLRestErrors.ServerError("ERROR #5001: chyba v metodě"));
        var b = new TestableBootstrapper { CompanyListFailure = ex };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.Same(ex, b.ReportedFailure);
        Assert.Null(b.Host.UnreachableFailure);
    }

    [Fact]
    public void Run_BuildShellThrows_ReportsAndShutsDown_NoHandOff()
    {
        var ex = new InvalidOperationException("MiniAppCatalog");
        var b = new TestableBootstrapper { BuildShellThrows = ex };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.Same(ex, b.ReportedFailure);
        Assert.False(b.HandOffCalled);
    }

    [Fact]
    public void Run_AcquireSessionThrows_ShutsDown_WithoutLogoutHook()
    {
        // VerifyLic spadl → zámek nevznikl, logout hook se nezapojuje, ale aplikace se ukončí.
        var b = new TestableBootstrapper { AcquireSessionThrows = new InvalidOperationException("VerifyLic") };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        Assert.NotNull(b.ReportedFailure);
        Assert.False(b.SessionLogoutWired);
        Assert.Null(b.SavedIdfa);
    }

    [Fact]
    public void Run_Success_RequestsReleaseNoticeForShell()
    {
        var b = new TestableBootstrapper();

        b.Run();

        Assert.True(b.Opener?.NoticeRequested);
    }

    [Fact]
    public void Run_Success_DoesNotReportFailure()
    {
        var b = new TestableBootstrapper();

        b.Run();

        Assert.Null(b.ReportedFailure);
        Assert.False(b.ShutdownCalled);
    }

    // Logout hook (uvolnění ACTIVITY zámku při Exit) se zapojuje hned po Accepted, aby ho
    // dostal i exit kvůli updatu a výjimka v dalších krocích, ne až vznik shellu.

    [Fact]
    public void Run_Success_WiresSessionLogoutBeforeUpdateCheck()
    {
        var b = new TestableBootstrapper();

        b.Run();

        Assert.True(b.SessionLogoutWired);
        Assert.True(b.LogoutWiredBeforeUpdateCheck);
    }

    [Fact]
    public void Run_UpdateTriggered_HasSessionLogoutWired()
    {
        var b = new TestableBootstrapper { UpdateTriggered = true };

        b.Run();

        Assert.True(b.SessionLogoutWired);
    }

    [Fact]
    public void Run_UpdateCheckThrows_HasSessionLogoutWired()
    {
        var b = new TestableBootstrapper { UpdateCheckThrows = new InvalidOperationException() };

        b.Run();

        Assert.True(b.SessionLogoutWired);
    }

    [Fact]
    public void Run_LicenceRefused_DoesNotWireSessionLogout()
    {
        // Refused/Cancelled = VerifyLic zámek nevytvořil, není co odhlašovat.
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };

        b.Run();

        Assert.False(b.SessionLogoutWired);
    }

    // Seznam firem po loginu: předloginový je úplný, shell nabízí jen povolené (legacy frmLoginEx.vb:467).

    private static readonly CompanyStruct[] TwoAllowed =
    [
        new() { Id = "CB_DP", Database = "CB_DP" },
        new() { Id = "JI_DP", Database = "JI_DP" },
    ];

    [Fact]
    public void Run_LastCompanyNotAllowed_StartsInFirstAllowed()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed };
        b.Prefs.SetLastIdfa("test-host", 9999, "PI_DP");

        b.Run();

        Assert.Equal(new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"), b.EstablishedConnection);
    }

    [Fact]
    public void Run_LastCompanyAllowed_StartsThere()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed };
        b.Prefs.SetLastIdfa("test-host", 9999, "JI_DP");

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
    }

    // První start nového shellu: local.json firmu nemá, shell naváže na firmu starého shellu, ne na výchozí firmu serveru.
    [Fact]
    public void Run_FirstStartWithoutLastCompany_StartsInLegacyShellCompany()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed, LegacyIdfa = "JI_DP" };

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
        Assert.Equal(("test-host", 9999), b.LegacyQueriedFor);
    }

    [Fact]
    public void Run_OwnLastCompany_WinsOverLegacyShellCompany()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed, ServerCompanies = TwoAllowed, LegacyIdfa = "CB_DP" };
        b.Prefs.SetLastIdfa("test-host", 9999, "JI_DP");

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
    }

    // Dvě instalace na stanici (Dev a Local) mají stejná IDFa: firma druhé instalace se nepoužije, i když na serveru je.
    [Fact]
    public void Run_LastCompanyOfOtherServer_StartsInLegacyShellCompany()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed, ServerCompanies = TwoAllowed, LegacyIdfa = "JI_DP" };
        b.Prefs.SetLastIdfa("other-host", 9999, "CB_DP");

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
    }

#pragma warning disable CS0618 // firma starší verze
    // První start po aktualizaci: firma starší verze vyhraje i nad firmou starého shellu, jako dřív.
    [Fact]
    public void Run_FirstStartAfterUpdate_StartsInCompanyOfOlderVersion()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed, ServerCompanies = TwoAllowed, LegacyIdfa = "CB_DP" };
        b.Prefs.LastIdfa.Returns("JI_DP");

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
    }

    [Fact]
    public void Run_FirstStartAfterUpdate_LicenceRefused_StoresNoCompanyForServer()
    {
        // Login okno ukládá prefs už po OK: firma starší verze se nesmí přiřadit serveru, kam se uživatel nedostal.
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };
        b.Prefs.LastIdfa.Returns("CB_DP");

        b.Run();

        Assert.Null(b.Prefs.LastIdfaByServer);
    }

    [Fact]
    public void Run_OtherServerHasOwnCompany_IgnoresCompanyOfOlderVersion()
    {
        // Firmu starší verze dostal první server, druhá instalace začne ve výchozí firmě.
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed, ServerCompanies = TwoAllowed };
        b.Prefs.LastIdfa.Returns("JI_DP");
        b.Prefs.SetLastIdfa("other-host", 9999, "PI_DP");

        b.Run();

        Assert.Equal("CB_DP", b.EstablishedConnection?.Idfa);
    }
#pragma warning restore CS0618

    [Fact]
    public void Run_LoginWindow_GetsGeometryOfConnectionServer()
    {
        var b = new TestableBootstrapper();
        b.Prefs.SetShellWindowBounds("other-host", 9999, new WindowBoundsPrefs { Left = 1920, Width = 1024, Height = 768 });
        b.Prefs.SetShellWindowBounds("test-host", 9999, new WindowBoundsPrefs { Left = 10, Top = 20, Width = 800, Height = 600 });

        b.Run();

        Assert.Equal(new WindowGeometry(10, 20, 800, 600, false), b.Host.ShownLoginWindowState?.Load());
    }

    [Fact]
    public void ReadLegacyLastCompany_TestRun_DoesNotReadRegistry() =>
        Assert.Null(new RealStepsBootstrapper(E2E()).CallReadLegacy("192.168.16.1", 57770));

    [Fact]
    public void Run_ShellGetsAllowedCompaniesOfLoggedUser()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed };

        b.Run();

        Assert.Same(TwoAllowed, b.ShellCompanies);
        Assert.Equal("test", b.AllowedQueriedFor?.UserName);   // dotaz nese uživatele, jinak server nefiltruje
    }

    [Fact]
    public void Run_NoAllowedCompany_InfoAndShutdown_NoSession()
    {
        var b = new TestableBootstrapper { AllowedCompanies = [] };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        b.Popup.Received(1).InfoSystem("Uživatel nemá přístup k žádné firmě.");
        Assert.Null(b.EstablishedConnection);
        Assert.False(b.AcquireSessionCalled);
    }

    [Fact]
    public void Run_LoginCancelled_DoesNotQueryAllowedCompanies()
    {
        var b = new TestableBootstrapper { LoginSucceeds = false };

        b.Run();

        Assert.Null(b.AllowedQueriedFor);
    }
}
