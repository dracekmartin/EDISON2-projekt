using System.ComponentModel;
using System.Windows;

namespace TemplateDesigner;

public partial class CacheTemplatePickerDialog : Window
{
    private readonly CacheTemplatePickerDialogViewModel _vm;

    public CacheTemplatePickerDialog(CacheTemplatePickerDialogViewModel vm)
    {
        DataContext = _vm = vm;
        InitializeComponent();

        // Konfigurace EDListu
        TheList.CacheClass = "ALVA.TemplateDesigner.API.TemplatesList";
        TheList.HostId = "TemplateDesigner.TemplatePicker";
        TheList.Title = "Šablony v Caché";

        // Eventy → VM
        TheList.SelectionChanged   += (_, _)  => _vm.Selected = TheList.SelectedRow;
        TheList.RowDoubleClicked   += (_, _)  => { if (_vm.OpenCommand.CanExecute(null)) _vm.OpenCommand.Execute(null); };

        // VM žádá reload (po Delete / Rename) → znovu načti s aktuálním filtrem
        _vm.RequestReload = ReloadList;

        // Změna agenda filtru → reload
        _vm.PropertyChanged += OnVmPropertyChanged;

        // Close
        _vm.RequestClose += result => { DialogResult = result; Close(); };

        // Init list po načtení okna
        Loaded += (_, _) =>
        {
            TheList.InitializeList();
            ReloadList();
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CacheTemplatePickerDialogViewModel.SelectedFilter))
            ReloadList();
    }

    private void ReloadList()
    {
        var code = _vm.SelectedFilter?.Code;
        if (string.IsNullOrEmpty(code))
            TheList.LoadList();
        else
            TheList.LoadList(new Dictionary<string, string> { ["agendaCode"] = code });
    }
}
