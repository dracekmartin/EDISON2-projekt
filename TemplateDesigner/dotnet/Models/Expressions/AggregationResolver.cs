using System.Globalization;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Models.Expressions;

/// <summary>
/// Vyhodnocuje agregační funkce v textu buňky nad kolekcí řádků.
/// Podporované funkce: SUM(výraz), COUNT(), AVG(výraz), MIN(výraz), MAX(výraz).
///
/// Argument agregace je <b>výraz</b> vyhodnocený per řádek přes <see cref="ExpressionEvaluator"/> —
/// nejen holé pole, ale i aritmetika nad poli řádku (počítaný sloupec):
///   "SUM({CENA})"                → "60000"          (holé pole)
///   "SUM({CENA}*{POCET})"        → "72000"          (výraz nad poli řádku)
///   "Celkem: SUM({CENA})"       → "Celkem: 60000"
///   "Řádků: COUNT()"             → "Řádků: 4"
///   "Průměr: AVG({CENA})"       → "Průměr: 15000"
///
/// Omezení: argument nesmí obsahovat vnořené závorky (regex bere obsah po první „)").
/// </summary>
public static class AggregationResolver
{
    // Pattern: FUNC( <výraz nebo prázdno> ) — argument = vše po první „)"
    private static readonly Regex AggregationPattern = new(
        @"(SUM|COUNT|AVG|MIN|MAX)\(([^)]*)\)",
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
            var arg = m.Groups[2].Value.Trim();   // výraz (s {poli}) nebo prázdno u COUNT()
            return Evaluate(func, arg, rows ?? []);
        });
    }

    private static string Evaluate(string func, string arg, IReadOnlyList<DataContext> rows)
    {
        if (func == "COUNT")
            return rows.Count.ToString(CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(arg)) return "";

        var values = new List<double>();
        foreach (var row in rows)
        {
            // Argument je výraz nad poli řádku — vyhodnotí ho evaluator (holé {POLE} = jen jeho hodnota)
            var raw = ExpressionEvaluator.EvaluateToString(arg, row);
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
