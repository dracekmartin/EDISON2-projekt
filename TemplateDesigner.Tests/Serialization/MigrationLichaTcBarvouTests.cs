using System.Text.Json.Nodes;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Podbarvení lichých Tč v Tabulce spojů se od 28. 10. nastavuje jen barvou (prázdná = bez), jako
/// Střídání řádků u ostatních tabulek - zaškrtávátko „Podbarvit lichá Tč" zmizelo (`B194d` E3c).
/// </summary>
public class MigrationLichaTcBarvouTests
{
    private static JsonObject Migruj(string prvekJson)
    {
        var root = JsonNode.Parse($$"""
        {
          "version": "1.15.0",
          "sections": [ { "elements": [ {{prvekJson}} ] } ]
        }
        """)!;
        return (JsonObject)new MigrationLichaTcBarvou().Migrate(root)["sections"]![0]!["elements"]![0]!;
    }

    [Fact]
    public void VypnutePodbarveni_DaPrazdnouBarvu()
    {
        var prvek = Migruj("""{ "type": "LineTableElement", "highlightOddTc": false, "oddTcBackColor": "#FFEEEEEE" }""");

        Assert.False(prvek.ContainsKey("highlightOddTc"));
        Assert.True(prvek.ContainsKey("oddTcBackColor"));
        Assert.Null(prvek["oddTcBackColor"]);
    }

    [Fact]
    public void ZapnutePodbarveni_NechaBarvu()
    {
        var prvek = Migruj("""{ "type": "LineTableElement", "highlightOddTc": true, "oddTcBackColor": "#FFDDDDDD" }""");

        Assert.False(prvek.ContainsKey("highlightOddTc"));
        Assert.Equal("#FFDDDDDD", prvek["oddTcBackColor"]!.GetValue<string>());
    }

    /// <summary>Výchozí bylo zapnuto - šablona bez obou klíčů zůstane bez nich a dostane výchozí barvu.</summary>
    [Fact]
    public void BezKlice_ZustaneBezeZmeny()
    {
        var prvek = Migruj("""{ "type": "LineTableElement", "width": 190 }""");

        Assert.False(prvek.ContainsKey("oddTcBackColor"));
        Assert.Equal(190f, prvek["width"]!.GetValue<float>());
    }

    [Fact]
    public void JinyPrvek_SeNemeni()
    {
        var prvek = Migruj("""{ "type": "DynamicTableElement", "highlightOddTc": false }""");

        Assert.False(prvek["highlightOddTc"]!.GetValue<bool>());
    }

    [Fact]
    public void Idempotence()
    {
        var root = JsonNode.Parse("""
        { "version": "1.15.0", "sections": [ { "elements": [ { "type": "LineTableElement", "highlightOddTc": false } ] } ] }
        """)!;
        var migrace = new MigrationLichaTcBarvou();
        var jednou = migrace.Migrate(root.DeepClone()).ToJsonString();

        Assert.Equal(jednou, migrace.Migrate(JsonNode.Parse(jednou)!).ToJsonString());
    }

    [Fact]
    public void PoskozenaSablona_Nespadne()
    {
        var migrace = new MigrationLichaTcBarvou();
        migrace.Migrate(JsonNode.Parse("""{ "version": "1.15.0" }""")!);
        migrace.Migrate(JsonNode.Parse("""{ "sections": [ null, { "elements": [ 1, { "type": "LineTableElement", "highlightOddTc": "ne" } ] } ] }""")!);
    }

    /// <summary>Celou cestou načtení: vypnuté podbarvení ze staré šablony vyjde jako prázdná barva.</summary>
    [Fact]
    public void StaraSablonaSVypnutymPodbarvenim_SeNactePrazdnouBarvou()
    {
        var t = TemplateSerializer.Deserialize("""
        { "version": "1.15.0", "name": "x", "sections": [ { "name": "s", "elements": [
          { "type": "LineTableElement", "highlightOddTc": false, "oddTcBackColor": "#FFEEEEEE" } ] } ] }
        """);

        Assert.Null(((LineTableElement)t.Sections[0].Elements[0]).OddTcBackColor);
    }

    /// <summary>Šablony se ukládají bez prázdných hodnot. Prázdná barva lichých Tč se ale uložit musí -
    /// výchozí je zapnuto, takže by se po načtení podbarvení samo zapnulo.</summary>
    [Fact]
    public void PrazdnaBarva_PrezijeUlozeniANacteni()
    {
        var t = TemplateSerializer.Deserialize("""
        { "version": "1.16.0", "name": "x", "sections": [ { "name": "s", "elements": [ { "type": "LineTableElement" } ] } ] }
        """);
        var prvek = (LineTableElement)t.Sections[0].Elements[0];
        Assert.Equal("#FFEEEEEE", prvek.OddTcBackColor);

        prvek.OddTcBackColor = null;
        var znovu = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(t));

        Assert.Null(((LineTableElement)znovu.Sections[0].Elements[0]).OddTcBackColor);
    }
}
