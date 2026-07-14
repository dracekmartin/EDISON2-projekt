using System.Windows;
using EDControls.EDTheme;
using EDControls.UserFonts;

namespace EDControls;

/// <summary>
/// Jedna společná registrace sdíleného vzhledu m-line nástrojů na startupu: uživatelský font
/// (<see cref="FontService"/>) + světlý/tmavý režim (<see cref="ThemeService"/>). Obojí čte
/// sdílené preference v <c>%APPDATA%\m-line\common\</c> a propaguje změny mezi procesy
/// (shell ↔ TemplateDesigner) přes FileSystemWatcher.
///
/// Volat jednou v App startupu **před** zobrazením prvního okna (eliminuje flicker z defaultů).
/// Nová m-line WPF apka stačí zavolat tohle jediné.
/// </summary>
public static class EDAppearance
{
    public static void Initialize(Application app)
    {
        if (app is null) return;   // testy bez WPF Application
        FontService.Initialize(app);
        ThemeService.Initialize(app);
    }
}
