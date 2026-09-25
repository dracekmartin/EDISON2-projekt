using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Minuty ve sloupci spoje stojí pod sebou, i když má jen některý řádek značku před časem.
/// </summary>
/// <remarks>
/// Značka (jen nástup, návaznost) stojí v buňce vlevo od času. Dokud se ostatní časy sloupce
/// sázely na střed, byl čas se značkou o její šířku vpravo a minuty nešly pod sebou
/// (hovor s kolegou 25. 9.).
/// </remarks>
public class MinutyPodSebouTests
{
    private const float MmToPx = 8f;

    /// <summary>⚠️ Jádro: pravý okraj času je u řádku se značkou i bez ní na stejném místě.</summary>
    [Fact]
    public void CasBezZnackySeZarovnaSeZnackou()
    {
        var okraje = PraveOkrajeCasu(Data(new JsonArray("N", "", "")));

        Assert.Equal(3, okraje.Count);
        Assert.All(okraje, x => Assert.InRange(x, okraje[0] - 1, okraje[0] + 1));
    }

    /// <summary>Sloupec bez značky se nehne - odsazení platí jen tam, kde značka je.</summary>
    [Fact]
    public void SloupecBezZnackySeNemeni()
    {
        var okraje = PraveOkrajeCasu(Data(znacky: null));

        Assert.Equal(3, okraje.Count);
        Assert.All(okraje, x => Assert.InRange(x, okraje[0] - 1, okraje[0] + 1));
    }

    /// <summary>
    /// Nejpravější tmavý pixel každého řádku zastávky - čas stojí vpravo od názvu zastávky,
    /// takže je to konec času. Řádky se poznají jako pruhy s kresbou; mřížka je světlá a nepočítá se.
    /// </summary>
    private static List<int> PraveOkrajeCasu(DataContext data)
    {
        var prvek = new LineTableElement
        {
            X = 0, Y = 0, Width = 120, Height = null,
            ShowTripNumbers = false, ShowPlatnostRow = false, ShowNzRow = false,
            ShowGroupHeaders = false, PadEmptyColumns = false,
        };
        prvek.DataStyle.FontSize = 8f;

        float vyska = prvek.CalculateHeightFromData(data);
        int w = (int)MathF.Ceiling((prvek.Width + 4f) * MmToPx);
        int h = (int)MathF.Ceiling((vyska + 4f) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        var pruhy = new List<int>();
        int pravy = -1;
        for (int y = 0; y < h; y++)
        {
            int radek = -1;
            for (int x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red < 120 && c.Green < 120 && c.Blue < 120) radek = x;
            }
            if (radek >= 0) pravy = Math.Max(pravy, radek);
            else if (pravy >= 0) { pruhy.Add(pravy); pravy = -1; }
        }
        if (pravy >= 0) pruhy.Add(pravy);
        // Úzké pruhy vlevo (kresba jen v popisných sloupcích) nejsou řádky s časem; záhlaví
        // sloupců je nad zastávkami - bereme poslední tři pruhy s časem.
        var sCasem = pruhy.Where(x => x > 20 * MmToPx).ToList();
        return sCasem.Count >= 3 ? sCasem[^3..] : sCasem;
    }

    private static DataContext Data(JsonArray? znacky)
    {
        var spoj = new List<(string, object?)>
        {
            ("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:00", "8:00")),
        };
        if (znacky is not null) spoj.Add(("ZNACKY", znacky));

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
