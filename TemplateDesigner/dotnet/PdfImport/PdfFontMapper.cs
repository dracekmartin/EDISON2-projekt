namespace TemplateDesigner.PdfImport;

/// <summary>
/// Mapování PDF fontů na systémové fonty + detekce Bold/Italic z názvu fontu.
/// </summary>
public static class PdfFontMapper
{
    private static readonly Dictionary<string, string> KnownMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TimesNewRoman"] = "Times New Roman",
        ["Times-Roman"] = "Times New Roman",
        ["Times"] = "Times New Roman",
        ["CourierNew"] = "Courier New",
        ["Courier"] = "Courier New",
        ["Helvetica"] = "Arial",
        ["ArialMT"] = "Arial",
        ["Arial-BoldMT"] = "Arial",
        ["Arial-ItalicMT"] = "Arial",
        ["Arial-BoldItalicMT"] = "Arial",
    };

    /// <summary>
    /// Mapuje PDF název fontu na (Family, IsBold, IsItalic).
    /// </summary>
    public static (string Family, bool IsBold, bool IsItalic) Map(string? pdfFontName)
    {
        if (string.IsNullOrWhiteSpace(pdfFontName))
            return ("Arial", false, false);

        // Odstranit subset prefix (vše před '+')
        string name = pdfFontName;
        int plusIndex = name.IndexOf('+');
        if (plusIndex >= 0 && plusIndex < name.Length - 1)
            name = name[(plusIndex + 1)..];

        // Detekce Bold/Italic z názvu
        bool isBold = ContainsAny(name, "Bold", "Heavy", "Black");
        bool isItalic = ContainsAny(name, "Italic", "Oblique");

        // Odstranit style suffixy pro mapování
        string cleanName = name;
        foreach (var suffix in new[] { "-BoldItalic", "-Bold", "-Italic", "-BoldOblique",
            "-Oblique", "Bold", "Italic", "MT", "PS", "PSMT", "-Regular" })
        {
            if (cleanName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName[..^suffix.Length];
                break;
            }
        }

        // Zkusit známé mapování
        if (KnownMappings.TryGetValue(cleanName, out var mapped))
            return (mapped, isBold, isItalic);

        // Zkusit i s původním názvem
        if (KnownMappings.TryGetValue(name, out mapped))
            return (mapped, isBold, isItalic);

        // Vrátit vyčištěný název, nebo fallback na Arial
        if (!string.IsNullOrWhiteSpace(cleanName) && cleanName.Length > 1)
            return (cleanName, isBold, isItalic);

        return ("Arial", isBold, isItalic);
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
