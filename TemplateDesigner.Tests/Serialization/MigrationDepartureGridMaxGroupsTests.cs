using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.3.0 → 1.4.0 (<see cref="MigrationDepartureGridMaxGroups"/>): starý implicitní
/// default maxGroupCount=3 → 0 (bez limitu); vědomé hodnoty (1, 2, 4…) a jiné elementy beze změny.
/// </summary>
public class MigrationDepartureGridMaxGroupsTests
{
    private static JsonObject Migrate(JsonNode root) =>
        (JsonObject)new MigrationDepartureGridMaxGroups().Migrate(root);

    private static JsonObject FirstElement(JsonObject root) =>
        (JsonObject)root["sections"]![0]!["elements"]![0]!;

    private static JsonNode Root(string elementJson) => JsonNode.Parse($$"""
        { "sections": [ { "elements": [ {{elementJson}} ] } ] }
        """)!;

    [Fact]
    public void StaryDefault3_SeZmeniNa0()
    {
        var root = Root("""{ "type": "DepartureGridElement", "maxGroupCount": 3 }""");
        Assert.Equal(0, FirstElement(Migrate(root))["maxGroupCount"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void VedomaHodnota_Zustava(int hodnota)
    {
        var root = Root($$"""{ "type": "DepartureGridElement", "maxGroupCount": {{hodnota}} }""");
        Assert.Equal(hodnota, FirstElement(Migrate(root))["maxGroupCount"]!.GetValue<int>());
    }

    [Fact]
    public void ChybejiciProperty_SeNepridava()
    {
        var root = Root("""{ "type": "DepartureGridElement" }""");
        Assert.False(FirstElement(Migrate(root)).ContainsKey("maxGroupCount"));
    }

    [Fact]
    public void JinyElement_SeNetkne()
    {
        var root = Root("""{ "type": "TextElement", "maxGroupCount": 3 }""");
        Assert.Equal(3, FirstElement(Migrate(root))["maxGroupCount"]!.GetValue<int>());
    }
}
