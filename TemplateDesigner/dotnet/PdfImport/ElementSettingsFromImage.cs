using System.Globalization;
using System.Text;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

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

    /// <summary>
    /// Na co se import z předlohy smí ptát: vlastnost to musí mít povolené u své definice
    /// (<see cref="PropertyDescriptor.FromImage"/>) - tedy jít z papíru potvrdit. Pozice a velikost
    /// se navíc měří z PDF přesně a tlačítka otevírají editor, ne hodnotu.
    /// </summary>
    private static bool IsOfferable(PropertyDescriptor d) =>
        d.FromImage
        && d is not ButtonPropertyDescriptor
        && d.Group != TemplateElement.PositionGroup
        && d.Group != TemplateElement.ConditionsGroup;

    /// <summary>
    /// Co patří do protokolu - tedy do soupisu „čím se prvek liší od čerstvě vloženého". Je to
    /// širší množina než <see cref="IsOfferable"/>: protokol má ukázat i to, co se z PDF **změřilo**
    /// (písma, výšky řádků), protože právě podle toho se kontroluje, jestli import měří správně.
    /// </summary>
    private static bool IsReportable(PropertyDescriptor d) =>
        d is not ButtonPropertyDescriptor
        && d.Group != TemplateElement.PositionGroup
        && d.Group != TemplateElement.ConditionsGroup;

    // I v protokolu se skupinou: popisek ve skupině části prvku název části neopakuje („Šipka směru · Pozice"),
    // samotné „Pozice" by v protokolu nic neřeklo.
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
            string pred = Describe(d);

            if (!TryWrite(d, value))
            {
                log.Add($"⚠ {KeyOf(d)}: hodnotě „{value}\" nerozumím, ponechána výchozí");
                continue;
            }

            // Do protokolu jen skutečné změny: „nastaveno" má znamenat, že se něco změnilo.
            // Model často potvrdí i hodnotu, kterou prvek stejně má, a v soupisu to pak vypadá,
            // že z předlohy vzešlo víc, než kolik se doopravdy přeneslo.
            if (Describe(d) != pred)
                log.Add($"{KeyOf(d)} = {value}");
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

    /// <summary>
    /// Čím se prvek liší od čerstvě vloženého téhož typu - tedy co všechno import z předlohy
    /// nastavil, ať už z odpovědi modelu, nebo změřením z PDF. Slouží ke kontrole importu:
    /// z protokolu jde porovnat s předlohou i s předchozím během.
    /// </summary>
    internal static List<string> DiffFromDefault(TemplateElement element)
    {
        var result = new List<string>();

        TemplateElement vychozi;
        try { vychozi = (TemplateElement)Activator.CreateInstance(element.GetType())!; }
        catch { return result; }

        var vychoziPodleKlice = vychozi.GetPropertyDescriptors()
            .Where(IsReportable)
            .GroupBy(KeyOf)
            .ToDictionary(g => g.Key, g => Describe(g.First()));

        foreach (var d in element.GetPropertyDescriptors().Where(IsReportable))
        {
            string ted = Describe(d);
            if (!vychoziPodleKlice.TryGetValue(KeyOf(d), out var puvodne) || ted == puvodne) continue;
            result.Add($"{KeyOf(d)} = {ted}   (výchozí: {puvodne})");
        }

        return result;
    }

    /// <summary>Hodnota deskriptoru jako text - u výběru popiskem volby, ne názvem z kódu.</summary>
    private static string Describe(PropertyDescriptor d)
    {
        try
        {
            return d switch
            {
                EnumPropertyDescriptor e =>
                    e.Options.FirstOrDefault(o => Equals(o.Value, e.Get())).Label ?? e.Get()?.ToString() ?? "",
                BoolPropertyDescriptor b => b.Get() ? "ano" : "ne",
                ColorPropertyDescriptor c => c.Get(),
                FloatPropertyDescriptor f => f.Get().ToString("0.##", CultureInfo.InvariantCulture)
                                             + (f.Unit.Length > 0 ? " " + f.Unit : ""),
                StringPropertyDescriptor s => s.Get(),
                _ => ""
            };
        }
        catch { return ""; }
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
