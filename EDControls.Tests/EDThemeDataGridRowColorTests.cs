using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>
/// Implicitní <c>DataGridCell</c> styl si nastavuje <c>Foreground</c> setterem, a ten přebíjí
/// hodnotu děděnou z <c>DataGridRow</c> - <c>DataGrid.RowStyle</c> s Foreground proto nemá efekt
/// (ve stock WPF setter není, proto to legacy vzor dělal přes řádek). Barva řádku musí jít na
/// <c>CellStyle</c>. Test drží obě poloviny tvrzení, aby doc v EDControls/CLAUDE.md nezestaral.
/// </summary>
public class EDThemeDataGridRowColorTests
{
    static EDThemeDataGridRowColorTests() => _ = Application.ResourceAssembly;

    private const string LightUri  = "pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml";
    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    private static readonly Color Marker = Colors.Red;

    [Fact]
    public void DataGridCellStyle_SetsForegroundExplicitly()
    {
        // Příčina pasti. Kdyby setter zmizel, barva z RowStyle by začala prosakovat a legacy
        // barvy navržené na bílé pozadí by se v dark modu staly nečitelnými.
        RunSta(() =>
        {
            var style = (Style)Load(StylesUri)[typeof(DataGridCell)]!;
            Assert.Contains(style.Setters.OfType<Setter>(),
                            s => s.Property == Control.ForegroundProperty);
        });
    }

    [Fact]
    public void RowStyleForeground_DoesNotReachCellText()
    {
        RunSta(() => Assert.NotEqual(Marker, CellForeground(viaRowStyle: true)));
    }

    [Fact]
    public void CellStyleForeground_DoesReachCellText()
    {
        RunSta(() => Assert.Equal(Marker, CellForeground(viaRowStyle: false)));
    }

    // ---------- Helpers ----------

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };

    /// <summary>Vykreslená barva textu první buňky při obarvení přes RowStyle, resp. CellStyle.</summary>
    private static Color CellForeground(bool viaRowStyle)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(Load(LightUri));
        root.Resources.MergedDictionaries.Add(Load(StylesUri));

        var brush = new SolidColorBrush(Marker);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            ItemsSource = new[] { "Praha" },
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Binding = new System.Windows.Data.Binding("Length"),
            Width = 120,
        });

        var target = viaRowStyle ? typeof(DataGridRow) : typeof(DataGridCell);
        var style = new Style(target)
        {
            BasedOn = root.TryFindResource(target) as Style,
            Setters = { new Setter(Control.ForegroundProperty, brush) },
        };
        if (viaRowStyle) grid.RowStyle = style; else grid.CellStyle = style;

        root.Children.Add(grid);
        root.Measure(new Size(400, 400));
        root.Arrange(new Rect(0, 0, 400, 400));
        root.UpdateLayout();

        var cell = Descendants(grid).OfType<DataGridCell>().First();
        return ((SolidColorBrush)cell.Foreground).Color;
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
