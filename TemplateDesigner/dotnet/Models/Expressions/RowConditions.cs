namespace TemplateDesigner.Models.Expressions;

/// <summary>
/// Sdílená řádková podmínková logika pro tabulkové elementy (DynamicTable, StopList, LineTable):
/// výraz nad daty <b>jednoho řádku</b> → viditelnost / barva zvýraznění pozadí. Vyhodnocení jde přes
/// <see cref="ExpressionEvaluator"/> (fail-open — překlep řádek nesmaže/neschová).
///
/// R1 filozofie: sdílíme <b>algoritmus</b>, ne datovou třídu — každý element si drží svá pole
/// (výraz + barva) i svůj řádkový POCO, do helperu posílá jen DataContext řádku.
///
/// DepartureGrid je vědomě mimo — je pivot (hodina × skupina dnů); „řádek" je agregace mnoha odjezdů,
/// ne jeden datový záznam, takže výraz nad řádkem tam nemá proti čemu běžet (per-odjezd barvu řeší
/// prezentační nápovědy FORE_COLOR/BACK_COLOR z dat).
/// </summary>
public static class RowConditions
{
    /// <summary>Řádek je viditelný, když výraz platí. Prázdný/null výraz = vždy viditelný.</summary>
    public static bool IsVisible(string? expression, DataContext? row) =>
        ExpressionEvaluator.IsVisible(expression, row);

    /// <summary>
    /// Barva pozadí zvýraznění řádku — <b>první splněné pravidlo vyhrává</b>. Vrátí barvu prvního
    /// pravidla, jehož <see cref="HighlightRule.Condition"/> platí a má vyplněnou barvu; jinak null
    /// (bez zvýraznění). Jen pozadí; <see cref="HighlightRule.TargetColumn"/> render zatím neřeší.
    /// </summary>
    public static string? HighlightColor(IReadOnlyList<HighlightRule>? rules, DataContext? row)
    {
        if (rules is null) return null;
        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.Condition)
                && !string.IsNullOrWhiteSpace(rule.BackgroundColor)
                && ExpressionEvaluator.IsVisible(rule.Condition, row))
                return rule.BackgroundColor;
        }
        return null;
    }
}
