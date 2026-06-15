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
}

/// <summary>Handle jednoho otevřeného panelu v docku.</summary>
public interface IMiniAppPane
{
    void Close();
}
