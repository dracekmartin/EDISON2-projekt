using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// `B162` - hláška o buňkách, do kterých se obsah nevešel ani po zmenšení na mez. Sbírá se při
/// kreslení, proto musí jmenovat prvek, u kterého se to stalo, a snést víc prvků najednou.
/// </summary>
public class OriznuteBunkyHlaskaTests
{
    [Fact]
    public void BezOriznuti_ZadnaHlaska()
    {
        Assert.Null(new ClippedCells().Summarize());
    }

    [Fact]
    public void JednaBunka_HlaskaJmenujePrvek()
    {
        var log = new ClippedCells { CurrentElement = "Seznam zastávek" };
        log.Report();

        string? hlaska = log.Summarize();
        Assert.NotNull(hlaska);
        Assert.Contains("Seznam zastávek", hlaska);
        Assert.Contains("jedné buňky", hlaska);
    }

    [Fact]
    public void ViceBunekTehozPrvku_SeSecte()
    {
        var log = new ClippedCells { CurrentElement = "Seznam zastávek" };
        log.Report();
        log.Report();
        log.Report();

        Assert.Contains("3 buněk", log.Summarize());
    }

    [Fact]
    public void VicePrvku_KazdyMaSvouCast()
    {
        var log = new ClippedCells { CurrentElement = "Minuty odjezdů" };
        log.Report();
        log.CurrentElement = "Seznam zastávek";
        log.Report();

        string? hlaska = log.Summarize();
        Assert.NotNull(hlaska);
        Assert.Contains("Minuty odjezdů", hlaska);
        Assert.Contains("Seznam zastávek", hlaska);
        Assert.Contains(";", hlaska);
    }

    [Fact]
    public void UzkaBunka_PriKresleniOhlasiOriznuti()
    {
        var log = KresliBunku("České Budějovice,U Zelené ratolesti rozc. 1.0", sirkaMm: 6f);
        Assert.NotNull(log.Summarize());
    }

    [Fact]
    public void SirokaBunka_PriKresleniNehlasiNic()
    {
        var log = KresliBunku("Dasný", sirkaMm: 40f);
        Assert.Null(log.Summarize());
    }

    /// <summary>
    /// Textový prvek s nízkou pevnou výškou, kterému se zalomený text nevejde ani po zmenšení -
    /// hlásí se při kreslení s daty stránky, protože pole jako dopravci stránky plní až sazba.
    /// </summary>
    [Fact]
    public void NizkyTextovyPrvek_PriKresleniOhlasiOriznuti()
    {
        var log = KresliText("{POPIS_DOPRAVCE}", vyskaMm: 3f);

        string? hlaska = log.Summarize();
        Assert.NotNull(hlaska);
        Assert.Contains("Text", hlaska);
        Assert.Contains("ořízl", hlaska);
    }

    [Fact]
    public void TextovyPrvekSPrazdnouVyskou_NehlasiNic()
    {
        Assert.Null(KresliText("{POPIS_DOPRAVCE}", vyskaMm: null).Summarize());
    }

    [Fact]
    public void PrvekPresOkrajStranky_TextSeZmensiDoViditelneSirky()
    {
        // Hlavička z tisku 23. 9.: prvek od 0,5 mm široký 200 mm na ploše široké 200 mm.
        var prvek = new TemplateDesigner.Models.Elements.TextElement
        {
            X = 0.5f, Width = 200, Height = 3.2f, WordWrap = false,
            Style = new TemplateDesigner.Models.TextStyle { FontFamily = "Arial", FontSize = 6f },
        };
        string text = string.Join(", ", Enumerable.Repeat("ČSAD AUTOBUSY České Budějovice a.s. (spoje 1-5, 9)", 5));

        float viditelna = prvek.SirkaTextuMm(200f);
        Assert.Equal(199.5f, viditelna, 3);
        Assert.True(prvek.ComputeShrinkScale(text, viditelna) < prvek.ComputeShrinkScale(text),
            "do viditelné šířky se musí text zmenšit víc než do celého prvku");
        Assert.Equal(200f, prvek.SirkaTextuMm(null));   // mimo stránku (testy, plátno) platí prvek
    }

    private static ClippedCells KresliText(string text, float? vyskaMm)
    {
        var log = new ClippedCells { CurrentElement = "Text" };
        var prvek = new TemplateDesigner.Models.Elements.TextElement
        {
            X = 0, Y = 0, Width = 60, Height = vyskaMm, Text = text, WordWrap = true,
            Style = new TemplateDesigner.Models.TextStyle { FontFamily = "Arial", FontSize = 6f },
        };
        var data = new TemplateDesigner.Models.DataContext(new Dictionary<string, object?>
        {
            ["POPIS_DOPRAVCE"] = string.Join(", ", Enumerable.Repeat("ČSAD AUTOBUSY České Budějovice a.s. (spoje 1-5, 9)", 12)),
        });
        using var bitmap = new SkiaSharp.SKBitmap(400, 100);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        prvek.Render(new TemplateDesigner.Models.RenderContext(canvas, 96f / 25.4f, data) { ClippedCells = log });
        return log;
    }

    /// <summary>Nakreslí jednu buňku dané šířky a vrátí sběrač, aby šlo zkontrolovat, co ohlásila.</summary>
    private static ClippedCells KresliBunku(string text, float sirkaMm)
    {
        var log = new ClippedCells { CurrentElement = "Seznam zastávek" };
        using var bitmap = new SkiaSharp.SKBitmap(400, 100);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        var ctx = new TemplateDesigner.Models.RenderContext(canvas, 96f / 25.4f) { ClippedCells = log };
        CellRenderer.DrawCell(ctx, new SkiaSharp.SKRect(0, 0, sirkaMm, 5f), text, CellDefaults.Default);
        return log;
    }
}
