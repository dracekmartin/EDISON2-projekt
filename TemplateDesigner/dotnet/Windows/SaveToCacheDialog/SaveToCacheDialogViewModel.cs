using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// Dialog pro pojmenování šablony před uložením do Caché.
/// Výchozí hodnoty z aktuální Template — uživatel může před uložením upravit.
/// </summary>
public class SaveToCacheDialogViewModel : EDViewModel
{
    public List<AgendaInfo> AgendaOptions { get; }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    private AgendaInfo _selectedAgenda;
    public AgendaInfo SelectedAgenda
    {
        get => _selectedAgenda;
        set => Set(ref _selectedAgenda, value);
    }

    public SaveToCacheDialogViewModel(string initialName, string? initialAgendaCode, IReadOnlyList<AgendaInfo> agendas)
    {
        _name = initialName;
        AgendaOptions = agendas.Count > 0
            ? agendas.ToList()
            : [new AgendaInfo(CacheTemplateService.GeneralAgendaCode, "Obecná (bez databáze)")];

        var code = initialAgendaCode ?? CacheTemplateService.GeneralAgendaCode;
        _selectedAgenda = AgendaOptions.FirstOrDefault(a =>
            a.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? AgendaOptions[0];
    }

    protected override void OnOk()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            PopupDisplayer.Info("Zadejte název šablony.");
            return;
        }
        Name = _name.Trim();
        CloseDialog(true);
    }
}
