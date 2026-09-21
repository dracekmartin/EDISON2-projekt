using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Čára u zastávky z atributů trasy `p` a `o` - `B172`.
/// </summary>
/// <remarks>
/// Dispečer jimi odděluje úsek linky a obě staré cesty je tisknou: Windows tisk silnou čarou přes
/// celou šířku tabulky (`JRapiLinPrtWin.mac:1259`), postskript vlastním sloupcem podtržení
/// (`JRapiLinPrtPS1.mac:110`). Používá je 12 tisíc linek z 15 tisíc u KV_DP (změřeno 21. 9.).
/// </remarks>
public class CaraUZastavkyTests
{
    private const float MmToPx = 8f;
    private const float VyskaRadku = 6f;

    [Fact]
    public void AtributPNakresliCaruPodRadkem()
    {
        Assert.True(TmavychRadku(Prvek(), Data("p")) > 0, "čára pod zastávkou se nenakreslila");
    }

    /// <summary>Bez atributu se nekreslí nic - jinak by čára přibyla všem hotovým sestavám.</summary>
    [Fact]
    public void BezAtributuZadnaCaraNeni()
    {
        Assert.Equal(0, TmavychRadku(Prvek(), Data(null)));
    }

    /// <summary>„Podtrženo nad i pod" (`o`) dá čáry dvě, ne jednu.</summary>
    [Fact]
    public void AtributONakresliCaryDve()
    {
        Assert.True(TmavychRadku(Prvek(), Data("o")) > TmavychRadku(Prvek(), Data("p")),
            "atribut o má nakreslit čáru nad řádkem i pod ním");
    }

    /// <summary>Vypínač prvku čáru z trasy potlačí.</summary>
    [Fact]
    public void VypnutyPrepinacCaruPotlaci()
    {
        var prvek = Prvek();
        prvek.CaryZastavekDleTrasy = false;

        Assert.Equal(0, TmavychRadku(prvek, Data("p")));
    }

    /// <summary>Kolik obrazových řádků je souvisle tmavých přes celou šířku tabulky.</summary>
    /// <remarks>Mřížka i text jsou ve fixtuře bílé, takže tmavý je jen to, co kreslí čára úseku.</remarks>
    private static int TmavychRadku(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 4f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 6f) * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        int radku = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            int tmavych = 0;
            for (int x = 0; x < (int)(prvek.Width * MmToPx); x++)
                if (JeInkoust(bmp.GetPixel(x, y))) tmavych++;
            if (tmavych > prvek.Width * MmToPx * 0.8f) radku++;
        }
        return radku;
    }

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    private static LineTableElement Prvek() => new()
    {
        // Šířka = součet sloupců, ať tabulka vyplní prvek a čára jde přes celou měřenou šířku.
        X = 0, Y = 0, Width = 45, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7, TextColor = "#FFFFFFFF" },
        // Text i mřížka bílé: v obrázku má být tmavá jen čára úseku.
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6, TextColor = "#FF000000" },
        HighlightOddTc = false,
        ShowGroupHeaders = false,
        ShowPlatnostRow = false,
        DataRowHeight = VyskaRadku,
        Grid = new GridStyle { ShowColumnLines = false, ShowRowLines = false, Color = "#FFFFFFFF", Width = 0.2f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = 30, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = 15, Visible = true },
        ],
    };

    private static DataContext Data(string? atributProstredni)
    {
        DataContext Zastavka(int poradi, string? atr) => atr is null
            ? RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Zastávka " + poradi), ("PORADI", poradi.ToString()))
            : RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Zastávka " + poradi), ("PORADI", poradi.ToString()),
                ("ATRIBUTY", new JsonArray(atr)));

        return RenderSnapshot.Data(
            ("ZASTAVKY", [Zastavka(1, null), Zastavka(2, atributProstredni), Zastavka(3, null)]),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "T"),
                    ("CASY", new JsonArray("", "", ""))),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }
}
