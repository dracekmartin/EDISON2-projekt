using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// `B158` - záhlaví Minut odjezdů jde nastavit po sloupcích. Dialog to nabízel, ale kreslení to
/// od 14. 6. ignorovalo: typografie záhlaví se brala vždy z prvku, takže font, velikost i řez
/// u skupiny dnů nic nedělaly. Hlásil TRANSDEV 16. 9.
/// </summary>
/// <remarks>
/// Pruh záhlaví je jeden pro celou mřížku, proto jeho výšku určuje největší písmo napříč sloupci -
/// stejně jako u seznamu zastávek. Bez toho by se větší písmo oříznulo o výšku počítanou z prvku.
/// </remarks>
public class ZahlaviOdjezduPerSloupecTests
{
    private const float MmToPx = 10f;
    private const float SirkaMm = 90f;

    private static DataContext Data() => RenderSnapshot.Data(
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota"), ("PORADI", "2")),
        ]),
        ("ODJEZDY",
        [
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15")),
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "2"), ("HODINA", "6"), ("MINUTA", "40")),
        ]));

    private static DepartureGridElement Prvek(params DepartureGroupColumnStyle[] sloupce) => new()
    {
        X = 0, Y = 0, Width = SirkaMm,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        ShowSkupinaDnuHeader = true,
        GroupColumns = [.. sloupce],
    };

    private static DepartureGroupColumnStyle Sloupec(string id, TextStyle? zahlavi = null) => new()
    {
        SkupinaDnuId = id,
        IsVisible = true,
        Style = new ColumnVisualStyle { Header = zahlavi },
    };

    [Fact]
    public void VetsiPismoJednohoSloupce_ZvysiPruhZahlavi()
    {
        var data = Data();
        float bezOverride = Prvek(Sloupec("1"), Sloupec("2")).CalculateHeightFromData(data);
        float sVetsim = Prvek(Sloupec("1", new TextStyle { FontSize = 20f }), Sloupec("2"))
            .CalculateHeightFromData(data);

        Assert.True(sVetsim > bezOverride,
            $"pruh záhlaví se má přizpůsobit nejvyššímu sloupci (bez={bezOverride:0.##}, s={sVetsim:0.##})");
    }

    [Fact]
    public void MensiPismoJednohoSloupce_PruhNezmensi()
    {
        // Druhý sloupec drží velikost prvku, takže se pruh nesmí smrsknout pod něj.
        var data = Data();
        float bezOverride = Prvek(Sloupec("1"), Sloupec("2")).CalculateHeightFromData(data);
        float sMensim = Prvek(Sloupec("1", new TextStyle { FontSize = 3f }), Sloupec("2"))
            .CalculateHeightFromData(data);

        Assert.Equal(bezOverride, sMensim, precision: 3);
    }

    [Fact]
    public void PerSloupcoveZahlavi_SeOpravduVykresli()
    {
        var bez = Inkoust(Prvek(Sloupec("1"), Sloupec("2")));
        var s = Inkoust(Prvek(Sloupec("1", new TextStyle { FontSize = 20f }), Sloupec("2")));

        Assert.True(s > bez, $"větší písmo sloupce se má projevit na kresbě (bez={bez}, s={s})");
    }

    [Fact]
    public void BezPerSloupcovehoNastaveni_RozhodujePrvek()
    {
        var data = Data();
        var maly = Prvek(Sloupec("1"), Sloupec("2"));
        var velky = Prvek(Sloupec("1"), Sloupec("2"));
        velky.HeaderStyle.FontSize = 20f;

        Assert.True(velky.CalculateHeightFromData(data) > maly.CalculateHeightFromData(data),
            "ovladač velikosti záhlaví v panelu prvku musí fungovat dál");
    }

    /// <summary>Kolik tmavých pixelů prvek nakreslí - hrubá míra toho, že se kresba změnila.</summary>
    private static int Inkoust(DepartureGridElement prvek)
    {
        var data = Data();
        int w = (int)((SirkaMm + 4f) * MmToPx);
        int h = (int)(70f * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));

        int pocet = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.Red < 160 && p.Green < 160 && p.Blue < 160) pocet++;
            }
        return pocet;
    }
}
