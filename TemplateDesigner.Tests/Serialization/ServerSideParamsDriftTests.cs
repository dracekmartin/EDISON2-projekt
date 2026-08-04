using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka allowlistu <c>ServerSideParams</c>. Parametr, jehož hodnotu zpracovává až
/// server v <c>GetData</c>, musí být v allowlistu - jinak jeho přepnutí v UI **nespustí re-fetch**,
/// takže klik nedělá nic a tisk vyjede jinak než náhled (tiskový dialog si data tahá čerstvě).
///
/// Past se vrátila **čtyřikrát** (`ROZDELIT_PER_OZNACNIK`, pak `SOUHRN_NA_ZNAMENI` +
/// `SPOJE_U_DOPRAVCE`, a `DELENI_SLOUPCU` našel až tenhle test), proto strojová kontrola: test
/// vyparsuje z agendových `.cls` čtení <c>^||ML("IN","KLIC")</c>, odečte infrastrukturní klíče
/// a ověří, že zbytek je podmnožinou allowlistu. Offline, bez Caché serveru.
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
        "key",                // GetTableTemplate: klíč šablony tabulky
        "search",             // GetAvailableInstances: filtr pickeru
        "limit",              // GetAvailableInstances: strop počtu
    };

    // Čtení pojmenovaného IN klíče. Skupina 2 zachytí znak za uvozovkou - když je to `_`, jde
    // o konkatenaci (dynamická rodina klíčů, viz DynamicKeysAreSkipped), ne o pevný název.
    private static readonly Regex InKeyRx = new(
        @"\^\|\|ML\(""IN"",""([A-Za-z_0-9]+)""(.?)",
        RegexOptions.Compiled);

    [Fact]
    public void KazdyParametrCtenyVAgende_JeVAllowlistu()
    {
        var allowed = TemplateDesignerViewModel.ServerSideParams.ToHashSet(StringComparer.Ordinal);
        var found = new List<string>();

        foreach (var file in AgendaFiles())
        {
            foreach (var key in StaticInKeys(file))
            {
                if (InfrastructureKeys.Contains(key) || allowed.Contains(key)) continue;
                found.Add($"{Path.GetFileName(file)}: \"{key}\"");
            }
        }

        Assert.True(found.Count == 0,
            "Agenda čte parametr, který není v ServerSideParams → jeho přepnutí v UI nespustí "
            + "re-fetch dat (klik je no-op a tisk vyjede jinak než náhled):\n"
            + string.Join("\n", found)
            + "\nDoplň klíč do ServerSideParams (TemplateDesignerViewModel), nebo - jde-li "
            + "o technický vstup dispatchu - do InfrastructureKeys v tomhle testu.");
    }

    [Fact]
    public void AllowlistNeobsahujeMrtveKlice()
    {
        // Druhý směr: klíč v allowlistu, který už žádná agenda nečte, znamená zbytečný re-fetch
        // při každé jeho změně (a nejspíš přežil odstranění parametru).
        var read = AgendaFiles().SelectMany(StaticInKeys).ToHashSet(StringComparer.Ordinal);
        var dead = TemplateDesignerViewModel.ServerSideParams.Where(p => !read.Contains(p)).ToList();

        Assert.True(dead.Count == 0,
            "ServerSideParams obsahuje klíče, které žádná agenda nečte - zbytečný re-fetch dat "
            + $"při jejich změně:\n{string.Join(", ", dead)}");
    }

    [Fact]
    public void ParserNasel_ZnameKliceAgendy()
    {
        // Pojistka regexu: kdyby zrezivěl, oba testy výše by prošly na prázdném vstupu.
        var keys = StaticInKeys(AgendaFile("ZastavkoveJrAd.cls"));

        Assert.Contains("PISMENA_VARIANT", keys);
        Assert.Contains("LINKA_ID", keys);
        Assert.True(keys.Count >= 10, $"Naparsováno jen {keys.Count} klíčů - regex nesedí na .cls?");
    }

    [Fact]
    public void DynamickeKlice_SePreskoci()
    {
        // ZastavkoveJrMhd čte `^||ML("IN","par_2_"_AtributID)` - rodina klíčů skládaná za běhu,
        // ne pevný název parametru (legacy „netisknuté atributy"; klíč dnes nikdo neposílá).
        // Kdyby parser bral `par_2_` jako parametr, test by hlásil neexistující drift.
        var keys = StaticInKeys(AgendaFile("ZastavkoveJrMhd.cls"));

        Assert.DoesNotContain("par_2_", keys);
    }

    // ── Parsování ─────────────────────────────────────────────────────────────

    /// <summary>Pevně pojmenované IN klíče souboru (bez klíčů skládaných konkatenací).</summary>
    private static List<string> StaticInKeys(string file)
    {
        string text = File.ReadAllText(file);
        return InKeyRx.Matches(text)
            .Where(m => m.Groups[2].Value != "_")   // "prefix"_var = dynamický klíč
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string[] AgendaFiles() => Directory.GetFiles(AgendyDir(), "*.cls");

    private static string AgendaFile(string name) => Path.Combine(AgendyDir(), name);

    private static string AgendyDir()
    {
        // Od bin/Debug/... nahoru k rootu repa (kde leží EDISON2.sln)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        // Běh z izolovaného výstupu (dotnet test -o mimo repo) → root odvodit z cesty tohoto zdrojáku
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;

        Assert.True(File.Exists(Path.Combine(dir!.FullName, "EDISON2.sln")),
            "Nenalezen root repa (EDISON2.sln) - drift test potřebuje zdrojáky .cls");
        return Path.Combine(dir.FullName,
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "Agendy");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
