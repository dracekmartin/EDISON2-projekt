using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Tučné písmo zadané u zastávky v trase linky (atribut „T" = doslova „tučně", číselník 16).
/// Obě staré cesty ho tisknou: postskript prefixem „#", Windows tisk příznakem řádku.
/// </summary>
public class TucneZastavkyDleTrasyTests
{
    private const float MmToPx = 20f;

    private static LineTableElement Prvek(bool tucne = true)
    {
        var el = ElementRenderFixtures.LineTable();
        el.TucneZastavkyDleTrasy = tucne;
        el.Width = 80;
        el.DataRowHeight = 4f;
        return el;
    }

    private static DataContext Data(params string[][] atributyZastavek) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            .. atributyZastavek.Select((a, i) => RenderSnapshot.Row(
                ("NAZEV_ZASTAVKY", "Kroměříž"),
                ("PORADI", (i + 1).ToString()),
                ("ATRIBUTY", new JsonArray([.. a.Select(x => (JsonNode?)x)])))),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray([.. atributyZastavek.Select(_ => (JsonNode?)"16:15")]))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

    // ── Zavádějící mezera ───────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ Řídicí atributy („T" tarifní bod, „P" oddělený příjezd) se netisknou, ale dřív kvůli nim
    /// před název přibyla mezera - zastávka vypadala odsazená bez viditelného důvodu, a byly to
    /// zrovna ty významné.
    /// </summary>
    [Theory]
    [InlineData("T")]
    [InlineData("P")]
    [InlineData("T,A")]
    [InlineData("")]
    public void RidiciAtributyNeodsazujiNazev(string atributy)
    {
        var seznam = atributy == "" ? [] : atributy.Split(',');

        Assert.Equal("Kroměříž", LineTableElement.NazevSeZnackami(seznam, "Kroměříž"));
    }

    /// <summary>Zobrazitelná značka („x" = na znamení) se před název postavit má, i s mezerou.</summary>
    [Fact]
    public void ZobrazitelnaZnackaStojiPredNazvem()
    {
        Assert.Equal("x Kroměříž", LineTableElement.NazevSeZnackami(["x", "T"], "Kroměříž"));
    }

    /// <summary>
    /// ⚠️ Má-li značku aspoň jedna zastávka linky, odsadí se **všechny** názvy stejně, takže stojí
    /// ve svislici - tak to dělá starý tisk (rozhodnutí Jan 22. 9.). Odsazení je změřená šířka
    /// „x ", ne mezery: mezera je užší než „x" a názvy bez značky začínaly o kus vlevo (23. 9.).
    /// </summary>
    [Fact]
    public void SeZnackouNaLinceZacinajiVsechnyNazvyNaStejnemMiste()
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 40f);
        string[] linka = ["x"];

        float bezZnacky = LineTableElement.DorovnaniNazvuMm("", linka, font, MmToPx);
        float seZnackou = LineTableElement.DorovnaniNazvuMm("x", linka, font, MmToPx);

        Assert.Equal(font.MeasureText("x ") / MmToPx, bezZnacky, 3);
        Assert.Equal(0f, seZnackou);
    }

    /// <summary>Linka, kde značku nemá nikdo, zůstane bez odsazení - jinak by se sloupec
    /// zbytečně posunul o znak, který se nikdy nevytiskne.</summary>
    [Fact]
    public void BezZnacekNaLinceSeNeodsazuje()
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 40f);

        Assert.Equal("Kroměříž", LineTableElement.NazevSeZnackami(["T"], "Kroměříž"));
        Assert.Equal(0f, LineTableElement.DorovnaniNazvuMm("", [], font, MmToPx));
    }

    // ── Tučné zvýraznění ────────────────────────────────────────────────────

    [Fact]
    public void ZastavkaSAtributemTucneJeTucne()
    {
        using var tucne = Vykresli(Prvek(tucne: true), Data(["T"]));
        using var normalne = Vykresli(Prvek(tucne: false), Data(["T"]));

        Assert.True(Inkoust(tucne) > Inkoust(normalne),
            "tučná významná zastávka nezabrala víc inkoustu než běžná");
    }

    /// <summary>Běžná zastávka se tučně vypisovat nesmí, jinak by zvýraznění nic neříkalo.</summary>
    [Fact]
    public void ZastavkaBezAtributuTucneNeni()
    {
        using var s = Vykresli(Prvek(tucne: true), Data([]));
        using var bez = Vykresli(Prvek(tucne: false), Data([]));

        Assert.Equal(Inkoust(bez), Inkoust(s));
    }

    // ── Pomocné ─────────────────────────────────────────────────────────────

    private static bool JeTmavy(SKColor c) => c.Red < 128 && c.Green < 128 && c.Blue < 128;

    /// <summary>X prvního tmavého pixelu v pásu názvů zastávek.</summary>
    private static int PrvniInkoustZleva(SKBitmap bmp)
    {
        for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeTmavy(bmp.GetPixel(x, y))) return x;
        return -1;
    }

    private static int Inkoust(SKBitmap bmp)
    {
        int n = 0;
        for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeTmavy(bmp.GetPixel(x, y))) n++;
        return n;
    }

    /// <summary>Vykreslí jen pás sloupce zastávek, ať do měření nespadnou čísla spojů ani časy.</summary>
    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 5f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 5f) * MmToPx);
        var plne = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(plne))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }
        // Sloupec zastávek je ve fixtuře čtvrtý zleva; bere se pás, kde leží jeho text.
        var vyrez = new SKBitmap((int)(45 * MmToPx), h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var c = new SKCanvas(vyrez))
        {
            c.Clear(SKColors.White);
            c.DrawBitmap(plne, new SKRect(0, 0, 45 * MmToPx, h), new SKRect(0, 0, 45 * MmToPx, h));
        }
        plne.Dispose();
        return vyrez;
    }
}
