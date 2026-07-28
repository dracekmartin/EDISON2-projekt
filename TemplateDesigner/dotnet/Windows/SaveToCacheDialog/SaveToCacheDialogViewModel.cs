using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

/// <summary>
/// Bridge mezi parent (MainWindow) a EDInputForm-based dialogem pro pojmenování
/// šablony před uložením do Caché. Po Wave 4 - definice formuláře (která pole,
/// labely, validace) žije v Caché třídě <c>ALVA.TemplateDesigner.EDInputForm.SaveTemplate</c>,
/// VM jen drží initial values (input → Caché Init) a final result (output ← form.Save).
/// </summary>
public class SaveToCacheDialogViewModel : EDViewModel
{
    /// <summary>Initial Name pro pre-fill formuláře (z aktuální Template).</summary>
    public string InitialName { get; }

    /// <summary>Initial Agenda code pro pre-fill formuláře.</summary>
    public string? InitialAgendaCode { get; }

    /// <summary>Final Name po user submit (čteno parentem po ShowDialog).</summary>
    public string Name { get; internal set; } = "";

    /// <summary>Final Agenda code po user submit (čteno parentem). Konzistentní s legacy `SelectedAgenda.Code`.</summary>
    public string AgendaCode { get; internal set; } = "";

    /// <summary>
    /// Backward-compat accessor - některé call sites referují `SelectedAgenda.Code`.
    /// Vrací AgendaInfo s prázdným Name (label není po EDInputForm migraci dostupný).
    /// </summary>
    public AgendaInfo SelectedAgenda => new(AgendaCode, "");

    public SaveToCacheDialogViewModel(string initialName, string? initialAgendaCode)
    {
        InitialName = initialName;
        InitialAgendaCode = initialAgendaCode;
        Name = initialName;
        AgendaCode = initialAgendaCode ?? "";
    }
}
