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
/// <para><b>Priorita:</b> embedded rodina → OS-nainstalovaná rodina → šířková varianta
/// („Arial Narrow", viz níže) → <b>Arial</b> (předvídatelný Helvetica-like default) → Skia default.
/// Tichá náhrada je tak aspoň konzistentní.</para>
///
/// <para><b>Šířkové varianty:</b> DirectWrite sdružuje rodiny po „šířka-váha-sklon", takže úzké
/// varianty <b>nejsou</b> samostatné rodiny - Arial Narrow je řez rodiny Arial se šířkou Condensed
/// a v <c>GetFontFamilies()</c> vůbec není. Uživatel je přitom zná jako samostatný font (tak je
/// nabízí Word i nastavení Windows), proto je do <see cref="InstalledFamilies"/> přidáváme pod
/// jménem odvozeným z reálného názvu řezu v OS a při resolve je překládáme zpět na rodinu + šířku.
/// Bez toho by se na jméno „Arial Narrow" tiše kreslil úplně jiný font (Skia na neznámou rodinu
/// nefallbackuje na příbuznou).</para>
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

    // ── Šířkové varianty rodin („Arial Narrow" = Arial + Condensed) ──────────────

    /// <summary>Rodina v OS + šířka (<see cref="SKFontStyleWidth"/>), na které se šířková varianta překládá.</summary>
    private readonly record struct WidthVariant(string Family, int Width);

    private static readonly Lazy<Dictionary<string, WidthVariant>> _widthVariants = new(LoadWidthVariants);
    private static Dictionary<string, WidthVariant> WidthVariants => _widthVariants.Value;

    // Slova popisující šířku, jak je OS píše v názvech řezů - anglicky i v české lokalizaci
    // (váhy jsou lokalizované („tučné"), šířky ne). Delší varianty první: „Extra Condensed"
    // musí vyhrát nad „Condensed".
    private static readonly string[] WidthWords =
    [
        "UltraCondensed", "Ultra Condensed", "ExtraCondensed", "Extra Condensed", "Ext Condensed",
        "SemiCondensed", "Semi Condensed", "Condensed", "Narrow",
        "UltraExpanded", "Ultra Expanded", "ExtraExpanded", "Extra Expanded",
        "SemiExpanded", "Semi Expanded", "Expanded",
    ];

    private static Dictionary<string, WidthVariant> LoadWidthVariants()
    {
        var map = new Dictionary<string, WidthVariant>(StringComparer.OrdinalIgnoreCase);

        foreach (var family in OsFamilies)
        {
            try
            {
                using var set = SKFontManager.Default.GetFontStyles(family);

                // Per šířka jedno jméno - všechny řezy téže šířky nesou totéž slovo („Narrow",
                // „Narrow Bold", „Narrow Italic"), stačí první nalezené.
                var wordByWidth = new Dictionary<int, string>();
                for (int i = 0; i < set.Count; i++)
                {
                    int width = set[i].Width;
                    if (!wordByWidth.ContainsKey(width)) wordByWidth[width] = WidthWord(set.GetStyleName(i), width);
                }

                // Jednošířková rodina nepotřebuje odvozené jméno, i když je úzká (Impact, Agency FB) -
                // vybere ji samo jméno rodiny a „Impact Condensed" by byl jen duplikát téhož fontu.
                if (wordByWidth.Count < 2) continue;

                // Na holé jméno rodiny dá OS šířku nejbližší normální; pro tu se jméno neodvozuje.
                // Druhé kritérium jen kvůli determinismu u rodiny bez normální šířky (např. jen
                // polo-úzká + polo-rozšířená) - jinak by výběr závisel na pořadí řezů v OS.
                int defaultWidth = wordByWidth.Keys
                    .OrderBy(w => Math.Abs(w - (int)SKFontStyleWidth.Normal)).ThenBy(w => w).First();

                foreach (var (width, word) in wordByWidth)
                {
                    if (width == defaultWidth) continue;
                    var name = $"{family} {word}";
                    if (OsFamilies.Contains(name)) continue;   // skutečná rodina téhož jména má přednost
                    map.TryAdd(name, new WidthVariant(family, width));
                }
            }
            catch { /* nečitelná rodina přeskočit - nesmí shodit nabídku fontů */ }
        }
        return map;
    }

    /// <summary>Slovo popisující šířku - z názvu řezu v OS, jinak kanonické podle číselné šířky.</summary>
    private static string WidthWord(string? styleName, int width)
    {
        if (!string.IsNullOrEmpty(styleName))
            foreach (var word in WidthWords)
                if (styleName.Contains(word, StringComparison.OrdinalIgnoreCase)) return word;

        return width switch
        {
            1 => "Ultra Condensed", 2 => "Extra Condensed", 3 => "Condensed", 4 => "Semi Condensed",
            6 => "Semi Expanded", 7 => "Expanded", 8 => "Extra Expanded", 9 => "Ultra Expanded",
            _ => "Condensed",
        };
    }

    /// <summary>
    /// Rozloží název rodiny na (rodina v OS, šířka jejích řezů). Šířková varianta („Arial Narrow")
    /// → rodina + její šířka; běžná rodina → jméno beze změny a šířka <c>null</c>. Potřebuje to
    /// i nabídka řezů (<see cref="FontWeightCatalog"/>), aby u úzké varianty nabídla jen váhy, které
    /// v té šířce existují. <c>null</c> znamená „šířku neomezovat" - rodina může být úzká sama
    /// o sobě (Agency FB) a filtr na normální šířku by jí nabídku vyprázdnil.
    /// </summary>
    public static (string Family, int? Width) ResolveFamilyAndWidth(string? family)
    {
        var f = family?.Trim() ?? "";
        return f.Length > 0 && !OsFamilies.Contains(f) && WidthVariants.TryGetValue(f, out var v)
            ? (v.Family, v.Width)
            : (f, null);
    }

    // ── Dostupné rodiny (OS + embedded + šířkové varianty) ───────────────────────

    private static HashSet<string>? _osFamilies;

    /// <summary>Rodiny hlášené OS (bez embedded a bez šířkových variant) - ty mají při resolve přednost.</summary>
    private static HashSet<string> OsFamilies => _osFamilies ??=
        new(SKFontManager.Default.GetFontFamilies(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string>? _families;

    /// <summary>Dostupné font rodiny (OS-nainstalované + embedded + šířkové varianty), seřazené -
    /// pro nabídku v UI. Cachované.</summary>
    public static IReadOnlyList<string> InstalledFamilies => _families ??=
        OsFamilies
            .Concat(Embedded.Keys)
            .Concat(WidthVariants.Keys)
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
    /// na znamení, automat = „{"…) podle jazyka šablony. Embedded font (<c>Fonts/TimeTable2*.ttf</c>).
    /// <para>
    /// ⚠️ **SK font NENÍ jen jiné ztvárnění týchž kódů** (změřeno 4. 8. 2026 obrysy z obou fontů;
    /// dřívější tvrzení „znaky jsou pro CZ i SK stejné" bylo nepravdivé). Kódy, které dnes
    /// překládáme, sedí v obou (`z` autobus, `x` vlak, `L` kolo - u `z` a `L` je obrys dokonce
    /// identický), ale **`w` je v CZ železniční vagon a v SK autobus** a **`D` je v CZ křížek
    /// a v SK dlaň**. Nová položka <c>SymbolGlyph</c> se proto musí ověřit v **obou** fontech,
    /// jinak se vzhled rozejde podle jazyka šablony. Postup v <c>elementy-architektura.md</c>.
    /// </para>
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

        // Skutečná rodina z OS před šířkovou variantou téhož jména.
        if (!string.IsNullOrEmpty(family) && OsFamilies.Contains(family))
            return SKTypeface.FromFamilyName(family, style) ?? SKTypeface.Default;

        // Šířková varianta („Arial Narrow" → Arial + Condensed). Váhu a sklon si drží z kaskády,
        // šířku dostane z varianty - jinak by vyšel obyčejně široký řez.
        if (!string.IsNullOrEmpty(family) && WidthVariants.TryGetValue(family, out var v))
            return SKTypeface.FromFamilyName(v.Family, new SKFontStyle(style.Weight, v.Width, style.Slant))
                ?? SKTypeface.Default;

        return SKTypeface.FromFamilyName("Arial", style) ?? SKTypeface.Default;
    }
}
