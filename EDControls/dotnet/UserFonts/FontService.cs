using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using FontFamily = System.Windows.Media.FontFamily;

namespace EDControls.UserFonts;

/// <summary>
/// Vypočtené font nastavení z user preferencí. <c>FontSize</c> je už v px (WPF FontSize),
/// prefs jsou v pt. Oba <c>null</c> = nepoužít user override, fallback na default tokeny
/// v <c>EDTheme.xaml</c>. Internal - implementační detail pro testy.
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
    // tokenů ji neovlivní) - slouží jako autoritativní zdroj default font sizes pro scaling.
    // Když někdo upraví defaulty v EDTheme.xaml, scaling tady se přizpůsobí automaticky.
    private static readonly ResourceDictionary ThemeDefaults = new()
    {
        Source = new Uri("pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml"),
    };

    private const string FontSizePrefix = "EDTheme.FontSize.";
    private const string IconSizePrefix = "EDTheme.IconSize.";

    // WPF FontSize je v device-independent px (1/96"), user prefs (a legacy EDISON,
    // a každý font picker) mluví v pt (1/72"). Konverze žije jen tady - jinde se
    // nesmí objevit, jinak se past vrátí.
    internal const double PtToPx = 96.0 / 72.0;

    // Rozsah smysluplné Body velikosti v pt. Chrání proti ručně editovanému font.json
    // (velikost 200 nebo 0 = nepoužitelné UI bez cesty zpět přes menu).
    internal const double MinBodyPt = 7;
    internal const double MaxBodyPt = 24;

    // Tokeny škálované proporcionálně k user Body size - font velikosti i rozměry ikon.
    // Odvozeno z EDTheme.xaml (jediný zdroj pravdy): nový FontSize.* / IconSize.* se propíše
    // bez ruční editace tady. Pořadí nehraje roli: každý klíč se škáluje nezávisle vůči Body.
    private static readonly string[] ScaledSizeKeys = ThemeDefaults.Keys
        .OfType<string>()
        .Where(k => k.StartsWith(FontSizePrefix, StringComparison.Ordinal)
                 || k.StartsWith(IconSizePrefix, StringComparison.Ordinal))
        .ToArray();

    /// <summary>
    /// Vypočte settings z prefs. <c>FontApplied=true</c> a non-empty <see cref="UserFontPreference.FontFamily"/>
    /// vrátí ne-null hodnoty; cokoli jiného vrátí oba null (= nepoužít user override).
    /// Velikost se převádí z pt na px a clampuje na <see cref="MinBodyPt"/>..<see cref="MaxBodyPt"/>.
    /// Internal - implementační detail vystavený testům přes <c>InternalsVisibleTo</c>.
    /// </summary>
    internal static FontSettings ComputeFromPrefs(UserFontPreference prefs)
    {
        if (prefs.FontApplied && !string.IsNullOrEmpty(prefs.FontFamily))
            return new FontSettings(prefs.FontFamily, ToPx(prefs.FontSize));
        return new FontSettings(null, null);
    }

    private static double? ToPx(double? sizePt) =>
        sizePt is { } pt ? ClampBodyPt(pt) * PtToPx : null;

    private static double ClampBodyPt(double sizePt) => Math.Clamp(sizePt, MinBodyPt, MaxBodyPt);

    /// <summary>
    /// User override velikosti **v pt** po clampu, nebo <c>null</c> když override neplatí.
    /// Dialog z toho staví předvolbu i řádek „Aktuálně" - musí ukazovat, co je opravdu vidět,
    /// ne surovou hodnotu z <c>font.json</c>.
    /// </summary>
    internal static double? OverrideBodyPt(UserFontPreference prefs) =>
        prefs.FontApplied && prefs.FontSize is { } pt ? ClampBodyPt(pt) : null;

    /// <summary>
    /// Vyvolá se po každém <see cref="Apply"/> - pokrývá in-process změnu (menu Styl)
    /// i cross-process (FileSystemWatcher → Dispatcher → Apply), vždy na UI threadu.
    /// Pro obsah mimo dosah WPF tokenů (WebView2 zoom apod.).
    /// </summary>
    public static event Action? Applied;

    /// <summary>Aktuální efektivní Body velikost **v px** - user override, jinak default z EDTheme.xaml.</summary>
    public static double BodySize => ComputeBodySize(UserFontPreference.Current);

    /// <summary>
    /// Výchozí Body velikost **v pt** - odvozená z tokenu v <c>EDTheme.xaml</c>, aby se
    /// číslo neduplikovalo v UI. Dialog nastavení fontu z ní staví popisek „(výchozí)".
    /// </summary>
    public static double DefaultBodyPt => DefaultBodyPx / PtToPx;

    private static double DefaultBodyPx => (double)ThemeDefaults["EDTheme.FontSize.Body"];

    internal static double ComputeBodySize(UserFontPreference prefs) =>
        ComputeFromPrefs(prefs).FontSize ?? DefaultBodyPx;

    /// <summary>
    /// Aplikuje settings z prefs overridem tokenů v <see cref="Application.Resources"/>.
    /// Volá se po startupu a po user change (toggle Apply font / Select font). Idempotentní -
    /// opakované volání se stejnou hodnotou je no-op.
    /// </summary>
    internal static void Apply(UserFontPreference prefs)
    {
        var resources = Application.Current.Resources;
        var settings = ComputeFromPrefs(prefs);

        if (settings.FontFamily is not null)
            resources["EDTheme.FontFamily"] = new FontFamily(settings.FontFamily);
        else
            resources.Remove("EDTheme.FontFamily");

        if (settings.FontSize is { } body)
        {
            // Scaling: user dává Body, ostatní fonty i ikony proporcionálně (zachová UI ratio
            // dle defaultů v EDTheme.xaml - žádná duplicita konstant).
            foreach (var key in ScaledSizeKeys)
                resources[key] = body * (double)ThemeDefaults[key] / DefaultBodyPx;
        }
        else
        {
            foreach (var key in ScaledSizeKeys)
                resources.Remove(key);
        }

        Applied?.Invoke();
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
