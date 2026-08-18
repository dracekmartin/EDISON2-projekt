using System.Globalization;
using System.Text;
using TemplateDesigner.Models;

namespace TemplateDesigner.PdfImport;

/// <summary>
/// Nastavení vzhledu prvku odvozené z předlohy. Seznam toho, na co se AI ptáme, i zapisovač
/// odpovědi vznikají z <see cref="PropertyDescriptor"/> prvku - tedy z téhož zdroje, ze kterého
/// se staví panel vlastností. Import proto nemá vlastní seznam vlastností, který by se s prvky
/// rozešel (přesně to se stalo dosavadní ručně psané trojici v promptu).
///
/// Klíč = „Skupina · Popisek", protože popisek sám se v rámci prvku opakuje (Barva textu…).
/// </summary>
internal static class ElementSettingsFromImage
{
    private const string KeySeparator = " · ";

    /// <summary>Vlastnosti, na které nemá smysl se ptát: pozice a velikost se měří z PDF přesně,
    /// podmínky jsou data (ne vzhled) a tlačítka otevírají editor, ne hodnotu.</summary>
    private static bool IsOfferable(PropertyDescriptor d) =>
        d is not ButtonPropertyDescriptor
        && d.Group != TemplateElement.PositionGroup
        && d.Group != TemplateElement.ConditionsGroup;

    private static string KeyOf(PropertyDescriptor d) => $"{d.Group}{KeySeparator}{d.Label}";

    /// <summary>
    /// Soupis nastavitelných vlastností prvku pro otázku modelu - klíč, typ a povolené hodnoty.
    /// </summary>
    internal static string BuildCatalog(TemplateElement element)
    {
        var sb = new StringBuilder();
        foreach (var d in element.GetPropertyDescriptors().Where(IsOfferable))
            sb.AppendLine($"- \"{KeyOf(d)}\": {DescribeAllowedValues(d)}");
        return sb.ToString();
    }

    private static string DescribeAllowedValues(PropertyDescriptor d) => d switch
    {
        EnumPropertyDescriptor e => "právě jedna z hodnot " + string.Join(" / ", e.Options.Select(o => $"\"{o.Label}\"")),
        BoolPropertyDescriptor => "\"ano\" nebo \"ne\"",
        ColorPropertyDescriptor => "barva ve tvaru #AARRGGBB",
        FloatPropertyDescriptor f => $"číslo {f.Min:0.#}–{f.Max:0.#}{(f.Unit.Length > 0 ? " " + f.Unit : "")}",
        _ => "text"
    };

    /// <summary>
    /// Zapíše hodnoty z odpovědi do prvku. Vrací protokol pro uživatele - co se nastavilo a co se
    /// nepovedlo. Neznámý klíč ani nesmyslnou hodnotu do modelu nepustí (šablona s nesmyslnou
    /// barvou vypadá rozbitě i po každém dalším otevření), jen je vypíše.
    /// </summary>
    internal static List<string> Apply(
        TemplateElement element, IReadOnlyDictionary<string, string>? settings)
    {
        var log = new List<string>();
        if (settings is null || settings.Count == 0) return log;

        var byKey = element.GetPropertyDescriptors()
            .Where(IsOfferable)
            .GroupBy(KeyOf)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (key, raw) in settings)
        {
            if (!byKey.TryGetValue(key.Trim(), out var d))
            {
                log.Add($"⚠ „{key}\" - takovou vlastnost prvek nemá, přeskočeno");
                continue;
            }

            var value = raw?.Trim() ?? "";
            if (TryWrite(d, value))
                log.Add($"{d.Label} = {value}");
            else
                log.Add($"⚠ {d.Label}: hodnotě „{value}\" nerozumím, ponechána výchozí");
        }

        return log;
    }

    private static bool TryWrite(PropertyDescriptor d, string value)
    {
        switch (d)
        {
            case EnumPropertyDescriptor e:
                if (MatchOption(e.Options, value) is { } match) { e.Set(match); return true; }
                return false;

            case BoolPropertyDescriptor b:
                if (TryParseBool(value, out bool parsed)) { b.Set(parsed); return true; }
                return false;

            case ColorPropertyDescriptor c:
                if (!ColorHex.IsValid(value)) return false;
                c.Set(value);
                return true;

            case FloatPropertyDescriptor f:
                if (!float.TryParse(value.Replace(",", "."), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float num)) return false;
                if (num < f.Min || num > f.Max) return false;   // mimo rozsah = model si vymyslel
                f.Set(num);
                return true;

            case StringPropertyDescriptor s:
                s.Set(value);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Najde volbu podle popisku. Popisky nesou vysvětlivku („Vlevo (u levého okraje elementu)"),
    /// zatímco model odpoví holé „Vlevo" - bereme proto i začátek popisku, ale jen když sedí na
    /// **jedinou** volbu. Dvě vyhovující volby znamenají, že nevíme, kterou chtěl; pak raději nic.
    /// </summary>
    private static object? MatchOption((string Label, object Value)[] options, string value)
    {
        foreach (var (label, val) in options)
            if (string.Equals(label, value, StringComparison.OrdinalIgnoreCase))
                return val;

        var byPrefix = options
            .Where(o => o.Label.StartsWith(value, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return byPrefix.Count == 1 ? byPrefix[0].Value : null;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        switch (value.ToLowerInvariant())
        {
            case "ano" or "true" or "1": result = true;  return true;
            case "ne" or "false" or "0": result = false; return true;
            default:                     result = false; return false;
        }
    }
}
