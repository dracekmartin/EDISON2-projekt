using System.Collections.Generic;
using System.Windows;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner;

public partial class LineTableColumnsEditorDialog : Window
{
    private const string GeneralSchemaClass = "ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit";

    private readonly LineTableColumnsEditorDialogViewModel _vm;
    private readonly LineTableElement _element;
    private Dictionary<string, string> _preFilled = [];

    /// <summary>Výsledné sloupce po OK — pass-through z VM.</summary>
    public List<Models.Elements.StopColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Command z tabu „Obecné" (EDInputForm) — null pokud se nic nezměnilo.</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

    /// <summary>Aplikace pravidel zvýraznění (button „Zvýraznění řádků…") — wire MainWindow (undoable).</summary>
    public Action<List<HighlightRule>>? RequestApplyHighlight { get; set; }

    public LineTableColumnsEditorDialog(LineTableElement element)
    {
        _element = element;
        _vm = new LineTableColumnsEditorDialogViewModel(element, element.Columns);
        DataContext = _vm;
        InitializeComponent();

        GeneralForm.CacheClass = GeneralSchemaClass;
        GeneralForm.Initialized += OnGeneralFormInitialized;
        GeneralForm.ButtonClicked += OnGeneralButtonClicked;

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private void OnGeneralButtonClicked(string key)
    {
        if (key != "__highlightRules") return;
        var dlg = new HighlightRulesEditorDialog(_element.HighlightRules) { Owner = this };
        if (dlg.ShowDialog() == true)
            RequestApplyHighlight?.Invoke(dlg.ResultRules);
    }

    private void OnGeneralFormInitialized()
    {
        ElementFormBinder.PopulateFontFields(GeneralForm);
        _preFilled = ElementFormBinder.Prefill(GeneralForm, _element);
        GeneralForm.Validate();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!GeneralForm.Save()) return;

        var cmds = ElementFormBinder.ExtractCommands(GeneralForm, _element, _preFilled);
        GeneralCommand = cmds.Count switch
        {
            0 => null,
            1 => cmds[0],
            _ => new CompositeCommand("Nastavení knižního JŘ", cmds)
        };

        _vm.OkCommand.Execute(null);
    }
}
