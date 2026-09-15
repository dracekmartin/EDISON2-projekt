using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Zastávka, u které se tiskne příjezd i odjezd, ve tvaru postskriptového tisku (`B140`):
/// popisné sloupce jednou přes oba řádky a mezi časy vodorovná úsečka.
/// </summary>
/// <remarks>
/// <para>Starý postskript na to má <b>tři</b> řádky - prostřední prázdný, jen kvůli úsečce, protože
/// ji sází jako znak (`JRapiLinPrtPS1.mac:51` přidává dva řádky). My kreslíme čáru, takže leží na
/// hranici dvou řádků a zastávka nezabere víc místa; parita je tedy ve tvaru, ne v počtu řádků.</para>
/// <para>⚠️ Nejdůležitější je tu test <see cref="BezRozdvojeneZastavky_VolbaNicNemeni"/>: volba se
/// nesmí dotknout sestav, které rozdvojenou zastávku nemají.</para>
/// </remarks>
public class RozdvojenaZastavkaSloucenaTests
{
    private const float MmToPx = 20f;

    // ── Rozpoznání dvojice ──────────────────────────────────────────────────

    /// <summary>Příjezdový řádek a odjezdový za ním tvoří dvojici; samostatná zastávka ne.</summary>
    [Fact]
    public void PrijezdASOdjezdemTvoriDvojici()
    {
        var radky = LineTableElement.RozdvojeneRadky(
        [
            Zastavka(1, "prijezd"), Zastavka(1, "zastavka"), Zastavka(2, "zastavka"),
        ]);

        Assert.Equal(
        [
            LineTableElement.RozdvojenyRadek.Horni,
            LineTableElement.RozdvojenyRadek.Dolni,
            LineTableElement.RozdvojenyRadek.Zadny,
        ], radky);
    }

    /// <summary>
    /// Dvě zastávky za sebou se stejným pořadím, ale bez příjezdového řádku, dvojice nejsou.
    /// </summary>
    [Fact]
    public void DvaOdjezdoveRadkyNejsouDvojice()
    {
        var radky = LineTableElement.RozdvojeneRadky([Zastavka(1, "zastavka"), Zastavka(1, "zastavka")]);

        Assert.All(radky, r => Assert.Equal(LineTableElement.RozdvojenyRadek.Zadny, r));
    }

    /// <summary>
    /// Skrytý příjezdový řádek dvojici ruší - odjezdový pak stojí sám a musí si popisy vypsat,
    /// jinak by zůstal prázdný.
    /// </summary>
    [Fact]
    public void SkrytyPrijezd_OdjezdStojiSam()
    {
        var stops = new List<LineTableElement.LineStopData>
        {
            Zastavka(1, "prijezd"), Zastavka(1, "zastavka"),
        };

        var radky = LineTableElement.RozdvojeneRadky(stops, s => !s.JePrijezdovy);

        Assert.All(radky, r => Assert.Equal(LineTableElement.RozdvojenyRadek.Zadny, r));
    }

    // ── Vykreslení ──────────────────────────────────────────────────────────

    /// <summary>Se zapnutou volbou se název zastávky vypíše jednou, ne na obou řádcích.</summary>
    [Fact]
    public void ZapnutaVolba_NazevJenJednou()
    {
        int slouceno = Inkoust(VykresliPopisneSloupce(Prvek(sloucit: true), Data()));
        int zopakovano = Inkoust(VykresliPopisneSloupce(Prvek(sloucit: false), Data()));

        Assert.True(slouceno < zopakovano,
            $"sloučený popis nezabral míň inkoustu než zopakovaný ({slouceno} vs {zopakovano})");
    }

    /// <summary>
    /// Se zapnutou volbou vede mezi časy příjezdu a odjezdu vodorovná úsečka. Bez ní není z tisku
    /// poznat, že ty dva časy patří téže zastávce.
    /// </summary>
    [Fact]
    public void ZapnutaVolba_MeziCasyJeUsecka()
    {
        using var slouceno = VykresliSpoje(Prvek(sloucit: true), Data());
        using var zopakovano = VykresliSpoje(Prvek(sloucit: false), Data());

        Assert.True(NejtmavsiRadek(slouceno) > NejtmavsiRadek(zopakovano),
            "ve sloupcích spojů nepřibyla souvislá vodorovná úsečka");
    }

