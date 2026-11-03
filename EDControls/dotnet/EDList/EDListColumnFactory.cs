using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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
        // Tím se vyhneme rekurzi: ToggleAllFromHeader → OnCheckedRowsChanged → IsChecked set → Click NEFires.
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
    public static DataGridColumn BuildDataColumn(ListColumn col)
    {
        var column = BuildByType(col);
        column.SetValue(FrameworkElement.TagProperty, col.Key);
        return column;
    }

    /// <summary>Text s ikonou z pole <see cref="ListColumn.IconKey"/> před ním; hledání a řazení dál jdou podle textu
    /// (klíč sloupce v <c>Tag</c>).</summary>
    private static DataGridColumn BuildTextWithIcon(ListColumn col, DataGridLength width)
    {
        var panel = new FrameworkElementFactory(typeof(DockPanel));
        panel.SetValue(DockPanel.LastChildFillProperty, true);

        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.WidthProperty, 16.0);
        image.SetValue(FrameworkElement.HeightProperty, 16.0);
        image.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 6, 0));
        image.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
        image.SetBinding(Image.SourceProperty,
            new Binding($"[{col.IconKey}]") { Converter = new ListIconKeyToImageConverter() });
        panel.AppendChild(image);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding($"[{col.Key}]"));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(text);

        return new DataGridTemplateColumn
        {
            Header = col.Label,
            Width = width,
            CellTemplate = new DataTemplate { VisualTree = panel },
            CanUserSort = col.Sortable,
        };
    }

    private static DataGridColumn BuildByType(ListColumn col)
    {
        var width = col.Width > 0
            ? new DataGridLength(col.Width)
            : new DataGridLength(1, DataGridLengthUnitType.Auto);

        var align = col.Align == ListColumnAlign.Auto ? EDListParsing.DefaultAlign(col.Type) : col.Align;

        if (col.Type == ListColumnType.Color)
        {
            var rect = new FrameworkElementFactory(typeof(Rectangle));
            rect.SetValue(Rectangle.HeightProperty, 16.0);
            rect.SetValue(Rectangle.WidthProperty, 30.0);
            rect.SetBinding(Rectangle.FillProperty,
                new Binding($"[{col.Key}]") { Converter = new ListColorToBrushConverter() });
            return new DataGridTemplateColumn
            {
                Header = col.Label,
                Width = width,
                CellTemplate = new DataTemplate { VisualTree = rect },
                CanUserSort = col.Sortable,
            };
        }

        if (col.Type == ListColumnType.Text && col.IconKey.Length > 0)
            return BuildTextWithIcon(col, width);

        if (col.Type == ListColumnType.Image)
        {
            var image = new FrameworkElementFactory(typeof(Image));
            image.SetValue(FrameworkElement.WidthProperty, 16.0);
            image.SetValue(FrameworkElement.HeightProperty, 16.0);
            image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            image.SetBinding(Image.SourceProperty,
                new Binding($"[{col.Key}]") { Converter = new ListIconKeyToImageConverter() });
            return new DataGridTemplateColumn
            {
                Header = col.Label,
                Width = width,
                CellTemplate = new DataTemplate { VisualTree = image },
                CanUserSort = col.Sortable,
            };
        }

        // Sem padá i rezervovaný CODEBOOK - zobrazí raw hodnotu.
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
