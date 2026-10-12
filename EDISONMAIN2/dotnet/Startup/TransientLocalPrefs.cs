namespace EDISONMAIN2;

/// <summary>
/// Preference E2E běhu jen v paměti: start je deterministický (výchozí firma a rozložení)
/// a vývojářův <c>local.json</c> zůstane netknutý.
/// </summary>
internal sealed class TransientLocalPrefs : ILocalPrefs
{
    public string? LastIdfa { get; set; }
    public Dictionary<string, string>? LoginNames { get; set; }
    public Dictionary<string, string>? RememberedPasswords { get; set; }
    [Obsolete(ServerLogins.LegacyLoginObsolete)] public string? LastLoginName { get; init; }
    [Obsolete(ServerLogins.LegacyLoginObsolete)] public string? RememberedPassword { get; init; }
    public Dictionary<string, string>? MiniAppLayouts { get; set; }
    public Dictionary<string, double>? TreeWidths { get; set; }
    public Dictionary<string, double>? SupportStripHeights { get; set; }
    public List<SavedConnection>? Connections { get; set; }
    public WindowBoundsPrefs? ShellWindowBounds { get; set; }

    public void Save() { }
}
