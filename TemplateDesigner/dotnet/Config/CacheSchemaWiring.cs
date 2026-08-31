using ALWA.SYS;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Propojení TemplateDesigneru s Caché databází přes MLRest.
/// Vytváří CacheAgendaProvider z nastavení v templatedesigner.appsettings.json.
/// Každá agenda odkazuje na Caché třídu dědící z ALVA.TemplateDesigner.Agendy.BaseAgenda.
/// </summary>
internal static class CacheSchemaWiring
{
    /// <summary>
    /// Vytvoří MLRest ze session shellu (EDISONMAIN2 launch kontrakt) - přebírá server,
    /// rest port, namespace, IdFa, identitu uživatele a session GUID. Neprázdný ShellGuid
    /// = sdílená shell session s CacheLocks=1 (requesty TD drží ACTIVITY historii shellu,
    /// HD 13132/F17; zánik shell zámku TD ukončí) - parita s EDISONMAIN2API.FormToRunGetter.
    /// Prázdný guid (legacy shell, standalone) → vlastní GUID bez kontrol jako dřív.
    /// TD nikdy nevolá Logout/LockClearExt - zámek i business zámky patří shellu.
    /// </summary>
    public static MLRest CreateMlRest(EDControls.EdisonLaunchArgs launch)
    {
        var mlRest = new MLRest(launch.Server, launch.RestPort, launch.Namespace, launch.Idfa)
        {
            LoginName = launch.LoginName,
            FullName  = launch.FullName,
            Mlp       = launch.Mlp
        };
        if (!string.IsNullOrEmpty(launch.ShellGuid))
        {
            mlRest.GUID = launch.ShellGuid;
            mlRest.CacheLocks = 1;
        }
        return mlRest;
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
    /// Vytvoří <see cref="CacheAgendaProvider"/> s předaným MLRest a seznamem agend.
    /// Agendy přichází z Caché registru (<see cref="IAgendaRegistry"/>), nikoli
    /// z <c>appsettings.json</c>. GENERAL agenda je v seznamu injektována syntheticky
    /// implementací registru. Vrátí null pokud seznam je prázdný.
    /// </summary>
    public static CacheAgendaProvider? TryCreate(MLRest mlRest, IReadOnlyList<AgendaRegistryEntry> agendas)
    {
        if (agendas.Count == 0) return null;

        // Agenda s prázdným CacheClass (např. "GENERAL") je validní volba - bez DB polí,
        // jen statický obsah šablony. CacheAgendaProvider pro ni vrací null schema.
        var tuples = agendas
            .Select(a => (a.Code, a.Label, a.CacheClass))
            .ToList();

        return new CacheAgendaProvider(mlRest, tuples);
    }
}
