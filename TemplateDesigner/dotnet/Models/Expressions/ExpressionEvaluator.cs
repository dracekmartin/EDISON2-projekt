using System.Collections.Concurrent;
using System.Globalization;

namespace TemplateDesigner.Models.Expressions;

/// <summary>
/// Vyhodnocuje výrazy VisibilityExpression vůči DataContext.
/// Parsovaný AST je cachovaný — každý výraz se parsuje jen jednou za běh aplikace.
///
/// Typová konverze proměnných z DataContext (vždy string):
///   ""            → false
///   "false" / "0" → false
///   "true"  / "1" → true
///   jiný neprázdný string → true (pokud se neporovnává s číslem)
///   číslo         → != 0 je true
/// </summary>
public static class ExpressionEvaluator
{
    private static readonly ConcurrentDictionary<string, ExpressionNode?> _cache = new();

    // ── Veřejné API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Vrátí true pokud element/sekce s daným výrazem má být viditelná.
    /// null nebo prázdný výraz → true (vždy viditelný).
    /// Chyba parsování nebo evaluace → true (fail-open — neskrývat kvůli překlepu).
    /// </summary>
    public static bool IsVisible(string? expression, DataContext? data)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;

        var ast = _cache.GetOrAdd(expression, TryParse);
        if (ast is null) return true; // parse selhal — fail-open

        try   { return ToBool(Evaluate(ast, data)); }
        catch { return true; }
    }

    /// <summary>
    /// Pokusí se parsovat výraz a vrátí chybovou zprávu pokud selže.
    /// Null = výraz je validní.
    /// </summary>
    public static string? Validate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        try   { ExpressionParser.Parse(expression); return null; }
        catch (FormatException ex) { return ex.Message; }
    }

    // ── Parsování ─────────────────────────────────────────────────────────────

    private static ExpressionNode? TryParse(string expr)
    {
        try   { return ExpressionParser.Parse(expr); }
        catch { return null; }
    }

    // ── Evaluace ──────────────────────────────────────────────────────────────

    private static object? Evaluate(ExpressionNode node, DataContext? data) => node switch
    {
        LiteralNode  lit  => lit.Value,
        VariableNode var_ => data?.GetValue(var_.Name),
        UnaryNode    un   => EvalUnary(un, data),
        BinaryNode   bin  => EvalBinary(bin, data),
        _                 => null
    };

    private static object? EvalUnary(UnaryNode un, DataContext? data)
    {
        var val = Evaluate(un.Operand, data);
        return un.Op == "!" ? (object)!ToBool(val) : null;
    }

    private static object? EvalBinary(BinaryNode bin, DataContext? data)
    {
        // Short-circuit pro && a ||
        if (bin.Op == "&&")
        {
            if (!ToBool(Evaluate(bin.Left, data))) return false;
            return ToBool(Evaluate(bin.Right, data));
        }
        if (bin.Op == "||")
        {
            if (ToBool(Evaluate(bin.Left, data))) return true;
            return ToBool(Evaluate(bin.Right, data));
        }

        var left  = Evaluate(bin.Left,  data);
        var right = Evaluate(bin.Right, data);
        return bin.Op switch
        {
            "==" => (object)(Compare(left, right) == 0),
            "!=" => Compare(left, right) != 0,
            ">"  => Compare(left, right) >  0,
            "<"  => Compare(left, right) <  0,
            ">=" => Compare(left, right) >= 0,
            "<=" => Compare(left, right) <= 0,
            _    => null
        };
    }

    /// <summary>
    /// Porovná dvě hodnoty. Pokud obě jdou parsovat jako číslo, porovná numericky.
    /// Jinak string compare (OrdinalIgnoreCase).
    /// </summary>
    private static int Compare(object? a, object? b)
    {
        var da = ToDouble(a);
        var db = ToDouble(b);
        if (da is not null && db is not null)
            return da.Value.CompareTo(db.Value);

        return string.Compare(
            a?.ToString() ?? "",
            b?.ToString() ?? "",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ToBool(object? val) => val switch
    {
        null   => false,
        bool b => b,
        double d => d != 0,
        string s when string.IsNullOrEmpty(s) => false,
        string s when s.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
        string s when s == "0" => false,
        string _ => true,   // neprázdný string != "false"/"0" → true
        _ => false
    };

    private static double? ToDouble(object? val) => val switch
    {
        double d => d,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
        _ => null
    };
}
