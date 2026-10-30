using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Parametr, jehož hodnotu zpracovává až server v <c>GetData</c>, musí mít v <c>GetParameters</c>
/// příznak <c>serverSide</c> - podle něj klient pozná, že jeho změna vyžaduje načtení dat. Bez
/// příznaku klik v UI nedělá nic a tisk vyjede jinak než náhled (tiskový dialog si data tahá čerstvě).
///
/// Past se vrátila **čtyřikrát** (`ROZDELIT_PER_OZNACNIK`, pak `SOUHRN_NA_ZNAMENI` +
/// `SPOJE_U_DOPRAVCE`, a `DELENI_SLOUPCU` našel až tenhle test), proto strojová kontrola: test
/// vyparsuje z agendových `.cls` čtení <c>^||ML("IN","KLIC")</c> i příznaky u definic parametrů,
/// odečte infrastrukturní klíče a porovná obojí. Offline, bez Caché serveru.
///
/// Do 21. 9. držel seznam takových parametrů klient natvrdo a test hlídal ten seznam; příznak
/// u definice se zapomene hůř, protože se píše na stejném místě jako parametr sám.
/// </summary>
public class ServerSideParamsDriftTests
{
    /// <summary>
    /// Klíče, které nejsou parametry sestavy, ale technický vstup dispatchu - do allowlistu nepatří
    /// (jejich změna se řeší jinou cestou než re-fetchem parametru).
    /// </summary>
    private static readonly HashSet<string> InfrastructureKeys = new(StringComparer.Ordinal)
    {
        "LINKA_ID",           // klíč instance (posílá Instance Picker, ne parametr)
        "INSTANCE_ID",        // alias téhož
        "SKUPINA_LINEK_ID",   // skupina linek - session stav pickeru
        "search",             // GetAvailableInstances: filtr pickeru
        "limit",              // GetAvailableInstances: strop počtu
        "DEFINICE_SLOUPCU",   // vlastní sloupce vývěsky - obsah šablony, ne parametr sestavy;
                              // posílá se vždy čerstvě ze šablony (ComposeGetDataParams) a re-fetch
                              // při změně řeší vlastní snapshot (RefetchIfColumnsDefinitionChanged)
        "JAZYK",              // jazyk textů značek = kultura šablony, ne volba v parametrech sestavy;
                              // re-fetch při přepnutí kultury drží tentýž snapshot (CurrentWireSnapshot)
        "TURNUSY_KE_SPOJUM",  // volba prvku (turnus je provozní věc dopravce, ne informace pro
                              // cestujícího) - posílá se ze šablony jako DEFINICE_SLOUPCU a re-fetch
                              // při přepnutí drží týž snapshot (CurrentWireSnapshot)
        "BARVY_SPOJU",        // třístav, který posílá starší návrhář místo checkboxu
                              // TISKNOUT_BARVY_SPOJU (BaseAgenda.VolbaBarevSpoju) - jen zpětná
                              // kompatibilita, nový návrhář ho nezná
    };

    // Čtení pojmenovaného IN klíče. Skupina 2 zachytí znak za uvozovkou - když je to `_`, jde
    // o konkatenaci (dynamická rodina klíčů, viz DynamicKeysAreSkipped), ne o pevný název.
    private static readonly Regex InKeyRx = new(
        @"\^\|\|ML\(""IN"",""([A-Za-z_0-9]+)""(.?)",
        RegexOptions.Compiled);

