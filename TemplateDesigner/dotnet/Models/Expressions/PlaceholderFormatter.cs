using System.Globalization;

namespace TemplateDesigner.Models.Expressions;

/// <summary>
/// Aplikuje formátovací instrukce na hodnotu placeholderu.
/// Syntaxe: {POLE:instrukce} — vše za ':' je instrukce.
///
/// Podporované instrukce:
///   .NET formát:        dd.MM.yyyy, N2, HH:mm, C2, P1 atd.
///   Řetězcové funkce:   LEFT(n), RIGHT(n), UPPER, LOWER, TRIM,
///                        PADLEFT(n,'c'), PADRIGHT(n,'c')
/// </summary>
public static class PlaceholderFormatter
{
    /// <summary>
    /// Naformátuje hodnotu podle instrukce. Pokud instrukce je null/prázdná, vrátí hodnotu beze změny.
    /// Při chybě vrátí původní hodnotu (fail-safe).
    /// </summary>
    public static string Format(string? value, string? formatSpec, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(formatSpec) || value is null) return value ?? "";

        // Zkusit řetězcové funkce
        var result = TryStringFunction(value, formatSpec);
        if (result is not null) return result;

        // Zkusit .NET formátovací řetězec (datum, číslo)
        var formatted = TryDotNetFormat(value, formatSpec, culture);
        return formatted is not null ? NormalizeWhitespace(formatted) : value;
    }

    private static string? TryStringFunction(string value, string spec)
    {
        var upper = spec.ToUpperInvariant();

        if (upper == "UPPER") return value.ToUpperInvariant();
        if (upper == "LOWER") return value.ToLowerInvariant();
        if (upper == "TRIM") return value.Trim();

        // LEFT(n)
        if (upper.StartsWith("LEFT(") && upper.EndsWith(")"))
        {
            if (int.TryParse(spec[5..^1], out int n))
                return value.Length <= n ? value : value[..n];
        }

        // RIGHT(n)
        if (upper.StartsWith("RIGHT(") && upper.EndsWith(")"))
        {
            if (int.TryParse(spec[6..^1], out int n))
                return value.Length <= n ? value : value[^n..];
        }

        // PADLEFT(n,'c') or PADLEFT(n)
        if (upper.StartsWith("PADLEFT(") && upper.EndsWith(")"))
            return TryPad(value, spec[8..^1], padLeft: true);

        // PADRIGHT(n,'c') or PADRIGHT(n)
        if (upper.StartsWith("PADRIGHT(") && upper.EndsWith(")"))
            return TryPad(value, spec[9..^1], padLeft: false);

        return null;
    }

    private static string? TryPad(string value, string args, bool padLeft)
    {
        // args: "3,'0'" or "3" (default pad char = ' ')
        var parts = args.Split(',', 2);
        if (!int.TryParse(parts[0].Trim(), out int totalWidth)) return null;

        char padChar = ' ';
        if (parts.Length == 2)
        {
            var charPart = parts[1].Trim().Trim('\'', '"');
            if (charPart.Length == 1) padChar = charPart[0];
        }

        return padLeft ? value.PadLeft(totalWidth, padChar) : value.PadRight(totalWidth, padChar);
    }

    /// <summary>Nahradí nezlomitelné a speciální mezery běžnou mezerou (SkiaSharp je nemusí renderovat).</summary>
    private static string NormalizeWhitespace(string s) =>
        s.Replace('\u00A0', ' ')   // non-breaking space (český formát tisíců)
         .Replace('\u202F', ' ');  // narrow non-breaking space

    private static string? TryDotNetFormat(string value, string formatSpec, CultureInfo? culture = null)
    {
        var fmt = culture ?? CultureInfo.CurrentCulture;

        // Zkusit parsovat jako číslo
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
        {
            try { return num.ToString(formatSpec, fmt); }
            catch (FormatException) { }
        }

        // Zkusit parsovat jako datum/čas
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            try { return dt.ToString(formatSpec, fmt); }
            catch (FormatException) { }
        }

        // Zkusit parsovat jako TimeSpan (pro časy typu "8:00", "14:30")
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
        {
            try
            {
                var asDateTime = DateTime.Today.Add(ts);
                return asDateTime.ToString(formatSpec, fmt);
            }
            catch (FormatException) { }
        }

        return null;
    }
}
