using System.Text.Json;
using ALWA.SYS;
using EDControls;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Services;

/// <summary>
/// Služba pro načtení dostupných instancí agendy (např. linek pro MHD) z Caché.
/// Volá <c>ALVA.TemplateDesigner.API.InstancesList.LoadList</c> — wire formát Styl 3
/// (CVar named IN + JSON multi-slot OUT) podle EDControls/DOCS/01-wire-formats.md.
///
/// Pozn.: Tato služba je alternativní cesta k EDList komponentě, která Caché endpoint
/// volá interně sama. Použít ji, když chceš stejná data bez UI komponenty (např. batch
/// volání, testy, nebo když EDList není vhodný).
/// </summary>
public class CacheInstanceService
{
    private const string CacheClass = "ALVA.TemplateDesigner.API.InstancesList";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IMLRest _mlRest;

    public CacheInstanceService(IMLRest mlRest)
    {
        _mlRest = mlRest;
    }

    /// <summary>
    /// Načte dostupné instance pro danou agendu.
    /// </summary>
    public async Task<List<AgendaInstance>> LoadInstancesAsync(string agendaCode)
    {
        var input = new CVar();
        input["agendaCode"] = new CVar(agendaCode);

        var output = await Task.Run(() => _mlRest.QueryOrThrow(CacheClass, "LoadList", input));

        var rows = new List<AgendaInstance>();
        foreach (KeyValuePair<string, CVar> kvp in output)
        {
            if (kvp.Key == "ERR") continue;
            var json = kvp.Value?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(json)) continue;
            var row = JsonSerializer.Deserialize<AgendaInstance>(json, JsonOpts);
            if (row is not null) rows.Add(row);
        }
        return rows;
    }
}
