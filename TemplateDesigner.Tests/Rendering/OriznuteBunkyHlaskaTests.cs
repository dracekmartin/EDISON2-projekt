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
