using System.Globalization;

namespace TemplateDesigner.Models;

/// <summary>
/// Parametr sestavy - renderovací rozhodnutí, které uživatel může nastavit před tiskem.
/// Definice žije v JSON šabloně; aktuální hodnoty jsou dočasné (paměť aplikace).
/// </summary>
public class TemplateParameter
{
    /// <summary>Interní název - použitelný jako placeholder {NAZEV} v šabloně.</summary>
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
    /// Null/prázdné = bez skupiny. Aditivní klíč kontraktu (R3) - starší server ho neposílá.</summary>
    public string? Group { get; set; }

    /// <summary>
    /// Podmínka viditelnosti parametru. Formát „NázevParametru=Hodnota".
    /// Null nebo prázdný = vždy viditelný. Vyhodnocuje se proti aktuálním hodnotám ostatních parametrů.
    /// Příklad: „CASOVA_OSA=Vlastní rozsah" - HODINY_OD/DO jsou viditelné jen v Range módu.
    /// </summary>
    public string? VisibleWhen { get; set; }

    /// <summary>
    /// Hodnotu parametru zpracovává až server v <c>GetData</c>, takže její změna musí spustit
    /// načtení dat, ne jen překreslení náhledu. Posílá ho agenda u definice parametru; starší
    /// server ho neposílá (aditivní klíč kontraktu, R3) a parametr se pak chová jako klientský.
    /// </summary>
    public bool ServerSide { get; set; }

    /// <summary>
    /// Klíč sloupce, bez kterého parametr nemá účinek: parametr se ukáže, jen když ho v šabloně
    /// některý prvek ukazuje (dnes varianty tras v seznamu zastávek). Null = nezávisí na šabloně.
    /// Aditivní klíč kontraktu (R3) - starší klient ho ignoruje a parametr ukáže vždy.
    /// </summary>
    public string? VisibleWhenColumn { get; set; }

    /// <summary>
    /// Hodnota patří vybrané instanci (lince), ne sestavě: nepamatuje se v předvolbách ani v šabloně
    /// a při výběru jiné instance se vrátí na výchozí. Dnes srovnávací linka rozdílového JŘ - z minula
    /// by ukazovala na jinou trasu (nález Jan 9. 10.). Aditivní klíč (R3), starší klient ji pamatuje.
    /// </summary>
    public bool PerInstance { get; set; }

    /// <summary>Vrátí DefaultValue převedenou na typovanou hodnotu dle Type.</summary>
    public object? GetDefaultTyped() => Type switch
    {
        "bool"   => string.Equals(DefaultValue, "true", StringComparison.OrdinalIgnoreCase),
        // Prázdné číslo zůstává prázdné (null), ne 0: u „Nejvýš sloupců variant" znamená prázdno
        // „bez omezení", kdežto 0 vypadalo jako „netisknout žádné" (nález Jan 25. 9.).
        "number" => float.TryParse(DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null,
        _        => DefaultValue ?? ""
    };

    /// <summary>
    /// Vyhodnotí <see cref="VisibleWhenColumn"/> proti šabloně a <see cref="VisibleWhen"/>
    /// ("POLE=hodnota") proti aktuálním hodnotám parametrů; platit musí obojí. Prázdné / neplatné
    /// pravidlo = vždy viditelný. Jediný zdroj pravdy pro viditelnost parametru (preview lišta
    /// i tiskový dialog) - porovnání case-insensitive.
    /// </summary>
    /// <param name="template">Šablona, ke které se parametry nastavují. Null = neznámá, sloupce se neposuzují.</param>
    public bool IsVisible(IReadOnlyDictionary<string, object?> values, Template? template)
    {
        if (!string.IsNullOrEmpty(VisibleWhenColumn) && template is not null && !template.ShowsColumn(VisibleWhenColumn))
            return false;
        if (string.IsNullOrEmpty(VisibleWhen)) return true;

        var parts = VisibleWhen.Split('=', 2);
        if (parts.Length != 2) return true;

        var actual = values.TryGetValue(parts[0].Trim(), out var v) ? v?.ToString() : null;
        return string.Equals(actual, parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
