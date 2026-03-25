namespace TemplateDesigner.PdfImport;

/// <summary>
/// Výsledek AI obohacení heuristické detekce JŘ.
/// AI validuje detekci a mapuje nezařazené texty na databázová pole.
/// </summary>
public class EnrichedImportResult
{
    /// <summary>Mapování nezařazených textů na DB pole a sémantické role.</summary>
    public List<TextMapping> TextMappings { get; init; } = [];

    /// <summary>Validace/korekce záhlaví skupin dnů.</summary>
    public List<DayGroupValidation> DayGroupValidations { get; init; } = [];

    /// <summary>Celková validace detekce (potvrzení oblastí, typ JŘ).</summary>
    public DetectionValidation? Validation { get; init; }
}

/// <summary>
/// Mapování jednoho nezařazeného textu na sémantickou roli a DB pole.
/// </summary>
public class TextMapping
{
    /// <summary>Původní text z PDF.</summary>
    public string OriginalText { get; init; } = "";

    /// <summary>Sémantická role: header, title, validity, company, route, decoration, footnote, unknown.</summary>
    public string Role { get; init; } = "unknown";

    /// <summary>Název DB pole z DataSchema. Null = AI si není jistá.</summary>
    public string? DbField { get; init; }

    /// <summary>Text s placeholdery: "Linka {CISLO_LINKY}". Null = nechat statický text.</summary>
    public string? PlaceholderText { get; init; }

    /// <summary>Confidence mapování 0–1. Pod 0.7 se neaplikuje automaticky.</summary>
    public float Confidence { get; init; }
}

/// <summary>
/// Validace jedné skupiny dnů — AI potvrdí nebo opraví záhlaví.
/// </summary>
public class DayGroupValidation
{
    /// <summary>Index skupiny v ScheduleDetectionResult.DayGroups.</summary>
    public int Index { get; init; }

    /// <summary>Původní záhlaví z heuristické detekce.</summary>
    public string OriginalHeader { get; init; } = "";

    /// <summary>Opravené záhlaví. Null = beze změny.</summary>
    public string? CorrectedHeader { get; init; }

    /// <summary>Zda oblast vypadá jako platná skupina dnů.</summary>
    public bool IsValid { get; init; } = true;
}

/// <summary>
/// Celková validace heuristické detekce ze strany AI.
/// </summary>
public class DetectionValidation
{
    /// <summary>StopList oblast je validní zastávkový sloupec.</summary>
    public bool StopListValid { get; init; } = true;

    /// <summary>DepartureGrid oblast je validní minutová mřížka.</summary>
    public bool DepartureGridValid { get; init; } = true;

    /// <summary>Notes oblast je validní legenda poznámek.</summary>
    public bool NotesValid { get; init; } = true;

    /// <summary>Potvrzení/korekce typu JŘ.</summary>
    public string? ScheduleTypeSuggestion { get; init; }

    /// <summary>Poznámky od AI pro debug log.</summary>
    public string? Notes { get; init; }
}
