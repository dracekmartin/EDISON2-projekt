using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Dvě věci, které Jan našel 8. 9. na výtisku tabulky linkového JŘ.
/// </summary>
/// <remarks>
/// <para><b>Značky nad časy se nepočítaly do šířky.</b> Dolní hranice sloupce spoje brala v úvahu
/// jen čas a značku návaznosti vedle něj. Řádek platnosti a řádek negativních značek se přitom
/// sázejí písmem zvětšeným o <c>PomerZnacek</c> (výchozí 1,5×) a na řádek platnosti jdou
/// <b>dvě značky vedle sebe</b> - takže sloupci přetékaly do sousedního.</para>
/// <para><b>Čas v přípojném řádku šel jako obyčejný text</b>, kdežto čas spoje přes společný tvar
/// (hodina větším písmem, minuta menším, bez vedoucí nuly). Vedle sebe to vypadalo jako dvě různé
/// tabulky.</para>
/// </remarks>
public class LineTableZnackyASirkaTests
{
    private static float SirkaSloupce(LineTableElement prvek, DataContext data) =>
        prvek.Rozvrzeni(data)[0].SirkySloupcu[0];

    /// <summary>
    /// ⚠️ Jádro první opravy: spoj se dvěma značkami platnosti potřebuje širší sloupec než spoj
    /// bez nich, protože se sázejí vedle sebe a větším písmem.
    /// </summary>
    [Fact]
    public void ZnackyPlatnostiRozsiriSloupec()
    {
        var bez = SirkaSloupce(Prvek(), Data(platnost: null));
        var se = SirkaSloupce(Prvek(), Data(platnost: new JsonArray("6", "+")));

        Assert.True(se > bez,
            $"sloupec je se značkami platnosti {se:0.00} mm a bez nich {bez:0.00} mm - "
            + "značky se do šířky nepropsaly a na výtisku přetečou");
    }

    /// <summary>
    /// Negativní značka se do šířky počítá taky - box z obrysu znaku plus vnitřní mezera.
    /// </summary>
    /// <remarks>
    /// ⚠️ Měří se při <b>trojnásobném</b> písmu značek, ne při výchozím: dvouznaková značka
    /// („23", „60") je i při 1,5× **užší než celý čas**, takže sloupec při výchozím nastavení
    /// rozšířit nemá proč a test by měřil nefunkční případ. Teprve nad tou hranicí je vidět,
    /// že se s ní vůbec počítá.
    /// </remarks>
    [Fact]
    public void NegativniZnackaRozsiriSloupec()
    {
        var bez = Prvek();
        bez.PomerZnacek = 3f;
        var se = Prvek();
        se.PomerZnacek = 3f;

        float sirkaBez = SirkaSloupce(bez, Data(nz: null));
        float sirkaSe = SirkaSloupce(se, Data(nz: "23"));

        Assert.True(sirkaSe > sirkaBez,
            $"sloupec je s negativní značkou {sirkaSe:0.00} mm a bez ní {sirkaBez:0.00} mm");
    }

    /// <summary>
    /// Větší „Velikost značek" musí sloupec rozšířit - dosud měnila jen výšku řádku, jak sliboval
    /// popisek pole, a šířka na ni nereagovala vůbec.
    /// </summary>
    [Fact]
    public void VetsiPomerZnacekRozsiriSloupec()
    {
        var data = Data(platnost: new JsonArray("6", "+"));
        var maly = Prvek();
        maly.PomerZnacek = 1f;
        var velky = Prvek();
        velky.PomerZnacek = 3f;

        Assert.True(SirkaSloupce(velky, data) > SirkaSloupce(maly, data),
            "trojnásobné písmo značek sloupec nerozšířilo");
    }

    /// <summary>Vypnutý řádek platnosti = značky se netisknou, takže do šířky nemají co mluvit.</summary>
    [Fact]
    public void VypnutyRadekPlatnostiSirkuNeovlivni()
    {
        var data = Data(platnost: new JsonArray("6", "+"));
        var prvek = Prvek();
        prvek.ShowPlatnostRow = false;
        prvek.ShowNzRow = false;

        Assert.Equal(SirkaSloupce(prvek, Data(platnost: null)), SirkaSloupce(prvek, data), precision: 3);
    }

    /// <summary>
    /// ⚠️ Druhá oprava: čas přípoje se rozpadá na hodinu a menší minutu stejně jako čas spoje.
    /// Testuje se přes <c>CasRuns</c>, tedy tentýž převod, který kreslení používá - kdyby ho
    /// přípojný řádek obešel, vrátí se sem obyčejný text.
    /// </summary>
    [Fact]
    public void CasPripojeSeRozpadaStejneJakoCasSpoje()
    {
        var runs = Prvek().CasRuns("07:10");

        Assert.NotNull(runs);
        Assert.Equal("7", runs![0].Text);          // bez vedoucí nuly, jako u spoje
        Assert.Equal("10", runs[2].Text);
        Assert.True(runs[2].Zmenseni < 1f, "minuta přípoje není menším písmem");
    }

    /// <summary>
    /// Prvek jen o málo širší, než zabere sloupec se zastávkami (42 mm). Na zbytek prvku tím
    /// nezůstane nic a o šířce sloupce spoje rozhoduje **dolní hranice** - jinak by testy měřily
    /// rozdělený zbytek a značky by v nich nebylo vidět (při 60 mm vyšlo 18 mm ve všech případech).
    /// </summary>
    private static LineTableElement Prvek() => new() { X = 0, Y = 0, Width = 44 };

    /// <summary>Jeden spoj, tři zastávky.</summary>
    private static DataContext Data(JsonArray? platnost = null, string? nz = null)
    {
        var spoj = new List<(string, object)>
        {
            ("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20", "8:40")),
        };
        if (platnost is not null) spoj.Add(("PLATNOST", platnost));
        if (nz is not null) spoj.Add(("NZ", nz));

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
            ]),
            ("SPOJE", [RenderSnapshot.Row([.. spoj])]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }
}
