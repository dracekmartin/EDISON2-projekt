using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Obousměrný tisk (`L6b`) - spoje opačného směru <b>nalevo</b> od zastávek.
/// </summary>
/// <remarks>
/// Data přicházejí jako <b>jedna</b> stránka, kde každý spoj nese `SMER`; o tom, která strana je
/// která, rozhoduje prvek (požadavek Jan 28. 8.: layout drží klient, ne Caché).
/// <para>⚠️ <b>Nalevo jde směr, jehož pořadí sleduje seznam zastávek</b> (jeho časy jdou odshora
/// dolů). Doloženo <b>tiskem obousměrné linky ze starého modulu</b> 28. 8.: vlevo spoj s časy
/// 07:33 → 07:38 a značkou „od" u první zastávky, vpravo spoj 07:53 → 07:39 se značkou „př".
/// Do té chvíle jsme to měli obráceně - a obě strany byly „rozumné", takže to bez papíru nešlo
/// rozhodnout.</para>
/// <para>⚠️ Fixtury jsou schválně <b>skoro prázdné</b>: v obrázku je vždy jen jedna věc, takže
/// „kde je inkoust" znamená „kde je ta věc". První verze těchto testů měřila jen <b>množství</b>
/// inkoustu a <b>neprošla ani jedna ze čtyř mutací</b> - při dvou spojích má tabulka stejnou šířku,
/// ať jsou sloupce vedle sebe, nebo na obou stranách zastávek.</para>
/// </remarks>
public class ObousmernyTiskTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 30f;
    private const float SirkaSpoje = 10f;

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        HighlightOddTc = false,
        ShowGroupHeaders = false,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.2f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>
    /// Fixtura, ve které je vidět <b>jen jedna</b> věc: buď názvy zastávek, nebo časy jednoho směru.
    /// </summary>
    private static DataContext Data(bool obousmerne, bool nazvyZastavek, bool casyTam, bool casyZpet)
    {
        string Nazev(string n) => nazvyZastavek ? n : "";
        JsonArray Casy(bool ano, string a, string b, string c) =>
            ano ? new JsonArray(a, b, c) : new JsonArray("", "", "");

        var spoje = new List<DataContext>
        {
            RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", Casy(casyTam, "8:00", "8:20", "8:40"))),
        };
        if (obousmerne)
            spoje.Add(RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "Z"),
                ("CASY", Casy(casyZpet, "9:40", "9:20", "9:00"))));

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Kroměříž")), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Hulín")), ("PORADI", "2")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Přerov")), ("PORADI", "3")),
            ]),
            ("SPOJE", spoje),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }

    /// <summary>
    /// Zastávky se zrcadleným blokem <b>posunou doprava</b> přesně o šířku jeho sloupce.
    /// </summary>
    /// <remarks>
    /// V obrázku jsou jen názvy zastávek (časy i čísla spojů prázdné), takže první inkoust = levá
    /// hrana sloupce zastávek. Bez zrcadlení začíná na nule, se zrcadlením za jeho sloupcem.
    /// </remarks>
    [Fact]
    public void ZastavkySePosunouDopravaOSirkuZrcadlenehoSloupce()
    {
        using var jednosmerne = Vykresli(Prvek(),
            Data(obousmerne: false, nazvyZastavek: true, casyTam: false, casyZpet: false));
        using var obousmerne = Vykresli(Prvek(),
            Data(obousmerne: true, nazvyZastavek: true, casyTam: false, casyZpet: false));

        float bez = PrvniInkoustXMm(jednosmerne);
        float se = PrvniInkoustXMm(obousmerne);

        Assert.True(bez < 1.5f, $"bez zrcadlení mají zastávky začínat u nuly, začínají na {bez:0.##} mm");
        Assert.True(se > SirkaSpoje - 0.5f,
            $"se zrcadlením mají zastávky začínat za jeho sloupcem ({SirkaSpoje} mm), začínají na {se:0.##} mm");
    }

    /// <summary>
    /// Směr, jehož pořadí sleduje seznam zastávek, se kreslí <b>nalevo</b> - jako v legacy.
    /// </summary>
    /// <remarks>
    /// V obrázku jsou jen jeho časy (názvy zastávek i druhý směr prázdné), takže veškerý inkoust
    /// musí ležet v levém pásu.
    /// </remarks>
    [Fact]
    public void SmerPodlePoradiZastavek_JeNalevo()
    {
        using var bmp = Vykresli(Prvek(),
            Data(obousmerne: true, nazvyZastavek: false, casyTam: true, casyZpet: false));

        float prvni = PrvniInkoustXMm(bmp);
        float posledni = PosledniInkoustXMm(bmp);

        Assert.True(prvni >= 0f, "časy se nenakreslily vůbec");
        Assert.True(posledni < SirkaSpoje + 0.5f,
            $"časy sahají až na {posledni:0.##} mm - nejsou v levém pásu (0-{SirkaSpoje} mm)");
    }

    /// <summary>
    /// Opačný směr zůstává <b>vpravo</b> za zastávkami - kontrola druhé strany.
    /// </summary>
    /// <remarks>
    /// ⚠️ Názvy zastávek tu být **musí**: s prázdnými by sloupec zastávek zmizel („prázdný sloupec
    /// se netiskne") a s ním i celé odsazení, které se měří. Na to naletěla první verze testu.
    /// </remarks>
    [Fact]
    public void OpacnySmer_ZustavaVpravo()
    {
        using var bmp = Vykresli(Prvek(),
            Data(obousmerne: true, nazvyZastavek: true, casyTam: false, casyZpet: true));

        float posledni = PosledniInkoustXMm(bmp);

        Assert.True(posledni > SirkaSpoje + SirkaZastavky,
            $"nejpravější inkoust je na {posledni:0.##} mm - časy opačného směru nejsou za zastávkami "
            + $"(ty končí na {SirkaSpoje + SirkaZastavky} mm)");
    }

    /// <summary>
    /// Jednosměrná stránka se <b>nezmění</b> - `SMER` u spoje posílá server vždy, i pro ni.
    /// </summary>
    [Fact]
    public void JednosmernaStranka_KresliJakoDosud()
    {
        var bezSmeru = RenderSnapshot.Data(
            ("ZASTAVKY", [RenderSnapshot.Row(("NAZEV_ZASTAVKY", "A"), ("PORADI", "1"))]),
            ("SPOJE", [RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("8:00")))]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));
        var seSmerem = RenderSnapshot.Data(
            ("ZASTAVKY", [RenderSnapshot.Row(("NAZEV_ZASTAVKY", "A"), ("PORADI", "1"))]),
            ("SPOJE", [RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", new JsonArray("8:00")))]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

        using var a = Vykresli(Prvek(), bezSmeru);
        using var b = Vykresli(Prvek(), seSmerem);

        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    Assert.Fail($"jednosměrná stránka se změnila kvůli poli SMER ({x / MmToPx:0.#}, {y / MmToPx:0.#} mm)");
    }

    /// <summary>
    /// Blok musí být širší o zrcadlený sloupec - jinak by se do šířky prvku počítala jen jedna strana
    /// a tabulka by přetekla.
    /// </summary>
    [Fact]
    public void SirkaBloku_PocitaObeStrany()
    {
        var prvek = Prvek();
        var data = Data(obousmerne: true, nazvyZastavek: true, casyTam: true, casyZpet: true);

        float sirka = prvek.Rozvrzeni(data).Max(b => b.SirkaMm);

        Assert.Equal(SirkaSpoje + SirkaZastavky + SirkaSpoje, sirka, 0.01f);
    }

    /// <summary>
    /// Šipka směru je v popisném bloku, takže se musí posunout <b>s ním</b> - ne zůstat na nule.
    /// </summary>
    /// <remarks>
    /// ⚠️ Tohle je jediná cesta, jak se pozná posun popisného bloku: buňky si pozici počítá
    /// `ColumnRenderer` sekvenčně, kdežto šipka a piktogram zastávky se kreslí přes souřadnici
    /// z mřížky. Bez tohohle testu mutace „popisný blok se neposune" prošla.
    /// </remarks>
    [Fact]
    public void SipkaSmeru_SePosuneSPopisnymBlokem()
    {
        var prvek = Prvek();
        prvek.Columns.Insert(0, new StopColumn
        {
            Key = ColumnKeys.Arrow, Header = "", Width = 4f, Visible = true,
        });

        // Jen šipka a názvy zastávek; časy prázdné, ať je inkoust jen tam, kde má být.
        using var bmp = Vykresli(prvek,
            Data(obousmerne: true, nazvyZastavek: true, casyTam: false, casyZpet: false));

        float prvni = PrvniInkoustXMm(bmp);
        Assert.True(prvni > SirkaSpoje - 0.5f,
            $"první inkoust je na {prvni:0.##} mm - popisný blok (a s ním šipka) se neposunul "
            + $"za zrcadlený sloupec ({SirkaSpoje} mm)");
    }

    /// <summary>
    /// Do šířky prvku se musí vejít <b>obě</b> strany - jinak tabulka přeteče přes jeho hranu.
    /// </summary>
    /// <remarks>
    /// Prvek je schválně těsný: na jednu stranu by se dva sloty vešly, na obě ne. Kdyby dělení
    /// počítalo jen jednu stranu, vyrobí jeden blok o čtyřech sloupcích spojů a ten se nevejde.
    /// </remarks>
    [Fact]
    public void DeleniNaBloky_PocitaSloupceObouStran()
    {
        var prvek = Prvek();
        prvek.Width = SirkaZastavky + 2 * SirkaSpoje;   // 50 mm: jeden slot na obě strany, ne dva

        var data = RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
            ]),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
                    ("CASY", new JsonArray("8:00", "8:40"))),
                RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"), ("SMER", "T"),
                    ("CASY", new JsonArray("9:00", "9:40"))),
                RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"), ("SMER", "Z"),
                    ("CASY", new JsonArray("10:40", "10:00"))),
                RenderSnapshot.Row(("CISLO_SPOJE", "4"), ("SKUPINA", "1"), ("SMER", "Z"),
                    ("CASY", new JsonArray("11:40", "11:00"))),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));

        float nejsirsi = prvek.Rozvrzeni(data).Max(b => b.SirkaMm);
        Assert.True(nejsirsi <= prvek.Width + 0.01f,
            $"blok je široký {nejsirsi:0.##} mm, prvek jen {prvek.Width:0.##} mm - dělení nepočítá "
            + "sloupce obou stran");

        using var bmp = Vykresli(prvek, data);
        for (int x = (int)((prvek.Width + 0.5f) * MmToPx); x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeInkoust(bmp.GetPixel(x, y)))
                    Assert.Fail($"tabulka přetekla přes hranu prvku - inkoust na {x / MmToPx:0.#} mm");
    }

    /// <summary>
    /// Dorovnání prázdnými sloupci musí doplnit i <b>zrcadlenou</b> stranu - jinak se v posledním
    /// bloku posunou zastávky doleva a svisle nesedí na bloky nad sebou.
    /// </summary>
    /// <remarks>
    /// Prvek je úzký schválně: tři sloty se do něj nevejdou, takže vznikne blok o dvou a blok
    /// o jednom. V obrázku jsou jen názvy zastávek, takže „první inkoust v řádku" je levá hrana
    /// jejich sloupce - a ta musí být v obou blocích stejná.
    /// </remarks>
    [Fact]
    public void Dorovnani_DoplniIZrcadlenouStranu()
    {
        var prvek = Prvek();
        prvek.Width = SirkaZastavky + 2 * 2 * SirkaSpoje;   // 70 mm = dva sloty, ne tři
        prvek.PadEmptyColumns = true;

        using var bmp = Vykresli(prvek, DataTriSloty(nazvyZastavek: true));

        // ⚠️ Porovnává se **nejlevější hrana v bloku**, ne hrana každého řádku: první a poslední
        // řádek pixelů písmene začíná jinde než jeho střed (tvar glyfu), takže „rozptyl přes
        // všechny řádky" by hlásil pár desetin i na dokonale zarovnané tabulce.
        float prvni = LevaHranaMm(bmp, 0, bmp.Height / 2);
        float druhy = LevaHranaMm(bmp, bmp.Height / 2, bmp.Height);

        Assert.True(prvni >= 0f && druhy >= 0f, "jeden z bloků se nenakreslil");
        Assert.True(MathF.Abs(prvni - druhy) < 1f,
            $"zastávky začínají v prvním bloku na {prvni:0.##} mm a ve druhém na {druhy:0.##} mm "
            + "- zrcadlená strana se nedorovnala");
    }

    /// <summary>
    /// Dorovnání stojí na <b>vnější</b> hraně - sloupce spojů zůstávají u zastávek.
    /// </summary>
    /// <remarks>
    /// V posledním bloku má zrcadlená strana jediný spoj a jeden sloupec dorovnání. Jeho časy proto
    /// musí ležet v pásu **hned vlevo od zastávek**, ne u levého okraje tabulky - jinak by mezi
    /// spoji a zastávkami zůstala prázdná mezera. Tak to dělá i pravá strana (dorovnává za spoji).
    /// </remarks>
    [Fact]
    public void Dorovnani_JeNaVnejsiHrane()
    {
        var prvek = Prvek();
        prvek.Width = SirkaZastavky + 2 * 2 * SirkaSpoje;
        prvek.PadEmptyColumns = true;

        using var bmp = Vykresli(prvek, DataTriSloty(nazvyZastavek: true, casyPosledniZrcadlene: true));

        // druhý blok, jen pás nalevo od zastávek (dva sloupce spojů = 2 × SirkaSpoje)
        float hrana = LevaHranaMm(bmp, bmp.Height / 2, bmp.Height, 2 * SirkaSpoje - 0.5f);

        Assert.True(hrana >= 0f, "časy zrcadleného spoje se v druhém bloku nenakreslily");
        Assert.True(hrana > SirkaSpoje - 0.5f,
            $"časy začínají na {hrana:0.##} mm - jsou v dorovnávacím sloupci u levého okraje "
            + $"místo v tom u zastávek (ten začíná na {SirkaSpoje} mm)");
    }

    /// <summary>
    /// Na jednosměrné stránce dorovnání <b>nesmí</b> vyrobit prázdný blok nalevo.
    /// </summary>
    /// <remarks>
    /// Kdyby se počítalo „chybějící sloupce = sloty mínus zrcadlené spoje" i bez zrcadlení, vyšlo by
    /// dorovnání na celý slot a tabulka by se odsunula od levé hrany prvku. Zvenčí by to vypadalo
    /// jako obrovský odstup, a **žádný jiný test to nechytí** - měří se šířka doprava, a ta při
    /// posunu jen naroste.
    /// </remarks>
    [Fact]
    public void Dorovnani_JednosmerneNevyrabiPrazdnyBlokNalevo()
    {
        var prvek = Prvek();
        prvek.Width = SirkaZastavky + 2 * SirkaSpoje;   // 50 mm = dva spoje na blok ze tří
        prvek.PadEmptyColumns = true;

        using var bmp = Vykresli(prvek, DataTriSloty(nazvyZastavek: true, obousmerne: false));

        float hrana = LevaHranaMm(bmp, 0, bmp.Height);
        Assert.True(hrana >= 0f, "tabulka se nenakreslila");
        Assert.True(hrana < 1.5f,
            $"zastávky začínají na {hrana:0.##} mm - dorovnání odsunulo tabulku od levé hrany");
    }

    /// <summary>
    /// S dorovnáním končí tabulka <b>na hraně prvku</b>, ne kousek před ní.
    /// </summary>
    /// <remarks>
    /// Počet prázdných sloupců vychází z celých sloupců, takže po nich zbude kus šířky. Bez
    /// rozpuštění do prázdných je za posledním sloupcem vidět useknutý pruh a vypadá to jako chyba
    /// sazby (nález Jan 21. 9.). Šířka prvku je schválně nesoudělná s šířkou sloupce.
    /// </remarks>
    [Fact]
    public void Dorovnani_KonciNaHranePrvku()
    {
        var prvek = Prvek();
        prvek.Width = SirkaZastavky + (3.4f * SirkaSpoje);
        prvek.PadEmptyColumns = true;
        // Měří se podle mřížky, takže musí být vidět - fixtura ji jinak kreslí bíle.
        prvek.Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FF000000", Width = 0.2f };

        using var bmp = Vykresli(prvek, DataTriSloty(nazvyZastavek: true, obousmerne: false));

        float konec = PosledniInkoustXMm(bmp);
        Assert.True(konec > 0f, "tabulka se nenakreslila");
        Assert.True(MathF.Abs(konec - prvek.Width) < 1f,
            $"tabulka končí na {konec:0.##} mm, prvek na {prvek.Width:0.##} mm "
            + "- zbytek šířky se nerozpustil do prázdných sloupců");
    }

    /// <summary>Tři sloty v obou směrech - pro dělení na bloky.</summary>
    private static DataContext DataTriSloty(bool nazvyZastavek, bool casyPosledniZrcadlene = false,
        bool obousmerne = true)
    {
        string Nazev(string n) => nazvyZastavek ? n : "";
        var spoje = new List<DataContext>();
        for (int i = 1; i <= 3; i++)
        {
            // Zrcadlená (levá) strana nese směr prvního spoje - tedy „T".
            bool casy = casyPosledniZrcadlene && i == 3;
            spoje.Add(RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", casy ? new JsonArray("8:00", "8:20", "8:40") : new JsonArray("", "", ""))));
            if (obousmerne)
                spoje.Add(RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "Z"),
                    ("CASY", new JsonArray("", "", ""))));
        }
        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Kroměříž")), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Hulín")), ("PORADI", "2")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Přerov")), ("PORADI", "3")),
            ]),
            ("SPOJE", spoje),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    /// <summary>Nejlevější inkoust (mm) v pásu řádků (volitelně jen do `doXMm`); -1 když nic.</summary>
    private static float LevaHranaMm(SKBitmap bmp, int odY, int doY, float doXMm = float.MaxValue)
    {
        int maxX = doXMm == float.MaxValue ? bmp.Width - 1 : Math.Min(bmp.Width - 1, (int)(doXMm * MmToPx));
        for (int x = 0; x <= maxX; x++)
            for (int y = odY; y < doY && y < bmp.Height; y++)
                if (JeInkoust(bmp.GetPixel(x, y))) return x / MmToPx;
        return -1f;
    }

    /// <summary>X prvního tmavého pixelu (mm), -1 když v obrázku není žádný.</summary>
    private static float PrvniInkoustXMm(SKBitmap bmp)
    {
        for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeInkoust(bmp.GetPixel(x, y))) return x / MmToPx;
        return -1f;
    }

    /// <summary>X posledního tmavého pixelu (mm), -1 když v obrázku není žádný.</summary>
    private static float PosledniInkoustXMm(SKBitmap bmp)
    {
        for (int x = bmp.Width - 1; x >= 0; x--)
            for (int y = 0; y < bmp.Height; y++)
                if (JeInkoust(bmp.GetPixel(x, y))) return x / MmToPx;
        return -1f;
    }

    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 4f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 6f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
