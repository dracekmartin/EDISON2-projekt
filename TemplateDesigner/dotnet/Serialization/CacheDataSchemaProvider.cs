using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Načítá schéma agendy z Caché databáze přes delegate.
/// TemplateDesigner neodkazuje přímo na ALWA.SYS - volající předá Func, která
/// zavolá MLRest.Query a vrátí seznam názvů polí pro danou agendu.
/// </summary>
public class CacheDataSchemaProvider : IDataSchemaProvider
{
    private readonly List<AgendaInfo> _agendas;

    /// <summary>
    /// Delegate volaný pro každé GetSchema(agendaCode).
    /// Vstup: kód agendy (např. "JR").
    /// Výstup: seznam názvů polí, nebo null při chybě připojení / neznámé agendě.
    /// </summary>
    private readonly Func<string, IReadOnlyList<string>?> _fetchFieldNames;

    private readonly Dictionary<string, DataSchema> _cache = new();

    /// <param name="agendas">Seznam agend dostupných přes Caché.</param>
    /// <param name="fetchFieldNames">
    /// Delegate vracející flat seznam názvů polí pro daný agendaCode.
    /// Příklad implementace s MLRest:
    /// <code>
    /// agendaCode => {
    ///     var result = mlRest.Query("ALWA.MHD2.Tisk.Designer", "LoadDBFieldIds",
    ///                               new object[] { agendaCode });
    ///     if (!string.IsNullOrEmpty(result[0]?.ToString())) return null; // chyba
    ///     return result.Skip(1)
    ///         .Select(o => o?.ToString())
    ///         .Where(s => !string.IsNullOrEmpty(s))
    ///         .ToList()!;
    /// }
    /// </code>
    /// </param>
    public CacheDataSchemaProvider(
        IEnumerable<AgendaInfo> agendas,
        Func<string, IReadOnlyList<string>?> fetchFieldNames)
    {
        _agendas         = agendas.ToList();
        _fetchFieldNames = fetchFieldNames;
    }

    public IReadOnlyList<AgendaInfo> GetAvailableAgendas() => _agendas;

    /// <summary>
    /// Parsuje jeden řetězec z Caché do DataField.
    /// Podporované formáty:
    ///   "NAZEV_POLE|Label"          → nový formát (preferovaný)
    ///   "DBFieldId~NAZEV_POLE"      → starý formát bez labelu
    ///   "DBFieldId~NAZEV_POLE|Label"→ starý formát s labelem
    /// </summary>
    private static DataField ParseField(string raw)
    {
        // Odeber starý prefix DBFieldId~ pokud je přítomen
        string s = raw.StartsWith("DBFieldId~", StringComparison.OrdinalIgnoreCase)
            ? raw[10..] : raw;

        // Rozděl na název a label podle '|'
        int pipe = s.IndexOf('|');
        string name  = (pipe >= 0 ? s[..pipe]       : s).Trim();
        string label = (pipe >= 0 ? s[(pipe + 1)..] : s).Trim();

        return new DataField { FieldName = name, Label = string.IsNullOrEmpty(label) ? name : label };
    }

    public DataSchema? GetSchema(string agendaCode)
    {
        if (_cache.TryGetValue(agendaCode, out var cached))
            return cached;

        var agenda = _agendas.FirstOrDefault(a =>
            a.Code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase));
        if (agenda is null)
            return null;

        var fieldNames = _fetchFieldNames(agendaCode);
        if (fieldNames is null)
            return null;

        var schema = new DataSchema
        {
            AgendaCode = agendaCode,
            AgendaName = agenda.Name,
            Fields     = fieldNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(ParseField)
                .Where(f => !string.IsNullOrWhiteSpace(f.FieldName))
                .ToList()
        };

        _cache[agendaCode] = schema;
        return schema;
    }
}
