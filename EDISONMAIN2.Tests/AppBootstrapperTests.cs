using System.Collections.Generic;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow;
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
    private class TestableBootstrapper(TestRunOptions? testRun = null) : AppBootstrapper(testRun)
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

        protected override bool TryLogin(MLRest loginRest, ILocalPrefs prefs, out LoginResult login)
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

        protected override SessionContext EstablishSession(ConnectionInfo conn, LoginResult login, EDServices services)
            => new(MLRest, login, () => null);

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

        protected override EdisonMainWindow BuildShellWindow(PreLoginContext prep, SessionContext session, Lic? licence,
            EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs,
            Task<ReleaseNoticeResult>? releaseNotice)
        {
            if (BuildShellThrows is not null) throw BuildShellThrows;
            BuildShellCalled = true;
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

    private class RealSaveBootstrapper : AppBootstrapper
    {
        public void CallSave(ILocalPrefs prefs, string idfa, string? name)
            => SavePostLoginState(prefs, idfa, name);
    }

    private class PrefsBootstrapper(TestRunOptions? testRun) : AppBootstrapper(testRun)
    {
        public ILocalPrefs Load() => LoadLocalPrefs();
    }

    [Fact]
    public void Run_TestRun_SkipsAutoLoginAndUsesDebugLogin()
    {
        var b = new TestableBootstrapper(TestRunOptions.Parse(["e2e=1"])) { AutoLoginSucceeds = true };

        b.Run();

        Assert.False(b.TryAutoLoginCalled);
        Assert.True(b.TryLoginCalled);
    }

    [Fact]
    public void LoadLocalPrefs_TestRun_KeepsPrefsInMemory()
    {
        var prefs = new PrefsBootstrapper(TestRunOptions.Parse(["e2e=1"])).Load();

        Assert.IsType<TransientLocalPrefs>(prefs);
        Assert.Null(prefs.LastIdfa);
    }

    [Fact]
    public void SavePostLoginState_Debug_DoesNotOverwriteLastLoginName()
    {
        var prefs = Substitute.For<ILocalPrefs>();

        new RealSaveBootstrapper().CallSave(prefs, "CB_DP", "jan.novak");

        prefs.Received().LastIdfa = "CB_DP";
        prefs.Received(1).Save();
#if DEBUG
        // Debug login je syntetický - jméno v local.json patří poslednímu ostrému přihlášení.
        prefs.DidNotReceive().LastLoginName = Arg.Any<string?>();
#else
        prefs.Received().LastLoginName = "jan.novak";
#endif
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

}
