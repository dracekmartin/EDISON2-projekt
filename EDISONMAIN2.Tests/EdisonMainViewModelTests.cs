using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

public class EdisonMainViewModelTests
{
    private static readonly CompanyStruct[] Companies =
    [
        new() { Id = "CB_DP", Database = "CB_DP" },
        new() { Id = "JI_DP", Database = "JI_DP" },
    ];

    private static EDServices MockServices(
        IMLRest? mlRest = null,
        IWindowNavigator? nav = null,
        IPopupDisplayer? popup = null) =>
        new(mlRest,
            nav ?? Substitute.For<IWindowNavigator>(),
            popup ?? Substitute.For<IPopupDisplayer>());

    private static IMLRest MockMLRest(string idfa = "CB_DP")
    {
        var mock = MLRestMocks.Create(idfa);
        // AccessLoader volá GetEdisonAccess při initial / switch / refresh. Default mock
        // vrátí apId=3 (admin, IsAllAccess=true) → access filter nepřebíjí ostatní testy.
        // Specifické access testy si MockMLRest přepíší vlastním setupem.
        mock.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("3"));
        return mock;
    }

    private static EdisonMainViewModel CreateVM(
        EDServices? services = null,
        IModuleTreeGetter? treeGetter = null,
        IPopupDisplayer? popup = null,
        ILocalPrefs? prefs = null,
        Action? closeAllWindows = null)
    {
        var mockMLRest = MockMLRest();
        var mockTree = treeGetter ?? EmptyTreeGetter();
        var svc = services ?? MockServices(popup: popup);
        // CompanySwitcher i VM čtou MLRest z services lazily — musí být v services před konstrukcí.
        svc.MLRest = mockMLRest;
        var close = closeAllWindows ?? (() => { });
        var p = prefs ?? Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, p, close);
        Func<SwitchUserOutcome> switchUser = () => new(SwitchUserResult.Cancelled, null);

        EDServices.Initialize(svc);
        try
        {
            return new EdisonMainViewModel(Companies, mockMLRest, mockTree, initialLicence: null, switcher, switchUser, p, () => [], close);
        }
        finally
        {
            EDServices.Reset();
        }
    }

    private static IModuleTreeGetter EmptyTreeGetter()
    {
        var mock = Substitute.For<IModuleTreeGetter>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        return mock;
    }

    [Fact]
    public void Constructor_LoadsCompanies()
    {
        var vm = CreateVM();

        Assert.Equal(2, vm.VisibleIdfas.Count);
        Assert.Equal("CB_DP", vm.Idfa);
        Assert.Equal("CB_DP", vm.Namespace);
    }

    [Fact]
    public void Constructor_FetchesAllThreeTrees()
    {
        var mockTree = Substitute.For<IModuleTreeGetter>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "main")
            .Returns([new ExecutableModule { DisplayedName = "MainApp", ExecutablePath = "app.exe", FormName = "" }]);
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "system")
            .Returns([new ExecutableModule { DisplayedName = "SysTool", ExecutablePath = "sys.exe", FormName = "" }]);
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "partners")
            .Returns([new ExecutableModule { DisplayedName = "Partner", ExecutablePath = "p.exe", FormName = "" }]);

        var vm = CreateVM(treeGetter: mockTree);

        Assert.Equal("MainApp", Assert.Single(vm.MainMenuTree).DisplayedName);
        Assert.Equal("SysTool", Assert.Single(vm.SystemAdminTree).DisplayedName);
        Assert.Equal("Partner", Assert.Single(vm.PartnerSolutionsTree).DisplayedName);
    }

    [Fact]
    public void SelectedCompanyIndex_ConfirmDenied_DoesNotSwitch()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var vm = CreateVM(popup: mockPopup);
        int original = vm.SelectedCompanyIndex;

        vm.SelectedCompanyIndex = original == 0 ? 1 : 0;

        Assert.Equal(original, vm.SelectedCompanyIndex);
    }

    [Fact]
    public void SelectedCompanyIndex_ConfirmAccepted_SwitchesCompany()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var vm = CreateVM(popup: mockPopup);

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        vm.SelectedCompanyIndex = target;

        Assert.Equal(target, vm.SelectedCompanyIndex);
        Assert.Equal(Companies[target].Database, vm.Namespace);
        Assert.Equal(Companies[target].Id, vm.Idfa);
    }

    [Fact]
    public void SelectedCompanyIndex_Switch_UpdatesMLRestProperties()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockMLRest = MockMLRest();
        var svc = MockServices(popup: mockPopup);
        svc.MLRest = mockMLRest;
        var prefs = Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, prefs, () => { });
        Func<SwitchUserOutcome> switchUser = () => new(SwitchUserResult.Cancelled, null);
        EDServices.Initialize(svc);
        EdisonMainViewModel vm;
        try
        {
            vm = new EdisonMainViewModel(Companies, mockMLRest, EmptyTreeGetter(), null, switcher, switchUser, prefs, () => [], () => { });
        }
        finally
        {
            EDServices.Reset();
        }

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        vm.SelectedCompanyIndex = target;

        mockMLRest.Received().DataNamespace = Companies[target].Database;
        mockMLRest.Received().Idfa = Companies[target].Id;
    }

    [Fact]
    public void SelectedCompanyIndex_Switch_ClosesAllWindows()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var closeAll = Substitute.For<Action>();
        var vm = CreateVM(popup: mockPopup, closeAllWindows: closeAll);

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        vm.SelectedCompanyIndex = target;

        closeAll.Received(1).Invoke();
    }

    [Fact]
    public void SelectedCompanyIndex_SameValue_DoesNotConfirm()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: mockPopup);

        vm.SelectedCompanyIndex = vm.SelectedCompanyIndex;

        mockPopup.DidNotReceive().Confirm(Arg.Any<string>());
    }
}
