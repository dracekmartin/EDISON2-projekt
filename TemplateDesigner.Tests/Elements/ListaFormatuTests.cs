using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Lišta formátu (tučné, kurzíva…, zarovnání) hledá vlastnosti podle tlačítka, které ovládají
/// (<see cref="PropertyDescriptor.ToolbarSlot"/>), ne podle popisku a skupiny. Do 30. 10. by
/// přejmenování popisku nebo skupiny lištu u textu i u buňky pevné tabulky tiše vypnulo.
/// </summary>
public class ListaFormatuTests
{
    private static readonly FormatToolbarSlot[] VsechnaTlacitka =
    [
        FormatToolbarSlot.Bold, FormatToolbarSlot.Italic, FormatToolbarSlot.Underline,
        FormatToolbarSlot.Strikethrough, FormatToolbarSlot.HorizontalAlignment, FormatToolbarSlot.VerticalAlignment,
    ];

    private static void MaVsechnaTlacitka(IEnumerable<PropertyDescriptor> vlastnosti)
    {
        var tlacitka = vlastnosti.Where(d => d.ToolbarSlot != FormatToolbarSlot.None).ToList();

        Assert.Equal(VsechnaTlacitka, tlacitka.Select(d => d.ToolbarSlot).Order());
        Assert.All(tlacitka, d => Assert.IsType(
            d.ToolbarSlot is FormatToolbarSlot.HorizontalAlignment or FormatToolbarSlot.VerticalAlignment
                ? typeof(EnumPropertyDescriptor) : typeof(BoolPropertyDescriptor), d));
    }

    [Fact]
    public void Text_OvladaVsechnaTlacitka() =>
        MaVsechnaTlacitka(new TextElement().GetPropertyDescriptors());

    [Fact]
    public void BunkaPevneTabulky_OvladaVsechnaTlacitka() =>
        MaVsechnaTlacitka(new TableCellFormatting(TableElement.CreateGrid(2, 2), () => null, [(0, 0)]).Descriptors());

    /// <summary>Dvě vlastnosti na jednom tlačítku by lišta rozlišila jen pořadím. Tučné přípojných
    /// řádků Tabulky spojů je část prvku, ne jeho text - na lištu nepatří.</summary>
    [Fact]
    public void KazdyPrvek_MaTlacitkoNejvysJednou()
    {
        foreach (var prvek in ElementCatalog.All)
        {
            var dvakrat = ElementCatalog.Create(prvek.Type, 0, 0).GetPropertyDescriptors()
                .Where(d => d.ToolbarSlot != FormatToolbarSlot.None)
                .GroupBy(d => d.ToolbarSlot).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.Empty(dvakrat);
        }
    }
}
