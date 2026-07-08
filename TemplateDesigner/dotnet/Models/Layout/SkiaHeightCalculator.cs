using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Implementace IElementHeightCalculator pomocí SkiaSharp — měří text/tabulky.
/// </summary>
public class SkiaHeightCalculator : IElementHeightCalculator
{
    public float CalculateHeight(TemplateElement element, float availableWidthMm, DataContext? data)
    {
        // Statické elementy — pevná výška
        if (element.Height.HasValue)
            return element.Height.Value;

        return element switch
        {
            TextElement te => CalculateTextElementHeight(te, data),
            TableElement table => table.CalculateHeightFromData(data),
            DynamicTableElement dynTable => dynTable.CalculateHeightFromData(data),
            StopListElement stopList => stopList.CalculateHeightFromData(data),
            DepartureGridElement grid => grid.CalculateHeightFromData(data),
            NotesLegendElement notes => notes.CalculateHeightFromData(data),
            LineTableElement lineTable => lineTable.CalculateHeightFromData(data),
            _ => element.EffectiveDesignerHeight
        };
    }

    private static float CalculateTextElementHeight(TextElement te, DataContext? data)
    {
        if (!te.WordWrap || te.Width <= 0)
            return te.EffectiveDesignerHeight;

        var resolvedText = data?.ResolvePlaceholders(te.Text) ?? te.Text;
        return te.CalculateTextHeight(resolvedText);
    }
}
