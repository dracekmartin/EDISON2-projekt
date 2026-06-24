namespace TemplateDesigner.Rendering;

/// <summary>
/// Jednotná sémantika nadpisu sloupce/skupiny pro VŠECHNY tabulkové komponenty
/// (StopList, DepartureGrid, TableElement): autorský override z šablony přebíjí zdrojový
/// výchozí text; prázdný/whitespace override → zdroj (žádná regrese).
///
/// Záměrně jediné místo definující tento koncept — komponenty se liší pouze tím, ČÍM je zdroj
/// (statický label sloupce vs. hodnota z dat, např. název skupiny dnů), ne tím, jak override
/// funguje. Tím se logika neimplementuje per komponenta zvlášť.
/// </summary>
public static class ColumnHeader
{
    /// <summary>Vrátí efektivní text záhlaví: <paramref name="authoredOverride"/> pokud je
    /// neprázdný, jinak <paramref name="sourceDefault"/> (statický label / hodnota z dat).</summary>
    public static string Resolve(string? authoredOverride, string sourceDefault) =>
        string.IsNullOrWhiteSpace(authoredOverride) ? sourceDefault : authoredOverride;
}
