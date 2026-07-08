namespace TemplateDesigner.Models;

/// <summary>
/// String identifikátory sloupců tabulkových JŘ elementů (<c>StopColumn.Key</c>). Řídí, kterou
/// renderovací logiku sloupec spustí (switch <c>ValueFor</c> / <c>DefaultHeaderFor</c> / <c>GetCellText</c>)
/// a jak se popisuje v editoru sloupců. NENÍ to název datového pole (to je kontrakt Caché agendy) —
/// je to interní směrovací klíč renderu.
///
/// Hodnoty se persistují do JSON šablony (<c>StopColumn.Key</c> v DB/souborech) — <b>neměnit bez migrace</b>.
/// Přejmenování hodnoty tiše rozbije uložené šablony (sloupec zůstane prázdný). Zamčeno testem
/// <c>ColumnKeysTests</c>.
/// </summary>
public static class ColumnKeys
{
    // ─── Sdílené (StopListElement i LineTableElement) ───
    public const string StopName = "StopName";   // název zastávky (+ značky) — slepené z víc polí
    public const string Tc       = "Tc";         // tarifní číslo
    public const string Zone     = "Zone";       // tarifní zóna/zóny

    // ─── LineTableElement (linkový JŘ) ───
    public const string Km   = "Km";     // kilometráž (expanduje se na N sub-sloupců)
    public const string Mhd  = "Mhd";    // MHD/IDS sloupec (auto-hide když prázdné)
    public const string Trip = "Trip";   // sloupce časů spojů (vznikají z dat, řídí šířku/styl)

    // ─── StopListElement (zastávkový JŘ) ───
    public const string TravelTime   = "TravelTime";   // čas jízdy (příp. rozvinut na varianty)
    public const string RouteLetters = "RouteLetters"; // průběhy trasy (písmena rozdílů)
    public const string BaseRoute    = "BaseRoute";    // základní trasa (ZT)

    /// <summary>Prefix variantních časových sub-sloupců (<c>"TravelTime#0"</c>, <c>"TravelTime#1"</c>, …),
    /// generovaných za běhu při více variantách jízdních dob. Nepersistuje se v default sloupcích.</summary>
    public const string TravelTimeVariantPrefix = "TravelTime#";
}
