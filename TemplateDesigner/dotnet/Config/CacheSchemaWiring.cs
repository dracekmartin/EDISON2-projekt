using ALWA.SYS;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Propojení TemplateDesigneru s Caché databází přes MLRest.
/// Vytváří CacheAgendaProvider z nastavení v appsettings.json.
/// Každá agenda odkazuje na Caché třídu dědící z ALVA.TemplateDesigner.API.AgendaAPI.
/// </summary>
internal static class CacheSchemaWiring
{
    /// <summary>
    /// Pokusí se vytvořit CacheAgendaProvider podle nastavení z appsettings.json.
    /// Vrátí null pokud připojení není nakonfigurováno nebo nejsou definovány žádné agendy.
    /// </summary>
    public static CacheAgendaProvider? TryCreate(AppSettings settings)
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
            .Where(a => !string.IsNullOrWhiteSpace(a.CacheClass))
            .Select(a => (a.Code, a.Name, a.CacheClass))
            .ToList();

        return new CacheAgendaProvider(mlRest, agendas);
    }
}
