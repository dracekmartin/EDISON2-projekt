namespace TemplateDesigner.Config;

/// <summary>
/// Obsah pole <c>inputParam</c> launch kontraktu EDISONMAIN2: <c>Agenda=&lt;kód&gt;</c> a za ním volitelně
/// další <c>KLÍČ=hodnota</c> oddělené středníkem (jména jako na wire agendy), např. z porovnání linek
/// v modulu JŘ: <c>Agenda=LINKOVY_JR_AD_ROZDILOVY;SKUPINA_LINEK_ID=307;LINKA_ID=0380760;POROVNAT_SKUPINU=207;POROVNAT_LINKU=0380760</c>.
/// </summary>
/// <remarks>
/// Producent je v repu EDISON (<c>ISYJR2/TiskJRHelper.cs</c>), proto tvar jen přidáváme.
/// ⚠️ Návrhář starší než tenhle formát přečte celý řetězec jako kód agendy, nepozná ho a otevře
/// poslední agendu - nový vstup smí modul JŘ posílat až s ním.
/// </remarks>
public sealed record LaunchInput(string? AgendaCode, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Klíč skupiny linek - stejné jméno jako v IN <c>GetData</c> a výběru instancí.</summary>
    public const string SkupinaKey = "SKUPINA_LINEK_ID";

    public static readonly LaunchInput Empty = new(null, new Dictionary<string, string>());

    /// <summary>Hodnota klíče, null když ve vstupu není (prázdná hodnota je hodnota).</summary>
    public string? Get(string key) => Values.TryGetValue(key, out var v) ? v : null;

    public static LaunchInput Parse(string? inputParam)
    {
        if (string.IsNullOrWhiteSpace(inputParam)) return Empty;

        string? agenda = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in inputParam.Split(';'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Length == 0) continue;
            if (key.Equals("Agenda", StringComparison.OrdinalIgnoreCase))
                agenda = value.Length > 0 ? value : null;
            else
                values[key] = value;
        }
        // Bez agendy nemají ostatní hodnoty k čemu patřit (dřívější chování: vstup bez Agenda= se ignoroval).
        return agenda is null ? Empty : new LaunchInput(agenda, values);
    }
}
