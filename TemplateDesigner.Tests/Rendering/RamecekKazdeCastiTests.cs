using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Rámeček kolem každé části tabulky spojů zvlášť.
/// </summary>
/// <remarks>
/// Linka, která se nevejde na šířku, se sází na několik částí pod sebou. Obecný rámeček prvku
/// obchází všechny najednou i mezeru mezi nimi, takže výsledek vypadá jako jedna tabulka s dírou
/// uprostřed (nález Jan 22. 9.). Každá část je přitom samostatná tabulka se svou hlavou.
/// </remarks>
public class RamecekKazdeCastiTests
{
    private const float MmToPx = 8f;

    /// <summary>Y-souřadnice (mm) všech vodorovných čar přes celou šířku prvku.</summary>
    private static List<float> VodorovneCaryMm(LineTableElement prvek, DataContext data)
    {
        float vyska = prvek.CalculateHeightFromData(data);
        int w = (int)MathF.Ceiling((prvek.X + prvek.Width + 4f) * MmToPx);
        int h = (int)MathF.Ceiling((prvek.Y + vyska + 4f) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            var ctx = new RenderContext(canvas, MmToPx, data);
            prvek.Render(ctx);
            prvek.RenderBorderPass(ctx);
        }

        int od = (int)((prvek.X + 1) * MmToPx), doX = (int)((prvek.X + prvek.Width - 1) * MmToPx);
        var cary = new List<float>();
        bool predchoziTmavy = false;
        for (int y = 0; y < h; y++)
        {
            int tmavych = 0;
            for (int x = od; x < doX; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red < 150 && c.Green < 150 && c.Blue < 150) tmavych++;
            }
            bool cara = tmavych > (doX - od) * 0.95;
            if (cara && !predchoziTmavy) cary.Add(y / MmToPx);
            predchoziTmavy = cara;
        }
        return cary;
    }

    /// <summary>
    /// ⚠️ Jádro: rozdělená tabulka má rámeček kolem každé části, tedy i vodorovnou čáru na konci
    /// první části a na začátku druhé. Dokud rámeček obcházel celý prvek, byly tam jen dvě.
    /// </summary>
    [Fact]
    public void KazdaCastMaSvujRamecek()
    {
        var prvek = Prvek();
        var data = Data(spoju: 40);
        var casti = prvek.Rozvrzeni(data);
        Assert.True(casti.Count > 1, "fixtura se musí rozdělit, jinak test nic neměří");

        var cary = VodorovneCaryMm(prvek, data);
        float konecPrvni = casti[0].Pocet > 0 ? prvek.CalculateHeightFromData(data) : 0f;

        // Rámeček každé části = čára na jejím začátku i konci; částí je N, čar tedy aspoň 2N.
        Assert.True(cary.Count >= casti.Count * 2,
            $"přes celou šířku vede {cary.Count} čar, pro {casti.Count} orámovaných částí "
            + $"jich má být aspoň {casti.Count * 2} (výška prvku {konecPrvni:0.0} mm)");
    }

    /// <summary>Nerozdělená tabulka se chová jako dosud - jeden rámeček kolem prvku.</summary>
    [Fact]
    public void NerozdelenaTabulkaMaJedenRamecek()
    {
        var prvek = Prvek();
        var data = Data(spoju: 4);
        Assert.Single(prvek.Rozvrzeni(data));

        var cary = VodorovneCaryMm(prvek, data);

        Assert.True(cary.Count >= 2, "rámeček prvku má dát horní i dolní čáru");
    }

    private static LineTableElement Prvek()
    {
        var prvek = new LineTableElement
        {
            X = 0, Y = 0, Width = 90, Height = null,
            BorderColor = "#FF000000", BorderWidth = 0.4f,
        };
        prvek.DataStyle.FontSize = 5f;
        return prvek;
    }

    private static DataContext Data(int spoju) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(1, spoju).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
