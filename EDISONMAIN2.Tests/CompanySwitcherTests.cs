using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

public class CompanySwitcherTests
{
    private static IMLRest MockMLRest(string idfa = "CB_DP", string ns = "DB_CB")
    {
        var mock = Substitute.For<IMLRest>();
        mock.Idfa.Returns(idfa);
        mock.DataNamespace.Returns(ns);
        return mock;
    }

    private static void SetupVerifyLic(IMLRest mlRest, params int[] results)
    {
        var returns = new object[results.Length][];
        for (int i = 0; i < results.Length; i++)
            returns[i] = new object[] { "", results[i].ToString() };

        if (returns.Length == 1)
        {
            mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
                  .Returns(returns[0]);
        }
        else
        {
            mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
                  .Returns(returns[0], returns[1..]);
        }
    }

    private static CompanySwitcher CreateSwitcher(
        IMLRest mlRest,
        Func<Lic?> licenceLoader,
        bool isAdmin = false,
        IPopupDisplayer? popup = null,
        IWindowNavigator? nav = null,
        Action? closeAllWindows = null) =>
        new(mlRest, licenceLoader, isAdmin,
            popup ?? Substitute.For<IPopupDisplayer>(),
            nav ?? Substitute.For<IWindowNavigator>(),
            closeAllWindows ?? (() => { }));

    // ---------- PickInitial ----------

    [Fact]
    public void PickInitial_PreferredMatch_ReturnsMatch()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "DB_CB", IsDefault = true },
            new() { Id = "JI_DP", Database = "DB_JI" },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: "JI_DP");

        Assert.Equal("JI_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_PreferredNotFound_FallsBackToIsDefault()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "DB_CB" },
            new() { Id = "JI_DP", Database = "DB_JI", IsDefault = true },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: "GONE");

        Assert.Equal("JI_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_NullPreferred_FallsBackToIsDefault()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "DB_CB" },
            new() { Id = "JI_DP", Database = "DB_JI", IsDefault = true },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: null);

        Assert.Equal("JI_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_NoPreferred_NoDefault_ReturnsFirst()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "DB_CB" },
            new() { Id = "JI_DP", Database = "DB_JI" },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: null);

        Assert.Equal("CB_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_PreferredOverridesIsDefault()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "DB_CB", IsDefault = true },
            new() { Id = "JI_DP", Database = "DB_JI" },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: "JI_DP");

        Assert.Equal("JI_DP", pick.Id);
    }

    // ---------- Switch — null licence (skip verify) ----------

    [Fact]
    public void Switch_NullLicence_AutoAccepts_NoVerifyCall()
    {
        var mlRest = MockMLRest();
        var switcher = CreateSwitcher(mlRest, () => null);

        var result = switcher.Switch("JI_DP", "DB_JI", currentLicence: null);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
    }

    [Fact]
    public void Switch_NullLicence_UpdatesMLRest()
    {
        var mlRest = MockMLRest();
        var switcher = CreateSwitcher(mlRest, () => null);

        switcher.Switch("JI_DP", "DB_JI", currentLicence: null);

        Assert.Equal("JI_DP", mlRest.Idfa);
        Assert.Equal("DB_JI", mlRest.DataNamespace);
    }

    // ---------- Switch — VerifyLic Accepted ----------

    [Fact]
    public void Switch_VerifyAccepted_ReturnsAccepted()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        var result = switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        Assert.Same(licence, result.Licence);
        Assert.Equal("JI_DP", mlRest.Idfa);
        Assert.Equal("DB_JI", mlRest.DataNamespace);
    }

    // ---------- Switch — non-admin over-limit ----------

    [Fact]
    public void Switch_NonAdminOverLimit_Refused()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false, popup: popup);

        var result = switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Refused, result.Outcome);
        popup.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Fact]
    public void Switch_NonAdminOverLimit_RollsBackMLRest()
    {
        var mlRest = MockMLRest(idfa: "CB_DP", ns: "DB_CB");
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false);

        switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        Assert.Equal("CB_DP", mlRest.Idfa);
        Assert.Equal("DB_CB", mlRest.DataNamespace);
    }

    // ---------- Switch — admin retry path ----------

    [Fact]
    public void Switch_AdminConfirmDenied_Cancelled()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(false);
        var nav = Substitute.For<IWindowNavigator>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup, nav: nav);

        var result = switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Cancelled, result.Outcome);
        nav.DidNotReceive().ShowLegacyAndWait(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void Switch_AdminConfirmDenied_RollsBackMLRest()
    {
        var mlRest = MockMLRest(idfa: "CB_DP", ns: "DB_CB");
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(false);
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup);

        switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        Assert.Equal("CB_DP", mlRest.Idfa);
        Assert.Equal("DB_CB", mlRest.DataNamespace);
    }

    [Fact]
    public void Switch_AdminRetryAfterUsersDialog_Accepted()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, -1, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var nav = Substitute.For<IWindowNavigator>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup, nav: nav);

        var result = switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        nav.Received(1).ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
        Assert.Equal("JI_DP", mlRest.Idfa);
    }

    // ---------- Switch — closeAllWindows side effect ----------

    [Fact]
    public void Switch_CallsCloseAllWindows_BeforeMLRestSwap()
    {
        var mlRest = MockMLRest();
        var closeAll = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => null, closeAllWindows: closeAll);

        switcher.Switch("JI_DP", "DB_JI", currentLicence: null);

        closeAll.Received(1).Invoke();
    }

    [Fact]
    public void Switch_CallsCloseAllWindows_EvenOnRefused()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var closeAll = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false, closeAllWindows: closeAll);

        switcher.Switch("JI_DP", "DB_JI", currentLicence: licence);

        closeAll.Received(1).Invoke();
    }

    // ---------- Switch — licenceLoader interaction ----------

    [Fact]
    public void Switch_VerifyAccepted_UsesLoaderResultForCountsLogin()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, 1);
        var freshLicence = new Lic { CountsLOGIN = 42 };
        var switcher = CreateSwitcher(mlRest, () => freshLicence);

        switcher.Switch("JI_DP", "DB_JI", currentLicence: new Lic { CountsLOGIN = 5 });

        mlRest.Received().QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 42));
    }
}
