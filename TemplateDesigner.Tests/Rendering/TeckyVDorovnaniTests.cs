using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Tečky v prázdných buňkách se sázejí i do <b>prázdných sloupců dorovnání</b>.
/// </summary>
/// <remarks>
/// Starý postskript jimi drží svislé vodicí pruhy přes celou šířku tabulky. Dokud je dorovnané
/// sloupce neměly, byla na jejich místě v každé řadě díra a tabulka končila „prázdnem"
/// (rozhodnutí Jan 22. 9.).
/// </remarks>
public class TeckyVDorovnaniTests
{
    private const float MmToPx = 8f;

    /// <summary>Kolik tmavých pixelů je v pravé části prvku, tedy tam, kde stojí dorovnané sloupce.</summary>
    private static int TmavychVpravo(LineTableElement prvek, DataContext data)
    {
        float vyska = prvek.CalculateHeightFromData(data);
        int w = (int)MathF.Ceiling((prvek.X + prvek.Width + 4f) * MmToPx);
        int h = (int)MathF.Ceiling((prvek.Y + vyska + 4f) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        int od = (int)((prvek.X + prvek.Width * 0.75f) * MmToPx);
        int doX = (int)((prvek.X + prvek.Width) * MmToPx);
        int tmavych = 0;
        for (int y = 0; y < h; y++)
            for (int x = od; x < doX; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red < 120 && c.Green < 120 && c.Blue < 120) tmavych++;
            }
        return tmavych;
    }

    /// <summary>⚠️ Jádro: se zapnutými tečkami je v dorovnaných sloupcích víc kresby než bez nich.</summary>
    [Fact]
    public void TeckySeSazejiIDoDorovnanychSloupcu()
    {
        var data = Data(spoju: 6);

        int bez = TmavychVpravo(Prvek(tecky: false), data);
        int s = TmavychVpravo(Prvek(tecky: true), data);

        Assert.True(s > bez + 20,
            $"s tečkami {s} tmavých pixelů, bez nich {bez} - dorovnané sloupce zůstaly prázdné");
    }

    /// <summary>Bez dorovnání se nic nemění - tečky patří do sloupců, ne do volného místa.</summary>
    [Fact]
    public void BezDorovnaniTeckyNeprisou()
    {
        var data = Data(spoju: 6);
        var bezDorovnani = Prvek(tecky: true);
        bezDorovnani.PadEmptyColumns = false;
        var referenceBezTecek = Prvek(tecky: false);
        referenceBezTecek.PadEmptyColumns = false;

        // Tečky v buňkách spojů zůstávají, ale vpravo od poslední sazby nesmí přibýt nic.
        Assert.Equal(TmavychVpravo(referenceBezTecek, data), TmavychVpravo(bezDorovnani, data));
    }

    private static LineTableElement Prvek(bool tecky)
    {
        var prvek = new LineTableElement
        {
            X = 0, Y = 0, Width = 200, Height = null,
            PadEmptyColumns = true,
            VyplnitPrazdneBunkySpoju = tecky,
        };
        prvek.DataStyle.FontSize = 5f;
        return prvek;
    }

    /// <summary>Málo spojů, takže se do prvku vejdou i dorovnané sloupce; časy jen na první
    /// zastávce, aby byly v tabulce prázdné buňky.</summary>
    private static DataContext Data(int spoju) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(1, spoju).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "", ""))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
