using System.Windows;
using System.Windows.Controls;
using EDISONMAIN2.MiniApps;
using EDTesting;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Absolutní mřížka plochy: desired size = obsah (přesah řeší scroll hostitele) a děti
/// leží přesně na svých Column/Row buňkách. Panel chce STA - sdílený Sta helper.
/// </summary>
public class TileGridPanelLayoutTests
{
    [Fact]
    public void AttachedColumnRow_RoundTrip_DefaultZero()
    {
        Sta.Run(() =>
        {
            var tile = new Border();

            Assert.Equal(0, TileGridPanel.GetColumn(tile));
            Assert.Equal(0, TileGridPanel.GetRow(tile));

            TileGridPanel.SetColumn(tile, 3);
            TileGridPanel.SetRow(tile, 2);

            Assert.Equal(3, TileGridPanel.GetColumn(tile));
            Assert.Equal(2, TileGridPanel.GetRow(tile));
        });
    }

    [Fact]
    public void MeasureAndArrange_PlaceTilesOnAbsoluteCells()
    {
        Sta.Run(() =>
        {
            var panel = new TileGridPanel { CellWidth = 100, CellHeight = 50 };
            var atOrigin = new Border();
            var atTwoOne = new Border();
            TileGridPanel.SetColumn(atTwoOne, 2);
            TileGridPanel.SetRow(atTwoOne, 1);
            panel.Children.Add(atOrigin);
            panel.Children.Add(atTwoOne);

            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            // Desired size = obsah (3 sloupce × 2 řady), ne available - přesah řeší scroll.
            Assert.Equal(new Size(300, 100), panel.DesiredSize);

            panel.Arrange(new Rect(panel.DesiredSize));

            Assert.Equal(new Point(0, 0), atOrigin.TranslatePoint(new Point(0, 0), panel));
            Assert.Equal(new Point(200, 50), atTwoOne.TranslatePoint(new Point(0, 0), panel));
        });
    }
}
