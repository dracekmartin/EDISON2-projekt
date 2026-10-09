using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EDControls.EDList;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDTesting;
using NSubstitute;
using List = EDControls.EDList.EDList;

namespace EDControls.Tests;

/// <summary>
/// Check mode s hledáním: záhlaví i jeho stav pracují jen s řádky, které hledání ukazuje (TD picker linek).
/// Okno netřeba, <c>DataGrid.Items</c> respektuje filtr view i bez <c>Show()</c>.
/// </summary>
public class EDListCheckTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    private static readonly ListColumn[] Columns = [new() { Key = "name", Label = "Linka", Type = ListColumnType.Text }];

    [Fact]
    public void HeaderClick_WithSearch_ChecksOnlyFound_KeepsHiddenChecked()
    {
        RunSta(() =>
        {
            var list = CheckList();
            list.SetChecked("4", true);
            list.SearchText = "12";

            ClickHeader(list);

            Assert.Equal(["1", "2", "4"], list.SelectedRowIds.Order());
        });
    }

    [Fact]
    public void AllFoundChecked_HeaderShowsAll_SearchClearShowsPart()
    {
        RunSta(() =>
        {
            var list = CheckList();
            list.SearchText = "12";

            list.SetChecked("1", true);
            list.SetChecked("2", true);
            Assert.True(Header(list).IsChecked);
            int raised = 0;
            list.CheckedRowsChanged += (_, _) => raised++;

            list.SearchText = "";
            Assert.Null(Header(list).IsChecked);
            Assert.Equal(0, raised);   // hledání výběr nemění
        });
    }

    private static List CheckList()
    {
        var list = new List
        {
            ServicesOverride = new EDServices(null,
                Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>())
            { UserPrefs = new NullUserPrefsService() },
            SelectionMode = ListSelectionMode.Check,
        };
        list.SetData(Columns, [Row("1", "12"), Row("2", "120"), Row("3", "5"), Row("4", "7")]);
        return list;
    }

    private static IReadOnlyDictionary<string, string?> Row(string id, string name) =>
        new Dictionary<string, string?> { ["id"] = id, ["name"] = name };

    private static CheckBox Header(List list) => (CheckBox)list.DataGrid.Columns[0].Header;

    private static void ClickHeader(List list) => Header(list).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    // EDList.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
