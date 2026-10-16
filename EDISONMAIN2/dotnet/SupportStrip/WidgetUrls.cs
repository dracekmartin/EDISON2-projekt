namespace EDISONMAIN2.SupportStrip;

/// <summary>
/// URL widgetu podpora.m-line.cz na jednom místě - sdílí ho fixní pruh podpory v shellu
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
    /// přihlášeného uživatele a jeho e-mail (podpora podle nich cílí sdělení); prázdné jde jako prázdný parametr.
    /// Přihlašovací obrazovka posílá jen IdFa - uživatel ještě není ověřený.</summary>
    internal static string Edison2(string idfa, string user, string email) =>
        $"{Base}edison2?key={Key}&idfa={Uri.EscapeDataString(idfa)}&user={Uri.EscapeDataString(user)}&email={Uri.EscapeDataString(email)}";
}
