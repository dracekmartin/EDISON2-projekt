using System.Windows;
using EDControls.EDList;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Attached property: nacpe legacy tabulku do <see cref="EDList"/>u. Přes binding to nejde,
/// data se do EDListu předávají metodou <c>SetData</c>.
/// </summary>
public static class DashboardListSource
{
    public static readonly DependencyProperty TableProperty = DependencyProperty.RegisterAttached(
        "Table", typeof(DashboardTable), typeof(DashboardListSource),
        new PropertyMetadata(null, OnTableChanged));

    public static DashboardTable? GetTable(DependencyObject d) => (DashboardTable?)d.GetValue(TableProperty);

    public static void SetTable(DependencyObject d, DashboardTable? value) => d.SetValue(TableProperty, value);

    private static void OnTableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EDList list || e.NewValue is not DashboardTable table) return;
        // HostId musí být dřív než data - persistovaný layout se načítá uvnitř SetData.
        list.HostId = table.PrefsKey;
        list.Title = table.Title;
        list.SetData(DashboardListMapper.ToColumns(table), DashboardListMapper.ToRows(table));
    }
}
