using ALWA.SYS;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Propojení TemplateDesigneru s Caché databází přes MLRest.
/// Vytváří CacheAgendaProvider z nastavení v templatedesigner.appsettings.json.
/// Každá agenda odkazuje na Caché třídu dědící z ALVA.TemplateDesigner.Agendas.AgendaAPI.
/// </summary>
internal static class CacheSchemaWiring
{
    /// <summary>
    /// Pokusí se vytvořit CacheAgendaProvider podle nastavení z templatedesigner.appsettings.json.
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
        var cache = settings.Cache;
        if (cache is null || !cache.IsConfigured) return null;

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

        // Agenda s prázdným CacheClass (např. "GENERAL") je validní volba — bez DB polí,
        // jen statický obsah šablony. CacheAgendaProvider pro ni vrací null schema.
        var agendas = settings.Agendas
            .Select(a => (a.Code, a.Name, a.CacheClass))
            .ToList();

        return new CacheAgendaProvider(mlRest, agendas);
    }
}
