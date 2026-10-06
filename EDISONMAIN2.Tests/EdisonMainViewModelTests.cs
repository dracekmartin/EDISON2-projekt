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
using static EDISONMAIN2.Tests.MiniAppTestDoubles;

namespace EDISONMAIN2.Tests;

[Collection(SharedStaticStateCollection.Name)]
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
            .Returns([new ExecutableModule { Name = "MainApp", ExecutablePath = "app.exe", FormName = "" }]);
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "system")
            .Returns([new ExecutableModule { Name = "SysTool", ExecutablePath = "sys.exe", FormName = "" }]);
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "partners")
            .Returns([new ExecutableModule { Name = "Partner", ExecutablePath = "p.exe", FormName = "" }]);

        var vm = CreateVM(treeLoader: mockTree);

        // Harness dává neutrální stanici (bez dekorace launchovatelnosti), název je tedy přesný.
        Assert.Equal("MainApp", Assert.Single(vm.MainMenuTree).DisplayedName);
        Assert.Equal("SysTool", Assert.Single(vm.SystemAdminTree).DisplayedName);
        Assert.Equal("Partner", Assert.Single(vm.PartnerSolutionsTree).DisplayedName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MlineOnlyModule_FollowsStationDomain_NotTheMachineRunningTheTest(bool isMlineDomain)
    {
        var mockTree = MainTreeWith(
            new ExecutableModule { Name = "Pro M-line", ElementId = "1", ExecutablePath = "m.exe", MlineOnly = true },
            new ExecutableModule { Name = "Pro všechny", ElementId = "2", ExecutablePath = "a.exe" });

        var vm = CreateVM(treeLoader: mockTree, station: NeutralStation with { IsMlineDomain = isMlineDomain });

        var names = vm.MainMenuTree.Select(m => m.DisplayedName).ToList();
        Assert.Equal(isMlineDomain ? 2 : 1, names.Count);
        Assert.Contains("Pro všechny", names);
    }

    [Fact]
    public void StationDecoration_AppliesToEveryFilteredRoot()
    {
        // Produkčně tu ModuleLaunchabilityCheck.Mark obarví stub a chybějící exe; dekorace patří stanici.
        var mockTree = MainTreeWith(
            new ExecutableModule { Name = "A", ExecutablePath = "a.exe" },
            new ExecutableModule { Name = "B", ExecutablePath = "b.exe" });

        var vm = CreateVM(treeLoader: mockTree,
                          station: NeutralStation with { Decorate = m => new ExecutableModule((ExecutableModule)m) { Color = "#123456" } });

        Assert.All(vm.MainMenuTree, m => Assert.Equal("#123456", ((ExecutableModule)m).Color));
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
        var mockTree = MainTreeWith(new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "" });

        var vm = CreateVM(treeLoader: mockTree, initialize: false);

        // Shell se ukáže hned; data dorazí až z ContentRendered → InitializeAsync.
        Assert.Empty(vm.MainMenuTree);
        mockTree.DidNotReceive().GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>());
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        var mockTree = MainTreeWith(new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "" });
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
                ? [new ExecutableModule { Name = "Turnusy", ExecutablePath = "t.exe", FormName = "" }]
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

        Assert.Equal("Turnusy", Assert.Single(vm.MainMenuTree).DisplayedName);
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
        // shellu (nebo force logout) tam proběhne s Idfa už NOVÉ firmy a prázdným dockem.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var prefs = Substitute.For<ILocalPrefs>();
        var layouts = new Dictionary<string, string>();
        var heights = new Dictionary<string, double>();
        prefs.MiniAppLayouts.Returns(layouts);
        prefs.SupportStripHeights.Returns(heights);
        var ml = MockMLRest();
        var runner = new SwitchableGateRunner(ml);
        var vm = CreateVM(popup: mockPopup, prefs: prefs, mlRest: ml, restRunner: runner);
        vm.AttachSupportLayout(() => 520, _ => { });
        vm.AttachMiniAppHost(Substitute.For<IMiniAppHost>());

        runner.Gate = new TaskCompletionSource();
        var switching = vm.SwitchCompanyAsync(1);
        vm.OnShellClosed();   // uživatel zavře shell během loadu
        runner.Gate.SetResult();
        await switching;

        Assert.DoesNotContain(DockKey("JI_DP"), layouts.Keys);
        Assert.Contains(DockKey("CB_DP"), layouts.Keys);
        Assert.DoesNotContain(DockKey("JI_DP"), heights.Keys);
        Assert.Contains(DockKey("CB_DP"), heights.Keys);
    }

    [Fact]
    public async Task SwitchCompany_LoadFails_ThenShellClosed_KeepsNewCompanyLayout()
    {
        // Po selhaném rebuildu zůstává dock prázdný a Idfa už nová - zavření ho nesmí uložit
        // přes platný layout cílové firmy.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var layouts = new Dictionary<string, string> { [DockKey("JI_DP")] = "ulozeny-layout" };
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var vm = CreateVM(popup: mockPopup, prefs: prefs, treeLoader: TreeFailingOnSecondLoad());
        vm.AttachMiniAppHost(new NullHost());

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.SwitchCompanyAsync(1));
        vm.OnShellClosed();

        Assert.Equal("ulozeny-layout", layouts[DockKey("JI_DP")]);
        Assert.Contains(DockKey("CB_DP"), layouts.Keys);
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
            new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "", ElementId = "17" },
            new ExecutableModule { Name = "B", ExecutablePath = "b.exe", FormName = "", ElementId = "9" });

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
            new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "", ElementId = "23" });
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
            new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "", ElementId = "23" });
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

    // Povolené firmy se mění s firmou (funkční omezení DB je per IDFa) i s uživatelem.

    [Fact]
    public async Task SwitchCompany_ReplacesCompaniesWithListAllowedInNewCompany()
    {
        CompanyStruct[] allowedInJi = [new() { Id = "JI_DP", Database = "JI_DP" }, new() { Id = "PI_DP", Database = "PI_DP" }];
        var vm = CreateVM(loadAllowedCompanies: _ => allowedInJi);

        await vm.SwitchCompanyAsync(1);

        Assert.Equal(["JI_DP", "PI_DP"], vm.Companies.Select(c => c.Id));
        Assert.Equal("JI_DP", vm.Idfa);
        Assert.Equal(0, vm.SelectedCompanyIndex);
    }

    [Fact]
    public async Task SwitchCompany_NewCompanyMissingInAllowedList_StaysSelected()
    {
        // Index musí odpovídat MLRest.Idfa - firma, ve které session běží, ze seznamu nezmizí.
        var vm = CreateVM(loadAllowedCompanies: _ => OneCompany);

        await vm.SwitchCompanyAsync(1);

        Assert.Equal(["CB_DP", "JI_DP"], vm.Companies.Select(c => c.Id));
        Assert.Equal("JI_DP", vm.Idfa);
    }

    [Fact]
    public async Task SwitchCompany_ReplacedList_NotifiesSwitcherBindings()
    {
        var vm = CreateVM(loadAllowedCompanies: _ => OneCompany);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        await vm.SwitchCompanyAsync(1);

        Assert.Contains(nameof(vm.CompanySuggestions), notified);
        Assert.Contains(nameof(vm.HasMultipleCompanies), notified);
    }

    [Fact]
    public async Task SwitchCompany_LoadAllowedFails_ReportsAndKeepsSessionAndPreviousList()
    {
        // Seznam je doplněk hotové session: jeho selhání se ohlásí, ale shell nevyprázdní (legacy frmNoMDI.vb:3271).
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(popup: popup,
                          treeLoader: MainTreeWith(new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "" }),
                          loadAllowedCompanies: _ => throw new InvalidOperationException("server je mimo"));

        await vm.SwitchCompanyAsync(1);

        popup.Received(1).Error(Arg.Any<InvalidOperationException>());
        Assert.Single(vm.MainMenuTree);
        Assert.Equal("JI_DP", vm.Idfa);
        Assert.Equal(["CB_DP", "JI_DP"], vm.Companies.Select(c => c.Id));
    }

    /// <summary>Inline runner, jen seznam firem čeká na <see cref="Release"/> - pomalý server.</summary>
    private sealed class GatedCompanyListRunner(IMLRest rest) : IRestCallRunner
    {
        private readonly TaskCompletionSource _gate = new();
        public void Release() => _gate.TrySetResult();

        public Task<T> RunAsync<T>(Func<IMLRest, T> call)
        {
            if (typeof(T) == typeof(IReadOnlyList<CompanyStruct>))
                return _gate.Task.ContinueWith(_ => call(rest), CancellationToken.None,
                                               TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            try { return Task.FromResult(call(rest)); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }
    }

    [Fact]
    public async Task SwitchCompany_DoesNotWaitForAllowedList()
    {
        // Přepínač je po dobu příkazu zablokovaný - pomalý pomocný dotaz by ho tiše vyřadil.
        var ml = MockMLRest();
        var runner = new GatedCompanyListRunner(ml);
        var vm = CreateVM(mlRest: ml, restRunner: runner, loadAllowedCompanies: _ => OneCompany);

        var switching = vm.SwitchCompanyAsync(1);

        Assert.True(switching.IsCompleted);   // seznam ještě nedorazil
        Assert.Equal("JI_DP", vm.Idfa);
        runner.Release();
        await switching;
    }

    [Fact]
    public Task SwitchCompany_PreviousCompanyListArrivingMidSwitch_StillSelectsTarget() => Task.Run(async () =>
    {
        // Rychle A→B→C: seznam povolených firem pro B doběhne až uvnitř přepínání na C (dialogy switche
        // pumpují dispatcher). Index spočítaný ze starého seznamu by pak mířil jinam než MLRest.Idfa.
        // Task.Run bez SynchronizationContext: doběhlý seznam se promítne hned při otevření brány.
        CompanyStruct[] three =
        [
            new() { Id = "CB_DP", Database = "CB_DP" },
            new() { Id = "JI_DP", Database = "JI_DP" },
            new() { Id = "PI_DP", Database = "PI_DP" },
        ];
        var ml = MockMLRest();
        var runner = new GatedCompanyListRunner(ml);
        bool releaseOnClose = false;
        EdisonMainViewModel? vmRef = null;
        string[]? listMidSwitch = null;
        var vm = CreateVM(companies: three, mlRest: ml, restRunner: runner,
                          closeAllWindows: () =>
                          {
                              if (!releaseOnClose) return;
                              runner.Release();
                              listMidSwitch = [.. vmRef!.Companies.Select(c => c.Id)];
                          },
                          loadAllowedCompanies: _ => [new CompanyStruct { Id = "JI_DP", Database = "JI_DP" }]);
        vmRef = vm;

        await vm.SwitchCompanyAsync(1);   // A→B, seznam pro B čeká na bránu
        releaseOnClose = true;
        await vm.SwitchCompanyAsync(2);   // B→C podle indexu z původního seznamu

        Assert.NotNull(listMidSwitch);
        Assert.Equal(["JI_DP"], listMidSwitch);   // předpoklad: seznam pro B se vyměnil uprostřed switche
        Assert.Equal("PI_DP", vm.Idfa);
        Assert.Contains("PI_DP", vm.Companies.Select(c => c.Id));
    });

    [Fact]
    public async Task SwitchCompany_LicenceRefreshDuringListLoad_KeepsList()
    {
        // Obnova licence session nemění - seznam nové firmy se nesmí zahodit.
        CompanyStruct[] allowedInJi = [new() { Id = "JI_DP", Database = "JI_DP" }];
        EdisonMainViewModel? vmRef = null;
        var vm = CreateVM(loadAllowedCompanies: _ =>
        {
            vmRef!.RefreshLicenceCommand.Execute(null);
            return allowedInJi;
        });
        vmRef = vm;

        await vm.SwitchCompanyAsync(1);

        Assert.Equal(["JI_DP"], vm.Companies.Select(c => c.Id));
    }

    [Fact]
    public async Task SwitchCompany_ListLoadFailingAfterUserSwitch_NotReported()
    {
        // Chyba patří session starého uživatele (odhlášený GUID) - nad novou by byla matoucí.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        EdisonMainViewModel? vmRef = null;
        var vm = CreateVM(services: svc,
                          switchUser: SwitchUserInto(svc, "PI_DP", new CompanyStruct { Id = "PI_DP", Database = "PI_DP" }),
                          loadAllowedCompanies: _ =>
                          {
                              vmRef!.LoginAsAnotherUserCommand.Execute(null);
                              throw new InvalidOperationException("zámek neexistuje");
                          });
        vmRef = vm;

        await vm.SwitchCompanyAsync(1);

        popup.DidNotReceiveWithAnyArgs().Error(default!);
        Assert.Equal(["PI_DP"], vm.Companies.Select(c => c.Id));
    }

    [Fact]
    public async Task SwitchCompany_AllowedListArrivingAfterUserSwitch_IsDropped()
    {
        // Mezitím se přihlásil jiný uživatel - seznam starého by přepsal jeho povolené firmy.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        EdisonMainViewModel? vmRef = null;
        var vm = CreateVM(services: svc,
                          switchUser: SwitchUserInto(svc, "PI_DP", new CompanyStruct { Id = "PI_DP", Database = "PI_DP" }),
                          loadAllowedCompanies: _ =>
                          {
                              vmRef!.LoginAsAnotherUserCommand.Execute(null);
                              return OneCompany;
                          });
        vmRef = vm;

        await vm.SwitchCompanyAsync(1);

        Assert.Equal(["PI_DP"], vm.Companies.Select(c => c.Id));
    }

    private static Func<SwitchUserOutcome> SwitchUserInto(EDServices svc, string idfa, params CompanyStruct[] allowed) =>
        () =>
        {
            svc.MLRest = MockMLRest(idfa);   // SessionFactory vymění MLRest služeb za session nového uživatele
            return new(SwitchUserResult.Success, allowed);
        };

    [Fact]
    public async Task LoginAsAnotherUser_ReplacesCompaniesWithNewUsersList()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        var vm = CreateVM(services: svc, switchUser: SwitchUserInto(svc, "JI_DP", new CompanyStruct { Id = "JI_DP", Database = "JI_DP" }));

        await vm.LoginAsAnotherUserAsync();

        Assert.Equal(["JI_DP"], vm.Companies.Select(c => c.Id));
        Assert.Equal("JI_DP", vm.Idfa);
        Assert.False(vm.HasMultipleCompanies);
    }

    [Fact]
    public async Task LoginAsAnotherUser_CompanyChanged_SavesLayoutUnderOldCompany()
    {
        // Session nového uživatele běží v jiné firmě: dock staré se nesmí uložit pod novou.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        var prefs = Substitute.For<ILocalPrefs>();
        var layouts = new Dictionary<string, string>();
        prefs.MiniAppLayouts.Returns(layouts);
        var vm = CreateVM(services: svc, prefs: prefs,
                          switchUser: SwitchUserInto(svc, "JI_DP", new CompanyStruct { Id = "JI_DP", Database = "JI_DP" }));
        vm.AttachMiniAppHost(Substitute.For<IMiniAppHost>());

        await vm.LoginAsAnotherUserAsync();

        Assert.Contains(DockKey("CB_DP"), layouts.Keys);
        Assert.DoesNotContain(DockKey("JI_DP"), layouts.Keys);
    }

    [Fact]
    public async Task LoginAsAnotherUser_CompanyChanged_RemembersItForNextStart()
    {
        // Jinak by další start zkusil firmu, do které poslední uživatel nesmí.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        var prefs = Substitute.For<ILocalPrefs>();
        var vm = CreateVM(services: svc, prefs: prefs,
                          switchUser: SwitchUserInto(svc, "JI_DP", new CompanyStruct { Id = "JI_DP", Database = "JI_DP" }));

        await vm.LoginAsAnotherUserAsync();

        prefs.Received().LastIdfa = "JI_DP";
        prefs.Received().Save();
    }

    [Fact]
    public async Task LoginAsAnotherUser_SameCompany_DoesNotRewriteLastIdfa()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        var prefs = Substitute.For<ILocalPrefs>();
        var vm = CreateVM(services: svc, prefs: prefs, switchUser: SwitchUserInto(svc, "CB_DP", Companies));

        await vm.LoginAsAnotherUserAsync();

        prefs.DidNotReceive().LastIdfa = Arg.Any<string?>();
    }

    [Fact]
    public async Task LoginAsAnotherUser_RebuildFails_ThenShellClosed_KeepsSavedLayout()
    {
        // Dock je před přestavbou zavřený a po selhání zůstane prázdný - zavření shellu ho nesmí
        // uložit přes layout, který se uložil při výměně.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        var layouts = new Dictionary<string, string>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var host = Substitute.For<IMiniAppHost>();
        host.SaveLayout().Returns("<layout/>", "<prazdny-dock/>");
        var vm = CreateVM(services: svc, prefs: prefs, treeLoader: TreeFailingOnSecondLoad(),
                          switchUser: SwitchUserInto(svc, "CB_DP", Companies));
        vm.AttachMiniAppHost(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoginAsAnotherUserAsync());
        vm.OnShellClosed();

        Assert.Equal("<layout/>", layouts[DockKey("CB_DP")]);
    }

    // Mock MLRest vrací pro Server a LoginName "" a pro Port 0.
    private static string DockKey(string idfa, string login = "") => new DockScope("", 0, idfa, login).Key;

    [Fact]
    public async Task LoginAsAnotherUser_SameCompany_KeepsDockPerUser()
    {
        // Sdílený terminál: další uživatel v téže firmě má vlastní dock, předchozímu ho nepřepíše.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = MockServices(popup: popup);
        IMLRest Session(string login)
        {
            var ml = MockMLRest();
            ml.Server.Returns("dev");
            ml.Port.Returns(57770);
            ml.LoginName.Returns(login);
            return ml;
        }
        var layouts = new Dictionary<string, string>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(layouts);
        var host = Substitute.For<IMiniAppHost>();
        host.SaveLayout().Returns("<novak/>", "<svoboda/>");
        var vm = CreateVM(services: svc, mlRest: Session("novak"), prefs: prefs, switchUser: () =>
        {
            svc.MLRest = Session("svoboda");
            return new(SwitchUserResult.Success, Companies);
        });
        vm.AttachMiniAppHost(host);

        await vm.LoginAsAnotherUserAsync();
        vm.OnShellClosed();

        Assert.Equal("<novak/>", layouts[new DockScope("dev", 57770, "CB_DP", "novak").Key]);
        Assert.Equal("<svoboda/>", layouts[new DockScope("dev", 57770, "CB_DP", "svoboda").Key]);
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

    // Okno čte URL přímo v handleru notifikace a stejnou URL zahodí jako duplicitní, takže
    // rozhoduje hodnota v okamžiku notifikace, ne až po doběhnutí příkazu.
    private static List<string> SupportWidgetUrlsAtNotification(EdisonMainViewModel vm)
    {
        var urls = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EdisonMainViewModel.SupportWidgetUrl)) urls.Add(vm.SupportWidgetUrl);
        };
        return urls;
    }

    [Fact]
    public async Task SwitchCompany_NotifiesSupportWidgetUrlWithNewIdfa()
    {
        // Pruh podpory cílí sdělení podle firmy - po přepnutí musí widget dostat novou IdFa.
        var vm = CreateVM();
        var urls = SupportWidgetUrlsAtNotification(vm);

        await vm.SwitchCompanyAsync(1);

        Assert.Contains(urls, u => u.Contains("idfa=JI_DP&"));
    }

    // Login jiného uživatele s výměnou MLRest a e-mailu tak, jak ji dělá SessionFactory.Establish.
    private static EdisonMainViewModel VmSwitchingNovakToSvoboda(IModuleTreeLoader? treeLoader = null)
    {
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var services = MockServices(popup: mockPopup);
        services.UserEmail = "novak@firma.cz";
        var oldRest = MockMLRest();
        oldRest.LoginName.Returns("novak");
        var newRest = MockMLRest();
        newRest.LoginName.Returns("svoboda");
        var vm = CreateVM(services: services, mlRest: oldRest, treeLoader: treeLoader, switchUser: () =>
        {
            services.MLRest = newRest;
            services.UserEmail = "svoboda@firma.cz";
            return new(SwitchUserResult.Success);
        });
        Assert.Contains("user=novak&", vm.SupportWidgetUrl);   // předpoklad
        return vm;
    }

    [Fact]
    public async Task LoginAsAnotherUser_Success_NotifiesSupportWidgetUrlWithNewUser()
    {
        // Firma zůstává stejná, takže notifikace Idfa by widget nepřenačetla a nesl by starého uživatele.
        var vm = VmSwitchingNovakToSvoboda();
        var urls = SupportWidgetUrlsAtNotification(vm);

        await vm.LoginAsAnotherUserAsync();

        var url = Assert.Single(urls);
        Assert.Contains("user=svoboda&", url);
        Assert.EndsWith("email=svoboda%40firma.cz", url);
    }

    [Fact]
    public async Task LoginAsAnotherUser_LoadFails_StillNotifiesSupportWidgetUrlWithNewUser()
    {
        // Session už patří novému uživateli i po selhaném loadu - pruh nesmí cílit na předchozího.
        var vm = VmSwitchingNovakToSvoboda(TreeFailingOnSecondLoad());
        var urls = SupportWidgetUrlsAtNotification(vm);

        await Assert.ThrowsAsync<InvalidOperationException>(vm.LoginAsAnotherUserAsync);

        Assert.Contains("user=svoboda&", Assert.Single(urls));
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
            new ExecutableModule { Name = "Turnusy", ExecutablePath = "t.exe", FormName = "" },
            new ExecutableModule { Name = "Objednávky", ExecutablePath = "o.exe", FormName = "" },
        });
        var vm = CreateVM(popup: mockPopup, treeLoader: mockTree);
        vm.TreeSearchText = "turnus";

        await vm.SwitchCompanyAsync(vm.SelectedCompanyIndex == 0 ? 1 : 0);

        var modules = vm.MainMenuTree.Cast<ExecutableModule>().ToList();
        Assert.True(modules.Single(m => m.DisplayedName == "Turnusy").IsVisibleBySearch);
        Assert.False(modules.Single(m => m.DisplayedName == "Objednávky").IsVisibleBySearch);
    }

    // FirstSearchResult - cíl Enteru v poli „Hledat modul" (HD 13132/F15).

    private static EdisonMainViewModel SearchableVM() =>
        CreateVM(treeLoader: TreeLoaderWith(
        [
            new ExecutableModule { Name = "Objednávky", ExecutablePath = "o.exe", FormName = "" },
            new ExecutableModule { Name = "Turnusy", ExecutablePath = "t.exe", FormName = "" },
        ]));

    [Fact]
    public void FirstSearchResult_DoesNotWaitForDebouncer()
    {
        // Enter přijde dřív, než doběhne 150ms debounce (v testech DispatcherTimer nikdo nepumpuje,
        // takže by bez flushe filtrace neproběhla nikdy).
        var vm = SearchableVM();
        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "main");
        vm.TreeSearchText = "turnus";

        Assert.Equal("Turnusy", vm.FirstSearchResult()!.DisplayedName);
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
    public void CloseMiniAppsCommand_KeepsDesktop_ResetClosesEverything()
    {
        var services = MockServices();
        var vm = CreateVM(services: services, miniAppCatalog: PinnedDesktopCatalog(services));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);
        var desktop = host.Panes.Single();

        vm.CloseMiniAppsCommand.Execute(null);
        Assert.False(desktop.IsClosed);

        // Reset zavře i Plochu a postaví novou sadu.
        vm.ResetMiniAppsCommand.Execute(null);
        Assert.True(desktop.IsClosed);
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
    public void ShellClose_ConfirmedThenClosed_SavesLayoutExactlyOnce_AndClosesWindows()
    {
        // Closing jen potvrzuje, ukládá až Closed - dřív se layout zapisoval v obou.
        var mockPopup = Substitute.For<IPopupDisplayer>();
        mockPopup.Confirm(Arg.Any<string>()).Returns(true);
        var widths = new Dictionary<string, double>();
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.TreeWidths.Returns(widths);
        int windowsClosed = 0;
        var vm = CreateVM(popup: mockPopup, prefs: prefs, closeAllWindows: () => windowsClosed++);
        vm.AttachTreeLayout(() => 275, _ => { });
        vm.AttachMiniAppHost(new NullHost());
        prefs.ClearReceivedCalls();

        Assert.True(vm.ConfirmShellClose());
        vm.OnShellClosed();

        Assert.Equal(275, widths[DockKey("CB_DP")]);
        prefs.Received(1).Save();
        Assert.Equal(1, windowsClosed);
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
                          switchUser: () => new(SwitchUserResult.Cancelled));

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
    public void IsTestMenuVisible_FollowsInternalDiagnostics()
    {
        // Menu „Test funkcí" (Vyvolat chybu, mock formuláře) nesmí být u zákazníka vidět.
        bool original = InternalDiagnostics.Enabled;
        try
        {
            var vm = CreateVM();

            InternalDiagnostics.Enabled = false;
            Assert.False(vm.IsTestMenuVisible);

            InternalDiagnostics.Enabled = true;
            Assert.True(vm.IsTestMenuVisible);
        }
        finally
        {
            InternalDiagnostics.Enabled = original;
        }
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
