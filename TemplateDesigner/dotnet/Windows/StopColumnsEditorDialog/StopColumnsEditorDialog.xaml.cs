using System.Collections.Generic;
using System.Windows;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner;

public partial class StopColumnsEditorDialog : Window
{
    private const string GeneralSchemaClass = "ALVA.TemplateDesigner.EDInputForm.StopListElementEdit";

    private readonly StopColumnsEditorDialogViewModel _vm;
    private readonly StopListElement _element;
    private Dictionary<string, string> _preFilled = [];

    /// <summary>Výsledné sloupce po OK - pass-through z VM.</summary>
    public List<StopColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Command z tabu „Obecné" (EDInputForm) - null pokud se nic nezměnilo.
    /// Caller vykoná spolu se ResultColumns.</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

    /// <summary>Pravidla zvýraznění po editaci (button „Zvýraznění řádků…"). Aplikuje caller až na OK
    /// dialogu (deferred - Storno columns dialogu změnu zahodí). <see cref="HighlightEdited"/> = bylo upraveno.</summary>
    public List<HighlightRule>? ResultHighlightRules { get; private set; }
    public bool HighlightEdited { get; private set; }

    public StopColumnsEditorDialog(StopListElement element)
    {
        _element = element;
        _vm = new StopColumnsEditorDialogViewModel(element, element.Columns);
        DataContext = _vm;
        InitializeComponent();

        // Tab „Obecné": EDInputForm (general properties přes reflexi). Styl záhlaví/buněk
        // řeší CellStyleEditor v témže tabu, sloupce tab „Sloupce".
        GeneralForm.CacheClass = GeneralSchemaClass;
        GeneralForm.SchemaLoaded += OnGeneralFormInitialized;
        ElementFormBinder.HookFontWeightRefresh(GeneralForm);
        GeneralForm.ButtonClicked += OnGeneralButtonClicked;

        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private void OnGeneralButtonClicked(string key)
    {
        if (key != "__highlightRules") return;
        var current = ResultHighlightRules ?? _element.HighlightRules;   // reopen ukáže rozeditovaná pravidla
        var dlg = new HighlightRulesEditorDialog(current) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ResultHighlightRules = dlg.ResultRules;
            HighlightEdited = true;
        }
    }

    private void OnGeneralFormInitialized()
    {
        ElementFormBinder.PopulateFontFields(GeneralForm);
        _preFilled = ElementFormBinder.Prefill(GeneralForm, _element);
        ElementFormBinder.PopulateFontWeightFields(GeneralForm);   // řezy dle rodiny (před Validate)
        GeneralForm.Validate();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // EDInputForm validace „Obecné" - při chybě zůstat v dialogu.
        if (!GeneralForm.Save()) return;

        var cmds = ElementFormBinder.ExtractCommands(GeneralForm, _element, _preFilled);
        GeneralCommand = cmds.Count switch
        {
            0 => null,
            1 => cmds[0],
            _ => new CompositeCommand("Nastavení zastávek", cmds)
        };

        // VM dokončí (CellStyle → element, ResultColumns, zavření).
        _vm.OkCommand.Execute(null);
    }
}
