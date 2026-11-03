using EDControls.EDViewModel;

namespace TemplateDesigner;

/// <summary>
/// Bridge mezi parent (MainWindow) a EDInputForm-based dialogem pro pojmenování
/// šablony před uložením do Caché. Definice formuláře (která pole,
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

    /// <summary>Agenda šablony - okno ji jen ukáže. Změnit ji jde jedině příkazem Změnit agendu, který
    /// založí novou šablonu a vymění nabídku dat; přes „Uložit jako" vznikala šablona s prvky jedné
    /// agendy pod jinou, i nezveřejněnou (`B194a` C10).</summary>
    public string AgendaCode { get; }

    /// <summary>Zobrazovaný název agendy pro formulář (obecná agenda v serverovém seznamu není).</summary>
    public string AgendaLabel { get; }

    /// <summary>Titulek okna; týž formulář slouží i pro Upravit v okně šablon.</summary>
    public string WindowTitle { get; init; } = "Uložit do databáze";

    public SaveToCacheDialogViewModel(string initialName, string? agendaCode, string? agendaLabel = null)
    {
        InitialName = initialName;
        InitialAgendaCode = agendaCode;
        Name = initialName;
        AgendaCode = agendaCode ?? "";
        AgendaLabel = string.IsNullOrEmpty(agendaLabel) ? AgendaCode : agendaLabel;
    }
}
