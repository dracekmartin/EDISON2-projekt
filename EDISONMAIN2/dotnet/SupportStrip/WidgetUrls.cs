namespace EDISONMAIN2.SupportStrip;

/// <summary>
/// URL widgetu podpora.m-line.cz pro fixní pruh podpory v shellu.
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
}
