using System;
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
/// Přihlášení jiného uživatele: 4 výsledky (cancel v dialogu, licence Refused/Cancelled, success)
/// a co při nich smí / nesmí proběhnout. Test subclass přepisuje hooky sahající na server a WPF.
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

        private TestableSessionSwitcher(EDServices services, WireSpy wire, Func<bool> closeAllWindows)
            : base(services, () => null, wire.Attach, closeAllWindows,
                   Substitute.For<IWindowStateStore>()) => _wire = wire;

        public static TestableSessionSwitcher Create(EDServices services, Func<bool> closeAllWindows)
            => new(services, new WireSpy(), closeAllWindows);

        public IMLRest? WiredMLRest => _wire.Wired;
        public LoginResult? PromptResult { get; set; }
        public MLRest? CapturedPromptConnection { get; private set; }
        public LicenceAcquireOutcome SessionOutcome { get; set; } = LicenceAcquireOutcome.Accepted;
        public IMLRest NewSessionMLRest { get; set; } = MLRestMocks.Create();
        public readonly List<string> Calls = [];
        public IMLRest? FlushedWith { get; private set; }

        protected override LoginResult? PromptLogin(MLRest connection)
        {
            CapturedPromptConnection = connection;
            return PromptResult;
        }

        protected override void FlushTelemetry(IMLRest? mlRest)
        {
            Calls.Add("flush");
            FlushedWith = mlRest;
        }

        protected override void LogoutOldSession(IMLRest? mlRest) => Calls.Add("logout");

        public IReadOnlyList<CompanyStruct> AllowedCompanies { get; set; } = [new() { Id = "CB_DP", Database = "CB_DP" }];
        public (ConnectionInfo Conn, LoginResult Login)? AllowedQuery { get; private set; }

        protected override IReadOnlyList<CompanyStruct> LoadAllowedCompanies(ConnectionInfo conn, LoginResult login)
        {
            AllowedQuery = (conn, login);
            return AllowedCompanies;
        }

        protected override SessionContext EstablishSession(ConnectionInfo conn, LoginResult login)
        {
            Calls.Add("establish");
            CapturedConnection = conn;
            return new SessionContext(NewSessionMLRest, login, () => null);
        }

        public ConnectionInfo? CapturedConnection { get; private set; }

        protected override (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session)
            => (SessionOutcome, null);
    }

    private static EDServices ServicesWith(string server, int port, string db, string idfa, string loginName)
    {
        var inner = new MLRest(server, port, db, idfa) { LoginName = loginName };
        return new EDServices(new MLRestAdapter(inner), Substitute.For<IWindowNavigator>(),
                              Substitute.For<IPopupDisplayer>());
    }

    private static LoginResult ReauthLogin() => new(7, "bob", "Bob", false, "", false, true, "");

    [Fact]
    public void Switch_PromptReturnsNull_ReturnsCancelled_NoSideEffects()
    {
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");
        var originalMLRest = services.MLRest;
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(services, closeAll);
        s.PromptResult = null;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        Assert.Null(outcome.Licence);
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
        Assert.Same(originalMLRest, services.MLRest);
        Assert.Null(s.WiredMLRest);
    }

    [Fact]
    public void Switch_PromptReceivesCurrentConnection()
    {
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        s.PromptResult = null;

        s.Switch();

        Assert.NotNull(s.CapturedPromptConnection);
        Assert.Equal("test-host", s.CapturedPromptConnection!.Server);
        Assert.Equal(9999, s.CapturedPromptConnection.Port);
        Assert.Equal("CB_DP", s.CapturedPromptConnection.DataNamespace);
        Assert.Equal("CB_DP", s.CapturedPromptConnection.Idfa);
    }

    [Fact]
    public void Switch_LoginAndLicenceAccepted_ReturnsSuccess_ClosesWindows()
    {
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), closeAll);
        s.PromptResult = ReauthLogin();
        s.SessionOutcome = LicenceAcquireOutcome.Accepted;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Success, outcome.Outcome);
        closeAll.Received(1).Invoke();
        Assert.Same(s.NewSessionMLRest, s.WiredMLRest);   // force-logout handler musí viset na nové session
    }

    [Fact]
    public void Switch_NewSessionKeepsCompanyAndConnection()
    {
        // Mění se uživatel - smí-li do aktuální firmy, nová session zdědí server/port/namespace/idfa staré.
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        s.PromptResult = ReauthLogin();

        s.Switch();

        Assert.Equal(new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"), s.CapturedConnection);
    }

    [Fact]
    public void Switch_WindowVetoedClose_Cancelled_WithoutSessionSwap()
    {
        // Veto okna (neuložená data) ruší switch před logoutem staré session - přeživší okno
        // by jinak pracovalo pod session, která už na serveru neexistuje.
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");
        var originalMLRest = services.MLRest;
        var s = TestableSessionSwitcher.Create(services, () => false);
        s.PromptResult = ReauthLogin();

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        Assert.Same(originalMLRest, services.MLRest);
        Assert.Null(s.WiredMLRest);   // nová session nevznikla
    }

    [Fact]
    public void Switch_Success_FlushesTelemetryThenLogsOutThenEstablishes()
    {
        // Deklarované pořadí: flush jde pod starou session (server bere userID z ní),
        // logout uvolní ACTIVITY zámek starého uživatele, teprve pak vzniká nová session.
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");
        var originalMLRest = services.MLRest;
        var s = TestableSessionSwitcher.Create(services, () => true);
        s.PromptResult = ReauthLogin();

        s.Switch();

        Assert.Equal(["flush", "logout", "establish"], s.Calls);
        Assert.Same(originalMLRest, s.FlushedWith);
    }

    [Fact]
    public void Switch_CancelledPromptOrVeto_NoFlushNoLogout()
    {
        var cancelled = TestableSessionSwitcher.Create(
            ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        cancelled.PromptResult = null;
        cancelled.Switch();
        Assert.Empty(cancelled.Calls);

        var vetoed = TestableSessionSwitcher.Create(
            ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => false);
        vetoed.PromptResult = ReauthLogin();
        vetoed.Switch();
        Assert.Empty(vetoed.Calls);
    }

    [Fact]
    public void Switch_LicenceRefused_MapsToLicenceRefused()
    {
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        s.PromptResult = ReauthLogin();
        s.SessionOutcome = LicenceAcquireOutcome.Refused;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.LicenceRefused, outcome.Outcome);
        Assert.Null(outcome.Licence);
    }

    [Fact]
    public void Switch_LicenceCancelled_MapsToLicenceCancelled()
    {
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        s.PromptResult = ReauthLogin();
        s.SessionOutcome = LicenceAcquireOutcome.Cancelled;

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.LicenceCancelled, outcome.Outcome);
    }

    [Theory]
    [InlineData(LicenceAcquireOutcome.Accepted, 1)]
    [InlineData(LicenceAcquireOutcome.Refused, 0)]
    [InlineData(LicenceAcquireOutcome.Cancelled, 0)]
    public void Switch_LogsLoginOnlyWhenAccepted_OnNewSession(LicenceAcquireOutcome outcome, int expected)
    {
        // Legacy mnuPrgLogin_Click (frmNoMDI.vb:3347) zapisuje přihlášení nového uživatele do sestavy USERS.
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        s.PromptResult = ReauthLogin();
        s.SessionOutcome = outcome;

        s.Switch();

        s.NewSessionMLRest.Received(expected).QueryOrThrow("ALVA.SYS.Login", "LogLogin",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == Environment.MachineName));
    }

    // Povolené firmy nového uživatele (legacy mnuPrgLogin_Click → frmLoginEx s checkAccess).

    [Fact]
    public void Switch_QueriesAllowedCompaniesAsNewUser_InCurrentCompany()
    {
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        var login = ReauthLogin();
        s.PromptResult = login;

        s.Switch();

        Assert.Equal(new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"), s.AllowedQuery?.Conn);
        Assert.Same(login, s.AllowedQuery?.Login);
    }

    [Fact]
    public void Switch_CurrentCompanyNotAllowed_SessionInFirstAllowed()
    {
        var s = TestableSessionSwitcher.Create(ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice"), () => true);
        s.PromptResult = ReauthLogin();
        s.AllowedCompanies = [new() { Id = "JI_DP", Database = "JI_DB" }, new() { Id = "PI_DP", Database = "PI_DB" }];

        var outcome = s.Switch();

        Assert.Equal(new ConnectionInfo("test-host", 9999, "JI_DB", "JI_DP"), s.CapturedConnection);
        Assert.Same(s.AllowedCompanies, outcome.Companies);
    }

    [Fact]
    public void Switch_NoAllowedCompany_CancelledBeforeClosingWindows()
    {
        // Odmítnutí až po zavření oken a logoutu by uživatele připravilo o session i rozdělanou práci.
        var services = ServicesWith("test-host", 9999, "CB_DP", "CB_DP", "alice");
        var originalMLRest = services.MLRest;
        var closeAll = Substitute.For<Func<bool>>();
        closeAll.Invoke().Returns(true);
        var s = TestableSessionSwitcher.Create(services, closeAll);
        s.PromptResult = ReauthLogin();
        s.AllowedCompanies = [];

        var outcome = s.Switch();

        Assert.Equal(SwitchUserResult.Cancelled, outcome.Outcome);
        services.PopupDisplayer.Received(1).Info("Uživatel nemá přístup k žádné firmě.");
        closeAll.DidNotReceiveWithAnyArgs().Invoke();
        Assert.Empty(s.Calls);
        Assert.Same(originalMLRest, services.MLRest);
    }
}
