using System.Windows;
using EDControls;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Geometrie shellu a přihlašovacího okna v local.json per server: shell ji uloží při zavření,
/// přihlašovací okno ji při dalším startu přečte ještě před loginem.
/// </summary>
public class ShellWindowStateStoreTests
{
    private static ShellWindowStateStore DevStore(ILocalPrefs prefs) => new(prefs, "dev-host", 57772);

    [Fact]
    public void Load_NothingStored_ReturnsNone()
    {
        var store = DevStore(Substitute.For<ILocalPrefs>());

        Assert.True(store.Load().IsEmpty);
    }

    [Fact]
    public void SaveLoad_RoundTripsGeometry()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        var store = DevStore(prefs);

        // Monitor vlevo od primárního - záporná pozice musí přežít.
        store.Save(new Rect(-1800, -120, 1600, 900), maximized: true);

        Assert.Equal(new WindowGeometry(-1800, -120, 1600, 900, true), store.Load());
    }

    [Fact]
    public void Save_PersistsImmediately()
    {
        // Bez zápisu na disk by se geometrie ztratila při ukončení procesu updatem.
        var prefs = Substitute.For<ILocalPrefs>();

        DevStore(prefs).Save(new Rect(0, 0, 800, 600), maximized: false);

        prefs.Received(1).Save();
    }

    [Fact]
    public void Save_OverwritesPreviousEntry()
    {
        // Jeden záznam pro obě okna - druhý zápis nesmí nechat kombinaci starých a nových hodnot.
        var prefs = Substitute.For<ILocalPrefs>();
        var store = DevStore(prefs);

        store.Save(new Rect(100, 100, 800, 600), maximized: true);
        store.Save(new Rect(0, 0, 1024, 768), maximized: false);

        Assert.Equal(new WindowGeometry(0, 0, 1024, 768, false), store.Load());
    }

    [Fact]
    public void Load_OtherServerStored_IsNotUsed()
    {
        // Dvě instalace na stanici: okno jedné nesmí naskočit tam, kam uživatel posunul okno druhé.
        var prefs = new TransientLocalPrefs();
        new ShellWindowStateStore(prefs, "localhost", 57772).Save(new Rect(1920, 0, 1024, 768), maximized: false);

        Assert.True(DevStore(prefs).Load().IsEmpty);
    }

#pragma warning disable CS0618 // geometrie starší verze
    [Fact]
    public void Load_ServerWithoutOwnEntry_UsesLegacyGeometry()
    {
        // První start po aktualizaci: okno naskočí tam, kde bylo ve starší verzi.
        var prefs = new TransientLocalPrefs { ShellWindowBounds = new WindowBoundsPrefs { Left = 10, Top = 20, Width = 800, Height = 600 } };

        Assert.Equal(new WindowGeometry(10, 20, 800, 600, false), DevStore(prefs).Load());
    }

    [Fact]
    public void Save_LeavesLegacyGeometry()
    {
        var legacy = new WindowBoundsPrefs { Left = 10, Width = 800 };
        var prefs = new TransientLocalPrefs { ShellWindowBounds = legacy };

        DevStore(prefs).Save(new Rect(50, 0, 900, 700), maximized: false);

        Assert.Same(legacy, prefs.ShellWindowBounds);
        Assert.Equal(new WindowGeometry(50, 0, 900, 700, false), DevStore(prefs).Load());
    }
#pragma warning restore CS0618
}
