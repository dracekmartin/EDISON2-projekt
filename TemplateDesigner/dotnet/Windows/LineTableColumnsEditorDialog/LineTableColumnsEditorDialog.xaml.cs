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
    /// <summary>Klíč pamatování filtru hledání (viz <c>EDInputForm.SearchMemoryKey</c>). Nastavuje
    /// ho jen otevření vlastností prvku z plátna - jinde se filtr nepamatuje.</summary>
    public string SearchMemoryKey { get => GeneralForm.SearchMemoryKey; set => GeneralForm.SearchMemoryKey = value; }

    private const string GeneralSchemaClass = "ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit";

    private readonly LineTableColumnsEditorDialogViewModel _vm;
    private readonly LineTableElement _element;
    private Dictionary<string, string> _preFilled = [];

    /// <summary>Které pole formuláře „Obecné" je vidět - viz <see cref="ElementEditDialog.ViditelnostPole"/>.
    /// Null = vše; nastavit před zobrazením.</summary>
    public Func<string, IReadOnlyDictionary<string, string>, bool>? ViditelnostPole { get; set; }

    /// <summary>Úprava polí „Obecné" po naplnění (nápověda podle agendy) - viz <see cref="ElementEditDialog.UpravaPoli"/>.</summary>
    public Action<IEnumerable<EDControls.EDInputForm.Item>?>? UpravaPoli { get; set; }

    /// <summary>Výsledné sloupce po OK - pass-through z VM.</summary>
    public List<Models.Elements.StopColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Command z tabu „Obecné" (EDInputForm) - null pokud se nic nezměnilo.</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

    /// <summary>Pravidla zvýraznění po editaci (button „Zvýraznění řádků…"). Aplikuje caller až na OK
    /// dialogu (deferred - Storno columns dialogu změnu zahodí). <see cref="HighlightEdited"/> = bylo upraveno.</summary>
    public List<HighlightRule>? ResultHighlightRules { get; private set; }
    public bool HighlightEdited { get; private set; }

    public LineTableColumnsEditorDialog(LineTableElement element)
    {
        _element = element;
        _vm = new LineTableColumnsEditorDialogViewModel(element, element.Columns);
        DataContext = _vm;
        InitializeComponent();
        Title = Models.ElementNames.TitulekVlastnosti(element);

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
        if (ViditelnostPole is not null) ElementFormBinder.PouzijViditelnost(GeneralForm.Items, ViditelnostPole);
        UpravaPoli?.Invoke(GeneralForm.Items);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!GeneralForm.Save()) return;

        var cmds = ElementFormBinder.ExtractCommands(GeneralForm, _element, _preFilled);
        GeneralCommand = cmds.Count switch
        {
            0 => null,
            1 => cmds[0],
            _ => new CompositeCommand("Nastavení linkového JŘ", cmds)
        };

        _vm.OkCommand.Execute(null);
    }
}
