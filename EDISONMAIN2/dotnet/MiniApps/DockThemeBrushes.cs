using System.Windows;
using AvalonDock.Themes.VS2013.Themes;
// Alias proti ambient System.Drawing (UseWindowsForms je v projektu zapnuté).
using Brush = System.Windows.Media.Brush;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Přebarvení tab stripu AvalonDocku na tokeny EDTheme. Chrome docku nese barvy balíček
/// VS2013 (accent #007ACC), ale jeho šablona je čte přes DynamicResource, takže se dají
/// přebít zvenčí - vybraná záložka pak vypadá jako selekce ve zbytku aplikace.
/// </summary>
internal static class DockThemeBrushes
{
    /// <summary>
    /// Klíč chromu VS2013 → token EDTheme. Neznámý token se tiše přeskočí, hlídá test.
    /// Pressed stavy křížku zůstávají na VS modré: drží se jen po dobu kliknutí a jejich
    /// bílý glyph je na ní čitelný v obou režimech.
    /// </summary>
    public static readonly IReadOnlyList<(object DockKey, string ThemeKey)> Map =
    [
        // Vybraná záložka v aktivním pane.
        (ResourceKeys.DocumentWellTabSelectedActiveBackground, "EDTheme.Accent.BlueSelected"),
        (ResourceKeys.DocumentWellTabSelectedActiveText,       "EDTheme.Text.Primary"),
        // Křížek na ní - výchozí je bílý pro tmavě modrý podklad a na světlém zmizí.
        (ResourceKeys.DocumentWellTabButtonSelectedActiveGlyph,             "EDTheme.Text.Secondary"),
        (ResourceKeys.DocumentWellTabButtonSelectedActiveHoveredBackground, "EDTheme.Accent.BlueBorder"),
        (ResourceKeys.DocumentWellTabButtonSelectedActiveHoveredBorder,     "EDTheme.Accent.BlueBorder"),
        (ResourceKeys.DocumentWellTabButtonSelectedActiveHoveredGlyph,      "EDTheme.Text.Primary"),

        // Nevybraná záložka pod myší - stejná neutrální šedá jako hover ve zbytku shellu.
        (ResourceKeys.DocumentWellTabUnselectedHoveredBackground, "EDTheme.Bg.Hover"),
        (ResourceKeys.DocumentWellTabUnselectedHoveredText,       "EDTheme.Text.Primary"),
        (ResourceKeys.DocumentWellTabButtonUnselectedTabHoveredGlyph,                   "EDTheme.Text.Secondary"),
        (ResourceKeys.DocumentWellTabButtonUnselectedTabHoveredButtonHoveredBackground, "EDTheme.Bg.Pressed"),
        (ResourceKeys.DocumentWellTabButtonUnselectedTabHoveredButtonHoveredBorder,     "EDTheme.Bg.Pressed"),
        (ResourceKeys.DocumentWellTabButtonUnselectedTabHoveredButtonHoveredGlyph,      "EDTheme.Text.Primary"),
    ];

    /// <summary>
    /// Vloží hodnoty palety přímo do <c>Resources</c> docku - přímé entries přebíjejí
    /// MergedDictionaries, kam si AvalonDock vkládá theme. Hodnota je snapshot (položka
    /// dictionary neumí DynamicResource), takže se musí zavolat znovu po každé změně motivu.
    /// </summary>
    public static void Apply(FrameworkElement dock)
    {
        foreach (var (dockKey, themeKey) in Map)
            if (dock.TryFindResource(themeKey) is Brush brush)
                dock.Resources[dockKey] = brush;
    }
}
