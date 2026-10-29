using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Tečková výplň starého postskriptového tisku: v buňkách spojů bez času (`TiT = "."`,
/// <c>JRapiLinPrtPS.mac:58</c>) a za názvem zastávky jako vodicí linka.
/// </summary>
/// <remarks>
/// Obojí je <b>výchozí vypnuté</b> - u linky, kde většina spojů jede jen část trasy, se tabulka
/// tečkami zaplní a čte se hůř. Windows tisk starého modulu výplň nemá vůbec.
/// </remarks>
public class TeckyVeVyplniTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 40f;
    private const float SirkaSpoje = 12f;

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 70, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        Grid = new GridStyle { ShowColumnLines = false, ShowRowLines = false },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>Dvě zastávky: na první spoj staví, na druhé nemá čas vůbec (prázdná buňka).</summary>
    private static DataContext Data(string druhyCas = "") => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"),   ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("16:15", druhyCas))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

    // ── Buňky spojů ─────────────────────────────────────────────────────────

    /// <summary>
    /// Zapnutá výplň přidá do sloupce spojů inkoust, protože prázdná buňka dostane tečky.
    /// </summary>
    /// <remarks>
    /// Porovnává se **celý sloupec**, ne jedna buňka: pozice datového řádku závisí na výšce
    /// záhlaví a pásu skupin dnů, takže by ji test musel dopočítávat - a spletl by se dřív než
    /// kreslení.
    /// </remarks>
    [Fact]
    public void ZapnutaVypln_PridaTeckyDoPrazdneBunky()
    {
        var data = Data();
        var bez = Prvek();
        var s = Prvek();
        s.VyplnitPrazdneBunkySpoju = true;

        using var bmpBez = Vykresli(bez, data);
        using var bmpS = Vykresli(s, data);

        Assert.True(InkoustVeSloupciSpoje(bmpS) > InkoustVeSloupciSpoje(bmpBez),
            "zapnutá výplň nepřidala do sloupce spojů žádný inkoust");
    }

    /// <summary>
    /// ⚠️ Buňka se značkou <b>není prázdná</b>: „&lt;" znamená jinou trasu, „|" projetí bez
    /// zastavení. Kdyby je výplň přepsala tečkami, zmizí informace, kterou vysvětluje legenda.
    /// </summary>
    [Theory]
    [InlineData("<")]
    [InlineData("|")]
    public void ZnackaMistoCasu_SeNevyplnuje(string znacka)
    {
        var data = Data(znacka);
        var bez = Prvek();
        var s = Prvek();
        s.VyplnitPrazdneBunkySpoju = true;

        using var bmpBez = Vykresli(bez, data);
        using var bmpS = Vykresli(s, data);

        Assert.Equal(InkoustVeSloupciSpoje(bmpBez), InkoustVeSloupciSpoje(bmpS));
    }

    // ── Vodicí tečky za názvem zastávky ─────────────────────────────────────

    [Fact]
    public void VychoziStav_ZaNazvemZastavkyNejsouTecky()
    {
        var bez = Prvek();
        var s = Prvek();
        s.TeckyZaNazvemZastavky = true;

        using var bmpBez = Vykresli(bez, Data());
        using var bmpS = Vykresli(s, Data());

        Assert.True(InkoustVeSloupciZastavky(bmpS) > InkoustVeSloupciZastavky(bmpBez),
            "zapnuté vodicí tečky nepřidaly ve sloupci zastávek žádný inkoust");
    }

    /// <summary>
    /// Tečky nesmí zabírat víc, než kolik je volného místa - jinak by je zmenšování textu
    /// srazilo a s nimi i název zastávky.
    /// </summary>
    [Fact]
    public void TeckyNepretecouSloupec()
    {
        var prvek = Prvek();
        prvek.TeckyZaNazvemZastavky = true;

        using var bmp = Vykresli(prvek, Data());

        // Za pravou hranou sloupce zastávek nesmí být z jeho obsahu nic.
        int zaHranou = 0;
        for (int x = (int)(SirkaZastavky * MmToPx) + 1; x < (int)((SirkaZastavky + 1) * MmToPx); x++)
            zaHranou += InkoustVeSloupci(bmp, x);

        Assert.Equal(0, zaHranou);
    }

    // ── Pomocné ─────────────────────────────────────────────────────────────

    private static int InkoustVeSloupciSpoje(SKBitmap bmp)
    {
        int n = 0;
        for (int x = (int)(SirkaZastavky * MmToPx); x < (int)((SirkaZastavky + SirkaSpoje) * MmToPx); x++)
            n += InkoustVeSloupci(bmp, x);
        return n;
    }

    private static int InkoustVeSloupciZastavky(SKBitmap bmp)
    {
        int n = 0;
        for (int x = 0; x < (int)(SirkaZastavky * MmToPx); x++) n += InkoustVeSloupci(bmp, x);
        return n;
    }

    private static int InkoustVeSloupci(SKBitmap bmp, int x)
    {
        if (x < 0 || x >= bmp.Width) return 0;
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
            if (JeTmavy(bmp.GetPixel(x, y))) n++;
        return n;
    }

    private static bool JeTmavy(SKColor c) => c.Red < 128 && c.Green < 128 && c.Blue < 128;

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
