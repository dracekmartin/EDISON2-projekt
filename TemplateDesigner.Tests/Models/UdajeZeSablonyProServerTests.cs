using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Údaje ze šablony pro server (<see cref="TemplateDataInputs"/>) jsou jeden typ, ze kterého se staví vstup
/// <c>GetData</c>, vstup ukázkových dat i výjimky testu parametrů. Do 30. 10. byly opsané na čtyřech místech
/// a ukázková data turnusy neznala.
/// </summary>
public class UdajeZeSablonyProServerTests
{
    private static readonly TemplateDataInputs Vse = new("""{"sloupce":[]}""", true, "sk");

    /// <summary>Klíč, který se posílá, ale není v seznamu, by se po přepnutí šablony neodebral z uložených parametrů.</summary>
    [Fact]
    public void PosilaPraveKliceZeSeznamu() =>
        Assert.Equal(TemplateDataInputs.Keys, Vse.Values().Select(v => v.Key));

    /// <summary>Ukázková data dostanou totéž co <c>GetData</c> - jinak náhled bez linky ukazuje jiné rozvržení.</summary>
    [Fact]
    public void UkazkovaDataDostanouTotezCoGetData()
    {
        var getData = TemplateDesignerViewModel.ComposeGetDataParams(
            new Dictionary<string, object?>(), "LINKA_ID", "42", null, Vse);
        var ukazka = CacheAgendaProvider.MockDataInput(Vse);

        Assert.Equal(
            TemplateDataInputs.Keys.ToDictionary(k => k, k => getData[k]?.ToString()),
            TemplateDataInputs.Keys.ToDictionary(k => k, k => ukazka[k]?.Value?.ToString()));
    }

    /// <summary>Každý údaj ze šablony mění klíč cache ukázkových dat, jinak se po změně vrátí starý vzorek.</summary>
    [Fact]
    public void KazdyUdajMeniKlicCache()
    {
        string klic = CacheAgendaProvider.MockCacheKey("LINKOVY_JR_AD", Vse);

        Assert.NotEqual(klic, CacheAgendaProvider.MockCacheKey("LINKOVY_JR_AD", Vse with { ColumnsDefinitionJson = null }));
        Assert.NotEqual(klic, CacheAgendaProvider.MockCacheKey("LINKOVY_JR_AD", Vse with { TurnusyKeSpojum = false }));
        Assert.NotEqual(klic, CacheAgendaProvider.MockCacheKey("LINKOVY_JR_AD", Vse with { Jazyk = "cs" }));
    }

    /// <summary>Kultura šablony bez kódu nebo s neznámým kódem znamená češtinu.</summary>
    [Theory]
    [InlineData(null, "cs")]
    [InlineData("sk-SK", "sk")]
    [InlineData("xx-NEEXISTUJE", "cs")]
    public void JazykZKulturySablony(string? kultura, string jazyk) =>
        Assert.Equal(jazyk, TemplateDataInputs.From(new Template { CultureCode = kultura }).Jazyk);
}
