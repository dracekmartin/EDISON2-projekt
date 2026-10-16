namespace TemplateDesigner.Models;

/// <summary>
/// String identifikátory sloupců <b>linkového</b> JŘ (<see cref="Elements.LineTableElement"/>,
/// <c>StopColumn.Key</c>). Řídí, kterou renderovací logiku sloupec spustí a jak se popisuje v editoru.
/// NENÍ to název datového pole - je to interní směrovací klíč renderu.
///
/// <para>Zastávkový JŘ (<see cref="Elements.StopListElement"/>) tuto sadu <b>nepoužívá</b> - jeho sloupce
/// jsou klíčované přímo field ID kolekce ZASTAVKY (<see cref="StopFields"/>), aby byly 1:1 s poli, která
/// nabízí DynamicTable. Linkový JŘ má vlastní datový tvar (km, spoje) → sdílet kontrakt se zastávkovým
/// nemusí.</para>
///
/// <para>Hodnoty se persistují do JSON šablony (<c>StopColumn.Key</c> v DB/souborech) - <b>neměnit bez
/// migrace</b>. Přejmenování hodnoty tiše rozbije uložené šablony. Zamčeno testem <c>ColumnKeysTests</c>.</para>
/// </summary>
public static class ColumnKeys
{
    // ─── LineTableElement (linkový JŘ) ───
    public const string StopName = "StopName";   // název zastávky (+ značky) - slepené z víc polí
    public const string Tc       = "Tc";         // tarifní číslo
    public const string Zone     = "Zone";       // tarifní zóna/zóny
    public const string Km       = "Km";         // kilometráž (expanduje se na N sub-sloupců)
    public const string Mhd      = "Mhd";        // MHD/IDS sloupec (auto-hide když prázdné)
    public const string Trip     = "Trip";       // sloupce časů spojů (vznikají z dat, řídí šířku/styl)
    public const string Arrow    = "Arrow";      // šipka směru jízdy - kreslí se vektorově, nemá text
    // Rozdílový JŘ (`B192e`): Tč a pásmo původní verze linky. Data je nesou, jen když se liší.
    public const string OriginalTc   = "OriginalTc";
    public const string OriginalZone = "OriginalZone";
}
