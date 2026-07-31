using System.Linq;
using System.Windows;
using ALWA.SYS;
using EDControls.EDInputForm;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

public partial class SaveToCacheDialog : Window
{
    private readonly SaveToCacheDialogViewModel _vm;

    public SaveToCacheDialog(SaveToCacheDialogViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        // Pre-fill initial values do Caché Init přes IN params - formulář se otevře
        // s aktuálním Name + AgendaCode šablony.
        var init = new CVar();
        init["name"]       = new CVar(vm.InitialName ?? "");
        init["agendaCode"] = new CVar(vm.InitialAgendaCode ?? "");
        TheForm.InitParams = init;

        // Tlačítko „Uložit" je nedostupné, dokud není vyplněné jméno - místo popupu při kliku
        // na prázdné jméno (přívětivější). Popup ze Save() zůstává jako záloha pro ostatní validace.
        TheForm.SchemaLoaded += UpdateSaveEnabled;
        TheForm.FieldChanged += _ => UpdateSaveEnabled();
    }

    private void UpdateSaveEnabled()
    {
        var name = (TheForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == "name")?.Value;
        BtnSave.IsEnabled = !string.IsNullOrWhiteSpace(name);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // EDInputForm.Save() = Validate(BeforeSave) + Caché Save. Pro náš případ je
        // server-side Save no-op (vlastní uložení dělá MainWindow přes CacheTemplateService).
        // Save() vrátí false a zobrazí popup s chybami pokud validace selže.
        if (!TheForm.Save()) return;

        // Extract final values z form Items
        var fields = (TheForm.Items ?? []).OfType<Field>().ToDictionary(f => f.Key, f => f.Value ?? "");
        _vm.Name        = fields.GetValueOrDefault("name", "").Trim();
        _vm.AgendaCode  = fields.GetValueOrDefault("agendaCode", "");

        DialogResult = true;
        Close();
    }
}
