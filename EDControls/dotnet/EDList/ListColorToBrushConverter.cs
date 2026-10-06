using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDList;

/// <summary>COLOR hodnota EDListu (<see cref="ColorValue"/>: hex i legacy <c>R;G;B</c>) → brush; neplatná = průhledná.</summary>
internal class ListColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && ColorValue.TryParse(s, out var color)
            ? new SolidColorBrush(color)
            : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
