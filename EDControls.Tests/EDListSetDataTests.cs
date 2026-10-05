using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EDControls.EDList;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDTesting;
using NSubstitute;
using List = EDControls.EDList.EDList;

namespace EDControls.Tests;

/// <summary>
/// Režim „data zvenčí" (<c>SetData</c>): host si data sehnal sám, EDList nesmí sáhnout na Caché.
/// Formátování i typové sort klíče musí projít stejnou cestou jako u <c>LoadList</c>.
/// </summary>
public class EDListSetDataTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    private static readonly ListColumn[] Columns =
    [
        new() { Key = "rz", Label = "Rz", Type = ListColumnType.Text, Sortable = true },
        new() { Key = "mth", Label = "Celkové MTH", Type = ListColumnType.Number, Format = "#,##0.00", Sortable = true },
        new() { Key = "_fore", Visible = false },
    ];

    // Raw hodnoty tak, jak je posílá legacy server: $j() nechává vodicí mezery a desetinnou tečku.
    private static IReadOnlyDictionary<string, string?> Row(string id, string rz, string mth, string? fore = null) =>
        new Dictionary<string, string?> { ["id"] = id, ["rz"] = rz, ["mth"] = mth, ["_fore"] = fore };

    [Fact]
    public void SetData_WithoutMLRestOrCacheClass_FillsRows()
    {
        RunSta(() =>
        {
            var list = NoRestList();

            list.SetData(Columns, [Row("1", "1A2 3456", "  123.45")]);

            Assert.Equal("1", Assert.Single(list.Rows).Id);
        });
    }

    [Fact]
    public void SetData_FormatsNumbersByColumnFormat()
    {
        RunSta(() =>
        {
            var list = NoRestList();

            list.SetData(Columns, [Row("1", "1A2 3456", "  1234.5")]);

            Assert.Equal(1234.5.ToString("#,##0.00", CultureInfo.CurrentCulture), list.Rows[0]["mth"]);
        });
    }

    /// <summary>Bez typových sort klíčů by se „1000" seřadilo před „999" (lexikograficky).</summary>
    [Fact]
    public void SetData_BuildsTypedSortKeys()
    {
        RunSta(() =>
        {
            var list = NoRestList();

            list.SetData(Columns, [Row("1", "A", "1000"), Row("2", "B", "999")]);

            var sorted = list.Rows.OrderBy(r => r, new EDListRowComparer([("mth", false)])).ToList();
            Assert.Equal(["2", "1"], sorted.Select(r => r.Id));
        });
    }

    [Fact]
    public void SetData_RowWithoutId_IsDropped()
    {
        RunSta(() =>
        {
            var list = NoRestList();

            list.SetData(Columns, [Row("", "A", "1"), Row("2", "B", "2")]);

            Assert.Equal("2", Assert.Single(list.Rows).Id);
        });
    }

    /// <summary>Skrytý sloupec se nerenderuje, ale hodnota musí projít přes indexer řádku (CellStyle si ji bindne).</summary>
    [Fact]
    public void SetData_HiddenColumn_NotRenderedButReadable()
    {
        RunSta(() =>
        {
            var list = NoRestList();

            list.SetData(Columns, [Row("1", "A", "1", fore: "#FF0000")]);

            Assert.Equal(["rz", "mth"], list.DataGrid.Columns.Select(c => c.GetValue(FrameworkElement.TagProperty)));
            Assert.Equal("#FF0000", list.Rows[0]["_fore"]);
        });
    }

    /// <summary>Typ sloupce zarovnává (legacy N sloupce jdou vpravo) - bez něj by číslo zůstalo vlevo.</summary>
    [Fact]
    public void SetData_NumberColumn_IsRightAligned()
    {
        RunSta(() =>
        {
            var list = NoRestList();

            list.SetData(Columns, [Row("1", "A", "1")]);

            var column = Assert.IsType<DataGridTextColumn>(list.DataGrid.Columns[1]);
            var setter = Assert.Single(column.ElementStyle.Setters.OfType<Setter>(),
                s => s.Property == FrameworkElement.HorizontalAlignmentProperty);
            Assert.Equal(HorizontalAlignment.Right, setter.Value);
        });
    }

    /// <summary>Přestavba sloupců (SelectionMode) zahodila šipku, ale CustomSort řadil dál.</summary>
    [Fact]
    public void SelectionModeChange_KeepsSortArrowAndOrder()
    {
        RunSta(() =>
        {
            var list = NoRestList(PrefsReturning(SortLayout("mth")));
            list.HostId = "Tests.EDList";
            list.SetData(Columns, [Row("1", "A", "1"), Row("2", "B", "2")]);
            Assert.Equal(ListSortDirection.Descending, ColumnByKey(list, "mth").SortDirection);

            list.SelectionMode = ListSelectionMode.Check;

            Assert.Equal(ListSortDirection.Descending, ColumnByKey(list, "mth").SortDirection);
            Assert.Equal(["2", "1"], list.DataGrid.Items.OfType<EDListRow>().Select(r => r.Id));
        });
    }

    [Fact]
    public void SetData_WithoutSortedColumn_DropsSort()
    {
        RunSta(() =>
        {
            var list = NoRestList(PrefsReturning(SortLayout("mth"), (JsonNode?)null));
            list.HostId = "Tests.EDList";
            list.SetData(Columns, [Row("1", "A", "1"), Row("2", "B", "2")]);

            list.SetData([Columns[0]], [Row("1", "A", "1"), Row("2", "B", "2")]);

            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(list.DataGrid.ItemsSource);
            Assert.Null(view.CustomSort);
        });
    }

    /// <summary>Řazení v paměti se liší od uloženého (debounce nedoběhl, uložení selhalo): po obnově
    /// šipek dorazí uložený layout a šipka ze starého řazení nesmí zůstat svítit.</summary>
    [Fact]
    public void SetData_StoredLayoutWithOtherSort_LeavesOnlyItsArrow()
    {
        RunSta(() =>
        {
            var list = NoRestList(PrefsReturning(SortLayout("rz"), SortLayout("mth")));
            list.HostId = "Tests.EDList";
            list.SetData(Columns, [Row("1", "A", "1"), Row("2", "B", "2")]);

            list.SetData(Columns, [Row("1", "A", "1"), Row("2", "B", "2")]);

            Assert.Null(ColumnByKey(list, "rz").SortDirection);
            Assert.Equal(ListSortDirection.Descending, ColumnByKey(list, "mth").SortDirection);
        });
    }

    /// <summary>Poškozený blob (klíč null) by trefil checkbox sloupec bez Tagu a comparer by spadl na null klíči.</summary>
    [Fact]
    public void SetData_StoredLayoutWithNullKeys_IsIgnored()
    {
        RunSta(() =>
        {
            var broken = JsonNode.Parse("""{"columns":[{"key":null,"width":99}],"sort":[{"key":null,"direction":"desc"}]}""");
            var list = NoRestList(PrefsReturning(broken));
            list.HostId = "Tests.EDList";
            list.SelectionMode = ListSelectionMode.Check;

            list.SetData(Columns, [Row("1", "A", "1"), Row("2", "B", "2")]);

            Assert.Equal(32, list.DataGrid.Columns[0].Width.Value);
            Assert.Null(list.DataGrid.Columns[0].SortDirection);
            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(list.DataGrid.ItemsSource);
            Assert.Null(view.CustomSort);
        });
    }

    private static JsonNode SortLayout(string key) => ColumnLayoutSerializer.Serialize(new ColumnLayout
    {
        Sort = [new SortPref { Key = key, Direction = "desc" }],
    });

    private static IUserPrefsService PrefsReturning(JsonNode? first, params JsonNode?[] next)
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(first), [.. next.Select(Task.FromResult)]);
        return prefs;
    }

    private static DataGridColumn ColumnByKey(List list, string key) =>
        list.DataGrid.Columns.Single(c => c.GetValue(FrameworkElement.TagProperty) as string == key);

    private static List NoRestList(IUserPrefsService? prefs = null) => new()
    {
        ServicesOverride = new EDServices(null,
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>())
        { UserPrefs = prefs ?? new NullUserPrefsService() },
    };

    // EDList.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky;
    // trvalé app vlákno viz EDComponentToolbarFlatTests.RunSta.
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
