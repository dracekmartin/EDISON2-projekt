using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Matematika mřížky plochy: <see cref="TileGridPanel.CellAtFor"/> (buňka pod bodem dropu)
/// a <see cref="TileGridPanel.ColumnsFor"/> (šířka mřížky pro přidělování nových pozic).
/// Špatná buňka tiše přesune dlaždici jinam, proto pokrýváme testem. Statické helpery = bez
/// WPF instance.
/// </summary>
public class TileGridPanelTests
{
    // Defaultní rozměry buňky panelu.
    private const double CellW = 140;
    private const double CellH = 84;

    private static (int Column, int Row) CellAt(double x, double y) =>
        TileGridPanel.CellAtFor(x, y, CellW, CellH);

    [Theory]
    [InlineData(420, 3)]
    [InlineData(280, 2)]
    [InlineData(140, 1)]
    [InlineData(100, 1)]   // pod jednu buňku → stále aspoň 1 sloupec
    [InlineData(0, 1)]
    public void Columns_ScalesWithWidth_MinOne(double width, int expected)
    {
        Assert.Equal(expected, TileGridPanel.ColumnsFor(width, CellW));
    }

    [Fact]
    public void CellAt_TopLeft_IsOrigin()
    {
        Assert.Equal((0, 0), CellAt(0, 0));
    }

    [Fact]
    public void CellAt_SecondColumn_FirstRow()
    {
        Assert.Equal((1, 0), CellAt(150, 10));
    }

    [Fact]
    public void CellAt_SecondRow_FirstColumn()
    {
        Assert.Equal((0, 1), CellAt(10, 90));
    }

    [Fact]
    public void CellAt_FarRight_KeepsGrowingColumn_NoWrap()
    {
        // Vpravo už není hranice (přesah = vodorovný scroll), takže se nezalomí na další řádek.
        Assert.Equal((7, 0), CellAt(1000, 10));
    }

    [Fact]
    public void CellAt_NegativePoint_ClampsToOrigin()
    {
        Assert.Equal((0, 0), CellAt(-50, -50));
    }
}
