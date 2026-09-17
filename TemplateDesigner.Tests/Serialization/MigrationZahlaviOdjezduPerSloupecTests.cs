using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// `B158` - migrace, která u Minut odjezdů maže per-sloupcovou typografii záhlaví. Od 14. 6. ji
/// kreslení ignorovalo; než se kaskáda zapnula zpátky, musela z šablon zmizet, jinak by sloupce
/// najednou vypadaly jinak než celé léto. Typicky jde o `bold`, které tam zapisoval import z PDF.
/// </summary>
public class MigrationZahlaviOdjezduPerSloupecTests
{
    private static JsonNode Sablona(string departureGridJson) =>
        JsonNode.Parse($$"""
        {
          "version": "1.12.0",
          "sections": [ { "elements": [ {{departureGridJson}} ] } ]
        }
        """)!;

    private static JsonNode Migruj(JsonNode root) => new MigrationZahlaviOdjezduPerSloupec().Migrate(root);

    private static JsonObject? ZahlaviSkupiny(JsonNode root, int index = 0) =>
        root["sections"]![0]!["elements"]![0]!["groupColumns"]![index]!["style"]?["header"] as JsonObject;

    [Fact]
    public void Typografie_ZmiziBarvaZustane()
    {
        var root = Migruj(Sablona("""
        { "type": "DepartureGridElement", "groupColumns": [
            { "skupinaDnuId": "g0", "style": { "header": {
                "backgroundColor": "#FFEEEEEE", "bold": true, "fontSize": 12, "fontFamily": "Arial" } } } ] }
        """));

        var zahlavi = ZahlaviSkupiny(root);
        Assert.NotNull(zahlavi);
        Assert.Equal("#FFEEEEEE", zahlavi["backgroundColor"]!.GetValue<string>());
        Assert.False(zahlavi.ContainsKey("bold"));
        Assert.False(zahlavi.ContainsKey("fontSize"));
        Assert.False(zahlavi.ContainsKey("fontFamily"));
    }

    [Fact]
    public void ZahlaviJenSTypografii_ZmiziCele()
    {
        var root = Migruj(Sablona("""
        { "type": "DepartureGridElement", "groupColumns": [
            { "skupinaDnuId": "g0", "style": { "header": { "bold": true } } } ] }
        """));

        Assert.Null(ZahlaviSkupiny(root));
    }

    [Fact]
    public void SloupecHodin_SeCistiTaky()
    {
        var root = Migruj(Sablona("""
        { "type": "DepartureGridElement",
          "hourColumnStyle": { "header": { "bold": true, "textColor": "#FF000000" } } }
        """));

        var zahlavi = root["sections"]![0]!["elements"]![0]!["hourColumnStyle"]!["header"] as JsonObject;
        Assert.NotNull(zahlavi);
        Assert.False(zahlavi.ContainsKey("bold"));
        Assert.Equal("#FF000000", zahlavi["textColor"]!.GetValue<string>());
    }

    [Fact]
    public void DatoveBunky_ZustanouNedotcene()
    {
        var root = Migruj(Sablona("""
        { "type": "DepartureGridElement", "groupColumns": [
            { "skupinaDnuId": "g0", "style": { "data": { "bold": true, "fontSize": 11 } } } ] }
        """));

        var data = root["sections"]![0]!["elements"]![0]!["groupColumns"]![0]!["style"]!["data"] as JsonObject;
        Assert.NotNull(data);
        Assert.True(data["bold"]!.GetValue<bool>());
        Assert.Equal(11, data["fontSize"]!.GetValue<float>());
    }

    [Fact]
    public void JinyPrvek_SeNetkne()
    {
        var root = Migruj(Sablona("""
        { "type": "StopListElement", "groupColumns": [
            { "skupinaDnuId": "g0", "style": { "header": { "bold": true } } } ] }
        """));

        Assert.True(ZahlaviSkupiny(root)!["bold"]!.GetValue<bool>());
    }

    [Fact]
    public void Idempotence_DruhyBehNicNezmeni()
    {
        var root = Migruj(Sablona("""
        { "type": "DepartureGridElement", "groupColumns": [
            { "skupinaDnuId": "g0", "style": { "header": { "backgroundColor": "#FFEEEEEE", "bold": true } } } ] }
        """));
        string poPrvnim = root.ToJsonString();

        Assert.Equal(poPrvnim, Migruj(root).ToJsonString());
    }

    [Fact]
    public void PoskozenaSablona_Nespadne()
    {
        var bezSekci = JsonNode.Parse("""{ "version": "1.12.0" }""")!;
        Assert.NotNull(Migruj(bezSekci));

        var prazdnyPrvek = Sablona("""{ "type": "DepartureGridElement" }""");
        Assert.NotNull(Migruj(prazdnyPrvek));

        var sloupecBezStylu = Sablona("""
        { "type": "DepartureGridElement", "groupColumns": [ { "skupinaDnuId": "g0" } ] }
        """);
        Assert.NotNull(Migruj(sloupecBezStylu));
    }
}
