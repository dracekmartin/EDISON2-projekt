namespace EDISONMAIN2.MiniApps;

/// <summary>
/// URL widgetů podpora.m-line.cz na jednom místě - sdílí je dockované Core mini-apps
/// (<see cref="CoreMiniApps"/>) i přihlašovací obrazovka (<see cref="Windows.LoginWindow.LoginWindow"/>).
/// </summary>
internal static class WidgetUrls
{
    // Base + klíč drží EDControls (používá je i panel Podpora) - jedna kopie klíče.
    // static readonly, ne const: const by se sem inlinoval a samostatný deploy EDControls.dll
    // by shellu klíč nezměnil.
    private static readonly string Base = EDControls.EDSupportPanel.SupportActions.WidgetBaseUrl;
    private static readonly string Key = EDControls.EDSupportPanel.SupportActions.WidgetKey;

    // Novinky/Wiki jsou auth-free (jen statický klíč) → bezpečné zobrazit i před loginem.
    internal static string Novinky => $"{Base}info?key={Key}";
    internal static string Wiki => $"{Base}wiki?key={Key}";

    /// <summary>Školení dostává IdFa + přihlášeného uživatele (kvůli zpětné vazbě) - mirror frmMain.vb.
    /// Pre-login se předá prázdná identita → widget se načte bez personalizace.</summary>
    internal static string Trainings(string idfa, string user) =>
        $"{Base}trainings?key={Key}&idfa={Uri.EscapeDataString(idfa)}&user={Uri.EscapeDataString(user)}";
}
