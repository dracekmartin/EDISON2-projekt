namespace TemplateDesigner.Models.Schema;

/// <summary>
/// Rozhraní pro načítání schémat agend.
/// Umožňuje přepnout zdroj (JSON soubory → Caché DB) bez změny zbytku kódu.
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
