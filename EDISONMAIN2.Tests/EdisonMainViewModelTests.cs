using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services.Processes;
using EDISONMAIN2.Services;
using EDISONMAIN2.Windows.EdisonMainWindow;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

public class EdisonMainViewModelTests
{
    private static readonly CompanyStruct[] Companies =
    [
        new() { Id = "CB_DP", Database = "DB_CB" },
        new() { Id = "JI_DP", Database = "DB_JI" },
    ];

    private static readonly ConnectionParams TestConnParams = new("localhost", 57770, "test", "Test User", 1, "test-guid");

    private static EDServices MockServices(
        IMLRest? mlRest = null,
        IWindowNavigator? nav = null,
        IPopupDisplayer? popup = null) =>
        new(mlRest,
            nav ?? Substitute.For<IWindowNavigator>(),
            popup ?? Substitute.For<IPopupDisplayer>());

    private static IMLRest MockMLRest(string idfa = "CB_DP")
    {
        var mock = Substitute.For<IMLRest>();
        mock.Idfa.Returns(idfa);
        return mock;
    }

    private static EdisonMainViewModel CreateVM(
        EDServices? services = null,
        IModuleTreeGetter? treeGetter = null,
        IProcessLauncher? processLauncher = null,
        IPopupDisplayer? popup = null)
    {
        var mockMLRest = MockMLRest();
        var mockTree = treeGetter ?? EmptyTreeGetter();
        var mockProcess = processLauncher ?? Substitute.For<IProcessLauncher>();
        var svc = services ?? MockServices(popup: popup);

        EDServices.Initialize(svc);
        try
        {
            return new EdisonMainViewModel(Companies, mockMLRest, mockTree, mockProcess, TestConnParams, activeLicence: null);
        }
        finally
        {
            EDServices.Reset();
        }
    }

    private static IModuleTreeGetter EmptyTreeGetter()
    {
        var mock = Substitute.For<IModuleTreeGetter>();
        mock.GetModuleTree(Arg.Any<IMLRest>()).Returns([]);
        return mock;
    }

    [Fact]
    public void Constructor_LoadsCompanies()
    {
        var vm = CreateVM();

        Assert.Equal(2, vm.VisibleIdfas.Count);
        Assert.Equal("CB_DP", vm.Idfa);
        Assert.Equal("DB_CB", vm.Namespace);
    }

    [Fact]
    public void Constructor_FetchesModuleTree()
    {
        var mockTree = Substitute.For<IModuleTreeGetter>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>())
            .Returns([new ExecutableModule { DisplayedName = "App", ExecutablePath = "app.exe", FormName = "" }]);

        var vm = CreateVM(treeGetter: mockTree);

        Assert.Single(vm.TopLevelModules);
        Assert.Equal("App", vm.TopLevelModules[0].DisplayedName);
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
        var mockMLRest = MockMLRest();
        var mockProcess = Substitute.For<IProcessLauncher>();
        var svc = MockServices(popup: mockPopup);
        EDServices.Initialize(svc);
        var mockTree = Substitute.For<IModuleTreeGetter>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>()).Returns([]);
        EdisonMainViewModel vm;
        try
        {
            vm = new EdisonMainViewModel(Companies, mockMLRest, mockTree, mockProcess, TestConnParams, activeLicence: null);
        }
        finally
        {
            EDServices.Reset();
        }

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
        var mockProcess = Substitute.For<IProcessLauncher>();
        var svc = MockServices(popup: mockPopup);
        EDServices.Initialize(svc);
        var mockTree = Substitute.For<IModuleTreeGetter>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>()).Returns([]);
        EdisonMainViewModel vm;
        try
        {
            vm = new EdisonMainViewModel(Companies, mockMLRest, mockTree, mockProcess, TestConnParams, activeLicence: null);
        }
        finally
        {
            EDServices.Reset();
        }
        svc.MLRest = mockMLRest;

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        vm.SelectedCompanyIndex = target;

        mockMLRest.Received().DataNamespace = Companies[target].Database;
        mockMLRest.Received().Idfa = Companies[target].Id;
    }

    [Fact]
    public void SelectedCompanyIndex_Switch_ClosesAllProcesses()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockProcess = Substitute.For<IProcessLauncher>();
        var vm = CreateVM(popup: mockPopup, processLauncher: mockProcess);

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        vm.SelectedCompanyIndex = target;

        mockProcess.Received(1).CloseAll();
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
