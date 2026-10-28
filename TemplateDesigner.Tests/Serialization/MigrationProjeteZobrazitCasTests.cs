using System.Text.Json.Nodes;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Čas u projetých zastávek je od 28. 10. zaškrtávátko „Projeté: Zobrazit čas" místo výběru
/// Nezobrazovat / Zobrazit - popisek i ovládání odpovídají typu pole (rozhodnutí Jan).
/// </summary>
public class MigrationProjeteZobrazitCasTests
{
    private static JsonObject Migruj(string prvekJson)
    {
        var root = JsonNode.Parse($$"""
        {
          "version": "1.16.0",
          "sections": [ { "elements": [ {{prvekJson}} ] } ]
        }
        """)!;
        return (JsonObject)new MigrationProjeteZobrazitCas().Migrate(root)["sections"]![0]!["elements"]![0]!;
    }

    [Fact]
    public void Zobrazit_DaZapnuto()
    {
        var prvek = Migruj("""{ "type": "StopListElement", "passedStopsTime": "Show" }""");

        Assert.False(prvek.ContainsKey("passedStopsTime"));
        Assert.True(prvek["passedStopsShowTime"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("\"Hide\"")]
    [InlineData("0")]
    public void Nezobrazovat_ZustaneVypnuto(string hodnota)
    {
        var prvek = Migruj($$"""{ "type": "StopListElement", "passedStopsTime": {{hodnota}} }""");

        Assert.False(prvek.ContainsKey("passedStopsTime"));
        Assert.False(prvek.ContainsKey("passedStopsShowTime"));
    }

    [Fact]
    public void CiselnaHodnota_JednaJeZobrazit()
    {
        var prvek = Migruj("""{ "type": "StopListElement", "passedStopsTime": 1 }""");

        Assert.True(prvek["passedStopsShowTime"]!.GetValue<bool>());
    }

    [Fact]
    public void JinyPrvek_SeNemeni()
    {
        var prvek = Migruj("""{ "type": "LineTableElement", "passedStopsTime": "Show" }""");

        Assert.Equal("Show", prvek["passedStopsTime"]!.GetValue<string>());
    }

    [Fact]
    public void Idempotence()
    {
        var root = JsonNode.Parse("""
        { "version": "1.16.0", "sections": [ { "elements": [ { "type": "StopListElement", "passedStopsTime": "Show" } ] } ] }
        """)!;
        var migrace = new MigrationProjeteZobrazitCas();
        var jednou = migrace.Migrate(root.DeepClone()).ToJsonString();

        Assert.Equal(jednou, migrace.Migrate(JsonNode.Parse(jednou)!).ToJsonString());
    }

    [Fact]
    public void PoskozenaSablona_Nespadne()
    {
        var migrace = new MigrationProjeteZobrazitCas();
        migrace.Migrate(JsonNode.Parse("""{ "version": "1.16.0" }""")!);
        migrace.Migrate(JsonNode.Parse("""{ "sections": [ null, { "elements": [ 1, { "type": "StopListElement", "passedStopsTime": [ 1 ] } ] } ] }""")!);
    }

    /// <summary>Celou cestou načtení: šablona se zobrazeným časem u projetých ho zobrazuje dál.</summary>
    [Fact]
    public void StaraSablona_SeNacteSeStejnymNastavenim()
    {
        var t = TemplateSerializer.Deserialize("""
        { "version": "1.16.0", "name": "x", "sections": [ { "name": "s", "elements": [
          { "type": "StopListElement", "passedStopsTime": "Show" },
          { "type": "StopListElement", "passedStopsTime": "Hide" } ] } ] }
        """);

        Assert.True(((StopListElement)t.Sections[0].Elements[0]).PassedStopsShowTime);
        Assert.False(((StopListElement)t.Sections[0].Elements[1]).PassedStopsShowTime);
    }
}
