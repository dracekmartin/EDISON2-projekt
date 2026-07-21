namespace EDISONMAIN2.MiniApps;

/// <summary>
/// URL widgetů podpora.m-line.cz na jednom místě — sdílí je dockované Core mini-apps
/// (<see cref="CoreMiniApps"/>) i přihlašovací obrazovka (<see cref="Windows.LoginWindow.LoginWindow"/>).
/// </summary>
internal static class WidgetUrls
{
    private const string Base = "https://podpora.m-line.cz/api/widgets/";
    private const string Key = "80716cc86270f1daff6a18ad0b2ba07e9e30011eea22072c7a840604b18423c6";

    // Novinky/Wiki jsou auth-free (jen statický klíč) → bezpečné zobrazit i před loginem.
    internal static string Novinky => $"{Base}info?key={Key}";
    internal static string Wiki => $"{Base}wiki?key={Key}";

    /// <summary>Školení dostává IdFa + přihlášeného uživatele (kvůli zpětné vazbě) — mirror frmMain.vb.
    /// Pre-login se předá prázdná identita → widget se načte bez personalizace.</summary>
    internal static string Trainings(string idfa, string user) =>
        $"{Base}trainings?key={Key}&idfa={Uri.EscapeDataString(idfa)}&user={Uri.EscapeDataString(user)}";
}
