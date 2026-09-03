using System.Text.Json.Nodes;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Podbarvení záhlaví se zapisuje do prvku (1.10.0 → 1.11.0).
/// </summary>
/// <remarks>
/// Podstata brány: <b>zákazníkovi se nesmí odbarvit hotová sestava</b>. Šedá dřív nebyla v prvku,
/// jen jako záloha v kaskádě - jakmile záloha zmizela, musí ji migrace prvkům dopsat, jinak by
/// záhlaví zbledlo každému, kdo si barvu nikdy nenastavoval (tedy skoro všem). Druhá polovina je
/// idempotence <b>i pro prázdnou hodnotu</b>: prázdno už znamená vědomé „bez podbarvení", takže ho
/// migrace nesmí přepsat zpátky na šedou - to by přesně vrátilo chybu, kterou celá položka řeší.
/// </remarks>
public class MigrationZahlaviPozadiTests
{
    private static JsonNode Sablona(string element) =>
        JsonNode.Parse($$"""
        { "version": "1.10.0", "sections": [ { "elements": [ {{element}} ] } ] }
        """)!;

    private static JsonNode Migruj(string element) =>
        new MigrationZahlaviPozadi().Migrate(Sablona(element));

    private static string? Pozadi(JsonNode root) =>
        root["sections"]![0]!["elements"]![0]!["headerStyle"]?["backgroundColor"]?.GetValue<string>();

    private static JsonNode? StylZahlavi(JsonNode root) =>
        root["sections"]![0]!["elements"]![0]!["headerStyle"];

    [Theory]
    [InlineData("StopListElement")]
    [InlineData("DepartureGridElement")]
    [InlineData("NotesLegendElement")]
    [InlineData("LineTableElement")]
    public void PrvkyJizdnichRadu_DostanouSedouZapsanou(string typ)
    {
        var root = Migruj($$"""{ "type": "{{typ}}" }""");
        Assert.Equal(ScheduleElementBase.NoveZahlaviPozadi, Pozadi(root));
    }

    [Theory]
    [InlineData("TableElement")]
    [InlineData("DynamicTableElement")]
    public void Tabulky_DostanouSvujOdstin(string typ)
    {
        var root = Migruj($$"""{ "type": "{{typ}}" }""");
        Assert.Equal(TableElement.NoveZahlaviPozadi, Pozadi(root));
    }

    [Fact]
    public void PrvekSeStylemZahlaviBezPozadi_PozadiDostane()
    {
        var root = Migruj("""
            { "type": "NotesLegendElement", "headerStyle": { "fontSize": 10.0, "bold": true } }
            """);
        Assert.Equal(ScheduleElementBase.NoveZahlaviPozadi, Pozadi(root));
        Assert.Equal(10.0, StylZahlavi(root)!["fontSize"]!.GetValue<double>());
    }

    [Fact]
    public void VlastniBarvaZustaneBytZmenena()
    {
        var root = Migruj("""
            { "type": "NotesLegendElement", "headerStyle": { "backgroundColor": "#FF3366CC" } }
            """);
        Assert.Equal("#FF3366CC", Pozadi(root));
    }

    [Fact]
    public void VymazanePozadiZustanePrazdne()
    {
        // Prázdno = vědomé „bez podbarvení". Přepsat ho na šedou by vrátilo přesně tu chybu,
        // kvůli které migrace vznikla (nález Pavel 1. 9.).
        var root = Migruj("""
            { "type": "NotesLegendElement", "headerStyle": { "backgroundColor": "" } }
            """);
        Assert.Equal("", Pozadi(root));
    }

    [Fact]
    public void OstatniPrvkySeNetknou()
    {
        var root = Migruj("""{ "type": "TextElement", "text": "Ahoj" }""");
        Assert.Null(StylZahlavi(root));
    }

    [Fact]
    public void DruhyProchodNicNezmeni()
    {
        var prvni = Migruj("""{ "type": "StopListElement" }""");
        var druhy = new MigrationZahlaviPozadi().Migrate(prvni.DeepClone());
        Assert.Equal(prvni.ToJsonString(), druhy.ToJsonString());
    }

    [Fact]
    public void PoskozenaSablonaNespadne()
    {
        var bezSekci  = JsonNode.Parse("""{ "version": "1.10.0" }""")!;
        var sekceBezPrvku = JsonNode.Parse("""{ "version": "1.10.0", "sections": [ {} ] }""")!;
        var prvekBezTypu  = JsonNode.Parse("""
            { "version": "1.10.0", "sections": [ { "elements": [ { "width": 40.0 } ] } ] }
            """)!;

        var m = new MigrationZahlaviPozadi();
        Assert.NotNull(m.Migrate(bezSekci));
        Assert.NotNull(m.Migrate(sekceBezPrvku));
        Assert.NotNull(m.Migrate(prvekBezTypu));
    }
}
