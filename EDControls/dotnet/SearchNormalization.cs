using System.Globalization;
using System.Text;

namespace EDControls;

/// <summary>Normalizace textu pro hledání bez diakritiky: „Pošta" → „posta".
/// Lowercase (invariant) + odstranění combining marks (NFD forma). null/prázdné → "".</summary>
public static class SearchNormalization
{
    public static string NormalizeForSearch(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var normalized = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}
