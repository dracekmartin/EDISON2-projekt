using System.Windows;
using EDControls;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Sdílená geometrie shellu a přihlašovacího okna v local.json: shell ji uloží při zavření,
/// přihlašovací okno ji při dalším startu přečte ještě před loginem.
/// </summary>
public class ShellWindowStateStoreTests
{
    [Fact]
    public void Load_NothingStored_ReturnsNone()
    {
        var store = new ShellWindowStateStore(Substitute.For<ILocalPrefs>());

        Assert.True(store.Load().IsEmpty);
    }

    [Fact]
    public void SaveLoad_RoundTripsGeometry()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        var store = new ShellWindowStateStore(prefs);

        // Monitor vlevo od primárního - záporná pozice musí přežít.
        store.Save(new Rect(-1800, -120, 1600, 900), maximized: true);

        Assert.Equal(new WindowGeometry(-1800, -120, 1600, 900, true), store.Load());
    }

    [Fact]
    public void Save_PersistsImmediately()
    {
        // Bez zápisu na disk by se geometrie ztratila při ukončení procesu updatem.
        var prefs = Substitute.For<ILocalPrefs>();

        new ShellWindowStateStore(prefs).Save(new Rect(0, 0, 800, 600), maximized: false);

        prefs.Received(1).Save();
    }

    [Fact]
    public void Save_OverwritesPreviousEntry()
    {
        // Jeden záznam pro obě okna - druhý zápis nesmí nechat kombinaci starých a nových hodnot.
        var prefs = Substitute.For<ILocalPrefs>();
        var store = new ShellWindowStateStore(prefs);

        store.Save(new Rect(100, 100, 800, 600), maximized: true);
        store.Save(new Rect(0, 0, 1024, 768), maximized: false);

        Assert.Equal(new WindowGeometry(0, 0, 1024, 768, false), store.Load());
    }
}
