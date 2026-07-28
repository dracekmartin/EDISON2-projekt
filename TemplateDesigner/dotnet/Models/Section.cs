using System.Text.Json.Serialization;

namespace TemplateDesigner.Models;

/// <summary>
/// Univerzální sekce šablony.
/// Chování sekce je určeno properties (PrintBehavior, OverflowBehavior), ne typem třídy.
/// </summary>
public class Section
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Sekce";

    /// <summary>
    /// Výška sekce v mm pro zobrazení v designeru.
    /// Null u dynamických sekcí (tabulka, nekonečný text) - designer zobrazí placeholder výšku.
    /// Print engine tuto hodnotu ignoruje.
    /// </summary>
    public float? DesignerHeight { get; set; } = 30f;

    /// <summary>Určuje, kdy a jak často se sekce tiskne.</summary>
    public SectionPrintBehavior PrintBehavior { get; set; } = SectionPrintBehavior.Once;

    /// <summary>Určuje, jak se sekce chová při přetečení stránky.</summary>
    public SectionOverflowBehavior OverflowBehavior { get; set; } = SectionOverflowBehavior.KeepTogether;

    /// <summary>
    /// Role sekce na stránce - Header (nahoře), Body (střed), Footer (dole).
    /// Relevantní pro opakující se sekce (OnEveryPage, OnFirstPageOnly, ...):
    /// Header i Footer se opakují, ale Header je umístěn nahoře a Footer dole na stránce.
    /// Pro PrintBehavior=Once nemá vliv na umístění (sekce je tam kde je v pořadí).
    /// </summary>
    public SectionRole Role { get; set; } = SectionRole.Body;

    /// <summary>
    /// Podmínka viditelnosti (volitelné). Null = vždy viditelné.
    /// Příklady: "{PAGE} > 1" (kromě první stránky), "{POCET} > 0" (pokud jsou data).
    /// Vyhodnocuje print engine, designer zobrazuje vždy.
    /// </summary>
    public string? VisibilityExpression { get; set; }

    // --- Rámeček sekce v tisku/preview ---

    /// <summary>Zobrazit rámeček kolem sekce v preview a PDF.</summary>
    public bool ShowBorder { get; set; } = false;

    /// <summary>Barva rámečku sekce (#AARRGGBB). Výchozí: černá.</summary>
    public string BorderColor { get; set; } = "#FF000000";

    /// <summary>Tloušťka rámečku sekce v mm.</summary>
    public float BorderWidth { get; set; } = 0.3f;

    // --- Speciální properties pro některé typy sekcí ---

    /// <summary>
    /// Klíč datového zdroje pro opakující se řádky (SectionTableDesigned).
    /// Používá se pouze pokud PrintBehavior = PerDataRow.
    /// Příklad: "JIZDNI_RAD", "POLOZKY_FAKTURY".
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Výška jednoho řádku v mm (SectionTableDesigned).
    /// Používá se pouze pokud PrintBehavior = PerDataRow.
    /// </summary>
    public float? RowHeight { get; set; }

    // --- Elementy a extensibility ---

    public List<TemplateElement> Elements { get; set; } = [];

    /// <summary>Bag pro budoucí vlastnosti bez zásahu do modelu.</summary>
    public Dictionary<string, string>? CustomProperties { get; set; }

    // --- Computed properties ---

    /// <summary>Skutečná výška pro designer - DesignerHeight nebo placeholder.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => DesignerHeight ?? DefaultPlaceholderHeight;

    /// <summary>Výchozí placeholder výška pro dynamické sekce (null DesignerHeight).</summary>
    [JsonIgnore]
    public virtual float DefaultPlaceholderHeight => 30f;

    /// <summary>
    /// Vytvoří hlubokou kopii sekce s novým Id a novými Id všech elementů.
    /// Používá se při duplicitě a načítání custom šablon.
    /// </summary>
    public Section Clone()
    {
        var clone = new Section
        {
            Id = Guid.NewGuid(),
            Name = Name,
            PrintBehavior = PrintBehavior,
            OverflowBehavior = OverflowBehavior,
            Role = Role,
            ShowBorder = ShowBorder,
            BorderColor = BorderColor,
            BorderWidth = BorderWidth,
            DesignerHeight = DesignerHeight,
            VisibilityExpression = VisibilityExpression,
            DataSource = DataSource,
            RowHeight = RowHeight,
            Elements = Elements.Select(e => e.Clone()).ToList(),
            CustomProperties = CustomProperties is not null
                ? new Dictionary<string, string>(CustomProperties)
                : null
        };
        return clone;
    }
}

// ---------------------------------------------------------------------------

public enum SectionPrintBehavior
{
    /// <summary>Vytiskne se jednou na místě kde je.</summary>
    Once,
    /// <summary>Na každé stránce (záhlaví, zápatí).</summary>
    OnEveryPage,
    /// <summary>Pouze na první stránce.</summary>
    OnFirstPageOnly,
    /// <summary>Pouze na poslední stránce.</summary>
    OnLastPageOnly,
    /// <summary>Na každé stránce kromě první (alternativní záhlaví).</summary>
    OnEveryPageExceptFirst,
    /// <summary>Opakuje se per řádek dat (tabulky s designem vlastních řádků).</summary>
    PerDataRow
}

public enum SectionRole
{
    /// <summary>Běžný obsah stránky (výchozí).</summary>
    Body,
    /// <summary>Hlavička - umístěna nahoře na stránce.</summary>
    Header,
    /// <summary>Patička - umístěna dole na stránce.</summary>
    Footer
}

public enum SectionOverflowBehavior
{
    /// <summary>Celá sekce přejde na další stránku pokud se nevejde.</summary>
    KeepTogether,
    /// <summary>Sekce se rozdělí - pokračuje na další stránce (tabulky).</summary>
    SplitAcrossPages,
    /// <summary>Rozdělení povoleno ale ne preferováno (textové bloky).</summary>
    AllowSplit
}
