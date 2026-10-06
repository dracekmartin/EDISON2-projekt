using System.Text.Json;
using System.Text.Json.Nodes;
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
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Oblíbené a Používané z pohledu VM: toggle přes command, usage hook, session load (rozbalení,
/// záložka, předpřipravené Oblíbené) a preference. Logiku plochy testuje <see cref="FavoritesCoordinatorTests"/>.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class FavoritesAndUsageTests
{
    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { Name = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static ModuleFamily Family(string name, params IModule[] children)
    {
        var f = new ModuleFamily { Name = name };
        foreach (var c in children) f.NestedModules.Add(c);
        return f;
    }

    [Fact]
    public void BuildUsedList_RanksByCountDesc_ThenRecency()
    {
        var a = Exe("A"); var b = Exe("B"); var c = Exe("C");
        var tree = new IModule[] { a, b, c };
        var usage = new Dictionary<string, UsageEntry>
        {
            ["A"] = new(Count: 3, LastUsedUtc: new DateTime(2026, 1, 1)),
            ["B"] = new(Count: 5, LastUsedUtc: new DateTime(2026, 1, 1)),
            ["C"] = new(Count: 1, LastUsedUtc: new DateTime(2026, 1, 1)),
        };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Equal(["B", "A", "C"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_TieBreakByLastUsed()
    {
        var a = Exe("A"); var d = Exe("D");
        var tree = new IModule[] { a, d };
        var usage = new Dictionary<string, UsageEntry>
        {
            ["A"] = new(Count: 2, LastUsedUtc: new DateTime(2026, 1, 2)),
            ["D"] = new(Count: 2, LastUsedUtc: new DateTime(2026, 1, 1)),
        };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Equal(["A", "D"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_CapsResultCount()
    {
        var tree = new IModule[] { Exe("A"), Exe("B"), Exe("C") };
        var usage = new Dictionary<string, UsageEntry>
        {
            ["A"] = new(3, default), ["B"] = new(2, default), ["C"] = new(1, default),
        };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 2);

        Assert.Equal(["A", "B"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_ExcludesModulesNotInTree()
    {
        // Modul, který už není viditelný (licence/přístup) → vypadne ze žebříčku.
        var tree = new IModule[] { Exe("A") };
        var usage = new Dictionary<string, UsageEntry> { ["A"] = new(1, default), ["999"] = new(99, default) };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Equal(["A"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_SharesTreeInstances()
    {
        var a = Exe("A");
        var tree = new IModule[] { Family("F", a) };
        var usage = new Dictionary<string, UsageEntry> { ["A"] = new(1, default) };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Same(a, Assert.Single(result));
    }

    [Fact]
    public async Task Prefs_LoadShortcuts_ParsesList()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse(
                """[{"Kind":"MenuModule","Id":"100","ElementId":"100","Name":"A","Col":1,"Row":3}]""")));
        var svc = new ModulePreferencesService(prefs);

        var dto = Assert.Single((await svc.LoadShortcutsAsync())!);
        Assert.Equal("MenuModule", dto.Kind);
        Assert.Equal("100", dto.ElementId);
        Assert.Equal((1, 3), (dto.Col, dto.Row));
    }

    [Fact]
    public async Task Prefs_LoadShortcuts_NullBlob_ReturnsEmpty()
    {
        var prefs = Substitute.For<IUserPrefsService>();   // LoadAsync default → Task<null>
        var svc = new ModulePreferencesService(prefs);

        Assert.Empty((await svc.LoadShortcutsAsync())!);
    }

    [Fact]
    public async Task Prefs_LoadShortcuts_PrefsThrow_ReturnsNull_NotEmpty()
    {
        // Výpadek ≠ prázdná plocha: null říká VM „neseeduj a nepersistuj", prázdno by ji přepsalo.
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromException<JsonNode?>(new InvalidOperationException("server down")));
        var svc = new ModulePreferencesService(prefs);

        Assert.Null(await svc.LoadShortcutsAsync());
    }

    [Fact]
    public void LegacyModuleExit_ReloadsDesktop_UntilShellClosed()
    {
        // Agenda zapisuje zástupce přímo do Caché, shell si blob po jejím exitu přečte znovu. Zavření
        // shellu ale zabíjí agendy - každý jejich exit by jinak četl blob uprostřed ukončení.
        var prefs = Substitute.For<IUserPrefsService>();
        var nav = Substitute.For<IWindowNavigator>();
        var vm = EdisonMainViewModelHarness.CreateVM(services: MockServices(nav: nav), companies: OneCompany,
            treeLoader: TreeLoaderWith([Exe("100", "App")]), userPrefs: prefs);
        prefs.ClearReceivedCalls();

        nav.LegacyModuleExited += Raise.Event<Action>();
        prefs.Received(1).LoadAsync("EDISONMAIN2", "Shortcuts");
        prefs.ClearReceivedCalls();

        vm.OnShellClosed();
        nav.LegacyModuleExited += Raise.Event<Action>();

        prefs.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Prefs_LoadShortcuts_CorruptBlob_ReturnsEmpty()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""{"not":"a list"}""")));
        var svc = new ModulePreferencesService(prefs);

        Assert.Empty((await svc.LoadShortcutsAsync())!);
    }

    [Fact]
    public async Task Prefs_Shortcuts_RoundTripsNonMenuItem_WithReportPayload()
    {
        // Zástupce tiskové sestavy (mimo hlavní nabídku) musí přežít uložení i načtení
        // včetně report payloadu - jinak by ho plocha po restartu neumožnila spustit.
        JsonNode? written = null;
        var prefs = Substitute.For<IUserPrefsService>();
        await prefs.SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Do<JsonNode>(n => written = n));
        var svc = new ModulePreferencesService(prefs);
        var report = new ReportPayload("AD", "EkonomickySystem", "1", "S12", "P7", JeVerejny: true);

        await svc.SaveShortcutsAsync([new DesktopShortcutDto(nameof(ShortcutKind.PrintReport), "PrehledSkod",
            "31001", "Přehled škod", "VOZIDLA2.exe", Col: 2, Row: 1, Report: report)]);

        prefs.LoadAsync("EDISONMAIN2", "Shortcuts").Returns(Task.FromResult(written));
        var dto = Assert.Single((await svc.LoadShortcutsAsync())!);
        Assert.Equal(nameof(ShortcutKind.PrintReport), dto.Kind);
        Assert.Equal("PrehledSkod", dto.Id);
        Assert.Equal((2, 1), (dto.Col, dto.Row));
        Assert.Equal(report, dto.Report);
    }

    [Fact]
    public async Task Prefs_LoadUsage_ParsesMap()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "ModuleUsage")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""{"100":{"Count":5,"LastUsedUtc":"2026-01-01T00:00:00"}}""")));
        var svc = new ModulePreferencesService(prefs);

        var usage = await svc.LoadUsageAsync();
        Assert.Equal(5, usage["100"].Count);
    }

    [Fact]
    public async Task Prefs_SaveShortcuts_WritesScopedBlob()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var svc = new ModulePreferencesService(prefs);

        await svc.SaveShortcutsAsync([new DesktopShortcutDto(nameof(ShortcutKind.MenuModule), "100", "100", "A", Col: 2, Row: 0)]);

        await prefs.Received().SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Any<JsonNode>());
    }

    [Fact]
    public void ToggleFavorite_AddsAndRemoves_FromFavoritesTree_AndPersists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);
        var module = vm.MainMenuTree.OfType<ExecutableModule>().Single();

        Assert.Empty(vm.FavoritesTree);
        Assert.False(module.IsFavorite);

        vm.ToggleFavoriteCommand.Execute(module);

        Assert.True(module.IsFavorite);
        Assert.Same(module, Assert.Single(vm.FavoritesTree));
        prefs.Received().SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Any<JsonNode>());

        vm.ToggleFavoriteCommand.Execute(module);

        Assert.False(module.IsFavorite);
        Assert.Empty(vm.FavoritesTree);
    }

    [Fact]
    public void OpenModule_RecordsUsage_AppearsInUsedList_AndPersists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);
        var module = vm.MainMenuTree.OfType<ExecutableModule>().Single();

        Assert.Empty(vm.UsedModules);

        vm.OpenModuleCommand.Execute(module);

        Assert.Same(module, Assert.Single(vm.UsedModules));
        prefs.Received().SaveAsync("EDISONMAIN2", "ModuleUsage", Arg.Any<JsonNode>());
    }

    [Fact]
    public void TreeExpansion_RestoredFromPrefs()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "TreeExpansion")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""["main:0"]""")));
        var vm = CreateVM(prefs, tree: [Family("MHD", Exe("1"))]);

        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();
        Assert.True(family.IsExpanded);   // index 0 v "main" → rozbaleno z uložených prefs
    }

    [Fact]
    public void TreeExpansion_UserExpand_Persists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Family("MHD", Exe("1"))]);
        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();
        prefs.ClearReceivedCalls();   // ignoruj načítání během konstrukce

        family.IsExpanded = true;     // simuluje TwoWay zápis z TreeViewItem při rozbalení

        prefs.Received().SaveAsync("EDISONMAIN2", "TreeExpansion", Arg.Any<JsonNode>());
    }

    [Fact]
    public void ShortcutsLoadedAtStartup_PrebuildsFavoritesTree()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse(
                """[{"Kind":"MenuModule","Id":"100","ElementId":"100","Name":"App"}]""")));
        var vm = CreateVM(prefs, tree: [Exe("100", "App"), Exe("200", "Other")]);

        var fav = Assert.IsType<ExecutableModule>(Assert.Single(vm.FavoritesTree));
        Assert.Equal("100", fav.ElementId);
        Assert.False(vm.HasNoFavorites);
        // Bez uložené volby vždy Hlavní nabídka - existující oblíbené default nepřebíjejí.
        Assert.Equal("main", vm.SelectedTab.Key);
    }

    [Fact]
    public void SelectedTab_RestoredFromPrefs()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "SelectedTab")
             .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("\"partners\"")));
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);

        Assert.Equal("partners", vm.SelectedTab.Key);   // uložená volba má přednost před výchozí
    }

    [Fact]
    public void SelectedTab_UnknownKey_FallsBackToFirstTab()
    {
        // Zrušená / přejmenovaná záložka nesmí shodit shell ani nechat výběr prázdný.
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "SelectedTab")
             .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("\"zruseno\"")));
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);

        Assert.Equal(vm.TreeTabs[0].Key, vm.SelectedTab.Key);
    }

    [Fact]
    public void SelectedTab_UserChange_PersistsKey()
    {
        JsonNode? written = null;
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.SaveAsync("EDISONMAIN2", "SelectedTab", Arg.Do<JsonNode>(n => written = n));
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);
        prefs.ClearReceivedCalls();

        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "used");

        prefs.Received().SaveAsync("EDISONMAIN2", "SelectedTab", Arg.Any<JsonNode>());
        Assert.Equal("\"used\"", written!.ToJsonString());   // klíč, ne index - přeuspořádání záložek volbu nepřepne
    }

    [Fact]
    public void SelectedTab_Change_RestoresSelectionOfThatTab()
    {
        // Stromy zůstávají živé, takže toolbar akce musí po návratu cílit na uzel viditelné záložky.
        var vm = CreateVM(Substitute.For<IUserPrefsService>(), tree: [Exe("100", "App")]);
        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "main");
        var module = vm.MainMenuTree.OfType<ExecutableModule>().Single();
        vm.SelectedModule = module;

        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "used");
        Assert.Null(vm.SelectedModule);

        vm.SelectedTab = vm.TreeTabs.Single(t => t.Key == "main");
        Assert.Same(module, vm.SelectedModule);
    }

    private static EdisonMainViewModel CreateVM(IUserPrefsService prefs, IReadOnlyList<IModule> tree) =>
        EdisonMainViewModelHarness.CreateVM(
            companies: OneCompany, treeLoader: TreeLoaderWith(tree), userPrefs: prefs);
}
