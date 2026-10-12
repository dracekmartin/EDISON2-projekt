using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;

// Namespace shodný s code-behind okna (xmlns:local v EdisonMainWindow.xaml).
namespace EDISONMAIN2
{
    /// <summary>
    /// Viditelnost obsahu podle klíče aktivní záložky (<c>ConverterParameter</c> = klíč té které
    /// záložky). Jen pro Používané, ty strom nemají; stromy jdou přes <see cref="ActiveTreeToVisibilityConverter"/>.
    /// </summary>
    public sealed class TabKeyToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Equals(value as string, parameter as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Strom je vidět, když jeho kolekce je strom aktivní záložky (hodnoty: <c>SelectedTab.Tree</c>, kolekce
    /// stromu). Klíč záložky tak v XAML nefiguruje a nová záložka nepotřebuje literál.
    /// </summary>
    public sealed class ActiveTreeToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
            values is [System.Collections.IEnumerable active, var tree] && ReferenceEquals(active, tree)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Klíč resource → hodnota z aplikačních resources (ikony záložek jsou v <c>EDIcons.xaml</c>).
    /// Deskriptor záložky tak nese jen klíč a nemusí do ViewModelu tahat WPF <c>ImageSource</c>.
    /// </summary>
    public sealed class ResourceKeyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string key ? Application.Current?.TryFindResource(key) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
