using System;
using System.Collections.Generic;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Přihlášení jiného uživatele: login dialog, zavření staré session před založením nové a mapování
/// výsledku <see cref="SessionOpener"/>. Cestu po loginu ověřuje <see cref="SessionOpenerTests"/>;
/// tady běží skutečná s nahrazenými kroky na serveru.
/// </summary>
public class SessionSwitcherTests
{
    /// <summary>Sonda na wire delegát - do base ctoru nejde předat lambda nad <c>this</c>.</summary>
    private sealed class WireSpy
    {
        public IMLRest? Wired { get; private set; }
        public void Attach(IMLRest mlRest) => Wired = mlRest;
    }

    private sealed class TestableSessionSwitcher : SessionSwitcher
    {
        private readonly WireSpy _wire;

        private TestableSessionSwitcher(EDServices services, WireSpy wire, TestableSessionOpener opener,
                                        Func<bool> closeAllWindows, Action shutdown)
            : base(services, () => null, opener, closeAllWindows, shutdown)
        {
            _wire = wire;
            Opener = opener;
        }

        public static TestableSessionSwitcher Create(EDServices services, Func<bool> closeAllWindows, Action? shutdown = null)
        {
            var wire = new WireSpy();
            return new(services, wire, new TestableSessionOpener(services, wire.Attach), closeAllWindows, shutdown ?? (() => { }));
        }

        public TestableSessionOpener Opener { get; }
        public List<string> Calls => Opener.Calls;
        public IMLRest? WiredMLRest => _wire.Wired;
        public LoginResult? PromptResult { get; set; }
        public IMLRest? CapturedPromptConnection { get; private set; }
        public IMLRest? FlushedWith { get; private set; }

        protected override LoginResult? PromptLogin(IMLRest connection)
        {
            CapturedPromptConnection = connection;
            return PromptResult;
        }

        public bool PasswordChanged { get; set; } = true;
        public Exception? PasswordThrows { get; set; }
        public (IMLRest Connection, string UserName)? PasswordChange { get; private set; }

        protected override bool ForcePasswordChange(IMLRest connection, string userName)
        {
            Calls.Add("password");
            PasswordChange = (connection, userName);
            if (PasswordThrows is not null) throw PasswordThrows;
            return PasswordChanged;
        }

        protected override void FlushTelemetry(IMLRest? mlRest)
        {
            Calls.Add("flush");
            FlushedWith = mlRest;
        }

        protected override void LogoutOldSession(IMLRest? mlRest) => Calls.Add("logout");
    }

    private static EDServices Services()
    {
        var rest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        rest.Server.Returns("test-host");
        rest.Port.Returns(9999);
        rest.LoginName.Returns("alice");
        return new EDServices(rest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
    }

    private static LoginResult ReauthLogin() => new(7, "bob", "Bob", false, "", false, true, "");

    [Fact]
    public void Switch_PromptReturnsNull_ReturnsCancelled_NoSideEffects()
    {
        var services = Services();
        var originalMLRest = services.MLRest;
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(services, closeAll);
        s.PromptResult = null;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
        Assert.Empty(s.Calls);
        Assert.Same(originalMLRest, services.MLRest);
        Assert.Null(s.WiredMLRest);
    }

    [Fact]
    public void Switch_PromptReceivesCurrentConnection()
    {
        var services = Services();
        var s = TestableSessionSwitcher.Create(services, () => true);
        s.PromptResult = null;

        s.Switch();

        Assert.Same(services.MLRest, s.CapturedPromptConnection);
    }

    [Fact]
    public void Switch_LoginAndLicenceAccepted_ReturnsSuccess_ClosesWindows()
    {
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(Services(), closeAll);
        s.PromptResult = ReauthLogin();

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Success, outcome.Outcome);
        closeAll.Received(1).Invoke();
        Assert.Same(s.Opener.NewSessionMLRest, s.WiredMLRest);   // force-logout handler musí viset na nové session
        Assert.Same(s.Opener.AllowedCompanies, outcome.Companies);
    }

