using System.Globalization;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Models.Expressions;

/// <summary>
/// Vyhodnocuje agregační funkce v textu buňky nad kolekcí řádků.
/// Podporované funkce: SUM({POLE}), COUNT(), AVG({POLE}), MIN({POLE}), MAX({POLE}).
///
/// Příklady:
///   "SUM({CENA})"                → "60000"
///   "Celkem: SUM({CENA})"       → "Celkem: 60000"
///   "Řádků: COUNT()"             → "Řádků: 4"
///   "Průměr: AVG({CENA})"       → "Průměr: 15000"
/// </summary>
public static class AggregationResolver
{
    // Pattern: SUM({FIELD}), COUNT(), AVG({FIELD}), MIN({FIELD}), MAX({FIELD})
    private static readonly Regex AggregationPattern = new(
        @"(SUM|COUNT|AVG|MIN|MAX)\((?:\{(\w+)\})?\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Nahradí agregační funkce v textu vypočítanými hodnotami.
    /// Pokud rows je prázdný nebo null, vrátí text s nahrazenými "0" / "" hodnotami.
    /// Bez agregačních funkcí vrátí text beze změny.
    /// </summary>
    public static string Resolve(string template, IReadOnlyList<DataContext>? rows)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return AggregationPattern.Replace(template, m =>
        {
            var func = m.Groups[1].Value.ToUpperInvariant();
            var field = m.Groups[2].Success ? m.Groups[2].Value : null;
            return Evaluate(func, field, rows ?? []);
        });
    }

    private static string Evaluate(string func, string? field, IReadOnlyList<DataContext> rows)
    {
        if (func == "COUNT")
            return rows.Count.ToString(CultureInfo.InvariantCulture);

        if (field is null) return "";

        var values = new List<double>();
        foreach (var row in rows)
        {
            var raw = row.GetString(field);
            if (!string.IsNullOrEmpty(raw) &&
                double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
            {
                values.Add(num);
            }
        }

        if (values.Count == 0) return "0";

        double result = func switch
        {
            "SUM" => values.Sum(),
            "AVG" => values.Average(),
            "MIN" => values.Min(),
            "MAX" => values.Max(),
            _     => 0
        };

        return result.ToString(CultureInfo.InvariantCulture);
    }
}
