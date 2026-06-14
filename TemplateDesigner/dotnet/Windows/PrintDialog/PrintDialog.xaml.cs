using System.ComponentModel;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;

namespace TemplateDesigner;

public partial class PrintDialog : Window
{
    private readonly PrintDialogViewModel _vm;

    public PrintDialog(PrintDialogViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };

        // Parametry sestavy renderuje EDInputForm z Caché (AgendaParams) — schéma řídí agenda
        // přes GetParameters. CacheClass + InitParams se musí nastavit před Loaded (Init běží tam).
        if (vm.HasParameters && !string.IsNullOrEmpty(vm.AgendaClass))
        {
            ParamForm.InitParams = vm.BuildInitParams();
            ParamForm.Initialized += OnParamFormInitialized;
            ParamForm.CacheClass = "ALVA.TemplateDesigner.EDInputForm.AgendaParams";
        }
    }

    // Po načtení formuláře z Caché: napojit změny polí na sdílený dict (živý náhled) + počáteční
    // viditelnost (visibleWhen). visibleWhen se vyhodnocuje v C# (VM), viditelnost přepínáme na polích.
    private void OnParamFormInitialized()
    {
        foreach (var field in (ParamForm.Items ?? []).OfType<Field>())
            field.PropertyChanged += OnFieldChanged;

        ApplyVisibility();
    }

    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Field field || e.PropertyName != nameof(Field.Value)) return;

        _vm.SetParamValue(field.Key, field.Value ?? "");
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        foreach (var field in (ParamForm.Items ?? []).OfType<Field>())
        {
            var target = _vm.IsParamVisible(field.Key)
                ? EItemVisibility.Visible
                : EItemVisibility.Collapsed;
            // Guard — Item.Visibility setter nemá early-return, vyhneme se zbytečným notifikacím při každém stisku.
            if (field.Visibility != target) field.Visibility = target;
        }
    }
}
