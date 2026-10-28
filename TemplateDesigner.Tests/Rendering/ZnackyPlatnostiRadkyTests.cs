using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Značky platnosti spoje (`L11`) - <b>nejvýš dvě na řádek</b>, zbytek se zalomí pod ně,
/// a negativní značka pod nimi se tiskne <b>inverzně</b>.
/// </summary>
/// <remarks>
/// <para><b>Dělení je mechanické, ne podle významu.</b> Starý modul prochází značky v pevném
/// pořadí a po každé druhé uzavře řádek (`JRapiLinPrtWin.mac:25` - `POCETZNAKUPLATNOSTI 2`,
/// skládání `:3752-3775`). Řádek proto může mít vedle sebe den i vlastnost vozidla - nejsou to
/// významové řádky. Rozliší to jedině <see cref="DeleniJeMechanicke_NePodleVyznamu"/>; ostatní
/// testy by prošly i dělení podle rodin značek.</para>
/// <para><b>Oracle</b> (R6): tisk ze starého modulu na lince 108360 (CB_CSADAD, skupina 1, směr
/// „tam"), viděný a potvrzený Janem 31. 8. Spoj <b>1017</b> má ⚒ + ♿ <b>na jednom řádku</b>,
/// spoje <b>4009</b> a <b>4025</b> mají ⑥ ✝ na prvním a ♿ na druhém. Fixtury níže tuhle dvojici
/// zrcadlí.</para>
/// <para>Fixtury jsou schválně skoro prázdné (čísla spojů i časy prázdné) a mřížka bílá, aby
/// „kde je inkoust" znamenalo „kde je značka". Názvy zastávek prázdné být <b>nesmí</b> - prázdný
/// sloupec se netiskne a posunul by sloupce spojů, které se měří.</para>
/// </remarks>
public class ZnackyPlatnostiRadkyTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 30f;
    private const float SirkaSpoje = 12f;

    /// <summary>Značky spoje 1017 z oracle tisku: pracovní dny + bezbariérové vozidlo.</summary>
    private static readonly string[] Spoj1017 = ["X", "BEV"];

    /// <summary>Značky spoje 4009 z oracle tisku: sobota + svátky + bezbariérové vozidlo.</summary>
    private static readonly string[] Spoj4009 = ["6", "+", "BEV"];

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        ShowGroupHeaders = false,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.2f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>Fixtura o dvou spojích - každý se svou sadou značek platnosti a negativní značkou.</summary>
    private static DataContext Data(
        IEnumerable<string>? znacky1 = null, IEnumerable<string>? znacky2 = null,
        string nz1 = "", string nz2 = "")
    {
        static DataContext Spoj(IEnumerable<string>? znacky, string nz)
        {
            var polozky = new List<(string, object?)>
            {
                ("CISLO_SPOJE", ""),
                ("SKUPINA", "1"),
                ("SMER", "T"),
                ("NZ", nz),
                ("CASY", new JsonArray("", "", "")),
            };
            if (znacky is not null)
            {
                var arr = new JsonArray();
                foreach (var z in znacky) arr.Add(z);
                polozky.Add(("PLATNOST", arr));
            }
            return RenderSnapshot.Row([.. polozky]);
        }

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
            ]),
            ("SPOJE", [Spoj(znacky1, nz1), Spoj(znacky2, nz2)]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }

    /// <summary>Přírůstek výšky za jeden řádek platnosti - měří se, ať test nemusí znát výšku řádku.</summary>
    private static float VyskaRadkuPlatnosti()
    {
        float dva = Prvek().CalculateHeightFromData(Data(Spoj1017));
        float tri = Prvek().CalculateHeightFromData(Data(Spoj4009));
        return tri - dva;
    }

    /// <summary>Dvě značky se vejdou na jeden řádek - tabulka je stejně vysoká jako s jednou.</summary>
    [Fact]
    public void DveZnacky_ZustanouNaJednomRadku()
    {
        float jedna = Prvek().CalculateHeightFromData(Data(["X"]));
        float dve = Prvek().CalculateHeightFromData(Data(Spoj1017));

        Assert.Equal(jedna, dve, 0.001f);
    }

    /// <summary>Třetí značka zalomí na druhý řádek, pátá na třetí - přesně po dvou.</summary>
    /// <remarks>
    /// Kdyby byl strop jinde než na dvou (nebo se nezalamovalo vůbec), přírůstky nevyjdou:
    /// tři značky = jeden řádek navíc, pět značek = dva řádky navíc.
    /// </remarks>
    [Fact]
    public void ZalomeniJdePoDvou()
    {
        float radek = VyskaRadkuPlatnosti();
        Assert.True(radek > 0.5f, $"třetí značka nepřidala řádek, přírůstek {radek:0.###} mm");

        float dve = Prvek().CalculateHeightFromData(Data(Spoj1017));
        float ctyri = Prvek().CalculateHeightFromData(Data(["X", "BEV", "6", "+"]));
        float pet = Prvek().CalculateHeightFromData(Data(["X", "BEV", "6", "+", "R"]));

        Assert.Equal(dve + radek, ctyri, 0.05f);
        Assert.Equal(dve + 2 * radek, pet, 0.05f);
    }

    /// <summary>Řádků platnosti je tolik, kolik jich potřebuje <b>nejbohatší</b> spoj.</summary>
    [Fact]
    public void RadkuJeTolik_KolikPotrebujeNejbohatsiSpoj()
    {
        float oba3 = Prvek().CalculateHeightFromData(Data(Spoj4009, Spoj4009));
        float jenDruhy3 = Prvek().CalculateHeightFromData(Data(Spoj1017, Spoj4009));
        float oba2 = Prvek().CalculateHeightFromData(Data(Spoj1017, Spoj1017));

        Assert.Equal(oba3, jenDruhy3, 0.001f);
        Assert.Equal(VyskaRadkuPlatnosti(), jenDruhy3 - oba2, 0.05f);
    }

    /// <summary>
    /// <b>Rozlišovací test proti dělení podle významu.</b> Spoj 1017 (⚒ + ♿) má obě značky na
    /// prvním řádku, spoj 4009 (⑥ ✝ + ♿) má vozíček až na druhém.
    /// </summary>
    /// <remarks>
    /// Kdyby se dělilo podle rodin značek („dny nahoře, vlastnosti vozidla pod nimi"), skončil by
    /// vozíček spoje 1017 taky na druhém řádku a inkoust obou sloupců by sahal stejně hluboko.
    /// Přesně tenhle rozdíl rozhodl podle tisku ze starého modulu, takže bez tohohle testu by
    /// obě implementace vypadaly zeleně.
    /// </remarks>
    [Fact]
    public void DeleniJeMechanicke_NePodleVyznamu()
    {
        using var bmp = Vykresli(Prvek(), Data(Spoj1017, Spoj4009));

        float dolni1017 = PosledniInkoustYMm(bmp, SirkaZastavky + 0.5f, SirkaZastavky + SirkaSpoje);
        float dolni4009 = PosledniInkoustYMm(bmp, SirkaZastavky + SirkaSpoje + 0.5f,
            SirkaZastavky + 2 * SirkaSpoje);

        Assert.True(dolni1017 >= 0f, "značky spoje 1017 se nenakreslily");
        Assert.True(dolni4009 > dolni1017 + 0.3f,
            $"spoj 1017 má inkoust do {dolni1017:0.##} mm, spoj 4009 do {dolni4009:0.##} mm - "
            + "obě značky spoje 1017 patří na první řádek, u 4009 se má zalomit až vozíček");
    }

    /// <summary>Bez značek nevznikne žádný řádek platnosti - tabulka je vysoká jako dosud.</summary>
    [Fact]
    public void BezZnacek_ZadnyRadekNevznikne()
    {
        float bezKlice = Prvek().CalculateHeightFromData(Data());
        float prazdnePole = Prvek().CalculateHeightFromData(Data([]));

        Assert.Equal(bezKlice, prazdnePole, 0.001f);
        Assert.True(Prvek().CalculateHeightFromData(Data(["X"])) > bezKlice + 0.5f,
            "jedna značka má řádek platnosti vyrobit");
    }

    /// <summary>
    /// Negativní značka se tiskne <b>inverzně</b> - vyplněný tmavý box se světlým číslem uvnitř.
    /// </summary>
    /// <remarks>
    /// Měří se <b>hustota inkoustu</b> v obalu značky: obyčejný text zaplní pár procent plochy,
    /// vyplněný box většinu. Zároveň v něm musí zůstat světlé pixely - to je to číslo; kdyby
    /// se nakreslil jen box, byla by hustota 100 % a značka nečitelná.
    /// </remarks>
    [Fact]
    public void NegativniZnacka_SeTiskneInverzne()
    {
        using var bmp = Vykresli(Prvek(), Data(nz1: "24"));

        float hustota = HustotaInkoustuVObalu(bmp, SirkaZastavky + 0.5f, SirkaZastavky + SirkaSpoje);

        Assert.True(hustota > 0.5f,
            $"negativní značka zaplňuje {hustota:P0} svého obalu - na inverzní tisk je to málo, "
            + "vypadá to na obyčejný text");
        Assert.True(hustota < 0.95f,
            $"negativní značka zaplňuje {hustota:P0} svého obalu - číslo v boxu není vidět");
    }

    /// <summary>Řádek negativních značek zůstává <b>pod</b> značkami platnosti.</summary>
    /// <remarks>
    /// První spoj má jen značky platnosti, druhý jen negativní značku, každý ve svém sloupci -
    /// stačí porovnat, který inkoust je výš. Pořadí řádků hlavy je dané starým modulem
    /// (platnost `1.1`, `1.2`… → negativní značky `1.9`).
    /// </remarks>
    [Fact]
    public void NegativniZnacka_JePodZnackamiPlatnosti()
    {
        using var bmp = Vykresli(Prvek(), Data(Spoj4009, null, nz2: "24"));

        float yPlatnost = PosledniInkoustYMm(bmp, SirkaZastavky + 0.5f, SirkaZastavky + SirkaSpoje);
        float yNz = PrvniInkoustYMm(bmp, SirkaZastavky + SirkaSpoje + 0.5f,
            SirkaZastavky + 2 * SirkaSpoje);

        Assert.True(yPlatnost >= 0f, "značky platnosti se nenakreslily");
        Assert.True(yNz >= 0f, "negativní značka se nenakreslila");
        Assert.True(yNz > yPlatnost - 0.3f,
            $"negativní značka je na {yNz:0.##} mm, poslední značka platnosti na {yPlatnost:0.##} mm - "
            + "negativní značky patří pod platnost");
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    /// <summary>
    /// Podíl tmavých pixelů v obdélníkovém obalu veškerého inkoustu v zadaném svislém pásu.
    /// -1, když tam žádný inkoust není.
    /// </summary>
    private static float HustotaInkoustuVObalu(SKBitmap bmp, float odMm, float doMm)
    {
        int x1 = Math.Max(0, (int)(odMm * MmToPx));
        int x2 = Math.Min(bmp.Width - 1, (int)(doMm * MmToPx));

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = x1; x <= x2; x++)
                if (JeInkoust(bmp.GetPixel(x, y)))
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (minX > maxX) return -1f;

        int tmavych = 0;
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) tmavych++;

        return (float)tmavych / ((maxX - minX + 1) * (maxY - minY + 1));
    }

    /// <summary>Y prvního tmavého pixelu (mm) ve svislém pásu daném X rozsahem; -1 když žádný.</summary>
    private static float PrvniInkoustYMm(SKBitmap bmp, float odMm, float doMm)
    {
        int x1 = Math.Max(0, (int)(odMm * MmToPx));
        int x2 = Math.Min(bmp.Width - 1, (int)(doMm * MmToPx));
        for (int y = 0; y < bmp.Height; y++)
            for (int x = x1; x <= x2; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) return y / MmToPx;
        return -1f;
    }

    /// <summary>Y posledního tmavého pixelu (mm) ve svislém pásu daném X rozsahem; -1 když žádný.</summary>
    private static float PosledniInkoustYMm(SKBitmap bmp, float odMm, float doMm)
    {
        int x1 = Math.Max(0, (int)(odMm * MmToPx));
        int x2 = Math.Min(bmp.Width - 1, (int)(doMm * MmToPx));
        for (int y = bmp.Height - 1; y >= 0; y--)
            for (int x = x1; x <= x2; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) return y / MmToPx;
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
