using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Vnitřní okraj datových buněk **pro celý prvek** tabulky linky.
/// </summary>
/// <remarks>
/// <para>Požadavek Jan 8. 9.: starý postskriptový tisk seřízne tabulku na šířku agresivněji než my
/// a chtěl na to volbu „standardní / velmi úzké". Ukázalo se, že ten parametr už existuje - je to
/// vnitřní okraj buňky - ale u tabulky linky se dal nastavit **jen po sloupcích** v dialogu Sloupce,
/// kam nikdo nedojde. Ostatní tabulkové prvky ho element-level mají, protože berou celý balíček
/// stylu ze sdíleného <c>StyleFields.cls</c>; tabulka linky si formulář skládá ručně.</para>
/// <para>⚠️ Není to kosmetika, je to rozměr: dolní práh sloupce spoje je šířka času **plus okraje
/// po stranách** (<c>MinAutoSloupecMm</c>), nejméně však 3 mm. Naměřeno na `330511` obousměrně
/// s prvkem zúženým na 120 mm: okraj 0,5 mm → základ 5,50 mm, 3 spoje na blok, 3 bloky;
/// 0,25 mm → 3,89 / 4 / 3; 0 mm → 3,11 / 5 / **2**. Tedy o jeden řez na šířku méně.</para>
/// </remarks>
public class LineTableVnitrniOkrajTests
{
    private static PropertyDescriptor Descriptor(LineTableElement prvek, string label) =>
        prvek.GetPropertyDescriptors().Single(d => d.Label == label && d.Group == "Datové buňky");

    /// <summary>Panel nabízí oba vodorovné okraje a ukazuje dědenou hodnotu z kaskády, ne prázdno.</summary>
    [Theory]
    [InlineData("Vnitřní okraj vlevo")]
    [InlineData("Vnitřní okraj vpravo")]
    public void PanelNabiziOkrajSDedenouHodnotou(string label)
    {
        var d = Assert.IsType<FloatPropertyDescriptor>(Descriptor(Prvek(), label));

        Assert.Equal(0.5f, d.Get(), precision: 2);
        Assert.Equal("mm", d.Unit);
    }

    /// <summary>Zápis jde do stylu prvku, takže z něj resolvuje celá kaskáda sloupců.</summary>
    [Fact]
    public void OkrajSeZapisujeDoStyluPrvku()
    {
        var prvek = Prvek();

        ((FloatPropertyDescriptor)Descriptor(prvek, "Vnitřní okraj vlevo")).Set(0f);
        ((FloatPropertyDescriptor)Descriptor(prvek, "Vnitřní okraj vpravo")).Set(0f);

        Assert.Equal(0f, prvek.DataStyle.PaddingLeft);
        Assert.Equal(0f, prvek.DataStyle.PaddingRight);
        Assert.Equal(0f, prvek.ResolveColumnDataParent().PaddingLeft, precision: 3);
        Assert.Equal(0f, prvek.ResolveColumnDataParent().PaddingRight, precision: 3);
    }

    /// <summary>
    /// ⚠️ Jádro požadavku: menší okraj musí zúžit sloupec spoje, takže se do bloku vejde víc spojů.
    /// Bez toho by nová volba měnila jen kresbu, ne rozvržení.
    /// </summary>
    [Fact]
    public void MensiOkrajZuziSloupecAVejdeSeVicSpoju()
    {
        var data = Data();
        var siroky = Prvek();
        var uzky = Prvek();
        ((FloatPropertyDescriptor)Descriptor(uzky, "Vnitřní okraj vlevo")).Set(0f);
        ((FloatPropertyDescriptor)Descriptor(uzky, "Vnitřní okraj vpravo")).Set(0f);

        var blokySiroke = siroky.Rozvrzeni(data);
        var blokyUzke = uzky.Rozvrzeni(data);

        Assert.True(blokyUzke[0].SloupecSpojeMm < blokySiroke[0].SloupecSpojeMm,
            $"sloupec spoje je s nulovým okrajem {blokyUzke[0].SloupecSpojeMm:0.00} mm "
            + $"a s výchozím {blokySiroke[0].SloupecSpojeMm:0.00} mm - okraj se do prahu nepropsal");
        Assert.True(blokyUzke[0].Pocet > blokySiroke[0].Pocet,
            $"do bloku se vejde {blokyUzke[0].Pocet} spojů proti {blokySiroke[0].Pocet} - "
            + "užší sloupec počet spojů na blok nezvedl");
        // ⚠️ Tvrdit „řezů ubude" nejde: počet bloků je skoková funkce počtu spojů na blok, takže
        // o jeden spoj víc řez ubere jen někdy (na `330511` při 120 mm ano, 3 → 2; na téhle fixtuře
        // se 40 spoji ne, 4 → 4). Testovatelný je jen ten směr - užší sloupec nesmí řez přidat.
        Assert.True(blokyUzke.Count <= blokySiroke.Count,
            $"bloků je {blokyUzke.Count} proti {blokySiroke.Count} - užší sloupec přidal řez");
    }

