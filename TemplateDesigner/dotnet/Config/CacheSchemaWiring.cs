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
        var mlRest = TryCreateMlRest(settings);
        return mlRest is not null ? TryCreate(mlRest, settings) : null;
    }

    /// <summary>
    /// Vytvoří MLRest instanci z nastavení. Vrátí null pokud připojení není nakonfigurováno.
    /// </summary>
    public static MLRest? TryCreateMlRest(AppSettings settings)
    {
        if (!settings.Cache.IsConfigured) return null;

        var cache = settings.Cache;
        return new MLRest(cache.Server, cache.Port, cache.Namespace, cache.Idfa)
        {
            LoginName = cache.LoginName,
            FullName  = cache.FullName,
            Mlp       = cache.Mlp
        };
    }

    /// <summary>
    /// Vytvoří CacheAgendaProvider s předaným MLRest (EDISON mód — MLRest pochází z hlavní aplikace).
    /// Vrátí null pokud nejsou definovány žádné agendy.
    /// </summary>
    public static CacheAgendaProvider? TryCreate(MLRest mlRest, AppSettings settings)
    {
        if (settings.Agendas.Count == 0) return null;

        var agendas = settings.Agendas
            .Where(a => !string.IsNullOrWhiteSpace(a.CacheClass))
            .Select(a => (a.Code, a.Name, a.CacheClass))
            .ToList();

        return agendas.Count == 0 ? null : new CacheAgendaProvider(mlRest, agendas);
    }
}
