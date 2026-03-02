namespace TemplateDesigner.Models.Schema;

/// <summary>
/// Schéma agendy — katalog dostupných databázových polí pro danou agendu.
/// Slouží pouze designeru (zobrazení browseru polí, vkládání placeholderů).
/// Print engine tuto vrstvu nepotřebuje.
/// </summary>
public class DataSchema
{
    /// <summary>Kód agendy, např. "JR", "FAKTURY". Musí odpovídat Template.AgendaCode.</summary>
    public string AgendaCode { get; set; } = "";

    /// <summary>Čitelný název agendy pro UI.</summary>
    public string AgendaName { get; set; } = "";

    /// <summary>Verze schématu — pro budoucí migrace.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Flat seznam dostupných polí (V1).
    /// V JSON souboru klíč "fields".
    /// </summary>
    public List<DataField> Fields { get; set; } = [];

    /// <summary>
    /// Volitelné skupiny pro V2+. Pokud není null ani prázdný,
    /// UI browser zobrazí stromovou strukturu místo flat listu.
    /// </summary>
    public List<DataFieldGroup>? Groups { get; set; }
}

/// <summary>Jedno dostupné databázové pole.</summary>
public class DataField
{
    /// <summary>
    /// Název pole — klíč pro placeholder {NAZEV_POLE}.
    /// Musí být unikátní v rámci agendy.
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>Čitelný popisek pro UI browser (výchozí = FieldName).</summary>
    public string Label { get; set; } = "";

    /// <summary>Datový typ — pouze hint pro UI. Placeholder {POLE} je vždy string.</summary>
    public FieldDataType DataType { get; set; } = FieldDataType.Text;

    /// <summary>Ukázková hodnota zobrazovaná v browseru, např. "42" nebo "Praha hl.n."</summary>
    public string? Example { get; set; }

    /// <summary>Volitelný popis pole pro tooltip v browseru.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Skupina polí pro V2+ (stromový browser).
/// Každá skupina má scope: Scalar (jednorázové hodnoty) nebo Table (kolekce řádků).
/// </summary>
public class DataFieldGroup
{
    /// <summary>Název skupiny zobrazovaný v browseru.</summary>
    public string Name { get; set; } = "";

    /// <summary>Scalar = jednorázové hodnoty, Table = kolekce řádků.</summary>
    public FieldScope Scope { get; set; } = FieldScope.Scalar;

    /// <summary>
    /// Pro Table scope: klíč pro DataContext.GetRows(collectionKey).
    /// Null pro Scalar scope.
    /// </summary>
    public string? CollectionKey { get; set; }

    public List<DataField> Fields { get; set; } = [];
}

/// <summary>Datový typ pole — pouze metadata pro UI browser. Placeholder je vždy string.</summary>
public enum FieldDataType
{
    Text,
    Number,
    Date,
    Time,
    DateTime,
    Boolean
}

/// <summary>Scope skupiny polí — hint pro UI browser.</summary>
public enum FieldScope
{
    /// <summary>Jednorázová hodnota (záhlaví sestavy, metadata).</summary>
    Scalar,
    /// <summary>Kolekce řádků (tabulka zastávek, položky faktury).</summary>
    Table
}

/// <summary>Základní info o dostupné agendě — pro výběr v UI.</summary>
public record AgendaInfo(string Code, string Name);
