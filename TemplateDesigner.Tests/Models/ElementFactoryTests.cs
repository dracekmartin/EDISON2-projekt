using TemplateDesigner;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Zámek továrny elementů - každý typ z palety musí vyrobit přesně svou třídu (továrna má
/// <c>_ =&gt; TextElement</c> fallback, takže překlep by tiše vrátil textový prvek).
/// Pevná tabulka má navíc vlastní test na to, s čím se vloží: bez mřížky a řádků by uživatel
/// dostal neviditelný prvek, protože tabulka bez sloupců nemá co kreslit.
/// </summary>
public class ElementFactoryTests
{
    [Fact]
    public void PevnaTabulka_SeVlozíJakoVyplnitelnaMrizka()
    {
        var el = Assert.IsType<TableElement>(
            TemplateDesignerViewModel.CreateElementByType(ElementTypes.Table, 0, 0));

        Assert.Equal(TableMode.Fixed, el.Mode);
        Assert.Equal(3, el.Columns.Count);
        Assert.Equal(3, el.Rows?.Count);
        Assert.All(el.Rows!, r => Assert.Equal(3, r.Cells.Count));

        // Bez viditelných čar by prázdná mřížka nebyla poznat.
        Assert.True(el.Grid.ShowRowLines);
        Assert.True(el.Grid.ShowColumnLines);

        // Pevná výška je podmínka toho, aby šlo za tabulku táhnout do výšky - prvek bez výšky
        // platí za dynamický a dostane od plátna jen boční úchyty.
        Assert.Equal(3 * el.RowHeight, el.Height);

        // Řádek záhlaví se u pevné tabulky vědomě nenabízí - záhlaví si udělá uživatel prvním řádkem.
        Assert.False(el.ShowHeader);
    }

    [Fact]
    public void Tabulka_ZTovarny_JeDynamicka()
    {
        var el = TemplateDesignerViewModel.CreateElementByType(ElementTypes.DynamicTable, 0, 0);
        Assert.IsType<DynamicTableElement>(el);
    }

    [Theory]
    [InlineData(ElementTypes.Text, typeof(TextElement))]
    [InlineData(ElementTypes.Rectangle, typeof(RectangleElement))]
    [InlineData(ElementTypes.Line, typeof(LineElement))]
    [InlineData(ElementTypes.Image, typeof(ImageElement))]
    [InlineData(ElementTypes.Barcode, typeof(BarcodeElement))]
    [InlineData(ElementTypes.Table, typeof(TableElement))]
    [InlineData(ElementTypes.StopList, typeof(StopListElement))]
    [InlineData(ElementTypes.DepartureGrid, typeof(DepartureGridElement))]
    [InlineData(ElementTypes.NotesLegend, typeof(NotesLegendElement))]
    [InlineData(ElementTypes.LineTable, typeof(LineTableElement))]
    public void ZiveTypy_VytvoriSpravnyElement(string type, System.Type expected)
    {
        var el = TemplateDesignerViewModel.CreateElementByType(type, 0, 0);
        Assert.IsType(expected, el);
    }
}
