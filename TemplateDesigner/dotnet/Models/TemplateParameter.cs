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
}
