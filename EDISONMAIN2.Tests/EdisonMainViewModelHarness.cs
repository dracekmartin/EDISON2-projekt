using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Jediná konstrukce <see cref="EdisonMainViewModel"/> pro testy. Ctor má 13 parametrů a statický
/// <c>EDServices.Initialize/Reset</c> kolem sebe, takže každá kopie harnessu znamenala opravovat
/// totéž na několika místech; proto jedno místo pro všechny test soubory.
/// </summary>
internal static class EdisonMainViewModelHarness
{
    internal static readonly CompanyStruct[] Companies =
    [
        new() { Id = "CB_DP", Database = "CB_DP" },
        new() { Id = "JI_DP", Database = "JI_DP" },
    ];

    internal static readonly CompanyStruct[] OneCompany =
    [
        new() { Id = "CB_DP", Database = "CB_DP" },
    ];

    internal static EDServices MockServices(
        IMLRest? mlRest = null,
        IWindowNavigator? nav = null,
        IPopupDisplayer? popup = null) =>
        new(mlRest,
            nav ?? Substitute.For<IWindowNavigator>(),
            popup ?? Substitute.For<IPopupDisplayer>());

    internal static IMLRest MockMLRest(string idfa = "CB_DP")
    {
        var mock = MLRestMocks.Create(idfa);
        // AccessLoader volá GetEdisonAccess při initial / switch / refresh. Default mock
        // vrátí apId=3 (admin, IsAllAccess=true) → access filter nepřebíjí ostatní testy.
        // Specifické access testy si MockMLRest přepíší vlastním setupem.
        mock.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("3"));
        return mock;
    }

    /// <summary>Stanice mimo doménu M-line bez dekorace - stromy nezávisí na stroji, kde test běží.</summary>
    internal static readonly ModuleTreeStation NeutralStation = new(IsMlineDomain: false, Decorate: m => m);

    internal static IModuleTreeLoader EmptyTreeLoader()
    {
        var mock = Substitute.For<IModuleTreeLoader>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        return mock;
    }

    /// <summary>Loader s daným hlavním stromem; system i partners prázdné.</summary>
    internal static IModuleTreeLoader TreeLoaderWith(IReadOnlyList<IModule> mainTree)
    {
        var mock = Substitute.For<IModuleTreeLoader>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(mainTree);
        mock.GetModuleTree(Arg.Any<IMLRest>(), "system").Returns([]);
        mock.GetModuleTree(Arg.Any<IMLRest>(), "partners").Returns([]);
        return mock;
    }

    internal static EdisonMainViewModel CreateVM(
        EDServices? services = null,
        IModuleTreeLoader? treeLoader = null,
        IPopupDisplayer? popup = null,
        ILocalPrefs? prefs = null,
        Action? closeAllWindows = null,
        MiniAppCatalog? miniAppCatalog = null,
        IMLRest? mlRest = null,
        IRestCallRunner? restRunner = null,
        Func<IReadOnlyList<RunningModuleInfo>>? getActiveModules = null,
        CompanyStruct[]? companies = null,
        Action? closeLegacyModules = null,
        IUserPrefsService? userPrefs = null,
        Lic? initialLicence = null,
        Func<SwitchUserOutcome>? switchUser = null,
        Action? forceShutdown = null,
        ModuleShortcutsService? shortcuts = null,
        IChangelogSource? changelogSource = null,
        Func<IMLRest, LicenceService.MlineSyncResult>? licenceSync = null,
        Func<IMLRest, IReadOnlyList<CompanyStruct>>? loadAllowedCompanies = null,
        ModuleTreeStation? station = null,
        bool initialize = true)
    {
        var mockMLRest = mlRest ?? MockMLRest();
        var mockTree = treeLoader ?? EmptyTreeLoader();
        var svc = services ?? MockServices(popup: popup);
        // V produkci session zakládá SessionFactory a licenci zapíše LicenceVerifier při Accepted;
        // VM i CompanySwitcher obojí jen čtou ze services.
        svc.MLRest = mockMLRest;
        if (initialLicence != null) svc.ActiveLicence = initialLicence;
        if (userPrefs != null) svc.UserPrefs = userPrefs;
        var close = closeAllWindows ?? (() => { });
        var p = prefs ?? Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, p, () => { close(); return true; }, closeLegacyModules ?? (() => { }));
        var catalog = miniAppCatalog ?? MiniAppCatalog.Build([]);

        EDServices.Initialize(svc);
        EdisonMainViewModel vm;
        try
        {
            var allCompanies = companies ?? Companies;
            vm = new EdisonMainViewModel(allCompanies, loadAllowedCompanies ?? (_ => allCompanies),
                                         mockTree, switcher,
                                         switchUser ?? (() => new(SwitchUserResult.Cancelled)), p,
                                         getActiveModules ?? (() => []), close, forceShutdown ?? (() => { }),
                                         catalog, shortcuts ?? new ModuleShortcutsService(), changelogSource,
                                         restRunner ?? new InlineRestCallRunner(mockMLRest),
                                         // Produkční sync volá EDUsers nad živým MLRest - v testech jen stub.
                                         licenceSync ?? (_ => new(false, "")),
                                         station ?? NeutralStation);
        }
        finally
        {
            EDServices.Reset();
        }
        // Session state se plní až v InitializeAsync (v produkci z ContentRendered). S inline
        // runnerem je load synchronní, takže GetResult tady nemá na co čekat ani na čem uváznout.
        if (initialize) vm.InitializeAsync().GetAwaiter().GetResult();
        return vm;
    }
}
