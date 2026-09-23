namespace EDControls.SessionState;

/// <summary>
/// Přechodný stav sdílený mezi instancemi controlů po dobu běhu procesu - to, co má přežít
/// zavření dialogu, ale ne restart aplikace (naposledy hledaný výraz, rozepsaná volba).
/// Protipól <see cref="UserPrefs.IUserPrefsService"/>, který ukládá do Caché a restart přežije.
///
/// scope = jmenný prostor komponenty, např. "EDInputForm.Search".
/// key   = identifikátor instance dodaný hostem, např. "TemplateDesigner.ElementEdit".
///
/// Hodnota je opaque string - komponenta si strukturu (třeba JSON) definuje sama.
/// </summary>
public interface ISessionState
{
    /// <summary>Vrátí zapamatovanou hodnotu, nebo null pokud klíč není.</summary>
    string? Get(string scope, string key);

    /// <summary>Zapamatuje hodnotu pod klíčem. <c>null</c> klíč zapomene.</summary>
    void Set(string scope, string key, string? value);
}
