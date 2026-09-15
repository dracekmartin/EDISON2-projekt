using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using EDControls.EDList;
using EDControls.EDViewModel;
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

    private static List NoRestList() => new()
    {
        ServicesOverride = new EDServices(null,
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
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
