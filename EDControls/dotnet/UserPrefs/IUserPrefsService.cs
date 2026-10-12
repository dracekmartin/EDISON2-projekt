using System.Text.Json.Nodes;

namespace EDControls.UserPrefs;

/// <summary>
/// Persistuje JSON state per-user (column layouty, hidden položky, ...) pod klíčem (scope × key).
/// Implementace zajišťuje rozlišení uživatele (Caché si bere IdFa + MLP z MLRest sys kontextu).
///
/// scope = jmenný prostor komponenty, fully qualified, např. "EDControls.EDList".
/// key   = identifikátor instance, např. "TemplateDesigner.TemplatePicker".
///
/// Hodnota je opaque JSON - každá komponenta si definuje vlastní strukturu, service ji neinterpretuje.
/// </summary>
public interface IUserPrefsService
{
    /// <summary>Vrátí persistovaný JSON, nebo null pokud klíč neexistuje.</summary>
    Task<JsonNode?> LoadAsync(string scope, string key);

    /// <summary>Uloží JSON pod klíčem. Přepíše existující hodnotu.</summary>
    Task SaveAsync(string scope, string key, JsonNode data);

    /// <summary>Smaže klíč. No-op pokud neexistoval.</summary>
    Task DeleteAsync(string scope, string key);
}

/// <summary>Snímek session pro odložený zápis přes libovolnou <see cref="IUserPrefsService"/>.</summary>
public static class UserPrefsSession
{
    /// <summary>
    /// Odložený zápis bere snímek při změně, ne při odeslání: přepnutí firmy mezi nimi by ho poslalo pod novou
    /// firmou. Dokud se session nezmění, vrací týž objekt. Služba bez session (no-op, testovací) vrací sama sebe.
    /// </summary>
    public static IUserPrefsService Capture(this IUserPrefsService prefs) =>
        prefs is CacheUserPrefsService cache ? cache.Capture() : prefs;
}
