namespace TemplateDesigner.Models;

/// <summary>
/// Mapování „druhu" agendy (<c>AgendaRegistryEntry.ElementKind</c>) na JŘ typy elementů, které okno
/// nabízí v kontextovém menu a paletě. <b>Jediné místo UI mapování druh→typy</b> — Caché deklaruje
/// jen business druh ("STOP"/"LINE"/""), C# vlastní, které konkrétní elementy k němu patří. Nová
/// agenda existujícího druhu = nula změn tady; nový druh (nová rodina elementů) = jeden case.
/// </summary>
public static class AgendaCapabilities
{
    private static readonly string[] StopTypes =
        [ElementTypes.StopList, ElementTypes.DepartureGrid, ElementTypes.NotesLegend];
    private static readonly string[] LineTypes = [ElementTypes.LineTable];

    /// <summary>JŘ typy elementů podporované daným druhem agendy. Prázdný/neznámý druh
    /// (GENERAL / fakturace / bez JŘ kontextu) → žádné JŘ elementy — ty patří jen ke své agendě.</summary>
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
