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
        Func<bool>? closeShellWindows = null,
        Action? closeLegacyModules = null)
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
            closeShellWindows ?? (() => true),
            closeLegacyModules ?? (() => { }));
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
    public void Switch_SameFirm_DoesNotLogout()
    {
        // Přepnutí na tutéž firmu (originalIdfa == newIdfa) nesmí odhlásit — jinak by shell
        // uvolnil vlastní ACTIVITY zámek pod aktuální Idfa.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        var result = switcher.Switch("CB_DP", "CB_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result.Outcome);
        mlRest.DidNotReceive().With(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>());
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

    [Fact]
    public void Switch_Refused_CarriesFreshTargetLicence()
    {
        // Na Refused vrací SwitchOutcome čerstvě načtenou licenci CÍLOVÉ firmy (z loaderu),
        // ne původní currentLicence — LicenceVerifier vrací licenceLoader() i při odmítnutí.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, -1);
        var currentLicence = new Lic { CountsLOGIN = 5 };
        var freshLicence = new Lic { CountsLOGIN = 99 };
        var switcher = CreateSwitcher(mlRest, () => freshLicence, isAdmin: false);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence);

        Assert.Equal(LicenceAcquireOutcome.Refused, result.Outcome);
        Assert.Same(freshLicence, result.Licence);
        Assert.NotSame(currentLicence, result.Licence);
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

    // ---------- Switch - zavírání oken a modulů ----------

    [Fact]
    public void Switch_ZavreShellOkna_PredSwapemMLRest()
    {
        // WPF okna shellu volají server přes sdílený MLRest - zavření (save) musí proběhnout
        // ještě pod starou Idfa, jinak by requesty šly do cizí firmy.
        var mlRest = MLRestMocks.Create();   // start Idfa = CB_DP
        string? idfaAtClose = null;
        var switcher = CreateSwitcher(mlRest, () => null,
                                      closeShellWindows: () => { idfaAtClose = mlRest.Idfa; return true; });

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal("CB_DP", idfaAtClose);
    }

    [Fact]
    public void Switch_Refused_NechaLegacyModulyBezet()
    {
        // Neúspěšné přepnutí nesmí zabít rozpracovanou práci v legacy modulech - mají vlastní
        // spojení se starou Idfa a shell zůstává na původní firmě. Shell okna se obětují
        // (musí se zavřít před swapem), legacy až po Accepted.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var closeShell = Substitute.For<Func<bool>>();
        closeShell.Invoke().Returns(true);
        var closeLegacy = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false,
                                      closeShellWindows: closeShell, closeLegacyModules: closeLegacy);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        closeShell.Received(1).Invoke();
        closeLegacy.DidNotReceive().Invoke();
    }

    [Fact]
    public void Switch_AdminCancelled_NechaLegacyModulyBezet()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(false);
        var closeLegacy = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: true, popup: popup,
                                      closeLegacyModules: closeLegacy);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        closeLegacy.DidNotReceive().Invoke();
    }

    [Fact]
    public void Switch_Accepted_ZavreLegacyModulyPredLogoutem()
    {
        // Pořadí kill → logout je nutné: moduly sdílejí shell GUID s CacheLocks=1, po zániku
        // ACTIVITY zámku staré firmy by jim padal každý request na LockExist.
        var mlRest = MLRestMocks.Create();   // start Idfa = CB_DP
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var closeLegacy = Substitute.For<Action>();
        var switcher = CreateSwitcher(mlRest, () => licence, closeLegacyModules: closeLegacy);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Received.InOrder(() =>
        {
            closeLegacy.Invoke();
            mlRest.With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP");
        });
    }

    [Fact]
    public void Switch_OknoVetovaloZavreni_CancelledBezeZmeny()
    {
        // Přeživší okno by přes sdílený MLRest mluvilo s cizí firmou - veto (e.Cancel
        // v Closing, typicky neuložená data) proto ruší switch ještě před swapem Idfa.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        var licence = new Lic { CountsLOGIN = 5 };
        var closeLegacy = Substitute.For<Action>();
        var prefs = Substitute.For<ILocalPrefs>();
        var switcher = CreateSwitcher(mlRest, () => licence, prefs: prefs,
                                      closeShellWindows: () => false, closeLegacyModules: closeLegacy);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Cancelled, result.Outcome);
        Assert.Equal("CB_DP", mlRest.Idfa);
        Assert.Equal("CB_DP", mlRest.DataNamespace);
        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
        closeLegacy.DidNotReceive().Invoke();
        prefs.DidNotReceive().Save();
    }

    // ---------- Switch — CacheLocks režim ----------

    [Fact]
    public void Switch_VerifyLicBeziBezCacheLocks_PoAcceptedJeJedna()
    {
        // Zámek nové firmy vznikne až ve VerifyLic - request pod novou Idfa s Locks=1
        // by padl na LockExist (EdisonActivityFailed → shutdown).
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        int? locksAtVerify = null;
        mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
              .Returns(_ => { locksAtVerify = mlRest.CacheLocks; return MLRestMocks.Output("1"); });
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(0, locksAtVerify);
        Assert.Equal(1, mlRest.CacheLocks);   // nastavil LicenceVerifier po Accepted
    }

    [Fact]
    public void Switch_Refused_RestoresCacheLocks()
    {
        // Zámek staré firmy pořád existuje - session se vrací do původního režimu.
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(1, mlRest.CacheLocks);
    }

    [Fact]
    public void Switch_NullLicence_KeepsCacheLocks()
    {
        // Verify přeskočen → LicenceVerifier režim nenastavil, musí se vrátit původní.
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        var switcher = CreateSwitcher(mlRest, () => null);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal(1, mlRest.CacheLocks);
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
