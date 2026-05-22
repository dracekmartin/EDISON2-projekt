using System;
using System.Windows;
// Aliasy proti ambient System.Windows.Forms / System.Drawing (UseWindowsForms je zapnuté pro FontDialog).
using Application = System.Windows.Application;
using FontFamily  = System.Windows.Media.FontFamily;

namespace EDISONMAIN2;

/// <summary>
/// Vypočtené font nastavení z user preferencí. Oba <c>null</c> = nepoužít user override,
/// fallback na default tokeny v <c>EDTheme.xaml</c>.
/// </summary>
public record FontSettings(string? FontFamily, double? FontSize);

/// <summary>
/// Aplikuje user font preference (z <see cref="LocalPrefs"/>) overridem tokenů
/// <c>EDTheme.FontFamily</c> a <c>EDTheme.FontSize.*</c> v <see cref="Application.Resources"/>.
/// Všechny styly v <c>EDControlStyles.xaml</c> používají <c>DynamicResource</c> → změna tokenu
/// se okamžitě propaguje do všech ovládacích prvků (Menu, ComboBox, Button, ...) v aplikaci.
///
/// <see cref="ComputeFromPrefs"/> je čistá funkce (testovatelná) — vrací co user
/// nastavil. <see cref="Apply"/> overridne tokeny v <c>Application.Resources</c>;
/// null pref → <c>Remove()</c> token (fallback na static default z <c>EDTheme.xaml</c>).
/// </summary>
public static class FontService
{
    // Čerstvá kopie EDTheme.xaml (ne sdílená s Application.Resources, takže user override
    // tokenů ji neovlivní) — slouží jako autoritativní zdroj default font sizes pro scaling.
    // Když kolega upraví defaulty v EDTheme.xaml, scaling tady se přizpůsobí automaticky.
    private static readonly ResourceDictionary ThemeDefaults = new()
    {
        Source = new Uri("pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml"),
    };

    private static readonly string[] FontSizeKeys =
        ["EDTheme.FontSize.Small", "EDTheme.FontSize.Caption", "EDTheme.FontSize.Body",
         "EDTheme.FontSize.Header", "EDTheme.FontSize.Large"];

    /// <summary>
    /// Vypočte settings z prefs. <c>FontApplied=true</c> a non-empty <see cref="LocalPrefs.FontFamily"/>
    /// vrátí ne-null hodnoty; cokoli jiného vrátí oba null (= nepoužít user override).
    /// </summary>
    public static FontSettings ComputeFromPrefs(LocalPrefs prefs)
    {
        if (prefs.FontApplied && !string.IsNullOrEmpty(prefs.FontFamily))
            return new FontSettings(prefs.FontFamily, prefs.FontSize);
        return new FontSettings(null, null);
    }

    /// <summary>
    /// Aplikuje settings z prefs overridem tokenů v <see cref="Application.Resources"/>.
    /// Volá se po startupu a po user change (toggle Apply font / Select font). Idempotentní.
    /// </summary>
    public static void Apply(LocalPrefs prefs)
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
}
