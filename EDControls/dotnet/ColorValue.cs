using System.Globalization;
using System.Windows.Media;

namespace EDControls;

/// <summary>
/// Jediná definice barvy na drátě: hex <c>#AARRGGBB</c> / <c>#RRGGBB</c> (formulář, nové seznamy;
/// <c>#</c> nepovinný) a legacy <c>R;G;B</c> (starší <c>LoadList</c> implementace EDListu, musí zůstat čitelné).
/// </summary>
internal static class ColorValue
{
    /// <summary>Hex i legacy <c>R;G;B</c>.</summary>
    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var s = value.Trim();
        if (!s.Contains(';')) return TryParseHex(s, out color);

        var parts = s.Split(';');
        if (parts.Length != 3
            || !byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            return false;
        color = Color.FromRgb(r, g, b);
        return true;
    }

    /// <summary>Jen hex - formulář: hostitel (TD) barvu parsuje jako hex, <c>R;G;B</c> by mu neprošlo.</summary>
    public static bool TryParseHex(string? value, out Color color)
    {
        color = default;
        var hex = value?.Trim().TrimStart('#') ?? "";
        // Kontrola znaků zvlášť: uint.TryParse s AllowHexSpecifier tiše bere koncové '\0'.
        if ((hex.Length != 8 && hex.Length != 6)
            || !hex.All(Uri.IsHexDigit)
            || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var argb))
            return false;
        if (hex.Length == 6) argb |= 0xFF000000;
        color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return true;
    }
}
