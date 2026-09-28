namespace EDISONMAIN2;

/// <summary>
/// Preference E2E běhu jen v paměti: start je deterministický (výchozí firma a rozložení)
/// a vývojářův <c>local.json</c> zůstane netknutý.
/// </summary>
internal sealed class TransientLocalPrefs : ILocalPrefs
{
    public string? LastIdfa { get; set; }
    public string? LastLoginName { get; set; }
    public string? RememberedPassword { get; set; }
    public Dictionary<string, string>? MiniAppLayouts { get; set; }
    public Dictionary<string, double>? TreeWidths { get; set; }
    public Dictionary<string, double>? SupportStripHeights { get; set; }
    public List<SavedConnection>? Connections { get; set; }
    public WindowBoundsPrefs? ShellWindowBounds { get; set; }

    public void Save() { }
}
