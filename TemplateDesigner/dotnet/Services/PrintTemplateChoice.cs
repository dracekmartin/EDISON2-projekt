using ALWA.SYS;

namespace TemplateDesigner.Services;

/// <summary>
/// Výsledek pravidla výběru šablony pro tisk (`API.SablonyProTisk`, `B190`): šablona a větev, ze které se načte,
/// nebo důvod pro uživatele, proč žádná není. Integrační testy soubor připojují samostatně, proto bez dalších závislostí.
/// </summary>
public record PrintTemplateChoice(Guid? TemplateId, string? SourceIdFa, string? Name, string? Duvod)
{
    public const string CacheClass = "ALVA.TemplateDesigner.API.SablonyProTisk";

    /// <summary>OUT <c>GetTemplateForPrint</c>: bez platného <c>templateId</c> nese <c>duvod</c>.</summary>
    public static PrintTemplateChoice FromOut(CVar result)
    {
        string? Out(string key) => result[key]?.Value?.ToString() is { Length: > 0 } s ? s : null;
        return Guid.TryParse(Out("templateId"), out var id)
            ? new PrintTemplateChoice(id, Out("sourceIdFa"), Out("name"), null)
            : new PrintTemplateChoice(null, null, null, Out("duvod") ?? "Server nevrátil šablonu pro tisk.");
    }
}
