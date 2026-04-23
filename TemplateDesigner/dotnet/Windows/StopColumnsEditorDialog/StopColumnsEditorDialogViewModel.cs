using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce ────────────────────────────────────────────

public class StopColumnVM : INotifyPropertyChanged
{
    private string _key = "";
    private string _header = "";
    private float  _width = 10f;
    private bool   _visible = true;
    private string _alignmentLabel = "Vlevo";

    public string Key            { get => _key;            set { _key = value;            OnChanged(); } }
    public string Header         { get => _header;         set { _header = value;         OnChanged(); } }
    public float  Width          { get => _width;          set { _width = value;          OnChanged(); } }
    public bool   Visible        { get => _visible;        set { _visible = value;        OnChanged(); } }
    public string AlignmentLabel { get => _alignmentLabel; set { _alignmentLabel = value; OnChanged(); } }

    private CellStyleVM _headerStyle = null!;
    public CellStyleVM HeaderStyle
    {
        get => _headerStyle;
        private init { _headerStyle = value; _headerStyle.PropertyChanged += OnChildChanged; }
    }

    private CellStyleVM _dataStyle = null!;
    public CellStyleVM DataStyle
    {
        get => _dataStyle;
        private init { _dataStyle = value; _dataStyle.PropertyChanged += OnChildChanged; }
    }

    /// <summary>True pokud sloupec má aspoň jeden per-column override v HeaderStyle nebo DataStyle.</summary>
    public bool HasAnyOverride => HeaderStyle.HasAnyExplicit || DataStyle.HasAnyExplicit;

