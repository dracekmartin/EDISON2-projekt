namespace TemplateDesigner.Models;

/// <summary>
/// Souhrn odjezdů, které při vlastních sloupcích vývěsky nespadly do žádného sloupce
/// (pole NEZARAZENE_SPOJE ze stránek GetData). Chybějící minuta je neviditelná vada -
/// souhrn se proto ukazuje v návrháři (status bar) i při tisku/exportu.
/// </summary>
public static class UnassignedDepartures
{
    /// <summary>Text upozornění, nebo null když je vše zařazené.</summary>
    public static string? Summarize(IReadOnlyList<DataContext> pages)
    {
        var times = pages
            .Select(p => p.GetValue("NEZARAZENE_SPOJE")?.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToList();
        if (times.Count == 0) return null;

        var sample = string.Join(", ", times.Take(6)) + (times.Count > 6 ? ", …" : "");
        var sloveso = times.Count == 1 ? "odjezd nespadá" : times.Count <= 4 ? "odjezdy nespadají" : "odjezdů nespadá";
        return $"{times.Count} {sloveso} do žádného sloupce ({sample})";
    }
}
