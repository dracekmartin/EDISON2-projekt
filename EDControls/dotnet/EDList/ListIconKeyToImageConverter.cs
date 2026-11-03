using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDList;

/// <summary>IMAGE hodnota EDListu = klíč sdílené ikony (<c>EDIcons.MlineLogo</c>) → obrázek; prázdná nebo neznámá
/// hodnota = bez obrázku. Server tak nese jen význam řádku, obrázek drží klient jako všechny ikony.</summary>
internal class ListIconKeyToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } key ? Application.Current?.TryFindResource(key) as ImageSource : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
