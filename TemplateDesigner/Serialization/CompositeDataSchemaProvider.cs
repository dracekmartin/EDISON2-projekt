using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Kombinuje dva providery: primární (Caché) a zálohu (JSON soubory).
/// Pokud primární vrátí null (chyba připojení, neznámá agenda), zkusí zálohu.
/// GetAvailableAgendas() vrátí sjednocení obou zdrojů (bez duplikátů).
/// </summary>
public class CompositeDataSchemaProvider : IDataSchemaProvider
{
    private readonly IDataSchemaProvider _primary;
    private readonly IDataSchemaProvider _fallback;

    public CompositeDataSchemaProvider(IDataSchemaProvider primary, IDataSchemaProvider fallback)
    {
        _primary  = primary;
        _fallback = fallback;
    }

    public IReadOnlyList<AgendaInfo> GetAvailableAgendas()
    {
        var result = _primary.GetAvailableAgendas().ToList();

        foreach (var agenda in _fallback.GetAvailableAgendas())
        {
            if (!result.Any(a => a.Code.Equals(agenda.Code, StringComparison.OrdinalIgnoreCase)))
                result.Add(agenda);
        }

        return result;
    }

    public DataSchema? GetSchema(string agendaCode) =>
        _primary.GetSchema(agendaCode) ?? _fallback.GetSchema(agendaCode);
}
