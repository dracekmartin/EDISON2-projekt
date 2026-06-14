using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

public class CompanySwitcherTests
{
    private static void SetupVerifyLic(IMLRest mlRest, params int[] results)
    {
        var outputs = new object[results.Length][];
        for (int i = 0; i < results.Length; i++)
            outputs[i] = MLRestMocks.Output(results[i].ToString());
        MLRestMocks.SetupQuerySequence(mlRest, "ALVA.SYS.Login", "VerifyLic", outputs);
    }

    private static CompanySwitcher CreateSwitcher(
        IMLRest mlRest,
        Func<Lic?> licenceLoader,
        bool isAdmin = false,
        IPopupDisplayer? popup = null,
        IWindowNavigator? nav = null,
        ILocalPrefs? prefs = null,
        Action? closeAllWindows = null)
    {
        var services = new EDServices(
            mlRest,
            nav ?? Substitute.For<IWindowNavigator>(),
            popup ?? Substitute.For<IPopupDisplayer>())
        {
            IsAdmin = isAdmin,
            LicenceLoader = licenceLoader,
        };
        return new CompanySwitcher(services,
            prefs ?? Substitute.For<ILocalPrefs>(),
            closeAllWindows ?? (() => { }));
    }

    // ---------- PickInitial ----------

    [Fact]
    public void PickInitial_PreferredMatch_ReturnsMatch()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "CB_DP", IsDefault = true },
            new() { Id = "JI_DP", Database = "JI_DP" },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: "JI_DP");

        Assert.Equal("JI_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_PreferredNotFound_FallsBackToIsDefault()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "CB_DP" },
            new() { Id = "JI_DP", Database = "JI_DP", IsDefault = true },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: "GONE");

        Assert.Equal("JI_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_NullPreferred_FallsBackToIsDefault()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "CB_DP" },
            new() { Id = "JI_DP", Database = "JI_DP", IsDefault = true },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: null);

        Assert.Equal("JI_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_NoPreferred_NoDefault_ReturnsFirst()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "CB_DP" },
            new() { Id = "JI_DP", Database = "JI_DP" },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: null);

        Assert.Equal("CB_DP", pick.Id);
    }

    [Fact]
    public void PickInitial_PreferredOverridesIsDefault()
    {
        var companies = new CompanyStruct[]
        {
            new() { Id = "CB_DP", Database = "CB_DP", IsDefault = true },
            new() { Id = "JI_DP", Database = "JI_DP" },
        };

        var pick = CompanySwitcher.PickInitial(companies, preferredIdfa: "JI_DP");

        Assert.Equal("JI_DP", pick.Id);
    }

    // ---------- Switch — null licence (skip verify) ----------

    [Fact]
    public void Switch_NullLicence_AutoAccepts_NoVerifyCall()
    {
        var mlRest = MLRestMocks.Create();
        var switcher = CreateSwitcher(mlRest, () => null);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
    }

    [Fact]
    public void Switch_NullLicence_UpdatesMLRest()
    {
        var mlRest = MLRestMocks.Create();
        var switcher = CreateSwitcher(mlRest, () => null);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal("JI_DP", mlRest.Idfa);
        Assert.Equal("JI_DP", mlRest.DataNamespace);
    }

    // ---------- Switch — VerifyLic Accepted ----------

    [Fact]
    public void Switch_VerifyAccepted_ReturnsAccepted()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        Assert.Same(licence, result.Licence);
        Assert.Equal("JI_DP", mlRest.Idfa);
        Assert.Equal("JI_DP", mlRest.DataNamespace);
    }

    [Fact]
    public void Switch_Accepted_ReleasesOldFirmLock()
    {
        // Po úspěšném přepnutí se uvolní ACTIVITY zámek staré firmy (jinak drží její licenční slot).
        // Logout běží na klonu se starou idfa/namespace — ověřujeme přes mlRest.With(...).
        var mlRest = MLRestMocks.Create();   // start Idfa = CB_DP
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        mlRest.Received().With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP");
        Assert.Equal("JI_DP", mlRest.Idfa);   // sdílená session zůstala na nové firmě (klon ji nemění)
    }

    [Fact]
    public void Switch_NullLicence_DoesNotLogout()
    {
        // Bez licence (testy / pre-licence stav) se neodhlašuje — žádný reálný zámek staré firmy.
        var mlRest = MLRestMocks.Create();
        var switcher = CreateSwitcher(mlRest, () => null);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        mlRest.DidNotReceive().With(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // ---------- Switch — non-admin over-limit ----------

    [Fact]
    public void Switch_NonAdminOverLimit_Refused()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false, popup: popup);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Refused, result.Outcome);
        popup.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Fact]
    public void Switch_NonAdminOverLimit_RollsBackMLRest()
    {
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal("CB_DP", mlRest.Idfa);
        Assert.Equal("CB_DP", mlRest.DataNamespace);
    }

    // ---------- Switch — admin retry path ----------

    [Fact]
    public void Switch_AdminConfirmDenied_Cancelled()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(false);
        var nav = Substitute.For<IWindowNavigator>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup, nav: nav);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Cancelled, result.Outcome);
        nav.DidNotReceive().ShowLegacyAndWait(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void Switch_AdminConfirmDenied_RollsBackMLRest()
    {
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(false);
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal("CB_DP", mlRest.Idfa);
        Assert.Equal("CB_DP", mlRest.DataNamespace);
    }

    [Fact]
    public void Switch_AdminRetryAfterUsersDialog_Accepted()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var nav = Substitute.For<IWindowNavigator>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup, nav: nav);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        nav.Received(1).ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
        Assert.Equal("JI_DP", mlRest.Idfa);
    }

    // ---------- Switch — closeAllWindows side effect ----------

    [Fact]
    public void Switch_CallsCloseAllWindows_BeforeMLRestSwap()
    {
        var mlRest = MLRestMocks.Create();
        var closeAll = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => null, closeAllWindows: closeAll);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        closeAll.Received(1).Invoke();
    }

    [Fact]
    public void Switch_CallsCloseAllWindows_EvenOnRefused()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var closeAll = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false, closeAllWindows: closeAll);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        closeAll.Received(1).Invoke();
    }

    // ---------- Switch — prefs persistence ----------

    [Fact]
    public void Switch_Accepted_SavesLastIdfaToPrefs()
    {
        var mlRest = MLRestMocks.Create();
        var prefs = Substitute.For<ILocalPrefs>();
        var switcher = CreateSwitcher(mlRest, () => null, prefs: prefs);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        prefs.Received().LastIdfa = "JI_DP";
        prefs.Received(1).Save();
    }

    [Fact]
    public void Switch_Refused_DoesNotSavePrefs()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var prefs = Substitute.For<ILocalPrefs>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false, prefs: prefs);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        prefs.DidNotReceive().Save();
    }

    // ---------- Switch — licenceLoader interaction ----------

    [Fact]
    public void Switch_VerifyAccepted_UsesLoaderResultForCountsLogin()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var freshLicence = new Lic { CountsLOGIN = 42 };
        var switcher = CreateSwitcher(mlRest, () => freshLicence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: new Lic { CountsLOGIN = 5 });

        mlRest.Received().QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 42));
    }
}