    // `s pRozd.name = "ROZDELENI_SLOUPCU"` → proměnná ↔ název parametru.
    private static readonly Regex ParamNameRx = new(
        @"^\s*s\s+(\w+)\.name\s*=\s*""([A-Za-z_0-9]+)""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // `s pRozd.serverSide = 1`
    private static readonly Regex ParamFlagRx = new(
        @"^\s*s\s+(\w+)\.serverSide\s*=\s*1\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // `d ..AddBoolParam(arr, "KLIC", …, 1)` (i `AddNumberParam`) - příznak je poslední argument na řádku.
    private static readonly Regex BoolParamFlagRx = new(
        @"Add(?:Bool|Number)Param\(arr,\s*""([A-Za-z_0-9]+)"".*,\s*1\)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void KazdyParametrCtenyVAgende_MaPriznakServerSide()
    {
        // Příznak stačí kdekoli v rodině agend: MHD čte klíče, jejichž definici drží AD varianta
        // (týž parametr, jiný zdroj dat).
        var flagged = AgendaFiles().SelectMany(ServerSideParams).ToHashSet(StringComparer.Ordinal);
        var found = new List<string>();

        foreach (var file in AgendaFiles())
        {
            foreach (var key in StaticInKeys(file))
            {
                if (InfrastructureKeys.Contains(key) || flagged.Contains(key)) continue;
                found.Add($"{Path.GetFileName(file)}: \"{key}\"");
            }
        }

        Assert.True(found.Count == 0,
            "Agenda čte parametr, který nemá v GetParameters příznak serverSide → jeho přepnutí "
            + "v UI nespustí načtení dat (klik je no-op a tisk vyjede jinak než náhled):\n"
            + string.Join("\n", found)
            + "\nDoplň příznak k definici parametru, nebo - jde-li o technický vstup dispatchu - "
            + "klíč do InfrastructureKeys v tomhle testu.");
    }

    [Fact]
    public void PriznakNemaParametr_KteryNikdoNecte()
    {
        // Druhý směr: příznak u parametru, jehož hodnotu žádná agenda nečte, znamená zbytečné
        // načítání dat při každé jeho změně (nejspíš přežil odstranění kódu, který ho četl).
        var read = AgendaFiles().SelectMany(StaticInKeys).ToHashSet(StringComparer.Ordinal);
        var dead = AgendaFiles()
            .SelectMany(f => ServerSideParams(f).Select(p => (Soubor: Path.GetFileName(f), Klic: p)))
            .Where(x => !read.Contains(x.Klic))
            .Select(x => $"{x.Soubor}: \"{x.Klic}\"")
            .ToList();

        Assert.True(dead.Count == 0,
            "Parametr má příznak serverSide, ale žádná agenda jeho hodnotu nečte - zbytečné "
            + $"načítání dat při každé změně:\n{string.Join("\n", dead)}");
    }

    [Fact]
    public void ParserNasel_PriznakyAgendy()
    {
        // Pojistka druhého parseru: kdyby zrezivěl, testy výše by prošly na prázdném vstupu.
        var flagged = ServerSideParams(AgendaFile("ZastavkoveJrAd.cls"));

        Assert.Contains("ROZDELENI_SLOUPCU", flagged);   // vlastní objekt
        Assert.Contains("SOUHRN_NA_ZNAMENI", flagged);   // přes AddBoolParam
        Assert.Contains("MAX_SLOUPCU_VARIANT", flagged); // přes AddNumberParam
        Assert.DoesNotContain("CASOVA_OSA", flagged);    // klientský parametr
    }

    [Fact]
    public void ParserNasel_ZnameKliceAgendy()
    {
        // Pojistka regexu: kdyby zrezivěl, oba testy výše by prošly na prázdném vstupu.
        var keys = StaticInKeys(AgendaFile("ZastavkoveJrAd.cls"));

        Assert.Contains("PRVNI_BEZ_PISMENE", keys);
        Assert.Contains("LINKA_ID", keys);
        Assert.True(keys.Count >= 10, $"Naparsováno jen {keys.Count} klíčů - regex nesedí na .cls?");
    }

    [Fact]
    public void DynamickeKlice_SePreskoci()
    {
        // Klíč skládaný za běhu (`"prefix"_promenna`) není název parametru - parser ho musí vynechat,
        // jinak by hlásil drift na něčem, co v allowlistu být nemá. Testuje se na vlastním vstupu,
        // ne na .cls z repa: v agendách dnes žádný takový klíč není (poslední byl legacy
        // `par_2_"_AtributID` v `ZastavkoveJrMhd`, mrtvá větev smazaná 4. 8. 2026), takže test nad
        // reálným souborem by prošel naprázdno a nic nehlídal.
        var keys = StaticInKeysFromText(
            """
            s hodnota = $g(^||ML("IN","PEVNY_KLIC"))
            i $d(^||ML("IN","dynamicky_"_AtributID)) d Neco()
            """);

        Assert.Contains("PEVNY_KLIC", keys);
        Assert.DoesNotContain("dynamicky_", keys);
    }

    // ── Parsování ─────────────────────────────────────────────────────────────

    /// <summary>Pevně pojmenované IN klíče souboru (bez klíčů skládaných konkatenací).</summary>
    private static List<string> StaticInKeys(string file) => StaticInKeysFromText(File.ReadAllText(file));

    /// <summary>
    /// Parametry, které si agenda označila příznakem <c>serverSide</c>. Dvě podoby zápisu:
    /// vlastní objekt (<c>s pX.name = "KLIC"</c> + <c>s pX.serverSide = 1</c>) a pomocná metoda
    /// <c>AddBoolParam(..., 1)</c> / <c>AddNumberParam(..., 1)</c>, kde je příznak poslední argument.
    /// </summary>
    private static List<string> ServerSideParams(string file)
    {
        var text = File.ReadAllText(file);

        var nazvyPromennych = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in ParamNameRx.Matches(text))
            nazvyPromennych[m.Groups[1].Value] = m.Groups[2].Value;

        var result = new List<string>();
        foreach (Match m in ParamFlagRx.Matches(text))
            if (nazvyPromennych.TryGetValue(m.Groups[1].Value, out var name))
                result.Add(name);

        foreach (Match m in BoolParamFlagRx.Matches(text))
            result.Add(m.Groups[1].Value);

        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> StaticInKeysFromText(string text) =>
        InKeyRx.Matches(text)
            .Where(m => m.Groups[2].Value != "_")   // "prefix"_var = dynamický klíč
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string[] AgendaFiles() => ZdrojeAgend.KodAgend();

    private static string AgendaFile(string name) => Path.Combine(ZdrojeAgend.Slozka, name);
}
