namespace TemplateDesigner.Models.Schema;

/// <summary>
/// Schéma agendy - katalog dostupných databázových polí pro danou agendu.
/// Slouží pouze designeru (zobrazení browseru polí, vkládání placeholderů).
/// Print engine tuto vrstvu nepotřebuje.
/// </summary>
public class DataSchema
{
    /// <summary>Kód agendy, např. "JR", "FAKTURY". Musí odpovídat Template.AgendaCode.</summary>
    public string AgendaCode { get; set; } = "";

    /// <summary>Čitelný název agendy pro UI.</summary>
    public string AgendaName { get; set; } = "";

    /// <summary>Verze schématu - pro budoucí migrace.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Flat seznam dostupných polí (V1).
    /// V JSON souboru klíč "fields".
    /// </summary>
    public List<DataField> Fields { get; set; } = [];

    /// <summary>
    /// Tabulkové kolekce - zdroje řádků pro Dynamic tabulky (V2+).
    /// Každá kolekce odpovídá jednomu collectionKey v Caché.
    /// </summary>
    public List<DataFieldCollection>? FieldCollections { get; set; }
}

/// <summary>Jedno dostupné databázové pole.</summary>
public class DataField
{
    /// <summary>
    /// Název pole - klíč pro placeholder {NAZEV_POLE}.
    /// Musí být unikátní v rámci agendy.
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>Čitelný popisek pro UI browser (výchozí = FieldName).</summary>
    public string Label { get; set; } = "";

    /// <summary>Datový typ - pouze hint pro UI. Placeholder {POLE} je vždy string.</summary>
    public FieldDataType DataType { get; set; } = FieldDataType.Text;

    /// <summary>Ukázková hodnota zobrazovaná v browseru, např. "42" nebo "Praha hl.n."</summary>
    public string? Example { get; set; }

    /// <summary>Volitelný popis pole pro tooltip v browseru.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Tabulková kolekce polí - definuje dostupná pole pro jeden datový zdroj Dynamic tabulky.
/// collectionKey odpovídá klíči v DataContext._rows a hodnotě DataSource na TableElement.
/// </summary>
public class DataFieldCollection
{
    /// <summary>Název kolekce zobrazovaný v browseru, např. "Zastávky".</summary>
    public string Name { get; set; } = "";

    /// <summary>Klíč kolekce - musí odpovídat collectionKey v Caché a DataSource na TableElement.</summary>
    public string CollectionKey { get; set; } = "";

    /// <summary>Pole dostupná v každém řádku kolekce.</summary>
    public List<DataField> Fields { get; set; } = [];
}

/// <summary>Datový typ pole - pouze metadata pro UI browser. Placeholder je vždy string.</summary>
public enum FieldDataType
{
    Text,
    Number,
    Date,
    Time,
    DateTime,
    Boolean
}

/// <summary>Základní info o dostupné agendě - pro výběr v UI.</summary>
public record AgendaInfo(string Code, string Name);

/// <summary>
/// Konkrétní instance agendy - např. jedna linka pro MHD, jedna faktura pro FAKTURY.
/// Vrací Caché GetAvailableInstances; Value se předává do GetData(LINKA_ID=Value).
/// </summary>
public record AgendaInstance(string Value, string Display, string? Subtitle);

/// <summary>
/// Skupina linek - organizační filtr nad instancemi (AD: subscript ^JRL; MHD: ENUMS "SkupinyLinek").
/// Vrací Caché GetAvailableSkupiny; Value se předává jako SKUPINA_LINEK_ID do GetAvailableInstances i GetData.
/// </summary>
public record AgendaSkupina(string Value, string Label)
{
    /// <summary>Zobrazení ve filtru „číslo - název" - hledání pak najde podle ID i názvu.</summary>
    public string Display => $"{Value} - {Label}";
}
