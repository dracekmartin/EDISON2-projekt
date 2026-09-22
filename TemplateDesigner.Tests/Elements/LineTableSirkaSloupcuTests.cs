using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Dno a strop šířky sloupce spoje u tabulky linkového JŘ.
/// </summary>
/// <remarks>
/// <para>Požadavek Jan 8. 9.: šířka měla dno (z písma a vnitřního okraje), ale <b>žádný strop</b>,
/// takže zbytek prvku se vždy rozdělil mezi sloupce. U linky s pár spoji vyšel sloupec extrémně
/// široký a čas stál uprostřed prázdného pole - lidem, kteří dnes tisknou postskriptem, se to
/// nelíbí. Starý tisk místo roztahování drží sloupce úzké a zbytek vyplní prázdnými sloupci.</para>
/// <para>Doplněním stropu vzniknou tři chování z jednoho pravidla, aniž si mezi nimi kdokoli volí:
/// hodně spojů → šířka na dně a řeže se do bloků (fungovalo už dřív); málo spojů → šířka na stropu
/// a zbytek se dorovná; mezi tím → šířka plave a vyplní prvek.</para>
/// <para>Dno jde nově zadat i <b>napřímo</b>. Přes vnitřní okraj to byla oklika - uživatel musel
/// znát vzorec „šířka času + okraj vlevo + okraj vpravo", aby výsledek odhadl.</para>
/// </remarks>
public class LineTableSirkaSloupcuTests
{
    private static PropertyDescriptor Descriptor(LineTableElement prvek, string label) =>
        prvek.GetPropertyDescriptors().Single(d => d.Label == label);

    private static float SirkaSloupce(LineTableElement prvek, DataContext data) =>
        prvek.Rozvrzeni(data)[0].SloupecSpojeMm;

    // ── Strop ───────────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ Jádro požadavku: u linky, která se do prvku vejde, se sloupce bez stropu roztáhnou přes
    /// celý prvek. Strop je má zastavit.
    /// </summary>
    [Fact]
    public void StropZastaviRoztahovaniUMaleLinky()
    {
        var data = Data(spoju: 5);
        float bez = SirkaSloupce(Prvek(), data);
        float dno = Dno(data);
        float strop = (dno + bez) / 2f;   // mezi dnem a přirozenou šířkou, ať se opravdu uplatní

        var seStropem = Prvek();
        seStropem.NejvetsiSirkaSpojeMm = strop;

        Assert.True(bez > dno + 1f,
            $"sloupec bez stropu vyšel {bez:0.00} mm při dnu {dno:0.00} mm - fixtura neroztahuje, "
            + "test by nic neověřil");
        Assert.Equal(strop, SirkaSloupce(seStropem, data), precision: 2);
    }

    /// <summary>Prázdný strop = chování jako dosud. Existující šablony tisknou totéž.</summary>
    [Fact]
    public void BezStropuSeNicNemeni()
    {
        var data = Data(spoju: 5);

        Assert.Equal(SirkaSloupce(Prvek(), data), SirkaSloupce(
            new LineTableElement { X = 0, Y = 0, Width = 200, NejvetsiSirkaSpojeMm = null }, data),
            precision: 3);
    }

    /// <summary>
    /// Strop nesmí podlézt tvrdou hranici čitelnosti - kdo zadá nesmysl, dostane 3 mm.
    /// </summary>
    [Fact]
    public void StropPodTvrdouHraniciSeNeuplatni()
    {
        var data = Data(spoju: 5);
        var prvek = Prvek();
        prvek.NejvetsiSirkaSpojeMm = 0.5f;

        Assert.Equal(3f, SirkaSloupce(prvek, data), precision: 2);
    }

    /// <summary>
    /// ⚠️ Na zúžení sloupce stačí <b>samotný strop</b> - stáhne i měřené dno. Do 22. 9. se o ně
    /// zarazil, takže úzký sloupec vyžadoval vyplnit obě pole naráz: samotné dno sloupec nezužuje
    /// (je to spodní hranice) a samotný strop nedosáhl pod obsah. Na lince 330510 to znamenalo,
    /// že ani se stropem 4 mm se sloupec nehnul z 5,43 mm (nález Jan 22. 9.).
    /// </summary>
    [Fact]
    public void SamotnyStropStahneISloupecPodZmerenyObsah()
    {
        var data = Data(spoju: 40);
        float zmereno = Dno(data);
        float strop = zmereno - 1f;
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 120, NejvetsiSirkaSpojeMm = strop };

