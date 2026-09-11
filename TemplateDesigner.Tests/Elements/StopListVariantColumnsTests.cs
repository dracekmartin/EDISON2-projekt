using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Rozvinutí položky „Varianty tras" na sloupce (<see cref="StopListElement.ExpandTravelTimeColumns"/>).
/// Sloupec „Čas jízdy" a variantní sloupce jsou **dvě nezávislé věci** - stejně jako ve starém modulu,
/// kde vedle sebe stojí „Časy" a „Trasa min. 1-15". Od `B80` je i **zapínání** nezávislé: varianty
/// jsou vlastní položka seznamu sloupců, ne zaškrtávátko v panelu visící na sloupci „Čas".
/// </summary>
public class StopListVariantColumnsTests
{
    private static StopColumn Varianty(float? sirka = 6f) =>
        new() { Key = StopFields.TravelTimeVariants, Width = sirka };

    private static List<StopColumn> Sloupce() =>
    [
        new StopColumn { Key = StopFields.TravelTime, Header = "min.", Width = 8f },
        Varianty(),
        new StopColumn { Key = StopFields.StopName,  Header = "Zastávky", Width = 33f },
    ];

    /// <summary>Stránka s písmeny variant; `pismena` = hodnota parametru „Písmena variant tras"
    /// (null = parametr vůbec nepřišel, tedy starší klient nebo šablona bez něj).</summary>
    private static DataContext Stranka(string[] varianty, string? pismena = null)
    {
        var values = new Dictionary<string, object?>
        {
            ["VARIANTY_TRAS"] = new System.Text.Json.Nodes.JsonArray([.. varianty.Select(p => (System.Text.Json.Nodes.JsonNode?)p)]),
        };
        if (pismena is not null) values[NoteVisibilityFilter.VariantLettersParamName] = pismena;
        return new DataContext(values);
    }

    // ── B141: parametr sestavy „Písmena variant tras" ───────────────────────────

    /// <summary>`B141` - vypnutý parametr vyprázdní záhlaví, ale **počet sloupců se nezmění**.
    /// Na počtu hlaviček visí rozvinutí položky, takže jejich zahozením by zmizely i sloupce
    /// a s nimi jízdní doby variant - přesně to, co si zákazník nechat chce.</summary>
    [Fact]
    public void VypnutaPismena_ZahlaviJePrazdneAleSloupceZustanou()
    {
        var hlavicky = StopListElement.VariantHeadersForRender(Stranka(["", "A", "D"], "false"));
        var vysledek = StopListElement.ExpandTravelTimeColumns(Sloupce(), hlavicky);

        Assert.Equal(3, hlavicky.Count);
        Assert.All(hlavicky, h => Assert.Equal("", h));
        Assert.Equal(3, vysledek.Count(c => c.Key.StartsWith(StopFields.TravelTimeVariantPrefix)));
    }

    /// <summary>Zapnutý parametr se do záhlaví promítne beze změny.</summary>
    [Fact]
    public void ZapnutaPismena_ZahlaviNesePismena()
    {
        var hlavicky = StopListElement.VariantHeadersForRender(Stranka(["", "A", "D"], "true"));

        Assert.Equal(["", "A", "D"], hlavicky);
    }

    /// <summary>Chybějící parametr = písmena se tisknou. Drží to chování starších šablon
    /// a serveru, který parametr ještě neposílá.</summary>
    [Fact]
    public void ChybejiciParametr_PismenaSeTisknou()
    {
        var hlavicky = StopListElement.VariantHeadersForRender(Stranka(["", "A"]));

        Assert.Equal(["", "A"], hlavicky);
    }

    /// <summary>Jediná varianta zmizí i s vypnutými písmeny - to pravidlo drží počet hlaviček,
    /// ne jejich obsah, takže se vypnutím písmen nesmí změnit.</summary>
    [Fact]
    public void VypnutaPismena_JedinaVariantaSloupecNedostane()
    {
        var hlavicky = StopListElement.VariantHeadersForRender(Stranka([""], "false"));
        var vysledek = StopListElement.ExpandTravelTimeColumns(Sloupce(), hlavicky);

        Assert.DoesNotContain(StopFields.TravelTimeVariants, vysledek.Select(c => c.Key));
        Assert.Empty(vysledek.Where(c => c.Key.StartsWith(StopFields.TravelTimeVariantPrefix)));
    }

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

    /// <summary>Ostatní sloupce zůstávají v původním pořadí - rozvinutí se dotýká jen položky variant.</summary>
    [Fact]
    public void OstatniSloupceZustanouNaSvychMistech()
    {
        List<StopColumn> cols =
        [
            new StopColumn { Key = StopFields.Poradi },
            new StopColumn { Key = StopFields.TravelTime },
            Varianty(),
            new StopColumn { Key = StopFields.StopName },
            new StopColumn { Key = StopFields.Zone },
        ];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["", "A"]);

