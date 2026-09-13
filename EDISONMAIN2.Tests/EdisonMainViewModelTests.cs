using System.Windows.Media;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

public class EdisonMainViewModelTests
{
    // Confirm před přepnutím firmy se ukazuje jen když je otevřený aspoň jeden modul.
    private static Func<IReadOnlyList<RunningModuleInfo>> OneOpenWindow() =>
        () => [new RunningModuleInfo("Okno", () => { })];

    [Fact]
    public void Constructor_LoadsCompanies()
    {
        var vm = CreateVM();

        Assert.Equal(2, vm.Companies.Count);
        Assert.Equal("CB_DP", vm.Idfa);
        Assert.Equal("CB_DP", vm.Namespace);
    }

    [Fact]
    public void Constructor_FetchesAllThreeTrees()
    {
        var mockTree = Substitute.For<IModuleTreeLoader>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "main")
            .Returns([new ExecutableModule { DisplayedName = "MainApp", ExecutablePath = "app.exe", FormName = "" }]);
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "system")
            .Returns([new ExecutableModule { DisplayedName = "SysTool", ExecutablePath = "sys.exe", FormName = "" }]);
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "partners")
            .Returns([new ExecutableModule { DisplayedName = "Partner", ExecutablePath = "p.exe", FormName = "" }]);

        var vm = CreateVM(treeLoader: mockTree);

        // StartsWith - ModuleLaunchabilityCheck připojuje k názvu suffix (" - chybí exe" apod.);
        // test ověřuje routing stromů do správných kolekcí, ne přesný label.
        Assert.StartsWith("MainApp", Assert.Single(vm.MainMenuTree).DisplayedName);
        Assert.StartsWith("SysTool", Assert.Single(vm.SystemAdminTree).DisplayedName);
        Assert.StartsWith("Partner", Assert.Single(vm.PartnerSolutionsTree).DisplayedName);
    }

    /// <summary>
    /// Runner, kterému lze mezi fázemi testu zavřít bránu - drží load rozpracovaný, aby šlo
    /// sledovat stav VM „během" rebuildu. <c>Gate == null</c> = synchronní jako inline runner.
    /// </summary>
    private sealed class SwitchableGateRunner(IMLRest rest) : IRestCallRunner
    {
        public TaskCompletionSource? Gate { get; set; }

        public async Task<T> RunAsync<T>(Func<IMLRest, T> call)
        {
            if (Gate is not null) await Gate.Task;
            return call(rest);
        }
    }

    [Fact]
    public void Constructor_LeavesTreesEmpty_UntilInitializeAsync()
    {
        var mockTree = MainTreeWith(new ExecutableModule { DisplayedName = "A", ExecutablePath = "a.exe", FormName = "" });

        var vm = CreateVM(treeLoader: mockTree, initialize: false);

        // Shell se ukáže hned; data dorazí až z ContentRendered → InitializeAsync.
        Assert.Empty(vm.MainMenuTree);
        mockTree.DidNotReceive().GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>());
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        var mockTree = MainTreeWith(new ExecutableModule { DisplayedName = "A", ExecutablePath = "a.exe", FormName = "" });
        var vm = CreateVM(treeLoader: mockTree, initialize: false);

        await vm.InitializeAsync();
        await vm.InitializeAsync();

        // Tři stromy jednou, ne dvakrát - druhé volání vrací týž task.
        Assert.Equal(3, mockTree.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModuleTreeLoader.GetModuleTree)));
        Assert.Single(vm.MainMenuTree);
    }

    [Fact]
    public async Task InitializeAsync_ShowsActionInProgress_WhileLoading()
    {
        var ml = MockMLRest();
        var runner = new SwitchableGateRunner(ml) { Gate = new TaskCompletionSource() };
        var vm = CreateVM(mlRest: ml, restRunner: runner, initialize: false);

        var loading = vm.InitializeAsync();

        Assert.True(vm.IsActionInProgress);
        runner.Gate.SetResult();
        await loading;
        Assert.False(vm.IsActionInProgress);
    }

    // Strom napoprvé projde, podruhé (rebuild) spadne - simuluje server mimo provoz při přepnutí.
    private static IModuleTreeLoader TreeFailingOnSecondLoad()
    {
        var mockTree = Substitute.For<IModuleTreeLoader>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        int mainCalls = 0;
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(_ =>
            ++mainCalls == 1
                ? [new ExecutableModule { DisplayedName = "Turnusy", ExecutablePath = "t.exe", FormName = "" }]
                : throw new InvalidOperationException("server je mimo"));
        return mockTree;
    }

    [Fact]
    public void RefreshLicence_LoadFailure_KeepsPreviousTrees()
    {
        // Táž firma i uživatel → předchozí strom je pořád platný. LoadAndFilterTree ho dřív
        // mazal PŘED requestem, takže selhání nechalo záložku trvale prázdnou.
        // Přes command (ne interní metodu): s inline runnerem doběhne synchronně a chyba
        // skončí v PopupDisplayer.Error, jak to udělá i AsyncRelayCommand v produkci.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: mockPopup, treeLoader: TreeFailingOnSecondLoad());
        Assert.Single(vm.MainMenuTree);   // předpoklad

        vm.RefreshLicenceCommand.Execute(null);

        Assert.StartsWith("Turnusy", Assert.Single(vm.MainMenuTree).DisplayedName);
        mockPopup.Received(1).Error(Arg.Any<InvalidOperationException>());
    }

    [Fact]
    public async Task SwitchCompany_LoadFailure_ClearsSessionState()
    {
        // Tady naopak: session už patří nové firmě, takže strom staré je cizí - šel by spustit
        // mimo licenci nové firmy a fire-and-forget save by jeho data zapsal do jejích blobů.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var vm = CreateVM(popup: mockPopup, treeLoader: TreeFailingOnSecondLoad());
        Assert.Single(vm.MainMenuTree);   // předpoklad

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.SwitchCompanyAsync(1));

        Assert.Empty(vm.MainMenuTree);
        Assert.Empty(vm.FavoritesTree);
        Assert.Empty(vm.UsedModules);
    }

    [Fact]
    public async Task SwitchCompany_ShellClosedDuringLoad_DoesNotSaveOldLayoutUnderNewIdfa()
    {
        // Mezi posunem Idfa a restore layoutu je await, ve kterém je UI thread volný - zavření
        // shellu (nebo force logout) tam volá PersistMiniAppLayout(Idfa), tedy už NOVOU firmu,
        // zatímco dock drží ještě mini-apps té staré.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var prefs = Substitute.For<ILocalPrefs>();
        var layouts = new Dictionary<string, string>();
        prefs.MiniAppLayouts.Returns(layouts);
        var ml = MockMLRest();
        var runner = new SwitchableGateRunner(ml);
        var vm = CreateVM(popup: mockPopup, prefs: prefs, mlRest: ml, restRunner: runner);
        vm.AttachMiniAppHost(Substitute.For<IMiniAppHost>());

        runner.Gate = new TaskCompletionSource();
        var switching = vm.SwitchCompanyAsync(1);
        Assert.True(vm.ConfirmShellClose());   // uživatel zavře shell během loadu
        runner.Gate.SetResult();
        await switching;

        Assert.DoesNotContain("JI_DP", layouts.Keys);
        Assert.Contains("CB_DP", layouts.Keys);
    }

    private static IModuleTreeLoader MainTreeWith(params ExecutableModule[] modules)
    {
        var mock = Substitute.For<IModuleTreeLoader>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        mock.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(modules);
        return mock;
    }

    [Fact]
    public void StartupWindow_LoadedFromServer_MarksMatchingModuleOnly()
    {
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("17"));
        var tree = MainTreeWith(
            new ExecutableModule { DisplayedName = "A", ExecutablePath = "a.exe", FormName = "", ElementId = "17" },
            new ExecutableModule { DisplayedName = "B", ExecutablePath = "b.exe", FormName = "", ElementId = "9" });

        var vm = CreateVM(treeLoader: tree, mlRest: ml);

        Assert.Equal(17, vm.DefaultWindowId);
        var modules = vm.MainMenuTree.Cast<ExecutableModule>().ToList();
        Assert.True(modules.Single(m => m.ElementId == "17").IsStartupWindow);
        Assert.False(modules.Single(m => m.ElementId == "9").IsStartupWindow);
    }

    [Fact]
    public void SetStartupWindowCommand_SetsServerValueAndMarksNode()
    {
        var ml = MockMLRest();   // GetUserDefaultForm nenastaven → start 0
        var tree = MainTreeWith(
            new ExecutableModule { DisplayedName = "A", ExecutablePath = "a.exe", FormName = "", ElementId = "23" });
        var vm = CreateVM(treeLoader: tree, mlRest: ml);
        var module = vm.MainMenuTree.Cast<ExecutableModule>().Single();

        vm.SetStartupWindowCommand.Execute(module);

        ml.Received().QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 23));
        Assert.Equal(23, vm.DefaultWindowId);
        Assert.True(module.IsStartupWindow);
    }

    [Fact]
    public void ClearStartupWindowCommand_ClearsServerValueAndUnmarks()
    {
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("23"));
        var tree = MainTreeWith(
            new ExecutableModule { DisplayedName = "A", ExecutablePath = "a.exe", FormName = "", ElementId = "23" });
        var vm = CreateVM(treeLoader: tree, mlRest: ml);
        var module = vm.MainMenuTree.Cast<ExecutableModule>().Single();
        Assert.True(module.IsStartupWindow);   // předpoklad

        vm.ClearStartupWindowCommand.Execute(null);

        ml.Received().QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 0));
        Assert.Equal(0, vm.DefaultWindowId);
        Assert.False(module.IsStartupWindow);
    }

    [Fact]
    public async Task SwitchCompany_ConfirmDenied_DoesNotSwitch()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var vm = CreateVM(popup: mockPopup, getActiveModules: OneOpenWindow());
        int original = vm.SelectedCompanyIndex;

        await vm.SwitchCompanyAsync(original == 0 ? 1 : 0);

        Assert.Equal(original, vm.SelectedCompanyIndex);
    }

    [Fact]
    public async Task SwitchCompany_ConfirmAccepted_SwitchesCompany()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var vm = CreateVM(popup: mockPopup, getActiveModules: OneOpenWindow());

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        await vm.SwitchCompanyAsync(target);

        Assert.Equal(target, vm.SelectedCompanyIndex);
        Assert.Equal(Companies[target].Database, vm.Namespace);
        Assert.Equal(Companies[target].Id, vm.Idfa);
        mockPopup.Received(1).Confirm(Arg.Any<string>());
    }

    [Fact]
    public async Task SwitchCompany_NoOpenWindows_DoesNotAskForConfirmation()
    {
        // Bez otevřených oken je přepnutí nedestruktivní - dialog by byl jen zbytečné friction.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: mockPopup);   // default: žádné aktivní moduly

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        await vm.SwitchCompanyAsync(target);

        mockPopup.DidNotReceive().Confirm(Arg.Any<string>());
        Assert.Equal(target, vm.SelectedCompanyIndex);
    }

    [Fact]
    public async Task SwitchCompany_BehemSynchronniFaze_JeActionInProgress()
    {
        // Synchronní fáze (CloseAll, VerifyLic) blokuje UI thread - overlay musí být aktivní
        // už před ní, jinak shell "zamrzne" bez indikace. Callback closeAll běží uvnitř Switch.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        EdisonMainViewModel? vmRef = null;
        bool? duringSwitch = null;
        var vm = CreateVM(popup: mockPopup, closeAllWindows: () => duringSwitch = vmRef!.IsActionInProgress);
        vmRef = vm;

        await vm.SwitchCompanyAsync(vm.SelectedCompanyIndex == 0 ? 1 : 0);

        Assert.True(duringSwitch);
        Assert.False(vm.IsActionInProgress);
    }

    [Fact]
    public void CompanyItem_DisplayName_FormatsAsIdfaDashName()
    {
        // Jednotný formát s displayem přepínače (EDFilterComboBox "Key - Label").
        Assert.Equal("CB - České Budějovice", new CompanyItem("CB", "České Budějovice", "DB").DisplayName);
        Assert.Equal("CB", new CompanyItem("CB", "", "DB").DisplayName);
    }

    [Fact]
    public void HasMultipleCompanies_JednaFirma_False()
    {
        var single = new CompanyStruct[] { new() { Id = "CB_DP", Database = "CB_DP" } };

        Assert.False(CreateVM(companies: single).HasMultipleCompanies);
        Assert.True(CreateVM().HasMultipleCompanies);
    }

    [Fact]
    public async Task SwitchCompany_Prepnuti_NotifikujeSelectedCompany()
    {
        // Na SelectedCompany visí ToolTip comba (plný název firmy) - bez notifikace by ukazoval starou.
        var vm = CreateVM();
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        await vm.SwitchCompanyAsync(vm.SelectedCompanyIndex == 0 ? 1 : 0);

        Assert.Contains(nameof(vm.SelectedCompany), notified);
    }

    [Fact]
    public async Task SwitchCompany_ReappliesTreeSearch()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockTree = Substitute.For<IModuleTreeLoader>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        // Čerstvé instance při každém volání - simuluje přestavbu stromu při přepnutí firmy.
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(_ => new IModule[]
        {
            new ExecutableModule { DisplayedName = "Turnusy", ExecutablePath = "t.exe", FormName = "" },
            new ExecutableModule { DisplayedName = "Objednávky", ExecutablePath = "o.exe", FormName = "" },
        });
        var vm = CreateVM(popup: mockPopup, treeLoader: mockTree);
        vm.TreeSearchText = "turnus";

        await vm.SwitchCompanyAsync(vm.SelectedCompanyIndex == 0 ? 1 : 0);

        // StartsWith - ModuleLaunchabilityCheck může připojit suffix k názvu.
        var modules = vm.MainMenuTree.Cast<ExecutableModule>().ToList();
        Assert.True(modules.Single(m => m.DisplayedName.StartsWith("Turnusy")).IsVisibleBySearch);
        Assert.False(modules.Single(m => m.DisplayedName.StartsWith("Objednávky")).IsVisibleBySearch);
    }

    // FirstSearchResult - cíl Enteru v poli „Hledat modul" (HD 13132/F15).

    private static EdisonMainViewModel SearchableVM() =>
        CreateVM(treeLoader: TreeLoaderWith(
        [
            new ExecutableModule { DisplayedName = "Objednávky", ExecutablePath = "o.exe", FormName = "" },
            new ExecutableModule { DisplayedName = "Turnusy", ExecutablePath = "t.exe", FormName = "" },
        ]));

    [Fact]
    public void FirstSearchResult_DoesNotWaitForDebouncer()
    {
        // Enter přijde dřív, než doběhne 150ms debounce (v testech DispatcherTimer nikdo nepumpuje,
        // takže by bez flushe filtrace neproběhla nikdy).
        var vm = SearchableVM();
        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "main");
        vm.TreeSearchText = "turnus";

        Assert.StartsWith("Turnusy", vm.FirstSearchResult()!.DisplayedName);
    }

    [Fact]
    public void FirstSearchResult_NullWithoutSearchText()
    {
        var vm = SearchableVM();
        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "main");

        // Prázdné hledání = vše viditelné; Enter nemá „první nález" a nesmí skočit na první uzel.
        Assert.Null(vm.FirstSearchResult());
    }

    [Fact]
    public void FirstSearchResult_NullOnTabWithoutTree()
    {
        // Používané je ListBox mimo filtr hledání - Enter tam nemá co dělat.
        var vm = SearchableVM();
        vm.TreeSearchText = "turnus";
        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "used");

        Assert.Null(vm.FirstSearchResult());
    }

    [Fact]
    public async Task SwitchCompany_UpdatesMLRestProperties()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockMLRest = MockMLRest();
        var vm = CreateVM(popup: mockPopup, mlRest: mockMLRest);

        int target = vm.SelectedCompanyIndex == 0 ? 1 : 0;
        await vm.SwitchCompanyAsync(target);

        mockMLRest.Received().DataNamespace = Companies[target].Database;
        mockMLRest.Received().Idfa = Companies[target].Id;
    }

    [Fact]
    public async Task SwitchCompany_ClosesShellWindowsAndLegacyModules()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var closeShell = Substitute.For<Action>();
        var closeLegacy = Substitute.For<Action>();
        var vm = CreateVM(popup: mockPopup, closeAllWindows: closeShell, closeLegacyModules: closeLegacy);

        await vm.SwitchCompanyAsync(vm.SelectedCompanyIndex == 0 ? 1 : 0);

        closeShell.Received(1).Invoke();
        closeLegacy.Received(1).Invoke();
    }

    [Fact]
    public async Task SwitchCompany_SameIndex_DoesNotConfirm()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: mockPopup);

        await vm.SwitchCompanyAsync(vm.SelectedCompanyIndex);

        mockPopup.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Fact]
    public async Task SwitchCompany_Refused_KeepsIndex_ShowsPopup()
    {
        // Non-admin over-limit → CompanySwitcher vrátí Refused → VM nesmí index posunout
        // a musí zobrazit hlášku (jinak UI ukazuje firmu, na kterou se reálně nepřepnulo).
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockMLRest = MockMLRest();
        mockMLRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("-1"));
        var licence = new Lic { CountsLOGIN = 5 };
        var svc = MockServices(popup: mockPopup);
        svc.ActiveLicence = licence;
        svc.LicenceLoader = () => licence;
        svc.IsAdmin = false;
        var vm = CreateVM(services: svc, mlRest: mockMLRest, initialLicence: licence);

        int original = vm.SelectedCompanyIndex;
        await vm.SwitchCompanyAsync(original == 0 ? 1 : 0);

        Assert.Equal(original, vm.SelectedCompanyIndex);
        Assert.Equal(Companies[original].Id, vm.Idfa);
        mockPopup.Received(1).Info(Arg.Is<string>(s => s.Contains("překročen počet licencí")));
    }

    [Fact]
    public void SelectedCompanyId_SetConfirmed_SwitchesCompany()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var vm = CreateVM(popup: mockPopup, getActiveModules: OneOpenWindow());

        vm.SelectedCompanyId = "JI_DP";

        Assert.Equal("JI_DP", vm.SelectedCompanyId);
        Assert.Equal(1, vm.SelectedCompanyIndex);
    }

    [Fact]
    public void SelectedCompanyId_SetConfirmDenied_Reverts()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var vm = CreateVM(popup: mockPopup, getActiveModules: OneOpenWindow());

        vm.SelectedCompanyId = "JI_DP";

        Assert.Equal("CB_DP", vm.SelectedCompanyId);   // nezměněno → combobox se vrátí na původní
    }

    [Fact]
    public void SelectedCompanyId_SetConfirmDenied_NotifiesBack()
    {
        // Combobox si drží vlastní editovanou hodnotu - bez notifikace by zůstal na firmě,
        // na kterou se nepřepnulo (getter sám nestačí, WPF se nezeptá).
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var vm = CreateVM(popup: mockPopup, getActiveModules: OneOpenWindow());
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.SelectedCompanyId = "JI_DP";

        Assert.Contains(nameof(vm.SelectedCompanyId), notified);
    }

    [Fact]
    public void SelectedCompanyId_SetUnknownId_DoesNothing()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: mockPopup);

        vm.SelectedCompanyId = "NEEXISTUJE";

        Assert.Equal("CB_DP", vm.SelectedCompanyId);
        mockPopup.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Fact]
    public void CompanySuggestions_ExposeIdAndName()
    {
        var vm = CreateVM();

        var ids = vm.CompanySuggestions.Select(s => s.Key).ToList();
        Assert.Contains("CB_DP", ids);
        Assert.Contains("JI_DP", ids);
    }

    [Fact]
    public void CloseMiniAppsCommand_ClosesAllOpenMiniApps()
    {
        var services = MockServices();
        var descriptor = new MiniAppDescriptor("A", "Title A", () => new StubMiniApp(services));
        var vm = CreateVM(miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(descriptor)]));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);
        vm.OpenMiniAppCommand.Execute(descriptor);
        vm.OpenMiniAppCommand.Execute(descriptor);
        Assert.True(vm.HasOpenMiniApps);

        vm.CloseMiniAppsCommand.Execute(null);

        Assert.False(vm.HasOpenMiniApps);
        Assert.All(host.Panes, p => Assert.True(p.IsClosed));
    }

    [Fact]
    public void ResetMiniAppsCommand_ClosesEverything_AndOpensDefaultSet()
    {
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services));
        var extra = new MiniAppDescriptor("Extra", "Extra", () => new StubMiniApp(services));
        var catalog = MiniAppCatalog.Build([new SingleProvider(desktop, extra)]);
        var vm = CreateVM(miniAppCatalog: catalog);
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);   // bez uloženého layoutu otevře výchozí sadu (Plocha)
        vm.OpenMiniAppCommand.Execute(extra);
        var beforeReset = host.Panes.ToList();
        Assert.Equal(2, beforeReset.Count);

        vm.ResetMiniAppsCommand.Execute(null);

        Assert.All(beforeReset, p => Assert.True(p.IsClosed));
        Assert.Equal(2, host.SkeletonResets);   // attach + reset
        var reopened = host.Panes.Where(p => !p.IsClosed).Select(p => p.ContentId).ToList();
        Assert.StartsWith(DesktopMiniAppProvider.DesktopId + "#", Assert.Single(reopened));
        Assert.True(vm.HasOpenMiniApps);
    }

    [Fact]
    public void RestoreMiniAppLayout_CorruptLayout_DefaultFallbackAlsoThrows_StillDoesNotThrow()
    {
        // Po zahození rozbitého layoutu se staví výchozí rozložení; když spadne i to
        // (ThrowingHost.AddPane), start shellu to shodit nesmí.
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services));
        var layouts = new Dictionary<string, string> { ["CB_DP"] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var vm = CreateVM(prefs: prefs, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(desktop)]));

        var ex = Record.Exception(() => vm.AttachMiniAppHost(new ThrowingHost()));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey("CB_DP"));
    }

    [Fact]
    public void HasOpenDesktop_TrueAfterDefaultLayout_FalseWhenClosed()
    {
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services));
        var vm = CreateVM(miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(desktop)]));
        var host = new RecordingHost();

        vm.AttachMiniAppHost(host);   // bez uloženého layoutu → výchozí sada (Plocha jako řada dole)
        Assert.True(vm.HasOpenDesktop);

        host.Panes.Single(p => p.ContentId.StartsWith(DesktopMiniAppProvider.DesktopId + "#")).Close();
        Assert.False(vm.HasOpenDesktop);
    }

    [Fact]
    public void ShowDesktopMiniAppCommand_OpensDesktop_SetsHasOpenDesktop()
    {
        var services = MockServices();
        var desktop = new MiniAppDescriptor(DesktopMiniAppProvider.DesktopId, "Plocha",
            () => new StubMiniApp(services));
        // Uložený layout → RecordingHost.RestoreLayout je no-op, Plocha se sama neotevře.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { ["CB_DP"] = "x" });
        var vm = CreateVM(prefs: prefs, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(desktop)]));
        vm.AttachMiniAppHost(new RecordingHost());
        Assert.False(vm.HasOpenDesktop);

        vm.ShowDesktopMiniAppCommand.Execute(null);

        Assert.True(vm.HasOpenDesktop);
    }

    private sealed class StubMiniApp(EDServices services) : EDMiniAppViewModel(services)
    {
        public override string Title => "stub";
    }

    private sealed class SingleProvider(params MiniAppDescriptor[] descriptors) : IMiniAppProvider
    {
        public string GroupTitle => "Test";
        public IEnumerable<MiniAppDescriptor> GetMiniApps() => descriptors;
    }

    private sealed class RecordingHost : IMiniAppHost
    {
        public List<RecordingPane> Panes { get; } = [];
        public int SkeletonResets { get; private set; }

        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    MiniAppPlacement? placement = null)
        {
            var pane = new RecordingPane(contentId, onClosed) { Placement = placement };
            Panes.Add(pane);
            return pane;
        }

        public IReadOnlyList<MiniAppTarget> GetTargets() =>
            [.. Panes.Where(p => !p.IsClosed).Select(p => new MiniAppTarget(p, [p.ContentId], false))];

        public void ResetToDefaultSkeleton() => SkeletonResets++;
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve) { }
    }

    private sealed class RecordingPane(string contentId, Action onClosed) : IMiniAppPane
    {
        public string ContentId => contentId;
        public MiniAppPlacement? Placement { get; set; }
        public bool IsClosed { get; private set; }

        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            onClosed();
        }
    }

    [Fact]
    public void RestoreMiniAppLayout_CorruptLayout_DoesNotThrow_AndDiscardsEntry()
    {
        // Poškozený uložený layout (kolegův pád: AvalonDock Deserialize hodí výjimku) nesmí
        // shodit start a má se zahodit, ať se to neopakuje.
        var layouts = new Dictionary<string, string> { ["CB_DP"] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var vm = CreateVM(prefs: prefs);

        var ex = Record.Exception(() => vm.AttachMiniAppHost(new ThrowingHost()));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey("CB_DP"));
        prefs.Received().Save();
    }

    private sealed class ThrowingHost : IMiniAppHost
    {
        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    MiniAppPlacement? placement = null)
            => throw new NotSupportedException();
        public IReadOnlyList<MiniAppTarget> GetTargets() => [];
        public void ResetToDefaultSkeleton() { }
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve)
            => throw new InvalidOperationException("simulovaný rozbitý layout");
    }

    // Neškodný host - prázdný katalog v testech znamená, že AddPane se nikdy nezavolá.
    private sealed class NullHost : IMiniAppHost
    {
        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    MiniAppPlacement? placement = null)
            => Substitute.For<IMiniAppPane>();
        public IReadOnlyList<MiniAppTarget> GetTargets() => [];
        public void ResetToDefaultSkeleton() { }
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve) { }
    }

    [Fact]
    public void RestoreMiniAppLayout_AppliesSavedTreeWidth()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(new Dictionary<string, double> { ["CB_DP"] = 320 });
        var vm = CreateVM(prefs: prefs);

        double? applied = null;
        vm.AttachTreeLayout(() => 0, w => applied = w);
        vm.AttachMiniAppHost(new NullHost());

        Assert.Equal(320, applied);
    }

    [Fact]
    public void RestoreMiniAppLayout_NoSavedTreeWidth_LeavesDefault()
    {
        var vm = CreateVM(); // ILocalPrefs substitute → TreeWidths null

        bool applied = false;
        vm.AttachTreeLayout(() => 0, _ => applied = true);
        vm.AttachMiniAppHost(new NullHost());

        Assert.False(applied);
    }

    [Fact]
    public void CloseAll_PersistsTreeWidthPerIdfa()
    {
        var widths = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(widths);
        var vm = CreateVM(prefs: prefs);

        vm.AttachTreeLayout(() => 275, _ => { });
        vm.AttachMiniAppHost(new NullHost());

        vm.CloseAllCommand.Execute(null);

        Assert.Equal(275, widths["CB_DP"]);
        prefs.Received().Save();
    }

    [Fact]
    public void UserDisplay_EmptyFullName_FallsBackToLogin()
    {
        var mlRest = MockMLRest();
        mlRest.LoginName.Returns("mhubata");
        mlRest.FullName.Returns("");

        Assert.Equal("mhubata", CreateVM(mlRest: mlRest).UserDisplay);
    }

    [Fact]
    public void UserDisplay_FilledFullName_ReturnsFullName()
    {
        var mlRest = MockMLRest();
        mlRest.LoginName.Returns("mhubata");
        mlRest.FullName.Returns("Martin Hubata");

        Assert.Equal("Martin Hubata", CreateVM(mlRest: mlRest).UserDisplay);
    }

    [Fact]
    public void ConnectionText_SkladaServerAPort()
    {
        var mlRest = MockMLRest();
        mlRest.Server.Returns("192.168.16.1");
        mlRest.Port.Returns(57770);

        Assert.Equal("192.168.16.1:57770", CreateVM(mlRest: mlRest).ConnectionText);
    }

    [Fact]
    public void NewVersionText_NewVersion_ShortTextWithoutPeriod()
    {
        var vm = CreateVM();

        vm.ReleaseNotice = new ReleaseNoticeResult(ReleaseNoticeOutcome.NewVersionAvailable, "20260801.1");

        // Na krátkosti chipu závisí layout status baru; plná věta patří do tooltipu.
        Assert.Contains("20260801.1", vm.NewVersionText);
        Assert.DoesNotContain(".", vm.NewVersionText.Replace("20260801.1", ""));
        Assert.EndsWith(".", vm.NewVersionTooltip);
    }

    [Fact]
    public void NewVersionText_UpToDate_StaysEmpty()
    {
        var vm = CreateVM();

        vm.ReleaseNotice = new ReleaseNoticeResult(ReleaseNoticeOutcome.UpToDate);

        Assert.Equal("", vm.NewVersionText);
        Assert.Equal("", vm.NewVersionTooltip);
        Assert.Equal("Jde o nejnovější verzi.", vm.UpToDateText);
    }

    [Fact]
    public void ConfirmShellClose_UserConfirms_ReturnsTrue()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var vm = CreateVM(popup: mockPopup);

        Assert.True(vm.ConfirmShellClose());
        mockPopup.Received(1).Confirm(Arg.Any<string>());
    }

    [Fact]
    public void ConfirmShellClose_UserDeclines_ReturnsFalse_AndDoesNotPersistLayout()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(new Dictionary<string, double>());
        var vm = CreateVM(popup: mockPopup, prefs: prefs);

        vm.AttachTreeLayout(() => 275, _ => { });
        vm.AttachMiniAppHost(new NullHost());
        prefs.ClearReceivedCalls();

        Assert.False(vm.ConfirmShellClose());
        prefs.DidNotReceive().Save();
    }

    [Fact]
    public void ConfirmShellClose_Confirmed_PersistsMiniAppLayout()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var widths = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(widths);
        var vm = CreateVM(popup: mockPopup, prefs: prefs);

        vm.AttachTreeLayout(() => 275, _ => { });
        vm.AttachMiniAppHost(new NullHost());

        Assert.True(vm.ConfirmShellClose());

        Assert.Equal(275, widths["CB_DP"]);
        prefs.Received().Save();
    }

    // Regrese na force logout / version gate - vynucené ukončení se uživatele ptát nesmí.
    [Fact]
    public void ConfirmShellClose_AfterSuppress_DoesNotPrompt()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: mockPopup);

        vm.SuppressCloseConfirm();

        Assert.True(vm.ConfirmShellClose());
        mockPopup.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Fact]
    public void Exit_Confirmed_ForcesShutdown()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var shutdown = Substitute.For<Action>();
        var vm = CreateVM(popup: mockPopup, forceShutdown: shutdown);

        vm.ExitCommand.Execute(null);

        shutdown.Received(1).Invoke();
    }

    [Fact]
    public void Exit_Declined_DoesNotShutdown()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var shutdown = Substitute.For<Action>();
        var vm = CreateVM(popup: mockPopup, forceShutdown: shutdown);

        vm.ExitCommand.Execute(null);

        shutdown.DidNotReceiveWithAnyArgs().Invoke();
    }

    [Theory]
    [InlineData(SwitchUserResult.LicenceRefused)]
    [InlineData(SwitchUserResult.LicenceCancelled)]
    public async Task LoginAsAnotherUser_LicenceFailure_ForcesShutdown(SwitchUserResult result)
    {
        // Po neúspěšné licenci je stará session už zničená (closeAllWindows proběhlo),
        // takže jediná bezpečná cesta je ukončit - a nesmí na ni jít potvrzovací dialog.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var shutdown = Substitute.For<Action>();
        var vm = CreateVM(popup: mockPopup, forceShutdown: shutdown,
                          switchUser: () => new(result, null));

        await vm.LoginAsAnotherUserAsync();

        shutdown.Received(1).Invoke();
    }

    [Fact]
    public async Task LoginAsAnotherUser_Cancelled_DoesNotShutdown()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var shutdown = Substitute.For<Action>();
        var vm = CreateVM(popup: mockPopup, forceShutdown: shutdown,
                          switchUser: () => new(SwitchUserResult.Cancelled, null));

        await vm.LoginAsAnotherUserAsync();

        shutdown.DidNotReceiveWithAnyArgs().Invoke();
    }

    [Fact]
    public void OpenLegacyShellCommand_IsWired()
    {
        var vm = CreateVM();

        Assert.NotNull(vm.OpenLegacyShellCommand);
        Assert.True(vm.OpenLegacyShellCommand.CanExecute(null));
    }

    [Fact]
    public void LegacyShellStartInfo_PassesRunEdisonReason()
    {
        // Bez reason=2 (RunEdison) frmNoMDI ohlásí "nelze spustit samostatně" a zavře se.
        var info = EdisonMainViewModel.LegacyShellStartInfo();

        Assert.Equal("EDISONMAIN.exe", System.IO.Path.GetFileName(info.FileName));
        Assert.Equal("reason=2", info.Arguments);
        Assert.True(info.UseShellExecute);
    }
}
