using SkiaSharp;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;

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
            TableElement table => CalculateTableElementHeight(table, data),
            DynamicTableElement dynTable => dynTable.CalculateHeightFromData(data),
            StopListElement stopList => stopList.CalculateHeightFromData(data),
            DepartureGridElement grid => grid.CalculateHeightFromData(data),
            NotesLegendElement notes => notes.CalculateHeightFromData(data),
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

    private static float CalculateTableElementHeight(TableElement table, DataContext? data)
    {
        float headerH = table.ShowHeader ? table.HeaderHeight : 0f;
        float summaryH = table.SummaryRows is { Count: > 0 } ? table.SummaryRows.Count * table.RowHeight : 0f;

        if (table.Mode == TableMode.Fixed)
        {
            int visibleRowCount = table.Rows?.Count(r =>
                ExpressionEvaluator.IsVisible(r.VisibilityExpression, data)) ?? 0;
            return headerH + visibleRowCount * table.RowHeight + summaryH;
        }
        else
        {
            // Dynamic — počet řádků z dat
            var rows = data?.GetRows(table.DataSource ?? "") ?? [];
            int rowCount = rows.Count > 0 ? rows.Count : 3; // 3 ukázkových řádků v designeru
            return headerH + rowCount * table.RowHeight + summaryH;
        }
    }

}
