using SkiaSharp;

namespace TemplateDesigner.Models;

/// <summary>
/// Jediné místo, kde se čte uživatelská barva ve tvaru <c>#AARRGGBB</c> / <c>#RRGGBB</c>.
///
/// <para><b>Proč to nesmí jít přes <c>SKColor.Parse</c>:</b> ta na neplatném vstupu <b>vyhodí
/// výjimku</b>, a barvy jdou z formuláře jako volný text (uživatel může hex vymazat nebo přepsat).
/// Padalo to až při kreslení, tedy daleko od místa zadání - a protože se neplatná hodnota uložila
/// do šablony, hodilo to výjimku i při každém dalším otevření (šablona vypadala jako rozbitá).
/// Kreslení proto barvu vždy jen <b>zkusí</b> přečíst a na neplatné vstupní hodnotě sáhne po
/// záložní barvě (fail-soft) - výstup může být na pohled jinak barevný, ale nikdy nespadne.</para>
///
/// <para>Zabránit tomu, aby se neplatná hodnota vůbec uložila, je věc vstupní kontroly
/// (<c>ElementFormBinder</c> u formulářových polí, <c>PropertiesPanel</c> u panelu).</para>
///
/// <para><b>Čtení je záměrně shovívavější než zadávání:</b> tady projde i zkrácený zápis
/// (<c>#ARGB</c>, <c>#RGB</c>), protože cílem je přečíst, co v šabloně je. Vstupní pole naopak
/// vyžaduje plný <c>#RRGGBB</c> / <c>#AARRGGBB</c> - u zadávání je předvídatelnost přednější.</para>
/// </summary>
public static class ColorHex
{
    /// <summary>Neprůhledná černá - rozumná záloha pro text, čáry a rámečky.</summary>
    public const string Black = "#FF000000";

    /// <summary>Průhledná - rozumná záloha pro pozadí (nic se nenakreslí).</summary>
    public const string Transparent = "#00FFFFFF";

    /// <summary>Přečte barvu; false = prázdná nebo neplatná hodnota. Barvu z dat
    /// (<see cref="IsDataBound"/>) rozloží podle dat právě kreslené stránky.</summary>
    public static bool TryParse(string? hex, out SKColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var value = hex.Trim();
        if (IsDataBound(value))
        {
            color = ResolveDataBound(value);
            return true;
        }
        return SKColor.TryParse(value, out color);
    }

    /// <summary>True pro hodnotu, kterou lze přečíst jako barvu (prázdná = false).</summary>
    public static bool IsValid(string? hex) => TryParse(hex, out _);

    /// <summary>Barva, nebo <paramref name="fallback"/> u prázdné/neplatné hodnoty. Nikdy nevyhodí výjimku.</summary>
    public static SKColor Parse(string? hex, SKColor fallback) =>
        TryParse(hex, out var color) ? color : fallback;

    /// <summary>Barva, nebo záložní barva zapsaná hexem (typicky konstanta v kódu).</summary>
    public static SKColor Parse(string? hex, string fallbackHex) =>
        TryParse(hex, out var color) ? color : Parse(fallbackHex, SKColors.Transparent);

    /// <summary>Barva, nebo <c>null</c> u prázdné/neplatné hodnoty - pro nepovinné barvy, kde
    /// „nenastaveno" už něco znamená (střídání řádků, zvýraznění): neplatná hodnota se pak chová
    /// stejně jako nezadaná, tedy featura se prostě nepoužije.</summary>
    public static SKColor? ParseOrNull(string? hex) => TryParse(hex, out var color) ? color : null;

    /// <summary>Barva, nebo černá u prázdné/neplatné hodnoty (text, čáry, rámečky).</summary>
    public static SKColor ParseOrBlack(string? hex) => Parse(hex, SKColors.Black);

    // ── Barva z dat („barva linky") ───────────────────────────────────────────

    /// <summary>Barva, na kterou padne nevyřešená barva z dat - linka barvu nastavenou nemá, nebo
    /// se kreslí bez dat (návrhář, čtvereček v dialogu). Bílá záměrně: podbarvení tak zůstane
    /// čitelné a nevypadá jako chyba.</summary>
    public const string UnresolvedDataColor = "#FFFFFFFF";

    /// <summary>
    /// Hodnota, která místo barvy nese <b>jedno datové pole</b> (<c>{BARVA_LINKY}</c>) - barvu určí
    /// až data konkrétní stránky. Schválně jen samotný token, ne text s dírou: barva je buď pevná,
    /// nebo z dat. Míchanina (<c>#FF33{X}</c>) tak zůstane neplatnou hodnotou.
    /// </summary>
    public static bool IsDataBound(string? hex)
    {
        var value = hex?.Trim();
        if (value is null || value.Length < 3 || value[0] != '{' || value[^1] != '}') return false;

        for (int i = 1; i < value.Length - 1; i++)
            if (value[i] is '{' or '}') return false;
        return true;
    }

    // Data právě kreslené stránky. Barvy se čtou z property elementu bez přístupu k datům
    // (SkBorderColor, ResolvedTextStyle…), takže je kreslení na dobu vykreslení sekce zveřejní tady.
    // ThreadStatic kvůli exportu, který kreslí na jiném vlákně než UI.
    [ThreadStatic] private static DataContext? _scopeData;

    /// <summary>Zveřejní data pro rozklad barev z dat na dobu kreslení. Volá kreslicí cesta
    /// (<c>PreviewCanvas.RenderPage</c>); mimo ni se barva z dat nevyřeší a padne na
    /// <see cref="UnresolvedDataColor"/>.</summary>
    public static IDisposable DataScope(DataContext? data) => new Scope(data);

    private sealed class Scope : IDisposable
    {
        private readonly DataContext? _previous;
        public Scope(DataContext? data) { _previous = _scopeData; _scopeData = data; }
        public void Dispose() => _scopeData = _previous;
    }

    private static SKColor ResolveDataBound(string token)
    {
        var resolved = _scopeData?.ResolvePlaceholders(token);

        // Bez dat / neznámé pole → ResolvePlaceholders vrátí token beze změny; prázdná hodnota
        // znamená „linka barvu nemá". Oboje končí na bílé.
        if (string.IsNullOrWhiteSpace(resolved) || resolved == token)
            return Parse(UnresolvedDataColor, SKColors.White);

        return Parse(resolved, SKColors.White);
    }

    /// <summary>Barva, nebo průhledná u prázdné/neplatné hodnoty (pozadí, výplně).</summary>
    public static SKColor ParseOrTransparent(string? hex) => Parse(hex, SKColors.Transparent);
}
