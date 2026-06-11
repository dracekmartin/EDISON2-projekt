using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EDControls.EDComponentToolbar;

/// <summary>int hodnota → Visible (≠0) / Collapsed (=0).</summary>
public class IntZeroToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i != 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>string → Visible (non-empty) / Collapsed (null/empty).</summary>
public class NonEmptyStringToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
