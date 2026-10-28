using System.Text.Json.Nodes;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Obdélník od 28. 10. kreslí obrys jen rámečkem prvku - vlastní okraj se stejným tvarem i rohy byl
/// druhé nastavení pro tutéž čáru (rozhodnutí Jan, `B194` popisky jednoduchých prvků).
/// </summary>
public class MigrationObdelnikRamecekTests
{
    private static JsonObject Migruj(string prvekJson)
    {
        var root = JsonNode.Parse($$"""
        {
          "version": "1.17.0",
          "sections": [ { "elements": [ {{prvekJson}} ] } ]
        }
        """)!;
        return (JsonObject)new MigrationObdelnikRamecek().Migrate(root)["sections"]![0]!["elements"]![0]!;
    }

    [Fact]
    public void OkrajBezRamecku_PrejdeDoRamecku()
    {
        var prvek = Migruj("""
            { "type": "RectangleElement", "strokeColor": "#FF112233", "strokeWidth": 0.3,
              "borderColor": "#00FFFFFF", "borderWidth": 0 }
            """);

        Assert.Equal("#FF112233", prvek["borderColor"]!.GetValue<string>());
        Assert.Equal(0.3, prvek["borderWidth"]!.GetValue<double>());
        Assert.False(prvek.ContainsKey("strokeColor"));
        Assert.False(prvek.ContainsKey("strokeWidth"));
    }

    [Fact]
    public void ViditelnyRamecek_Zustane()
    {
        var prvek = Migruj("""
            { "type": "RectangleElement", "strokeColor": "#FF112233", "strokeWidth": 0.3,
              "borderColor": "#FFFF0000", "borderWidth": 1 }
            """);

        Assert.Equal("#FFFF0000", prvek["borderColor"]!.GetValue<string>());
        Assert.Equal(1, prvek["borderWidth"]!.GetValue<double>());
        Assert.False(prvek.ContainsKey("strokeWidth"));
    }

    [Fact]
    public void BezOkraje_RamecekSeNemeni()
    {
        var prvek = Migruj("""
            { "type": "RectangleElement", "strokeColor": "#FF000000", "strokeWidth": 0,
              "borderColor": "#00FFFFFF", "borderWidth": 0 }
            """);

        Assert.Equal(0, prvek["borderWidth"]!.GetValue<double>());
        Assert.False(prvek.ContainsKey("strokeColor"));
    }

    /// <summary>Chybějící klíče znamenaly tehdejší výchozí hodnoty: okraj černý 0,5 mm, rámeček žádný.</summary>
    [Fact]
    public void ChybejiciKlice_VychoziOkrajPrejdeDoRamecku()
    {
        var prvek = Migruj("""{ "type": "RectangleElement" }""");

        Assert.Equal("#FF000000", prvek["borderColor"]!.GetValue<string>());
        Assert.Equal(0.5, prvek["borderWidth"]!.GetValue<double>());
    }

    [Fact]
    public void BarvaZDat_PrejdeDoRamecku()
    {
        var prvek = Migruj("""{ "type": "RectangleElement", "strokeColor": "{BARVA_LINKY}", "strokeWidth": 0.5 }""");

        Assert.Equal("{BARVA_LINKY}", prvek["borderColor"]!.GetValue<string>());
    }

    [Fact]
    public void JinyPrvek_SeNemeni()
    {
        var prvek = Migruj("""{ "type": "LineElement", "strokeWidth": 0.3 }""");

        Assert.Equal(0.3, prvek["strokeWidth"]!.GetValue<double>());
        Assert.False(prvek.ContainsKey("borderWidth"));
    }

    /// <summary>Nový obdélník vypadá jako dřív: černý obrys 0,5 mm, teď jako rámeček prvku.</summary>
    [Fact]
    public void NovyObdelnik_MaCernyRamecek()
    {
        var obdelnik = new RectangleElement();

        Assert.Equal("#FF000000", obdelnik.BorderColor);
        Assert.Equal(0.5f, obdelnik.BorderWidth);
    }

    [Fact]
    public void Migrace_JeZapojena()
    {
        Assert.Contains(new TemplateMigrator().Migrations, m => m is MigrationObdelnikRamecek);
        Assert.Equal("1.18.0", TemplateMigrator.CurrentVersion);
    }
}
