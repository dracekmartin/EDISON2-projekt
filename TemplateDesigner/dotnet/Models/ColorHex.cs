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

    /// <summary>Přečte barvu; false = prázdná nebo neplatná hodnota.</summary>
    public static bool TryParse(string? hex, out SKColor color)
    {
        color = default;
        return !string.IsNullOrWhiteSpace(hex) && SKColor.TryParse(hex.Trim(), out color);
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

    /// <summary>Barva, nebo průhledná u prázdné/neplatné hodnoty (pozadí, výplně).</summary>
    public static SKColor ParseOrTransparent(string? hex) => Parse(hex, SKColors.Transparent);
}
