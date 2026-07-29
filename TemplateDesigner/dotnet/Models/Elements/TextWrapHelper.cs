using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Sdílený helper pro zalamování textu. Používá TextElement i SkiaHeightCalculator.
/// </summary>
public static class TextWrapHelper
{
    /// <summary>Rozdělí text na řádky podle šířky v px. Respektuje \r\n i \n.</summary>
    public static List<string> WrapText(string text, SKFont font, float maxWidthPx)
    {
        var result = new List<string>();

        // Normalizace řádkových zlomů - WPF TextBox posílá \r\n
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
