using System.Windows;
using EDControls.EDTheme;
using EDControls.UserFonts;

namespace EDControls;

/// <summary>
/// Jedna společná registrace sdíleného vzhledu m-line nástrojů na startupu: uživatelský font
/// (<see cref="FontService"/>), světlý/tmavý režim (<see cref="ThemeService"/>), panel Podpora
/// (<see cref="EDSupportPanel.EDSupportPanel"/>) a usage telemetrie
/// (<see cref="EDViewModel.EDUsageTelemetryHook"/>) a obnovu ikony v liště (<see cref="TaskbarIcon"/>).
/// Font a téma čtou sdílené preference
/// v <c>%APPDATA%\m-line\common\</c> a propagují změny mezi procesy (shell ↔ TemplateDesigner)
/// přes FileSystemWatcher.
///
/// Volat jednou v App startupu **před** zobrazením prvního okna (eliminuje flicker z defaultů)
/// a **po** <c>EDServices.Initialize</c> (panel Podpora z něj bere služby).
/// Nová m-line WPF apka stačí zavolat tohle jediné.
/// </summary>
public static class EDAppearance
{
    public static void Initialize(Application app)
    {
        if (app is null) return;   // testy bez WPF Application
        FontService.Initialize(app);
        ThemeService.Initialize(app);
        // Panel Podpora a usage telemetrie se váží na Window.Loaded, ne na ViewModel - okno je
        // dostane i tam, kde se nespouští přes WindowNavigator (hlavní okno a dialogy TemplateDesigneru).
        EDSupportPanel.EDSupportPanel.AttachToAllWindows();
        EDViewModel.EDUsageTelemetryHook.AttachToAllWindows();
        // Taskbar Win11 se na ikonu ptá jen jednou po vzniku okna; blokovaný startup ho nechá na výchozí.
        TaskbarIcon.AttachToAllWindows();
    }

    /// <summary>
    /// Přesměruje celou sdílenou složku <c>%APPDATA%\m-line\common</c> (téma, písmo i záložní log
    /// chyb): E2E běh nesmí číst ani přepsat preference vývojáře. Volat před <see cref="Initialize"/>
    /// a před prvním použitím cesty, potom hodí <see cref="InvalidOperationException"/>.
    /// </summary>
    public static void UseCommonFolder(string folder) => MlineCommonPaths.Redirect(folder);
}
