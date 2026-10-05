using System.Collections.Generic;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.LoginWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// 4 exit pathy z <see cref="AppBootstrapper.Run"/>: login cancel, licence Refused,
/// licence Cancelled, success. Test subclass přepisuje virtual hooky aby se obešel
/// MLRest/WPF window initialization (vyžaduje real server / STA thread).
/// </summary>
public class AppBootstrapperTests
{
    /// <remarks>Bezparametrický ctor → <c>Application</c> je null; nový krok v Run(), který na něj
    /// sáhne (dialog, Dispatcher, ShutdownMode), musí jít za <c>protected virtual</c> hook.</remarks>
    private class TestableBootstrapper(StartupOptions? options = null) : AppBootstrapper(options ?? new StartupOptions())
    {
        public bool ShutdownCalled { get; private set; }
        public bool BuildShellCalled { get; private set; }
        public string? SavedIdfa { get; private set; }
        public string? SavedLoginName { get; private set; }
        public string? LoginDialogPrefill { get; private set; }
        public bool WireActivityCalled { get; private set; }
        public bool AcquireSessionCalled { get; private set; }

        public bool Configured { get; set; } = true;
        public bool EnsureConfiguredCalled { get; private set; }
        public bool LoginSucceeds { get; set; } = true;

        protected override bool EnsureConfigured()
        {
            EnsureConfiguredCalled = true;
            return Configured;
        }

        public bool PreLoginGatesPass { get; set; } = true;
        public bool PreLoginGatesCalled { get; private set; }

        protected override bool PassesPreLoginGates(MLRest loginRest, EDServices services)
        {
            PreLoginGatesCalled = true;
            return PreLoginGatesPass;
        }
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

        protected override ILocalPrefs LoadLocalPrefs() => Prefs;

        protected override PreLoginContext PrepareLogin(ILocalPrefs prefs) =>
            new(LoginRest: null!, Companies: new List<CompanyStruct>(), StartCompany: default,
                Server: "test-host", Port: 9999);

        protected override bool TryLogin(MLRest loginRest, ILocalPrefs prefs, EDServices services, out LoginResult login)
        {
            TryLoginCalled = true;
            LoginDialogPrefill = prefs.LastLoginName;
            login = new LoginResult(42, "test", "Test User", true, "",
                ChangePasswordNeeded: LoginRequiresPasswordChange, MayUpdate: true, Email: "");
            return LoginSucceeds;
        }

        public bool LoginRequiresPasswordChange { get; set; }
        public bool ForcePasswordChangeResult { get; set; } = true;
        public bool ForcePasswordChangeCalled { get; private set; }

        protected override bool ForcePasswordChange(MLRest loginRest, string username)
        {
            ForcePasswordChangeCalled = true;
            return ForcePasswordChangeResult;
        }

        public bool AutoLoginSucceeds { get; set; }
        public bool TryAutoLoginCalled { get; private set; }
        public bool TryLoginCalled { get; private set; }

        protected override bool TryAutoLogin(MLRest loginRest, ILocalPrefs prefs, out LoginResult login)
        {
            TryAutoLoginCalled = true;
            if (AutoLoginSucceeds)
            {
                login = new LoginResult(7, "auto", "Auto User", false, "", false, true, "");
                return true;
            }
            login = null!;
            return false;
        }

        public IReadOnlyList<CompanyStruct> AllowedCompanies { get; set; } = [new() { Id = "CB_DP", Database = "CB_DP" }];
        public LoginResult? AllowedQueriedFor { get; private set; }

        protected override IReadOnlyList<CompanyStruct> LoadAllowedCompanies(ConnectionInfo conn, LoginResult login)
        {
            AllowedQueriedFor = login;
            return AllowedCompanies;
        }

        public ConnectionInfo? EstablishedConnection { get; private set; }

        protected override SessionContext EstablishSession(ConnectionInfo conn, LoginResult login, EDServices services)
        {
            EstablishedConnection = conn;
            return new(MLRest, login, () => null);
        }

        public Exception? AcquireSessionThrows { get; set; }

        protected override (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session, EDServices services)
        {
            AcquireSessionCalled = true;
            if (AcquireSessionThrows is not null) throw AcquireSessionThrows;
            return (SessionOutcome, null);
        }

        protected override void SavePostLoginState(ILocalPrefs prefs, string idfa, string? loginName)
        {
            SavedIdfa = idfa;
            SavedLoginName = loginName;
        }

        protected override void WireActivityFailedHandler(SessionContext session, EDServices services)
            => WireActivityCalled = true;

        public bool SessionLogoutWired { get; private set; }
        public bool LogoutWiredBeforeUpdateCheck { get; private set; }

        protected override void WireSessionLogout(EDServices services) => SessionLogoutWired = true;

        public bool UpdateTriggered { get; set; }
        public Exception? UpdateCheckThrows { get; set; }

