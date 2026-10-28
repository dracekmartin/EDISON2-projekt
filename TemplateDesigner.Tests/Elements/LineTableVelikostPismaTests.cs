using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Velikost písma záhlaví a buněk **pro celý prvek** tabulky linky - v panelu i ve formuláři prvku.
/// </summary>
/// <remarks>
/// <para>Nález Jan 7. 9. při stavbě šablony na školení: prvek měl element-level jen barvy, takže
/// velikost šla nastavit **jen po sloupcích** v dialogu (šest sloupců × dvě zóny), nebo přeškálováním
/// celé šablony. Přitom u Minut odjezdů („Velikost záhlaví") i u Poznámek („Velikost" v Datových buňkách)
/// element-level velikost je - u tabulky linky chyběla jako jediná.</para>
/// <para>⚠️ Není to kosmetika: velikost buněk nese **výšku řádku** (auto se počítá z fontu)
/// i **šířku sloupce spoje**. Na 31 zastávkách je 9 pt proti 4,8 pt rozdíl 158 proti 65 mm, tedy
/// dvě stránky proti jedné.</para>
/// </remarks>
public class LineTableVelikostPismaTests
{
    private static LineTableElement Prvek() => new() { X = 0, Y = 0, Width = 200 };

    private static PropertyDescriptor Descriptor(LineTableElement prvek, string skupina) =>
        prvek.GetPropertyDescriptors().Single(d => d.Label == "Velikost" && d.Group == skupina);

    /// <summary>
    /// Panel nabízí velikost v obou zónách a ukazuje hodnotu z kaskády, ne prázdno.
    /// ⚠️ Zóny mají **různé** zapečené výchozí velikosti (záhlaví 9 pt, buňky 8 pt) - jedno číslo
    /// pro obě by ve formuláři ukázalo jinou hodnotu, než se vykreslí (chytil driftový test).
    /// </summary>
    [Theory]
    [InlineData("Záhlaví", 9f)]
    [InlineData("Datové buňky", 8f)]
    public void PanelNabiziVelikostVObouZonach(string skupina, float vychozi)
    {
        var d = Assert.IsType<FloatPropertyDescriptor>(Descriptor(Prvek(), skupina));

        Assert.Equal(vychozi, d.Get(), precision: 2);
        Assert.Equal("pt", d.Unit);
    }

    /// <summary>Zápis jde do stylu prvku, takže z něj resolvuje celá kaskáda sloupců.</summary>
    [Fact]
    public void VelikostBunekSeZapisujeDoStyluPrvku()
    {
        var prvek = Prvek();

        ((FloatPropertyDescriptor)Descriptor(prvek, "Datové buňky")).Set(4.8f);

        Assert.Equal(4.8f, prvek.DataStyle.FontSize);
        Assert.Equal(4.8f, prvek.ResolveColumnDataParent().FontSize, precision: 2);
    }

    /// <summary>Dtto záhlaví - jeho zóna má vlastní parent, ať se obě nesejdou v jedné hodnotě.</summary>
    [Fact]
    public void VelikostZahlaviSeZapisujeDoStyluPrvku()
    {
        var prvek = Prvek();

        ((FloatPropertyDescriptor)Descriptor(prvek, "Záhlaví")).Set(5f);

        Assert.Equal(5f, prvek.HeaderStyle.FontSize);
        Assert.Equal(5f, prvek.ResolveColumnHeaderParent().FontSize, precision: 2);
        Assert.Equal(8f, prvek.ResolveColumnDataParent().FontSize, precision: 2);
    }

    /// <summary>
    /// ⚠️ Jádro nálezu: menší písmo buněk musí zkrátit i **výšku tabulky**, protože auto výška
    /// řádku se počítá z fontu. Bez toho by nová volba měnila jen kresbu a rozvržení ne.
    /// </summary>
    [Fact]
    public void MensiPismoBunekZkratiTabulku()
    {
        var data = Data();
        var velke = Prvek();
        var male = Prvek();
        ((FloatPropertyDescriptor)Descriptor(male, "Datové buňky")).Set(4.8f);

        float vyskaVelke = velke.CalculateHeightFromData(data);
        float vyskaMale = male.CalculateHeightFromData(data);

        Assert.True(vyskaMale < vyskaVelke * 0.75f,
            $"tabulka na 4,8 pt měří {vyskaMale:0.#} mm a na 9 pt {vyskaVelke:0.#} mm - "
            + "menší písmo se do výšky řádku nepropsalo");
    }

    /// <summary>Zadaná výška řádku má přednost - kdo si ji nastavil, o ni změnou písma nepřijde.</summary>
    [Fact]
    public void ZadanaVyskaRadkuPismemNehne()
    {
        var data = Data();
        var prvek = Prvek();
        prvek.DataRowHeight = 2.1f;
        float pred = prvek.CalculateHeightFromData(data);

        ((FloatPropertyDescriptor)Descriptor(prvek, "Datové buňky")).Set(4.8f);

        Assert.Equal(pred, prvek.CalculateHeightFromData(data), precision: 2);
    }

    /// <summary>Linka o třech zastávkách a jednom spoji - stačí na měření výšky řádků.</summary>
    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", new JsonArray("8:00", "8:20", "8:40"))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
