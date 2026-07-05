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
    /// Barva pozadí zvýraznění, pokud řádek splní <paramref name="expression"/> a je zadaná
    /// <paramref name="color"/>; jinak null (bez zvýraznění). Jedno pravidlo, jen pozadí.
    /// </summary>
    public static string? HighlightColor(string? expression, string? color, DataContext? row) =>
        !string.IsNullOrWhiteSpace(expression)
        && !string.IsNullOrWhiteSpace(color)
        && ExpressionEvaluator.IsVisible(expression, row)
            ? color : null;
}
