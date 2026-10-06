using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDViewModel;

/// <summary>
/// Barva (<see cref="ColorValue"/>: <c>"#RRGGBB"</c>, <c>"#AARRGGBB"</c>, <c>R;G;B</c>) → <see cref="SolidColorBrush"/>.
/// Prázdná hodnota nebo neplatný formát → <see cref="DependencyProperty.UnsetValue"/> = WPF
/// použije výchozí hodnotu z theme stylu (např. <c>EDTheme.Text.Primary</c>).
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return DependencyProperty.UnsetValue;
        if (ColorValue.TryParse(hex, out var color))
            return new SolidColorBrush(color);
        // Zbytek syntaxe WPF (pojmenované barvy, #RGB) převodník bral i dřív - zachovat.
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
