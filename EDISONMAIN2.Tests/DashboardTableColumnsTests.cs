using System.Windows.Controls;
using EDISONMAIN2.LegacyMiniApps;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Sloupce legacy tabulky se staví za běhu z dat. Poslední musí dotáhnout zbytek šířky -
/// bez toho zůstane vpravo od tabulky prázdná plocha.
/// </summary>
public class DashboardTableColumnsTests
{
    [Fact]
    public void LastColumn_TakesRemainingWidth()
    {
        Sta.Run(() =>
        {
            var grid = new DataGrid();
            DashboardTableColumns.SetColumns(grid, ["Rz", "Celkové MTH"]);

            Assert.Equal(2, grid.Columns.Count);
            Assert.True(grid.Columns[0].Width.IsAuto);
            Assert.True(grid.Columns[^1].Width.IsStar);
            Assert.True(grid.Columns[^1].MinWidth > 0);
        });
    }

    [Fact]
    public void Headers_FollowSourceOrder()
    {
        Sta.Run(() =>
        {
            var grid = new DataGrid();
            DashboardTableColumns.SetColumns(grid, ["Rz", "MTH", "Datum"]);

            Assert.Equal(["Rz", "MTH", "Datum"], grid.Columns.Select(c => c.Header));
        });
    }

    /// <summary>Firma bez dat pošle prázdnou tabulku - hvězdičková větev to nesmí shodit.</summary>
    [Fact]
    public void EmptyColumns_DoNotThrow()
    {
        Sta.Run(() =>
        {
            var grid = new DataGrid();
            DashboardTableColumns.SetColumns(grid, []);

            Assert.Empty(grid.Columns);
        });
    }

    [Fact]
    public void NewColumns_ReplaceOld()
    {
        Sta.Run(() =>
        {
            var grid = new DataGrid();
            DashboardTableColumns.SetColumns(grid, ["A", "B", "C"]);
            DashboardTableColumns.SetColumns(grid, ["X"]);

            Assert.Single(grid.Columns);
            Assert.True(grid.Columns[0].Width.IsStar);
        });
    }
}
