using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Plný blok s dorovnáním, do kterého se celý prázdný sloupec nevejde - co dostane zbytek šířky.
/// </summary>
/// <remarks>
/// Od 21. 9. ho celý dostal poslední spoj. Na 410110 (KV_DP) s formátem „hh.mm" zbyla skoro
/// celá šířka sloupce a poslední spoj vyšel téměř dvojnásobný (nález Jan 15. 10.). Pravidlo teď
/// je: spoj má vždy svou šířku a zbytek patří prázdným sloupcům. Aspoň půl sloupce = jeden užší
/// prázdný sloupec na konci, menší zbytek se rozdělí mezi spoje (rozhodnutí Jan 15. 10.).
/// <para>Fixtura nemá časy ani záhlaví, takže tmavé svislé pruhy přes většinu výšky jsou jen čáry mřížky.</para>
/// </remarks>
public class DorovnaniZbytkuTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 30f;
    private const float SirkaSpoje = 10f;

    [Fact]
    public void ZbytekAsponPulSloupce_UzsiPrazdnySloupecNaKonci()
    {
        // Tři spoje po 10 mm a 8 mm navíc: prázdný sloupec se nevejde, zbytek je přes půl sloupce.
        var cary = SvisleCaryMm(Prvek(SirkaZastavky + 3.8f * SirkaSpoje));

        Assert.True(MaCaru(cary, 60f),
            $"poslední spoj nekončí na 60 mm, zbytek spolkl on - čáry: {Vypis(cary)}");
        Assert.True(MaCaru(cary, 50f), $"spoje nemají svou šířku - čáry: {Vypis(cary)}");
    }

    [Fact]
    public void ZbytekMensiNezPulSloupce_RozdeliSeMeziSpoje()
    {
        // 3 mm navíc: každý ze tří spojů o milimetr širší, žádný úzký proužek.
        var cary = SvisleCaryMm(Prvek(SirkaZastavky + 3.3f * SirkaSpoje));

        Assert.True(MaCaru(cary, 41f) && MaCaru(cary, 52f),
            $"zbytek se nerozdělil rovnoměrně mezi spoje - čáry: {Vypis(cary)}");
        Assert.False(MaCaru(cary, 50f), $"poslední spoj je širší než ostatní - čáry: {Vypis(cary)}");
    }

    private static LineTableElement Prvek(float sirkaMm) => new()
    {
        X = 0, Y = 0, Width = sirkaMm, Height = null,
        PadEmptyColumns = true,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        ShowGroupHeaders = false,
        ShowTripNumbers = false,
        ShowPlatnostRow = false,
        ShowNzRow = false,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = false, Color = "#FF000000", Width = 0.2f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            // Názvy musí být neprázdné, jinak prvek sloupec zastávek schová a do šířky se vejdou
            // celé prázdné sloupce. Písmo zabere jen zlomek výšky, takže se za čáru nepočítá.
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [
            .. Enumerable.Range(1, 3).Select(_ => RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"),
                ("CASY", new JsonArray("", "", "")))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));

    /// <summary>Středy svislých čar mřížky (mm) - sloupce obrázku, kde je tmavá většina výšky tabulky.</summary>
    private static List<float> SvisleCaryMm(LineTableElement prvek)
    {
        var data = Data();
        int w = (int)((prvek.Width + 4f) * MmToPx);
        int h = (int)(prvek.CalculateHeightFromData(data) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        var tmave = new int[w];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red < 100 && c.Green < 100 && c.Blue < 100) tmave[x]++;
            }

        var cary = new List<float>();
        int prah = h / 2;
        for (int x = 0; x < w; x++)
        {
            if (tmave[x] < prah) continue;
            int od = x;
            while (x + 1 < w && tmave[x + 1] >= prah) x++;
            cary.Add((od + x) / 2f / MmToPx);
        }
        return cary;
    }

    private static bool MaCaru(List<float> cary, float mm) => cary.Any(c => MathF.Abs(c - mm) < 0.4f);

    private static string Vypis(List<float> cary) => string.Join(", ", cary.Select(c => c.ToString("0.0")));
}
