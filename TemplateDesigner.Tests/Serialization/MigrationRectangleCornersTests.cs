using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.4.0 → 1.5.0 (<see cref="MigrationRectangleCorners"/>): staré
/// <c>cornerRadius</c> → base <c>cornerStyle=Rounded</c> + <c>cornerSize</c>; klíč se odstraní.
/// </summary>
public class MigrationRectangleCornersTests
{
    private static JsonObject Migrate(JsonNode root) =>
        (JsonObject)new MigrationRectangleCorners().Migrate(root);

    private static JsonObject FirstElement(JsonObject root) =>
        (JsonObject)root["sections"]![0]!["elements"]![0]!;

    private static JsonNode Root(string elementJson) => JsonNode.Parse($$"""
        { "sections": [ { "elements": [ {{elementJson}} ] } ] }
        """)!;

    [Fact]
    public void KladnyCornerRadius_SeZmeniNaRoundedPlusSize()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "RectangleElement", "cornerRadius": 3.5 }""")));
        Assert.Equal("Rounded", el["cornerStyle"]!.GetValue<string>());
        Assert.Equal(3.5, el["cornerSize"]!.GetValue<double>(), 3);
        Assert.False(el.ContainsKey("cornerRadius"));
    }

    [Fact]
    public void NulovyCornerRadius_JenOdstraniKlic()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "RectangleElement", "cornerRadius": 0 }""")));
        Assert.False(el.ContainsKey("cornerRadius"));
        Assert.False(el.ContainsKey("cornerStyle"));   // ostré rohy = default, nic nepřidávat
        Assert.False(el.ContainsKey("cornerSize"));
    }

    [Fact]
    public void BezCornerRadius_BezeZmeny()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "RectangleElement" }""")));
        Assert.False(el.ContainsKey("cornerStyle"));
        Assert.False(el.ContainsKey("cornerSize"));
    }
}
