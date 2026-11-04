using System;
using System.Collections.Generic;
using System.Linq;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Cesta po loginu společná pro start a přihlášení jiného uživatele: pořadí kroků, zrušení v každém
/// z nich, update check a zápis přihlášení. Testy obou cest už jen ověřují, že ji volají.
/// </summary>
public class SessionOpenerTests
{
    private static readonly ConnectionInfo Query = new("test-host", 9999, "CB_DP", "CB_DP");

    private static LoginResult Login(bool changePassword = false) =>
        new(7, "bob", "Bob", false, "", changePassword, true, "");

    private static (TestableSessionOpener Opener, EDServices Services) Create()
    {
        var services = new EDServices(MLRestMocks.Create(), Substitute.For<IWindowNavigator>(),
                                      Substitute.For<IPopupDisplayer>());
        var calls = new List<string>();
        return (new TestableSessionOpener(services, _ => calls.Add("wire"), calls), services);
    }

    /// <summary>Kroky volajícího zapisuje do týchž <see cref="TestableSessionOpener.Calls"/>.
    /// <paramref name="pick"/> = volba ve výběru jiné firmy (dostane nabídku), bez něj uživatel výběr zruší.</summary>
    private static SessionOpenSteps Steps(TestableSessionOpener opener, bool passwordChanged = true,
                                          bool establish = true,
                                          Func<IReadOnlyList<CompanyStruct>, CompanyStruct?>? pick = null) => new()
    {
        ChangePassword = _ =>
        {
            opener.Calls.Add("password");
            return passwordChanged;
        },
        PickOtherCompany = (refused, offered) =>
        {
            opener.Calls.Add($"pick {refused.Id}");
            return pick?.Invoke(offered);
        },
        BeforeEstablish = () =>
        {
            opener.Calls.Add("before");
            return establish;
        },
        AfterAccepted = _ => opener.Calls.Add("accepted"),
    };

