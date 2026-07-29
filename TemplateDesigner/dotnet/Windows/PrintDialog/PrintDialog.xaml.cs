using System.ComponentModel;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;
using TemplateDesigner.Models.Layout;

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

        // Parametry sestavy + export pole renderuje EDInputForm z Caché (AgendaParams) - schéma
        // řídí agenda přes GetParameters, AgendaParams přidává universální skupinu „Export".
        // CacheClass + InitParams se musí nastavit před Loaded (Init běží tam). Staví se i pro agendu
        // bez GetParameters (jen export skupina) - proto HasForm, ne HasParameters.
        if (vm.HasForm)
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
        var fields = (ParamForm.Items ?? []).OfType<Field>().ToList();

        // Název souboru má smysl jen při členění na víc souborů (split != Single).
        var split = fields.FirstOrDefault(f => f.Key == PrintDialogViewModel.ExportSplitKey)?.Value ?? "SINGLE";
        bool isSplit = split != "SINGLE";

        // Detailní pole imposice (mřížka/opakování/značky) jen když je zvolen formát listu.
        var sheet = fields.FirstOrDefault(f => f.Key == SheetLayoutKeys.Sheet)?.Value ?? SheetLayoutKeys.SheetNone;
        bool hasSheet = sheet is not ("" or SheetLayoutKeys.SheetNone);

        foreach (var field in fields)
        {
            bool visible = field.Key switch
            {
                PrintDialogViewModel.ExportFileNameKey => isSplit,
                SheetLayoutKeys.Grid or SheetLayoutKeys.Repeat or SheetLayoutKeys.Marks => hasSheet,
                _ => _vm.IsParamVisible(field.Key),
            };
            var target = visible ? EItemVisibility.Visible : EItemVisibility.Collapsed;
            // Guard - Item.Visibility setter nemá early-return, vyhneme se zbytečným notifikacím při každém stisku.
            if (field.Visibility != target) field.Visibility = target;
        }
    }
}
