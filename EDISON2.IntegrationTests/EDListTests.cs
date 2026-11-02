using System.Text.Json;
using ALWA.SYS;
using EDControls.EDList;

namespace EDISON2.IntegrationTests;

/// <summary>
/// EDList: JSON sloupců a řádků přes skutečné MLRest do <see cref="EDListParsing"/>. Parser je tolerantní
/// (chybějící vlastnost sloupce = výchozí hodnota, řádek bez <c>id</c> zmizí), proto se kontroluje i surový výstup.
/// Builder <c>ALVA.EDControls.EDList.List</c> kryje Caché <c>ListTest</c>, tady jen drát a parser.
/// </summary>
[Trait("Category", "Integration")]
public class EDListTests
{
    private const string MockList = "ALVA.EDControls.EDList.MockList";

    private static readonly string[] ColumnProperties = ["key", "label", "type", "width", "format", "align", "sortable", "visible"];

    [DevServerFact]
    public void InitializeList_MockList_RawJsonHasEveryPropertyAndParserReadsIt()
    {
        CVar raw = DevServer.Connect().QueryOrThrow(MockList, "InitializeList", new CVar());

        using (var doc = JsonDocument.Parse(raw["1"].Value?.ToString() ?? ""))
        {
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.All(doc.RootElement.EnumerateArray(), column =>
                Assert.All(ColumnProperties, name => Assert.True(column.TryGetProperty(name, out _), $"Sloupec bez vlastnosti '{name}'.")));
        }

        var columns = EDListParsing.ParseColumns(raw);

        Assert.Equal(["rz", "typ", "aktivni", "odjezd", "pocet", "datum", "posledniZmena", "barva"], columns.Select(c => c.Key));
        Assert.Equal(
            [ListColumnType.Text, ListColumnType.Text, ListColumnType.Bool, ListColumnType.HoursMinutes, ListColumnType.Number,
             ListColumnType.Date, ListColumnType.DateTime, ListColumnType.Color],
            columns.Select(c => c.Type));
        var pocet = columns.Single(c => c.Key == "pocet");
        Assert.Equal(("Počet km", 90, ListColumnAlign.Right, true, "N1"), (pocet.Label, pocet.Width, pocet.Align, pocet.Sortable, pocet.Format));
        Assert.Equal(ListColumnAlign.Right, columns.Single(c => c.Key == "odjezd").Align);
        Assert.All(columns.Where(c => c.Key != "pocet"), c => Assert.False(c.Sortable, $"Sloupec {c.Key} je řaditelný."));
        Assert.All(columns, c => Assert.True(c.Visible, $"Sloupec {c.Key} je skrytý."));
    }

    [DevServerFact]
    public void LoadList_MockList_EveryRowHasIdAndParserKeepsRawValues()
    {
        var rest = DevServer.Connect();
        var columns = EDListParsing.ParseColumns(rest.QueryOrThrow(MockList, "InitializeList", new CVar()));
        CVar raw = rest.QueryOrThrow(MockList, "LoadList", new CVar());

        var rawRows = 0;
        foreach (KeyValuePair<string, CVar> kvp in raw)
        {
            if (!int.TryParse(kvp.Key, out _)) continue;
            using var doc = JsonDocument.Parse(kvp.Value.Value?.ToString() ?? "");
            Assert.True(doc.RootElement.TryGetProperty("id", out _), $"Řádek {kvp.Key} nemá id, parser by ho zahodil.");
            rawRows++;
        }
        Assert.Equal(5, rawRows);

        var rows = EDListParsing.ParseRows(raw, columns).ToList();

        Assert.Equal(["1", "2", "3", "4", "5"], rows.Select(r => r.Id));
        var first = rows[0];
        Assert.Equal(("1B2 3456", "Kloubový", "#FFFF8000"), (first["rz"], first["typ"], first["barva"]));
        Assert.Equal("200;200;200", rows[4]["barva"]);
        // Zobrazený text závisí na kultuře, raw hodnotu typovaných sloupců nesou sort klíče.
        Assert.Equal(true, first.GetSortKey("aktivni"));
        Assert.Equal(830m, first.GetSortKey("odjezd"));
        Assert.Equal(142.5m, first.GetSortKey("pocet"));
        Assert.Equal(new DateTime(2024, 3, 15), first.GetSortKey("datum"));
        Assert.Equal(new DateTime(2024, 3, 15, 8, 30, 0), first.GetSortKey("posledniZmena"));
    }
}