    private static void AssertLogLogin(IMLRest session, int times) =>
        session.Received(times).QueryOrThrow("ALVA.SYS.Login", "LogLogin",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == Environment.MachineName));

    [Fact]
    public void Open_Success_RunsStepsInOrder_LogsLoginOnNewSession()
    {
        var (opener, _) = Create();

        var result = opener.Open(Login(changePassword: true), Query, "CB_DP", Steps(opener));

        Assert.Equal(SessionOpenOutcome.Opened, result.Outcome);
        Assert.Equal(["password", "allowed", "before", "establish", "wire", "acquire", "accepted", "update"], opener.Calls);
        // Legacy zapisuje přihlášení až po update checku (frmNoMDI.vb:3324 před :3347).
        AssertLogLogin(opener.NewSessionMLRest, 1);
        Assert.Same(opener.NewSessionMLRest, result.Session?.MLRest);
    }

    [Fact]
    public void Open_NoPasswordFlag_SkipsDialog()
    {
        var (opener, _) = Create();

        opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.DoesNotContain("password", opener.Calls);
    }

    [Fact]
    public void Open_PasswordChangeCancelled_Cancelled_NothingElse()
    {
        var (opener, _) = Create();

        var result = opener.Open(Login(changePassword: true), Query, "CB_DP", Steps(opener, passwordChanged: false));

        Assert.Equal(SessionOpenOutcome.Cancelled, result.Outcome);
        Assert.Equal(["password"], opener.Calls);
        Assert.Null(result.Session);
    }

    [Fact]
    public void Open_NoAllowedCompany_InfoAndCancelled_BeforeCallerStep()
    {
        // Odmítnutí až po kroku volajícího by uživatele připravilo o okna a starou session.
        var (opener, services) = Create();
        opener.AllowedCompanies = [];

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.Equal(SessionOpenOutcome.Cancelled, result.Outcome);
        services.PopupDisplayer.Received(1).InfoSystem("Uživatel nemá přístup k žádné firmě.");
        Assert.Equal(["allowed"], opener.Calls);
    }

    [Fact]
    public void Open_CallerStepRefuses_Cancelled_NoSession()
    {
        var (opener, _) = Create();

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener, establish: false));

        Assert.Equal(SessionOpenOutcome.Cancelled, result.Outcome);
        Assert.Equal(["allowed", "before"], opener.Calls);
        Assert.Null(opener.EstablishedConnection);
    }

    [Theory]
    [InlineData(LicenceAcquireOutcome.Refused, SessionOpenOutcome.LicenceRefused)]
    [InlineData(LicenceAcquireOutcome.Cancelled, SessionOpenOutcome.LicenceCancelled)]
    public void Open_LicenceNotAccepted_SingleCompany_NoOfferNoUpdateCheckNoLogLogin(LicenceAcquireOutcome licence, SessionOpenOutcome expected)
    {
        // Jediná povolená firma: není co nabídnout, hlášku a konec volí volající jako dřív.
        var (opener, _) = Create();
        opener.LicenceOutcome = licence;

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(["allowed", "before", "establish", "wire", "acquire"], opener.Calls);
        AssertLogLogin(opener.NewSessionMLRest, 0);
    }

    private static readonly CompanyStruct Cb = new() { Id = "CB_DP", Database = "CB_DP" };
    private static readonly CompanyStruct Ji = new() { Id = "JI_DP", Database = "JI_DB" };
    private static readonly CompanyStruct Pi = new() { Id = "PI_DP", Database = "PI_DB" };

    [Theory]
    [InlineData(LicenceAcquireOutcome.Refused)]
    [InlineData(LicenceAcquireOutcome.Cancelled)]   // admin odmítl okno aktivity
    public void Open_LicenceNotAccepted_OtherCompanyPicked_SessionThere(LicenceAcquireOutcome first)
    {
        // Legacy admin po odmítnutí licence přepnul firmu z prázdného shellu (frmNoMDI.vb:4926), nový shell ji nabídne.
        var (opener, _) = Create();
        opener.AllowedCompanies = [Cb, Ji];
        opener.LicenceOutcomes.Enqueue(first);

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener, pick: offered => offered[0]));

        Assert.Equal(SessionOpenOutcome.Opened, result.Outcome);
        Assert.Equal(["allowed", "before", "establish", "wire", "acquire", "pick CB_DP", "allowed",
                      "establish", "wire", "acquire", "accepted", "update"], opener.Calls);
        Assert.Equal(new ConnectionInfo("test-host", 9999, "JI_DB", "JI_DP"), opener.EstablishedConnection);
        AssertLogLogin(opener.NewSessionMLRest, 1);
    }

    [Fact]
    public void Open_LicenceRefused_OfferExcludesRefusedCompany()
    {
        var (opener, _) = Create();
        opener.AllowedCompanies = [Cb, Ji, Pi];
        opener.LicenceOutcome = LicenceAcquireOutcome.Refused;
        IReadOnlyList<CompanyStruct>? offer = null;

        opener.Open(Login(), Query, "CB_DP", Steps(opener, pick: offered => { offer = offered; return null; }));

        Assert.Equal(["JI_DP", "PI_DP"], offer?.Select(c => c.Id));
    }

    [Fact]
    public void Open_OtherCompanyPicked_AllowedCompaniesReloadedThere()
    {
        // Funkční omezení DB je per IDFa, přepínač nové session musí nést seznam ze zvolené firmy.
        var (opener, _) = Create();
        opener.AllowedCompanies = [Cb, Ji];
        IReadOnlyList<CompanyStruct> inJi = [Ji, Pi];
        opener.AllowedByIdfa["JI_DP"] = inJi;
        opener.LicenceOutcomes.Enqueue(LicenceAcquireOutcome.Refused);

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener, pick: _ => Ji));

        Assert.Equal(new ConnectionInfo("test-host", 9999, "JI_DB", "JI_DP"), opener.AllowedQueries[^1]);
        Assert.Same(inJi, result.Companies);
    }

    [Fact]
    public void Open_PickedCompanyMissingInItsOwnList_SessionStillThere_AndAdded()
    {
        // PickForSession by tu vzal první povolenou, tedy zase odmítnutou firmu.
        var (opener, _) = Create();
        opener.AllowedCompanies = [Cb, Ji];
        opener.AllowedByIdfa["JI_DP"] = [Cb];
        opener.LicenceOutcomes.Enqueue(LicenceAcquireOutcome.Refused);

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener, pick: _ => Ji));

        Assert.Equal(SessionOpenOutcome.Opened, result.Outcome);
        Assert.Equal("JI_DP", opener.EstablishedConnection?.Idfa);
        Assert.Equal(["CB_DP", "JI_DP"], result.Companies?.Select(c => c.Id));
    }

    [Fact]
    public void Open_RefusedCompaniesNotOfferedAgain_NothingLeft_RefusedWithoutOffer()
    {
        // Dvě obsazené firmy se nesmí střídat dokola (každé kolo ukazuje hlášky licence).
        var (opener, _) = Create();
        opener.AllowedCompanies = [Cb, Ji, Pi];
        opener.LicenceOutcome = LicenceAcquireOutcome.Refused;
        var offers = new List<string[]>();

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener, pick: offered =>
        {
            offers.Add([.. offered.Select(c => c.Id)]);
            return offered[0];
        }));

        Assert.Equal(SessionOpenOutcome.LicenceRefused, result.Outcome);
        Assert.Equal([["JI_DP", "PI_DP"], ["PI_DP"]], offers);
        Assert.Equal(["CB_DP", "JI_DP", "PI_DP"], opener.EstablishedConnections.Select(c => c.Idfa));
    }

    [Fact]
    public void Open_OtherCompanyNotPicked_CancelledWithoutUpdateCheck()
    {
        // Dialog už důvod řekl, volající končí bez další hlášky.
        var (opener, _) = Create();
        opener.AllowedCompanies = [Cb, Ji];
        opener.LicenceOutcome = LicenceAcquireOutcome.Refused;

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.Equal(SessionOpenOutcome.LicenceCancelled, result.Outcome);
        Assert.Equal(["allowed", "before", "establish", "wire", "acquire", "pick CB_DP"], opener.Calls);
        AssertLogLogin(opener.NewSessionMLRest, 0);
    }

    [Fact]
    public void Open_UpdateRequired_AfterCallerStep_NoLogLogin()
    {
        // Legacy start ukončený updatem přihlášení nezapsal (frmNoMDI.vb:4917 před :4948), přepnutí uživatele stejně.
        var (opener, _) = Create();
        opener.UpdateRequired = true;

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.Equal(SessionOpenOutcome.UpdateRequired, result.Outcome);
        Assert.Equal(["allowed", "before", "establish", "wire", "acquire", "accepted", "update"], opener.Calls);
        AssertLogLogin(opener.NewSessionMLRest, 0);
    }

    [Fact]
    public void Open_Success_ReturnsAllowedCompaniesAndRequestedReleaseNotice()
    {
        var (opener, _) = Create();
        opener.ReleaseNotice = System.Threading.Tasks.Task.FromResult(new ReleaseNoticeResult(ReleaseNoticeOutcome.Unknown));

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener) with { StartsReleaseNotice = true });

        Assert.Same(opener.AllowedCompanies, result.Companies);
        Assert.True(opener.NoticeRequested);
        Assert.Same(opener.ReleaseNotice, result.ReleaseNotice);
    }

    [Fact]
    public void Open_WithoutNoticeRequest_DoesNotStartReleaseNotice()
    {
        var (opener, _) = Create();

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.False(opener.NoticeRequested);
        Assert.Null(result.ReleaseNotice);
    }

    [Fact]
    public void Open_QueriesAllowedCompaniesAsLoggedUser()
    {
        // Bez uživatele server nefiltruje a vrátí všechny firmy.
        var (opener, _) = Create();
        var login = Login();

        opener.Open(login, Query, "CB_DP", Steps(opener));

        Assert.Equal(Query, opener.AllowedQuery?.Conn);
        Assert.Same(login, opener.AllowedQuery?.Login);
    }

    [Fact]
    public void Open_PreferredCompanyAllowed_SessionThere()
    {
        var (opener, _) = Create();
        opener.AllowedCompanies = [new() { Id = "CB_DP", Database = "CB_DP" }, new() { Id = "JI_DP", Database = "JI_DB" }];

        opener.Open(Login(), Query, "JI_DP", Steps(opener));

        Assert.Equal(new ConnectionInfo("test-host", 9999, "JI_DB", "JI_DP"), opener.EstablishedConnection);
    }

    [Fact]
    public void Open_PreferredCompanyNotAllowed_SessionInFirstAllowed()
    {
        var (opener, _) = Create();
        opener.AllowedCompanies = [new() { Id = "JI_DP", Database = "JI_DB" }, new() { Id = "PI_DP", Database = "PI_DB" }];

        var result = opener.Open(Login(), Query, "CB_DP", Steps(opener));

        Assert.Equal(new ConnectionInfo("test-host", 9999, "JI_DB", "JI_DP"), opener.EstablishedConnection);
        Assert.Same(opener.AllowedCompanies, result.Companies);
    }

    [Fact]
    public void EstablishSession_UsesStartupOptions()
    {
        // Session přihlášení jiného uživatele musí mít GUID a verzi jako startovní - v dev jinak hromadí
        // zámky, E2E by shodil vývojářův shell.
        var services = new EDServices(MLRestMocks.Create(), Substitute.For<IWindowNavigator>(),
                                      Substitute.For<IPopupDisplayer>());
        var opener = new TestableSessionOpener(services, options: StartupOptions.Development(["e2e=1"], null));

        var inner = ((MLRestAdapter)opener.CallEstablish(Query, Login()).MLRest).Inner;

        Assert.Equal($"{TestRunOptions.WindowMarker}-{Environment.MachineName}-{Environment.UserName}", inner.GUID);
        Assert.Equal("", inner.EdisonVersion);
    }
}
