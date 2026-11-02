namespace TemplateDesigner.Models.Schema;

/// <summary>
/// Rozhraní pro načítání schémat agend. Se serverem je to <c>CacheAgendaProvider</c>, bez něj
/// <see cref="EmptyDataSchemaProvider"/>; testy si dosazují vlastní.
/// </summary>
public interface IDataSchemaProvider
{
    /// <summary>Vrátí seznam dostupných agend (kód + název).</summary>
    IReadOnlyList<AgendaInfo> GetAvailableAgendas();

    /// <summary>
    /// Vrátí schéma pro danou agendu, nebo null pokud agenda neexistuje.
    /// </summary>
    DataSchema? GetSchema(string agendaCode);
}

/// <summary>Nabídka dat bez serveru: žádné agendy ani pole. Do 30. 10. tu byla záloha ze souborů
/// <c>schemas/*.schema.json</c>, jenže ta složka od <c>899e05ba</c> neexistuje.</summary>
public sealed class EmptyDataSchemaProvider : IDataSchemaProvider
{
    public static readonly EmptyDataSchemaProvider Instance = new();

    public IReadOnlyList<AgendaInfo> GetAvailableAgendas() => [];

    public DataSchema? GetSchema(string agendaCode) => null;
}
