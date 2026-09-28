using System.ComponentModel;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner;

public partial class PrintDialog : Window
{
    private readonly PrintDialogViewModel _vm;

    /// <summary>Nabídka polí pro tlačítko „vložit pole" (název souboru exportu). Caché seznam nezná -
    /// dodává ho návrhář ze schématu agendy, viz <see cref="EDInputForm.PlaceholderFields"/>.</summary>
    public IReadOnlyList<EDControls.ListItem> PlaceholderFields
    {
        get => ParamForm.PlaceholderFields;
        set => ParamForm.PlaceholderFields = value;
    }

    public PrintDialog(PrintDialogViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };
        UsporadaniPanel.DataContext = vm.Usporadani;
        vm.RequestApplyVisibility = ApplyVisibility;
        // Hlášky kontroly ve vlastním okně - nezavírá tiskové, jde mít otevřené obojí.
        vm.RequestShowHlaseni = (nadpis, hlaseni) =>
            new KontrolaVysledkuDialog(new KontrolaVysledkuDialogViewModel(nadpis, hlaseni)) { Owner = this }.Show();

        // Parametry sestavy + export pole renderuje EDInputForm z Caché (AgendaParams) - schéma
        // řídí agenda přes GetParameters, AgendaParams přidává universální skupinu „Export".
        // CacheClass + InitParams se musí nastavit před Loaded (Init běží tam). Staví se i pro agendu
        // bez GetParameters (jen export skupina) - proto HasForm, ne HasParameters.
        if (vm.HasForm)
        {
            ParamForm.InitParams = vm.BuildInitParams();
            ParamForm.SchemaLoaded += OnParamFormInitialized;
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
        // PNG je obrázek za každou stránku - členění do souborů ani měřítko tisku se ho netýkají.
        bool png = _vm.JePng;

        foreach (var field in fields)
        {
            bool visible = field.Key switch
            {
                PrintDialogViewModel.ExportFileNameKey => isSplit && !png,
                PrintDialogViewModel.ExportSplitKey or SheetLayoutKeys.Meritko => !png,
                _ => _vm.IsParamVisible(field.Key),
            };
            var target = visible ? ItemVisibility.Visible : ItemVisibility.Collapsed;
            // Guard - Item.Visibility setter nemá early-return, vyhneme se zbytečným notifikacím při každém stisku.
            if (field.Visibility != target) field.Visibility = target;
        }

        SchovejPrazdneSkupiny();
    }

    private readonly Dictionary<Group, ItemVisibility> _stavSkupinyPredSchovanim = [];

    /// <summary>
    /// Skupina bez jediného viditelného pole se schová i s nadpisem - u PNG by jinak zůstal prázdný
    /// nadpis „Export". Při návratu dostane zpátky rozbalení, které měla.
    /// </summary>
    private void SchovejPrazdneSkupiny()
    {
        Group? skupina = null;
        bool maPole = false;
        foreach (var item in (ParamForm.Items ?? []).Append(null))
        {
            if (item is Field f) { maPole |= f.Visibility == ItemVisibility.Visible; continue; }
            if (skupina is not null)
            {
                if (!maPole && skupina.Visibility != ItemVisibility.ProgrammerHidden)
                {
                    _stavSkupinyPredSchovanim[skupina] = skupina.Visibility;
                    skupina.Visibility = ItemVisibility.ProgrammerHidden;
                }
                else if (maPole && _stavSkupinyPredSchovanim.Remove(skupina, out var puvodni))
                    skupina.Visibility = puvodni;
            }
            skupina = item as Group;
            maPole = false;
        }
    }
}
