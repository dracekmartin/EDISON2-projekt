using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Limity dělení sloupce s pruhem podpory a plochou miniaplikací. Potřebuje živý layout
/// (ActualHeight/ActualWidth), takže STA vlákno + Measure/Arrange.
/// </summary>
public class ShellSplitLimitsTests
{
    private const double SupportFloor = 400;
    private const double SplitterHeight = 5;
    private const double MenuHeight = 28;
    private const double StatusBarHeight = 26;
    private const double MinDockHeight = 220;
    private const double MinDockWidth = 540;
    private const double TreeFloor = 150;

    [Fact]
    public void SupportCeiling_LeavesDockItsMinimum()
    {
        Sta.Run(() =>
        {
            var l = MeasuredRows(supportHeight: SupportFloor, totalHeight: 900);

            l.ApplyCeiling();

            // Sloupec dostal 900 - menu - statusbar, dělič z toho bere 5, ploše musí zůstat 220.
            Assert.Equal(900 - MenuHeight - StatusBarHeight - SplitterHeight - MinDockHeight,
                         l.Support.MaxHeight, precision: 3);

            // Uživatel zatáhne za dělič nad strop → render se o strop zastaví.
            l.Support.Height = new GridLength(880);
            l.Relayout();
            l.ApplyCeiling();
            l.Relayout();
            Assert.True(l.Dock.ActualHeight >= MinDockHeight,
                        $"Plocha spadla na {l.Dock.ActualHeight}.");
        });
    }

    [Fact]
    public void SupportCeiling_NeverDropsBelowFloor()
    {
        Sta.Run(() =>
        {
            // Nízké okno: na pruh i plochu dohromady nezbyde ani podlaha + minimum plochy.
            var l = MeasuredRows(supportHeight: SupportFloor, totalHeight: 500);

            l.ApplyCeiling();

            Assert.Equal(SupportFloor, l.Support.MaxHeight, precision: 3);
        });
    }

    [Fact]
    public void SupportCeiling_KeepsUserHeight_WhenWindowShrinksAndGrowsBack()
    {
        Sta.Run(() =>
        {
            var l = MeasuredRows(supportHeight: 700, totalHeight: 1000);
            l.ApplyCeiling();
            l.Relayout();
            Assert.Equal(700, l.Support.ActualHeight, precision: 3);

            // Menší monitor - strop pruh srazí, ale zvolenou výšku nesmí přepsat.
            l.Resize(700);
            l.ApplyCeiling();
            l.Relayout();
            Assert.Equal(700, l.Support.Height.Value, precision: 3);
            Assert.Equal(MinDockHeight, l.Dock.ActualHeight, precision: 3);
            Assert.True(l.Support.ActualHeight < 700, "Strop měl pruh srazit.");

            // Zpátky na velký - pruh se sám vrátí na zvolenou výšku.
            l.Resize(1000);
            l.ApplyCeiling();
            l.Relayout();
            Assert.Equal(700, l.Support.ActualHeight, precision: 3);
        });
    }

    [Fact]
    public void SupportCeiling_SecondPassKeepsSameCeiling()
    {
        Sta.Run(() =>
        {
            // Strop mění výšky obou řádků, takže by se na SizeChanged mohl zacyklit.
            var l = MeasuredRows(supportHeight: 880, totalHeight: 900);

            l.ApplyCeiling();
            l.Relayout();
            double first = l.Support.MaxHeight;

            l.ApplyCeiling();
            l.Relayout();

            Assert.Equal(first, l.Support.MaxHeight, precision: 3);
        });
    }

    [Fact]
    public void SupportCeiling_OversizedSupport_LeavesContentRowAllocationIntact()
    {
        Sta.Run(() =>
        {
            // Předpoklad, na kterém strop stojí: star řádek drží přidělenou výšku, i když
            // ho vnořený Grid přetéká. Vnitřní Grid v tom stavu naopak vrací větší ActualHeight.
            var l = MeasuredRows(supportHeight: 5000, totalHeight: 900);

            Assert.Equal(900 - MenuHeight - StatusBarHeight, l.ContentRow.ActualHeight, precision: 3);
        });
    }

    [Fact]
    public void SupportCeiling_BeforeFirstLayout_DoesNothing()
    {
        var support = new RowDefinition { Height = new GridLength(SupportFloor), MinHeight = SupportFloor };
        var splitter = new RowDefinition { Height = new GridLength(SplitterHeight) };

        ShellSplitLimits.ApplySupportCeiling(support, splitter, availableHeight: 0, MinDockHeight);

        Assert.Equal(double.PositiveInfinity, support.MaxHeight);
    }

