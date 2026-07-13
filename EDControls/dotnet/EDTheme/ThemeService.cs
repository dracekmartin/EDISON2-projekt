using System;
using System.Windows;
using System.Windows.Controls;

namespace EDControls.EDTheme;

public enum AppTheme { Light, Dark }

/// <summary>
/// Přepíná světlý/tmavý režim za běhu. Tmavá varianta = <c>EDThemeDark.xaml</c> přidaná na
/// konec <c>Application.Resources.MergedDictionaries</c> (přebije barevné brush klíče z
/// EDTheme.xaml pro DynamicResource lookup). Návrat na světlý = odebrání této dictionary.
/// Funguje jen pro barvy konzumované přes <c>DynamicResource</c> (EDControlStyles i EDTheme to dělají).
/// </summary>
public static class ThemeService
{
    private const string DarkUri = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";
    private static ResourceDictionary? _darkOverride;
    private static bool _initialized;

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>
    /// Zaregistruje class handler na <see cref="FrameworkElement.LoadedEvent"/> pro
    /// <see cref="Window"/> — chytá i všechny podtřídy (implicitní styl by cílil jen přesný
    /// typ <c>Window</c>). Každému oknu bez vlastního pozadí nastaví <c>Bg.Base</c> přes
    /// <c>SetResourceReference</c> (drží DynamicResource → přeblikne při swapu tématu).
    /// Volat jednou při startu aplikace, před zobrazením prvního okna.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window w) return;
        // Respektuj okna s vlastním inline pozadím / popředím; jinak napoj na theme tokeny.
        if (w.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
            w.SetResourceReference(Control.BackgroundProperty, "EDTheme.Bg.Base");
        if (w.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
            w.SetResourceReference(Control.ForegroundProperty, "EDTheme.Text.Primary");
    }

    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        _darkOverride ??= new ResourceDictionary { Source = new Uri(DarkUri) };
        var dicts = app.Resources.MergedDictionaries;

        if (theme == AppTheme.Dark)
        {
            if (!dicts.Contains(_darkOverride)) dicts.Add(_darkOverride);
        }
        else
        {
            dicts.Remove(_darkOverride);
        }
        Current = theme;
    }
}
