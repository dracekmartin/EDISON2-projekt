using TemplateDesigner.Models;
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
        log.Report("Kroměříž");

        string? hlaska = log.Summarize();
        Assert.NotNull(hlaska);
        Assert.Contains("Seznam zastávek", hlaska);
    }

    /// <summary>
    /// Samotné jméno prvku nestačí: v tabulce spojů jsou tisíce buněk a bez textu nejde poznat,
    /// která se ořízla, ani jestli je hláška oprávněná (nález Jan 15. 10.).
    /// </summary>
    [Fact]
    public void JednaBunka_HlaskaRekneTextBunky()
    {
        var log = new ClippedCells { CurrentElement = "Tabulka spojů" };
        log.Report("14.05");

        Assert.Contains("obsah buňky „14.05“", log.Summarize());
    }

    [Fact]
    public void BunkaBezTextu_HlaskaZustaneObecna()
    {
        var log = new ClippedCells { CurrentElement = "Tabulka spojů" };
        log.Report("");

        Assert.Contains("obsah jedné buňky", log.Summarize());
    }

    [Fact]
    public void HlaskaRekneStranu()
    {
        var log = new ClippedCells { CurrentElement = "Tabulka spojů", CurrentPage = 2 };
        log.Report("14.05");

        Assert.Contains("Tabulka spojů · strana 2", log.Summarize());
    }

    [Fact]
    public void ViceStran_VypiseVsechny()
    {
        var log = new ClippedCells { CurrentElement = "Tabulka spojů", CurrentPage = 3 };
        log.Report("14.05");
        log.CurrentPage = 1;
        log.Report("15.10");
        log.Report("15.40");

        Assert.Contains("strany 1, 3", log.Summarize());
    }

    [Fact]
    public void ViceBunek_VyjmenujeTriTextyAZbytekShrne()
    {
        var log = new ClippedCells { CurrentElement = "Tabulka spojů" };
        foreach (var text in new[] { "A1", "B2", "C3", "D4", "E5" }) log.Report(text);

        string? hlaska = log.Summarize();
        Assert.Contains("obsah 5 buněk", hlaska);
        Assert.Contains("„A1“, „B2“, „C3“ a další", hlaska);
        Assert.DoesNotContain("D4", hlaska);
    }

    [Fact]
    public void ViceBunekSeStejnymTextem_TextJenJednou()
    {
        var log = new ClippedCells { CurrentElement = "Tabulka spojů" };
        log.Report("14.05");
        log.Report("14.05");

        string? hlaska = log.Summarize();
        Assert.Contains("obsah 2 buněk", hlaska);
        Assert.Contains("(„14.05“)", hlaska);
    }

    [Fact]
    public void DlouhyAViceradkovyText_SeZkratiDoJednohoRadku()
    {
        var log = new ClippedCells { CurrentElement = "Seznam zastávek" };
        log.Report("Zastávka\nStop");
        log.CurrentElement = "Tabulka spojů";
        log.Report(new string('x', 100));

        string? hlaska = log.Summarize();
        Assert.Contains("„Zastávka / Stop“", hlaska);
        Assert.Contains(new string('x', 39) + "…“", hlaska);
        Assert.DoesNotContain(new string('x', 40), hlaska);
    }

    [Fact]
    public void ViceBunekTehozPrvku_SeSecte()
    {
        var log = new ClippedCells { CurrentElement = "Seznam zastávek" };
        log.Report("a");
        log.Report("b");
        log.Report("c");

        Assert.Contains("3 buněk", log.Summarize());
    }

    [Fact]
    public void VicePrvku_KazdyMaSvouCast()
    {
        var log = new ClippedCells { CurrentElement = "Minuty odjezdů" };
        log.Report("12");
        log.CurrentElement = "Seznam zastávek";
        log.Report("Kroměříž");

        string? hlaska = log.Summarize();
        Assert.NotNull(hlaska);
        Assert.Contains("Minuty odjezdů", hlaska);
        Assert.Contains("Seznam zastávek", hlaska);
        Assert.Contains(";", hlaska);
    }

    [Fact]
    public void UzkaBunka_PriKresleniOhlasiOriznutiSTextem()
    {
        var log = KresliBunku("Dolní Dvořiště", sirkaMm: 3f);
        Assert.Contains("„Dolní Dvořiště“", log.Summarize());
    }

    [Fact]
    public void SirokaBunka_PriKresleniNehlasiNic()
    {
        var log = KresliBunku("Dasný", sirkaMm: 40f);
        Assert.Null(log.Summarize());
    }

    /// <summary>
    /// Text po zmenšení začíná u levého vnitřního okraje a hrana buňky ho usekne až za pravým.
    /// Když zabere jen pravý vnitřní okraj, je celý vidět a hlásit se nesmí - „11,12" v úzkém
    /// sloupci TPZ (nález Jan 15. 10.).
    /// </summary>
    [Fact]
    public void TextVPravemVnitrnimOkraji_NehlasiOriznuti()
    {
        var (styl, sirkaTextu) = NezmensitelnyStyl("11,12");

        // Obsah bez okrajů je o půl pravého okraje užší než text, buňka o půl okraje širší.
        var log = KresliBunku("11,12", styl.PaddingLeft + sirkaTextu + styl.PaddingRight / 2f, styl);

        Assert.Null(log.Summarize());
    }

    [Fact]
    public void TextPresHranuBunky_HlasiOriznuti()
    {
        var (styl, sirkaTextu) = NezmensitelnyStyl("11,12");

        var log = KresliBunku("11,12", styl.PaddingLeft + sirkaTextu - 0.2f, styl);

        Assert.Contains("„11,12“", log.Summarize());
    }

    /// <summary>Styl, který už zmenšit nejde (4 body = dno zmenšování), a šířka textu v něm (mm).</summary>
    private static (ResolvedTextStyle Styl, float SirkaMm) NezmensitelnyStyl(string text)
    {
        var styl = CellDefaults.Default with { FontSize = CellRenderer.MinFontPt, PaddingLeft = 0.5f, PaddingRight = 1f };
        using var font = styl.CreateFont(1f, MmToPx);
        return (styl, font.MeasureText(text) / MmToPx);
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

    private const float MmToPx = 96f / 25.4f;

    /// <summary>Nakreslí jednu buňku dané šířky a vrátí sběrač, aby šlo zkontrolovat, co ohlásila.</summary>
    private static ClippedCells KresliBunku(string text, float sirkaMm, ResolvedTextStyle? styl = null)
    {
        var log = new ClippedCells { CurrentElement = "Seznam zastávek" };
        using var bitmap = new SkiaSharp.SKBitmap(400, 100);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        var ctx = new TemplateDesigner.Models.RenderContext(canvas, MmToPx) { ClippedCells = log };
        CellRenderer.DrawCell(ctx, new SkiaSharp.SKRect(0, 0, sirkaMm, 5f), text, styl ?? CellDefaults.Default);
        return log;
    }
}
