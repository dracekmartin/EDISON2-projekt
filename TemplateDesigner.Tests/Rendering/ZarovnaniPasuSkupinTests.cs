using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Pás skupin dnů Tabulky spojů („Pracovní dny" nad sloupci spojů) poslouchá Zarovnání vodorovně
/// ze stylu Záhlaví. Do 28. 10. byl natvrdo na střed i s vyplněným zarovnáním - pole, které
/// formulář nabízí, by tam nic nedělalo (`B194` E3). Prázdné zarovnání dál znamená na střed.
/// </summary>
/// <remarks>Měří se nejlevější tmavý pixel ve sloupci spoje. Čísla spojů a řádky značek jsou
/// vypnuté, takže tam stojí jen nadpis skupiny a časy (ty jsou na střed v obou případech).</remarks>
public class ZarovnaniPasuSkupinTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 30f;

    [Fact]
    public void BezZarovnaniVeStylu_JePasNaStred()
    {
        int x = NejlevejsiInkoustVeSpoji(Prvek(null));

        Assert.True(x > (SirkaZastavky + 5f) * MmToPx, $"nadpis skupiny začíná na {x} px, čekal se na střed sloupce");
    }

    [Fact]
    public void ZarovnaniVlevo_PosunePasKLevemuOkraji()
    {
        int naStred = NejlevejsiInkoustVeSpoji(Prvek(null));
        int vlevo = NejlevejsiInkoustVeSpoji(Prvek(TextHorizontalAlignment.Left));

        Assert.True(vlevo < naStred - 5f * MmToPx, $"zarovnaný vlevo začíná na {vlevo} px, na střed na {naStred} px");
    }

    private static LineTableElement Prvek(TextHorizontalAlignment? zarovnani) => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        JednotnaSirkaSpoju = false,
        ShowTripNumbers = false, ShowPlatnostRow = false, ShowNzRow = false,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8, HAlign = zarovnani },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.3f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Width = 40, Visible = true,
                Alignment = TextHorizontalAlignment.Center },
        ],
    };

    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "A"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "B"), ("PORADI", "2")),
        ]),
        // Pás se kreslí jen se dvěma a víc skupinami (`HasGroupHeaderBand`); měří se ve sloupci první.
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("CASY", new JsonArray("8:00", "8:20"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "2"), ("CASY", new JsonArray("9:00", "9:20"))),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota"), ("PORADI", "2")),
        ]));

    private static int NejlevejsiInkoustVeSpoji(LineTableElement prvek)
    {
        var data = Data();
        float vyska = prvek.CalculateHeightFromData(data);
        int w = (int)MathF.Ceiling((prvek.Width + 4f) * MmToPx);
        int h = (int)MathF.Ceiling((vyska + 4f) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        int od = (int)(SirkaZastavky * MmToPx) + 3;
        for (int x = od; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red < 100 && c.Green < 100 && c.Blue < 100) return x;
            }
        return w;
    }
}
