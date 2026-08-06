using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Testy migrace 1.6.0 → 1.7.0 (<see cref="MigrationGridStyle"/>) - nejsložitější převod, jaký
/// šablony mají: mřížka tabulkových prvků se sjednotila do objektu <c>grid</c> a **vnější obrys
/// přešel na rámeček elementu**. U tabulek se navíc pět hodnot volby <c>borderStyle</c> překládá
/// na dva přepínače vnitřních čar + přenos dosud zadrátovaného rámce.
///
/// Proč to testovat: uložená vývěska u zákazníka musí po aktualizaci vypadat identicky. Chyba
/// v tomhle převodu se pozná až tím, že hotové vývěsce zmizí (nebo přebydou) čáry.
/// </summary>
public class MigrationGridStyleTests
{
    private const string TableBorderColor = "#FF999999";
    private const string TableGridColor   = "#FFCCCCCC";
    private const double TableBorderMm    = 0.19;

    private static JsonObject Migrate(JsonNode root) =>
        (JsonObject)new MigrationGridStyle().Migrate(root);

    private static JsonObject FirstElement(JsonObject root) =>
        (JsonObject)root["sections"]![0]!["elements"]![0]!;

    private static JsonNode Root(string elementJson) => JsonNode.Parse($$"""
        { "sections": [ { "elements": [ {{elementJson}} ] } ] }
        """)!;

    private static JsonObject Grid(JsonObject el) => (JsonObject)el["grid"]!;

    private static bool Bool(JsonObject obj, string key) => obj[key]!.GetValue<bool>();

    // ── JŘ prvky: skaláry → objekt ────────────────────────────────────────────

