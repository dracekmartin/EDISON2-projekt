using System;
using System.Collections.Generic;
using System.Linq;

namespace EDControls.UserFonts;

/// <summary>
/// Nabídka fontů pro dialog nastavení. Rodiny jsou úmyslně **whitelist**, ne výčet
/// nainstalovaných fontů: systémový picker nabízel i symbolové fonty (Wingdings, Marlett)
/// a uživatel, který si takový font nastaví, přijde o čitelné UI včetně cesty zpátky.
/// Logika je oddělená od WPF, aby šla testovat bez <c>Application</c>.
/// </summary>
internal static class FontCatalog
{
    /// <summary>Výchozí rodina - shodná s tokenem <c>EDTheme.FontFamily</c>, drží ji i legacy EDISON.</summary>
    public const string DefaultFamily = "Segoe UI";

    /// <summary>Povolené rodiny v pořadí, v jakém se nabízejí. Bezpatkové UI fonty dodávané s Windows / Office.</summary>
    public static readonly IReadOnlyList<string> AllowedFamilies = new[]
    {
        DefaultFamily, "Segoe UI Semibold", "Calibri", "Verdana", "Tahoma", "Arial",
    };

    private static readonly double[] OfferedSizesPt = { 9, 10, 11, 12, 14, 16, 18 };

    /// <summary>
    /// Rodiny k nabídnutí: whitelist protnutý skutečně nainstalovanými. <paramref name="current"/>
    /// mimo whitelist (starší font.json, ruční edit) se přidá na konec - combo má ukazovat
    /// pravdu a potvrzení dialogu nesmí volbu mlčky přepsat.
    /// </summary>
    internal static IReadOnlyList<string> Families(IEnumerable<string> installed, string? current)
    {
        var present = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
        var result = AllowedFamilies.Where(present.Contains).ToList();

        // Segoe UI je fallback i kdyby ho enumerace nevrátila - bez něj by dialog neměl co nabídnout.
        if (result.Count == 0) result.Add(DefaultFamily);

        if (!string.IsNullOrEmpty(current)
            && !result.Contains(current, StringComparer.OrdinalIgnoreCase))
            result.Add(current);

        return result;
    }

    /// <summary>Je rodina na whitelistu? Neřeší, jestli je na stroji nainstalovaná.</summary>
    internal static bool IsAllowed(string? family) =>
        family is not null && AllowedFamilies.Contains(family, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Velikosti v pt k nabídnutí, včetně <paramref name="defaultPt"/> (typicky necelé -
    /// plyne z px tokenu) a <paramref name="current"/>, setříděné a bez duplicit.
    /// <paramref name="current"/> mimo nabídku (ruční edit font.json) se přidá ze stejného
    /// důvodu jako u rodin - combo nesmí tvrdit něco jiného, než co je aplikované.
    /// </summary>
    internal static IReadOnlyList<double> SizesPt(double defaultPt, double? current = null) =>
        OfferedSizesPt.Append(defaultPt).Concat(current is { } c ? [c] : Array.Empty<double>())
            .Distinct().OrderBy(s => s).ToList();

    /// <summary>Nainstalované rodiny fontů dle WPF. Tenký wrapper - celá logika je v <see cref="Families"/>.</summary>
    internal static IEnumerable<string> InstalledFamilies() =>
        System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source);
}