    /// <summary>
    /// Okraj nastavený u konkrétního sloupce zůstává silnější než element-level - stejně jako
    /// u velikosti písma. Kdo si sloupec doladil v dialogu, o to novou volbou nepřijde.
    /// </summary>
    [Fact]
    public void PerSloupcovyOkrajPrebijeElementovy()
    {
        var data = Data();
        var bezSloupce = Prvek();
        var seSloupcem = Prvek();
        foreach (var p in new[] { bezSloupce, seSloupcem })
            ((FloatPropertyDescriptor)Descriptor(p, "Vnitřní okraj vlevo")).Set(0f);
        seSloupcem.Columns.Single(c => c.Key == "Trip").Style =
            new ColumnVisualStyle { Data = new TextStyle { PaddingLeft = 2f } };

        var bezOkraje = bezSloupce.Rozvrzeni(data)[0].SloupecSpojeMm;
        var sOkrajem = seSloupcem.Rozvrzeni(data)[0].SloupecSpojeMm;

        Assert.True(sOkrajem > bezOkraje,
            $"základ sloupce je s per-sloupcovým okrajem 2 mm {sOkrajem:0.00} mm a bez něj "
            + $"{bezOkraje:0.00} mm - element-level nulový okraj ten sloupcový přebil");
    }

    /// <summary>
    /// Pod 3 mm sloupec spoje nespadne, i kdyby byl okraj nulový a písmo drobné - jinak by se
    /// dělení nasypalo do bloku tolik spojů, že by čas v buňce nebyl čitelný.
    /// </summary>
    [Fact]
    public void PodTriMilimetrySloupecNespadne()
    {
        var prvek = Prvek();
        ((FloatPropertyDescriptor)prvek.GetPropertyDescriptors()
            .Single(d => d.Label == "Velikost" && d.Group == "Datové buňky")).Set(3f);
        ((FloatPropertyDescriptor)Descriptor(prvek, "Vnitřní okraj vlevo")).Set(0f);
        ((FloatPropertyDescriptor)Descriptor(prvek, "Vnitřní okraj vpravo")).Set(0f);

        var bloky = prvek.Rozvrzeni(Data());

        // Pojistka fixtury: dokud se tabulka nedělí, sloupce si rozdělí zbytek prvku a dno se
        // neuplatní - test by pak prošel na širokém sloupci, aniž by cokoli ověřil.
        Assert.True(bloky.Count > 1, "fixtura se nedělí na bloky, takže dno sloupce nic nedrží");
        Assert.True(bloky[0].SloupecSpojeMm >= 3f,
            $"sloupec spoje vyšel {bloky[0].SloupecSpojeMm:0.00} mm - dno 3 mm se neuplatnilo");
    }

    private static LineTableElement Prvek() => new() { X = 0, Y = 0, Width = 120 };

    /// <summary>
    /// Tři zastávky a <b>čtyřicet</b> spojů. Počet není libovolný: dokud se všechny spoje do šířky
    /// vejdou, sloupce si rozdělí zbytek prvku a dolní práh se **vůbec neuplatní** - okraj pak
    /// nemá co ovlivnit a test měří nic. Při deseti spojích to tak spadlo (sloupec vyšel 7,80 mm
    /// s nulovým i s výchozím okrajem).
    /// </summary>
    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(1, 40).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20", "8:40"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
