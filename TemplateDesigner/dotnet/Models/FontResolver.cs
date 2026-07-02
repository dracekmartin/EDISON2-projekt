using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace TemplateDesigner.Models;

/// <summary>
/// Centrální resolver fontů. Jediný zdroj pravdy „jaké fonty jsou k dispozici" (z <see cref="SKFontManager"/>
/// — tentýž zdroj, který renderer reálně umí vykreslit) a „jak vyřešit typeface".
///
/// <para>Klíčové: když požadovaná rodina NENÍ nainstalovaná, padne na <b>Arial</b> (předvídatelný
/// Helvetica-like default), ne na náhodný systémový default Skie. Tím je tichá náhrada aspoň
/// konzistentní a blízká očekávání. Seznam i set se počítají jednou a cachují.</para>
/// </summary>
public static class FontResolver
{
    private static IReadOnlyList<string>? _families;

    /// <summary>Nainstalované font rodiny, seřazené (pro nabídku v UI). Cachované.</summary>
    public static IReadOnlyList<string> InstalledFamilies => _families ??=
        SKFontManager.Default.GetFontFamilies()
            .Distinct()
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static HashSet<string>? _set;
    private static HashSet<string> Set => _set ??= new(InstalledFamilies, StringComparer.OrdinalIgnoreCase);

    /// <summary>True pokud je rodina nainstalovaná (case-insensitive, ořez okrajů).</summary>
    public static bool IsInstalled(string? family) =>
        !string.IsNullOrWhiteSpace(family) && Set.Contains(family.Trim());

    /// <summary>
    /// Vyřeší typeface pro rodinu + styl. Nenainstalovaná / prázdná rodina → Arial → Skia default.
    /// Volající musí typeface uvolnit (dispose).
    /// </summary>
    public static SKTypeface Resolve(string? family, SKFontStyle style)
    {
        var f = family?.Trim();
        if (string.IsNullOrEmpty(f) || !Set.Contains(f)) f = "Arial";
        return SKTypeface.FromFamilyName(f, style) ?? SKTypeface.Default;
    }
}