    /// <summary>
    /// <b>Sestava bez rozdvojené zastávky se volbou nezmění ani o pixel.</b> Rozdvojení má jen
    /// část zastávek, takže zapnutá volba nesmí sáhnout na zbytek tabulky.
    /// </summary>
    [Fact]
    public void BezRozdvojeneZastavky_VolbaNicNemeni()
    {
        var data = Data(rozdvojena: false);
        using var slouceno = Vykresli(Prvek(sloucit: true), data);
        using var zopakovano = Vykresli(Prvek(sloucit: false), data);

        Assert.Equal(zopakovano.Bytes, slouceno.Bytes);
    }

    // ── Pomocné ─────────────────────────────────────────────────────────────

    private static LineTableElement.LineStopData Zastavka(int poradi, string typ) =>
        new() { Poradi = poradi, Typ = typ, Popis = "Kroměříž" };

    private static LineTableElement Prvek(bool sloucit)
    {
        var el = ElementRenderFixtures.LineTable();
        el.SloucitPopisyRozdvojene = sloucit;
        el.Width = 80;
        el.DataRowHeight = 4f;
        el.ShowPripojeRows = false;
        return el;
    }

    /// <summary>Tři zastávky, prostřední volitelně rozdvojená na příjezd a odjezd.</summary>
    private static DataContext Data(bool rozdvojena = true)
    {
        List<DataContext> zastavky =
        [
            Radek(1, "zastavka", "Kroměříž", []),
            .. rozdvojena ? new[] { Radek(2, "prijezd", "Hulín", ["J"]) } : [],
            Radek(2, "zastavka", "Hulín", rozdvojena ? ["J"] : []),
            Radek(3, "zastavka", "Přerov", []),
        ];
        string[] casy = rozdvojena ? ["8:00", "8:20", "8:22", "9:05"] : ["8:00", "8:22", "9:05"];

        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
                    ("CASY", new JsonArray([.. casy.Select(c => (JsonNode?)c)]))),
                RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"), ("SMER", "T"),
                    ("CASY", new JsonArray([.. casy.Select(c => (JsonNode?)c)]))),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));
    }

    private static DataContext Radek(int poradi, string typ, string nazev, string[] atributy) =>
        RenderSnapshot.Row(("PORADI", poradi.ToString()), ("TYP", typ), ("NAZEV_ZASTAVKY", nazev),
            ("TC", poradi.ToString()),
            ("ATRIBUTY", new JsonArray([.. atributy.Select(a => (JsonNode?)a)])));

    private static bool JeTmavy(SKColor c) => c.Red < 128 && c.Green < 128 && c.Blue < 128;

    private static int Inkoust(SKBitmap bmp)
    {
        int n = 0;
        for (int x = 0; x < bmp.Width; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeTmavy(bmp.GetPixel(x, y))) n++;
        bmp.Dispose();
        return n;
    }

    /// <summary>Nejvíc tmavých pixelů na jedné vodorovné řádce - úsečka je jich souvislá řada.</summary>
    private static int NejtmavsiRadek(Obraz obraz)
    {
        int max = 0;
        for (int y = 0; y < obraz.Bitmap.Height; y++)
        {
            int n = 0;
            for (int x = 0; x < obraz.Bitmap.Width; x++)
                if (JeTmavy(obraz.Bitmap.GetPixel(x, y))) n++;
            max = Math.Max(max, n);
        }
        return max;
    }

    private sealed class Obraz(SKBitmap bitmap) : IDisposable
    {
        public SKBitmap Bitmap { get; } = bitmap;
        public byte[] Bytes => Bitmap.Bytes;
        public void Dispose() => Bitmap.Dispose();
    }

    private static Obraz Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 10f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 10f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return new Obraz(bmp);
    }

    /// <summary>Jen pás popisných sloupců - do měření nesmí spadnout časy spojů.</summary>
    private static SKBitmap VykresliPopisneSloupce(LineTableElement prvek, DataContext data) =>
        Vyrez(prvek, data, 0f, 40f);

    /// <summary>Jen pás sloupců spojů - tam leží úsečka mezi příjezdem a odjezdem.</summary>
    private static Obraz VykresliSpoje(LineTableElement prvek, DataContext data) =>
        new(Vyrez(prvek, data, 45f, prvek.Width + 10f));

    private static SKBitmap Vyrez(LineTableElement prvek, DataContext data, float odMm, float doMm)
    {
        using var plne = Vykresli(prvek, data);
        int x0 = (int)(odMm * MmToPx), x1 = (int)(doMm * MmToPx);
        var vyrez = new SKBitmap(x1 - x0, plne.Bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var c = new SKCanvas(vyrez);
        c.Clear(SKColors.White);
        c.DrawBitmap(plne.Bitmap, new SKRect(x0, 0, x1, plne.Bitmap.Height),
            new SKRect(0, 0, x1 - x0, plne.Bitmap.Height));
        return vyrez;
    }
}
