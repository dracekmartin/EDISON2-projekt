namespace EDControls.MiniApps;

/// <summary>
/// Mini-app, jejíž data jdou znovu načíst. Implementuje VM s obnovitelným obsahem (typicky volá
/// stejnou cestu jako tlačítko „Načíst"). Hromadný refresh zařizuje shellový <c>MiniAppManager</c>.
/// </summary>
public interface IRefreshableMiniApp
{
    /// <summary>Znovu načte data mini-app (ekvivalent ručního „Načíst").</summary>
    void Refresh();
}
