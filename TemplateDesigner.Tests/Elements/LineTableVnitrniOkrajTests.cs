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
/// <para>⚠️ **Rozsah pole se odpoledne 8. 9. zúžil.** Ráno okraj sloupec zužoval (naměřeno na
/// `330511` s prvkem 120 mm: 0,5 mm → základ 5,50 mm a 3 bloky, 0 mm → 3,11 mm a 2 bloky). Pak
/// přišel požadavek na vůli kolem času, která platí vždy - a protože je 1 mm, tedy právě výchozí
/// okraj, **utahování okrajem tím zaniklo**. Okraj dnes sloupec už jen rozšiřuje; zužuje se
/// velikostí písma a polem „Nejmenší šířka sloupce spoje".</para>
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
    /// ⚠️ **Nulový okraj sloupec nezúží** - drží ho vůle kolem času (1 mm celkem), která platí vždy.
    /// </summary>
    /// <remarks>
    /// Ráno 8. 9. tohle pole sloupec zužovat umělo a bylo to jeho hlavní využití. Odpoledne přišel
    /// požadavek, že kolem času musí zůstat vůle, ať uživatel nastaví cokoli - při nulovém okraji
    /// procházely svislé čáry vedle číslic. Vůle je 1 mm, tedy právě to, co dává výchozí okraj
    /// 0,5 mm na stranu, takže **vynulování okraje už nic nedělá**.
    /// </remarks>
    [Fact]
    public void NulovyOkrajSloupecNezuziPodVuli()
    {
        var data = Data();
        var vychozi = Prvek();
        var nulovy = Prvek();
        ((FloatPropertyDescriptor)Descriptor(nulovy, "Vnitřní okraj vlevo")).Set(0f);
        ((FloatPropertyDescriptor)Descriptor(nulovy, "Vnitřní okraj vpravo")).Set(0f);

        Assert.Equal(vychozi.Rozvrzeni(data)[0].SloupecSpojeMm,
            nulovy.Rozvrzeni(data)[0].SloupecSpojeMm, precision: 3);
    }

    /// <summary>Okraj větší než vůle sloupec naopak rozšíří - potud pole funguje dál.</summary>
    [Fact]
    public void VetsiOkrajSloupecRozsiri()
    {
        var data = Data();
        var vychozi = Prvek();
        var siroky = Prvek();
        ((FloatPropertyDescriptor)Descriptor(siroky, "Vnitřní okraj vlevo")).Set(2f);
        ((FloatPropertyDescriptor)Descriptor(siroky, "Vnitřní okraj vpravo")).Set(2f);

        Assert.True(siroky.Rozvrzeni(data)[0].SloupecSpojeMm > vychozi.Rozvrzeni(data)[0].SloupecSpojeMm,
            "okraj 2 mm na stranu sloupec nerozšířil");
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