        protected override (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) CheckForUpdate(
            SessionContext session, EDServices services)
        {
            LogoutWiredBeforeUpdateCheck = SessionLogoutWired;
            if (UpdateCheckThrows is not null) throw UpdateCheckThrows;
            return (UpdateTriggered, null);
        }

        public Exception? BuildShellThrows { get; set; }
        public IReadOnlyList<CompanyStruct>? ShellCompanies { get; private set; }

        protected override EdisonMainWindow BuildShellWindow(PreLoginContext prep, SessionContext session, Lic? licence,
            EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs,
            Task<ReleaseNoticeResult>? releaseNotice)
        {
            if (BuildShellThrows is not null) throw BuildShellThrows;
            BuildShellCalled = true;
            ShellCompanies = prep.Companies;
            return null!;
        }

        public Exception? ReportedFailure { get; private set; }

        protected override void ReportStartupFailure(Exception ex) => ReportedFailure = ex;

        public bool HandOffCalled { get; private set; }
        public bool ShellBuiltBeforeHandOff { get; private set; }

        protected override void HandOffLoginWindow(EdisonMainWindow wnd)
        {
            HandOffCalled = true;
            ShellBuiltBeforeHandOff = BuildShellCalled;
        }

        protected override void Shutdown() => ShutdownCalled = true;
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
    public void Run_PreLoginGateRefuses_ShutsDown_BeforeLogin()
    {
        // Zákaz přihlašování / běžící instalace na serveru (parita s EDLOADER) - konec ještě před loginem.
        var b = new TestableBootstrapper { PreLoginGatesPass = false };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.PreLoginGatesCalled);
        Assert.True(b.ShutdownCalled);
        Assert.False(b.TryLoginCalled);
        Assert.False(b.BuildShellCalled);
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
    public void Run_NoPasswordChangeNeeded_SkipsForce()
    {
        var b = new TestableBootstrapper
        {
            LoginSucceeds = true,
            SessionOutcome = LicenceAcquireOutcome.Accepted,
            MLRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP"),
        };

        b.Run();

        Assert.False(b.ForcePasswordChangeCalled);
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
        Assert.False(b.WireActivityCalled);
    }

    [Fact]
    public void Run_LicenceRefused_ShowsPopupInfo_CallsShutdown_ReturnsNull()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        b.Popup.Received(1).Info("Nelze se přihlásit, byl překročen počet licencí.");
    }

