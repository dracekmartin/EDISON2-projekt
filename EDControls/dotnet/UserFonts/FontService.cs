using System;
using System.Windows;
using System.Windows.Threading;
using FontFamily = System.Windows.Media.FontFamily;

namespace EDControls.UserFonts;

/// <summary>
/// Vypočtené font nastavení z user preferencí. Oba <c>null</c> = nepoužít user override,
/// fallback na default tokeny v <c>EDTheme.xaml</c>. Internal — implementační detail pro testy.
/// </summary>
internal record FontSettings(string? FontFamily, double? FontSize);

/// <summary>
/// Aplikuje user font preference overridem tokenů <c>EDTheme.FontFamily</c> a
/// <c>EDTheme.FontSize.*</c> v <see cref="Application.Resources"/>. Všechny styly
/// v <c>EDControlStyles.xaml</c> používají <c>DynamicResource</c> → změna tokenu se
/// okamžitě propaguje do všech ovládacích prvků v aplikaci.
///
/// <see cref="Initialize"/> wirduje cross-process propagaci: shell zapíše do shared
/// font.json přes <see cref="UserFontPreference.Save"/>, ostatní procesy (TemplateDesigner)
/// dostanou notifikaci přes FileSystemWatcher → marshal na UI thread → re-Apply.
/// </summary>
public static class FontService
{
    // Čerstvá kopie EDTheme.xaml (ne sdílená s Application.Resources, takže user override
    // tokenů ji neovlivní) — slouží jako autoritativní zdroj default font sizes pro scaling.
    // Když někdo upraví defaulty v EDTheme.xaml, scaling tady se přizpůsobí automaticky.
    private static readonly ResourceDictionary ThemeDefaults = new()
    {
        Source = new Uri("pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml"),
    };

    private static readonly string[] FontSizeKeys =
        ["EDTheme.FontSize.Small", "EDTheme.FontSize.Caption", "EDTheme.FontSize.Body",
         "EDTheme.FontSize.Header", "EDTheme.FontSize.Large"];

    /// <summary>
    /// Vypočte settings z prefs. <c>FontApplied=true</c> a non-empty <see cref="UserFontPreference.FontFamily"/>
    /// vrátí ne-null hodnoty; cokoli jiného vrátí oba null (= nepoužít user override).
    /// Internal — implementační detail vystavený testům přes <c>InternalsVisibleTo</c>.
    /// </summary>
    internal static FontSettings ComputeFromPrefs(UserFontPreference prefs)
    {
        if (prefs.FontApplied && !string.IsNullOrEmpty(prefs.FontFamily))
            return new FontSettings(prefs.FontFamily, prefs.FontSize);
        return new FontSettings(null, null);
    }

    /// <summary>
    /// Aplikuje settings z prefs overridem tokenů v <see cref="Application.Resources"/>.
    /// Volá se po startupu a po user change (toggle Apply font / Select font). Idempotentní —
    /// opakované volání se stejnou hodnotou je no-op.
    /// </summary>
    public static void Apply(UserFontPreference prefs)
    {
        var resources = Application.Current.Resources;
        var settings = ComputeFromPrefs(prefs);

        if (settings.FontFamily is not null)
            resources["EDTheme.FontFamily"] = new FontFamily(settings.FontFamily);
        else
            resources.Remove("EDTheme.FontFamily");

        if (settings.FontSize is { } body)
        {
            // Scaling: user dává Body, ostatní proporcionálně (zachová UI ratio Small:Caption:...:Large)
            // dle defaultů v EDTheme.xaml — žádná duplicita konstant.
            var defaultBody = (double)ThemeDefaults["EDTheme.FontSize.Body"];
            foreach (var key in FontSizeKeys)
                resources[key] = body * (double)ThemeDefaults[key] / defaultBody;
        }
        else
        {
            foreach (var key in FontSizeKeys)
                resources.Remove(key);
        }
    }

    /// <summary>
    /// Jednorázový bootstrap pro hostitelskou aplikaci: načte shared user font preference,
    /// aplikuje na EDTheme tokeny a spustí cross-process listening (FileSystemWatcher → když
    /// shell upraví font, ostatní procesy okamžitě re-Apply přes Dispatcher). Volat v App
    /// startup **před** Show prvního okna (eliminuje flicker z default na user font při
    /// prvním paintu). Použij ve všech m-line WPF nástrojích co chtějí respektovat sdílenou
    /// user font preferenci.
    /// </summary>
    public static void Initialize(Application app)
    {
        UserFontPreference.Load();
        Apply(UserFontPreference.Current);
        UserFontPreference.BeginListening();
        UserFontPreference.Changed += () =>
            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                () => Apply(UserFontPreference.Current));
    }
}
