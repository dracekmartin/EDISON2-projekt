using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls.EDInfoBar;

/// <summary>
/// Nenápadný informační proužek: ikona (volitelná) + text + volitelná akce (odkaz). Slouží
/// jako lehké upozornění „něco není vidět / dělej X" (např. pod stromem modulů, když uživatel
/// nemá otevřenou miniaplikaci). Vzhled = implicitní styl/template v EDControlStyles.xaml;
/// prázdný <see cref="ActionText"/> akci schová. Viditelnost proužku řídí konzument.
/// </summary>
public sealed class EDInfoBar : Control
{
    static EDInfoBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EDInfoBar), new FrameworkPropertyMetadata(typeof(EDInfoBar)));
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(EDInfoBar), new PropertyMetadata(""));

    /// <summary>Zobrazené sdělení.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(EDInfoBar), new PropertyMetadata(null));

    /// <summary>Volitelná ikona vlevo; null → jen text.</summary>
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(EDInfoBar), new PropertyMetadata(""));

    /// <summary>Popisek akčního odkazu vpravo. Prázdný → akce se nezobrazí.</summary>
    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EDInfoBar), new PropertyMetadata(null));

    /// <summary>Příkaz spuštěný kliknutím na akci.</summary>
    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }
}
