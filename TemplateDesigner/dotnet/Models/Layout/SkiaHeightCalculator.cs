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
            _ => element.EffectiveDesignerHeight
        };
    }

    private static float CalculateTextElementHeight(TextElement te, DataContext? data)
    {
        if (!te.WordWrap || te.Width <= 0)
            return te.EffectiveDesignerHeight;

        var resolvedText = data?.ResolvePlaceholders(te.Text) ?? te.Text;
        if (string.IsNullOrEmpty(resolvedText))
            return 8f;

        var typeface = SKTypeface.FromFamilyName(
            te.FontFamily,
            te.FontWeight == TextFontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            te.FontStyle == TextFontStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        float fontSizeMm = te.FontSize * 0.3528f;
        const float refMmToPx = 96f / 25.4f;
        float fontSizePx = fontSizeMm * refMmToPx;
        float widthPx = te.Width * refMmToPx;

        using var font = new SKFont(typeface, fontSizePx);
        var lines = WrapText(resolvedText, font, widthPx);
        float lineHeightMm = fontSizeMm * te.LineSpacing;

        return Math.Max(lineHeightMm * lines.Count + fontSizeMm * 0.3f, fontSizeMm);
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

    /// <summary>Rozdělí text na řádky podle šířky v px. Respektuje \r\n i \n.</summary>
    private static List<string> WrapText(string text, SKFont font, float maxWidthPx)
    {
        var result = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                result.Add("");
                continue;
            }

            var words = paragraph.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                if (font.MeasureText(testLine) > maxWidthPx && currentLine.Length > 0)
                {
                    result.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (currentLine.Length > 0)
                result.Add(currentLine);
        }

        if (result.Count == 0)
            result.Add("");

        return result;
    }
}
