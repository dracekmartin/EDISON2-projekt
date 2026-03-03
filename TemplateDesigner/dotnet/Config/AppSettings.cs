using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Konfigurace aplikace načítaná z appsettings.json.
/// Soubor leží vedle spustitelného souboru (AppContext.BaseDirectory).
/// </summary>
public class AppSettings
{
    public CacheConnectionSettings Cache     { get; set; } = new();
    public List<SchemaAgendaSettings> Agendas { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    /// <summary>
    /// Načte appsettings.json ze složky vedle exe.
    /// Pokud soubor neexistuje nebo je poškozený, vrátí výchozí instanci (bez Caché).
    /// </summary>
    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}

/// <summary>Nastavení připojení k Caché / IRIS serveru přes MLRest.</summary>
public class CacheConnectionSettings
{
    /// <summary>IP adresa nebo hostname serveru.</summary>
    public string Server    { get; set; } = "127.0.0.1";

    /// <summary>Port MLRest serveru (výchozí 57772).</summary>
    public int    Port      { get; set; } = 57772;

    /// <summary>Namespace Caché — třetí parametr konstruktoru MLRest.</summary>
    public string Namespace { get; set; } = "ALWA";

    /// <summary>IDFA identifikátor — čtvrtý parametr konstruktoru MLRest.</summary>
    public string Idfa      { get; set; } = "";

    /// <summary>Přihlašovací jméno (MLRest.LoginName).</summary>
    public string LoginName { get; set; } = "";

    /// <summary>Celé jméno (MLRest.FullName).</summary>
    public string FullName  { get; set; } = "";

    /// <summary>MLP token pro autentizaci (MLRest.Mlp).</summary>
    public string Mlp       { get; set; } = "";

    /// <summary>True pokud je připojení nakonfigurováno.</summary>
    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && Port > 0;
}

/// <summary>Definice jedné agendy načítané z Caché.</summary>
public class SchemaAgendaSettings
{
    /// <summary>Kód agendy — musí odpovídat Template.AgendaCode, např. "JR".</summary>
    public string Code        { get; set; } = "";

    /// <summary>Čitelný název agendy pro UI.</summary>
    public string Name        { get; set; } = "";

    /// <summary>Caché třída obsahující metodu pro načtení polí, např. "ALWA.MHD2.Tisk.Designer".</summary>
    public string CacheClass  { get; set; } = "";

    /// <summary>Název Caché metody vracející seznam polí, např. "GetDatabazovePolozky".</summary>
    public string CacheMethod { get; set; } = "";
}
