using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Výchozí rozestup odjezdů je Rovnoměrný (1.11.0 → 1.12.0).
/// </summary>
/// <remarks>
/// Podstata brány: <b>hotové vývěsce se nesmí přesázet mřížka minut</b>. Vlastnost vznikla až
/// 12. 8. 2026, takže starší šablony ji v JSON nemají - a nový výchozí stav by jim mlčky změnil
/// rozestupy na všech řádcích. Migrace jim proto dopíše to, podle čeho se doteď tiskly.
/// Druhá polovina je idempotence: šablona, která si Rovnoměrný vědomě zvolila, si ho musí udržet.
/// </remarks>
public class MigrationRozestupOdjezduTests
{
    private static JsonNode Migruj(string element) =>
        new MigrationRozestupOdjezdu().Migrate(JsonNode.Parse($$"""
        { "version": "1.11.0", "sections": [ { "elements": [ {{element}} ] } ] }
        """)!);

    private static string? Rozestup(JsonNode root) =>
        root["sections"]![0]!["elements"]![0]!["minuteSpacing"]?.GetValue<string>();

    [Fact]
    public void StaraVyveska_DostaneZapsanePodleObsahu()
    {
        var root = Migruj("""{ "type": "DepartureGridElement" }""");

        Assert.Equal("ByContent", Rozestup(root));
    }

    /// <summary>Vlastní volba se nepřepisuje, ani když je to zrovna nový výchozí stav - jinak by
    /// migrace přepsala rozhodnutí uživatele.</summary>
    [Theory]
    [InlineData("Even")]
    [InlineData("ByContent")]
    public void VlastniVolba_ZustaneBezeZmeny(string volba)
    {
        var root = Migruj($$"""{ "type": "DepartureGridElement", "minuteSpacing": "{{volba}}" }""");

        Assert.Equal(volba, Rozestup(root));
    }

    /// <summary>Druhý průchod už nic nemění - migrace musí jít pustit opakovaně.</summary>
    [Fact]
    public void Idempotence()
    {
        var prvni = Migruj("""{ "type": "DepartureGridElement" }""");
        var druhy = new MigrationRozestupOdjezdu().Migrate(prvni);

        Assert.Equal("ByContent", Rozestup(druhy));
    }

    /// <summary>Jiných prvků se to netýká - rozestup odjezdů má jen mřížka minut.</summary>
    [Fact]
    public void JinyPrvek_ZustaneBezeZmeny()
    {
        var root = Migruj("""{ "type": "StopListElement" }""");

        Assert.Null(Rozestup(root));
    }

    /// <summary>Poškozená šablona migraci nesmí shodit - bez sekcí i bez prvků projde beze změny.</summary>
    [Theory]
    [InlineData("""{ "version": "1.11.0" }""")]
    [InlineData("""{ "version": "1.11.0", "sections": [ {} ] }""")]
    [InlineData("""{ "version": "1.11.0", "sections": [ { "elements": [ null ] } ] }""")]
    public void PoskozenaSablona_Projde(string json)
    {
        var root = JsonNode.Parse(json)!;

        var vysledek = new MigrationRozestupOdjezdu().Migrate(root);

        Assert.NotNull(vysledek);
    }
}
