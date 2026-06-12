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
    private class TestableBootstrapper : AppBootstrapper
    {
        public bool ShutdownCalled { get; private set; }
        public bool BuildShellCalled { get; private set; }
        public string? SavedIdfa { get; private set; }
        public bool WireActivityCalled { get; private set; }
        public bool AcquireSessionCalled { get; private set; }

        // Configurable
        public bool LoginSucceeds { get; set; } = true;
        public LicenceAcquireOutcome SessionOutcome { get; set; } = LicenceAcquireOutcome.Accepted;
        public IPopupDisplayer Popup { get; set; } = Substitute.For<IPopupDisplayer>();
        public IWindowNavigator Nav { get; set; } = Substitute.For<IWindowNavigator>();
        public IMLRest MLRest { get; set; } = MLRestMocks.Create();
        public LoginResult? ReauthLoginResult { get; set; }
        public MLRest? CapturedPromptConnection { get; private set; }

        protected override LoginResult? PromptLoginForReauth(MLRest loginRest)
        {
            CapturedPromptConnection = loginRest;
            return ReauthLoginResult;
        }

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

        protected override bool TryLogin(MLRest loginRest, out LoginResult login)
        {
            login = new LoginResult(42, "test", "Test User", true, "", false, true, "");
            return LoginSucceeds;
        }

        protected override SessionContext EstablishSession(ConnectionInfo conn, LoginResult login, EDServices services)
            => new(MLRest, login, () => null);

        protected override (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session, EDServices services)
        {
            AcquireSessionCalled = true;
            return (SessionOutcome, null);
        }

        protected override void SavePostLoginState(ILocalPrefs prefs, string idfa) { SavedIdfa = idfa; }

        protected override void WireActivityFailedHandler(SessionContext session, EDServices services)
            => WireActivityCalled = true;

        public bool UpdateTriggered { get; set; }
        protected override bool CheckForUpdate(SessionContext session, EDServices services) => UpdateTriggered;

        protected override EdisonMainWindow BuildShellWindow(PreLoginContext prep, SessionContext session, Lic? licence,
            EDServices services, WindowNavigator nav, PopupDisplayer popup, ILocalPrefs prefs)
        {
            BuildShellCalled = true;
            return null!;
        }

        protected override void Shutdown() => ShutdownCalled = true;
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
    public void Run_Success_BuildsShellWindow()
    {
        var b = new TestableBootstrapper { SessionOutcome = LicenceAcquireOutcome.Accepted };

        b.Run();

        Assert.True(b.BuildShellCalled);
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

    // ---------- SwitchUser ----------

    private static EDServices ServicesWith(string server, int port, string db, string idfa, string loginName)
    {
        var inner = new MLRest(server, port, db, idfa) { LoginName = loginName };
        var adapter = new MLRestAdapter(inner);
        return new EDServices(adapter, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
    }

    [Fact]
    public void SwitchUser_PromptReturnsNull_ReturnsCancelled_NoSideEffects()
    {
        var b = new TestableBootstrapper { ReauthLoginResult = null };
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");
        var originalMLRest = services.MLRest;
        var closeAll = Substitute.For<Action>();

        var outcome = b.SwitchUser(services, closeAll);

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        Assert.Null(outcome.Licence);
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
        Assert.Same(originalMLRest, services.MLRest);
    }

    [Fact]
    public void SwitchUser_PromptReceivesCurrentConnection()
    {
        var b = new TestableBootstrapper { ReauthLoginResult = null };
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");

        b.SwitchUser(services, () => { });

        Assert.NotNull(b.CapturedPromptConnection);
        Assert.Equal("test-host", b.CapturedPromptConnection!.Server);
        Assert.Equal(9999, b.CapturedPromptConnection.Port);
        Assert.Equal("CB_DP", b.CapturedPromptConnection.DataNamespace);
        Assert.Equal("CB_DP", b.CapturedPromptConnection.Idfa);
    }

    private static LoginResult ReauthLogin() => new(7, "bob", "Bob", false, "", false, true, "");

    [Fact]
    public void SwitchUser_LoginAndLicenceAccepted_ReturnsSuccess_ClosesWindows()
    {
        var b = new TestableBootstrapper
        {
            ReauthLoginResult = ReauthLogin(),
            SessionOutcome = LicenceAcquireOutcome.Accepted,
        };
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");
        var closeAll = Substitute.For<Action>();

        var outcome = b.SwitchUser(services, closeAll);

        Assert.Equal(SwitchUserResult.Success, outcome.Outcome);
        closeAll.Received(1).Invoke();
        Assert.True(b.WireActivityCalled);
    }

    [Fact]
    public void SwitchUser_LicenceRefused_MapsToLicenceRefused()
    {
        var b = new TestableBootstrapper
        {
            ReauthLoginResult = ReauthLogin(),
            SessionOutcome = LicenceAcquireOutcome.Refused,
        };

        var outcome = b.SwitchUser(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => { });

        Assert.Equal(SwitchUserResult.LicenceRefused, outcome.Outcome);
        Assert.Null(outcome.Licence);
    }

    [Fact]
    public void SwitchUser_LicenceCancelled_MapsToLicenceCancelled()
    {
        var b = new TestableBootstrapper
        {
            ReauthLoginResult = ReauthLogin(),
            SessionOutcome = LicenceAcquireOutcome.Cancelled,
        };

        var outcome = b.SwitchUser(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => { });

        Assert.Equal(SwitchUserResult.LicenceCancelled, outcome.Outcome);
    }
}
