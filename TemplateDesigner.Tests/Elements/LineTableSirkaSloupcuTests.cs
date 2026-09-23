using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Šířka sloupce spoje u tabulky linkového JŘ.
/// </summary>
/// <remarks>
/// <para><b>Šířka se nezadává, počítá se z obsahu</b> (rozhodnutí Jan 22. 9.): sloupec je tak
/// široký, jak ho potřebuje to nejširší, co v něm stojí, plus vzduch kolem. Hustotu tisku řídí
/// písmo, velikost značek a odstupy mezi nimi - tedy sazba, ne ruční číslo.</para>
/// <para>Předtím se to za jediný den zkusilo dvěma způsoby a oba měly touž vadu: dvojice
/// „nejmenší/největší šířka" svádí k tomu, že jde o pár, ale zužovat uměl jen strop; jedno pole
/// „šířka sloupce" zase stálo proti volbě „stejně široké sloupce spojů", která ho tiše přebíjela.
/// Dvě nastavení na jednu věc znamenají, že se jedno z nich chová, jako by nefungovalo.</para>
/// </remarks>
public class LineTableSirkaSloupcuTests
{
    private static PropertyDescriptor Descriptor(LineTableElement prvek, string label) =>
        prvek.GetPropertyDescriptors().Single(d => d.Label == label);

    /// <summary>Šířka prvního sloupce spoje prvního bloku.</summary>
    private static float SirkaSloupce(LineTableElement prvek, DataContext data) =>
        prvek.Rozvrzeni(data)[0].SirkySloupcu[0];

    // ── Šířka z obsahu ──────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ Jádro modelu: sloupec se drží u obsahu, i když je v prvku místa habaděj. Dřív se zbytek
    /// prvku rozdělil mezi sloupce, takže u linky s pár spoji stál čas uprostřed centimetrového
    /// pole - a to je přesně to, co se lidem zvyklým na postskript nelíbilo.
    /// </summary>
    [Fact]
    public void SloupecSeDrziObsahu()
    {
        var uzkyPrvek = Prvek();
        uzkyPrvek.Width = 60f;
        var sirokyPrvek = Prvek();
        sirokyPrvek.Width = 400f;

        Assert.Equal(SirkaSloupce(uzkyPrvek, Data(spoju: 5)),
            SirkaSloupce(sirokyPrvek, Data(spoju: 5)), precision: 2);
    }

    /// <summary>Sloupec se značkami nad časem je širší než sloupec, kde jsou jen minuty.</summary>
    [Fact]
    public void SloupecSeZnackamiJeSirsiNezMinutovy()
    {
        float sZnackami = SirkaSloupce(Prvek(), DataSeZnackami(spoju: 20));
        float jenMinuty = SirkaSloupce(Prvek(), Data(spoju: 20));

        Assert.True(sZnackami > jenMinuty + 0.2f,
            $"se značkami {sZnackami:0.00} mm, jen s minutami {jenMinuty:0.00} mm");
    }

    /// <summary>Menší písmo = užší sloupec. Tohle je hlavní páka na hustotu tisku.</summary>
    [Fact]
    public void MensiPismoZuziSloupec()
    {
        var velke = Prvek();
        velke.DataStyle.FontSize = 9f;
        var male = Prvek();
        male.DataStyle.FontSize = 5f;

        Assert.True(SirkaSloupce(male, Data(spoju: 20)) < SirkaSloupce(velke, Data(spoju: 20)) - 1f,
            "menší písmo sloupec nezúžilo");
    }

    /// <summary>Pod tvrdou hranici čitelnosti se sloupec nedostane ani u drobného písma.</summary>
    [Fact]
    public void SloupecNepodlezeTvrdouHranici()
    {
        var prvek = Prvek();
        prvek.DataStyle.FontSize = 3f;

        Assert.True(SirkaSloupce(prvek, Data(spoju: 20)) >= 3f);
    }

    /// <summary>Jednotná šířka srovná všechny sloupce na ten nejširší - cesta k pravidelné
    /// mřížce za cenu volného místa.</summary>
    [Fact]
    public void JednotnaSirkaSrovnaSloupceNaNejsirsi()
    {
        var data = DataSeZnackami(spoju: 20);
        var prvek = Prvek();
        prvek.JednotnaSirkaSpoju = true;

        var sirky = prvek.Rozvrzeni(data)[0].SirkySloupcu;
        Assert.All(sirky, w => Assert.Equal(sirky[0], w, precision: 3));
    }

