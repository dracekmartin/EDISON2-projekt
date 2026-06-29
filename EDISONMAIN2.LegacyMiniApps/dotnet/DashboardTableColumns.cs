using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Attached property: postaví sloupce <see cref="DataGrid"/>u za běhu z data-driven seznamu názvů
/// (legacy tabulka má proměnný počet sloupců, neznámý v XAML). Buňky se bindují na
/// <c>DashboardRow.Cells[i]</c> dle pořadí sloupce.
/// </summary>
public static class DashboardTableColumns
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
        "Columns", typeof(IReadOnlyList<string>), typeof(DashboardTableColumns),
        new PropertyMetadata(null, OnColumnsChanged));

    public static IReadOnlyList<string>? GetColumns(DependencyObject d) =>
        (IReadOnlyList<string>?)d.GetValue(ColumnsProperty);

    public static void SetColumns(DependencyObject d, IReadOnlyList<string>? value) =>
        d.SetValue(ColumnsProperty, value);

    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;

        grid.Columns.Clear();
        if (e.NewValue is not IReadOnlyList<string> columns) return;

        for (int i = 0; i < columns.Count; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = columns[i],
                Binding = new Binding($"Cells[{i}]"),
            });
        }
    }
}
