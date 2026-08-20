namespace EDControls.EDList;

/// <summary>
/// Stateless helper pro client-side filter EDList řádků. Substring match (case-insensitive)
/// napříč zadanými klíči sloupců.
/// </summary>
public static class EDListFilter
{
    /// <summary>
    /// Vrátí true pokud řádek odpovídá search textu - alespoň jeden ze zadaných klíčů obsahuje
    /// substring. Prázdný/null search vrací vždy true (žádný filter).
    /// </summary>
    public static bool Matches(EDListRow row, string? searchText, IEnumerable<string> columnKeys)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return true;
        var needle = searchText.Trim();
        foreach (var key in columnKeys)
        {
            var value = row[key];
            if (string.IsNullOrEmpty(value)) continue;
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
