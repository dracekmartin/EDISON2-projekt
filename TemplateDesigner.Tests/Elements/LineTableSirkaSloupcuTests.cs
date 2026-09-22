using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Šířka sloupce spoje u tabulky linkového JŘ.
/// </summary>
/// <remarks>
/// <para>Do 22. 9. tu byla dvojice „nejmenší/největší": dvě pole svádějí k tomu, že jde o pár,
/// jenže zužovat uměl jen strop - a ten se zarážel o změřený obsah, takže sloupec s dvojicí
/// piktogramů nad časy nešlo zúžit vůbec (linka 330510: 5,43 mm, ať se zadalo cokoli).</para>
/// <para>Nový model je jedno číslo a jedno pravidlo: <b>zadaná šířka platí všude, kde se obsah
/// vejde, a kde ne, zůstane sloupec tak široký, jak obsah potřebuje</b>. Uživatel tím nastaví
/// hustotu podle většiny sloupců, nic nepřeteče a písmo zůstane, jaké je (rozhodnutí Jan 22. 9.).
/// Cena: sloupce nejsou stejně široké - od toho je volba „Stejně široké sloupce spojů".</para>
/// </remarks>
public class LineTableSirkaSloupcuTests
{
    private static PropertyDescriptor Descriptor(LineTableElement prvek, string label) =>
        prvek.GetPropertyDescriptors().Single(d => d.Label == label);

    /// <summary>Šířka prvního sloupce spoje prvního bloku.</summary>
    private static float SirkaSloupce(LineTableElement prvek, DataContext data) =>
        prvek.Rozvrzeni(data)[0].SirkySloupcu[0];

    /// <summary>Nejširší sloupec spoje prvního bloku.</summary>
    private static float NejsirsiSloupec(LineTableElement prvek, DataContext data) =>
        prvek.Rozvrzeni(data)[0].SirkySloupcu.Max();

    // ── Zadaná šířka ────────────────────────────────────────────────────────

    /// <summary>
    /// Jádro požadavku: u linky, která se do prvku vejde, se sloupce bez zadané šířky roztáhnou
    /// přes celý prvek. Zadaná hodnota je má zastavit.
    /// </summary>
    [Fact]
    public void ZadanaSirkaZastaviRoztahovani()
    {
        var data = Data(spoju: 5);
        float bez = SirkaSloupce(Prvek(), data);
        var uzky = Prvek();
        uzky.SirkaSloupceSpojeMm = 8f;

        Assert.True(bez > 10f, $"sloupec bez zadané šířky vyšel {bez:0.00} mm - fixtura neroztahuje");
        Assert.Equal(8f, SirkaSloupce(uzky, data), precision: 2);
    }

    /// <summary>Prázdná hodnota = chování jako dosud. Existující šablony tisknou totéž.</summary>
    [Fact]
    public void BezZadaneSirkySeNicNemeni()
    {
        var data = Data(spoju: 5);

        var bezZadani = Prvek();
        bezZadani.SirkaSloupceSpojeMm = null;

        Assert.Equal(SirkaSloupce(Prvek(), data), SirkaSloupce(bezZadani, data), precision: 3);
    }

    /// <summary>
    /// ⚠️ Zadaná šířka sežere nejdřív <b>vůli</b> kolem obsahu, teprve o číslice se zarazí.
    /// Bez toho zůstal sloupec o milimetr širší, než si uživatel řekl, a u sloupců jen s minutami
    /// to bylo vidět jako zbytečné místo (nález Jan 22. 9.).
    /// </summary>
    [Fact]
    public void ZadanaSirkaSezereNejdrivVuli()
    {
        var data = Data(spoju: 5);
        var uzky = Prvek();
        uzky.SirkaSloupceSpojeMm = 0.5f;   // pod vším, takže zůstane holý obsah
        var automaticky = Prvek();
        automaticky.Width = 1f;            // bez zadání rozhoduje obsah i s vůlí

        float holy = SirkaSloupce(uzky, data);
        float sVuli = SirkaSloupce(automaticky, data);

        Assert.True(holy >= 3f, $"sloupec vyšel {holy:0.00} mm - pod tvrdou hranicí čitelnosti");
        Assert.True(sVuli > holy + 0.5f,
            $"holý obsah {holy:0.00} mm proti {sVuli:0.00} mm s vůlí - vůle neustoupila");
    }

    /// <summary>Zadaná hodnota nad obsahem platí přesně - vůle se k ní nepřičítá.</summary>
    [Fact]
    public void ZadanaSirkaNadObsahemPlatiPresne()
    {
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 6f;

        Assert.Equal(6f, SirkaSloupce(prvek, Data(spoju: 20)), precision: 2);
    }

    // ── Sloupec, do kterého se obsah nevejde ────────────────────────────────

    /// <summary>
    /// ⚠️ Vlastní smysl nového modelu: sloupec se širším obsahem se nezúží pod svou potřebu,
    /// takže obsah nepřeteče do sousedního sloupce a písmo zůstane nezmenšené.
    /// </summary>
    [Fact]
    public void SirokyObsahSloupecNezuzi()
    {
        var data = DataSeZnackami(spoju: 20);
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 4f;

        // Značky platnosti se sázejí 1,5x zvětšené, takže sloupec s nimi musí zůstat širší.
        Assert.True(SirkaSloupce(prvek, data) > 4f,
            $"sloupec vyšel {SirkaSloupce(prvek, data):0.00} mm - obsah se do něj nevejde");
    }

