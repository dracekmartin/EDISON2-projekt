namespace EDControls.MiniApps;

/// <summary>
/// Implementuje API projekt agendy a vrací mini-apps, které vystavuje do shellu. Registruje se
/// v composition root (<c>AppBootstrapper</c>) do <see cref="MiniAppCatalog"/>.
/// </summary>
public interface IMiniAppProvider
{
    /// <summary>Název skupiny (submenu) v menu „Miniaplikace". Pořadí skupin = pořadí registrace providerů.</summary>
    string GroupTitle { get; }

    IEnumerable<MiniAppDescriptor> GetMiniApps();
}
