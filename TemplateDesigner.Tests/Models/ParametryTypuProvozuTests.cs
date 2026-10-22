using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Typy provozu linkového JŘ MHD: firma jich má desítky (KV_DP 20), linka pár (KV_DP linka 1 sedm).
/// Panel parametrů ukáže jen typy, které načtená linka má - jinak by uživatel nevěděl, co odklikat.
/// Bez dat, nebo když je server nevyjmenuje, se nic neschovává (rozhodnutí Jan 22. 10.).
/// </summary>
public class ParametryTypuProvozuTests
{
    private static TemplateParameter TypProvozu(string name) => new()
    {
        Name = name, Type = "bool", DefaultValue = "true", VisibleWhenListedIn = "TYPY_PROVOZU_LINKY",
    };

    private static readonly Dictionary<string, object?> BezHodnot = [];

    /// <summary>Stránky dat tak, jak je klient dostane ze serveru (hodnota je JSON pole).</summary>
    private static List<DataContext> Stranky(params string[] valuesJson) =>
        CacheAgendaProvider.ParsePages(valuesJson.Select((v, i) => ((i + 1).ToString(), (string?)$$"""{ "values": {{v}} }""")))!;

    [Fact]
    public void TypLinkySeUkaze_OstatniSeSchovaji()
    {
        var data = Stranky("""{ "SMER": "T", "TYPY_PROVOZU_LINKY": ["TYP_PROVOZU_1001", "TYP_PROVOZU_1004"] }""");

        Assert.True(TypProvozu("TYP_PROVOZU_1004").IsVisible(BezHodnot, null, data));
        Assert.False(TypProvozu("TYP_PROVOZU_1011").IsVisible(BezHodnot, null, data));
    }

    [Fact]
    public void BezNactenychDat_SeNicNeschova()
    {
        Assert.True(TypProvozu("TYP_PROVOZU_1011").IsVisible(BezHodnot, null, null));
        Assert.True(TypProvozu("TYP_PROVOZU_1011").IsVisible(BezHodnot, null, []));
    }

    /// <summary>Starší server seznam neposílá - parametr musí zůstat vidět, jinak by nešel nastavit.</summary>
    [Fact]
    public void DataBezSeznamu_SeNicNeschova()
    {
        var data = Stranky("""{ "SMER": "T" }""");

        Assert.True(TypProvozu("TYP_PROVOZU_1011").IsVisible(BezHodnot, null, data));
    }

    /// <summary>Stačí, když typ vyjmenuje kterákoli stránka (víc směrů, víc linek v náhledu).</summary>
    [Fact]
    public void StaciJednaStranka()
    {
        var data = Stranky(
            """{ "SMER": "T", "TYPY_PROVOZU_LINKY": ["TYP_PROVOZU_1001"] }""",
            """{ "SMER": "Z", "TYPY_PROVOZU_LINKY": ["TYP_PROVOZU_1004"] }""");

        Assert.True(TypProvozu("TYP_PROVOZU_1004").IsVisible(BezHodnot, null, data));
    }

    /// <summary>Parametr bez klíče se podle dat neschovává, ani když seznam v datech je.</summary>
    [Fact]
    public void ParametrBezKlice_SeNeschova()
    {
        var data = Stranky("""{ "TYPY_PROVOZU_LINKY": ["TYP_PROVOZU_1001"] }""");
        var smer = new TemplateParameter { Name = "SMER_TISKU", Type = "enum" };

        Assert.True(smer.IsVisible(BezHodnot, null, data));
    }

    [Fact]
    public void KlicSeNacteZDefiniceParametru()
    {
        var parameters = CacheAgendaProvider.ParseParameters("""
            [ { "name": "TYP_PROVOZU_1001", "type": "bool", "visibleWhenListedIn": "TYPY_PROVOZU_LINKY" },
              { "name": "SMER_TISKU", "type": "enum" } ]
            """);

        Assert.Equal("TYPY_PROVOZU_LINKY", parameters[0].VisibleWhenListedIn);
        Assert.Null(parameters[1].VisibleWhenListedIn);
    }
}
