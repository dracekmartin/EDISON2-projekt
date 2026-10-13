using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace EDControls.EDInputForm;

/// <summary>
/// Converter pro Image field náhled - dekóduje Base64 string (PNG/JPG) na
/// <see cref="BitmapImage"/>. Null / prázdný / neplatný input → null (Image se nezobrazí).
/// Jednosměrný. Obrázek se načítá celý do paměti (CacheOption.OnLoad), aby se neuzamkl stream.
/// </summary>
internal class Base64ToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string b64 || string.IsNullOrWhiteSpace(b64))
            return null;

        try
        {
            byte[] bytes = System.Convert.FromBase64String(b64);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException("Base64ToImageSourceConverter is one-way only");
}
