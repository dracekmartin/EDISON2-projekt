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

        // Subset prefix („ABCDEF+Arial") a okrajové oddělovače pryč - PdfPig je vrací tak, jak jsou v PDF.
        string name = pdfFontName.Trim(Separators);
        int plusIndex = name.IndexOf('+');
        if (plusIndex >= 0 && plusIndex < name.Length - 1)
            name = name[(plusIndex + 1)..].Trim(Separators);

        bool isBold = ContainsAny(name, "Bold", "Heavy", "Black");
        bool isItalic = ContainsAny(name, "Italic", "Oblique");

        string cleanName = StripStyleSuffixes(name);

        // Známé mapování - nejdřív vyčištěný název, pak původní (kvůli „Arial-BoldMT").
        if (KnownMappings.TryGetValue(cleanName, out var mapped)
            || KnownMappings.TryGetValue(name, out mapped))
            return (mapped, isBold, isItalic);

        if (cleanName.Length > 1) return (cleanName, isBold, isItalic);

        return ("Arial", isBold, isItalic);
    }

    /// <summary>Oddělovače, které v PDF názvu oddělují řez od rodiny („Arial,Bold", „Arial-Bold").</summary>
    private static readonly char[] Separators = [',', '-', ' ', '.'];

    private static readonly string[] StyleSuffixes =
        ["BoldItalic", "BoldOblique", "Bold", "Italic", "Oblique", "Regular", "MT", "PS", "PSMT"];

    /// <summary>
    /// Odloupne z názvu řezy i s oddělovačem, dokud je co loupat („Arial,BoldItalic" → „Arial").
    /// Loop je nutný: řez bývá složený a jedno kolo nechá zpátky „Arial,Bold". Zbylý oddělovač se
    /// musí uříznout taky - do 18. 8. zůstával v názvu rodiny („Arial,"), takže font vypadal jako
    /// nenainstalovaný a šablona po importu hlásila „Font Arial není na tomto počítači".
    /// </summary>
    private static string StripStyleSuffixes(string name)
    {
        string result = name;
        bool stripped;
        do
        {
            stripped = false;
            result = result.TrimEnd(Separators);
            foreach (var suffix in StyleSuffixes)
            {
                if (!result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                // „MT" jen jako přípona za oddělovačem nebo za písmenem - ne když je to celý název.
                if (result.Length == suffix.Length) continue;
                result = result[..^suffix.Length];
                stripped = true;
                break;
            }
        }
        while (stripped);

        return result.TrimEnd(Separators);
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
