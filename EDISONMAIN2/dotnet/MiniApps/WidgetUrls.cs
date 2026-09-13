namespace EDISONMAIN2.MiniApps;

/// <summary>
/// URL widgetů podpora.m-line.cz na jednom místě - sdílí je fixní pruh podpory v shellu
/// i přihlašovací obrazovka (<see cref="Windows.LoginWindow.LoginWindow"/>).
/// </summary>
internal static class WidgetUrls
{
    // Base + klíč drží EDControls (používá je i panel Podpora) - jedna kopie klíče.
    // static readonly, ne const: const by se sem inlinoval a samostatný deploy EDControls.dll
    // by shellu klíč nezměnil.
    private static readonly string Base = EDControls.EDSupportPanel.SupportActions.WidgetBaseUrl;
    private static readonly string Key = EDControls.EDSupportPanel.SupportActions.WidgetKey;

    /// <summary>Centrální widget shellu - jeden blok místo tří dílčích (fixní pruh nahoře). Nese IdFa,
    /// přihlášeného uživatele a jeho e-mail (podpora podle nich cílí sdělení); prázdné jde jako prázdný parametr.</summary>
    internal static string Edison2(string idfa, string user, string email) =>
        $"{Base}edison2?key={Key}&idfa={Uri.EscapeDataString(idfa)}&user={Uri.EscapeDataString(user)}&email={Uri.EscapeDataString(email)}";

    // Novinky/Wiki jsou auth-free (jen statický klíč) → bezpečné zobrazit i před loginem.
    internal static string Novinky => $"{Base}info?key={Key}";
    internal static string Wiki => $"{Base}wiki?key={Key}";

    /// <summary>Školení dostává IdFa + přihlášeného uživatele (kvůli zpětné vazbě) - mirror frmMain.vb.
    /// Pre-login se předá prázdná identita → widget se načte bez personalizace.</summary>
    internal static string Trainings(string idfa, string user) =>
        $"{Base}trainings?key={Key}&idfa={Uri.EscapeDataString(idfa)}&user={Uri.EscapeDataString(user)}";
}
