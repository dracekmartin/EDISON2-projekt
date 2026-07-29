using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SkiaSharp;

namespace TemplateDesigner.Models;

/// <summary>
/// Centrální resolver fontů. Jediný zdroj pravdy „jaké fonty jsou k dispozici" a „jak vyřešit typeface".
/// Přes tuto třídu teče <b>veškeré</b> řešení fontů (text, buňky tabulek, JŘ elementy) - embedding se
/// tak řeší jen tady a projeví se všude.
///
/// <para><b>Embedded fonty:</b> libovolný `.ttf`/`.otf` přidaný do .csproj jako `&lt;EmbeddedResource&gt;`
/// pod složkou <c>Fonts/</c> se automaticky načte (bez úpravy kódu) a je dostupný podle názvu rodiny
/// - v renderu i v UI výběru fontu. Bundlují se do single-file .exe (bajty v assembly manifestu).
/// Použij font s licencí povolující redistribuci (OFL/Apache - např. Arimo, Liberation Sans);
/// komerční (Helvetica/Arial) se embednout nesmí, spoléhá se na OS.</para>
///
/// <para><b>Priorita:</b> embedded rodina → OS-nainstalovaná rodina → <b>Arial</b> (předvídatelný
/// Helvetica-like default) → Skia default. Tichá náhrada je tak aspoň konzistentní.</para>
///
/// <para><b>Lifetime:</b> vyřešené typefacy jsou <b>sdílené singletony</b> (cachované, app-lifetime).
/// Volající je <b>nesmí</b> disposovat - jen zabalit do `SKFont` (ten typeface nevlastní).</para>
/// </summary>
public static class FontResolver
{
    // ── Embedded fonty (auto-discovery z manifest resources pod "Fonts/") ────────

    // Rodina (case-insensitive) → varianty (regular/bold/italic…), každá nese svůj SKFontStyle.
    private static readonly Lazy<Dictionary<string, List<SKTypeface>>> _embedded = new(LoadEmbeddedFonts);
    private static Dictionary<string, List<SKTypeface>> Embedded => _embedded.Value;

    private static Dictionary<string, List<SKTypeface>> LoadEmbeddedFonts()
    {
        var map = new Dictionary<string, List<SKTypeface>>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(FontResolver).Assembly;

        foreach (var name in asm.GetManifestResourceNames())
        {
            // Konvence: resource pod složkou Fonts/ (v manifestu ".Fonts.") s příponou fontu.
            bool isFont = name.Contains(".Fonts.", StringComparison.OrdinalIgnoreCase)
                && (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
            if (!isFont) continue;

            try
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is null) continue;

                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
                // FromData drží native referenci na SKData → typeface žije po dobu běhu (nedisposujeme).
                var typeface = SKTypeface.FromData(SKData.CreateCopy(bytes));
                if (typeface is null || string.IsNullOrEmpty(typeface.FamilyName)) continue;

                if (!map.TryGetValue(typeface.FamilyName, out var variants))
                    map[typeface.FamilyName] = variants = [];
                variants.Add(typeface);
            }
            catch { /* vadný font ignorovat - nesmí shodit start aplikace */ }
        }
        return map;
    }

    /// <summary>Nejlepší embedded varianta rodiny pro daný styl (přesný sklon + nejbližší váha), nebo null.</summary>
    private static SKTypeface? ResolveEmbedded(string family, SKFontStyle style) =>
        Embedded.TryGetValue(family, out var variants) && variants.Count > 0
            ? variants
                .OrderBy(t => t.FontStyle.Slant == style.Slant ? 0 : 1)
                .ThenBy(t => Math.Abs(t.FontStyle.Weight - style.Weight))
                .First()
            : null;

    // ── Dostupné rodiny (OS + embedded) ──────────────────────────────────────────

    private static IReadOnlyList<string>? _families;

    /// <summary>Dostupné font rodiny (OS-nainstalované + embedded), seřazené - pro nabídku v UI. Cachované.</summary>
    public static IReadOnlyList<string> InstalledFamilies => _families ??=
        SKFontManager.Default.GetFontFamilies()
            .Concat(Embedded.Keys)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static HashSet<string>? _set;
    private static HashSet<string> Set => _set ??= new(InstalledFamilies, StringComparer.OrdinalIgnoreCase);

    /// <summary>True pokud je rodina dostupná (embedded nebo OS; case-insensitive, ořez okrajů).</summary>
    public static bool IsInstalled(string? family) =>
        !string.IsNullOrWhiteSpace(family) && Set.Contains(family.Trim());

    // ── Resolve ───────────────────────────────────────────────────────────────────

    // Vyřešené typefacy sdílené a cachované (app-lifetime) - viz Lifetime v hlavičce třídy.
    private static readonly ConcurrentDictionary<(string Family, int Weight, int Width, SKFontStyleSlant Slant), SKTypeface> _cache = new();

    /// <summary>
    /// Vyřeší <b>sdílený</b> typeface pro rodinu + styl. Priorita: embedded → OS → Arial → Skia default.
    /// Výsledek <b>nedisposovat</b> (sdílený singleton, vlastní ho FontResolver).
    /// </summary>
    public static SKTypeface Resolve(string? family, SKFontStyle style)
    {
        var f = family?.Trim() ?? "";
        return _cache.GetOrAdd((f, style.Weight, style.Width, style.Slant), _ => ResolveUncached(f, style));
    }

    /// <summary>
    /// Rodina symbolového JŘ fontu (piktogramy dopravy: vozíček = „H", bezbariér částečně = „F",
    /// na znamení, automat = „{"…) podle jazyka šablony. Znaky (drátové kódy z Caché) jsou pro CZ i SK
    /// stejné, liší se jen tvary glyfů - SK má vlastní variantu. Embedded font (<c>Fonts/TimeTable2*.ttf</c>).
    /// </summary>
    public static string SymbolFontFamily(System.Globalization.CultureInfo? culture) =>
        string.Equals(culture?.TwoLetterISOLanguageName, "sk", StringComparison.OrdinalIgnoreCase)
            ? "TimeTable_nove_SK"
            : "Timetable_nove";

    private static SKTypeface ResolveUncached(string family, SKFontStyle style)
    {
        // Embedded má přednost - deterministický výstup nezávislý na tom, co je na stroji nainstalované.
        if (!string.IsNullOrEmpty(family) && ResolveEmbedded(family, style) is { } embedded)
            return embedded;

        var name = !string.IsNullOrEmpty(family) && Set.Contains(family) ? family : "Arial";
        return SKTypeface.FromFamilyName(name, style) ?? SKTypeface.Default;
    }
}
