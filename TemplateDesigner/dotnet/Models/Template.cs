namespace TemplateDesigner.Models;

public class Template
{
    /// <summary>Verze formátu JSON — pro budoucí migrace.</summary>
    public string Version { get; set; } = "1.0";

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}
