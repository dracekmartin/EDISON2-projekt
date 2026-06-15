namespace EDControls.MiniApps;

/// <summary>
/// Implementuje API projekt agendy a vrací mini-apps, které vystavuje do shellu. Registruje se
/// v composition root (<c>AppBootstrapper</c>) do <see cref="MiniAppCatalog"/>.
/// </summary>
public interface IMiniAppProvider
{
    IEnumerable<MiniAppDescriptor> GetMiniApps();
}
