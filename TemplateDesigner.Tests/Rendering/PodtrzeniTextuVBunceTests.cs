using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Podtržení a přeškrtnutí textu, který se nekreslí jako celá buňka: minuta a značky u odjezdů
/// v Minutách odjezdů, čas a značky v Tabulce spojů (sada stylu Text, `B194` E3). Formulář je
/// nabízel, ale kreslení je vynechávalo - pole nic nedělala.
/// </summary>
/// <remarks>Měří se přírůstek tmavých pixelů proti témuž textu bez čáry; čára přes celou šířku
/// textu přidá řádově desítky pixelů, posun vyhlazení písma jen jednotky.</remarks>
public class PodtrzeniTextuVBunceTests
{
    private const float MmToPx = 10f;
    private const int PrirustekCary = 15;

    [Fact]
    public void Minuta_Podtrzeni_SeNakresli()
    {
        int bez = Inkoust(Mrizka(_ => { }));
        int s = Inkoust(Mrizka(p => p.MinuteStyle.Underline = true));

        Assert.True(s > bez + PrirustekCary, $"podtržená minuta má {s} tmavých pixelů, bez podtržení {bez}");
    }

    [Fact]
    public void Minuta_Preskrtnuti_SeNakresli()
    {
        int bez = Inkoust(Mrizka(_ => { }));
        int s = Inkoust(Mrizka(p => p.MinuteStyle.Strikethrough = true));

        Assert.True(s > bez + PrirustekCary, $"přeškrtnutá minuta má {s} tmavých pixelů, bez přeškrtnutí {bez}");
    }

    [Fact]
    public void TextovaZnackaUOdjezdu_Podtrzeni_SeNakresli()
    {
        int bez = Inkoust(Mrizka(_ => { }, znacka: "xyz"));
        int s = Inkoust(Mrizka(p => p.AnotaceTextStyle.Underline = true, znacka: "xyz"));

        Assert.True(s > bez + PrirustekCary, $"podtržená značka má {s} tmavých pixelů, bez podtržení {bez}");
    }

    /// <summary>Čas v Tabulce spojů se sází po úsecích (hodina větší, minuty menší), ne jako buňka.</summary>
    [Fact]
    public void CasPoUsecich_Podtrzeni_SeNakresli()
    {
        int bez = Inkoust(Usekovy(underline: false));
        int s = Inkoust(Usekovy(underline: true));

        Assert.True(s > bez + PrirustekCary, $"podtržený čas má {s} tmavých pixelů, bez podtržení {bez}");
    }

    /// <summary>Pod úsekem s vlastní barvou (značka jedné verze linky v rozdílovém JŘ) je čára jeho barvou,
    /// jako pod barevným textem buňky - do 28. 10. byla v barvě textu buňky (`B194h` H5).</summary>
    [Fact]
    public void BarevnyUsek_Podtrzeni_MaJehoBarvu()
    {
        int bez = Cervene(Usekovy(underline: false, barvaMinut: "#FFFF0000"));
        int s = Cervene(Usekovy(underline: true, barvaMinut: "#FFFF0000"));

        Assert.True(s > bez + PrirustekCary, $"podtržený červený úsek má {s} červených pixelů, bez podtržení {bez}");
    }

    private static int Cervene(SKBitmap bmp)
    {
        using (bmp)
        {
            int n = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.Red > 180 && c.Green < 90 && c.Blue < 90) n++;
                }
            return n;
        }
    }

    private static SKBitmap Mrizka(Action<DepartureGridElement> nastav, string? znacka = null)
    {
        var prvek = new DepartureGridElement
        {
            X = 0, Y = 0, Width = 40, Height = 20,
            HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
            DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 10 },
            ShowSkupinaDnuHeader = false,
            Grid = new GridStyle { ShowRowLines = false, ShowColumnLines = false },
        };
        nastav(prvek);

        var odjezd = znacka is null
            ? RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15"))
            : RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15"),
                ("ANOTACE", RenderSnapshot.Nested([("TYP", "text"), ("ZNAK", znacka)])));
        var data = RenderSnapshot.Data(
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]),
            ("ODJEZDY", [odjezd]));

        var bmp = new SKBitmap((int)(prvek.Width * MmToPx), (int)(prvek.Height!.Value * MmToPx),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }

    private static SKBitmap Usekovy(bool underline, string? barvaMinut = null)
    {
        var styl = new TextStyle { FontFamily = "Arial", FontSize = 10, Underline = underline }
            .Resolve(CellDefaults.Default);
        List<SymbolRunRenderer.Run> useky = [new("16", false), new(" ", false, 0.75f),
            new("40", false, 0.75f, Barva: barvaMinut)];

        var bmp = new SKBitmap(200, 80, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        SymbolRunRenderer.Draw(new RenderContext(canvas, MmToPx, null), new SKRect(0, 0, 20, 8),
            useky, styl, SKTypeface.Default, 1f);
        return bmp;
    }

    private static int Inkoust(SKBitmap bmp)
    {
        using (bmp)
        {
            int n = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.Red < 100 && c.Green < 100 && c.Blue < 100) n++;
                }
            return n;
        }
    }
}