        Assert.True(strop > 3f, $"fixtura měří {zmereno:0.00} mm - strop by spadl na tvrdou hranici");
        Assert.Equal(strop, SirkaSloupce(prvek, data), precision: 2);
        Assert.Null(prvek.NejmensiSirkaSpojeMm);
    }

    /// <summary>
    /// U linky, která se dnes řeže, strop nezasáhne - sloupec tam sedí na dně a strop je nad ním.
    /// Tím je doložené, že nová volba nemění to, co dnes funguje.
    /// </summary>
    [Fact]
    public void UVelkeLinkyStropNezasahne()
    {
        var data = Data(spoju: 40);
        var uzky = new LineTableElement { X = 0, Y = 0, Width = 120 };
        float prirozena = SirkaSloupce(uzky, data);
        var uzkySeStropem = new LineTableElement
        {
            X = 0, Y = 0, Width = 120,
            // Strop nad přirozenou šířkou - u řezané tabulky je sloupec u dna, takže nemá co dělat.
            NejvetsiSirkaSpojeMm = prirozena + 10f,
        };

        Assert.Equal(prirozena, SirkaSloupce(uzkySeStropem, data), precision: 3);
        Assert.Equal(uzky.Rozvrzeni(data).Count, uzkySeStropem.Rozvrzeni(data).Count);
    }

    // ── Dorovnání volného místa ─────────────────────────────────────────────

    /// <summary>
    /// Se stropem zbyde vpravo volno i u tabulky, která se vůbec neřeže. Zapnuté dorovnání ho má
    /// rozkreslit prázdnými sloupci - to je ten postskriptový vzhled.
    /// </summary>
    [Fact]
    public void SeStropemSeVolneMistoDorovna()
    {
        var data = Data(spoju: 5);
        var prvek = Prvek();
        prvek.NejvetsiSirkaSpojeMm = 6f;
        prvek.PadEmptyColumns = true;

        // Prázdné sloupce nese kreslení, ne rozvržení - ověřuje se, že po nich zbylo místo:
        // blok je užší než prvek, takže je co dorovnávat.
        var blok = prvek.Rozvrzeni(data)[0];
        Assert.True(blok.SirkaMm < prvek.Width - 6f,
            $"blok měří {blok.SirkaMm:0.0} mm z {prvek.Width:0.0} mm - strop nenechal volné místo, "
            + "takže dorovnání nemá co vyplnit");
    }

    // ── Dno napřímo ─────────────────────────────────────────────────────────

    /// <summary>Zadané dno nahradí výpočet z písma a okraje, takže okraj na šířku přestane sahat.</summary>
    [Fact]
    public void ZadaneDnoPrebijeVypocetZOkraje()
    {
        var data = Data(spoju: 40);
        // Dno musí být nad přirozenou šířkou, jinak by rozhodoval zbytek prvku a ne ono.
        float dno = SirkaSloupce(new LineTableElement { X = 0, Y = 0, Width = 120 }, data) + 5f;
        var sOkrajem = new LineTableElement { X = 0, Y = 0, Width = 120, NejmensiSirkaSpojeMm = dno };
        sOkrajem.DataStyle.PaddingLeft = 2f;
        sOkrajem.DataStyle.PaddingRight = 2f;
        var bezOkraje = new LineTableElement { X = 0, Y = 0, Width = 120, NejmensiSirkaSpojeMm = dno };

        // Dno je spodní hranice, ne přesná šířka: zvednutím dna se do bloku vejde méně spojů,
        // takže na každý zbyde o něco víc místa a sloupec vyjde širší než zadané minimum.
        Assert.True(SirkaSloupce(bezOkraje, data) >= dno,
            $"sloupec vyšel {SirkaSloupce(bezOkraje, data):0.00} mm pod zadaným dnem {dno:0.00} mm");
        // Vlastní pointa: s okrajem i bez něj totéž - okraj na šířku přestal sahat.
        Assert.Equal(SirkaSloupce(bezOkraje, data), SirkaSloupce(sOkrajem, data), precision: 3);
    }

    /// <summary>
    /// ⚠️ Dno menší, než obsah měří, se <b>uplatní</b> - to je celý smysl toho pole. Do 22. 9. ho
    /// pojistka jen zvedala, takže u vývěsky s dvojicí piktogramů nad časy pole tiše nedělalo nic:
    /// dno tam drží značky (na lince 330510 5,43 mm), ne čas, a uživatel se pod ně nedostal.
    /// Cenou je, že se do užšího sloupce značky nevejdou - vidí to hned v náhledu.
    /// </summary>
    [Fact]
    public void DnoPodSirkouObsahuSeUplatni()
    {
        var data = Data(spoju: 40);
        float zmereno = Dno(data);
        float zadane = zmereno - 1f;
        // Prvek o šířce 1 mm: o sloupci rozhoduje jen dno, ne dělení zbytku mezi spoje.
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 1, NejmensiSirkaSpojeMm = zadane };

        Assert.True(zadane > 3f, $"fixtura měří {zmereno:0.00} mm - zadané dno by spadlo na tvrdou hranici");
        Assert.Equal(zadane, SirkaSloupce(prvek, data), precision: 2);
    }

    /// <summary>Vyplněné dno odpojí i vnitřní okraj buňky - o šířce rozhoduje jen zadané číslo.</summary>
    [Fact]
    public void ZadaneDnoOdpojiVnitrniOkraj()
    {
        var data = Data(spoju: 40);
        float zadane = Dno(data) - 1f;
        var siroky = new LineTableElement { X = 0, Y = 0, Width = 1, NejmensiSirkaSpojeMm = zadane };
        siroky.DataStyle.PaddingLeft = 2f;
        siroky.DataStyle.PaddingRight = 2f;

        Assert.Equal(zadane, SirkaSloupce(siroky, data), precision: 2);
    }

    /// <summary>
    /// Vůle se s vnitřním okrajem <b>nesčítá</b> - platí ta větší z nich, takže větší okraj dál
    /// funguje a nulový se zarazí o vůli.
    /// </summary>
    [Fact]
    public void VuleSeSOkrajemNescita()
    {
        var data = Data(spoju: 40);
        var nulovy = new LineTableElement { X = 0, Y = 0, Width = 120 };
        nulovy.DataStyle.PaddingLeft = 0f;
        nulovy.DataStyle.PaddingRight = 0f;
        var vychozi = new LineTableElement { X = 0, Y = 0, Width = 120 };
        var siroky = new LineTableElement { X = 0, Y = 0, Width = 120 };
        siroky.DataStyle.PaddingLeft = 2f;
        siroky.DataStyle.PaddingRight = 2f;

        // Výchozí okraj (0,5 + 0,5) je právě vůle, takže vynulování nic nezmění.
        Assert.Equal(SirkaSloupce(vychozi, data), SirkaSloupce(nulovy, data), precision: 3);
        Assert.True(SirkaSloupce(siroky, data) > SirkaSloupce(vychozi, data),
            "okraj větší než vůle se neuplatnil");
    }

    /// <summary>Ani zadané dno nepustí sloupec pod tvrdou hranici čitelnosti.</summary>
    [Fact]
    public void ZadaneDnoNepodlezeTvrdouHranici()
    {
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 120, NejmensiSirkaSpojeMm = 0.5f };

        Assert.True(SirkaSloupce(prvek, Data(spoju: 40)) >= 3f, "zadané dno podlezlo tvrdou hranici 3 mm");
    }

    // ── Panel ───────────────────────────────────────────────────────────────

    /// <summary>Obě nová pole jsou v panelu a v jedné skupině se zbytkem nastavení šířky.</summary>
    [Theory]
    [InlineData("Nejmenší šířka sloupce spoje")]
    [InlineData("Největší šířka sloupce spoje")]
    [InlineData("Dorovnat prázdnými sloupci")]
    [InlineData("Stejně široké sloupce spojů")]
    [InlineData("Mezera mezi skupinami sloupců")]
    public void NastaveniSirkyJsouVJedneSkupine(string label)
        => Assert.Equal("Šířka sloupců", Descriptor(Prvek(), label).Group);

    /// <summary>Prázdná hodnota se v panelu zadává nulou - jinak by z něj nešlo omezení zrušit.</summary>
    [Fact]
    public void NulaVPanelaZnamenaBezOmezeni()
    {
        var prvek = Prvek();
        var d = (FloatPropertyDescriptor)Descriptor(prvek, "Největší šířka sloupce spoje");

        d.Set(6f);
        Assert.Equal(6f, prvek.NejvetsiSirkaSpojeMm);
        d.Set(0f);
        Assert.Null(prvek.NejvetsiSirkaSpojeMm);
    }

    /// <summary>Obě nová pole musí přežít kopii prvku, jinak by se duplikát tiskl jinak.</summary>
    [Fact]
    public void KopiePrvkuNesePodleObouMezi()
    {
        var prvek = Prvek();
        prvek.NejmensiSirkaSpojeMm = 3.5f;
        prvek.NejvetsiSirkaSpojeMm = 7f;

        var kopie = (LineTableElement)prvek.Clone();

        Assert.Equal(3.5f, kopie.NejmensiSirkaSpojeMm);
        Assert.Equal(7f, kopie.NejvetsiSirkaSpojeMm);
    }

    // ── Dno, strop a rovnoměrné dělení dohromady ────────────────────────────

    /// <summary>
    /// Zadané dno šířky sloupce pustí do části míň spojů, takže jich je víc - a části musejí
    /// i tak vyjít stejně velké a vejít se do prvku.
    /// </summary>
    /// <remarks>
    /// Dno a rovnoměrné dělení do sebe zasahují: dno určuje, kolik spojů se do části vejde, a podle
    /// toho se teprve počítá, na kolik částí se linka rozdělí. Kdyby se rovnoměrné dělení počítalo
    /// z jiného čísla než z toho, které dno vrátí, části by přetekly přes hranu prvku.
    /// </remarks>
    [Fact]
    public void DnoSirkySloupceRozdeliSpojeDoVicCasti_AStejneVelkych()
    {
        var data = Data(spoju: 40);
        var siroke = Prvek();
        var uzke = Prvek();
        uzke.NejmensiSirkaSpojeMm = 12f;

        var bezDna = siroke.Rozvrzeni(data);
        var sDnem = uzke.Rozvrzeni(data);

        Assert.True(sDnem.Count > bezDna.Count,
            $"se dnem 12 mm má být částí víc, vyšlo {sDnem.Count} proti {bezDna.Count}");
        Assert.True(sDnem.Max(b => b.Pocet) - sDnem.Min(b => b.Pocet) <= 1,
            $"části vyšly {string.Join(" + ", sDnem.Select(b => b.Pocet))}");
        Assert.All(sDnem, b => Assert.True(b.SirkaMm <= uzke.Width + 0.01f,
            $"část je {b.SirkaMm:0.0} mm, prvek má {uzke.Width} mm"));
        Assert.Equal(40, sDnem.Sum(b => b.Pocet));
    }

    // ── Hláška o úzkém sloupci ──────────────────────────────────────────────

    /// <summary>
    /// Sloupec zúžený pod obsah se musí ohlásit ve stavovém řádku - jinak vypadá přetečení
    /// značek do souseda jako vada sazby, ne jako důsledek nastavení (požadavek Jan 22. 9.).
    /// </summary>
    [Fact]
    public void UzkySloupecSeOhlasi()
    {
        // Značky platnosti se sázejí 1,5x zvětšené, takže jsou širší než čas - u nich se
        // přetečení pozná dřív než u samotných číslic.
        var data = DataSeZnackami(spoju: 40);
        var prvek = Prvek();
        prvek.NejvetsiSirkaSpojeMm = 3.2f;
        var sablona = new Template();
        sablona.Sections.Add(new Section { Elements = { prvek } });

        var hlaska = TripColumnWidthWarnings.Summarize(sablona, [data]);

        Assert.NotNull(hlaska);
        Assert.Contains("obsah potřebuje", hlaska);
        Assert.Contains("značky platnosti", hlaska);
        Assert.Contains(ElementNames.ForUser(prvek), hlaska);
    }

    /// <summary>
    /// Sloupec, do kterého se obsah vejde, hlášku nevyvolá - a to ani když je užší než dno.
    /// Dno zahrnuje vůli 1 mm kolem obsahu; její ztráta není přetečení a hlásit se nemá
    /// (nález Jan 22. 9.: sloupec 5 mm proti hlášené potřebě 5,4 mm, zatímco obsah měřil 4,4 mm).
    /// </summary>
    [Fact]
    public void SloupecBezVuleHlaskuNevyvola()
    {
        var data = Data(spoju: 40);
        var tesny = Prvek();
        tesny.NejvetsiSirkaSpojeMm = Dno(data) - 0.5f;   // pod dnem, ale obsah se vejde
        var sablona = new Template();
        sablona.Sections.Add(new Section { Elements = { Prvek() } });
        var tesnaSablona = new Template();
        tesnaSablona.Sections.Add(new Section { Elements = { tesny } });

        Assert.Null(TripColumnWidthWarnings.Summarize(sablona, [data]));
        Assert.Null(TripColumnWidthWarnings.Summarize(tesnaSablona, [data]));
    }

    private static LineTableElement Prvek() => new() { X = 0, Y = 0, Width = 200 };

    /// <summary>Dno fixtury - prvek tak úzký, že o šířce sloupce rozhoduje jen spodní hranice.
    /// Měří se, ne hardcoduje: závisí na písmu, a to se může změnit.</summary>
    private static float Dno(DataContext data) =>
        new LineTableElement { X = 0, Y = 0, Width = 1 }.Rozvrzeni(data)[0].SloupecSpojeMm;

    /// <summary>Táž linka, ale se značkami platnosti nad časy - ty jsou 1,5x zvětšené.</summary>
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
