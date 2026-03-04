using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Serialization;

/// <summary>Metadata o jedné Fixed šabloně tabulky.</summary>
public class TableTemplateInfo
{
    public string  Key   { get; set; } = "";
    public string  Label { get; set; } = "";
}

/// <summary>Kompletní definice Fixed šablony — sloupce + řádky.</summary>
public class TableTemplateDefinition
{
    public List<TableColumn> Columns { get; set; } = [];
    public List<TableRow>    Rows    { get; set; } = [];
}

/// <summary>
/// Poskytuje seznam Fixed šablon tabulek a jejich definice.
/// Implementace: CacheAgendaProvider (z Caché) nebo JsonFileTableTemplateProvider (offline fallback).
/// </summary>
public interface ITableTemplateProvider
{
    /// <summary>Vrátí seznam dostupných šablon pro danou agendu (nebo všechny pokud agendaCode = null).</summary>
    IReadOnlyList<TableTemplateInfo> GetAvailableTemplates(string? agendaCode = null);

    /// <summary>Vrátí kompletní definici šablony podle klíče. Null = šablona neexistuje.</summary>
    TableTemplateDefinition? GetTemplate(string key);
}
