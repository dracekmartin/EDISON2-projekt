using System.Text.Json.Serialization;

namespace TemplateDesigner.Models;

public class Template
{
    /// <summary>
    /// Schema verze formátu Template JSON (semver, např. "1.0.0").
    /// Při breaking change v JSON schema → bumpni verzi + přidej migraci do <c>TemplateMigrator</c>.
    /// TemplateMigrator čte verzi při loadu a aplikuje pipeline migrací do aktuální verze.
    /// Detail convention: <c>docs/template-schema-versions.md</c>.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// True pokud byla šablona auto-migrována při loadu (verze v souboru byla starší než aktuální).
    /// Transient — neserializuje se. UI vrstva (status bar) podle toho zobrazí warning.
    /// </summary>
    [JsonIgnore]
    public bool WasMigrated { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nová šablona";
    public string Description { get; set; } = "";

    public PageSize PageSize { get; set; } = PageSize.A4Portrait;
    public List<Section> Sections { get; set; } = [];

    /// <summary>
    /// Kód agendy pro tuto šablonu, např. "JR", "FAKTURY".
    /// Null = obecná šablona bez vazby na konkrétní agendu.
    /// Používá se pro načtení schématu databázových polí v designeru.
    /// </summary>
    public string? AgendaCode { get; set; }

    /// <summary>
    /// Vzorová data pro preview uložená spolu se šablonou.
    /// Klíč = název pole, hodnota = ukázková hodnota jako string.
    /// </summary>
    public Dictionary<string, string>? SampleData { get; set; }

    /// <summary>
    /// Parametry sestavy — renderovací rozhodnutí vystavená uživateli před tiskem.
    /// Null / prázdný seznam = šablona nemá parametry.
    /// Připraveno pro budoucí offline podporu (bez Caché) a pro přepsání Caché defaultů per šablona.
    /// Aktuálně se parametry načítají pouze z Caché AgendaAPI.GetParameters().
    /// </summary>
    public List<TemplateParameter>? Parameters { get; set; }

    /// <summary>
    /// Kód kultury pro formátování čísel a datumů, např. "cs-CZ", "en-US".
    /// Null = výchozí "cs-CZ".
    /// </summary>
    public string? CultureCode { get; set; }

    /// <summary>
    /// Designer kontext (grid, snap) — zachová se prostředí, ve kterém byla šablona laděná.
    /// Null = applikace použije globální default při otevření.
    /// </summary>
    public TemplateEditorPreferences? EditorPreferences { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
