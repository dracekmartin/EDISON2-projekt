using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Migrace, která u tabulky spojů ruší ruční šířku sloupce - od 22. 9. se počítá z obsahu.
/// Proti sobě stála dvě nastavení (zadané číslo a „stejně široké sloupce spojů") a vyhrávalo to
/// druhé bez vysvětlení, takže číslo zmizelo.
/// </summary>
public class MigrationSirkaZObsahuTests
{
    private static JsonObject Migruj(string prvekJson)
    {
        var root = JsonNode.Parse($$"""
        {
          "version": "1.14.0",
          "sections": [ { "elements": [ {{prvekJson}} ] } ]
        }
        """)!;
        return (JsonObject)new MigrationSirkaZObsahu().Migrate(root)["sections"]![0]!["elements"]![0]!;
    }

    [Fact]
    public void RucniSirkaZmizi()
    {
        var prvek = Migruj("""
        { "type": "LineTableElement", "sirkaSloupceSpojeMm": 4, "width": 190 }
        """);

        Assert.False(prvek.ContainsKey("sirkaSloupceSpojeMm"));
        Assert.Equal(190f, prvek["width"]!.GetValue<float>());
    }

    /// <summary>Šablona bez té volby měla sloupce srovnané - od 7. 9. to bylo výchozí chování,
    /// takže bez zápisu by se jí rozvržení změnilo.</summary>
    [Fact]
    public void ChybejiciVolbaSeZapiseJakoZapnuta()
    {
        var prvek = Migruj("""{ "type": "LineTableElement" }""");

        Assert.True(prvek["jednotnaSirkaSpoju"]!.GetValue<bool>());
    }

    /// <summary>Vypnutou volbu migrace nepřepíše - to je vědomé nastavení šablony.</summary>
    [Fact]
    public void VypnutaVolbaZustane()
    {
        var prvek = Migruj("""
        { "type": "LineTableElement", "jednotnaSirkaSpoju": false }
        """);

        Assert.False(prvek["jednotnaSirkaSpoju"]!.GetValue<bool>());
    }

    /// <summary>Jiných prvků se migrace netýká.</summary>
    [Fact]
    public void JinyPrvekSeNetkne()
    {
        var prvek = Migruj("""
        { "type": "TextElement", "sirkaSloupceSpojeMm": 4 }
        """);

        Assert.Equal(4f, prvek["sirkaSloupceSpojeMm"]!.GetValue<float>());
        Assert.False(prvek.ContainsKey("jednotnaSirkaSpoju"));
    }
}
