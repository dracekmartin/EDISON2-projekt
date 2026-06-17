using System.Windows.Media;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Seam mezi <see cref="MiniAppManager"/> a docking frameworkem (AvalonDock). Drží manager
/// testovatelný mimo STA vlákno (fake host); reálná implementace je <see cref="AvalonDockHost"/>.
/// </summary>
public interface IMiniAppHost
{
    /// <summary>
    /// Přidá panel do docku a vrátí jeho handle. <paramref name="onClosed"/> se zavolá, když se
    /// panel zavře — uživatelem (křížek) i programově (<see cref="IMiniAppPane.Close"/>).
    /// </summary>
    IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed);

    /// <summary>Serializuje aktuální rozložení docku (geometrie + ContentId panelů).</summary>
    string SaveLayout();

    /// <summary>
    /// Obnoví rozložení z <paramref name="layoutXml"/>. Pro každý panel zavolá <paramref name="resolve"/>
    /// s jeho <c>ContentId</c> + handle; vrácený obsah se vloží do panelu a napojí se jeho zavření.
    /// Null z resolve = panel zahodit (descriptor už neexistuje).
    /// </summary>
    void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve);
}

/// <summary>Handle jednoho otevřeného panelu v docku.</summary>
public interface IMiniAppPane
{
    void Close();
}

/// <summary>Obsah obnoveného panelu — VM a handler jeho zavření (z <see cref="MiniAppManager"/>).</summary>
public sealed record RestoredContent(object Content, Action OnClosed);
