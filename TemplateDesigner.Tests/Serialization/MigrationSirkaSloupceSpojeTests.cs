using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Migrace, která u tabulky spojů slévá dvojici „nejmenší/největší šířka sloupce" do jedné šířky.
/// Přebírá se strop - to bylo pole, kterým se šířka reálně určovala; když chybí, vezme se dno.
/// </summary>
public class MigrationSirkaSloupceSpojeTests
{
    private static JsonNode Sablona(string prvekJson) =>
        JsonNode.Parse($$"""
        {
          "version": "1.13.0",
          "sections": [ { "elements": [ {{prvekJson}} ] } ]
        }
        """)!;

    private static JsonObject Prvek(JsonNode root) =>
        (JsonObject)root["sections"]![0]!["elements"]![0]!;

    private static JsonObject Migruj(string prvekJson) =>
        Prvek(new MigrationSirkaSloupceSpoje().Migrate(Sablona(prvekJson)));

    [Fact]
    public void StropSeStaneSirkou()
    {
        var prvek = Migruj("""
        { "type": "LineTableElement", "nejmensiSirkaSpojeMm": 3.5, "nejvetsiSirkaSpojeMm": 6 }
        """);

        Assert.Equal(6f, prvek["sirkaSloupceSpojeMm"]!.GetValue<float>());
        Assert.False(prvek.ContainsKey("nejmensiSirkaSpojeMm"));
        Assert.False(prvek.ContainsKey("nejvetsiSirkaSpojeMm"));
    }

    /// <summary>Bez stropu se vezme dno - jiné číslo o zamýšlené šířce v šabloně není.</summary>
    [Fact]
    public void BezStropuSeVezmeDno()
    {
        var prvek = Migruj("""
        { "type": "LineTableElement", "nejmensiSirkaSpojeMm": 4 }
        """);

        Assert.Equal(4f, prvek["sirkaSloupceSpojeMm"]!.GetValue<float>());
    }

    /// <summary>Nula znamenala „nenastaveno", takže se nesmí stát zadanou šířkou.</summary>
    [Fact]
    public void NulaZnamenaNenastaveno()
    {
        var prvek = Migruj("""
        { "type": "LineTableElement", "nejmensiSirkaSpojeMm": 0, "nejvetsiSirkaSpojeMm": 0 }
        """);

        Assert.False(prvek.ContainsKey("sirkaSloupceSpojeMm"));
    }

    /// <summary>Prvek bez obou polí zůstane beze změny - šablona se jím nesmí rozbít.</summary>
    [Fact]
    public void PrvekBezPoliZustaneBezeZmeny()
    {
        var prvek = Migruj("""{ "type": "LineTableElement", "width": 190 }""");

        Assert.False(prvek.ContainsKey("sirkaSloupceSpojeMm"));
        Assert.Equal(190f, prvek["width"]!.GetValue<float>());
    }

    /// <summary>Jiných prvků se migrace netýká.</summary>
    [Fact]
    public void JinyPrvekSeNetkne()
    {
        var prvek = Migruj("""
        { "type": "TextElement", "nejvetsiSirkaSpojeMm": 6 }
        """);

        Assert.False(prvek.ContainsKey("sirkaSloupceSpojeMm"));
        Assert.Equal(6f, prvek["nejvetsiSirkaSpojeMm"]!.GetValue<float>());
    }
}
