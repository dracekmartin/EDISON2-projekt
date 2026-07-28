using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.2.0 → 1.3.0 (<see cref="MigrationUnifyStopColumns"/>): remap StopList klíčů na field ID,
/// odstranění zrušených sloupců (TČ/průběhy/ZT), remap DynamicTable field a přepis placeholder tokenů
/// s ochranou hranice (skalární {POPIS_DOPRAVCE} se netkne).
/// </summary>
public class MigrationUnifyStopColumnsTests
{
    private static JsonObject Migrate(JsonNode root) =>
        (JsonObject)new MigrationUnifyStopColumns().Migrate(root);

    private static JsonArray StopCols(JsonObject root) =>
        (JsonArray)root["sections"]![0]!["elements"]![0]!["columns"]!;

    [Fact]
    public void StopList_RemapujeKliceNaFieldId()
    {
        var root = JsonNode.Parse("""
        {
          "sections": [ { "elements": [
            { "type": "StopListElement", "columns": [
              { "key": "TravelTime" }, { "key": "StopName" }, { "key": "Zone" },
              { "key": "Poradi" }, { "key": "JeAktualni" }
            ] }
          ] } ]
        }
        """)!;

        var cols = StopCols(Migrate(root));
        var keys = cols.Select(c => c!["key"]!.GetValue<string>()).ToArray();
        Assert.Equal(["CAS_JIZDY", "NAZEV_ZASTAVKY", "ZONY", "PORADI", "AKTUALNI_ZASTAVKA"], keys);
    }

    [Fact]
    public void StopList_OdstraniZruseneSloupce()
    {
        var root = JsonNode.Parse("""
        {
          "sections": [ { "elements": [
            { "type": "StopListElement", "columns": [
              { "key": "StopName" }, { "key": "Tc" }, { "key": "RouteLetters" }, { "key": "BaseRoute" }
            ] }
          ] } ]
        }
        """)!;

        var keys = StopCols(Migrate(root)).Select(c => c!["key"]!.GetValue<string>()).ToArray();
        Assert.Equal(["NAZEV_ZASTAVKY"], keys);
    }

    [Fact]
    public void DynamicTable_RemapujeFieldId()
    {
        var root = JsonNode.Parse("""
        {
          "sections": [ { "elements": [
            { "type": "DynamicTableElement", "columns": [
              { "field": "POPIS" }, { "field": "JE_AKTUALNI" }, { "field": "CAS_JIZDY" }
            ] }
          ] } ]
        }
        """)!;

        var fields = StopCols(Migrate(root)).Select(c => c!["field"]!.GetValue<string>()).ToArray();
        Assert.Equal(["NAZEV_ZASTAVKY", "AKTUALNI_ZASTAVKA", "CAS_JIZDY"], fields);
    }

    [Fact]
    public void Tokeny_PrepiseCelaPoleAleNeSkalarniSPrefixem()
    {
        var root = JsonNode.Parse("""
        {
          "sections": [ { "elements": [
            { "type": "TextElement", "text": "{POPIS} - {POPIS:UPPER} - {POPIS_DOPRAVCE}",
              "visibilityExpression": "{JE_AKTUALNI} == true", "content": "č. {TC} zast." }
          ] } ]
        }
        """)!;

        var el = Migrate(root)["sections"]![0]!["elements"]![0]!;
        Assert.Equal("{NAZEV_ZASTAVKY} - {NAZEV_ZASTAVKY:UPPER} - {POPIS_DOPRAVCE}", el["text"]!.GetValue<string>());
        Assert.Equal("{AKTUALNI_ZASTAVKA} == true", el["visibilityExpression"]!.GetValue<string>());
        Assert.Equal("č.  zast.", el["content"]!.GetValue<string>());
    }

    [Fact]
    public void JeIdempotentni()
    {
        var root = JsonNode.Parse("""
        {
          "sections": [ { "elements": [
            { "type": "StopListElement", "columns": [ { "key": "NAZEV_ZASTAVKY" }, { "key": "CAS_JIZDY" } ] }
          ] } ]
        }
        """)!;

        var once = Migrate(root);
        var twice = Migrate(JsonNode.Parse(once.ToJsonString())!);
        Assert.Equal(once.ToJsonString(), twice.ToJsonString());
    }
}
