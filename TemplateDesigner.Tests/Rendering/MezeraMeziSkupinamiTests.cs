using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Mezera mezi <b>skupinami sloupců</b> linkové tabulky - za popisným blokem a mezi skupinami dnů.
/// Vzor IDZK 940 dělí tabulku na popisnou skupinu × Pracovní dny × Víkendy a mezi nimi má volno.
/// </summary>
/// <remarks>
/// ⚠️ Měří se <b>poloha inkoustu</b>, ne hodnoty properties. Sloupce spojů se skládají na čtyřech
/// místech a tři věci se kreslí ručně mimo buňky (značky platnosti, značka návaznosti u času,
/// piktogram zastávky) - každá z nich si dřív odsazení počítala sama jako `fixedSum + i * tripW`.
/// Test proto tvrdí jediné: <b>v mezeře nesmí být nic</b>. Cesta, která by o mezeře nevěděla,
/// kreslí přesně tam - proto je mezera schválně širší než sloupec spoje.
/// </remarks>
public class MezeraMeziSkupinamiTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 30f;
    private const float SirkaSpoje = 11f;
    private const float MezeraMm = 16f;

    /// <summary>
    /// Prvek s <b>pevnými</b> šířkami všech sloupců a <b>bílou</b> mřížkou.
    /// </summary>
    /// <remarks>
    /// Pevné šířky: mezera pak sloupce neztenčí, jen posune, takže test může spočítat, kde má která
    /// mezera ležet. Bílá mřížka: vodorovné čáry jdou přes celou šířku prvku, tedy i přes mezeru -
    /// bez toho by „v mezeře nic není" neplatilo ani při správném kreslení.
    /// </remarks>
    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 100, Height = null,
        SirkaSloupceSpojeMm = SirkaSpoje,
        // ⚠️ Vypnuto schválně: konstanty níž počítají s tím, že sloupec spoje je přesně
        // `SirkaSpoje` široký a jen ten se značkou návaznosti je širší. Se zapnutou volbou
        // (dnes výchozí) jsou všechny sloupce široké jako ten nejširší a celá geometrie
        // se posune. Zapnutou variantu hlídá `ZnackaNavaznosti_JeVeSloupciSvehoSpoje_JednotnaSirka`.
        JednotnaSirkaSpoju = false,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8, BackgroundColor = "#FFE0E0E0" },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        HighlightOddTc = false,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.3f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = SirkaZastavky, Visible = true },
            // ⚠️ Šířka sloupce spoje je na prvku, ne pevná na sloupci: pevná je výslovné zadání
            // a obsah ji nerozšíří, takže by sloupec se značkou nebyl širší (od 22. 9.).
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj",
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>Tři spoje ve dvou skupinách dnů (1, 1, 2), a v datech všechno, co se kreslí ručně:
    /// značka platnosti, návaznost vedle času a piktogram u zastávky.</summary>
    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2"),
                ("ATRIBUTY", new JsonArray("L"))),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("8:00", "8:20", "8:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("9:00", "9:20", "9:40"))),
            // ⚠️ Značka návaznosti je na **posledním** spoji záměrně: sloupec se značkou je o ni
            // širší (parita s legacy), takže na spoji uprostřed by posunula všechny mezery za ním
            // a testy pozic mezer by měřily jiná čísla, než která si spočítaly.
            RenderSnapshot.Row(("CISLO_SPOJE", "5"), ("SKUPINA", "2"),
                ("CASY", new JsonArray("10:00", "10:20", "10:40")),
                ("PLATNOST", new JsonArray("X")),
                ("PRIPOJE", RenderSnapshot.Nested([("TYP", "vycka"), ("PORADI", "2"), ("CAS", "10:25")]))),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "V"), ("PORADI", "2")),
        ]));

    /// <summary>
    /// Táž linka, ale sloupec druhé skupiny je <b>prázdný až na značku platnosti</b>: spoj nemá
    /// číslo ani časy a skupina nemá nadpis.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bez toho by tenhle test nic neověřil. Značky platnosti se kreslí ručně, a zapomenutá
    /// mezera je neposune do mezery (tam by je „v mezeře nic není" našlo), ale do <b>cizího
    /// sloupce</b> - u tří spojů a dvou mezer přesně na sloupec prvního spoje. Pozná se to jen tak,
    /// že se pravý sloupec vyprázdní a čeká se v něm ta jedna značka.
    /// </remarks>
    private static DataContext DataJenZnackaPlatnosti() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("8:00", "8:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("9:00", "9:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "2"),
                ("PLATNOST", new JsonArray("X"))),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", ""), ("PORADI", "2")),
        ]));

    /// <summary>Značky platnosti se kreslí ručně mimo buňky (jedna buňka umí jen jeden font),
    /// takže o mezeře musí vědět samy - jinak skončí ve sloupci cizího spoje.</summary>
    [Fact]
    public void ZnackaPlatnosti_JeVeSloupciSvehoSpoje()
    {
        var prvek = Prvek();
        prvek.MezeraMeziSkupinamiMm = MezeraMm;

        using var bmp = Vykresli(prvek, DataJenZnackaPlatnosti());

        float odMm = SirkaZastavky + 2 * MezeraMm + 2 * SirkaSpoje;   // sloupec třetího spoje
        int inkoust = 0;
        for (int x = Sloupec(odMm + 0.5f); x <= Sloupec(odMm + SirkaSpoje - 0.5f); x++)
            inkoust += InkoustVeSloupci(bmp, x);

        Assert.True(inkoust > 0,
            $"ve sloupci spoje s platností ({odMm:0.#}-{odMm + SirkaSpoje:0.#} mm) není nic - "
            + "značka se nakreslila jinam, protože její kreslicí cesta o mezerách neví");
    }

    /// <summary>
    /// Táž fixtura, ale s <b>přípojným řádkem</b> (vyčká jako vložený řádek matice) - ten má
    /// vodorovnou čáru nad i pod sebou, takže se na něm nejdřív projevilo, že čáry mezeru přemostí.
    /// </summary>
    private static DataContext DataSPripojnymRadkem() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("8:00", "8:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("9:00", "9:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "5"), ("SKUPINA", "2"),
                ("CASY", new JsonArray("10:00", "10:40")),
                ("PLATNOST", new JsonArray("X")),
                ("PRIPOJE", RenderSnapshot.Nested(
                    [("TYP", "vycka"), ("PORADI", "1"), ("CAS", "10:05"), ("RADEK", "1")]))),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "V"), ("PORADI", "2")),
        ]));

    /// <summary>
    /// V mezeře nesmí být <b>ani vodorovná čára</b> - ani u přípojných řádků, které mají čáru nad
    /// i pod sebou.
    /// </summary>
    /// <remarks>
    /// Nález Jan 27. 8. na vzoru 940: „zůstane tam spojující úsečka mezi levou částí pravého okraje
    /// a pravou částí levého" a u přípojek „ta mezera udělána není vůbec". Obojí měla jednu příčinu -
    /// vodorovné čáry i pozadí řádku se kreslily přes <b>celou šířku prvku</b>, takže mezeru
    /// přemostily. Tenhle test jede se <b>černou</b> mřížkou, na rozdíl od
    /// <see cref="VMezereNeniNic_AniZjedneZeCtyrCestSkladaniSloupcu"/>, který bílou mřížkou měří
    /// jen obsah buněk.
    /// </remarks>
    [Fact]
    public void VMezereNeniAniVodorovnaCara_AniUPripojnychRadku()
    {
        var prvek = Prvek();
        prvek.Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FF000000", Width = 0.3f };
        prvek.MezeraMeziSkupinamiMm = MezeraMm;

        using var bmp = Vykresli(prvek, DataSPripojnymRadkem());

        foreach (var (od, do_) in OcekavaneMezery())
        {
            int x = Sloupec(od + 1f);
            int konec = Sloupec(do_ - 1f);
            for (; x <= konec; x++)
                Assert.True(InkoustVeSloupci(bmp, x) == 0,
                    $"v mezeře ({od:0.#}-{do_:0.#} mm) je čára - sloupec {x / MmToPx:0.#} mm má "
                    + "inkoust, takže vodorovná čára nebo pozadí řádku mezeru přemostilo");
        }
    }

    /// <summary>Kde mají mezery ležet: za popisným blokem a před spojem, kterým začíná druhá
    /// skupina dnů (spoje jsou 1, 1, 2 - hranice je před třetím).</summary>
    private static (float Od, float Do)[] OcekavaneMezery() =>
    [
        (SirkaZastavky, SirkaZastavky + MezeraMm),
        (SirkaZastavky + MezeraMm + 2 * SirkaSpoje, SirkaZastavky + 2 * MezeraMm + 2 * SirkaSpoje),
    ];

    [Fact]
    public void VMezereNeniNic_AniZjedneZeCtyrCestSkladaniSloupcu()
    {
        var prvek = Prvek();
        prvek.MezeraMeziSkupinamiMm = MezeraMm;

        using var bmp = Vykresli(prvek, Data());

        foreach (var (od, do_) in OcekavaneMezery())
        {
            // Půl milimetru od hran: na hranici leží čára skupiny a antialiasing textu
            // ze sousedního sloupce, a to není chyba odsazení.
            // ⚠️ Sloupce spojů jsou proto schválně široké (11 mm): značka návaznosti se kreslí
            // vedle času **bez zmenšování na buňku**, takže v úzkém sloupci přeteče o ~1,5 mm
            // doprava a test by hlásil mezeru, i když je odsazení správné. Je to vlastní,
            // dřívější vada kreslení značek, ne chyba mezer.
            int x = Sloupec(od + 0.5f);
            int konec = Sloupec(do_ - 0.5f);
            for (; x <= konec; x++)
                Assert.True(InkoustVeSloupci(bmp, x) == 0,
                    $"v mezeře ({od:0.#}-{do_:0.#} mm) se kreslí obsah - sloupec {x / MmToPx:0.#} mm "
                    + "má inkoust, takže některá cesta o mezeře neví");
        }
    }

    [Fact]
    public void MezeraPosuneSloupceSpoju_ObsahJeVpravoOdNi()
    {
        var bez = Prvek();
        var s = Prvek();
        s.MezeraMeziSkupinamiMm = MezeraMm;

        using var bmpBez = Vykresli(bez, Data());
        using var bmpS = Vykresli(s, Data());

        // Bez mezery začínají spoje hned za popisným blokem, s mezerou o mezeru dál.
        Assert.True(InkoustVeSloupci(bmpBez, Sloupec(SirkaZastavky + SirkaSpoje / 2)) > 0,
            "bez mezery nemá hned za popisným blokem nic - fixtura nekreslí časy spojů");
        Assert.True(InkoustVeSloupci(bmpS, Sloupec(SirkaZastavky + MezeraMm + SirkaSpoje / 2)) > 0,
            "s mezerou nezačíná obsah spojů za mezerou");
    }

    [Fact]
    public void MezeraSeVejdeDoSirkyPrvku_TabulkaNepretece()
    {
        var prvek = Prvek();
        prvek.MezeraMeziSkupinamiMm = MezeraMm;
        var data = Data();

        using var bmp = Vykresli(prvek, data);

        // Poslední sloupec spoje končí na 30 + 2*16 + 3*11 = 95 mm, prvek má 100 mm.
        for (int x = Sloupec(prvek.Width + 0.5f); x < bmp.Width; x++)
            Assert.True(InkoustVeSloupci(bmp, x) == 0,
                $"tabulka přetekla přes pravou hranu prvku - inkoust na {x / MmToPx:0.#} mm");
    }

    [Fact]
    public void NulovaMezera_JeVychoziAKresliJakoDosud()
    {
        var prvek = Prvek();

        Assert.Equal(0f, prvek.MezeraMeziSkupinamiMm);

        using var bmp = Vykresli(prvek, Data());
        // Bez mezery musí být obsazená i místa, kde by mezera byla - jinak by se výchozí stav
        // šablon u zákazníků změnil.
        foreach (var (od, _) in OcekavaneMezery())
            Assert.True(InkoustVeSloupci(bmp, Sloupec(od + 0.5f)) >= 0);
        Assert.True(InkoustVeSloupci(bmp, Sloupec(SirkaZastavky + SirkaSpoje / 2)) > 0,
            "bez mezery se za popisným blokem nekreslí obsah spojů");
    }

    /// <summary>Mezera se bere ze šířky prvku, takže se na řádek vejde méně spojů - u linky, která
    /// byla na hraně, se tím přidá blok pod tabulku (a tím i výška).</summary>
    [Fact]
    public void MezeraUbereMistoSpojum_TakzeMuzePridatBlok()
    {
        var data = Data();
        var uzky = Prvek();
        uzky.Width = SirkaZastavky + 3 * SirkaSpoje + 1f;   // tři spoje se vejdou jen tak tak

        float bezMezery = uzky.CalculateHeightFromData(data);

        uzky.MezeraMeziSkupinamiMm = MezeraMm;
        float sMezerou = uzky.CalculateHeightFromData(data);

        Assert.True(sMezerou > bezMezery,
            $"mezera neubrala místo sloupcům spojů - výška zůstala {bezMezery:0.#} mm, "
            + "takže se dělení na bloky o mezeře nedozvědělo a tabulka přeteče");
    }

    /// <summary>
    /// Sloupec spoje <b>bez pevné šířky</b> (výchozí stav) si dělí zbytek šířky prvku - mezery se
    /// z toho zbytku musí odečíst <b>před</b> dělením, ne po něm.
    /// </summary>
    /// <remarks>
    /// Kdyby se neodečetly, sloupce vyjdou širší, než je místo, blok se do prvku nevejde a dělení
    /// z něj ubere spoje - tabulka se rozřeže na víc bloků, i když je na řádku místa dost.
    /// Nepřeteče, jen se zbytečně natáhne na víc stránek, takže je to vidět na výšce.
    /// </remarks>
    [Fact]
    public void MezeraSeOdecteAzNaKonci_ZbytecnyBlokNevznikne()
    {
        var data = Data();
        var prvek = Prvek();
        prvek.Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Width = null, Visible = true },
        ];

        float bezMezery = prvek.CalculateHeightFromData(data);

        prvek.MezeraMeziSkupinamiMm = MezeraMm;
        float sMezerou = prvek.CalculateHeightFromData(data);

        Assert.Equal(bezMezery, sMezerou, 0.01f);
    }

    /// <summary>
    /// Hranice skupin sloupců musí být vidět i v <b>pásu s nadpisy</b>, tedy od horního okraje
    /// tabulky - ne až o řádek níž.
    /// </summary>
    /// <remarks>
    /// Nález Jan 27. 8.: „v té první subtabulce Směr jízdy to má pravý okraj vykreslený, ale levý
    /// ne". Levý okraj je vnější hrana a tu kreslí rámeček prvku (ve vzoru 940 vypnutý) - ale
    /// změřením se ukázalo, že hranice skupiny opravdu začíná až <b>pod</b> pásem: čára běžela
    /// y 51-168 ze 169 px, pás nad ní žádné svislé čáry neměl. Vypnuly se tam celé, aby
    /// „nerozsekaly nadpis přes několik sloupců spojů" - jenže v pásu je <b>jedna buňka na
    /// skupinu</b>, takže čára mezi nimi padne přesně na hranici skupiny a žádný nadpis neprotne.
    /// <para>⚠️ Platí i <b>bez</b> mezer: mezi pásy „Pracovní dny" a „Soboty" se čára objeví taky
    /// (rozhodnutí Jan 27. 8.) - je to změna vzhledu existujících šablon, proto vlastní test.</para>
    /// </remarks>
    [Fact]
    public void HraniceSkupin_JdeOdVrchuTabulky_TedyIVPasuSNadpisy()
    {
        var sMezerou = Prvek();
        sMezerou.Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FF000000", Width = 0.3f };
        sMezerou.MezeraMeziSkupinamiMm = MezeraMm;

        using var bmp = Vykresli(sMezerou, Data());

        // hrana popisného bloku (= levá hrana první mezery) a hrana mezi skupinami dnů
        foreach (float hranaMm in new[] { SirkaZastavky, SirkaZastavky + MezeraMm + 2 * SirkaSpoje })
        {
            float vrch = PrvniInkoustMm(bmp, Sloupec(hranaMm));
            Assert.True(vrch >= 0 && vrch < 1f,
                $"hranice skupiny na {hranaMm:0.#} mm nezačíná u horního okraje tabulky "
                + $"(první inkoust {vrch:0.#} mm) - v pásu s nadpisy se nekreslí");

            // Na hraně skupiny má být jedna čára, ne dvě kolem mezery - i v pásu.
            float zaMezerou = PrvniInkoustMm(bmp, Sloupec(hranaMm + MezeraMm));
            Assert.True(zaMezerou < 0 || zaMezerou >= 1f,
                $"za mezerou na {hranaMm + MezeraMm:0.#} mm je v pásu druhá čára "
                + $"(inkoust už na {zaMezerou:0.#} mm) - na hraně skupiny mají být dvě čáry vedle sebe");
        }
    }

    /// <summary>Bez mezer platí totéž - hranice mezi skupinami dnů je vidět i v pásu.</summary>
    [Fact]
    public void HraniceSkupinDnu_JdeOdVrchu_IBezMezer()
    {
        var prvek = Prvek();
        prvek.Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FF000000", Width = 0.3f };

        using var bmp = Vykresli(prvek, Data());

        float hranaMm = SirkaZastavky + 2 * SirkaSpoje;   // spoje jsou 1, 1, 2
        float vrch = PrvniInkoustMm(bmp, Sloupec(hranaMm));
        Assert.True(vrch >= 0 && vrch < 1f,
            $"hranice skupin dnů na {hranaMm:0.#} mm nezačíná u horního okraje (první inkoust "
            + $"{vrch:0.#} mm) - pás s nadpisy zůstal bez svislých čar");
    }

    /// <summary>
    /// Za posledním sloupcem tabulky už nesmí být <b>nic</b> - ani vodorovná čára, ani podbarvení.
    /// </summary>
    /// <remarks>
    /// Nález Jan 27. 8.: „vpravo, když skončí ta tabulka, tak tam pak takhle pokračují do pravého
    /// okraje stránky ty vodorovné čáry, ale žádné vertikální". Není to záměr - vodorovné čáry
    /// a pozadí řádku se kreslily přes <b>celou šířku prvku</b>, kdežto buňky jen po konec tabulky.
    /// Volné místo vpravo <b>záměr je</b> (blok nemusí vyjít na celou šířku, dorovnání prázdnými
    /// sloupci je samostatná volba, #8372) - ale má být opravdu volné, ne prošpikované čarami.
    /// </remarks>
    [Fact]
    public void ZaPoslednimSloupcem_NeniAniCara()
    {
        var prvek = Prvek();
        prvek.Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FF000000", Width = 0.3f };
        prvek.MezeraMeziSkupinamiMm = MezeraMm;

        var data = DataSPripojnymRadkem();
        using var bmp = Vykresli(prvek, data);

        // Kam tabulka sahá, si řekne sám prvek - sloupec se značkou je širší, takže konstanta by
        // tady lhala. Test tím zároveň hlídá, že se kreslí jen tam, kam si rozvrh řekl.
        float konecTabulky = prvek.Rozvrzeni(data).Max(b => b.SirkaMm);
        for (int x = Sloupec(konecTabulky + 1f); x < bmp.Width; x++)
            Assert.True(InkoustVeSloupci(bmp, x) == 0,
                $"za koncem tabulky ({konecTabulky:0.#} mm) je inkoust na {x / MmToPx:0.#} mm - "
                + "vodorovná čára nebo pozadí řádku pokračuje do volného místa vpravo");
    }

    /// <summary>
    /// Sloupec, ve kterém je někde <b>značka návaznosti</b> vedle času, je o ni <b>širší</b> - a jen on.
    /// </summary>
    /// <remarks>
    /// Parita se starým modulem, doložená čtením legacy (rozhodnutí Jan 27. 8.): sloupec spoje má
    /// v <c>JRapiLinPrtWin.mac:1793</c> čtyři šířky a vybírá se podle největšího počtu značek
    /// ve sloupci (<c>:1769</c>, slot „znacky spoje" viz <c>:2611</c>). Text se tedy
    /// <b>nezmenšuje</b>, dělá se místo - a jen v tom sloupci, který značku nese.
    /// <para>Nález Jan 27. 8.: „značka + minuta pak tu minutu posune ven mimo šířku sloupce" -
    /// minimální šířka se počítala jen z „00:00".</para>
    /// </remarks>
    [Fact]
    public void SloupecSeZnackou_JeSirsiNezSloupecBezNi()
    {
        var prvek = Prvek();
        // Užší, než kolik si žádá čas se značkou - jinak by se značka vešla do volného místa
        // a sloupec by se rozšiřovat nemusel (od 22. 9.).
        prvek.SirkaSloupceSpojeMm = 6f;
        var data = Data();   // značka návaznosti je na posledním ze tří spojů

        var sirky = prvek.Rozvrzeni(data)[0].SirkySloupcu;

        Assert.True(sirky[^1] > sirky[0] + 0.1f,
            $"sloupec se značkou ({sirky[^1]:0.##} mm) není širší než sloupec bez ní "
            + $"({sirky[0]:0.##} mm) - značka pak z buňky vyleze");
    }

    /// <summary>
    /// Táž linka, ale poslední sloupec je prázdný až na značku vedle času - spoj nemá číslo
    /// a jeho skupina nemá nadpis.
    /// </summary>
    private static DataContext DataJenZnackaNavaznosti() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("8:00", "8:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("9:00", "9:40"))),
            RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "2"),
                ("CASY", new JsonArray("", "10:40")),
                ("PRIPOJE", RenderSnapshot.Nested([("TYP", "vycka"), ("PORADI", "2"), ("CAS", "10:40")]))),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", ""), ("PORADI", "2")),
        ]));

    /// <summary>
    /// Totéž při <b>výchozím</b> nastavení, tedy se stejně širokými sloupci spojů. Musí to být
    /// vlastní test: geometrie zbytku souboru je připíchnutá k legacy rozvržení, kdežto tady se
    /// odsazení odvozuje ze skutečných šířek, které prvek spočítal.
    /// </summary>
    /// <remarks>
    /// ⚠️ Značka se kreslí ručně mimo buňky. Kdyby si šířku sloupce brala odjinud než rozvržení,
    /// při jednotné šířce by skončila ve sloupci cizího spoje - a testy pod legacy nastavením
    /// by o tom nevěděly.
    /// </remarks>
    [Fact]
    public void ZnackaNavaznosti_JeVeSloupciSvehoSpoje_JednotnaSirka()
    {
        var prvek = Prvek();
        prvek.JednotnaSirkaSpoju = true;
        prvek.MezeraMeziSkupinamiMm = MezeraMm;
        // Širší než u ostatních testů: jednotná šířka sloupce je o značku větší, takže by se
        // do stovky milimetrů všechny tři spoje nevešly a rozřezaly by se do bloků.
        prvek.Width = 130;
        var data = DataJenZnackaNavaznosti();

        using var bmp = Vykresli(prvek, data);

        // Sloupec třetího spoje: zastávky + dvě mezery (za popisným blokem a před druhou skupinou)
        // + dva sloupce spojů o skutečné, tedy jednotné šířce.
        var sirky = prvek.Rozvrzeni(data)[0].SirkySloupcu;
        Assert.Equal(3, sirky.Count);
        Assert.Single(sirky.Distinct());
        float odMm = SirkaZastavky + 2 * MezeraMm + sirky[0] + sirky[1];
        float doMm = odMm + sirky[2];

        int inkoust = 0;
        for (int x = Sloupec(odMm + 0.5f); x <= Sloupec(doMm - 0.5f); x++)
            inkoust += InkoustVeSloupci(bmp, x);

        Assert.True(inkoust > 0,
            $"ve sloupci spoje se značkou návaznosti ({odMm:0.#}-{doMm:0.#} mm) není nic - "
            + "značka s časem se při jednotné šířce nakreslila jinam");

        // ⚠️ Samotné „něco tam je" nestačí: sloupec je širší než posun, který vznikne, když se
        // odsazení počítá ze základu místo ze skutečných šířek - značka pak sedí vedle středu,
        // ale pořád uvnitř sloupce. Proto se hlídá **vystředění** (odhaleno mutací).
        int prvni = -1, posledni = -1;
        for (int x = Sloupec(odMm); x <= Sloupec(doMm); x++)
        {
            if (InkoustVeSloupci(bmp, x) == 0) continue;
            if (prvni < 0) prvni = x;
            posledni = x;
        }
        float vlevoMm = prvni / MmToPx - odMm;
        float vpravoMm = doMm - posledni / MmToPx;
        Assert.True(MathF.Abs(vlevoMm - vpravoMm) < 0.5f,
            $"značka s časem není ve sloupci vystředěná (vlevo {vlevoMm:0.##} mm, vpravo "
            + $"{vpravoMm:0.##} mm) - odsazení sloupce se počítá z jiné šířky, než jakou sloupec má");
    }

    /// <summary>Značka návaznosti se kreslí ručně mimo buňky, takže o mezerách i o vlastní šířce
    /// sloupce musí vědět sama - jinak skončí ve sloupci cizího spoje.</summary>
    [Fact]
    public void ZnackaNavaznosti_JeVeSloupciSvehoSpoje()
    {
        var prvek = Prvek();
        prvek.MezeraMeziSkupinamiMm = MezeraMm;
        var data = DataJenZnackaNavaznosti();

        using var bmp = Vykresli(prvek, data);

        float odMm = SirkaZastavky + 2 * MezeraMm + 2 * SirkaSpoje;
        float doMm = prvek.Rozvrzeni(data).Max(b => b.SirkaMm);
        int inkoust = 0;
        for (int x = Sloupec(odMm + 0.5f); x <= Sloupec(doMm - 0.5f); x++)
            inkoust += InkoustVeSloupci(bmp, x);

        Assert.True(inkoust > 0,
            $"ve sloupci spoje se značkou návaznosti ({odMm:0.#}-{doMm:0.#} mm) není nic - "
            + "značka s časem se nakreslila jinam");

        // Sloupec je prázdný až na tuhle jednu buňku, takže její inkoust musí být ve sloupci
        // vystředěný. Kdyby se značka kreslila do rámu jiné šířky než sám sloupec, sedí vlevo.
        int prvni = -1, posledni = -1;
        for (int x = Sloupec(odMm); x <= Sloupec(doMm); x++)
        {
            if (InkoustVeSloupci(bmp, x) == 0) continue;
            if (prvni < 0) prvni = x;
            posledni = x;
        }
        float vlevoMm = prvni / MmToPx - odMm;
        float vpravoMm = doMm - posledni / MmToPx;
        Assert.True(MathF.Abs(vlevoMm - vpravoMm) < 0.5f,
            $"značka s časem není ve sloupci vystředěná (vlevo {vlevoMm:0.##} mm, vpravo "
            + $"{vpravoMm:0.##} mm) - kreslí se do rámu jiné šířky, než jakou má sloupec");
    }

    /// <summary>
    /// U <b>automatické</b> šířky sloupce spoje se místo na značku musí odečíst ze zbytku šířky -
    /// jinak vyjdou sloupce širší, než je místo, a tabulka přeteče přes hranu prvku.
    /// </summary>
    [Fact]
    public void MistoNaZnacku_SeOdectePredRozdelenimSirky()
    {
        var prvek = Prvek();
        prvek.MezeraMeziSkupinamiMm = MezeraMm;
        prvek.Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Width = null,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ];
        var data = Data();

        float sirkaBloku = prvek.Rozvrzeni(data).Max(b => b.SirkaMm);
        Assert.True(sirkaBloku <= prvek.Width + 0.01f,
            $"blok je široký {sirkaBloku:0.##} mm, prvek jen {prvek.Width:0.##} mm - místo na značku "
            + "se neodečetlo ze zbytku šířky");

        using var bmp = Vykresli(prvek, data);
        for (int x = Sloupec(prvek.Width + 0.5f); x < bmp.Width; x++)
            Assert.True(InkoustVeSloupci(bmp, x) == 0,
                $"tabulka přetekla přes hranu prvku - inkoust na {x / MmToPx:0.#} mm");
    }

    /// <summary>
    /// Pás s nadpisy musí sahat až na <b>konec tabulky</b> - jeho sloučené buňky se skládají
    /// z týchž sloupců jako řádky pod ním.
    /// </summary>
    /// <remarks>
    /// Kdyby pás počítal šířku skupiny z jednotné šířky sloupce, byl by u linky se značkou kratší
    /// než tabulka a hranice skupin by v něm ležely jinde než v datech pod ním. Měří se
    /// <b>podbarvení</b> pásu, ne inkoust - je to jediná věc, která na tom místě má být.
    /// </remarks>
    [Fact]
    public void PasSNadpisy_SahaAzNaKonecTabulky()
    {
        var prvek = Prvek();
        prvek.MezeraMeziSkupinamiMm = MezeraMm;
        var data = Data();

        float konecTabulky = prvek.Rozvrzeni(data).Max(b => b.SirkaMm);
        using var bmp = Vykresli(prvek, data);

        Assert.True(PodbarveneVPasu(bmp, Sloupec(konecTabulky - 1f)) > 0,
            $"na konci tabulky ({konecTabulky:0.##} mm) není podbarvení pásu - pás je kratší než "
            + "tabulka, takže hranice skupin v něm neleží nad hranicemi sloupců");
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static int Sloupec(float mm) => (int)(mm * MmToPx);

    /// <summary>
    /// Y prvního tmavého pixelu na hraně sloupce (mm od horní hrany), -1 když tam nic není.
    /// </summary>
    /// <remarks>
    /// ⚠️ Hledá v <b>okně ±2 px</b>, ne v jednom sloupci pixelů: svislou čáru kreslí
    /// <c>ColumnRenderer</c> <b>před</b> buňkou, takže pozadí buňky vpravo od ní překryje její
    /// pravou polovinu. Vzorkování přesně na hraně tak může minout půlku čáry a test by hlásil
    /// „čára chybí", i když se kreslí.
    /// </remarks>
    private static float PrvniInkoustMm(SKBitmap bmp, int x)
    {
        float nejvyse = -1f;
        for (int dx = -2; dx <= 2; dx++)
        {
            int sx = x + dx;
            if (sx < 0 || sx >= bmp.Width) continue;
            for (int y = 0; y < bmp.Height; y++)
            {
                var c = bmp.GetPixel(sx, y);
                if (c.Red >= 100 || c.Green >= 100 || c.Blue >= 100) continue;
                if (nejvyse < 0 || y / MmToPx < nejvyse) nejvyse = y / MmToPx;
                break;
            }
        }
        return nejvyse;
    }

    /// <summary>
    /// Počet podbarvených pixelů <b>v pásu s nadpisy</b>, tedy v horním řádku tabulky.
    /// </summary>
    /// <remarks>
    /// ⚠️ Měřit celý svislý pás nestačí: podbarvení má i řádek čísel spojů pod ním, a ten má
    /// šířky sloupců správně, takže by test prošel i s vadným pásem. Rozsah 0,2-1 mm je bezpečně
    /// uvnitř prvního řádku pro každou rozumnou velikost písma.
    /// </remarks>
    private static int PodbarveneVPasu(SKBitmap bmp, int x) =>
        PodbarveneVeSloupci(bmp, x, (int)(0.2f * MmToPx), (int)(1.0f * MmToPx));

    /// <summary>Počet podbarvených pixelů ve svislém pásu - tedy ani bílá stránka, ani inkoust.</summary>
    private static int PodbarveneVeSloupci(SKBitmap bmp, int x, int odY = 0, int doY = int.MaxValue)
    {
        if (x < 0 || x >= bmp.Width) return 0;
        int n = 0;
        for (int y = Math.Max(0, odY); y < bmp.Height && y <= doY; y++)
        {
            var c = bmp.GetPixel(x, y);
            bool bila = c.Red > 240 && c.Green > 240 && c.Blue > 240;
            bool inkoust = c.Red < 100 && c.Green < 100 && c.Blue < 100;
            if (!bila && !inkoust) n++;
        }
        return n;
    }

    /// <summary>Počet tmavých pixelů ve svislém pásu obrázku - inkoust textu a značek.</summary>
    private static int InkoustVeSloupci(SKBitmap bmp, int x)
    {
        if (x < 0 || x >= bmp.Width) return 0;
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            var c = bmp.GetPixel(x, y);
            if (c.Red < 100 && c.Green < 100 && c.Blue < 100) n++;
        }
        return n;
    }

    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 10f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 10f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
