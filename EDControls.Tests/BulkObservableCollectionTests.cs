using System.Collections.Specialized;
using EDControls.EDList;

namespace EDControls.Tests;

/// <summary>
/// Hromadná výměna obsahu seznamu. Plnění po jednom znamenalo N+1 přepočtů navázaného
/// pohledu a bylo vidět jako škubnutí, když načtení dosedlo, zatímco uživatel psal do filtru.
/// </summary>
public class BulkObservableCollectionTests
{
    private static (BulkObservableCollection<string> collection, List<NotifyCollectionChangedAction> events) Watched()
    {
        var collection = new BulkObservableCollection<string>();
        var events = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => events.Add(e.Action);
        return (collection, events);
    }

    [Fact]
    public void ReplaceAll_RaisesSingleResetEvent()
    {
        var (collection, events) = Watched();

        collection.ReplaceAll(["a", "b", "c", "d"]);

        Assert.Equal([NotifyCollectionChangedAction.Reset], events);
        Assert.Equal(["a", "b", "c", "d"], collection);
    }

    /// <summary>Dosavadní cesta (Clear + Add) je to, co se nahrazuje - pro srovnání, ať je vidět,
    /// o kolik oznámení jde.</summary>
    [Fact]
    public void AddingOneByOne_RaisesEventPerRow()
    {
        var (collection, events) = Watched();

        collection.Clear();
        foreach (var item in new[] { "a", "b", "c", "d" }) collection.Add(item);

        Assert.Equal(5, events.Count);   // 1 Reset z Clear + 4 Add
    }

    [Fact]
    public void ReplaceAll_DiscardsPreviousContent()
    {
        var collection = new BulkObservableCollection<string> { "stare1", "stare2" };

        collection.ReplaceAll(["nove"]);

        Assert.Equal(["nove"], collection);
    }

    [Fact]
    public void ReplaceAll_EmptyList_Clears()
    {
        var (collection, events) = Watched();
        collection.ReplaceAll(["a", "b"]);
        events.Clear();

        collection.ReplaceAll([]);

        Assert.Empty(collection);
        Assert.Equal([NotifyCollectionChangedAction.Reset], events);
    }

    /// <summary>Počet se musí ohlásit taky - bez toho by ukazatel „N řádků" zůstal na staré hodnotě.</summary>
    [Fact]
    public void ReplaceAll_RaisesCountChange()
    {
        var collection = new BulkObservableCollection<string>();
        var changedProperties = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)collection).PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        collection.ReplaceAll(["a", "b"]);

        Assert.Contains("Count", changedProperties);
        Assert.Contains("Item[]", changedProperties);
    }
}
