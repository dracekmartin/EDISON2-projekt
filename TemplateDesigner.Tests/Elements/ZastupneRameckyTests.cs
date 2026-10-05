using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Zástupný rámeček prázdného obrázku a kódu patří jen do návrháře. Do 5. 10. se tiskl do PDF jako
/// „IMG" a „QR" - výchozí šablona zastávkového JŘ má místo na logo i QR, které data nemusí naplnit.
/// </summary>
public class ZastupneRameckyTests
{
    private static int NebilychPixelu(TemplateElement el, bool navrh, DataContext? data = null)
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        el.Render(new RenderContext(canvas, 4f, data) { JeNavrh = navrh });
        int n = 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y) != SKColors.White) n++;
        return n;
    }

    public static TheoryData<TemplateElement> PrazdnePrvky => new()
    {
        new ImageElement { X = 5, Y = 5, Width = 30, Height = 20 },
        new BarcodeElement { X = 5, Y = 5, Width = 25, Height = 25, BarcodeType = BarcodeElementType.QR, Content = "{QR_KOD}" },
    };

    [Theory]
    [MemberData(nameof(PrazdnePrvky))]
    public void VTiskuSePrazdnyPrvekNekresli(TemplateElement el)
    {
        var data = new DataContext(values: new Dictionary<string, object?> { ["QR_KOD"] = "" });
        Assert.Equal(0, NebilychPixelu(el, navrh: false, data));
    }

    [Theory]
    [MemberData(nameof(PrazdnePrvky))]
    public void VNavrhariMaZastupnyRamecek(TemplateElement el)
    {
        Assert.True(NebilychPixelu(el, navrh: true) > 0);
    }
}
