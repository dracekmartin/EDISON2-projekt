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
    public void Move_ReordersFavorites()
    {
        var a = Exe("100"); var b = Exe("200"); var c = Exe("300");
        var svc = new ModuleShortcutsService();
        svc.Seed([a, b, c], ["100", "200", "300"]);

        svc.Move(0, 2);

        Assert.Equal(["200", "300", "100"], svc.Favorites.Select(f => f.ElementId));
    }

    [Fact]
    public void Move_OutOfRange_NoOp()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        svc.Seed([a], ["100"]);

        svc.Move(0, 5);

        Assert.Equal(["100"], svc.Favorites.Select(f => f.ElementId));
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

    [Fact]
    public void RequestLaunch_RaisesLaunchRequested()
    {
        var a = Exe("100");
        var svc = new ModuleShortcutsService();
        ExecutableModule? launched = null;
        svc.LaunchRequested += m => launched = m;

        svc.RequestLaunch(a);

        Assert.Same(a, launched);
    }
}
