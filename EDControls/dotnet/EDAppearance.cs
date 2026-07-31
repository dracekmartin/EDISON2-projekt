using System.Windows;
using EDControls.EDTheme;
using EDControls.UserFonts;

namespace EDControls;

/// <summary>
/// Jedna společná registrace sdíleného vzhledu m-line nástrojů na startupu: uživatelský font
/// (<see cref="FontService"/>), světlý/tmavý režim (<see cref="ThemeService"/>) a panel Podpora
/// (<see cref="EDSupportPanel.EDSupportPanel"/>). Font a téma čtou sdílené preference
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
        // Panel Podpora se váže na Window.Loaded, ne na ViewModel — okno ho dostane i tam, kde se
        // nespouští přes WindowNavigator (hlavní okno a dialogy TemplateDesigneru).
        EDSupportPanel.EDSupportPanel.AttachToAllWindows();
    }
}
