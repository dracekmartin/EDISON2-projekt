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
        Action? closeAllWindows = null,
        MiniAppCatalog? miniAppCatalog = null,
        IMLRest? mlRest = null)
    {
        var mockMLRest = mlRest ?? MockMLRest();
        var mockTree = treeGetter ?? EmptyTreeGetter();
        var svc = services ?? MockServices(popup: popup);
        // CompanySwitcher i VM čtou MLRest z services lazily — musí být v services před konstrukcí.
        svc.MLRest = mockMLRest;
        var close = closeAllWindows ?? (() => { });
        var p = prefs ?? Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, p, close);
        Func<SwitchUserOutcome> switchUser = () => new(SwitchUserResult.Cancelled, null);
        var catalog = miniAppCatalog ?? MiniAppCatalog.Build([]);

        EDServices.Initialize(svc);
        try
        {
            return new EdisonMainViewModel(Companies, mockMLRest, mockTree, initialLicence: null, switcher, switchUser, p, () => [], close, catalog);
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

        // StartsWith — ModuleLaunchabilityCheck připojuje k názvu suffix (" — chybí exe" apod.);
        // test ověřuje routing stromů do správných kolekcí, ne přesný label.
        Assert.StartsWith("MainApp", Assert.Single(vm.MainMenuTree).DisplayedName);
        Assert.StartsWith("SysTool", Assert.Single(vm.SystemAdminTree).DisplayedName);
        Assert.StartsWith("Partner", Assert.Single(vm.PartnerSolutionsTree).DisplayedName);
    }

    // ── Výchozí okno (startup window) ──────────────────────────────────────────────

    private static IModuleTreeGetter MainTreeWith(params ExecutableModule[] modules)
    {
        var mock = Substitute.For<IModuleTreeGetter>();
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

        var vm = CreateVM(treeGetter: tree, mlRest: ml);

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
        var vm = CreateVM(treeGetter: tree, mlRest: ml);
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
        var vm = CreateVM(treeGetter: tree, mlRest: ml);
        var module = vm.MainMenuTree.Cast<ExecutableModule>().Single();
        Assert.True(module.IsStartupWindow);   // předpoklad

        vm.ClearStartupWindowCommand.Execute(null);

        ml.Received().QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 0));
        Assert.Equal(0, vm.DefaultWindowId);
        Assert.False(module.IsStartupWindow);
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
    public void SelectedCompanyIndex_Switch_ReappliesTreeSearch()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockTree = Substitute.For<IModuleTreeGetter>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        // Čerstvé instance při každém volání — simuluje přestavbu stromu při přepnutí firmy.
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(_ => new IModule[]
        {
            new ExecutableModule { DisplayedName = "Turnusy", ExecutablePath = "t.exe", FormName = "" },
            new ExecutableModule { DisplayedName = "Objednávky", ExecutablePath = "o.exe", FormName = "" },
        });
        var vm = CreateVM(popup: mockPopup, treeGetter: mockTree);
        vm.TreeSearchText = "turnus";

        vm.SelectedCompanyIndex = vm.SelectedCompanyIndex == 0 ? 1 : 0;

        // StartsWith — ModuleLaunchabilityCheck může připojit suffix k názvu.
        var modules = vm.MainMenuTree.Cast<ExecutableModule>().ToList();
        Assert.True(modules.Single(m => m.DisplayedName.StartsWith("Turnusy")).IsVisibleBySearch);
        Assert.False(modules.Single(m => m.DisplayedName.StartsWith("Objednávky")).IsVisibleBySearch);
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
            vm = new EdisonMainViewModel(Companies, mockMLRest, EmptyTreeGetter(), null, switcher, switchUser, prefs, () => [], () => { }, MiniAppCatalog.Build([]));
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

    [Fact]
    public void SelectedCompanyIndex_SwitchRefused_RollsBackIndex_ShowsPopup()
    {
        // Non-admin over-limit → CompanySwitcher vrátí Refused → VM musí vrátit index zpět
        // a zobrazit hlášku (jinak UI ukazuje firmu, na kterou se reálně nepřepnulo).
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var mockMLRest = MockMLRest();
        mockMLRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("-1"));
        var licence = new Lic { CountsLOGIN = 5 };
        var svc = MockServices(popup: mockPopup);
        svc.MLRest = mockMLRest;
        svc.ActiveLicence = licence;
        svc.LicenceLoader = () => licence;
        svc.IsAdmin = false;
        var prefs = Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, prefs, () => { });
        Func<SwitchUserOutcome> switchUser = () => new(SwitchUserResult.Cancelled, null);
        EDServices.Initialize(svc);
        EdisonMainViewModel vm;
        try
        {
            vm = new EdisonMainViewModel(Companies, mockMLRest, EmptyTreeGetter(), licence, switcher, switchUser, prefs, () => [], () => { }, MiniAppCatalog.Build([]));
        }
        finally
        {
            EDServices.Reset();
        }

        int original = vm.SelectedCompanyIndex;
        vm.SelectedCompanyIndex = original == 0 ? 1 : 0;

        Assert.Equal(original, vm.SelectedCompanyIndex);
        Assert.Equal(Companies[original].Id, vm.Idfa);
        mockPopup.Received(1).Info(Arg.Is<string>(s => s.Contains("překročen počet licencí")));
    }

    [Fact]
    public void SelectedCompanyId_SetConfirmed_SwitchesCompany()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var vm = CreateVM(popup: mockPopup);

        vm.SelectedCompanyId = "JI_DP";

        Assert.Equal("JI_DP", vm.SelectedCompanyId);
        Assert.Equal(1, vm.SelectedCompanyIndex);
    }

    [Fact]
    public void SelectedCompanyId_SetConfirmDenied_Reverts()
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(false);
        var vm = CreateVM(popup: mockPopup);

        vm.SelectedCompanyId = "JI_DP";

        Assert.Equal("CB_DP", vm.SelectedCompanyId);   // nezměněno → combobox se vrátí na původní
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
        var carousel = new MiniAppDescriptor("Core.Podpora", "Podpora M-line",
            () => new StubMiniApp(services), Pinned: true);
        var extra = new MiniAppDescriptor("Extra", "Extra", () => new StubMiniApp(services));
        var catalog = MiniAppCatalog.Build([new SingleProvider(carousel, extra)]);
        var vm = CreateVM(miniAppCatalog: catalog);
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);   // bez uloženého layoutu otevře výchozí sadu (karusel)
        vm.OpenMiniAppCommand.Execute(extra);
        var beforeReset = host.Panes.ToList();
        Assert.Equal(2, beforeReset.Count);

        vm.ResetMiniAppsCommand.Execute(null);

        Assert.All(beforeReset, p => Assert.True(p.IsClosed));
        Assert.Equal(2, host.SkeletonResets);   // attach + reset
        var reopened = host.Panes.Where(p => !p.IsClosed).Select(p => p.ContentId).ToList();
        Assert.StartsWith("Core.Podpora#", Assert.Single(reopened));
        Assert.True(vm.HasOpenMiniApps);
    }

    [Fact]
    public void CloseMiniAppsCommand_KeepsPinnedOpen()
    {
        var services = MockServices();
        var plain = new MiniAppDescriptor("A", "Title A", () => new StubMiniApp(services));
        var pinned = new MiniAppDescriptor("P", "Pinned", () => new StubMiniApp(services), Pinned: true);
        var vm = CreateVM(miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(plain, pinned)]));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);   // bez uloženého layoutu: výchozí sada prázdná, EnsurePinnedOpen otevře P
        vm.OpenMiniAppCommand.Execute(plain);

        vm.CloseMiniAppsCommand.Execute(null);

        Assert.True(vm.HasOpenMiniApps);
        Assert.Equal("P#1", Assert.Single(host.Panes.Where(p => !p.IsClosed)).ContentId);
    }

    [Fact]
    public void RestoreMiniAppLayout_SavedLayoutWithoutPinned_ReopensPinned()
    {
        // Layout uložený před zavedením pinned sady pinned mini-app neobsahuje — musí se dootevřít.
        var services = MockServices();
        var pinned = new MiniAppDescriptor("P", "Pinned", () => new StubMiniApp(services), Pinned: true);
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { ["CB_DP"] = "<layout bez P>" });
        var vm = CreateVM(prefs: prefs, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(pinned)]));
        var host = new RecordingHost();   // RestoreLayout nic neobnoví → simuluje layout bez pinned

        vm.AttachMiniAppHost(host);

        Assert.Equal("P#1", Assert.Single(host.Panes).ContentId);
    }

    [Fact]
    public void RestoreMiniAppLayout_CorruptLayout_WithPinned_StillDoesNotThrow()
    {
        // Po zahození rozbitého layoutu se zkusí dootevřít pinned; když i to spadne
        // (ThrowingHost.AddPane), start shellu to shodit nesmí.
        var services = MockServices();
        var pinned = new MiniAppDescriptor("P", "Pinned", () => new StubMiniApp(services), Pinned: true);
        var layouts = new Dictionary<string, string> { ["CB_DP"] = "bad-xml" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var vm = CreateVM(prefs: prefs, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(pinned)]));

        var ex = Record.Exception(() => vm.AttachMiniAppHost(new ThrowingHost()));

        Assert.Null(ex);
        Assert.False(layouts.ContainsKey("CB_DP"));
    }

    [Fact]
    public void HasOpenFavorites_TrueAfterDefaultLayout_FalseWhenClosed()
    {
        var services = MockServices();
        var favorites = new MiniAppDescriptor(FavoritesMiniAppProvider.FavoritesId, "Oblíbené",
            () => new StubMiniApp(services));
        var vm = CreateVM(miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(favorites)]));
        var host = new RecordingHost();

        vm.AttachMiniAppHost(host);   // bez uloženého layoutu → výchozí sada (Oblíbené jako řada dole)
        Assert.True(vm.HasOpenFavorites);

        host.Panes.Single(p => p.ContentId.StartsWith("Core.Oblibene#")).Close();
        Assert.False(vm.HasOpenFavorites);
    }

    [Fact]
    public void ShowFavoritesMiniAppCommand_OpensFavorites_SetsHasOpenFavorites()
    {
        var services = MockServices();
        var favorites = new MiniAppDescriptor(FavoritesMiniAppProvider.FavoritesId, "Oblíbené",
            () => new StubMiniApp(services));
        // Uložený layout → RecordingHost.RestoreLayout je no-op, Oblíbené se samo neotevře.
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string> { ["CB_DP"] = "x" });
        var vm = CreateVM(prefs: prefs, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(favorites)]));
        vm.AttachMiniAppHost(new RecordingHost());
        Assert.False(vm.HasOpenFavorites);

        vm.ShowFavoritesMiniAppCommand.Execute(null);

        Assert.True(vm.HasOpenFavorites);
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
                                    bool newColumn = false, bool newRow = false, bool canClose = true)
        {
            var pane = new RecordingPane(contentId, onClosed);
            Panes.Add(pane);
            return pane;
        }

        public void ResetToDefaultSkeleton() => SkeletonResets++;
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve) { }
    }

    private sealed class RecordingPane(string contentId, Action onClosed) : IMiniAppPane
    {
        public string ContentId => contentId;
        public bool IsClosed { get; private set; }

        public void Close()
        {
            if (IsClosed) return;
            IsClosed = true;
            onClosed();
        }

        public void Activate() { }
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
                                    bool newColumn = false, bool newRow = false, bool canClose = true)
            => throw new NotSupportedException();
        public void ResetToDefaultSkeleton() { }
        public string SaveLayout() => "";
        public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve)
            => throw new InvalidOperationException("simulovaný rozbitý layout");
    }

    // Neškodný host — prázdný katalog v testech znamená, že AddPane se nikdy nezavolá.
    private sealed class NullHost : IMiniAppHost
    {
        public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                                    bool newColumn = false, bool newRow = false, bool canClose = true)
            => Substitute.For<IMiniAppPane>();
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
}