    [Fact]
    public void Run_LicenceRefused_DoesNotBuildShell()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Refused };

        b.Run();

        Assert.False(b.BuildShellCalled);
        Assert.False(b.WireActivityCalled);
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
        b.Popup.DidNotReceive().Info(Arg.Any<string>());
    }

    [Fact]
    public void Run_LicenceCancelled_DoesNotBuildShell()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Cancelled };

        b.Run();

        Assert.False(b.BuildShellCalled);
        Assert.False(b.WireActivityCalled);
        Assert.Null(b.SavedIdfa);
    }

    [Fact]
    public void Run_Success_DoesNotCallShutdown()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Accepted };

        b.Run();

        Assert.False(b.ShutdownCalled);
    }

    [Fact]
    public void Run_Success_WiresActivityFailedHandler()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Accepted };

        b.Run();

        Assert.True(b.WireActivityCalled);
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
        Assert.Equal("test", b.SavedLoginName);   // jméno z úspěšného loginu
    }

    [Fact]
    public void Run_LoginCancelled_DoesNotSavePrefs()
    {
        // Důvod: dokud nemáme úspěšný login + accepted licenci, nemá smysl ukládat LastIdfa.
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
        // Update check je až po SavePostLoginState - LastIdfa musí být uložená
        // i když shell hned skončí updatem (příští start ji načte).
        var b = new TestableBootstrapper { UpdateTriggered = true };

        b.Run();

        Assert.True(b.AcquireSessionCalled);
        Assert.NotNull(b.SavedIdfa);
        Assert.True(b.WireActivityCalled);
    }

    /// <summary>Volá skutečné kroky bez přepsání - jen ty, které na <c>Application</c> nesáhnou.</summary>
    private class RealStepsBootstrapper(StartupOptions options) : AppBootstrapper(options)
    {
        public void CallSave(ILocalPrefs prefs, string idfa, string? name)
            => SavePostLoginState(prefs, idfa, name);

        public ILocalPrefs Load() => LoadLocalPrefs();

        public bool CallTryLogin(out LoginResult login) =>
            TryLogin(null!, Substitute.For<ILocalPrefs>(), null!, out login);
    }

    private static StartupOptions E2E(params string[] args) => StartupOptions.Development(["e2e=1", .. args], null);

    [Fact]
    public void Run_TestRun_SkipsAutoLogin()
    {
        var b = new TestableBootstrapper(E2E()) { AutoLoginSucceeds = true };

        b.Run();

        Assert.False(b.TryAutoLoginCalled);
        Assert.True(b.TryLoginCalled);
    }

    [Fact]
    public void LoadLocalPrefs_TestRun_KeepsPrefsInMemory()
    {
        var prefs = new RealStepsBootstrapper(E2E()).Load();

        Assert.IsType<TransientLocalPrefs>(prefs);
        Assert.Null(prefs.LastIdfa);
    }

    [Fact]
    public void LoadLocalPrefs_TestRunWithIdfa_StartsInThatCompany()
    {
        // PickInitial bere LastIdfa jako preferovanou firmu - stejná cesta jako běžný start.
        var prefs = new RealStepsBootstrapper(E2E("idfa=CB_DP")).Load();

        Assert.Equal("CB_DP", prefs.LastIdfa);
    }

    [Fact]
    public void TryLogin_DevLogin_ReturnsItWithoutDialog()
    {
        // Bez Application: dialog větev by na ShutdownMode spadla, DevLogin na ni nesmí sáhnout.
        var options = StartupOptions.Development([], null);

        Assert.True(new RealStepsBootstrapper(options).CallTryLogin(out var login));
        Assert.Same(options.DevLogin, login);
    }

    [Fact]
    public void SavePostLoginState_DevLogin_DoesNotOverwriteLastLoginName()
    {
        // DevLogin je syntetický - jméno v local.json patří poslednímu ostrému přihlášení.
        var prefs = Substitute.For<ILocalPrefs>();

        new RealStepsBootstrapper(StartupOptions.Development([], null)).CallSave(prefs, "CB_DP", "jan.novak");

        prefs.Received().LastIdfa = "CB_DP";
        prefs.Received(1).Save();
        prefs.DidNotReceive().LastLoginName = Arg.Any<string?>();
    }

    [Fact]
    public void SavePostLoginState_DialogLogin_SavesLastLoginName()
    {
        var prefs = Substitute.For<ILocalPrefs>();

        new RealStepsBootstrapper(StartupOptions.Production()).CallSave(prefs, "CB_DP", "jan.novak");

        prefs.Received().LastIdfa = "CB_DP";
        prefs.Received().LastLoginName = "jan.novak";
        prefs.Received(1).Save();
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void MiniAppProviders_DevSamplesOnlyInDevelopment(bool devMiniApps, int devSamples)
    {
        var providers = AppBootstrapper.MiniAppProviders(new ModuleShortcutsService(), devMiniApps);

        Assert.Equal(devSamples, providers.OfType<MiniApps.DevSampleMiniApps>().Count());
        Assert.IsType<MiniApps.DesktopMiniAppProvider>(providers[0]);   // pořadí = pořadí skupin v menu
    }

    [Fact]
    public void Run_Success_CallsHandOffAfterBuildShell()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Accepted };

        b.Run();

        Assert.True(b.HandOffCalled);
        Assert.True(b.ShellBuiltBeforeHandOff);   // handoff váže ContentRendered na hotové okno
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

    // LogLogin = záznam do sestavy USERS „Přehled přihlášení" (legacy frmNoMDI.vb:4948).

    private static void AssertLogLogin(TestableBootstrapper b, int times) =>
        b.MLRest.Received(times).QueryOrThrow("ALVA.SYS.Login", "LogLogin",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == Environment.MachineName));

    [Fact]
    public void Run_Success_LogsLoginWithMachineName()
    {
        var b = new TestableBootstrapper();

        b.Run();

        AssertLogLogin(b, 1);
    }

    [Theory]
    [InlineData(LicenceAcquireOutcome.Refused)]
    [InlineData(LicenceAcquireOutcome.Cancelled)]
    public void Run_LicenceNotAccepted_DoesNotLogLogin(LicenceAcquireOutcome outcome)
    {
        var b = new TestableBootstrapper { SessionOutcome = outcome };

        b.Run();

        AssertLogLogin(b, 0);
    }

    [Fact]
    public void Run_UpdateTriggered_DoesNotLogLogin()
    {
        // Legacy start ukončený updatem (frmNoMDI.vb:4917) k LogLogin (:4948) nedošel.
        var b = new TestableBootstrapper { UpdateTriggered = true };

        b.Run();

        AssertLogLogin(b, 0);
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
        b.Prefs.LastIdfa.Returns("PI_DP");

        b.Run();

        Assert.Equal(new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"), b.EstablishedConnection);
    }

    [Fact]
    public void Run_LastCompanyAllowed_StartsThere()
    {
        var b = new TestableBootstrapper { AllowedCompanies = TwoAllowed };
        b.Prefs.LastIdfa.Returns("JI_DP");

        b.Run();

        Assert.Equal("JI_DP", b.EstablishedConnection?.Idfa);
    }

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
        b.Popup.Received(1).Info("Uživatel nemá přístup k žádné firmě.");
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
