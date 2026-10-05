using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDList;

/// <summary>COLOR hodnota EDListu <c>"R;G;B"</c> → brush; neplatná = průhledná.</summary>
internal class RgbStringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            var parts = s.Split(';');
            if (parts.Length == 3
                && byte.TryParse(parts[0], out byte r)
                && byte.TryParse(parts[1], out byte g)
                && byte.TryParse(parts[2], out byte b))
                return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
