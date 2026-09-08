using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Negativní značka je tmavý box a musí se vejít do své buňky.
/// </summary>
/// <remarks>
/// ⚠️ Box se kreslí přes celou výšku písma, kdežto řádek značek je vysoký jako datový řádek.
/// U hustě nastavené tabulky (malá výška řádku proti velikosti písma) box přesahoval přes spodní
/// linku do řádku pod sebou - a protože je černý, bylo to v tisku hned vidět (nález Jan 7. 9.).
/// </remarks>
public class NegativniZnackaVeskaTests
{
    private const float MmToPx = 20f;

    private static ResolvedTextStyle Styl(float velikostPt) =>
        new TextStyle { FontFamily = "Arial", FontSize = velikostPt, TextColor = "#FF000000" }
            .Resolve(ResolvedTextStyle.Default);

    /// <summary>Svislý rozsah tmavých pixelů v obrázku, nebo (-1, -1) když tam žádné nejsou.</summary>
    private static (int Od, int Do) SvislyRozsahInkoustu(SKBitmap bmp)
    {
        int od = -1, doo = -1;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red >= 128 || c.Green >= 128 || c.Blue >= 128) continue;
                if (od < 0) od = y;
                doo = y;
                break;
            }
        return (od, doo);
    }

    private static SKBitmap Vykresli(float vyskaBunkyMm, float velikostPt)
    {
        // Buňka leží uprostřed plátna, aby bylo vidět i to, co z ní vyteče nahoru nebo dolů.
        const float okrajMm = 4f;
        var bmp = new SKBitmap((int)(20 * MmToPx), (int)((vyskaBunkyMm + 2 * okrajMm) * MmToPx),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        var ctx = new RenderContext(canvas, MmToPx, null);
        NegativniZnackaRenderer.DrawCell(ctx,
            new SKRect(1f, okrajMm, 19f, okrajMm + vyskaBunkyMm), "23", Styl(velikostPt), 1f);
        return bmp;
    }

    /// <summary>
    /// ⚠️ Podstata: písmo vyšší, než je řádek. Před opravou box přetekl přes spodní hranu.
    /// </summary>
    [Theory]
    [InlineData(2.1f, 4.8f)]    // hustá tabulka podle starého postskriptu
    [InlineData(1.5f, 6.0f)]    // krajní případ - řádek výrazně nižší než písmo
    [InlineData(2.6f, 6.0f)]
    public void BoxSeVejdeDoBunky(float vyskaBunkyMm, float velikostPt)
    {
        const float okrajMm = 4f;
        using var bmp = Vykresli(vyskaBunkyMm, velikostPt);

        var (od, doo) = SvislyRozsahInkoustu(bmp);

        Assert.True(od >= 0, "značka se vůbec nenakreslila");
        // Tolerance jeden pixel na vyhlazení hran.
        Assert.True(od >= (int)(okrajMm * MmToPx) - 1,
            $"značka přetéká nad horní hranu buňky (inkoust od {od}, buňka od {(int)(okrajMm * MmToPx)})");
        Assert.True(doo <= (int)((okrajMm + vyskaBunkyMm) * MmToPx) + 1,
            $"značka přetéká pod spodní hranu buňky (inkoust do {doo}, "
            + $"buňka do {(int)((okrajMm + vyskaBunkyMm) * MmToPx)})");
    }

    /// <summary>Když se písmo do řádku vejde, nesmí se nic zmenšovat - jinak by oprava zdrobnila
    /// značky i tam, kde byly v pořádku.</summary>
    [Fact]
    public void DostVysokyRadekZnackuNezmensi()
    {
        using var uzky = Vykresli(vyskaBunkyMm: 8f, velikostPt: 6f);
        using var siroky = Vykresli(vyskaBunkyMm: 12f, velikostPt: 6f);

        var (odU, doU) = SvislyRozsahInkoustu(uzky);
        var (odS, doS) = SvislyRozsahInkoustu(siroky);

        Assert.Equal(doU - odU, doS - odS);
    }
}
