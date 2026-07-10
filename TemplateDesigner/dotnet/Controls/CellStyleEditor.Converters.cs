using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Controls;

/// <summary>Vrátí Visible pokud je hodnota null nebo prázdný string, jinak Collapsed.</summary>
public sealed class NullOrEmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return Visibility.Visible;
        if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Vrátí Visible pokud hodnota NENÍ null a (u stringů) není prázdná, jinak Collapsed.</summary>
public sealed class NotNullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return Visibility.Collapsed;
        if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Multi-value converter: první non-null / non-empty string z hodnot → Brush.
/// Používá se na color swatch pro zobrazení efektivní barvy (explicit ?? inherited).
/// </summary>
public sealed class FirstNonEmptyHexToBrushConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var v in values)
        {
            if (v is string s && !string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var sk = SKColor.Parse(s);
                    return new SolidColorBrush(Color.FromArgb(sk.Alpha, sk.Red, sk.Green, sk.Blue));
                }
                catch { /* neparsovatelný hex → zkus další hodnotu v kaskádě, jinak Transparent */ }
            }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Převede TextHorizontalAlignment na český label (pro placeholder zobrazení zděděné hodnoty).</summary>
public sealed class HAlignToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TextHorizontalAlignment.Left   => "Vlevo",
        TextHorizontalAlignment.Center => "Na střed",
        TextHorizontalAlignment.Right  => "Vpravo",
        _ => "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Převede TextVerticalAlignment na český label (pro placeholder zobrazení zděděné hodnoty).</summary>
public sealed class VAlignToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TextVerticalAlignment.Top    => "Nahoru",
        TextVerticalAlignment.Middle => "Na střed",
        TextVerticalAlignment.Bottom => "Dolů",
        _ => "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