    /// <summary>Výchozí je vypnutá - úspora místa vyhrála a odpovídá to starému tisku.</summary>
    [Fact]
    public void JednotnaSirkaJeVychoziVypnuta()
        => Assert.False(new LineTableElement().JednotnaSirkaSpoju);

    /// <summary>Každý blok se musí vejít do prvku i s tím, že jsou sloupce různě široké -
    /// dělení proto sčítá skutečné šířky, ne počet × základ.</summary>
    [Fact]
    public void BlokySeVejdouDoPrvkuISRuznymiSirkami()
    {
        var data = DataSeZnackami(spoju: 60);
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 120 };

        var bloky = prvek.Rozvrzeni(data);

        Assert.All(bloky, b => Assert.True(b.SirkaMm <= prvek.Width + 0.01f,
            $"blok měří {b.SirkaMm:0.0} mm, prvek má {prvek.Width:0.0} mm"));
        Assert.Equal(60, bloky.Sum(b => b.Pocet));
    }

    /// <summary>Tabulka končí tam, kde skončí sloupce - vpravo zbyde volno, které vyplní až
    /// dorovnání prázdnými sloupci.</summary>
    [Fact]
    public void UzkaTabulkaNechaMistoVpravo()
    {
        var prvek = Prvek();

        var blok = prvek.Rozvrzeni(Data(spoju: 5))[0];

        Assert.True(blok.SirkaMm < prvek.Width - 20f,
            $"blok měří {blok.SirkaMm:0.0} mm z {prvek.Width:0.0} mm - sloupce se roztáhly");
    }

    // ── Vzduch kolem obsahu ─────────────────────────────────────────────────

    /// <summary>Vnitřní okraj buňky je páka na vzduch kolem času - sloupec o něj roste.</summary>
    [Fact]
    public void VnitrniOkrajRozsiriSloupec()
    {
        var data = Data(spoju: 20);
        var siroky = Prvek();
        siroky.DataStyle.PaddingLeft = 2f;
        siroky.DataStyle.PaddingRight = 2f;

        Assert.True(SirkaSloupce(siroky, data) > SirkaSloupce(Prvek(), data) + 1f,
            "okraj se do šířky sloupce nepropsal");
    }

    // ── Odstupy uvnitř sloupce ──────────────────────────────────────────────

    /// <summary>
    /// ⚠️ Mezera mezi značkami nad časem je jediná cesta, jak zúžit sloupec, ve kterém stojí
    /// dvojice piktogramů - ty jsou širší než čas a jinak by se muselo zmenšit písmo
    /// (požadavek Jan 22. 9.).
    /// </summary>
    [Fact]
    public void MezeraMeziZnackamiUtahneSloupec()
    {
        var data = DataSeZnackami(spoju: 20);
        var uzka = Prvek();
        uzka.MezeraMeziZnackamiMm = 0f;

        Assert.True(SirkaSloupce(uzka, data) < SirkaSloupce(Prvek(), data) - 0.2f,
            "odstup mezi značkami se do šířky nepropsal");
    }

    /// <summary>Mezera mezi hodinou a minutou je uvnitř času, takže utáhne i minutový sloupec.</summary>
    [Fact]
    public void MezeraHodinaMinutaUtahneSloupecSMinutami()
    {
        var data = Data(spoju: 20);
        var uzka = Prvek();
        uzka.MezeraHodinaMinutaMm = 0f;

        Assert.True(SirkaSloupce(uzka, data) < SirkaSloupce(Prvek(), data) - 0.1f,
            "odstup mezi hodinou a minutou se do šířky nepropsal");
    }

    /// <summary>
    /// ⚠️ Změna odstupu se musí projevit hned, na týchž datech: šířky se měří jednou a schovávají,
    /// takže bez přepočtu uživatel v panelu utahoval mezeru a v náhledu se nedělo nic
    /// (nález Jan 22. 9.).
    /// </summary>
    [Fact]
    public void ZmenaOdstupuPrepocitaSirkuNaTychzDatech()
    {
        var data = DataSeZnackami(spoju: 20);
        var prvek = Prvek();

        float zPisma = SirkaSloupce(prvek, data);
        prvek.MezeraMeziZnackamiMm = 0f;

        Assert.True(SirkaSloupce(prvek, data) < zPisma - 0.2f, "šířka se nepřepočítala");
    }

    // ── Hláška o úzkém sloupci ──────────────────────────────────────────────

    /// <summary>Sloupec počítaný z obsahu hlášku nevyvolá - není co hlásit.</summary>
    [Fact]
    public void SirkaZObsahuHlaskuNevyvola()
    {
        var data = DataSeZnackami(spoju: 20);
        var sablona = new Template();
        sablona.Sections.Add(new Section { Elements = { Prvek() } });

        Assert.Null(TripColumnWidthWarnings.Summarize(sablona, [data]));
    }

    /// <summary>
    /// Pevná šířka zadaná přímo sloupci spoje je výslovné zadání a obsah v ní přeteče - to je
    /// jediná cesta, jak dnes přetečení vyrobit, a musí se ozvat.
    /// </summary>
    [Fact]
    public void PevnaSirkaSloupceSeOhlasi()
    {
        var data = DataSeZnackami(spoju: 20);
        var prvek = Prvek();
        prvek.Columns.Single(c => c.Key == ColumnKeys.Trip).Width = 3f;
        var sablona = new Template();
        sablona.Sections.Add(new Section { Elements = { prvek } });

        var hlaska = TripColumnWidthWarnings.Summarize(sablona, [data]);

        Assert.NotNull(hlaska);
        Assert.Contains("obsah potřebuje", hlaska);
        Assert.Contains("značky platnosti", hlaska);
    }

    // ── Panel a kopie ───────────────────────────────────────────────────────

    /// <summary>Nastavení šířky jsou v panelu v jedné skupině.</summary>
    [Theory]
    [InlineData("Mezera mezi značkami")]
    [InlineData("Mezera mezi značkou a časem")]
    [InlineData("Mezera mezi hodinou a minutou")]
    [InlineData("Dorovnat prázdnými sloupci")]
    [InlineData("Stejně široké sloupce spojů")]
    [InlineData("Mezera mezi skupinami sloupců")]
    public void NastaveniSirkyJsouVJedneSkupine(string label)
        => Assert.Equal("Šířka sloupců", Descriptor(Prvek(), label).Group);

    /// <summary>Ruční šířka sloupce spoje v panelu <b>není</b> - počítá se z obsahu.</summary>
    [Fact]
    public void RucniSirkaSloupceVPaneluNeni()
        => Assert.DoesNotContain(Prvek().GetPropertyDescriptors(),
            d => d.Label.Contains("Šířka sloupce spoje"));

    /// <summary>Prázdná hodnota se v panelu zadává nulou - jinak by z něj nešel odstup zrušit.</summary>
    [Fact]
    public void NulaVPaneluZnamenaBezOmezeni()
    {
        var prvek = Prvek();
        var d = (FloatPropertyDescriptor)Descriptor(prvek, "Mezera mezi značkami");

        d.Set(0.4f);
        Assert.Equal(0.4f, prvek.MezeraMeziZnackamiMm);
        d.Set(0f);
        Assert.Null(prvek.MezeraMeziZnackamiMm);
    }

    /// <summary>Odstupy musejí přežít kopii prvku, jinak by se duplikát tiskl jinak.</summary>
    [Fact]
    public void KopiePrvkuNeseOdstupy()
    {
        var prvek = Prvek();
        prvek.MezeraMeziZnackamiMm = 0.2f;
        prvek.MezeraZnackaCasMm = 0.3f;
        prvek.MezeraHodinaMinutaMm = 0.1f;

        var kopie = (LineTableElement)prvek.Clone();

        Assert.Equal(0.2f, kopie.MezeraMeziZnackamiMm);
        Assert.Equal(0.3f, kopie.MezeraZnackaCasMm);
        Assert.Equal(0.1f, kopie.MezeraHodinaMinutaMm);
    }

    // ── Fixtury ─────────────────────────────────────────────────────────────

    /// <summary>Prvek s drobným písmem vývěskové šablony - při 9 pt si sám čas říká o 7 mm.</summary>
    private static LineTableElement Prvek()
    {
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 200 };
        prvek.DataStyle.FontSize = 5f;
        return prvek;
    }

    /// <summary>Táž linka, ale se značkami platnosti nad časy - ty jsou 1,5x zvětšené, takže
    /// sloupec s nimi je znatelně širší než sloupec se samotnými časy.</summary>
    private static DataContext DataSeZnackami(int spoju) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(1, spoju).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("PLATNOST", new JsonArray("X", "BEV")),
            ("CASY", new JsonArray("8:00", "8:20"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));

    private static DataContext Data(int spoju) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(1, spoju).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20", "8:40"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
