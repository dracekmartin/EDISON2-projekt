namespace TemplateDesigner.Models;

/// <summary>
/// String identifikátory typů elementů v JSON serializaci (JsonDerivedType discriminator) - atribut
/// potřebuje konstantu. Všechno ostatní o prvku (jméno, paleta, továrna, agendy) drží <see cref="ElementCatalog"/>.
/// Hodnoty musí odpovídat JSON wire formátu (šablony v DB/souborech) - neměnit bez migrace.
/// </summary>
public static class ElementTypes
{
    public const string Text          = "TextElement";
    public const string Rectangle     = "RectangleElement";
    public const string Line          = "LineElement";
    public const string Image         = "ImageElement";
    public const string Table         = "TableElement";
    public const string DynamicTable  = "DynamicTableElement";
    public const string Barcode       = "BarcodeElement";
    public const string StopList      = "StopListElement";
    public const string DepartureGrid = "DepartureGridElement";
    public const string NotesLegend   = "NotesLegendElement";
    public const string LineTable     = "LineTableElement";
}
