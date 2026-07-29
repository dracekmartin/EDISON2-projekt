using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Resolve pozičních klíčů <c>__INDEX_N</c> per-sloupcových stylů skupin dnů na skutečná ID
/// (<see cref="GroupColumnIndexResolver"/>) - poziční klíče vznikají při AI importu, kdy runtime
/// ID skupin ještě nejsou známá.
/// </summary>
public class GroupColumnIndexResolverTests
{
    private static DepartureGroupColumnStyle Gc(string id, float? headerFont = null) => new()
    {
        SkupinaDnuId = id,
        Style = new ColumnVisualStyle { Header = headerFont is null ? null : new TextStyle { FontSize = headerFont } },
    };

    [Fact]
    public void IndexoveKlice_SeMapujiPozicneNaSkutecnaId()
    {
        var lookup = GroupColumnIndexResolver.BuildStyleLookup(
            [Gc("__INDEX_0", 10f), Gc("__INDEX_1", 12f)],
            ["10", "20"]);

        Assert.Equal(10f, lookup["10"].Style.Header!.FontSize);
        Assert.Equal(12f, lookup["20"].Style.Header!.FontSize);
    }

    [Fact]
    public void SkutecneId_MaPrednostPredIndexovym()
    {
        // Skupina "20" má vlastní styl - poziční záznam ji nesmí přepsat.
        var lookup = GroupColumnIndexResolver.BuildStyleLookup(
            [Gc("20", 8f), Gc("__INDEX_0", 14f)],
            ["20"]);

        Assert.Equal(8f, lookup["20"].Style.Header!.FontSize);
    }

    [Fact]
    public void HeaderFontLookup_VytahneJenVelikost()
    {
        var lookup = GroupColumnIndexResolver.BuildHeaderFontLookup(
            [Gc("__INDEX_0", 11f), Gc("__INDEX_1")],
            ["1", "2"]);

        Assert.Equal(11f, lookup["1"]);
        Assert.Null(lookup["2"]);
    }

    [Fact]
    public void ViceIndexuNezSkupin_PrebytecneSeIgnoruji()
    {
        var lookup = GroupColumnIndexResolver.BuildStyleLookup(
            [Gc("__INDEX_0", 9f), Gc("__INDEX_1", 10f), Gc("__INDEX_2", 11f)],
            ["jedina"]);

        Assert.Equal(9f, lookup["jedina"].Style.Header!.FontSize);
        Assert.Equal(4, lookup.Count);   // 3 poziční klíče + 1 vyřešené ID (poziční zůstávají v mapě)
    }
}
