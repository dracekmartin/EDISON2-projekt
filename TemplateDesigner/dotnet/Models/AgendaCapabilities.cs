namespace TemplateDesigner.Models;

/// <summary>
/// Mapování „druhu" agendy (<c>AgendaRegistryEntry.ElementKind</c>) na JŘ typy elementů, které okno
/// nabízí v kontextovém menu a paletě. <b>Jediné místo UI mapování druh→typy</b> - Caché deklaruje
/// jen business druh ("STOP"/"LINE"/""), C# vlastní, které konkrétní elementy k němu patří. Nová
/// agenda existujícího druhu = nula změn tady; nový druh (nová rodina elementů) = jeden case.
/// </summary>
public static class AgendaCapabilities
{
    private static readonly string[] StopTypes =
        [ElementTypes.StopList, ElementTypes.DepartureGrid, ElementTypes.NotesLegend];

    /// <summary>
    /// Linkový (knižní) jízdní řád: tabulka zastávky × spoje a **prvek Poznámky**.
    /// </summary>
    /// <remarks>
    /// ⚠️ Poznámky přibyly 3. 9. (`B117`, záměr Jan). Do té doby si uživatel legendu linkového JŘ
    /// skládal **dynamickou tabulkou** nad kolekcí POZNAMKY, takže nedostal ani inverzní box
    /// negativních značek, ani řazení podle typu - jen sloupce textu. Rozhodnutí, proč to tak bylo,
    /// nikde nebylo; vzniklo to tak, že to bylo nejsnazší.
    ///
    /// <para>Prvek nad linkovými daty funguje, protože kontrakt poznámek je stejný: agenda posílá
    /// TYP `nz` / `znak` / `text`, což prvek mapuje na negativní značku, speciální znak a text.
    /// ⚠️ Filtry na **kategorie** (bezbariér, na znamení, přestupy, písmena variant) zůstanou
    /// u linkových nečinné - to jsou zastávkové pojmy a linkový posílá kategorii prázdnou.
    /// Nastavení „počet poznámek na řádek per druh", které má starý modul, prvek zatím neumí
    /// vůbec (`#7487`).</para>
    /// </remarks>
    private static readonly string[] LineTypes =
        [ElementTypes.LineTable, ElementTypes.NotesLegend];

    /// <summary>JŘ typy elementů podporované daným druhem agendy. Prázdný/neznámý druh
    /// (GENERAL / fakturace / bez JŘ kontextu) → žádné JŘ elementy - ty patří jen ke své agendě.</summary>
    public static IReadOnlyList<string> ScheduleElementTypes(string? elementKind) =>
        elementKind?.ToUpperInvariant() switch
        {
            "STOP" => StopTypes,
            "LINE" => LineTypes,
            _      => [],
        };

    /// <summary>Podporuje daný druh agendy tento typ elementu? (řízení menu/palety).</summary>
    public static bool Supports(string? elementKind, string elementType) =>
        ScheduleElementTypes(elementKind).Contains(elementType);
}
