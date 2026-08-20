using System.Collections.Specialized;
using EDControls.EDList;

namespace EDControls.Tests;

/// <summary>
/// Hromadná výměna obsahu seznamu. Plnění po jednom znamenalo N+1 přepočtů navázaného
/// pohledu a bylo vidět jako škubnutí, když načtení dosedlo, zatímco uživatel psal do filtru.
/// </summary>
public class BulkObservableCollectionTests
{
    private static (BulkObservableCollection<string> kolekce, List<NotifyCollectionChangedAction> udalosti) Sledovana()
    {
        var kolekce = new BulkObservableCollection<string>();
        var udalosti = new List<NotifyCollectionChangedAction>();
        kolekce.CollectionChanged += (_, e) => udalosti.Add(e.Action);
        return (kolekce, udalosti);
    }

    [Fact]
    public void ReplaceAll_PosleJedinouUdalost()
    {
        var (kolekce, udalosti) = Sledovana();

        kolekce.ReplaceAll(["a", "b", "c", "d"]);

        Assert.Equal([NotifyCollectionChangedAction.Reset], udalosti);
        Assert.Equal(["a", "b", "c", "d"], kolekce);
    }

    /// <summary>Dosavadní cesta (Clear + Add) je to, co se nahrazuje - pro srovnání, ať je vidět,
    /// o kolik oznámení jde.</summary>
    [Fact]
    public void PlneniPoJednom_PosleOznameniZaKazdyRadek()
    {
        var (kolekce, udalosti) = Sledovana();

        kolekce.Clear();
        foreach (var polozka in new[] { "a", "b", "c", "d" }) kolekce.Add(polozka);

        Assert.Equal(5, udalosti.Count);   // 1 Reset z Clear + 4 Add
    }

    [Fact]
    public void ReplaceAll_ZahodiPuvodniObsah()
    {
        var kolekce = new BulkObservableCollection<string> { "stare1", "stare2" };

        kolekce.ReplaceAll(["nove"]);

        Assert.Equal(["nove"], kolekce);
    }

    [Fact]
    public void ReplaceAll_PrazdnymSeznamemVyprazdni()
    {
        var (kolekce, udalosti) = Sledovana();
        kolekce.ReplaceAll(["a", "b"]);
        udalosti.Clear();

        kolekce.ReplaceAll([]);

        Assert.Empty(kolekce);
        Assert.Equal([NotifyCollectionChangedAction.Reset], udalosti);
    }

    /// <summary>Počet se musí ohlásit taky - bez toho by ukazatel „N řádků" zůstal na staré hodnotě.</summary>
    [Fact]
    public void ReplaceAll_OhlasiZmenuPoctu()
    {
        var kolekce = new BulkObservableCollection<string>();
        var zmeneneVlastnosti = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)kolekce).PropertyChanged += (_, e) => zmeneneVlastnosti.Add(e.PropertyName);

        kolekce.ReplaceAll(["a", "b"]);

        Assert.Contains("Count", zmeneneVlastnosti);
        Assert.Contains("Item[]", zmeneneVlastnosti);
    }
}