    [Fact]
    public void JrPrvek_SkalarniNastaveniMrizkySePresuneDoObjektu()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "DepartureGridElement", "gridColor": "#FF112233",
              "gridLineWidth": 0.3, "gridLineStyle": "Dashed" }
            """)));

        var grid = Grid(el);
        Assert.Equal("#FF112233", grid["color"]!.GetValue<string>());
        Assert.Equal(0.3, grid["width"]!.GetValue<double>(), 3);
        Assert.Equal("Dashed", grid["style"]!.GetValue<string>());

        Assert.False(el.ContainsKey("gridColor"));
        Assert.False(el.ContainsKey("gridLineWidth"));
        Assert.False(el.ContainsKey("gridLineStyle"));
    }

    /// <summary>JŘ prvky čáry kreslily vždy - po převodu proto musí být oba přepínače zapnuté,
    /// jinak by hotové vývěsce zmizely čáry.</summary>
    [Fact]
    public void JrPrvek_BezPrepinacuCarJeZapneOba()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "DepartureGridElement", "gridColor": "#FF112233" }
            """)));

        Assert.True(Bool(Grid(el), "showRowLines"));
        Assert.True(Bool(Grid(el), "showColumnLines"));
    }

    /// <summary>Seznam zastávek měl dva vlastní přepínače - ty mají přednost před „kreslilo se vždy".</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SeznamZastavek_VlastniPrepinaceCarSePrenesou(bool rows, bool cols)
    {
        string json = $$"""
            { "type": "StopListElement", "gridColor": "#FF112233",
              "showRowSeparators": {{(rows ? "true" : "false")}},
              "showColumnSeparators": {{(cols ? "true" : "false")}} }
            """;

        var el = FirstElement(Migrate(Root(json)));

        Assert.Equal(rows, Bool(Grid(el), "showRowLines"));
        Assert.Equal(cols, Bool(Grid(el), "showColumnLines"));
        Assert.False(el.ContainsKey("showRowSeparators"));
        Assert.False(el.ContainsKey("showColumnSeparators"));
    }

    [Fact]
    public void JrPrvek_JizExistujiciObjektMrizkySeNeprebije()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "DepartureGridElement", "gridColor": "#FF112233",
              "grid": { "color": "#FFAABBCC" } }
            """)));

        Assert.Equal("#FFAABBCC", Grid(el)["color"]!.GetValue<string>());
        Assert.False(el.ContainsKey("gridColor"));
    }

    // ── Tabulky: volba pěti hodnot → přepínače + rámeček ──────────────────────

    [Theory]
    [InlineData("All",         true,  true)]
    [InlineData("RowsOnly",    true,  false)]
    [InlineData("ColumnsOnly", false, true)]
    [InlineData("Outer",       false, false)]
    [InlineData("None",        false, false)]
    public void Tabulka_VolbaRamceSePrelozNaVnitrniCary(string borderStyle, bool rows, bool cols)
    {
        var el = FirstElement(Migrate(Root($$"""
            { "type": "TableElement", "borderStyle": "{{borderStyle}}" }
            """)));

        Assert.Equal(rows, Bool(Grid(el), "showRowLines"));
        Assert.Equal(cols, Bool(Grid(el), "showColumnLines"));
        Assert.False(el.ContainsKey("borderStyle"));
    }

    [Theory]
    [InlineData("All")]
    [InlineData("RowsOnly")]
    [InlineData("ColumnsOnly")]
    [InlineData("Outer")]
    public void Tabulka_ZadratovanyRamecPrejdeDoRameckuElementu(string borderStyle)
    {
        var el = FirstElement(Migrate(Root($$"""
            { "type": "TableElement", "borderStyle": "{{borderStyle}}" }
            """)));

        Assert.Equal(TableBorderMm, el["borderWidth"]!.GetValue<double>(), 3);
        Assert.Equal(TableBorderColor, el["borderColor"]!.GetValue<string>());
    }

    /// <summary>„Žádný" znamenalo bez obrysu - přenést rámec by nakreslil čáru, která tam nebyla.</summary>
    [Fact]
    public void Tabulka_BezRamce_RamecekElementuZustaneNedotcen()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "TableElement", "borderStyle": "None" }
            """)));

        Assert.False(el.ContainsKey("borderWidth"));
        Assert.False(el.ContainsKey("borderColor"));
    }

    [Fact]
    public void Tabulka_VlastniRamecekUzivateleSeNeprebije()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "TableElement", "borderStyle": "All",
              "borderWidth": 1.5, "borderColor": "#FF00FF00" }
            """)));

        Assert.Equal(1.5, el["borderWidth"]!.GetValue<double>(), 3);
        Assert.Equal("#FF00FF00", el["borderColor"]!.GetValue<string>());
    }

    /// <summary>Nula a průhledná barva = rámeček byl fakticky vypnutý, takže ho smíme nastavit.</summary>
    [Fact]
    public void Tabulka_VypnutyRamecekSePrepiseZadratovanym()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "TableElement", "borderStyle": "All",
              "borderWidth": 0, "borderColor": "#00FFFFFF" }
            """)));

        Assert.Equal(TableBorderMm, el["borderWidth"]!.GetValue<double>(), 3);
        Assert.Equal(TableBorderColor, el["borderColor"]!.GetValue<string>());
    }

    [Fact]
    public void Tabulka_DostaneVychoziBarvuMrizky()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "TableElement", "borderStyle": "All" }
            """)));

        Assert.Equal(TableGridColor, Grid(el)["color"]!.GetValue<string>());
    }

    [Fact]
    public void Tabulka_JizNastavenaBarvaMrizkySeNeprebije()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "TableElement", "borderStyle": "All",
              "grid": { "color": "#FF010203" } }
            """)));

        Assert.Equal("#FF010203", Grid(el)["color"]!.GetValue<string>());
    }

    // ── Odolnost ──────────────────────────────────────────────────────────────

    [Fact]
    public void PrvekBezMrizky_ZustaneBezeZmeny()
    {
        var el = FirstElement(Migrate(Root("""{ "type": "TextElement", "text": "Ahoj" }""")));

        Assert.False(el.ContainsKey("grid"));
        Assert.Equal("Ahoj", el["text"]!.GetValue<string>());
    }

    [Fact]
    public void OpakovanaMigrace_DavaStejnyVysledek()
    {
        var once  = Migrate(Root("""{ "type": "TableElement", "borderStyle": "RowsOnly" }"""));
        var twice = Migrate(once);

        var el = FirstElement(twice);
        Assert.True(Bool(Grid(el), "showRowLines"));
        Assert.False(Bool(Grid(el), "showColumnLines"));
        Assert.Equal(TableBorderMm, el["borderWidth"]!.GetValue<double>(), 3);
    }

    [Fact]
    public void SablonaBezSekci_Nespadne()
    {
        var root = JsonNode.Parse("""{ "version": "1.6.0" }""")!;

        Assert.NotNull(new MigrationGridStyle().Migrate(root));
    }

    [Fact]
    public void SekceBezPrvku_Nespadne()
    {
        var root = JsonNode.Parse("""{ "sections": [ { "name": "Hlavička" } ] }""")!;

        Assert.NotNull(new MigrationGridStyle().Migrate(root));
    }

    /// <summary>Neznámá hodnota volby se chová jako „Vnější" - obrys byl vždy nakreslený,
    /// takže se přenese, ale vnitřní čáry ne.</summary>
    [Fact]
    public void Tabulka_NeznamaVolbaRamce_SeChovaJakoVnejsi()
    {
        var el = FirstElement(Migrate(Root("""
            { "type": "TableElement", "borderStyle": "NecoNoveho" }
            """)));

        Assert.False(Bool(Grid(el), "showRowLines"));
        Assert.False(Bool(Grid(el), "showColumnLines"));
        Assert.Equal(TableBorderMm, el["borderWidth"]!.GetValue<double>(), 3);
    }
}