    /// <summary>Sloupce bez širokého obsahu se zúžit mají - jinak by zadaná hodnota nic nedělala.</summary>
    [Fact]
    public void SloupecBezSirokehoObsahuSeZuzi()
    {
        var data = Data(spoju: 20);
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 6f;

        Assert.Equal(6f, NejsirsiSloupec(prvek, data), precision: 2);
    }

    /// <summary>Jednotná šířka srovná všechny sloupce na ten nejširší - to je ta cesta
    /// k pravidelné mřížce za cenu volného místa.</summary>
    [Fact]
    public void JednotnaSirkaSrovnaSloupceNaNejsirsi()
    {
        var data = DataSeZnackami(spoju: 20);
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 4f;
        prvek.JednotnaSirkaSpoju = true;

        var sirky = prvek.Rozvrzeni(data)[0].SirkySloupcu;
        Assert.All(sirky, w => Assert.Equal(sirky[0], w, precision: 3));
    }

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

    // ── Dorovnání volného místa ─────────────────────────────────────────────

    /// <summary>
    /// Se zadanou šířkou zbyde vpravo volno i u tabulky, která se vůbec neřeže. Zapnuté dorovnání
    /// ho má rozkreslit prázdnými sloupci - to je ten postskriptový vzhled.
    /// </summary>
    [Fact]
    public void SeZadanouSirkouSeVolneMistoDorovna()
    {
        var data = Data(spoju: 5);
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 8f;
        prvek.PadEmptyColumns = true;

        // Prázdné sloupce nese kreslení, ne rozvržení - ověřuje se, že po nich zbylo místo.
        var blok = prvek.Rozvrzeni(data)[0];
        Assert.True(blok.SirkaMm < prvek.Width - 8f,
            $"blok měří {blok.SirkaMm:0.0} mm z {prvek.Width:0.0} mm - nezbylo co dorovnávat");
    }

    // ── Vnitřní okraj ───────────────────────────────────────────────────────

    /// <summary>
    /// Vůle kolem času se s vnitřním okrajem <b>nesčítá</b> - platí ta větší z nich. Měří se bez
    /// zadané šířky; s ní vůle ustupuje (viz <c>ZadanaSirkaSezereNejdrivVuli</c>).
    /// </summary>
    [Fact]
    public void VuleSeSOkrajemNescita()
    {
        var data = Data(spoju: 40);
        var nulovy = Uzky();
        nulovy.DataStyle.PaddingLeft = 0f;
        nulovy.DataStyle.PaddingRight = 0f;
        var vychozi = Uzky();
        var siroky = Uzky();
        siroky.DataStyle.PaddingLeft = 2f;
        siroky.DataStyle.PaddingRight = 2f;

        // Výchozí okraj (0,5 + 0,5) je právě vůle, takže vynulování nic nezmění.
        Assert.Equal(SirkaSloupce(vychozi, data), SirkaSloupce(nulovy, data), precision: 3);
        Assert.True(SirkaSloupce(siroky, data) > SirkaSloupce(vychozi, data),
            "okraj větší než vůle se neuplatnil");

        static LineTableElement Uzky()
        {
            // Prvek tak úzký, že o sloupci rozhoduje obsah, a bez zadané šířky - jinak by vůle
            // ustoupila a okraj by neměl co ukázat.
            var prvek = new LineTableElement { X = 0, Y = 0, Width = 1 };
            prvek.DataStyle.FontSize = 5f;
            return prvek;
        }
    }

    // ── Hláška o úzkém sloupci ──────────────────────────────────────────────

    /// <summary>
    /// Zadaná šířka hlášku nevyvolá - sloupec se pod obsah nezúží, takže není co hlásit.
    /// </summary>
    [Fact]
    public void ZadanaSirkaHlaskuNevyvola()
    {
        var data = DataSeZnackami(spoju: 20);
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 4f;
        var sablona = new Template();
        sablona.Sections.Add(new Section { Elements = { prvek } });

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
    [InlineData("Šířka sloupce spoje")]
    [InlineData("Dorovnat prázdnými sloupci")]
    [InlineData("Stejně široké sloupce spojů")]
    [InlineData("Mezera mezi skupinami sloupců")]
    public void NastaveniSirkyJsouVJedneSkupine(string label)
        => Assert.Equal("Šířka sloupců", Descriptor(Prvek(), label).Group);

    /// <summary>Prázdná hodnota se v panelu zadává nulou - jinak by z něj nešlo omezení zrušit.</summary>
    [Fact]
    public void NulaVPaneluZnamenaBezOmezeni()
    {
        var prvek = Prvek();
        var d = (FloatPropertyDescriptor)Descriptor(prvek, "Šířka sloupce spoje");

        d.Set(6f);
        Assert.Equal(6f, prvek.SirkaSloupceSpojeMm);
        d.Set(0f);
        Assert.Null(prvek.SirkaSloupceSpojeMm);
    }

    /// <summary>Šířka musí přežít kopii prvku, jinak by se duplikát tiskl jinak.</summary>
    [Fact]
    public void KopiePrvkuNeseSirku()
    {
        var prvek = Prvek();
        prvek.SirkaSloupceSpojeMm = 7f;

        Assert.Equal(7f, ((LineTableElement)prvek.Clone()).SirkaSloupceSpojeMm);
    }

    // ── Fixtury ─────────────────────────────────────────────────────────────

    /// <summary>Prvek s drobným písmem vývěskové šablony - při 9 pt si sám čas říká o 7 mm,
    /// takže by na něm nešlo zkoušet, co dělá šířka pod tím.</summary>
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
