using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Strom šablony mluví jmény z palety a formuláře sekce, ne názvy tříd. Do 26. 10. ukazoval
/// „[Z0] DepartureGrid: Odjezdy auto", „[OnEveryPage]" a id skupiny (`B194` E1).
/// </summary>
public class TemplateTreeNamesTests
{
    [Fact]
    public void SablonaAStranka()
    {
        var sablona = new Template
        {
            Name = "Linkový JŘ AD",
            PageSize = new PageSize { Width = 210, Height = 297, MarginLeft = 10, MarginTop = 8, MarginRight = 10, MarginBottom = 12.5f },
        };

        Assert.Equal("Šablona: Linkový JŘ AD", TemplateTreeNames.Sablona(sablona));
        Assert.Equal("Stránka: 210 × 297 mm, okraje vlevo 10, nahoře 8, vpravo 10, dole 12,5 mm",
            TemplateTreeNames.Stranka(sablona.PageSize));
    }

    [Fact]
    public void SekceUkazujeTypZFormulareAVysku()
    {
        var zahlavi = new Section { Name = "Záhlaví", DesignerHeight = 25 };
        zahlavi.Typ = TypSekce.HlavickaStranky;
        var obsah = new Section { Name = "Obsah", DesignerHeight = null };

        Assert.Equal("Záhlaví - Hlavička stránky, 25 mm", TemplateTreeNames.Sekce(zahlavi));
        // sekce pojmenovaná jako její typ ho neopakuje
        Assert.Equal("Obsah, výška podle obsahu", TemplateTreeNames.Sekce(obsah));
    }

    [Theory]
    [InlineData(1, "Skupina (1 prvek)")]
    [InlineData(3, "Skupina (3 prvky)")]
    [InlineData(5, "Skupina (5 prvků)")]
    public void SkupinaBezInternihoId(int pocet, string ocekavany) =>
        Assert.Equal(ocekavany, TemplateTreeNames.Skupina(pocet));

    [Fact]
    public void PrvekJmenemZPalety()
    {
        Assert.Equal("Minuty odjezdů", TemplateTreeNames.Prvek(new DepartureGridElement()));
        Assert.Equal("Tabulka spojů", TemplateTreeNames.Prvek(new LineTableElement()));
        Assert.Equal("Obdélník", TemplateTreeNames.Prvek(new RectangleElement()));
        Assert.Equal("Text „Linka {CISLO_LINKY}“", TemplateTreeNames.Prvek(new TextElement { Text = "Linka {CISLO_LINKY}" }));
    }
}
