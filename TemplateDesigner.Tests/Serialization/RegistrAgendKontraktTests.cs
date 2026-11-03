using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Co návrhář o agendě ví, ohlásí agenda parametrem třídy (<c>BaseAgenda.cls</c>), registr ho pošle
/// (<c>AgendyRegistr.ListAgendy</c>) a klient ho přečte do <see cref="AgendaRegistryEntry"/> (`B194g` R4). Tři místa
/// se doplňují ručně; do 30. 10. je nic neporovnávalo a zapomenutý krok znamenal, že parametr
/// agendy neměl žádný účinek, nebo že klient dostal výchozí hodnotu pro starší server.
/// </summary>
public class RegistrAgendKontraktTests
{
    private static readonly Regex ParametrTridy = new(@"^Parameter (\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static string ListAgendy()
    {
        var zdroj = File.ReadAllText(RepoCesty.Cache("API", "AgendyRegistr.cls"));
        return Regex.Match(zdroj, @"ClassMethod ListAgendy\(.*?\n\}", RegexOptions.Singleline).Value;
    }

    /// <summary>Parametry agendy, které čte jen server; návrháři je registr neposílá.</summary>
    private static readonly Dictionary<string, string> JenProServer = new()
    {
        ["ParametryJenVeFirme"] = "čte ho jen odeslání šablony jiné firmě (API.SdileniSablon), návrhář s ním nepracuje",
    };

    [Fact]
    public void RegistrPosilaKazdyParametrAgendy()
    {
        var parametry = ParametrTridy.Matches(File.ReadAllText(RepoCesty.Cache("Agendy", "BaseAgenda.cls")))
            .Select(m => m.Groups[1].Value).Where(p => !JenProServer.ContainsKey(p)).ToList();
        var cteRegistr = Regex.Matches(ListAgendy(), @"\$parameter\(className, ""(\w+)""\)").Select(m => m.Groups[1].Value).ToHashSet();

        Assert.True(parametry.Count >= 10, $"v BaseAgenda se našlo jen {parametry.Count} parametrů - hledání je rozbité");
        var neposila = parametry.Where(p => !cteRegistr.Contains(p)).ToList();
        Assert.True(neposila.Count == 0, "Parametr agendy, který registr návrháři neposílá: " + string.Join(", ", neposila));
    }

    [Fact]
    public void KlientCteKazdyKlicRegistru()
    {
        var klice = Regex.Matches(ListAgendy(), @"\bentry\.(?:%Set\(""(\w+)""|(\w+)\s*=)")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Klient čte JSON do parametrů konstruktoru (bez ohledu na velikost písmen, CacheAgendaRegistry).
        var vlastnosti = typeof(AgendaRegistryEntry).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single().GetParameters().Select(p => p.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(klice.Count >= 10, $"v ListAgendy se našlo jen {klice.Count} klíčů - hledání je rozbité");
        Assert.Equal(klice.Order(StringComparer.OrdinalIgnoreCase), vlastnosti.Order(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }
}
