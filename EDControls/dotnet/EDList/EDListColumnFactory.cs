using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;

namespace EDControls.EDList;

/// <summary>Staví <c>DataGrid</c> sloupce EDListu z definic <see cref="ListColumn"/>.</summary>
internal static class EDListColumnFactory
{
    /// <summary>Checkbox sloupec Check mode; <paramref name="header"/> si view drží kvůli stavu „vše/část/nic".</summary>
    public static DataGridColumn BuildCheckBoxColumn(RoutedEventHandler onRowClick, Action onHeaderClick, out CheckBox header)
    {
        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        checkBox.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
        // OneWay binding: source (EDListRow.IsChecked) → CheckBox (programmatic změny propagují
        // do UI přes INotifyPropertyChanged). User click řeší explicit ClickEvent handler níže -
        // DataGridTemplateColumn.CellTemplate má známé problémy s reverzní cestou TwoWay binding.
        checkBox.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(EDListRow.IsChecked)) { Mode = BindingMode.OneWay });
        checkBox.AddHandler(CheckBox.ClickEvent, onRowClick);

        header = new CheckBox
        {
            ToolTip = "Označit / odznačit vše viditelné",
            IsThreeState = true  // null = mixed state (zobrazí jako čtvereček)
        };
        // Click (ne Checked/Unchecked) fires jen z user akce, ne z programmatic IsChecked.
        // Tím se vyhneme rekurzi: CheckAll → SyncHeaderCheckState → IsChecked set → Click NEFires.
        header.Click += (_, _) => onHeaderClick();

        return new DataGridTemplateColumn
        {
            Header = header,
            Width = new DataGridLength(32),
            CellTemplate = new DataTemplate { VisualTree = checkBox },
            CanUserSort = false,
            CanUserReorder = false,
        };
    }

    /// <summary>Datový sloupec; <c>Tag</c> nese klíč sloupce (mapování zpět při snapshotu layoutu a hledání).</summary>
    public static DataGridColumn BuildDataColumn(ListColumn col, Action<string, string> onLinkClick)
    {
        var column = BuildByType(col, onLinkClick);
        column.SetValue(FrameworkElement.TagProperty, col.Key);
        return column;
    }

    private static DataGridColumn BuildByType(ListColumn col, Action<string, string> onLinkClick)
    {
        var width = col.Width > 0
            ? new DataGridLength(col.Width)
            : new DataGridLength(1, DataGridLengthUnitType.Auto);

        var align = col.Align == ListColumnAlign.Auto ? EDListParsing.DefaultAlign(col.Type) : col.Align;

        if (col.IsLink)
        {
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetBinding(TextBlock.TextProperty, new Binding($"[{col.Key}]"));
            tb.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
            tb.SetValue(TextBlock.CursorProperty, Cursors.Hand);
            // SetResourceReference (= DynamicResource): SystemColors.HotTrackBrush je
            // systémová barva, kterou dark mode nepřepne.
            tb.SetResourceReference(TextBlock.ForegroundProperty, "EDTheme.Accent.Blue");
            if (align == ListColumnAlign.Right)
                tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            else if (align == ListColumnAlign.Center)
                tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            tb.AddHandler(TextBlock.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((sender, _) =>
            {
                if (sender is TextBlock el && el.DataContext is EDListRow row)
                    onLinkClick(row.Id, col.Key);
            }));
            return new DataGridTemplateColumn
            {
                Header = col.Label,
                Width = width,
                CellTemplate = new DataTemplate { VisualTree = tb },
                CanUserSort = col.Sortable,
            };
        }

        if (col.Type == ListColumnType.Color)
        {
            var rect = new FrameworkElementFactory(typeof(Rectangle));
            rect.SetValue(Rectangle.HeightProperty, 16.0);
            rect.SetValue(Rectangle.WidthProperty, 30.0);
            rect.SetBinding(Rectangle.FillProperty,
                new Binding($"[{col.Key}]") { Converter = new RgbStringToBrushConverter() });
            return new DataGridTemplateColumn
            {
                Header = col.Label,
                Width = width,
                CellTemplate = new DataTemplate { VisualTree = rect },
                CanUserSort = col.Sortable,
            };
        }

        var textCol = new DataGridTextColumn
        {
            Header = col.Label,
            Binding = new Binding($"[{col.Key}]"),
            Width = width,
            CanUserSort = col.Sortable,
        };

        if (align != ListColumnAlign.Left)
        {
            var ha = align == ListColumnAlign.Right
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Center;
            // ElementStyle implicitní <Style TargetType="TextBlock"> nahrazuje, nedědí -
            // bez BasedOn by zarovnané sloupce minuly typografii z EDControlStyles.
            textCol.ElementStyle = new Style(typeof(TextBlock))
            {
                BasedOn = Application.Current?.TryFindResource(typeof(TextBlock)) as Style,
                Setters = { new Setter(TextBlock.HorizontalAlignmentProperty, ha) }
            };
        }

        return textCol;
    }
}
