using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Autoritativní výchozí hodnoty pro buňku tabulky.
/// Poslední vrstva kaskády: column.Style → element.HeaderStyle/DataStyle → CellDefaults.
/// Změna zde se projeví ve všech tabulkových elementech.
/// </summary>
public static class CellDefaults
{
    // Getter (ne static readonly) - aby se přes DocumentDefaults.Apply propsal výchozí styl
    // šablony (font…) i do tabulkové kaskády. Hodnota = zapečené defaulty overlaynuté ambientem.
    public static ResolvedTextStyle Default => DocumentDefaults.Apply(new()
    {
        FontFamily      = "Arial",
        FontSize        = 9f,
        Bold            = false,
        Italic          = false,
        Underline       = false,
        Strikethrough   = false,
        TextColor       = "#FF000000",
        BackgroundColor = "#00FFFFFF",
        HAlign          = TextHorizontalAlignment.Left,
        VAlign          = TextVerticalAlignment.Middle,
        PaddingTop      = 0.5f,
        PaddingRight    = 0.5f,
        PaddingBottom   = 0.5f,
        PaddingLeft     = 0.5f,
        LineSpacing     = 1.2f,
    });
}
