using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EDControls.EDInputForm;
using Form = EDControls.EDInputForm.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// Jednosloupcový režim úzkého formuláře. Regrese, které to hlídá: dvousloupcová minima
/// (120+120) v úzkém hostiteli, obsah přetékající přes viewport (ScrollViewer ho musí
/// omezovat) a rozkmitání layoutu na jediném prahu.
/// </summary>
public class EDInputFormNarrowLayoutTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Wide_KeepsLabelAndValueInOneRowOfTwoColumns()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm(), 800);

            Assert.False(form.IsNarrowLayout);
            var row = FieldRows(form).First();
            Assert.Equal(2, row.ColumnDefinitions.Count);
            Assert.Equal(2, row.RowDefinitions.Count);
        });
    }

    [Fact]
    public void Narrow_StacksValueUnderLabelInOneColumn()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm(), 300);

            Assert.True(form.IsNarrowLayout);
            var row = FieldRows(form).First();
            Assert.Empty(row.ColumnDefinitions);
            Assert.Equal(3, row.RowDefinitions.Count);
        });
    }

    /// <summary>Jádro nahlášené vady: úzký formulář se místo zúžení ořízl, protože
    /// ScrollViewer měřil obsah na nekonečné šířce.</summary>
    [Fact]
    public void Narrow_RowFitsIntoViewport()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm(), 300);

            foreach (var row in FieldRows(form))
                Assert.True(row.DesiredSize.Width <= 300,
                    $"řádek chce {row.DesiredSize.Width} px, k dispozici je 300");
        });
    }

    /// <summary>Hystereze: bez ní by hostitel měnící šířku podle obsahu layout rozkmital.</summary>
    [Fact]
    public void ThresholdHasHysteresis()
    {
        RunSta(() =>
        {
            var form = NewForm();
            var root = Host(form, 800);

            Resize(root, 370);
            Assert.True(form.IsNarrowLayout);

            Resize(root, 390);
            Assert.True(form.IsNarrowLayout);    // mezi prahy zůstává, kde byl

            Resize(root, 410);
            Assert.False(form.IsNarrowLayout);

            Resize(root, 390);
            Assert.False(form.IsNarrowLayout);
        });
    }

    /// <summary>Řádek pole je Grid se třemi ContentPresentery (popisek, hodnota, zpráva) -
    /// jiný takový tvar ve formuláři není.</summary>
    private static List<Grid> FieldRows(Form form) =>
        Descendants<Grid>(form)
            .Where(g => g.Children.Count == 3 && g.Children.OfType<ContentPresenter>().Count() == 3)
            .ToList();

    private static Form NewForm()
    {
        var group = new Group { Number = 1, Key = "g1", GroupName = "Linky", IsExpanded = true };
        return new Form
        {
            Items = new List<Item>
            {
                group,
                new Field { Number = 2, Key = "linka", Label = "Číslo linky", ParentGroup = group },
                new Field { Number = 3, Key = "vuz", Label = "RZ vozu", ParentGroup = group },
            },
        };
    }

    private static Form Layout(Form form, double width)
    {
        Host(form, width);
        return form;
    }

    private static Grid Host(Form form, double width)
    {
        var root = new Grid();
        root.Children.Add(form);
        Resize(root, width);
        return root;
    }

    private static void Resize(Grid root, double width)
    {
        root.Measure(new Size(width, 600));
        root.Arrange(new Rect(0, 0, width, 600));
        root.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
