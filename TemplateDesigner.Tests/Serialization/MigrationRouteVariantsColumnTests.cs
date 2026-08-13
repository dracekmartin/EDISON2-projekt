using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Převod zaškrtávátka „Všechny varianty tras" na položku seznamu sloupců (`B80`, 1.7.0 → 1.8.0).
/// Podstata brány: **zákazníkovi se nesmí změnit hotová vývěska**, takže se hlídá viditelnost,
/// pozice i převzatý styl - ne jen to, že se položka objevila.
/// </summary>
public class MigrationRouteVariantsColumnTests
{
    private static JsonNode Sablona(string stopListElement) =>
        JsonNode.Parse($$"""
        { "version": "1.7.0", "sections": [ { "elements": [ {{stopListElement}} ] } ] }
        """)!;

    private static string StopList(string? flag, bool casVisible = true, string extraCasProps = "") =>
        $$"""
        {
          "type": "StopListElement",
          {{(flag is null ? "" : $"\"showAllRouteVariants\": {flag},")}}
          "columns": [
            { "key": "CAS_JIZDY", "header": "min.", "width": 8.0, "visible": {{(casVisible ? "true" : "false")}}{{extraCasProps}} },
            { "key": "NAZEV_ZASTAVKY", "header": "Zastávka", "width": 50.0, "visible": true }
          ]
        }
        """;

    private static JsonArray Sloupce(JsonNode root) =>
        root["sections"]![0]!["elements"]![0]!["columns"]!.AsArray();

    private static JsonNode Migruj(string stopList) =>
        new MigrationRouteVariantsColumn().Migrate(Sablona(stopList));

    [Fact]
    public void PolozkaVznikneHnedZaSloupcemCasu()
    {
        var cols = Sloupce(Migruj(StopList("true")));

        Assert.Equal(
            [StopFields.TravelTime, StopFields.TravelTimeVariants, StopFields.StopName],
            cols.Select(c => c!["key"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ViditelnostOdpovidaZaskrtavatku(string flag, bool ocekavana)
    {
        var cols = Sloupce(Migruj(StopList(flag)));

        Assert.Equal(ocekavana, cols[1]!["visible"]!.GetValue<bool>());
    }

    /// <summary>Věrný převod dnešního vzhledu: varianty se rozvíjely ZE sloupce „Čas", takže při jeho
    /// skrytí se netiskly, ať bylo zaškrtnuto cokoli. Zapnout je tady by zákazníkovi vysypalo do
    /// hotové vývěsky až dvanáct sloupců, aniž by na cokoli klikl.</summary>
    [Fact]
    public void SkrytySloupecCasu_ZnamenaSkryteVarianty()
    {
        var cols = Sloupce(Migruj(StopList("true", casVisible: false)));

        Assert.False(cols[1]!["visible"]!.GetValue<bool>());
    }

    /// <summary>Chybějící vlastnost = dosavadní default prvku (zapnuto), ne vypnuto.</summary>
    [Fact]
    public void ChybejiciVlastnost_SeBereJakoZapnuta()
    {
        var cols = Sloupce(Migruj(StopList(flag: null)));

        Assert.True(cols[1]!["visible"]!.GetValue<bool>());
    }

    /// <summary>Zarovnání a styl si varianty dosud půjčovaly od sloupce „Čas" za běhu - po převodu
    /// je musí nést samy, jinak se změní vzhled.</summary>
    [Fact]
    public void ZarovnaniAStylSePreberouZeSloupceCasu()
    {
        string cas = ""","alignment": "Right", "style": { "data": { "bold": true } }""";
        var cols = Sloupce(Migruj(StopList("true", extraCasProps: cas)));

        Assert.Equal("Right", cols[1]!["alignment"]!.GetValue<string>());
        Assert.True(cols[1]!["style"]!["data"]!["bold"]!.GetValue<bool>());
    }

    [Fact]
    public void ZrusenaVlastnostZeSablonyZmizi()
    {
        var el = Migruj(StopList("true"))["sections"]![0]!["elements"]![0]!.AsObject();

        Assert.False(el.ContainsKey("showAllRouteVariants"));
    }

    /// <summary>Šířka = dosavadní zadrátovaných 6 mm, ať se vzhled nezmění.</summary>
    [Fact]
    public void SirkaOdpovidaDosavadniZadratovane()
    {
        var cols = Sloupce(Migruj(StopList("true")));

        Assert.Equal(6f, cols[1]!["width"]!.GetValue<float>());
    }

    [Fact]
    public void DvojiSpusteni_DaTotez()
    {
        var migrace = new MigrationRouteVariantsColumn();
        var jednou = migrace.Migrate(Sablona(StopList("true")));
        string poPrvnim = jednou.ToJsonString();

        Assert.Equal(poPrvnim, migrace.Migrate(jednou).ToJsonString());
    }

    /// <summary>Jiné prvky se nesmí tknout - položka patří jen seznamu zastávek.</summary>
    [Fact]
    public void JinyPrvekZustaneBezeZmeny()
    {
        string text = """{ "type": "TextElement", "columns": [ { "key": "CAS_JIZDY" } ] }""";

        var cols = Sloupce(Migruj(text));

        Assert.Single(cols);
    }

    [Theory]
    [InlineData("""{ "version": "1.7.0" }""")]
    [InlineData("""{ "version": "1.7.0", "sections": [] }""")]
    [InlineData("""{ "version": "1.7.0", "sections": [ { } ] }""")]
    [InlineData("""{ "version": "1.7.0", "sections": [ { "elements": [ { "type": "StopListElement" } ] } ] }""")]
    [InlineData("""{ "version": "1.7.0", "sections": [ { "elements": [ { "type": "StopListElement", "columns": "nesmysl" } ] } ] }""")]
    public void PoskozenaSablona_Nespadne(string json)
    {
        var root = JsonNode.Parse(json)!;

        var ex = Record.Exception(() => new MigrationRouteVariantsColumn().Migrate(root));

        Assert.Null(ex);
    }
}
