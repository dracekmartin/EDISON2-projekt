using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Doplnění sloupce šipky směru do linkových tabulek (`L6a`, 1.8.0 → 1.9.0).
/// </summary>
/// <remarks>
/// Podstata brány: <b>zákazníkovi se nesmí změnit hotová sestava</b>, takže se hlídá, že sloupec
/// přijde <b>skrytý</b> a na správné místo - ne jen to, že se objevil. Migrace vůbec existuje proto,
/// že dialog sloupců umí sloupce jen přeskládat a skrýt, ne přidat; bez ní by se šipka dala zapnout
/// jen v šablonách založených po téhle verzi.
/// </remarks>
public class MigrationLineArrowColumnTests
{
    private static JsonNode Sablona(string element) =>
        JsonNode.Parse($$"""
        { "version": "1.8.0", "sections": [ { "elements": [ {{element}} ] } ] }
        """)!;

    private static string LinkovaTabulka(string sloupce) =>
        $$"""
        { "type": "LineTableElement", "columns": [ {{sloupce}} ] }
        """;

    private const string BezneSloupce = """
        { "key": "Zone", "header": "Zóna", "width": 8.0, "visible": true },
        { "key": "Km", "header": "km", "width": 6.0, "visible": true },
        { "key": "StopName", "header": "Zastávka", "width": 42.0, "visible": true },
        { "key": "Trip", "header": "Spoj", "visible": true }
        """;

    private static JsonArray Sloupce(JsonNode root) =>
        root["sections"]![0]!["elements"]![0]!["columns"]!.AsArray();

    private static JsonNode Migruj(string element) =>
        new MigrationLineArrowColumn().Migrate(Sablona(element));

    [Fact]
    public void SloupecPrijdePredZastavku_TedyTamKdeHoMaStaryModul()
    {
        var cols = Sloupce(Migruj(LinkovaTabulka(BezneSloupce)));

        Assert.Equal(
            [ColumnKeys.Zone, ColumnKeys.Km, ColumnKeys.Arrow, ColumnKeys.StopName, ColumnKeys.Trip],
            cols.Select(c => c!["key"]!.GetValue<string>()));
    }

    /// <summary>Migrace nesmí změnit vzhled hotové šablony - proto skrytý.</summary>
    [Fact]
    public void SloupecPrijdeSkryty()
    {
        var cols = Sloupce(Migruj(LinkovaTabulka(BezneSloupce)));
        var sipka = cols.First(c => c!["key"]!.GetValue<string>() == ColumnKeys.Arrow)!;

        Assert.False(sipka["visible"]!.GetValue<bool>());
        Assert.Equal(4f, sipka["width"]!.GetValue<float>(), 0.001f);
    }

    [Fact]
    public void SpustenaDvakrat_NepridaSloupecDvakrat()
    {
        var jednou = Migruj(LinkovaTabulka(BezneSloupce));
        var dvakrat = new MigrationLineArrowColumn().Migrate(jednou);

        Assert.Single(Sloupce(dvakrat).Where(c => c!["key"]!.GetValue<string>() == ColumnKeys.Arrow));
    }

    /// <summary>Bez sloupce zastávek (teoreticky možné) se přidá na konec, ne že by se to zahodilo.</summary>
    [Fact]
    public void BezSloupceZastavek_PrijdeNaKonec()
    {
        var cols = Sloupce(Migruj(LinkovaTabulka("""
            { "key": "Km", "header": "km", "width": 6.0, "visible": true }
            """)));

        Assert.Equal([ColumnKeys.Km, ColumnKeys.Arrow],
            cols.Select(c => c!["key"]!.GetValue<string>()));
    }

    [Fact]
    public void JinehoPrvkuSeNetkne()
    {
        var root = Migruj("""
            { "type": "StopListElement", "columns": [
              { "key": "NAZEV_ZASTAVKY", "header": "Zastávka", "width": 50.0, "visible": true } ] }
            """);

        Assert.Equal([StopFields.StopName], Sloupce(root).Select(c => c!["key"]!.GetValue<string>()));
    }

    /// <summary>Prvek bez sloupců nebo s jiným typem hodnoty migraci nesmí shodit.</summary>
    [Theory]
    [InlineData("""{ "type": "LineTableElement" }""")]
    [InlineData("""{ "type": "LineTableElement", "columns": "nesmysl" }""")]
    [InlineData("""{ "columns": [] }""")]
    public void PoskozenyPrvek_Prezije(string element) =>
        new MigrationLineArrowColumn().Migrate(Sablona(element));
}
