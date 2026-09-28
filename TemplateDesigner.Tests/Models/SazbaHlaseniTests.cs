using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Models;

/// <summary>`B178`: co sazba sama upravila nebo nesplnila, se musí dozvědět i uživatel.</summary>
public class SazbaHlaseniTests
{
    private static (Template Sablona, LayoutResult Sazba) Vysad(DataContext data, params Section[] sekce)
    {
        var sablona = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 100, MarginTop = 0, MarginBottom = 0 },
            Sections = [.. sekce],
        };
        return (sablona, new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data));
    }

    /// <summary>Nová stránka má přednost před Počtem stran - a hláška řekne proč.</summary>
    [Fact]
    public void PocetStranNesplnenKvuliNoveStrance_JeUpozorneniSPricinou()
    {
        var data = new DataContext(new Dictionary<string, object?> { [PageLayoutEngine.CilStranKlic] = "1 strana" });
        var (sablona, sazba) = Vysad(data,
            new Section { Name = "Tabulka", DesignerHeight = 30 },
            new Section { Name = "Poznámky", DesignerHeight = 20, ZacitNaNoveStrance = true });

        var h = Assert.Single(SazbaHlaseni.Seznam(sablona, sazba));

        Assert.Equal(ZavaznostHlaseni.Upozorneni, h.Zavaznost);
        Assert.Contains("Počet stran 1", h.Text);
        Assert.Contains("„Poznámky“ začíná na nové stránce", h.Text);
    }

    [Fact]
    public void NecitelnaPodminka_JeUpozorneni()
    {
        var prvek = new TextElement { X = 0, Y = 0, Width = 50, Height = 5, Text = "x", VisibilityExpression = "{A} >" };
        var (sablona, sazba) = Vysad(new DataContext(new Dictionary<string, object?>()),
            new Section { Name = "Obsah", DesignerHeight = 20, Elements = [prvek], VisibilityExpression = "((" });

        var hlaseni = SazbaHlaseni.Seznam(sablona, sazba);

        Assert.Equal(2, hlaseni.Count);
        Assert.All(hlaseni, h => Assert.Equal(ZavaznostHlaseni.Upozorneni, h.Zavaznost));
        Assert.Contains(hlaseni, h => h.PrvekId == prvek.Id);
    }

    [Fact]
    public void BezUprav_ZadnaHlaska()
    {
        var (sablona, sazba) = Vysad(new DataContext(new Dictionary<string, object?>()),
            new Section { Name = "Obsah", DesignerHeight = 20 });

        Assert.Empty(SazbaHlaseni.Seznam(sablona, sazba));
    }

    [Fact]
    public void ZmensenaTabulka_JeInformace()
    {
        var sazba = new LayoutResult { ZmenseniTabulky = 0.94f };

        var h = Assert.Single(SazbaHlaseni.Seznam(new Template(), sazba));

        Assert.Equal(ZavaznostHlaseni.Informace, h.Zavaznost);
        Assert.Contains("94 %", h.Text);
    }
}
