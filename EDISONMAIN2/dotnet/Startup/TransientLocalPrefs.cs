namespace EDISONMAIN2;

/// <summary>
/// Preference E2E běhu jen v paměti: start je deterministický (výchozí firma a rozložení)
/// a vývojářův <c>local.json</c> zůstane netknutý.
/// </summary>
internal sealed class TransientLocalPrefs : ILocalPrefs
{
    public Dictionary<string, string>? LastIdfaByServer { get; set; }
    [Obsolete(ServerPrefs.LegacyGlobalObsolete)] public string? LastIdfa { get; init; }
    public Dictionary<string, string>? LoginNames { get; set; }
    public Dictionary<string, string>? RememberedPasswords { get; set; }
    [Obsolete(ServerPrefs.LegacyLoginObsolete)] public string? LastLoginName { get; init; }
    [Obsolete(ServerPrefs.LegacyLoginObsolete)] public string? RememberedPassword { get; init; }
    public Dictionary<string, string>? MiniAppLayouts { get; set; }
    public Dictionary<string, double>? TreeWidths { get; set; }
    public Dictionary<string, double>? SupportStripHeights { get; set; }
    public List<SavedConnection>? Connections { get; set; }
    public Dictionary<string, WindowBoundsPrefs>? ShellWindowBoundsByServer { get; set; }
    [Obsolete(ServerPrefs.LegacyGlobalObsolete)] public WindowBoundsPrefs? ShellWindowBounds { get; init; }
    public Dictionary<string, string>? ReportedUpdaterFailures { get; set; }

    public void Save() { }
}
