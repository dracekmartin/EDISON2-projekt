using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy sdílené služby oblíbených (zdroj pravdy pro strom ★ i miniaplikaci Oblíbené).
/// Čistě in-memory — bez persistence a launch implementace (ty drží EdisonMainViewModel).
/// </summary>
public class ModuleShortcutsServiceTests
{
    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe" };

    [Fact]
    public void Seed_PopulatesInOrder_AndSetsIsFavorite()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b, c], ["300", "100"]);

        Assert.Equal(["300", "100"], svc.Favorites.Select(f => f.ElementId));
        Assert.True(c.IsFavorite);
        Assert.True(a.IsFavorite);
        Assert.False(b.IsFavorite);
    }

    [Fact]
    public void Seed_SkipsUnknownIds()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], ["999", "100"]);

        Assert.Equal(["100"], svc.Favorites.Select(f => f.ElementId));
    }

    [Fact]
    public void Seed_ClearsPreviousFavorites_AndResetsOldFlags()
    {
        var old = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([old], ["100"]);
        Assert.True(old.IsFavorite);

        var fresh = Exe("200");
        svc.Seed([fresh], ["200"]);

        Assert.False(old.IsFavorite);
        Assert.Equal(["200"], svc.Favorites.Select(f => f.ElementId));
    }

    [Fact]
    public void Add_IsIdempotent_AndSetsFlag()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], []);

        svc.Add(a);
        svc.Add(a);

        Assert.Same(a, Assert.Single(svc.Favorites));
        Assert.True(a.IsFavorite);
    }

    [Fact]
    public void Add_IgnoresEmptyElementId()
    {
        var svc = new ModuleShortcutsService();

        svc.Add(Exe("", "NoId"));

        Assert.Empty(svc.Favorites);
    }

    [Fact]
    public void Remove_ClearsFlag_ByElementId()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], ["100"]);

        svc.Remove(a);

        Assert.Empty(svc.Favorites);
        Assert.False(a.IsFavorite);
    }

    [Fact]
    public void Seed_AssignsOrderSlots_ByDefault()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], ["200", "100"]);

        Assert.Equal(0, b.FavoriteSlot);   // první v pořadí
        Assert.Equal(1, a.FavoriteSlot);
    }

    [Fact]
    public void Seed_UsesPersistedSlots_WhenProvided()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], ["100", "200"], new Dictionary<string, int> { ["100"] = 5, ["200"] = 2 });

        Assert.Equal(5, a.FavoriteSlot);
        Assert.Equal(2, b.FavoriteSlot);
    }

    [Fact]
    public void Seed_MissingSlotForId_FallsBackToInsertionOrder()
    {
        // Persistovaný slot pro "100" je 3; "200" v mapě chybí → spadne na pořadí (order=1).
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();

        svc.Seed([a, b], ["100", "200"], new Dictionary<string, int> { ["100"] = 3 });

        Assert.Equal(3, a.FavoriteSlot);
        Assert.Equal(1, b.FavoriteSlot);
    }

    [Fact]
    public void Seed_DuplicateIds_AddedOnce()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();

        svc.Seed([a], ["100", "100"]);

        Assert.Same(a, Assert.Single(svc.Favorites));
    }

    [Fact]
    public void PlaceAt_Guards_NoOp_DoNotRaiseSlotsChanged()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], ["100"]);   // a=slot 0, b není oblíbený
        var raised = false;
        svc.SlotsChanged += () => raised = true;

        svc.PlaceAt(a, -1);   // záporný slot
        svc.PlaceAt(b, 2);    // modul mimo oblíbené
        svc.PlaceAt(a, 0);    // stejný slot

        Assert.Equal(0, a.FavoriteSlot);
        Assert.False(raised);
    }

    [Fact]
    public void PlaceAt_Swap_RaisesSlotsChangedOnce()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], ["100", "200"]);   // a=0, b=1
        int raised = 0;
        svc.SlotsChanged += () => raised++;

        svc.PlaceAt(a, 1);   // obsazeno b → swap

        Assert.Equal(1, a.FavoriteSlot);
        Assert.Equal(0, b.FavoriteSlot);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Add_AssignsLowestFreeSlot()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b, c], []);

        svc.Add(a);   // slot 0
        svc.Add(b);   // slot 1
        svc.Remove(a);
        svc.Add(c);   // znovu nejnižší volný = 0

        Assert.Equal(0, c.FavoriteSlot);
        Assert.Equal(1, b.FavoriteSlot);
    }

    [Fact]
    public void PlaceAt_MovesToEmptySlot_LeavingGap()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], ["100"]);   // slot 0

        svc.PlaceAt(a, 4);

        Assert.Equal(4, a.FavoriteSlot);   // mezera 0–3
    }

    [Fact]
    public void PlaceAt_OccupiedSlot_Swaps()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], ["100", "200"]);   // a=0, b=1

        svc.PlaceAt(a, 1);   // obsazeno b → swap

        Assert.Equal(1, a.FavoriteSlot);
        Assert.Equal(0, b.FavoriteSlot);
    }

    [Fact]
    public void PlaceAt_RaisesSlotsChanged()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], ["100"]);
        var raised = false;
        svc.SlotsChanged += () => raised = true;

        svc.PlaceAt(a, 3);

        Assert.True(raised);
    }

    [Fact]
    public void Resolve_FindsAcrossAllExecutables_NotJustFavorites()
    {
        var a = Exe("100"); var b = Exe("200");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b], ["100"]);

        Assert.Same(b, svc.Resolve("200"));
        Assert.Null(svc.Resolve("999"));
    }
}
