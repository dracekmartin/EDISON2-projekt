using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace TemplateDesigner.Controls;

/// <summary>
/// Levý seznam sloupců sdílené záložky Sloupce (<see cref="ColumnsTab"/>).
/// Položky implementují <see cref="IColumnRowVM"/>; přesun ▲▼ deleguje na příkazy záložky.
/// </summary>
public partial class ColumnListEditor : UserControl
{
    public ColumnListEditor() => InitializeComponent();

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ColumnListEditor));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(ColumnListEditor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty MoveUpCommandProperty =
        DependencyProperty.Register(nameof(MoveUpCommand), typeof(ICommand), typeof(ColumnListEditor));

    public ICommand? MoveUpCommand
    {
        get => (ICommand?)GetValue(MoveUpCommandProperty);
        set => SetValue(MoveUpCommandProperty, value);
    }

    public static readonly DependencyProperty MoveDownCommandProperty =
        DependencyProperty.Register(nameof(MoveDownCommand), typeof(ICommand), typeof(ColumnListEditor));

    public ICommand? MoveDownCommand
    {
        get => (ICommand?)GetValue(MoveDownCommandProperty);
        set => SetValue(MoveDownCommandProperty, value);
    }
}

/// <summary>Parse/format šířky sloupce pro záložku Sloupce.
/// Přijímá tečku i čárku, InvariantCulture; prázdné/nevalidní/≤0 → null (auto).</summary>
public static class ColumnWidthText
{
    public static float? Parse(string? text) =>
        float.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    public static string Format(float? width) =>
        width is float w ? w.ToString("0.#", CultureInfo.InvariantCulture) : "";
}

/// <summary>Šířka mm → text „X mm" pro seznam; null/prázdné = „" (sloupec bez pevné šířky = auto).</summary>
public class NullableMmToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is float w ? $"{w:0.#} mm" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
