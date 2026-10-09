using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Přípojný řádek jde odlišit od časů spojů (KU HK `#8360`): velikostí, řezem, barvou textu
/// a pozadím řádku - s bílým písmem na tmavém pozadí vznikne inverzní řádek, o který si zákazník
/// řekl vedle velikosti a řezu.
/// </summary>
public class PripojnyRadekVzhledTests
{
    private const float MmToPx = 96f / 25.4f;

    private static DataContext Linka() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Český Krumlov"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Kaplice, rozcestí"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"),
                ("PRIPOJE", RenderSnapshot.Nested(
                    [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                      ("CAS", "10:25"), ("LINKA", "330520"), ("TEXT", "Kaplice")]]))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120,
        DataRowHeight = 5f,
        ShowPripojeRows = true,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowGroupHeaders = false,
    };

    /// <summary>Kolik tmavých pixelů je v pásu přípojného řádku - je v tabulce poslední.</summary>
    private static int TmavychVPripojnemRadku(LineTableElement prvek)
    {
        var data = Linka();
        float vyska = prvek.CalculateHeightFromData(data);
        using var bitmap = new SKBitmap((int)(120 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }
        int tmavych = 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = (int)((vyska - 4.5f) * MmToPx); y < (int)((vyska - 0.5f) * MmToPx); y++)
                if (bitmap.GetPixel(x, y).Red < 64) tmavych++;
        return tmavych;
    }

    [Fact]
    public void PozadiRadku_PodbarviCelyPripojnyRadek()
    {
        var bez = Prvek();
        var inverzni = Prvek();
        inverzni.PripojStyle.BackgroundColor = "#FF000000";
        inverzni.PripojStyle.TextColor = "#FFFFFFFF";

        int sPozadim = TmavychVPripojnemRadku(inverzni);
        int bezPozadi = TmavychVPripojnemRadku(bez);

        // Bez pozadí jsou v pásu tmavé jen znaky popisku a času, s pozadím celý řádek přes popis
        // i spoje (ověřeno okem na výstupu 9. 10.).
        Assert.True(sPozadim > 5 * bezPozadi, $"s pozadím {sPozadim} px, bez {bezPozadi} px");
    }

    [Fact]
    public void FormularNabiziPozadiRadku()
    {
        Assert.Contains(Prvek().GetPropertyDescriptors(),
            d => d.Label == "Pozadí řádku" && d.Group == "Přípojné řádky");
    }
}
