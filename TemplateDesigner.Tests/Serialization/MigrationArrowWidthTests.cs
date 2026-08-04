using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.5.0 → 1.6.0 (<see cref="MigrationArrowWidth"/>): přepínač „tučná šipka"
/// (<c>directionArrowBold</c>) → tloušťka v mm (<c>directionArrowWidth</c>). Uložené šablony musí
/// vypadat stejně jako dřív, jinak by se zákazníkovi po aktualizaci změnila hotová vývěska.
/// </summary>
public class MigrationArrowWidthTests
{
    private static JsonObject Migrate(JsonNode root) =>
        (JsonObject)new MigrationArrowWidth().Migrate(root);

    private static JsonObject FirstElement(JsonObject root) =>
        (JsonObject)root["sections"]![0]!["elements"]![0]!;

    private static JsonNode Root(string elementJson) => JsonNode.Parse($$"""
        { "sections": [ { "elements": [ {{elementJson}} ] } ] }
        """)!;

    [Fact]
    public void TucnaSipka_SeZmeniNaSilnejsiCaru()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "StopListElement", "directionArrowBold": true }""")));

        Assert.Equal(0.4, el["directionArrowWidth"]!.GetValue<double>(), 3);
        Assert.False(el.ContainsKey("directionArrowBold"));
    }

    [Fact]
    public void NetucnaSipka_DostaneVychoziTloustku()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "StopListElement", "directionArrowBold": false }""")));

        Assert.Equal(0.2, el["directionArrowWidth"]!.GetValue<double>(), 3);
        Assert.False(el.ContainsKey("directionArrowBold"));
    }

    [Fact]
    public void ExplicitniTloustkaMaPrednost()
    {
        var el = FirstElement(Migrate(Root(
            """{ "type": "StopListElement", "directionArrowBold": true, "directionArrowWidth": 1.0 }""")));

        Assert.Equal(1.0, el["directionArrowWidth"]!.GetValue<double>(), 3);
        Assert.False(el.ContainsKey("directionArrowBold"));
    }

    [Fact]
    public void BezStarehoKlice_BezeZmeny()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "StopListElement" }""")));

        Assert.False(el.ContainsKey("directionArrowWidth"));   // default drží model, ne migrace
    }

    [Fact]
    public void OpakovanaMigrace_DavaStejnyVysledek()
    {
        var once = Migrate(Root("""{ "type": "StopListElement", "directionArrowBold": true }"""));
        var twice = Migrate(once);

        Assert.Equal(0.4, FirstElement(twice)["directionArrowWidth"]!.GetValue<double>(), 3);
    }

    [Fact]
    public void SablonaBezSekci_Nespadne()
    {
        var root = JsonNode.Parse("""{ "version": "1.5.0" }""")!;

        Assert.NotNull(new MigrationArrowWidth().Migrate(root));
    }
}
