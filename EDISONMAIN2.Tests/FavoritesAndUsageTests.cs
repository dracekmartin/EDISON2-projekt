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
/// Testy featury Oblíbené (ruční ★, ořezaný strom) + Používané (automatický žebříček).
/// Statické buildery jsou čisté funkce (jako TryFilter); VM integrace ověřuje toggle + usage hook.
/// </summary>
public class FavoritesAndUsageTests
{
    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static ModuleFamily Family(string name, params IModule[] children)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var c in children) f.NestedModules.Add(c);
        return f;
    }

    // ---------- BuildFavoritesTree ----------------------------------------

    [Fact]
    public void BuildFavoritesTree_KeepsOnlyFavoritedLeaves_PrunesEmptyFamilies()
    {
        var keep = Exe("100", "Keep");
        var drop = Exe("200", "Drop");
        var tree = new IModule[] { Family("A", keep, drop), Family("B", Exe("300")) };

        var result = EdisonMainViewModel.BuildFavoritesTree([tree], new HashSet<string> { "100" });

        // Jen rodina A zůstane (má oblíbené dítě), uvnitř jen "Keep". Rodina B i "Drop" zmizí.
        var family = Assert.IsType<ModuleFamily>(Assert.Single(result));
        Assert.Equal("A", family.DisplayedName);
        var leaf = Assert.IsType<ExecutableModule>(Assert.Single(family.NestedModules));
        Assert.Equal("100", leaf.ElementId);
    }

    [Fact]
    public void BuildFavoritesTree_SharesLeafInstances_NotCopies()
    {
        var fav = Exe("100");
        var tree = new IModule[] { Family("A", fav) };

        var result = EdisonMainViewModel.BuildFavoritesTree([tree], new HashSet<string> { "100" });

        var leaf = (ExecutableModule)((ModuleFamily)result[0]).NestedModules[0];
        // Sdílená instance → přepnutí IsFavorite v jednom stromě se projeví ve druhém.
        Assert.Same(fav, leaf);
    }

    [Fact]
    public void BuildFavoritesTree_EmptyWhenNoFavorites()
    {
        var tree = new IModule[] { Family("A", Exe("100")) };

        Assert.Empty(EdisonMainViewModel.BuildFavoritesTree([tree], new HashSet<string>()));
    }

    [Fact]
    public void BuildFavoritesTree_SpansMultipleSourceTrees()
    {
        var main = new IModule[] { Exe("100") };
        var system = new IModule[] { Exe("200") };

        var result = EdisonMainViewModel.BuildFavoritesTree([main, system], new HashSet<string> { "100", "200" });

        Assert.Equal(2, result.Count);
    }

    // ---------- BuildUsedList ---------------------------------------------

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

    // ---------- ModulePreferencesService ----------------------------------

    [Fact]
    public async Task Prefs_LoadShortcuts_ParsesList()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse(
                """[{"Kind":"MenuModule","Id":"100","ElementId":"100","Name":"A","Slot":3}]""")));
        var svc = new ModulePreferencesService(prefs);

        var dto = Assert.Single(await svc.LoadShortcutsAsync());
        Assert.Equal("MenuModule", dto.Kind);
        Assert.Equal("100", dto.ElementId);
        Assert.Equal(3, dto.Slot);
    }

    [Fact]
    public async Task Prefs_LoadShortcuts_NullBlob_ReturnsEmpty()
    {
        var prefs = Substitute.For<IUserPrefsService>();   // LoadAsync default → Task<null>
        var svc = new ModulePreferencesService(prefs);

        Assert.Empty(await svc.LoadShortcutsAsync());
    }

    [Fact]
    public async Task Prefs_LoadShortcuts_CorruptBlob_ReturnsEmpty()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""{"not":"a list"}""")));
        var svc = new ModulePreferencesService(prefs);

        Assert.Empty(await svc.LoadShortcutsAsync());
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
            "31001", "Přehled škod", "VOZIDLA2.exe", Slot: 2, Report: report)]);

        prefs.LoadAsync("EDISONMAIN2", "Shortcuts").Returns(Task.FromResult(written));
        var dto = Assert.Single(await svc.LoadShortcutsAsync());
        Assert.Equal(nameof(ShortcutKind.PrintReport), dto.Kind);
        Assert.Equal("PrehledSkod", dto.Id);
        Assert.Equal(2, dto.Slot);
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

        await svc.SaveShortcutsAsync([new DesktopShortcutDto(nameof(ShortcutKind.MenuModule), "100", "100", "A", Slot: 2)]);

        await prefs.Received().SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Any<JsonNode>());
    }

    // ---------- VM integrace: toggle + usage ------------------------------

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
    public void ToggleFavorite_Family_FavoritesAllChildren_AndAggregatesFlag()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Family("MHD", Exe("1"), Exe("2"))]);
        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();

        Assert.False(family.IsFavorite);
        Assert.Empty(vm.FavoritesTree);

        vm.ToggleFavoriteCommand.Execute(family);

        Assert.True(family.IsFavorite);
        var favFamily = Assert.IsType<ModuleFamily>(Assert.Single(vm.FavoritesTree));
        Assert.Equal(2, favFamily.NestedModules.Count);
        Assert.All(family.NestedModules.OfType<ExecutableModule>(), m => Assert.True(m.IsFavorite));
        prefs.Received().SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Any<JsonNode>());

        vm.ToggleFavoriteCommand.Execute(family);

        Assert.False(family.IsFavorite);
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
        Assert.Equal("favorites", vm.SelectedTab.Key);   // bez uložené volby výchozí Oblíbené, když nějaké jsou
    }

    [Fact]
    public void NonMenuShortcut_NotInFavoritesTree_ButSurvivesFavoriteToggle()
    {
        // Tisková sestava na ploše není položkou hlavní nabídky → do záložky Oblíbené nepatří,
        // ale změna oblíbených ji nesmí z uloženého blobu vyhodit.
        JsonNode? written = null;
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse(
                """[{"Kind":"PrintReport","Id":"PrehledSkod","ElementId":"31001","Name":"Přehled škod"}]""")));
        prefs.SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Do<JsonNode>(n => written = n));
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);

        Assert.Empty(vm.FavoritesTree);

        vm.ToggleFavoriteCommand.Execute(vm.MainMenuTree.OfType<ExecutableModule>().Single());

        Assert.Contains("PrehledSkod", written!.ToJsonString());
        Assert.Contains("\"100\"", written.ToJsonString());
    }

    [Fact]
    public void ShortcutForModuleMissingFromTree_SurvivesFavoriteToggle()
    {
        // Modul, který strom aktuálně nezná (odebraná licence) - po toggle jiného modulu
        // musí zůstat v uloženém blobu, jinak by o zástupce uživatel tiše přišel.
        JsonNode? written = null;
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Shortcuts")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse(
                """[{"Kind":"MenuModule","Id":"999","ElementId":"999","Name":"Nelicencovaný"}]""")));
        prefs.SaveAsync("EDISONMAIN2", "Shortcuts", Arg.Do<JsonNode>(n => written = n));
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);

        vm.ToggleFavoriteCommand.Execute(vm.MainMenuTree.OfType<ExecutableModule>().Single());

        Assert.Contains("\"999\"", written!.ToJsonString());
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
        Assert.Equal("\"used\"", written!.ToJsonString());   // klíč, ne index — přeuspořádání záložek volbu nepřepne
    }

    [Fact]
    public void TreeTabs_HaveUniqueKeys_AndExactlyOneNonExpandable()
    {
        // Identita záložky je persistovaný klíč; duplicita by tiše přepsala uloženou volbu.
        // Používané jsou ListBox bez stromu — jediná záložka, kam Rozbalit/Sbalit vše necílí.
        var vm = CreateVM(Substitute.For<IUserPrefsService>(), tree: [Exe("100", "App")]);

        Assert.Equal(vm.TreeTabs.Count, vm.TreeTabs.Select(t => t.Key).Distinct().Count());
        Assert.Single(vm.TreeTabs, t => !t.Expandable);
        Assert.All(vm.TreeTabs, t => Assert.Equal(t.Expandable, t.Tree != null));
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

    // ---------- harness ----------------------------------------------------

    private static EdisonMainViewModel CreateVM(IUserPrefsService prefs, IReadOnlyList<IModule> tree) =>
        EdisonMainViewModelHarness.CreateVM(
            companies: OneCompany, treeGetter: TreeGetterWith(tree), userPrefs: prefs);
}
