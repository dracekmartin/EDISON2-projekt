using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Drop-target matematika mřížky oblíbených dlaždic: <see cref="SlotGridPanel.ColumnsFor"/> (počet
/// sloupců dle šířky, min 1) a <see cref="SlotGridPanel.SlotAtFor"/> (row-major index pod bodem).
/// Špatný slot tiše přesune dlaždici jinam, proto pokrýváme testem. Statické helpery = bez WPF instance.
/// </summary>
public class SlotGridPanelTests
{
    // Defaultní rozměry buňky panelu.
    private const double CellW = 140;
    private const double CellH = 84;

    private static int SlotAt(double x, double y, double width) =>
        SlotGridPanel.SlotAtFor(x, y, width, CellW, CellH);

    [Theory]
    [InlineData(420, 3)]
    [InlineData(280, 2)]
    [InlineData(140, 1)]
    [InlineData(100, 1)]   // pod jednu buňku → stále aspoň 1 sloupec
    [InlineData(0, 1)]
    public void Columns_ScalesWithWidth_MinOne(double width, int expected)
    {
        Assert.Equal(expected, SlotGridPanel.ColumnsFor(width, CellW));
    }

    [Fact]
    public void SlotAt_TopLeft_IsZero()
    {
        Assert.Equal(0, SlotAt(0, 0, width: 420));
    }

    [Fact]
    public void SlotAt_SecondColumn_FirstRow()
    {
        // 3 sloupce (šířka 420); x v druhém sloupci → slot 1.
        Assert.Equal(1, SlotAt(150, 10, width: 420));
    }

    [Fact]
    public void SlotAt_SecondRow_IsRowMajor()
    {
        // 3 sloupce; y ve druhém řádku (>84), x v prvním sloupci → 1*3 + 0 = 3.
        Assert.Equal(3, SlotAt(10, 90, width: 420));
    }

    [Fact]
    public void SlotAt_ClampsColumnToLastColumn()
    {
        // x daleko za posledním sloupcem se ořízne na cols-1 (2), ne přeteče do dalšího řádku.
        Assert.Equal(2, SlotAt(9999, 0, width: 420));
    }

    [Fact]
    public void SlotAt_NegativePoint_ClampsToZero()
    {
        Assert.Equal(0, SlotAt(-50, -50, width: 420));
    }
}
