using System.Text.Json;
using ALWA.SYS;
using EDControls;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Caché-backed implementace <see cref="IAgendaRegistry"/> - volá
/// `ALVA.TemplateDesigner.API.AgendyRegistr.ListAgendy` přes MLRest.
///
/// MLRest call je sync (HTTP); wrap v <see cref="Task.Run(System.Action)"/> uvolní
/// UI thread (R1). Při chybě (Caché nedostupné, class neexistuje, ...) vrátí jen
/// GENERAL agendu a chybu - TD startuje funkčně i bez registru, jen bez Caché-driven agend.
/// </summary>
public class CacheAgendaRegistry : IAgendaRegistry
{
    public const string CacheClass = "ALVA.TemplateDesigner.API.AgendyRegistr";

    /// <summary>
    /// GENERAL agenda - šablona bez DB vazby. Nemá Caché class, je vždy dostupná.
    /// Injektována syntheticky, ne vyhledávána z auto-discovery.
    /// </summary>
    private static readonly AgendaRegistryEntry GeneralAgenda =
        new("GENERAL", "Obecná (bez dat)", "",
            Agenda: "GENERAL", AgendaLabel: "Obecná (bez dat)");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMLRest _mlRest;

    public CacheAgendaRegistry(IMLRest mlRest) => _mlRest = mlRest;

    public Task<AgendaRegistryLoad> ListAsync() => Task.Run(ListSync);

    private AgendaRegistryLoad ListSync()
    {
        var combined = new List<AgendaRegistryEntry> { GeneralAgenda };

        try
        {
            CVar result = _mlRest.QueryOrThrow(CacheClass, "ListAgendy", new CVar());
            string? json = result["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return new(combined);

            var fromCache = JsonSerializer.Deserialize<List<AgendaRegistryEntry>>(json, JsonOptions);
            if (fromCache is { Count: > 0 }) combined.AddRange(fromCache);
            return new(combined);
        }
        catch (Exception ex)
        {
            return new(combined, ex);
        }
    }
}