    private void OnChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CellStyleVM.HasAnyExplicit))
            OnChanged(nameof(HasAnyOverride));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public StopColumn ToModel() => new()
    {
        Key       = Key,
        Header    = Header,
        Width     = Width,
        Visible   = Visible,
        Alignment = AlignmentLabel switch
        {
            "Střed"  => TextHorizontalAlignment.Center,
            "Vpravo" => TextHorizontalAlignment.Right,
            _        => TextHorizontalAlignment.Left
        },
        Style = BuildStyle()
    };

    private ColumnVisualStyle? BuildStyle()
    {
        var style = new ColumnVisualStyle
        {
            Header = HeaderStyle.ToModel(),
            Data = DataStyle.ToModel()
        };
        return style.IsEmpty ? null : style;
    }

    public static StopColumnVM FromModel(StopColumn c,
        ResolvedTextStyle headerInherited, ResolvedTextStyle dataInherited) => new()
    {
        Key            = c.Key,
        Header         = c.Header,
        Width          = c.Width,
        Visible        = c.Visible,
        AlignmentLabel = c.Alignment switch
        {
            TextHorizontalAlignment.Center => "Střed",
            TextHorizontalAlignment.Right  => "Vpravo",
            _                              => "Vlevo"
        },
        HeaderStyle = CellStyleVM.FromModel(c.Style?.Header, headerInherited),
        DataStyle   = CellStyleVM.FromModel(c.Style?.Data,   dataInherited)
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class StopColumnsEditorDialogViewModel : EDViewModel
{
    // ── Tab 1: Obecné — element-level styly + property descriptors ───────

    /// <summary>Výchozí styl záhlaví elementu (kaskáda: per-column → tento → ScheduleHeaderDefaults).</summary>
    public CellStyleVM ElementHeaderStyle { get; }

    /// <summary>Výchozí styl dat elementu (kaskáda: per-column → tento → ScheduleDataDefaults).</summary>
    public CellStyleVM ElementDataStyle { get; }

    public IReadOnlyList<PropertyDescriptor> GeneralDescriptors { get; }

    // ── Tab 2: Sloupce ──────────────────────────────────────────────────

    public ObservableCollection<StopColumnVM> ColumnVMs { get; } = [];

    public string[] AlignmentOptions { get; } = ["Vlevo", "Střed", "Vpravo"];
    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    // ── UI omezení editace — co CellStyleEditor zobrazí per zóna ─────────
    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;
    public CellStyleEditOptions DataEditOptions   { get; } = CellStyleEditOptions.AllEnabled;

    /// <summary>Výsledné sloupce po OK — čteny volajícím.</summary>
    public List<StopColumn> ResultColumns { get; private set; } = [];

    // ── Výběr ──────────────────────────────────────────────────────────────

    private StopColumnVM? _selectedColumnVM;
    public StopColumnVM? SelectedColumnVM
    {
        get => _selectedColumnVM;
        set { Set(ref _selectedColumnVM, value); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedColumnVM is not null;

    // ── Callbacks ──────────────────────────────────────────────────────────

    public Action? RequestCommitEdits { get; set; }
    public Func<string, string?>? RequestColorPicker { get; set; }

    // ── Commands ────────────────────────────────────────────────────────────

    public RelayCommand MoveUpCommand   { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand ResetCommand    { get; }

    // Color picker commands — záhlaví
    public RelayCommand PickHeaderTextColorCommand { get; }
    public RelayCommand PickHeaderBgColorCommand { get; }
    public RelayCommand ClearHeaderTextColorCommand { get; }
    public RelayCommand ClearHeaderBgColorCommand { get; }

    // Color picker commands — data
    public RelayCommand PickDataTextColorCommand { get; }
    public RelayCommand PickDataBgColorCommand { get; }
    public RelayCommand ClearDataTextColorCommand { get; }
    public RelayCommand ClearDataBgColorCommand { get; }

    public RelayCommand ResetStyleCommand { get; }
    public RelayCommand ResetAllColumnStylesCommand { get; }

    // Element-level (Obecné tab)
    public RelayCommand PickElementHeaderTextColorCommand { get; }
    public RelayCommand PickElementHeaderBgColorCommand { get; }
    public RelayCommand PickElementDataTextColorCommand { get; }
    public RelayCommand PickElementDataBgColorCommand { get; }
    public RelayCommand ClearElementHeaderTextColorCommand { get; }
    public RelayCommand ClearElementHeaderBgColorCommand { get; }
    public RelayCommand ClearElementDataTextColorCommand { get; }
    public RelayCommand ClearElementDataBgColorCommand { get; }
    public RelayCommand ResetElementStyleCommand { get; }

    // ── Konstruktor ─────────────────────────────────────────────────────────

    private readonly StopListElement _element;

    public StopColumnsEditorDialogViewModel(
        StopListElement element,
        List<StopColumn> columns,
        IReadOnlyList<PropertyDescriptor> generalDescriptors)
    {
        _element           = element;
        GeneralDescriptors = generalDescriptors;

        // Element-level CellStyleVM (Obecné tab) — parent je ScheduleHeaderDefaults/ScheduleDataDefaults
        ElementHeaderStyle = CellStyleVM.FromModel(element.HeaderStyle, element.ResolveElementHeaderParent());
        ElementDataStyle   = CellStyleVM.FromModel(element.DataStyle,   element.ResolveElementDataParent());

        var headerInherited = element.ResolveColumnHeaderParent();
        var dataInherited   = element.ResolveColumnDataParent();

        foreach (var col in columns)
            ColumnVMs.Add(StopColumnVM.FromModel(col, headerInherited, dataInherited));

        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];

        MoveUpCommand   = Command(MoveUp);
        MoveDownCommand = Command(MoveDown);
        ResetCommand    = Command(Reset);

        // Color picker commands — záhlaví
        PickHeaderTextColorCommand  = Command(() => PickColor(g => g.HeaderStyle.TextColor, (g, c) => g.HeaderStyle.TextColor = c));
        PickHeaderBgColorCommand    = Command(() => PickColor(g => g.HeaderStyle.BgColor,   (g, c) => g.HeaderStyle.BgColor = c));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.HeaderStyle.TextColor = null; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.HeaderStyle.BgColor   = null; });

        // Color picker commands — data
        PickDataTextColorCommand  = Command(() => PickColor(g => g.DataStyle.TextColor, (g, c) => g.DataStyle.TextColor = c));
        PickDataBgColorCommand    = Command(() => PickColor(g => g.DataStyle.BgColor,   (g, c) => g.DataStyle.BgColor = c));
        ClearDataTextColorCommand = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.DataStyle.TextColor = null; });
        ClearDataBgColorCommand   = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.DataStyle.BgColor   = null; });

        // Reset style — vybraný sloupec
        ResetStyleCommand = Command(() =>
        {
            if (SelectedColumnVM is null) return;
            SelectedColumnVM.HeaderStyle.Reset();
            SelectedColumnVM.DataStyle.Reset();
        });

        // Reset všech sloupců — smaže per-column overrides
        ResetAllColumnStylesCommand = Command(() =>
        {
            foreach (var vm in ColumnVMs)
            {
                vm.HeaderStyle.Reset();
                vm.DataStyle.Reset();
            }
        });

        // Element-level color picker commands (Obecné tab)
        PickElementHeaderTextColorCommand  = Command(() => PickElementColor(ElementHeaderStyle, s => s.TextColor, (s, c) => s.TextColor = c));
        PickElementHeaderBgColorCommand    = Command(() => PickElementColor(ElementHeaderStyle, s => s.BgColor,   (s, c) => s.BgColor   = c));
        PickElementDataTextColorCommand    = Command(() => PickElementColor(ElementDataStyle,   s => s.TextColor, (s, c) => s.TextColor = c));
        PickElementDataBgColorCommand      = Command(() => PickElementColor(ElementDataStyle,   s => s.BgColor,   (s, c) => s.BgColor   = c));
        ClearElementHeaderTextColorCommand = Command(() => ElementHeaderStyle.TextColor = null);
        ClearElementHeaderBgColorCommand   = Command(() => ElementHeaderStyle.BgColor   = null);
        ClearElementDataTextColorCommand   = Command(() => ElementDataStyle.TextColor   = null);
        ClearElementDataBgColorCommand     = Command(() => ElementDataStyle.BgColor     = null);

        ResetElementStyleCommand = Command(() =>
        {
            ElementHeaderStyle.Reset();
            ElementDataStyle.Reset();
        });
    }

    private void PickElementColor(CellStyleVM vm, Func<CellStyleVM, string?> getter, Action<CellStyleVM, string?> setter)
    {
        if (RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(vm) ?? "");
        if (result is not null)
            setter(vm, result);
    }

    // ── Přesouvání ──────────────────────────────────────────────────────────

    private void MoveUp()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        if (idx > 0) ColumnVMs.Move(idx, idx - 1);
    }

    private void MoveDown()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        if (idx >= 0 && idx < ColumnVMs.Count - 1) ColumnVMs.Move(idx, idx + 1);
    }

    private void Reset()
    {
        var headerInherited = _element.ResolveColumnHeaderParent();
        var dataInherited   = _element.ResolveColumnDataParent();

        ColumnVMs.Clear();
        foreach (var col in StopListElement.GetDefaultColumns())
            ColumnVMs.Add(StopColumnVM.FromModel(col, headerInherited, dataInherited));
        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];
    }

    private void PickColor(Func<StopColumnVM, string?> getter, Action<StopColumnVM, string?> setter)
    {
        if (SelectedColumnVM is null || RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(SelectedColumnVM) ?? "");
        if (result is not null)
            setter(SelectedColumnVM, result);
    }

    // ── OK ──────────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        RequestCommitEdits?.Invoke();

        // Element-level styl — zápis zpět do elementu (ScheduleElementBase.HeaderStyle/DataStyle)
        _element.HeaderStyle = ElementHeaderStyle.ToModel() ?? new TextStyle();
        _element.DataStyle   = ElementDataStyle.ToModel()   ?? new TextStyle();

        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
