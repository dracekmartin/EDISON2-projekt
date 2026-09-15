using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EDControls.EDInputForm;
using Form = EDControls.EDInputForm.EDInputForm;
using EdList = EDControls.EDList.EDList;
using Toolbar = EDControls.EDComponentToolbar.EDComponentToolbar;

namespace EDControls.Tests;

/// <summary>
/// Hlavičková část EDInputFormu a její parita s EDListem. Regrese, které to hlídá:
/// vrácení toolbaru dovnitř ScrollVieweru (scrollbar ukrojí pravý konec hledání),
/// rozbití sdílené šířky sloupce po přesunu IsSharedSizeScope a rozejití barvy pruhu.
/// </summary>
public class EDInputFormHeaderTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    /// <summary>Jádro nahlášené vady: uvnitř scrolleru scrollbar překrýval zaoblený
    /// pravý konec search boxu a při scrollování hlavička utekla pryč.</summary>
    [Fact]
    public void Toolbar_LivesOutsideScrollViewer()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm());

            var ancestors = new List<DependencyObject>();
            for (DependencyObject? d = form.Toolbar; d is not null; d = VisualTreeHelper.GetParent(d))
                ancestors.Add(d);

            Assert.Contains(form, ancestors);   // ať test neprojde na nenarostlém stromu
            Assert.DoesNotContain(ancestors.TakeWhile(a => a != form), a => a is ScrollViewer);

            // Field rows naopak ve scrolleru zůstávají.
            Assert.NotEmpty(Descendants<ScrollViewer>(form));
        });
    }

    /// <summary>Field rows jsou ve scrolleru, hlavička nad ním - scope musí přemostit
    /// hranici ScrollVieweru, jinak se popisky rozjedou.</summary>
    [Fact]
    public void ColumnHeader_SharesLabelWidthWithFieldRows()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm());

            var shared = Descendants<Grid>(form)
                .Where(g => g.ColumnDefinitions.Count == 2
                            && g.ColumnDefinitions[0].SharedSizeGroup == "ItemLabel")
                .ToList();

            Assert.True(shared.Count >= 2, $"čekal hlavičku + field rows, našel {shared.Count}");
            Assert.Single(shared.Select(g => g.ColumnDefinitions[0].ActualWidth).Distinct());
        });
    }

    [Fact]
    public void Title_ForwardsToToolbar()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm());
            form.Title = "Detail linky";
            Assert.Equal("Detail linky", form.Toolbar.Title);
        });
    }

    /// <summary>Pruh EDInputFormu a EDListu je týž brandový podklad - hostitel ho
    /// nesmí přebít vlastním Borderem.</summary>
    [Fact]
    public void HeaderBar_HasSameBackgroundAsEDList()
    {
        RunSta(() =>
        {
            var form = Layout(NewForm());
            var list = Layout(new EdList());

            var expected = (Brush)Application.Current!.Resources["EDTheme.Accent.Header"];
            Assert.Equal(expected.ToString(), BarOf(form.Toolbar).ToString());
            Assert.Equal(expected.ToString(), BarOf(list.HeaderToolbar).ToString());
        });
    }

    private static Brush BarOf(Toolbar toolbar) => ((Border)toolbar.Content).Background;

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

    // Styly i sdílená šířka sloupce se vyhodnotí až po layoutu v resource stromu.
    private static T Layout<T>(T control) where T : FrameworkElement
    {
        var root = new Grid();
        root.Children.Add(control);
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        root.UpdateLayout();
        return control;
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
