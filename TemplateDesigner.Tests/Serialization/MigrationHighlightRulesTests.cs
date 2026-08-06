using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.0.0 → 1.1.0 (<see cref="MigrationHighlightRules"/>): zvýraznění řádků z jednoho
/// pravidla (podmínka + barva jako dvě samostatná pole) na seznam pravidel. Bez převodu by šablona
/// o své zvýraznění přišla; převedení „na půl" by naopak vyrobilo pravidlo bez barvy, které
/// v editoru jen mate.
/// </summary>
public class MigrationHighlightRulesTests
{
    private static JsonObject Migrate(JsonNode root) =>
        (JsonObject)new MigrationHighlightRules().Migrate(root);

    private static JsonObject FirstElement(JsonObject root) =>
        (JsonObject)root["sections"]![0]!["elements"]![0]!;

    private static JsonNode Root(string elementJson) => JsonNode.Parse($$"""
        { "sections": [ { "elements": [ {{elementJson}} ] } ] }
        """)!;

    [Fact]
    public void PodminkaSBarvou_SeStanePrvnimPravidlem()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "DynamicTableElement", "rowHighlightExpression": "{CENA} > 1000",
              "rowHighlightColor": "#FFFFDD00" }
            """)));

        var rules = (JsonArray)el["highlightRules"]!;
        Assert.Single(rules);
        Assert.Equal("{CENA} > 1000", rules[0]!["condition"]!.GetValue<string>());
        Assert.Equal("#FFFFDD00", rules[0]!["backgroundColor"]!.GetValue<string>());

        Assert.False(el.ContainsKey("rowHighlightExpression"));
        Assert.False(el.ContainsKey("rowHighlightColor"));
    }

    /// <summary>Staré zvýraznění potřebovalo podmínku I barvu - jinak nic nebarvilo, takže
    /// se z toho nesmí stát pravidlo.</summary>
    [Theory]
    [InlineData("{CENA} > 1000", null)]           // chybí barva
    [InlineData(null, "#FFFFDD00")]               // chybí podmínka
    [InlineData("", "#FFFFDD00")]                 // prázdná podmínka
    [InlineData("  ", "#FFFFDD00")]               // jen mezery
    [InlineData("{CENA} > 1000", "")]             // prázdná barva
    public void NeuplneZvyrazneni_PravidloNevytvori(string? expr, string? color)
    {
        var fields = new List<string> { "\"type\": \"DynamicTableElement\"" };
        if (expr is not null) fields.Add($"\"rowHighlightExpression\": \"{expr}\"");
        if (color is not null) fields.Add($"\"rowHighlightColor\": \"{color}\"");

        var el = FirstElement(Migrate(Root("{ " + string.Join(", ", fields) + " }")));

        Assert.False(el.ContainsKey("highlightRules"));
        Assert.False(el.ContainsKey("rowHighlightExpression"));
        Assert.False(el.ContainsKey("rowHighlightColor"));
    }

    [Fact]
    public void JizExistujiciSeznamPravidel_SeNeprebije()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "DynamicTableElement", "rowHighlightExpression": "{CENA} > 1000",
              "rowHighlightColor": "#FFFFDD00",
              "highlightRules": [ { "condition": "{POCET} > 5", "backgroundColor": "#FF00FF00" } ] }
            """)));

        var rules = (JsonArray)el["highlightRules"]!;
        Assert.Single(rules);
        Assert.Equal("{POCET} > 5", rules[0]!["condition"]!.GetValue<string>());
    }

    /// <summary>Poškozená šablona (číslo místo textu) nesmí shodit otevření.</summary>
    [Fact]
    public void NetextovaHodnota_SePreskoci()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "DynamicTableElement", "rowHighlightExpression": 42,
              "rowHighlightColor": "#FFFFDD00" }
            """)));

        Assert.False(el.ContainsKey("highlightRules"));
    }

    [Fact]
    public void OpakovanaMigrace_DavaStejnyVysledek()
    {
        var once = Migrate(Root("""
            { "type": "DynamicTableElement", "rowHighlightExpression": "{CENA} > 1000",
              "rowHighlightColor": "#FFFFDD00" }
            """));
        var twice = Migrate(once);

        var rules = (JsonArray)FirstElement(twice)["highlightRules"]!;
        Assert.Single(rules);
        Assert.Equal("{CENA} > 1000", rules[0]!["condition"]!.GetValue<string>());
    }

    [Fact]
    public void PrvekBezZvyrazneni_ZustaneBezeZmeny()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "TextElement", "text": "Ahoj" }""")));

        Assert.False(el.ContainsKey("highlightRules"));
        Assert.Equal("Ahoj", el["text"]!.GetValue<string>());
    }

    [Fact]
    public void SablonaBezSekci_Nespadne()
    {
        var root = JsonNode.Parse("""{ "version": "1.0.0" }""")!;

        Assert.NotNull(new MigrationHighlightRules().Migrate(root));
    }
}
