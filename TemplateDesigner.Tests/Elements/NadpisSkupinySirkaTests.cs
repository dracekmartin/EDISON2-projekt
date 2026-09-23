using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Nadpis skupiny dnů se nesmí uříznout: sloupce skupiny musí být dohromady aspoň tak široké
/// jako nadpis nad nimi (nález Jan 23. 9. na 330517, „soboty, neděle a svátky" nad dvěma spoji).
/// </summary>
public class NadpisSkupinySirkaTests
{
    private const string DlouhyNadpis = "soboty, neděle a svátky";

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 180, Height = null,
        JednotnaSirkaSpoju = false,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
    };

    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Český Krumlov"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Frymburk"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("CASY", new JsonArray("6:10", "6:50"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "3"), ("SKUPINA", "1"), ("CASY", new JsonArray("8:10", "8:50"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "5"), ("SKUPINA", "2"), ("CASY", new JsonArray("13:35", "14:15"))),
            RenderSnapshot.Row(("CISLO_SPOJE", "23"), ("SKUPINA", "2"), ("CASY", new JsonArray("15:55", "16:35"))),
        ]),
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", DlouhyNadpis), ("PORADI", "2")),
        ]));

    [Fact]
    public void SloupceSkupiny_JsouAsponTakSirokeJakoJejiNadpis()
    {
        var el = Prvek();
        var blok = Assert.Single(el.Rozvrzeni(Data()));

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 8f);
        float nadpisMm = font.MeasureText(DlouhyNadpis) * 0.3528f;

        Assert.True(blok.SirkySloupcu[2] + blok.SirkySloupcu[3] >= nadpisMm,
            $"skupina má {blok.SirkySloupcu[2] + blok.SirkySloupcu[3]:0.00} mm, nadpis {nadpisMm:0.00} mm");
    }

    /// <summary>Krátký nadpis sloupce nerozšíří - šířka dál vychází z časů.</summary>
    [Fact]
    public void KratkyNadpis_SirkuNemeni()
    {
        var blok = Assert.Single(Prvek().Rozvrzeni(Data()));

        Assert.Equal(blok.SirkySloupcu[0], blok.SirkySloupcu[1], 3);
        Assert.True(blok.SirkySloupcu[0] < blok.SirkySloupcu[2]);
    }
}
