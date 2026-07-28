using TemplateDesigner;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Zámek továrny elementů. Hlídá zejména <b>spící Fixed tabulku</b> (<see cref="TableElement"/>):
/// nesmí jít vytvořit z UI/továrny - živá je <see cref="DynamicTableElement"/>. Když někdo Fixed
/// tabulku oživí (vrátí větev do <c>CreateElementByType</c>), tento test spadne a připomene, že
/// vědomě odmrazuje typ (schéma musí být záměrné) - viz TableElement docstring.
/// </summary>
public class ElementFactoryTests
{
    [Fact]
    public void FixedTable_NelzeVytvoritZTovarny()
    {
        var el = TemplateDesignerViewModel.CreateElementByType(ElementTypes.Table, 0, 0);
        Assert.IsNotType<TableElement>(el);
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
