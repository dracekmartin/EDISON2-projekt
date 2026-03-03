using System.Diagnostics;
using ALWA.SYS;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Propojení TemplateDesigneru s Caché databází přes MLRest.
/// Vytváří CacheDataSchemaProvider z nastavení v appsettings.json.
/// </summary>
internal static class CacheSchemaWiring
{
    /// <summary>
    /// Pokusí se vytvořit CacheDataSchemaProvider podle nastavení z appsettings.json.
    /// Vrátí null pokud:
    /// — připojení není nakonfigurováno (server prázdný nebo port 0)
    /// — nejsou definovány žádné agendy
    /// </summary>
    public static CacheDataSchemaProvider? TryCreate(AppSettings settings)
    {
        if (!settings.Cache.IsConfigured || settings.Agendas.Count == 0)
            return null;

        var cache = settings.Cache;

        var mlRest = new MLRest(cache.Server, cache.Port, cache.Namespace, cache.Idfa)
        {
            LoginName = cache.LoginName,
            FullName  = cache.FullName,
            Mlp       = cache.Mlp
        };

        var agendas = settings.Agendas
            .Select(a => new AgendaInfo(a.Code, a.Name))
            .ToList();

        return new CacheDataSchemaProvider(
            agendas: agendas,
            fetchFieldNames: agendaCode =>
            {
                var agenda = settings.Agendas.FirstOrDefault(a =>
                    a.Code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase));
                if (agenda is null) return null;

                try
                {
                    // CVar Query overload — výsledek je Caché variable/array
                    CVar lOut = mlRest.Query(agenda.CacheClass, agenda.CacheMethod, new CVar());

                    Debug.WriteLine($"[Schema] RestStatus: {mlRest.RestStatus.State}");

                    if (mlRest.RestStatus.State == MLRStatus.EState.Error)
                    {
                        Debug.WriteLine($"[Schema] Chyba: {mlRest.RestStatus.Message}");
                        return null;
                    }

                    // Diagnostika: root hodnota CVar (= počet záznamů v ^||ML("OUT"))
                    Debug.WriteLine($"[Schema] lOut.Value (root) = '{lOut.Value}'");

                    // Diagnostika: všechny přijaté záznamy
                    var allEntries = lOut.ToList();
                    Debug.WriteLine($"[Schema] Počet položek v CVar: {allEntries.Count}");
                    foreach (var kvp in allEntries)
                        Debug.WriteLine($"[Schema]   Key='{kvp.Key}'  Value='{kvp.Value.Value}'");

                    // lOut je CVar s numerickými klíči ("1", "2", ...) odpovídajícími
                    // ^||ML("OUT", 1..n) z Caché metody — každý obsahuje název pole
                    var result = allEntries
                        .Select(kvp => kvp.Value.Value?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList()!;

                    Debug.WriteLine($"[Schema] Po filtrování: {result.Count} polí");
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Schema] Výjimka: {ex.GetType().Name}: {ex.Message}");
                    return null; // chyba připojení → fallback na JSON schema
                }
            });
    }
}