    [Fact]
    public void Switch_QueriesNewUserInCurrentCompany_SessionKeepsConnection()
    {
        // Mění se uživatel - smí-li do aktuální firmy, nová session zdědí server/port/namespace/idfa staré.
        var s = TestableSessionSwitcher.Create(Services(), () => true);
        var login = ReauthLogin();
        s.PromptResult = login;

        s.Switch();

        Assert.Equal(new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"), s.Opener.AllowedQuery?.Conn);
        Assert.Same(login, s.Opener.AllowedQuery?.Login);
        Assert.Equal(new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"), s.Opener.EstablishedConnection);
    }

    [Fact]
    public void Switch_WindowVetoedClose_Cancelled_WithoutSessionSwap()
    {
        // Veto okna (neuložená data) ruší switch před logoutem staré session - přeživší okno
        // by jinak pracovalo pod session, která už na serveru neexistuje.
        var services = Services();
        var originalMLRest = services.MLRest;
        var s = TestableSessionSwitcher.Create(services, () => false);
        s.PromptResult = ReauthLogin();

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        Assert.Equal(["allowed"], s.Calls);   // bez flush, logoutu i nové session
        Assert.Same(originalMLRest, services.MLRest);
        Assert.Null(s.WiredMLRest);
    }

    [Fact]
    public void Switch_Success_FlushesTelemetryThenLogsOutThenEstablishes()
    {
        // Deklarované pořadí: flush jde pod starou session (server bere userID z ní),
        // logout uvolní ACTIVITY zámek starého uživatele, teprve pak vzniká nová session.
        var services = Services();
        var originalMLRest = services.MLRest;
        var s = TestableSessionSwitcher.Create(services, () => true);
        s.PromptResult = ReauthLogin();

        s.Switch();

        Assert.Equal(["allowed", "flush", "logout", "establish", "acquire", "update"], s.Calls);
        Assert.Same(originalMLRest, s.FlushedWith);
    }

    [Fact]
    public void Switch_NoAllowedCompany_CancelledBeforeClosingWindows()
    {
        // Odmítnutí až po zavření oken a logoutu by uživatele připravilo o session i rozdělanou práci.
        var services = Services();
        var originalMLRest = services.MLRest;
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(services, closeAll);
        s.PromptResult = ReauthLogin();
        s.Opener.AllowedCompanies = [];

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
        Assert.Equal(["allowed"], s.Calls);
        Assert.Same(originalMLRest, services.MLRest);
    }

    // Vynucená změna hesla (feature #17) platí i pro přihlášení jiného uživatele (legacy frmNoMDI.vb:3320 → :734).

    [Fact]
    public void Switch_PasswordChangeCancelled_Cancelled_WindowsStayOpen()
    {
        var services = Services();
        var originalMLRest = services.MLRest;
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(services, closeAll);
        s.PromptResult = ReauthLogin() with { ChangePasswordNeeded = true };
        s.PasswordChanged = false;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
        Assert.Equal(["password"], s.Calls);
        Assert.Same(originalMLRest, services.MLRest);
        Assert.Null(s.WiredMLRest);
    }

    [Fact]
    public void Switch_PasswordChanged_ContinuesAsNewUser()
    {
        var s = TestableSessionSwitcher.Create(Services(), () => true);
        s.PromptResult = ReauthLogin() with { ChangePasswordNeeded = true };

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Success, outcome.Outcome);
        Assert.Equal("password", s.Calls[0]);
        Assert.Same(s.CapturedPromptConnection, s.PasswordChange?.Connection);
        Assert.Equal("bob", s.PasswordChange?.UserName);
    }

    [Theory]
    [InlineData(LicenceAcquireOutcome.Refused, SwitchUserResult.LicenceRefused)]
    [InlineData(LicenceAcquireOutcome.Cancelled, SwitchUserResult.LicenceCancelled)]
    public void Switch_LicenceNotAccepted_MapsOutcome_ShutdownLeftToCaller(
        LicenceAcquireOutcome licence, SwitchUserResult expected)
    {
        var shutdown = Substitute.For<Action>();
        var s = TestableSessionSwitcher.Create(Services(), () => true, shutdown);
        s.PromptResult = ReauthLogin();
        s.Opener.LicenceOutcome = licence;

        var outcome = s.Switch();

        Assert.Equal(expected, outcome.Outcome);
        shutdown.DidNotReceive().Invoke();   // hlášku a ukončení volí VM
    }

    [Fact]
    public void Switch_UpdateRequired_ShutsDownLikeStart()
    {
        // Legacy mnuPrgLogin_Click (frmNoMDI.vb:3324) po přepnutí kontroluje aktualizace a při vynucené končí.
        var shutdown = Substitute.For<Action>();
        var s = TestableSessionSwitcher.Create(Services(), () => true, shutdown);
        s.PromptResult = ReauthLogin();
        s.Opener.UpdateRequired = true;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.UpdateRequired, outcome.Outcome);
        shutdown.Received(1).Invoke();
    }

    [Fact]
    public void Switch_DoesNotStartReleaseNotice()
    {
        // Notice čte jen status bar nového shellu; po přepnutí by dotaz na release server přišel nazmar.
        var s = TestableSessionSwitcher.Create(Services(), () => true);
        s.PromptResult = ReauthLogin();

        s.Switch();

        Assert.False(s.Opener.NoticeRequested);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Switch_ThrowsAfterOldSessionClosed_ShutsDownAndRethrows(bool inUpdateCheck)
    {
        // Okna jsou zavřená a stará session odhlášená: VM by zůstal se stromem a právy starého uživatele.
        var shutdown = Substitute.For<Action>();
        var s = TestableSessionSwitcher.Create(Services(), () => true, shutdown);
        s.PromptResult = ReauthLogin();
        var failure = new InvalidOperationException("server");
        if (inUpdateCheck) s.Opener.UpdateCheckThrows = failure;
        else s.Opener.AcquireThrows = failure;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(s.Switch));
        shutdown.Received(1).Invoke();
    }

    [Fact]
    public void Switch_ThrowsBeforeOldSessionClosed_KeepsShellRunning()
    {
        var shutdown = Substitute.For<Action>();
        var closeAll = Substitute.For<Func<bool>>();
        var s = TestableSessionSwitcher.Create(Services(), closeAll, shutdown);
        s.PromptResult = ReauthLogin() with { ChangePasswordNeeded = true };
        s.PasswordThrows = new InvalidOperationException("server");

        Assert.Throws<InvalidOperationException>(s.Switch);
        shutdown.DidNotReceive().Invoke();
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
    }
}
