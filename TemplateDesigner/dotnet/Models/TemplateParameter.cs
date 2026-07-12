namespace TemplateDesigner.Models;

/// <summary>
/// Parametr sestavy — renderovací rozhodnutí, které uživatel může nastavit před tiskem.
/// Definice žije v JSON šabloně; aktuální hodnoty jsou dočasné (paměť aplikace).
/// </summary>
public class TemplateParameter
{
    /// <summary>Interní název — použitelný jako placeholder {NAZEV} v šabloně.</summary>
    public string Name { get; set; } = "";

    /// <summary>Popis zobrazený uživateli v preview panelu.</summary>
    public string Label { get; set; } = "";

    /// <summary>Typ parametru: "bool", "enum", "string", "number", "date".</summary>
    public string Type { get; set; } = "string";

    /// <summary>Výchozí hodnota jako string (konverze probíhá dle Type).</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Možné hodnoty pro typ "enum".</summary>
    public List<string>? Options { get; set; }

    /// <summary>Skupina pro vizuální seskupení v UI (např. „Obsah", „Sloupce a časová osa").
    /// Null/prázdné = bez skupiny. Aditivní klíč kontraktu (R3) — starší server ho neposílá.</summary>
    public string? Group { get; set; }

    /// <summary>
    /// Podmínka viditelnosti parametru. Formát „NázevParametru=Hodnota".
    /// Null nebo prázdný = vždy viditelný. Vyhodnocuje se proti aktuálním hodnotám ostatních parametrů.
    /// Příklad: „CASOVA_OSA=Vlastní rozsah" — HODINY_OD/DO jsou viditelné jen v Range módu.
    /// </summary>
    public string? VisibleWhen { get; set; }

    /// <summary>Vrátí DefaultValue převedenou na typovanou hodnotu dle Type.</summary>
    public object? GetDefaultTyped() => Type switch
    {
        "bool"   => string.Equals(DefaultValue, "true", StringComparison.OrdinalIgnoreCase),
        "number" => float.TryParse(DefaultValue, out var f) ? f : 0f,
        _        => DefaultValue ?? ""
    };

    /// <summary>
    /// Vyhodnotí <see cref="VisibleWhen"/> ("POLE=hodnota") proti aktuálním hodnotám parametrů.
    /// Prázdné / neplatné pravidlo = vždy viditelný. Jediný zdroj pravdy pro viditelnost parametru
    /// (preview lišta i tiskový dialog) — porovnání case-insensitive.
    /// </summary>
    public bool IsVisible(IReadOnlyDictionary<string, object?> values)
    {
        if (string.IsNullOrEmpty(VisibleWhen)) return true;

        var parts = VisibleWhen.Split('=', 2);
        if (parts.Length != 2) return true;

        var actual = values.TryGetValue(parts[0].Trim(), out var v) ? v?.ToString() : null;
        return string.Equals(actual, parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