        Assert.Equal(
            [StopFields.Poradi, StopFields.TravelTime, StopFields.TravelTimeVariantPrefix + "0", StopFields.TravelTimeVariantPrefix + "1", StopFields.StopName, StopFields.Zone],
            vysledek.Select(c => c.Key));
    }

    /// <summary>Šablona bez položky variant je nedostane - přidávat je samovolně by uživateli
    /// změnilo sestavu, kterou si takhle nastavil.</summary>
    [Fact]
    public void BezPolozkyVariantSeNicNepridava()
    {
        List<StopColumn> cols = [new StopColumn { Key = StopFields.TravelTime }, new StopColumn { Key = StopFields.StopName }];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["", "A"]);

        Assert.Equal([StopFields.TravelTime, StopFields.StopName], vysledek.Select(c => c.Key));
    }

    /// <summary>Past zrušená v `B80`: varianty se rozvíjely ZE sloupce „Čas", takže jeho skrytí je
    /// tiše vyplo. Teď stojí samostatně - uživatel si smí nechat jen varianty.</summary>
    [Fact]
    public void VariantyNepotrebujiSloupecCasu()
    {
        List<StopColumn> cols = [Varianty(), new StopColumn { Key = StopFields.StopName }];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["", "A"]);

        Assert.Equal(
            [StopFields.TravelTimeVariantPrefix + "0", StopFields.TravelTimeVariantPrefix + "1", StopFields.StopName],
            vysledek.Select(c => c.Key));
    }

    /// <summary>Linka s jedinou variantou (nebo bez dat) žádný variantní sloupec nedostane - je to
    /// totéž co sloupec „Čas" a prázdný sloupec s písmenem by vypadal jako chyba dat.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MeneNezDveVarianty_PolozkaZmizi(int pocet)
    {
        var hlavicky = Enumerable.Range(0, pocet).Select(i => $"T{i}").ToList();

        var vysledek = StopListElement.ExpandTravelTimeColumns(Sloupce(), hlavicky);

        Assert.Equal([StopFields.TravelTime, StopFields.StopName], vysledek.Select(c => c.Key));
    }

    /// <summary>Šířka položky platí pro **každý** variantní sloupec - počet variant je daný daty,
    /// takže dělit jednu šířku mezi ně by na lince s dvanácti variantami dalo nečitelné sloupce.</summary>
    [Fact]
    public void SirkaPolozkyPlatiProKazdySloupec()
    {
        List<StopColumn> cols = [Varianty(sirka: 9f)];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["A", "B", "C"]);

        Assert.All(vysledek, c => Assert.Equal(9f, c.Width));
    }

    /// <summary>Zarovnání i styl si varianty nesou samy - dřív si je půjčovaly od sloupce „Čas",
    /// protože neměly vlastní položku. Uloženým šablonám to zachovává migrace (zkopíruje styl „Času"
    /// na novou položku), viz <c>MigrationRouteVariantsColumnTests</c>.</summary>
    [Fact]
    public void ZarovnaniAStylSeBerouZPolozkyVariant()
    {
        var styl = new ColumnVisualStyle
        {
            Data = new TextStyle { HAlign = TextHorizontalAlignment.Right, VAlign = TextVerticalAlignment.Bottom },
        };
        List<StopColumn> cols =
        [
            new StopColumn { Key = StopFields.TravelTime, Alignment = TextHorizontalAlignment.Left },
            new StopColumn { Key = StopFields.TravelTimeVariants, Alignment = TextHorizontalAlignment.Right, Style = styl },
        ];

        var vysledek = StopListElement.ExpandTravelTimeColumns(cols, ["A", "B"]);

        var casove = vysledek.Where(c => c.Key.StartsWith(StopFields.TravelTimeVariantPrefix)).ToList();
        Assert.Equal(2, casove.Count);
        Assert.All(casove, c => Assert.Equal(TextHorizontalAlignment.Right, c.Alignment));
        // Dialog sloupců píše zarovnání (vodorovné i svislé) do stylu, ne do Alignment - bez přenosu
        // stylu by se nastavení u linky s víc variantami tiše zahodilo.
        Assert.All(casove, c => Assert.Equal(TextHorizontalAlignment.Right, c.Style?.Data?.HAlign));
        Assert.All(casove, c => Assert.Equal(TextVerticalAlignment.Bottom, c.Style?.Data?.VAlign));
        // Sloupec „Čas" si své zarovnání drží - jsou to dvě nezávislé věci.
        Assert.Equal(TextHorizontalAlignment.Left, vysledek.Single(c => c.Key == StopFields.TravelTime).Alignment);
    }

    /// <summary>Zapnutí variant čte prvek z viditelnosti sloupce, ne z vlastnosti - na tom visí
    /// i rozpad zastávky na řádky podle stanovišť.</summary>
    [Fact]
    public void ZobrazeniVariantSeCteZViditelnostiSloupce()
    {
        var el = new StopListElement();
        Assert.True(el.ShowsRouteVariants);

        el.Columns.Single(c => c.Key == StopFields.TravelTimeVariants).Visible = false;
        Assert.False(el.ShowsRouteVariants);
    }
}
