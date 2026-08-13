using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Rozvinutí časových sloupců na varianty tras (<see cref="StopListElement.ExpandTravelTimeColumns"/>).
/// Sloupec „Čas jízdy" a variantní sloupce jsou **dvě nezávislé věci** - stejně jako ve starém modulu,
/// kde vedle sebe stojí „Časy" a „Trasa min. 1-15". Dřív ho varianty nahradily, takže deklarovaná doba
/// linky ze sestavy zmizela, jakmile měla zastávka dvě varianty.
/// </summary>
public class StopListVariantColumnsTests
{
    private static List<StopColumn> Sloupce() =>
    [
        new StopColumn { Key = StopFields.TravelTime, Header = "min.", Width = 8f },
        new StopColumn { Key = StopFields.StopName,  Header = "Zastávky", Width = 33f },
    ];

    [Fact]
    public void SloupecCasuZustavaAVariantyJdouZaNim()
    {
        var vysledek = StopListElement.ExpandTravelTimeColumns(Sloupce(), ["", "A"]);

        Assert.Equal(
            [StopFields.TravelTime, StopFields.TravelTimeVariantPrefix + "0", StopFields.TravelTimeVariantPrefix + "1", StopFields.StopName],
            vysledek.Select(c => c.Key));
    }

    /// <summary>Základní sloupec si drží svou hlavičku i šířku - varianty si je určují samy, takže
    /// se do něj nesmí promítnout.</summary>
    [Fact]
    public void ZakladniSloupecSiDrziHlavickuISirku()
    {
        var vysledek = StopListElement.ExpandTravelTimeColumns(Sloupce(), ["", "A"]);

        Assert.Equal("min.", vysledek[0].Header);
        Assert.Equal(8f, vysledek[0].Width);
        Assert.Equal(["", "A"], vysledek.Skip(1).Take(2).Select(c => c.Header));
    }

    /// <summary>Ostatní sloupce zůstávají v původním pořadí - rozvinutí se dotýká jen časů.</summary>
    [Fact]
    public void OstatniSloupceZustanouNaSvychMistech()
    {
        List<StopColumn> cols =
        [
            new StopColumn { Key = StopFields.Poradi },
            new StopColumn { Key = StopFields.TravelTime },
            new StopColumn { Key = StopFields.StopName },
            new StopColumn { Key = StopFields.Zone },
        ];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["", "A"]);

        Assert.Equal(
            [StopFields.Poradi, StopFields.TravelTime, StopFields.TravelTimeVariantPrefix + "0", StopFields.TravelTimeVariantPrefix + "1", StopFields.StopName, StopFields.Zone],
            vysledek.Select(c => c.Key));
    }

    /// <summary>Šablona bez sloupce časů varianty nedostane - není kam je zařadit a přidávat je
    /// samovolně by uživateli změnilo sestavu, kterou si takhle nastavil.</summary>
    [Fact]
    public void BezSloupceCasuSeNicNepridava()
    {
        List<StopColumn> cols = [new StopColumn { Key = StopFields.StopName }];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["", "A"]);

        Assert.Equal([StopFields.StopName], vysledek.Select(c => c.Key));
    }
}
