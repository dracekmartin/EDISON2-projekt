using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EDControls.EDTheme;

public enum AppTheme { Light, Dark }

/// <summary>
/// Přepíná světlý/tmavý režim za běhu. Tmavá varianta = <c>EDThemeDark.xaml</c> přidaná na
/// konec <c>Application.Resources.MergedDictionaries</c> (přebije barevné brush klíče z
/// EDTheme.xaml pro DynamicResource lookup). Návrat na světlý = odebrání této dictionary.
/// Funguje jen pro barvy konzumované přes <c>DynamicResource</c> (EDControlStyles i EDTheme to dělají).
///
/// <see cref="Initialize"/> je jednorázová registrace pro hostitelskou aplikaci (analogie
/// <c>FontService.Initialize</c>): načte sdílenou preferenci (<see cref="UserThemePreference"/>,
/// <c>%APPDATA%\m-line\common\theme.json</c>), aplikuje ji a spustí cross-process propagaci —
/// když jeden proces (shell) přepne dark, ostatní (TemplateDesigner) se přepnou přes FileSystemWatcher.
/// </summary>
public static class ThemeService
{
    private const string DarkUri = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";
    private static ResourceDictionary? _darkOverride;
    private static bool _initialized;

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    /// <summary>Aktuální stav sdílené preference (pro binding přepínače v menu).</summary>
    public static bool IsDark => UserThemePreference.Current.DarkMode;

    /// <summary>Vyvolá se po každém <see cref="Apply"/> (i cross-process propagaci) — pro imperativní
    /// konzumenty mimo DynamicResource pipeline (např. WebView2 obsah). Handlery musí být idempotentní.</summary>
    public static event Action? Applied;

    /// <summary>
    /// Jednorázový bootstrap: class handler na <see cref="FrameworkElement.LoadedEvent"/> pro
    /// <see cref="Window"/> (obarví pozadí i podtříd — implicitní styl cílí jen přesný typ),
    /// načte + aplikuje sdílenou preferenci a spustí watcher pro cross-process propagaci
    /// (jiný proces přepne → tento re-Apply přes Dispatcher). Volat v startupu před prvním oknem.
    /// </summary>
    public static void Initialize(Application app)
    {
        if (_initialized || app is null) return;
        _initialized = true;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));

        UserThemePreference.Load();
        ApplyFromPreference();
        UserThemePreference.BeginListening();
        UserThemePreference.Changed += () =>
            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)ApplyFromPreference);
    }

    /// <summary>Přepne tmavý režim, uloží do sdílené preference (propaguje do ostatních procesů) a aplikuje.</summary>
    public static void SetDarkMode(bool dark)
    {
        UserThemePreference.Current.DarkMode = dark;
        UserThemePreference.Current.Save();
        Apply(dark ? AppTheme.Dark : AppTheme.Light);
    }

    private static void ApplyFromPreference() =>
        Apply(UserThemePreference.Current.DarkMode ? AppTheme.Dark : AppTheme.Light);

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
        Applied?.Invoke();
    }
}
