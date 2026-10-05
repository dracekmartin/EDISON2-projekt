using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Čáry mřížky musí ležet nad pozadím buněk. Čára sedí na hranici napůl v obou sousedech, a když se
/// pozadí souseda kreslilo až po ní, zakrylo jí polovinu - v podbarvených lichých Tč linkového JŘ
/// pak vypadaly svislé čáry slabší než v bílých řádcích (nález Jan 5. 10.).
/// </summary>
public class CaryNadPozadimBunekTests
{
    private const float MmToPx = 10f;
    private static readonly ResolvedTextStyle Bila = CellDefaults.Default;
    private static readonly ResolvedTextStyle Seda = CellDefaults.Default with { BackgroundColor = "#FF808080" };

    private static SKPaint Pero() => new()
    {
        Color = SKColors.Black, StrokeWidth = 4f, Style = SKPaintStyle.Stroke, IsAntialias = false,
    };

    private static SKBitmap Nakresli(params RowSpec[] radky)
    {
        var bitmap = new SKBitmap(300, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        RowPlanRenderer.DrawRows(new RenderContext(canvas, MmToPx), 0f, 0f, 20f, radky);
        return bitmap;
    }

    [Fact]
    public void SvislaCaraVPodbarvenemRadkuMaCelouTloustku()
    {
        using var pero = Pero();
        using var bitmap = Nakresli(new RowSpec(RowKind.Data, 5f, [new(10f, "", Seda), new(10f, "", Seda)])
            { VerticalLinePaint = pero });

        // Čára na x = 100 px, tloušťka 4 px: levá půlka 98-99, pravá 100-101.
        Assert.Equal(SKColors.Black, bitmap.GetPixel(98, 25));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(101, 25));
    }

    [Fact]
    public void VodorovnaCaraNadPodbarvenymRadkemMaCelouTloustku()
    {
        using var pero = Pero();
        using var bitmap = Nakresli(
            new RowSpec(RowKind.Data, 5f, [new(10f, "", Bila), new(10f, "", Bila)]) { LineBelowPaint = pero },
            new RowSpec(RowKind.Data, 5f, [new(10f, "", Seda), new(10f, "", Seda)]));

        // Čára na y = 50 px: horní půlka 48-49 v bílém řádku, dolní 50-51 v šedém.
        Assert.Equal(SKColors.Black, bitmap.GetPixel(50, 48));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(50, 51));
    }
}
