using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;
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

    public CellStyleVM HeaderStyle { get; set; } = new();
    public CellStyleVM DataStyle { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    private static readonly TextHorizontalAlignment[] HAlignMap =
        [TextHorizontalAlignment.Left, TextHorizontalAlignment.Center, TextHorizontalAlignment.Right];

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

    public static StopColumnVM FromModel(StopColumn c) => new()
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
        HeaderStyle = CellStyleVM.FromModel(c.Style?.Header),
        DataStyle   = CellStyleVM.FromModel(c.Style?.Data)
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class StopColumnsEditorDialogViewModel : EDViewModel
{
    // ── Tab 1: Obecné — property descriptors z elementu ──────────────────

    public IReadOnlyList<PropertyDescriptor> GeneralDescriptors { get; }

    // ── Tab 2: Sloupce ──────────────────────────────────────────────────

    public ObservableCollection<StopColumnVM> ColumnVMs { get; } = [];

    public string[] AlignmentOptions { get; } = ["Vlevo", "Střed", "Vpravo"];
    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

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

    // ── Konstruktor ─────────────────────────────────────────────────────────

    public StopColumnsEditorDialogViewModel(
        List<StopColumn> columns,
        IReadOnlyList<PropertyDescriptor> generalDescriptors)
    {
        GeneralDescriptors = generalDescriptors;

        foreach (var col in columns)
            ColumnVMs.Add(StopColumnVM.FromModel(col));

        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];

        MoveUpCommand   = Command(MoveUp);
        MoveDownCommand = Command(MoveDown);
        ResetCommand    = Command(Reset);

        // Color picker commands — záhlaví
        PickHeaderTextColorCommand  = Command(() => PickColor(g => g.HeaderStyle.TextColor, (g, c) => g.HeaderStyle.TextColor = c));
        PickHeaderBgColorCommand    = Command(() => PickColor(g => g.HeaderStyle.BgColor,   (g, c) => g.HeaderStyle.BgColor = c));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.HeaderStyle.TextColor = ""; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.HeaderStyle.BgColor = ""; });

        // Color picker commands — data
        PickDataTextColorCommand  = Command(() => PickColor(g => g.DataStyle.TextColor, (g, c) => g.DataStyle.TextColor = c));
        PickDataBgColorCommand    = Command(() => PickColor(g => g.DataStyle.BgColor,   (g, c) => g.DataStyle.BgColor = c));
        ClearDataTextColorCommand = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.DataStyle.TextColor = ""; });
        ClearDataBgColorCommand   = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.DataStyle.BgColor = ""; });

        // Reset style
        ResetStyleCommand = Command(() =>
        {
            if (SelectedColumnVM is null) return;
            SelectedColumnVM.HeaderStyle.Reset();
            SelectedColumnVM.DataStyle.Reset();
        });
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
        ColumnVMs.Clear();
        foreach (var col in StopListElement.GetDefaultColumns())
            ColumnVMs.Add(StopColumnVM.FromModel(col));
        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];
    }

    private void PickColor(Func<StopColumnVM, string> getter, Action<StopColumnVM, string> setter)
    {
        if (SelectedColumnVM is null || RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(SelectedColumnVM));
        if (result is not null)
            setter(SelectedColumnVM, result);
    }

    // ── OK ──────────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        RequestCommitEdits?.Invoke();
        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
