using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace TemplateDesigner.Controls;

/// <summary>
/// Sdílený levý seznam sloupců pro tabulkové editory (StopList, DepartureGrid).
/// Položky implementují <see cref="IColumnRowVM"/>; přesun ▲▼ deleguje na příkazy z host VM.
/// Detail vybraného sloupce zůstává v hostujícím dialogu (vpravo).
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

/// <summary>Šířka mm → text „X mm" pro seznam; null/prázdné = „" (sloupec bez pevné šířky = auto).</summary>
public class NullableMmToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is float w ? $"{w:0.#} mm" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