    [Fact]
    public void SplitterDrag_StopsOnCeilingAndFloor()
    {
        // Jediný test se živým oknem: tažení děliče jde vyvolat jen jeho klávesovou obsluhou
        // a ta potřebuje PresentationSource. Zamyká, že GridSplitter meze řádku respektuje -
        // na tom stojí volba stropu místo clampu po dragu.
        var log = new List<(double Support, double Dock)>();
        Sta.RunOnAppThread(() =>
        {
            var l = MeasuredRows(supportHeight: SupportFloor, totalHeight: 900);
            var splitter = new GridSplitter
            {
                Height = SplitterHeight,
                ResizeDirection = GridResizeDirection.Rows,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Grid.SetRow(splitter, 1);
            l.Column.Children.Add(splitter);

            // Mimo viditelnou plochu a bez chrome - test nesmí blikat přes obrazovku.
            var window = new Window
            {
                Content = l.Root, Width = 1000, Height = 900,
                WindowStyle = WindowStyle.None, ShowInTaskbar = false, Left = -3000,
            };
            window.Show();
            Nudge(window, l, splitter, Key.Down, 200);   // zvětšovat pruh, dokud to jde
            log.Add((l.Support.ActualHeight, l.Dock.ActualHeight));
            Nudge(window, l, splitter, Key.Up, 400);     // a zpátky až na podlahu
            log.Add((l.Support.ActualHeight, l.Dock.ActualHeight));
            window.Close();
        });

        var atCeiling = log[0];
        Assert.Equal(MinDockHeight, atCeiling.Dock, precision: 3);
        Assert.True(atCeiling.Support > SupportFloor, "Pruh se vůbec nezvětšil.");
        Assert.Equal(SupportFloor, log[1].Support, precision: 3);
    }

    // Jeden stisk = jeden krok děliče + přepočet stropu, stejně jako to dělá SizeChanged v okně.
    private static void Nudge(Window window, Layout l, GridSplitter splitter, Key key, int times)
    {
        splitter.Focus();
        for (int i = 0; i < times; i++)
        {
            splitter.RaiseEvent(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(splitter), 0, key)
            {
                RoutedEvent = Keyboard.KeyDownEvent,
            });
            window.UpdateLayout();
            l.ApplyCeiling();
            window.UpdateLayout();
        }
    }

    [Fact]
    public void ClampTreeWidth_NarrowDock_ShrinksTreeByDeficit()
    {
        Sta.Run(() =>
        {
            var (_, tree, dock) = MeasuredColumns(treeWidth: 400, totalWidth: 900);

            ShellSplitLimits.ClampTreeWidth(tree, dock.ActualWidth, MinDockWidth);

            // Plocha měla 495 (900 - 400 - 5), schodek 45 jde ze stromu.
            Assert.Equal(355, tree.Width.Value, precision: 3);
        });
    }

    [Fact]
    public void ClampTreeWidth_NeverDropsBelowTreeMinimum()
    {
        Sta.Run(() =>
        {
            var (_, tree, dock) = MeasuredColumns(treeWidth: 400, totalWidth: 600);

            ShellSplitLimits.ClampTreeWidth(tree, dock.ActualWidth, MinDockWidth);

            Assert.Equal(TreeFloor, tree.Width.Value, precision: 3);
        });
    }

    [Fact]
    public void ClampTreeWidth_DockAboveMinimum_LeavesTreeStarSized()
    {
        Sta.Run(() =>
        {
            var (_, tree, dock) = MeasuredColumns(treeWidth: 300, totalWidth: 1400);

            ShellSplitLimits.ClampTreeWidth(tree, dock.ActualWidth, MinDockWidth);

            Assert.Equal(300, tree.Width.Value, precision: 3);
        });
    }

    // Dvouúrovňový layout jako v shellu: root Auto/star/Auto, ve star řádku vnořený Grid
    // s pruhem podpory (px) / děličem (px) / plochou (*).
    private static Layout MeasuredRows(double supportHeight, double totalHeight)
    {
        var support = new RowDefinition { Height = new GridLength(supportHeight), MinHeight = SupportFloor };
        var splitter = new RowDefinition { Height = new GridLength(SplitterHeight) };
        var column = new Grid();
        column.RowDefinitions.Add(support);
        column.RowDefinitions.Add(splitter);
        column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var dock = new Border();
        column.Children.Add(Placed(new Border(), row: 0));
        column.Children.Add(Placed(new Border(), row: 1));
        column.Children.Add(Placed(dock, row: 2));

        var contentRow = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
        var root = new Grid { Width = 1000, Height = totalHeight };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(contentRow);
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(Placed(new Border { Height = MenuHeight }, row: 0));
        Grid.SetRow(column, 1);
        root.Children.Add(column);
        root.Children.Add(Placed(new Border { Height = StatusBarHeight }, row: 2));

        Relayout(root);
        return new Layout(root, column, contentRow, support, splitter, dock);
    }

    private sealed record Layout(Grid Root, Grid Column, RowDefinition ContentRow,
                                 RowDefinition Support, RowDefinition Splitter, FrameworkElement Dock)
    {
        public void ApplyCeiling() =>
            ShellSplitLimits.ApplySupportCeiling(Support, Splitter, ContentRow.ActualHeight, MinDockHeight);

        public void Relayout() => ShellSplitLimitsTests.Relayout(Root);

        public void Resize(double height)
        {
            Root.Height = height;
            Relayout();
        }
    }

    // Tři sloupce jako v shellu: strom (*) / dělič (5) / plocha (3*).
    private static (Grid Grid, ColumnDefinition Tree, FrameworkElement Dock) MeasuredColumns(
        double treeWidth, double totalWidth)
    {
        var tree = new ColumnDefinition { Width = new GridLength(treeWidth), MinWidth = TreeFloor };
        var grid = new Grid { Width = totalWidth, Height = 800 };
        grid.ColumnDefinitions.Add(tree);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

        var dock = new Border();
        grid.Children.Add(PlacedColumn(new Border(), column: 0));
        grid.Children.Add(PlacedColumn(new Border { Width = 5 }, column: 1));
        grid.Children.Add(PlacedColumn(dock, column: 2));
        Relayout(grid);
        return (grid, tree, dock);
    }

    private static Border Placed(Border element, int row)
    {
        Grid.SetRow(element, row);
        return element;
    }

    private static Border PlacedColumn(Border element, int column)
    {
        Grid.SetColumn(element, column);
        return element;
    }

    internal static void Relayout(Grid grid)
    {
        grid.Measure(new Size(grid.Width, grid.Height));
        grid.Arrange(new Rect(0, 0, grid.Width, grid.Height));
        grid.UpdateLayout();
    }
}
