using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserPrefs;
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

    internal static IModuleTreeGetter EmptyTreeGetter()
    {
        var mock = Substitute.For<IModuleTreeGetter>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        return mock;
    }

    /// <summary>Getter s daným hlavním stromem; system i partners prázdné.</summary>
    internal static IModuleTreeGetter TreeGetterWith(IReadOnlyList<IModule> mainTree)
    {
        var mock = Substitute.For<IModuleTreeGetter>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(mainTree);
        mock.GetModuleTree(Arg.Any<IMLRest>(), "system").Returns([]);
        mock.GetModuleTree(Arg.Any<IMLRest>(), "partners").Returns([]);
        return mock;
    }

    internal static EdisonMainViewModel CreateVM(
        EDServices? services = null,
        IModuleTreeGetter? treeGetter = null,
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
        bool initialize = true)
    {
        var mockMLRest = mlRest ?? MockMLRest();
        var mockTree = treeGetter ?? EmptyTreeGetter();
        var svc = services ?? MockServices(popup: popup);
        // CompanySwitcher i VM čtou MLRest z services lazily — musí být v services před konstrukcí.
        svc.MLRest = mockMLRest;
        if (userPrefs != null) svc.UserPrefs = userPrefs;
        var close = closeAllWindows ?? (() => { });
        var p = prefs ?? Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, p, close, closeLegacyModules ?? (() => { }));
        var catalog = miniAppCatalog ?? MiniAppCatalog.Build([]);

        EDServices.Initialize(svc);
        EdisonMainViewModel vm;
        try
        {
            vm = new EdisonMainViewModel(companies ?? Companies, mockMLRest, mockTree, initialLicence, switcher,
                                         switchUser ?? (() => new(SwitchUserResult.Cancelled, null)), p,
                                         getActiveModules ?? (() => []), close, catalog,
                                         restRunner: restRunner ?? new InlineRestCallRunner(mockMLRest));
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
