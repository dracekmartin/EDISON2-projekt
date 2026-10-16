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
        Action? closeLegacyModules = null,
        Action<IMLRest?>? flushTelemetry = null) =>
        CreateSwitcher(mlRest, licenceLoader, out _, isAdmin, popup, nav, prefs, closeShellWindows, closeLegacyModules,
                       flushTelemetry);

    private static CompanySwitcher CreateSwitcher(
        IMLRest mlRest,
        Func<Lic?> licenceLoader,
        out EDServices services,
        bool isAdmin = false,
        IPopupDisplayer? popup = null,
        IWindowNavigator? nav = null,
        ILocalPrefs? prefs = null,
        Func<bool>? closeShellWindows = null,
        Action? closeLegacyModules = null,
        Action<IMLRest?>? flushTelemetry = null)
    {
        services = new EDServices(
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
            closeLegacyModules ?? (() => { }),
            // Skutečný flush čeká na zápisy na pozadí ze sdíleného bufferu celého procesu testů.
            flushTelemetry ?? (_ => { }));
    }

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

    // PickForSession: povolené firmy dotazované per firma (funkční omezení DB je per IDFa).

    private static readonly ConnectionInfo QueryInCb = new("host", 1, "CB_DB", "CB_DP");

    private static CompanyStruct Company(string id) => new() { Id = id, Database = id + "_DB" };

    [Fact]
    public void PickForSession_QueriedCompanyAllowed_SingleQuery()
    {
        var queries = new List<ConnectionInfo>();
        CompanyStruct[] allowed = [Company("CB_DP"), Company("JI_DP")];

        var picked = CompanySwitcher.PickForSession(QueryInCb, "CB_DP", q => { queries.Add(q); return allowed; });

        Assert.Equal("CB_DP", picked?.Company.Id);
        Assert.Same(allowed, picked?.Allowed);
        Assert.Equal([QueryInCb], queries);
    }

    [Fact]
    public void PickForSession_OtherCompanyPicked_ReloadsListInIt()
    {
        // Seznam z firmy dotazu by nesl funkční omezení cizí firmy (legacy CreateCompanyMenu až ve výsledné).
        var queries = new List<ConnectionInfo>();
        CompanyStruct[] inCb = [Company("JI_DP"), Company("PI_DP")];
        CompanyStruct[] inJi = [Company("JI_DP")];

        var picked = CompanySwitcher.PickForSession(QueryInCb, "CB_DP",
            q => { queries.Add(q); return q.Idfa == "CB_DP" ? inCb : inJi; });

        Assert.Equal("JI_DP", picked?.Company.Id);
        Assert.Same(inJi, picked?.Allowed);
        Assert.Equal([QueryInCb, QueryInCb with { Database = "JI_DP_DB", Idfa = "JI_DP" }], queries);
    }

    [Fact]
    public void PickForSession_PickedCompanyMissingAfterReload_StaysInList()
    {
        // Index přepínače musí odpovídat MLRest.Idfa - firma session ze seznamu nezmizí.
        CompanyStruct[] inCb = [Company("JI_DP")];
        CompanyStruct[] inJi = [Company("PI_DP")];

        var picked = CompanySwitcher.PickForSession(QueryInCb, null, q => q.Idfa == "CB_DP" ? inCb : inJi);

        Assert.Equal(["PI_DP", "JI_DP"], picked?.Allowed.Select(c => c.Id));
    }

    [Fact]
    public void PickForSession_NothingAllowed_Null()
    {
        Assert.Null(CompanySwitcher.PickForSession(QueryInCb, "CB_DP", _ => []));
    }

    [Fact]
    public void LoginProbe_CarriesLoggedUserAndQueriedCompany()
    {
        var login = new LoginResult(42, "novak", "Jan Novák", false, "", false, false, "");

        var probe = CompanySwitcher.LoginProbe(QueryInCb, login);

        Assert.Equal(("host", 1, "CB_DB", "CB_DP"), (probe.Server, probe.Port, probe.DataNamespace, probe.Idfa));
        Assert.Equal(("42", "novak"), (probe.Mlp, probe.LoginName));
    }

    [Fact]
    public void Switch_NullLicence_AutoAccepts_NoVerifyCall()
    {
        var mlRest = MLRestMocks.Create();
        var switcher = CreateSwitcher(mlRest, () => null);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result);
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

    // Licence cílové firmy je ve službách hned po Accepted, ne až po přestavbě shellu - launch
    // argumenty agend a HasAccess ji čtou spolu s MLRest.Idfa.
    [Fact]
    public void Switch_VerifyAccepted_ActiveLicenceIsTargetFirms()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var currentLicence = new Lic { CountsLOGIN = 5 };
        var targetLicence = new Lic { CountsLOGIN = 9 };
        var switcher = CreateSwitcher(mlRest, () => targetLicence, out var services);
        services.ActiveLicence = currentLicence;

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result);
        Assert.Same(targetLicence, services.ActiveLicence);
        Assert.Equal("JI_DP", mlRest.Idfa);
        Assert.Equal("JI_DP", mlRest.DataNamespace);
    }

    [Fact]
    public void Switch_Accepted_DoesNotLogLogin()
    {
        // Parita: legacy SelectDatabaseHandler (frmNoMDI.vb:2934) do sestavy přihlášení nezapisuje.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "LogLogin", Arg.Any<object[]>());
    }

    [Fact]
    public void Switch_Accepted_ReleasesOldFirmLock()
    {
        // Po úspěšném přepnutí se uvolní ACTIVITY zámek staré firmy (jinak drží její licenční slot).
        // Logout běží na klonu se starou idfa/namespace - ověřujeme přes mlRest.With(...).
        var mlRest = MLRestMocks.Create();   // start Idfa = CB_DP
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        mlRest.Received().With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP");
        Assert.Equal("JI_DP", mlRest.Idfa);   // sdílená session zůstala na nové firmě (klon ji nemění)
    }

    [Fact]
    public void Switch_SupportLogin_AcceptedWithoutLock()
    {
        // Podpora (_mline) jede bez zámku: přepnutí nesmí zavolat VerifyLic ani vrátit session na Locks=1.
        var mlRest = MLRestMocks.Create();
        mlRest.LoginName.Returns("_mline");
        mlRest.UserID.Returns("_mline");
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result);
        Assert.Equal(0, mlRest.CacheLocks);
        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
    }

    [Fact]
    public void Switch_SameFirm_DoesNotLogout()
    {
        // Přepnutí na tutéž firmu (originalIdfa == newIdfa) nesmí odhlásit - jinak by shell
        // uvolnil vlastní ACTIVITY zámek pod aktuální Idfa.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        var result = switcher.Switch("CB_DP", "CB_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result);
        mlRest.DidNotReceive().With(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public void Switch_NullLicence_DoesNotLogout()
    {
        // Bez licence (testy / pre-licence stav) se neodhlašuje - žádný reálný zámek staré firmy.
        var mlRest = MLRestMocks.Create();
        var switcher = CreateSwitcher(mlRest, () => null);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        mlRest.DidNotReceive().With(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public void Switch_NonAdminOverLimit_Refused()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var licence = new Lic { CountsLOGIN = 5 };
        var popup = Substitute.For<IPopupDisplayer>();
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false, popup: popup);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Refused, result);
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
    public void Switch_Refused_KeepsCurrentLicence()
    {
        // Loader už načetl licenci cílové firmy, ale session zůstala na původní - licence s ní.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, -1);
        var currentLicence = new Lic { CountsLOGIN = 5 };
        var freshLicence = new Lic { CountsLOGIN = 99 };
        var switcher = CreateSwitcher(mlRest, () => freshLicence, out var services, isAdmin: false);
        services.ActiveLicence = currentLicence;

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence);

        Assert.Equal(LicenceAcquireOutcome.Refused, result);
        Assert.Same(currentLicence, services.ActiveLicence);
    }

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

        Assert.Equal(LicenceAcquireOutcome.Cancelled, result);
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

        Assert.Equal(LicenceAcquireOutcome.Accepted, result);
        nav.Received(1).ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
        Assert.Equal("JI_DP", mlRest.Idfa);
    }

    [Fact]
    public void Switch_ClosesShellWindows_BeforeMLRestSwap()
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
    public void Switch_FlushesBackgroundWritesAfterClosingWindows_UnderOldCompany()
    {
        // Zavřená okna dopisují na pozadí pod starou firmou; po jejím odhlášení by server zápis odmítl (LockExist).
        var mlRest = MLRestMocks.Create();   // start Idfa = CB_DP
        mlRest.CacheLocks = 1;
        var calls = new List<string>();
        IMLRest? flushed = null;
        var switcher = CreateSwitcher(mlRest, () => null,
            closeShellWindows: () => { calls.Add("close"); return true; },
            flushTelemetry: rest =>
            {
                flushed = rest;
                calls.Add($"flush {rest?.Idfa} locks={rest?.CacheLocks}");
            });

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal(["close", "flush CB_DP locks=1"], calls);
        Assert.Same(mlRest, flushed);
    }

    [Fact]
    public void Switch_Refused_LeavesLegacyModulesRunning()
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
    public void Switch_AdminCancelled_LeavesLegacyModulesRunning()
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
    public void Switch_Accepted_ClosesLegacyModulesBeforeLogout()
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
    public void Switch_WindowVetoedClose_CancelledWithoutChange()
    {
        // Přeživší okno by přes sdílený MLRest mluvilo s cizí firmou - veto (e.Cancel
        // v Closing, typicky neuložená data) proto ruší switch ještě před swapem Idfa.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        var licence = new Lic { CountsLOGIN = 5 };
        var closeLegacy = Substitute.For<Action>();
        var prefs = Substitute.For<ILocalPrefs>();
        var flush = Substitute.For<Action<IMLRest?>>();
        var switcher = CreateSwitcher(mlRest, () => licence, prefs: prefs,
                                      closeShellWindows: () => false, closeLegacyModules: closeLegacy,
                                      flushTelemetry: flush);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Cancelled, result);
        Assert.Equal("CB_DP", mlRest.Idfa);
        Assert.Equal("CB_DP", mlRest.DataNamespace);
        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
        closeLegacy.DidNotReceive().Invoke();
        prefs.DidNotReceive().Save();
        flush.DidNotReceiveWithAnyArgs().Invoke(default);
    }

    [Fact]
    public void Switch_VerifyLicRunsWithoutCacheLocks_SingleCallAfterAccepted()
    {
        // Zámek nové firmy vznikne až ve VerifyLic - request pod novou Idfa s Locks=1
        // by padl na LockExist (EdisonActivityFailed → shutdown).
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        int? locksAtVerify = null;
        mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
              .Returns(_ => { locksAtVerify = mlRest.CacheLocks; return MLRestMocks.Output("1"); });
        // Obnova zámku po logoutu staré firmy vrací session zpátky na Locks=1.
        var reacquireClone = MLRestMocks.Create();
        SetupVerifyLic(reacquireClone, 1);
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(reacquireClone);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(0, locksAtVerify);
        Assert.Equal(1, mlRest.CacheLocks);
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

    [Fact]
    public void Switch_Accepted_SavesLastIdfaToPrefs()
    {
        var mlRest = MLRestMocks.Create();
        mlRest.Server.Returns("dev-host");
        mlRest.Port.Returns(57772);
        var prefs = Substitute.For<ILocalPrefs>();
        var switcher = CreateSwitcher(mlRest, () => null, prefs: prefs);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: null);

        Assert.Equal("JI_DP", prefs.LastIdfaFor("dev-host", 57772));
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

    [Fact]
    public void Switch_Accepted_ObnoviActivityZamekPoLogoutu()
    {
        // Logout uklízí business zámky GUID-wide, takže smete i ACTIVITY zámek nové firmy;
        // bez obnovy padne první další request session na LockExist.
        var mlRest = MLRestMocks.Create();   // start Idfa = CB_DP
        SetupVerifyLic(mlRest, 1);
        var oldClone = MLRestMocks.Create();
        var reacquireClone = MLRestMocks.Create();
        SetupVerifyLic(reacquireClone, 1);
        mlRest.With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP").Returns(oldClone);
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(reacquireClone);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Received.InOrder(() =>
        {
            oldClone.QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", Arg.Any<object[]>());
            reacquireClone.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
        });
        Assert.Equal(1, mlRest.CacheLocks);   // obnovený zámek → kontroly zpátky
    }

    [Fact]
    public void Switch_ObnovaZamku_NecteLicenciZeServeru()
    {
        // LicenceLoader je sám request na SDÍLENÉ instanci (LicenceService drží MLRest.Inner);
        // v okně bez ACTIVITY zámku by shodil session ještě před VerifyLic. Počet licencí proto
        // musí jít z už načtené licence, ne z dalšího čtení.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var oldClone = MLRestMocks.Create();
        var reacquireClone = MLRestMocks.Create();
        SetupVerifyLic(reacquireClone, 1);
        mlRest.With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP").Returns(oldClone);
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(reacquireClone);
        var currentLicence = new Lic { CountsLOGIN = 5 };
        var targetLicence = new Lic { CountsLOGIN = 9 };
        int loaderCalls = 0;
        var switcher = CreateSwitcher(mlRest, () => { loaderCalls++; return targetLicence; });

        switcher.Switch("JI_DP", "JI_DP", currentLicence);

        // Jediné čtení licence je to v TryAcquireSession, které běží ještě s Locks=0; zámek se
        // obnovuje s počtem licencí CÍLOVÉ firmy (limit je per IdFa).
        Assert.Equal(1, loaderCalls);
        reacquireClone.Received(1).QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 9));
    }

    [Fact]
    public void Switch_ObnovaZamkuSelze_CacheLocksZustaneNula()
    {
        // Bez zámku nemá smysl LockExist kontrolovat - Locks=1 by session jen shodil.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var oldClone = MLRestMocks.Create();
        var reacquireClone = MLRestMocks.Create();
        SetupVerifyLic(reacquireClone, -1);
        mlRest.With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP").Returns(oldClone);
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(reacquireClone);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        var result = switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        Assert.Equal(LicenceAcquireOutcome.Accepted, result);   // přepnutí platí dál
        Assert.Equal(0, mlRest.CacheLocks);
    }

    [Fact]
    public void Switch_Refused_NeobnovujeZamek()
    {
        // Rollback nechává zámek staré firmy naživu - Logout ani obnova neproběhly.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence, isAdmin: false);

        switcher.Switch("JI_DP", "JI_DP", currentLicence: licence);

        clone.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
    }

    [Fact]
    public void Switch_StejnaFirma_NeobnovujeZamek()
    {
        // Bez Logoutu není co obnovovat - druhé VerifyLic by bylo jen zbytečný request.
        var mlRest = MLRestMocks.Create(idfa: "CB_DP", dataNamespace: "CB_DP");
        SetupVerifyLic(mlRest, 1);
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);
        var licence = new Lic { CountsLOGIN = 5 };
        var switcher = CreateSwitcher(mlRest, () => licence);

        switcher.Switch("CB_DP", "CB_DP", currentLicence: licence);

        clone.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>());
    }
}
