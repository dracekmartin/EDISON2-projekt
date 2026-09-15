using EDControls.EDList;
using EDISONMAIN2.LegacyMiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Překlad legacy tabulky na vstup EDListu: typ sloupce nese formátování, zarovnání i řazení,
/// takže se musí dostat až do <see cref="ListColumn"/>.
/// </summary>
public class DashboardListMapperTests
{
    private static DashboardTable Table(params DashboardColumn[] columns) =>
        new(columns, [], "prefs");

    [Fact]
    public void ToColumns_NumberKeepsTypeAndFormat()
    {
        var columns = DashboardListMapper.ToColumns(Table(
            new DashboardColumn("Rz", 'T', ""),
            new DashboardColumn("Celkové MTH", 'N', "#,##0.00")));

        Assert.Equal(ListColumnType.Text, columns[0].Type);
        Assert.Equal("Celkové MTH", columns[1].Label);
        Assert.Equal(ListColumnType.Number, columns[1].Type);
        Assert.Equal("#,##0.00", columns[1].Format);
        Assert.All(columns, c => Assert.True(c.Sortable || c.Key == DashboardListMapper.ForeColorKey));
    }

    /// <summary>Wire posílá datum už naformátované (dd.MM.yyyy), EDList parsuje jen Invariant - DATE by propadlo na raw.</summary>
    [Fact]
    public void ToColumns_DateStaysText() =>
        Assert.Equal(ListColumnType.Text,
            DashboardListMapper.ToColumns(Table(new DashboardColumn("Datum", 'D', "")))[0].Type);

    [Fact]
    public void ToColumns_AppendsHiddenForeColorColumn()
    {
        var columns = DashboardListMapper.ToColumns(Table(new DashboardColumn("A", 'T', "")));

        var fore = Assert.Single(columns, c => c.Key == DashboardListMapper.ForeColorKey);
        Assert.False(fore.Visible);
    }

    [Fact]
    public void ToRows_IdFromOrdinal_CellsByColumnPosition()
    {
        var table = new DashboardTable(
            [new DashboardColumn("Rz", 'T', ""), new DashboardColumn("MTH", 'N', "###0.00")],
            [new DashboardRow(["1A2 3456", "  123.45"], null, 0),
             new DashboardRow(["9B8 7654", "  999.00"], "#FF0000", 3)],
            "prefs");

        var rows = DashboardListMapper.ToRows(table);
        var keys = DashboardListMapper.ToColumns(table).Select(c => c.Key).ToList();

        // EDList zahodí řádek bez neprázdného id, legacy data ho nenesou.
        Assert.Equal(["1", "2"], rows.Select(r => r["id"]));
        Assert.Equal("1A2 3456", rows[0][keys[0]]);
        Assert.Equal("  123.45", rows[0][keys[1]]);
        Assert.Null(rows[0][DashboardListMapper.ForeColorKey]);
        Assert.Equal("#FF0000", rows[1][DashboardListMapper.ForeColorKey]);
    }

    /// <summary>Řádek kratší než hlavička (legacy to posílá) nesmí spadnout - chybějící buňka je prostě prázdná.</summary>
    [Fact]
    public void ToRows_ShorterRowThanColumns_DoesNotThrow()
    {
        var table = new DashboardTable(
            [new DashboardColumn("A", 'T', ""), new DashboardColumn("B", 'T', "")],
            [new DashboardRow(["jen A"], null, 0)],
            "prefs");

        var row = Assert.Single(DashboardListMapper.ToRows(table));

        Assert.False(row.ContainsKey("c1"));
    }
}
