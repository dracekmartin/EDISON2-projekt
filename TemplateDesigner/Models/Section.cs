using System.Text.Json.Serialization;
using TemplateDesigner.Models.Sections;

namespace TemplateDesigner.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SectionHeader),        "SectionHeader")]
[JsonDerivedType(typeof(SectionFixedHeight),   "SectionFixedHeight")]
[JsonDerivedType(typeof(SectionTable),         "SectionTable")]
[JsonDerivedType(typeof(SectionTableDesigned), "SectionTableDesigned")]
[JsonDerivedType(typeof(SectionInfiniteText),  "SectionInfiniteText")]
[JsonDerivedType(typeof(SectionSeparator),     "SectionSeparator")]
[JsonDerivedType(typeof(SectionFooter),        "SectionFooter")]
public abstract class Section
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>
    /// Výška sekce v mm pro zobrazení v designeru.
    /// Null u dynamických sekcí (tabulka, nekonečný text) — designer zobrazí placeholder výšku.
    /// Print engine tuto hodnotu ignoruje.
    /// </summary>
    public float? DesignerHeight { get; set; }

    public SectionPrintBehavior PrintBehavior { get; set; } = SectionPrintBehavior.Once;
    public SectionOverflowBehavior OverflowBehavior { get; set; } = SectionOverflowBehavior.KeepTogether;

    public List<TemplateElement> Elements { get; set; } = [];

    /// <summary>Bag pro budoucí vlastnosti bez zásahu do modelu.</summary>
    public Dictionary<string, string>? CustomProperties { get; set; }

    /// <summary>Skutečná výška pro designer — DesignerHeight nebo placeholder.</summary>
    [JsonIgnore]
    public float EffectiveDesignerHeight => DesignerHeight ?? DefaultPlaceholderHeight;

    protected virtual float DefaultPlaceholderHeight => 30f;
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
    OnEveryPageExceptFirst
}

public enum SectionOverflowBehavior
{
    /// <summary>Celá sekce přejde na další stránku pokud se nevejde.</summary>
    KeepTogether,
    /// <summary>Sekce se rozdělí — pokračuje na další stránce (tabulky).</summary>
    SplitAcrossPages,
    /// <summary>Rozdělení povoleno ale ne preferováno (textové bloky).</summary>
    AllowSplit
}
