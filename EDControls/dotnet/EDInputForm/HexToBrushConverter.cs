using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDInputForm;

/// <summary>
/// Converter pro Color field swatch — parsuje hex string `"#AARRGGBB"` (SkiaSharp formát)
/// na <see cref="SolidColorBrush"/>. Neplatný / null input → transparent brush.
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;

        string h = hex.TrimStart('#');
        // Podpora #AARRGGBB (8 znaků) a #RRGGBB (6 znaků)
        byte a = 0xFF, r, g, b;
        try
        {
            if (h.Length == 8)
            {
                a = System.Convert.ToByte(h.Substring(0, 2), 16);
                r = System.Convert.ToByte(h.Substring(2, 2), 16);
                g = System.Convert.ToByte(h.Substring(4, 2), 16);
                b = System.Convert.ToByte(h.Substring(6, 2), 16);
            }
            else if (h.Length == 6)
            {
                r = System.Convert.ToByte(h.Substring(0, 2), 16);
                g = System.Convert.ToByte(h.Substring(2, 2), 16);
                b = System.Convert.ToByte(h.Substring(4, 2), 16);
            }
            else
            {
                return Brushes.Transparent;
            }
        }
        catch { return Brushes.Transparent; }

        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException("HexToBrushConverter is one-way only");
}
