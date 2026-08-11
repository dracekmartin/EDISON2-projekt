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
    /// <remarks>
    /// Jede přes bezparametrický ctor, takže <c>Application</c> uvnitř je null. Nový krok
    /// v <see cref="AppBootstrapper.Run"/>, který na něj sáhne (dialog, Dispatcher, ShutdownMode),
    /// tady spadne na NRE až za běhu testu - takový krok patří za <c>protected virtual</c> hook.
    /// </remarks>
    private class TestableBootstrapper : AppBootstrapper
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
        public LicenceAcquireOutcome SessionOutcome { get; set; } = LicenceAcquireOutcome.Accepted;
        public IPopupDisplayer Popup { get; set; } = Substitute.For<IPopupDisplayer>();
        public IWindowNavigator Nav { get; set; } = Substitute.For<IWindowNavigator>();
        public IMLRest MLRest { get; set; } = MLRestMocks.Create();

        protected override (WindowNavigator Nav, PopupDisplayer Popup, EDServices Services) InitializeServices()
        {
            // Tady bychom v produkci založili EDServices.Initialize(services). Test mock
            // místo toho vrátí stub WindowNavigator + PopupDisplayer + EDServices se svými
            // mock referenced fields. EDServices.Initialize je side effect, ale test neuvolňuje
            // a další testy si přepíší.
            var nav = new WindowNavigator();
            var popup = new PopupDisplayer();
            // Wrap mock impls jako EDServices fields — base Run() je čte přes 'services' parametr.
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
        public bool TryLoginCalled { get; private set; }

        protected override bool TryAutoLogin(MLRest loginRest, ILocalPrefs prefs, out LoginResult login)
        {
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

        protected override (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session, EDServices services)
        {
            AcquireSessionCalled = true;
            return (SessionOutcome, null);
        }

        protected override void SavePostLoginState(ILocalPrefs prefs, string idfa, string? loginName)
        {
            SavedIdfa = idfa;
            SavedLoginName = loginName;
        }

        protected override void WireActivityFailedHandler(SessionContext session, EDServices services)
            => WireActivityCalled = true;

        public bool UpdateTriggered { get; set; }

        protected override (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) CheckForUpdate(
            SessionContext session, EDServices services) => (UpdateTriggered, null);

        protected override EdisonMainWindow BuildShellWindow(PreLoginContext prep, SessionContext session, Lic? licence,
            EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs,
            Task<ReleaseNoticeResult>? releaseNotice)
        {
            BuildShellCalled = true;
            return null!;
        }

        public bool HandOffCalled { get; private set; }
        public bool ShellBuiltBeforeHandOff { get; private set; }

        protected override void HandOffLoginWindow(EdisonMainWindow wnd)
        {
            HandOffCalled = true;
            ShellBuiltBeforeHandOff = BuildShellCalled;
        }

        protected override void Shutdown() => ShutdownCalled = true;
    }

    // ---------- First-launch setup ----------

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
    public void Run_Configured_ProceedsToLogin()
    {
        var b = new TestableBootstrapper { Configured = true };

        b.Run();

        Assert.True(b.EnsureConfiguredCalled);
        Assert.True(b.BuildShellCalled);
    }

    // ---------- Auto-login ----------

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

    // ---------- Vynucená změna hesla (feature #17) ----------

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

    // ---------- Path 1: Login cancelled ----------

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

        // EstablishSession by se nemělo dostat ke slovu — výsledkem by byl unused real MLRest.
        // Sledujeme přes AcquireSession (volá se po EstablishSession).
        Assert.False(b.AcquireSessionCalled);
        Assert.False(b.BuildShellCalled);
        Assert.False(b.WireActivityCalled);
    }

    // ---------- Path 2: Licence Refused ----------

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

    // ---------- Path 3: Licence Cancelled ----------

    [Fact]
    public void Run_LicenceCancelled_CallsShutdown_NoPopup()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Cancelled };

        var result = b.Run();

        Assert.Null(result);
        Assert.True(b.ShutdownCalled);
        // Cancelled je admin volba — žádná zpráva (admin už ví že rušil sám).
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

    // ---------- Path 4: Success ----------

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

    // ---------- Step ordering ----------

    [Fact]
    public void Run_LoginCancelled_DoesNotSavePrefs()
    {
        // Důvod: dokud nemáme úspěšný login + accepted licenci, nemá smysl ukládat LastIdfa.
        var b = new TestableBootstrapper { LoginSucceeds = false };

        b.Run();

        Assert.Null(b.SavedIdfa);
    }

    // ---------- CheckForUpdate ----------

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
        // Update check je až po SavePostLoginState — LastIdfa musí být uložená
        // i když shell hned skončí updatem (příští start ji načte).
        var b = new TestableBootstrapper { UpdateTriggered = true };

        b.Run();

        Assert.True(b.AcquireSessionCalled);
        Assert.NotNull(b.SavedIdfa);
        Assert.True(b.WireActivityCalled);
    }

    // ---------- Handoff login okna ----------

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

}
