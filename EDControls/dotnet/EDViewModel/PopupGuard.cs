using System.Windows;
using System.Windows.Threading;

namespace EDControls.EDViewModel;

/// <summary>
/// Po začátku ukončení aplikace WPF nemá <c>Application.Current</c> ani balík zdrojů a načtení XAML okna
/// hodí „The Application object is being shut down." Pozdní hláška (konec session, chyba zavíracího
/// zápisu) proto okno nevytvoří, hlášení do telemetrie už odešlo.
/// </summary>
internal static class PopupGuard
{
    internal static bool CanOpen() => CanOpen(Application.Current?.Dispatcher);

    internal static bool CanOpen(Dispatcher? appDispatcher) => appDispatcher is { HasShutdownStarted: false };

    /// <summary>Zavřený owner WPF odmítne, hláška pak jde bez něj.</summary>
    internal static void SetOwner(Window popup, Window? owner)
    {
        try { popup.Owner = owner; }
        catch (InvalidOperationException) { }
    }
}
