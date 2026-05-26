using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDViewModel;

/// <summary>
/// Hex barva (<c>"#RRGGBB"</c> nebo <c>"#AARRGGBB"</c>) → <see cref="SolidColorBrush"/>.
/// Prázdná hodnota nebo neplatný formát → <see cref="DependencyProperty.UnsetValue"/> = WPF
/// použije výchozí hodnotu z theme stylu (např. <c>EDTheme.Text.Primary</c>).
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return DependencyProperty.UnsetValue;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
