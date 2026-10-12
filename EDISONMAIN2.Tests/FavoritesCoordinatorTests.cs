using System.Text.Json;
using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Plocha a záložka Oblíbené bez ViewModelu: ořezaný strom, ★ ve stromu, změny z miniaplikace,
/// přenačtení po exitu agendy a ochrana uživatelovy plochy po selhaném loadu. Plocha jde na server
/// po změnách (<c>ALVA.EDControls.DesktopShortcuts</c>), celý blob nikdy: zapisují do něj i agendy.
/// </summary>
public class FavoritesCoordinatorTests
{
    private const string Scope = "EDISONMAIN2";
    private const string ShortcutsKey = "Shortcuts";

    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { Name = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static ModuleFamily Family(string name, params IModule[] children)
    {
        var f = new ModuleFamily { Name = name };
        foreach (var c in children) f.NestedModules.Add(c);
        return f;
    }

    private static DesktopShortcutDto Menu(string elementId, int? col = 0, int? row = 0) =>
        new(nameof(ShortcutKind.MenuModule), elementId, elementId, "M", Col: col, Row: row);

    private sealed record DesktopCall(string Method, List<DesktopShortcutDto> Items)
    {
        public DesktopShortcutDto Item => Assert.Single(Items);
    }

    // Stav po načtení stromů session; plochu dodá test přes Apply (jako ApplySessionData ve VM).
    private sealed class Fixture
    {
        public IUserPrefsService Prefs { get; } = Substitute.For<IUserPrefsService>();
        public IMLRest Rest { get; } = Substitute.For<IMLRest>();
        // Výchozí hlášení by chybu poslalo na podporu.
        public List<Exception> Failures { get; } = [];
        public IWindowNavigator Nav { get; } = Substitute.For<IWindowNavigator>();
        public ModuleShortcutsService Shortcuts { get; } = new();
        public ModuleTreeState Trees { get; }
        public FavoritesCoordinator Coordinator { get; }
        public int Rebuilds { get; private set; }

        public Fixture(params IModule[] main)
        {
            var modulePrefs = new ModulePreferencesService(Prefs, new InlineRestCallRunner(Rest), Failures.Add);
            Trees = new ModuleTreeState(() => modulePrefs, EdisonMainViewModelHarness.NeutralStation);
            Trees.Load(new Dictionary<string, IReadOnlyList<IModule>> { ["main"] = main, ["system"] = [], ["partners"] = [] },
                       licence: null, access: null, expansion: null);
            Coordinator = new FavoritesCoordinator(Shortcuts, Trees, () => modulePrefs, Nav, () => Rebuilds++);
        }

        // Load staví filtrované instance, proto se modul hledá až ve stromu.
        public ExecutableModule Module(string id) => Trees.AllExecutables().Single(e => e.ElementId == id);

        // Volání ALVA.EDControls.DesktopShortcuts v pořadí odeslání, s položkami z IN["item"] / IN["items"].
        public List<DesktopCall> Desktop() =>
        [
            .. Rest.ReceivedCalls()
                .Where(c => c.GetArguments() is ["ALVA.EDControls.DesktopShortcuts", _, CVar])
                .Select(c =>
                {
                    var method = (string)c.GetArguments()[1]!;
                    var input = (CVar)c.GetArguments()[2]!;
                    var items = method == "Place"
                        ? JsonSerializer.Deserialize<List<DesktopShortcutDto>>(input["items"].Value!.ToString()!)!
                        : [JsonSerializer.Deserialize<DesktopShortcutDto>(input["item"].Value!.ToString()!)!];
                    return new DesktopCall(method, items);
                }),
        ];

        public void NoWholeBlob() => Prefs.DidNotReceive().SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void BuildFavoritesTree_KeepsOnlyFavoritedLeaves_PrunesEmptyFamilies()
    {
        var keep = Exe("100", "Keep");
        var drop = Exe("200", "Drop");
        var tree = new IModule[] { Family("A", keep, drop), Family("B", Exe("300")) };

        var result = FavoritesCoordinator.BuildFavoritesTree([tree], new HashSet<string> { "100" });

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

        var result = FavoritesCoordinator.BuildFavoritesTree([tree], new HashSet<string> { "100" });

        var leaf = (ExecutableModule)((ModuleFamily)result[0]).NestedModules[0];
        // Sdílená instance → přepnutí IsFavorite v jednom stromě se projeví ve druhém.
        Assert.Same(fav, leaf);
    }

    [Fact]
    public void BuildFavoritesTree_EmptyWhenNoFavorites()
    {
        var tree = new IModule[] { Family("A", Exe("100")) };

        Assert.Empty(FavoritesCoordinator.BuildFavoritesTree([tree], new HashSet<string>()));
    }

    [Fact]
    public void BuildFavoritesTree_SpansMultipleSourceTrees()
    {
        var main = new IModule[] { Exe("100") };
        var system = new IModule[] { Exe("200") };

        var result = FavoritesCoordinator.BuildFavoritesTree([main, system], new HashSet<string> { "100", "200" });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_SeedsFavoritesTree_AndReportsRebuild()
    {
        // Callback řídí HasNoFavorites ve VM - bez něj by empty-state záložky Oblíbené visel.
        var fx = new Fixture(Exe("100", "App"), Exe("200", "Other"));

        fx.Coordinator.Apply([Menu("100")]);

        var fav = Assert.IsType<ExecutableModule>(Assert.Single(fx.Trees.FavoritesTree));
        Assert.Equal("100", fav.ElementId);
        Assert.Equal(1, fx.Rebuilds);
        Assert.Empty(fx.Desktop());   // načtené se neposílá zpět
        fx.NoWholeBlob();
    }

    // Zástupce agendy bez pozice dostane buňku až při načtení; bez zápisu by po restartu mohl skočit jinam.
    [Fact]
    public void Apply_CellAssignedOnLoad_IsPlacedOnServer()
    {
        var fx = new Fixture(Exe("100", "App"));

        fx.Coordinator.Apply([Menu("100"), new DesktopShortcutDto(nameof(ShortcutKind.PrintReport), "1003", "31001", "Sestava")]);

        var call = Assert.Single(fx.Desktop());
        Assert.Equal("Place", call.Method);
        Assert.Equal(("1003", 1, 0), (call.Item.Id, call.Item.Col, call.Item.Row));
    }

    [Fact]
    public void Apply_LoadFailed_SendsNothing()
    {
        var fx = new Fixture(Exe("100", "App"));

        fx.Coordinator.Apply(null);

        Assert.Empty(fx.Desktop());
    }

    [Fact]
    public void Toggle_Module_SendsAddWithCell_ThenRemove()
    {
        var fx = new Fixture(Exe("100", "App"), Exe("200", "Other"));
        fx.Coordinator.Apply([Menu("100")]);

        fx.Coordinator.Toggle(fx.Module("200"));
        fx.Coordinator.Toggle(fx.Module("200"));

        var calls = fx.Desktop();
        Assert.Equal(["Add", "Remove"], calls.Select(c => c.Method));
        Assert.Equal(("200", 1, 0), (calls[0].Item.ElementId, calls[0].Item.Col, calls[0].Item.Row));
        Assert.Equal("200", calls[1].Item.ElementId);
        fx.NoWholeBlob();
    }

    [Fact]
    public void Toggle_Family_FavoritesAllChildren_AggregatesFlag_AndAddsEach()
    {
        var fx = new Fixture(Family("MHD", Exe("1"), Exe("2")));
        fx.Coordinator.Apply([]);
        var family = fx.Trees.MainMenuTree.OfType<ModuleFamily>().Single();

        fx.Coordinator.Toggle(family);

        Assert.True(family.IsFavorite);
        var favFamily = Assert.IsType<ModuleFamily>(Assert.Single(fx.Trees.FavoritesTree));
        Assert.Equal(2, favFamily.NestedModules.Count);
        Assert.All(family.NestedModules.OfType<ExecutableModule>(), m => Assert.True(m.IsFavorite));
        Assert.Equal([("Add", "1"), ("Add", "2")], fx.Desktop().Select(c => (c.Method, c.Item.ElementId)));

        fx.Coordinator.Toggle(family);

        Assert.False(family.IsFavorite);
        Assert.Empty(fx.Trees.FavoritesTree);
    }

    [Fact]
    public void DesktopRemove_ViaSharedService_RebuildsFavoritesAndPersists()
    {
        // Miniaplikace Plocha mění sdílenou službu přímo, strom o tom ví jen z jejího eventu.
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([Menu("100")]);
        Assert.Single(fx.Trees.FavoritesTree);   // předpoklad

        fx.Shortcuts.Remove(fx.Shortcuts.Items.Single());

        Assert.Empty(fx.Trees.FavoritesTree);
        var call = Assert.Single(fx.Desktop());
        Assert.Equal(("Remove", "100"), (call.Method, call.Item.ElementId));
    }

    // Přesun na obsazenou buňku prohodí dvě dlaždice: obě pozice jdou jedním voláním.
    [Fact]
    public void Reorder_Swap_SendsOnePlaceWithBothTiles()
    {
        var fx = new Fixture(Exe("100"), Exe("200"));
        fx.Coordinator.Apply([Menu("100", 0, 0), Menu("200", 3, 1)]);

        fx.Shortcuts.PlaceAt(fx.Shortcuts.Items[0], 3, 1);

        var call = Assert.Single(fx.Desktop());
        Assert.Equal("Place", call.Method);
        Assert.Equal([("100", 3, 1), ("200", 0, 0)],
                     call.Items.Select(i => (i.ElementId, i.Col ?? -1, i.Row ?? -1)).OrderBy(i => i.ElementId));
    }

    // Drop ze stromu na buňku: dlaždici nese jedno Add s cílovou buňkou, dva zápisy by závisely na pořadí doběhnutí.
    [Fact]
    public void DropOnCell_SendsSingleAddWithTargetCell_AndPlacesOnlyTheOccupant()
    {
        var fx = new Fixture(Exe("100"), Exe("200"));
        fx.Coordinator.Apply([Menu("100", 2, 0)]);

        fx.Shortcuts.Add(fx.Module("200"), (2, 0));

        var calls = fx.Desktop();
        Assert.Equal(["Add", "Place"], calls.Select(c => c.Method));
        Assert.Equal(("200", 2, 0), (calls[0].Item.ElementId, calls[0].Item.Col, calls[0].Item.Row));
        Assert.Equal(("100", 0, 0), (calls[1].Item.ElementId, calls[1].Item.Col, calls[1].Item.Row));
    }

    [Fact]
    public void NonMenuShortcut_NotInFavoritesTree_ButSurvivesFavoriteToggle()
    {
        // Tisková sestava na ploše není položkou hlavní nabídky → do záložky Oblíbené nepatří,
        // a změna oblíbených na ni na serveru nesahá.
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([new DesktopShortcutDto(nameof(ShortcutKind.PrintReport), "PrehledSkod", "31001", "Přehled škod", Col: 0, Row: 0)]);

        Assert.Empty(fx.Trees.FavoritesTree);

        fx.Coordinator.Toggle(fx.Module("100"));

        var call = Assert.Single(fx.Desktop());
        Assert.Equal(("Add", "100"), (call.Method, call.Item.ElementId));
    }

    [Fact]
    public void ShortcutForModuleMissingFromTree_SurvivesFavoriteToggle()
    {
        // Modul, který strom aktuálně nezná (odebraná licence) - toggle jiného modulu ho na serveru
        // nesmí odebrat, jinak by o zástupce uživatel tiše přišel. Jeho buňku nová dlaždice nepřevezme.
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([Menu("999")]);

        fx.Coordinator.Toggle(fx.Module("100"));

        var call = Assert.Single(fx.Desktop());
        Assert.Equal(("Add", "100", 1), (call.Method, call.Item.ElementId, call.Item.Col));
        fx.NoWholeBlob();
    }

    // Na serveru je varianta AD ze shellu i zápis agendy bez parametru; obojí ukazuje jedna dlaždice.
    // Odebrání musí smazat obě položky, jinak se dlaždice po přenačtení vrátí.
    [Fact]
    public void Remove_TileWithHiddenDuplicate_RemovesBothOnServer()
    {
        var fx = new Fixture(
            new ExecutableModule { Name = "Převody AD", ElementId = "146016", ExecutablePath = "MZDY2.exe", InputParam = "AD" });
        fx.Coordinator.Apply([Menu("146016") with { Input = "AD" }, Menu("146016", 1, 0)]);

        fx.Shortcuts.Remove(fx.Shortcuts.Items.Single());

        Assert.Equal([("Remove", "AD"), ("Remove", "")], fx.Desktop().Select(c => (c.Method, c.Item.Input)));
    }

    // Zápis, který server nepřijal, by v paměti zůstal: dlaždice by zmizela až po restartu.
    // Shell se proto srovná se serverem hned.
    [Fact]
    public void DesktopWriteFailed_ReloadsFromServer()
    {
        var fx = new Fixture(Exe("100", "App"), Exe("200", "Other"));
        fx.Coordinator.Apply([Menu("100")]);
        fx.Rest.QueryOrThrow("ALVA.EDControls.DesktopShortcuts", "Add", Arg.Any<CVar>())
            .Returns(_ => throw new InvalidOperationException("server down"));
        fx.Prefs.LoadAsync(Scope, ShortcutsKey).Returns(Task.FromResult<JsonNode?>(JsonSerializer.SerializeToNode(new[] { Menu("100") })));

        fx.Coordinator.Toggle(fx.Module("200"));

        Assert.False(fx.Module("200").IsFavorite);
        Assert.Single(fx.Shortcuts.Items);
        Assert.Single(fx.Failures);
    }

    // Server bez Place (shell vydaný před .cls) by jinak načítal plochu pořád dokola: Apply → Place → chyba → Apply.
    [Fact]
    public void PlaceAfterLoadRejected_DoesNotReload()
    {
        var fx = new Fixture(Exe("100", "App"));
        fx.Rest.QueryOrThrow("ALVA.EDControls.DesktopShortcuts", "Place", Arg.Any<CVar>())
            .Returns(_ => throw new InvalidOperationException("<METHOD DOES NOT EXIST>"));
        fx.Prefs.LoadAsync(Scope, ShortcutsKey).Returns(Task.FromResult<JsonNode?>(JsonSerializer.SerializeToNode(new[] { Menu("100") })));

        fx.Coordinator.Apply([Menu("100", null, null)]);

        fx.Prefs.DidNotReceive().LoadAsync(Scope, ShortcutsKey);
    }

    // Agenda skončila a blob se načítá; ★ v tu chvíli odejde souběžně a načtený blob ji ještě nemá.
    [Fact]
    public void ChangeDuringReload_LoadsAgain()
    {
        var fx = new Fixture(Exe("100", "App"), Exe("200", "Other"));
        fx.Coordinator.Apply([Menu("100")]);
        var firstLoad = new TaskCompletionSource<JsonNode?>();
        fx.Prefs.LoadAsync(Scope, ShortcutsKey).Returns(
            firstLoad.Task,
            Task.FromResult<JsonNode?>(JsonSerializer.SerializeToNode(new[] { Menu("100"), Menu("200", 1, 0) })));

        fx.Nav.LegacyModuleExited += Raise.Event<Action>();
        fx.Coordinator.Toggle(fx.Module("200"));
        firstLoad.SetResult(JsonSerializer.SerializeToNode(new[] { Menu("100") }));

        fx.Prefs.Received(2).LoadAsync(Scope, ShortcutsKey);
        Assert.True(fx.Module("200").IsFavorite);
    }

    // Agenda zapsala modul bez parametru, strom ho přiřadil variantě AD: odebrání musí na serveru trefit původní klíč.
    [Fact]
    public void Remove_FallbackVariant_SendsStoredInput()
    {
        var fx = new Fixture(
            new ExecutableModule { Name = "Převody AD", ElementId = "146016", ExecutablePath = "MZDY2.exe", InputParam = "AD" });
        fx.Coordinator.Apply([Menu("146016")]);

        fx.Shortcuts.Remove(fx.Shortcuts.Items.Single());

        var call = Assert.Single(fx.Desktop());
        Assert.Equal(("Remove", ""), (call.Method, call.Item.Input));
    }

    [Fact]
    public void ReloadAfterLegacyExit_LoadFails_KeepsDesktopAndSendsNothing()
    {
        // Exit agendy během výpadku dřív vyprázdnil plochu v paměti a první toggle prázdno zapsal.
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([Menu("100")]);
        Assert.Single(fx.Trees.FavoritesTree);

        fx.Prefs.LoadAsync(Scope, ShortcutsKey)
            .Returns(Task.FromException<JsonNode?>(new InvalidOperationException("server down")));
        fx.Nav.LegacyModuleExited += Raise.Event<Action>();

        Assert.Single(fx.Trees.FavoritesTree);
        Assert.Empty(fx.Desktop());

        fx.Coordinator.Toggle(fx.Module("100"));

        // Blob se v této session načetl, změna z shellu se posílat smí.
        Assert.Equal("Remove", Assert.Single(fx.Desktop()).Method);
    }

    [Fact]
    public void SessionLoadFailed_ToggleWorksInMemory_SaveResumesAfterSuccessfulReload()
    {
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply(null);   // load plochy v session selhal
        var module = fx.Module("100");

        fx.Coordinator.Toggle(module);

        Assert.True(module.IsFavorite);   // v paměti plocha funguje
        Assert.Empty(fx.Desktop());       // ale buňky přidělené nad prázdnem by kolidovaly s uloženou plochou

        fx.Prefs.LoadAsync(Scope, ShortcutsKey).Returns(Task.FromResult<JsonNode?>(new JsonArray()));
        fx.Nav.LegacyModuleExited += Raise.Event<Action>();
        fx.Coordinator.Toggle(module);

        Assert.Equal("Add", Assert.Single(fx.Desktop()).Method);
    }

    [Fact]
    public void Detach_StopsReloadAndPersistence()
    {
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([]);
        fx.Prefs.ClearReceivedCalls();

        fx.Coordinator.Detach();
        fx.Nav.LegacyModuleExited += Raise.Event<Action>();
        fx.Shortcuts.Add(fx.Module("100"));                       // členství
        fx.Shortcuts.PlaceAt(fx.Shortcuts.Items.Single(), 3, 2);  // pozice

        fx.Prefs.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<string>());
        fx.Prefs.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<JsonNode>());
        Assert.Empty(fx.Desktop());
    }

    // ★ u Převodů MHD nesmí označit ani přidat do Oblíbených Převody AD (týž ElementId, jiný parametr).
    [Fact]
    public void Toggle_Variant_FavoritesOnlyThatVariant()
    {
        var fx = new Fixture(Family("Mzdy",
            new ExecutableModule { Name = "Převody AD", ElementId = "146016", ExecutablePath = "MZDY2.exe", InputParam = "AD" },
            new ExecutableModule { Name = "Převody MHD", ElementId = "146016", ExecutablePath = "MZDY2.exe", InputParam = "MHD" }));
        fx.Coordinator.Apply([]);
        var mhd = fx.Trees.AllExecutables().Single(e => e.InputParam == "MHD");
        var ad = fx.Trees.AllExecutables().Single(e => e.InputParam == "AD");

        fx.Coordinator.Toggle(mhd);

        Assert.True(mhd.IsFavorite);
        Assert.False(ad.IsFavorite);
        var family = Assert.IsType<ModuleFamily>(Assert.Single(fx.Trees.FavoritesTree));
        Assert.Same(mhd, Assert.Single(family.NestedModules));
    }
}
