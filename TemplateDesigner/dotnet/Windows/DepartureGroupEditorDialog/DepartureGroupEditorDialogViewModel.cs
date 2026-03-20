using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

// ─── ViewModel pro řádek seznamu skupin ──────────────────────────────────────

public class DepartureGroupColumnVM : INotifyPropertyChanged
{
    private string _skupinaDnuId = "";
    private string _name = "";
    private bool   _isVisible = true;

    // Záhlaví
    private int    _headerAlignmentIndex = 1; // 0=Vlevo, 1=Střed, 2=Vpravo
    private int    _headerVAlignIndex = 2;    // 0=Nahoru, 1=Střed, 2=Dolů
    private float? _headerFontSize;
    private bool   _headerBold = true;
    private bool   _headerItalic;
    private string _headerTextColor = "";
    private string _headerBgColor = "";

    // Data
    private float? _dataFontSize;
    private bool   _dataBold;
    private bool   _dataItalic;
    private string _columnBgColor = "";

    public string SkupinaDnuId { get => _skupinaDnuId; set { _skupinaDnuId = value; OnChanged(); } }
    public string Name         { get => _name;         set { _name = value;         OnChanged(); } }
    public bool   IsVisible    { get => _isVisible;    set { _isVisible = value;    OnChanged(); } }

    public int    HeaderAlignmentIndex  { get => _headerAlignmentIndex; set { _headerAlignmentIndex = value; OnChanged(); } }
    public int    HeaderVAlignIndex    { get => _headerVAlignIndex;    set { _headerVAlignIndex = value;    OnChanged(); } }
    public float? HeaderFontSize    { get => _headerFontSize;    set { _headerFontSize = value;    OnChanged(); } }
    public bool   HeaderBold        { get => _headerBold;        set { _headerBold = value;        OnChanged(); } }
    public bool   HeaderItalic      { get => _headerItalic;      set { _headerItalic = value;      OnChanged(); } }
    public string HeaderTextColor   { get => _headerTextColor;   set { _headerTextColor = value;   OnChanged(); } }
    public string HeaderBgColor     { get => _headerBgColor;     set { _headerBgColor = value;     OnChanged(); } }

    public float? DataFontSize      { get => _dataFontSize;      set { _dataFontSize = value;      OnChanged(); } }
    public bool   DataBold          { get => _dataBold;          set { _dataBold = value;          OnChanged(); } }
    public bool   DataItalic        { get => _dataItalic;        set { _dataItalic = value;        OnChanged(); } }
    public string ColumnBgColor     { get => _columnBgColor;     set { _columnBgColor = value;     OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    private static readonly TextHorizontalAlignment[] HAlignMap =
        [TextHorizontalAlignment.Left, TextHorizontalAlignment.Center, TextHorizontalAlignment.Right];
    private static readonly VerticalTextAlignment[] VAlignMap =
        [VerticalTextAlignment.Top, VerticalTextAlignment.Center, VerticalTextAlignment.Bottom];

    public DepartureGroupColumnStyle ToModel() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = IsVisible,
        HeaderAlignment = _headerAlignmentIndex == 1 ? null : HAlignMap[_headerAlignmentIndex],
        HeaderVerticalAlignment = _headerVAlignIndex == 2 ? null : VAlignMap[_headerVAlignIndex],
        HeaderFontSize = HeaderFontSize,
        HeaderBold = _headerBold ? null : false,       // true = výchozí → null
        HeaderItalic = _headerItalic ? true : null,    // false = výchozí → null
        HeaderTextColor = NullIfEmpty(HeaderTextColor),
        HeaderBackgroundColor = NullIfEmpty(HeaderBgColor),
        DataFontSize = DataFontSize,
        DataBold = _dataBold ? true : null,            // false = výchozí → null
        DataItalic = _dataItalic ? true : null,        // false = výchozí → null
        ColumnBackgroundColor = NullIfEmpty(ColumnBgColor),
    };

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    public static DepartureGroupColumnVM FromData(
        SkupinaDnuData skupina, DepartureGroupColumnStyle? s) => new()
    {
        SkupinaDnuId = skupina.Id,
        Name = skupina.Nadpis + (skupina.Nadpis2.Length > 0 ? " " + skupina.Nadpis2 : ""),
        IsVisible = s?.IsVisible ?? true,
        HeaderAlignmentIndex = s?.HeaderAlignment switch
        {
            TextHorizontalAlignment.Left  => 0,
            TextHorizontalAlignment.Right => 2,
            _ => 1
        },
        HeaderVAlignIndex = s?.HeaderVerticalAlignment switch
        {
            VerticalTextAlignment.Top    => 0,
            VerticalTextAlignment.Center => 1,
            _ => 2 // Bottom = výchozí
        },
        HeaderFontSize = s?.HeaderFontSize,
        HeaderBold = s?.HeaderBold ?? true,
        HeaderItalic = s?.HeaderItalic ?? false,
        HeaderTextColor = s?.HeaderTextColor ?? "",
        HeaderBgColor = s?.HeaderBackgroundColor ?? "",
        DataFontSize = s?.DataFontSize,
        DataBold = s?.DataBold ?? false,
        DataItalic = s?.DataItalic ?? false,
        ColumnBgColor = s?.ColumnBackgroundColor ?? ""
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class DepartureGroupEditorDialogViewModel : EDViewModel
{
    public ObservableCollection<DepartureGroupColumnVM> GroupVMs { get; } = [];

    public string[] AlignmentOptions { get; } = ["Vlevo", "Na střed", "Vpravo"];
    public string[] VAlignmentOptions { get; } = ["Nahoru", "Na střed", "Dolů"];
    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    public List<DepartureGroupColumnStyle> ResultGroupColumns { get; private set; } = [];

    // ── Výběr ──────────────────────────────────────────────────────────────

    private DepartureGroupColumnVM? _selectedGroupVM;
    public DepartureGroupColumnVM? SelectedGroupVM
    {
        get => _selectedGroupVM;
        set { Set(ref _selectedGroupVM, value); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedGroupVM is not null;

    // ── Callbacks ──────────────────────────────────────────────────────────

    public Func<string, string?>? RequestColorPicker { get; set; }

    // ── Commands ────────────────────────────────────────────────────────────

    public RelayCommand PickHeaderTextColorCommand { get; }
    public RelayCommand PickHeaderBgColorCommand { get; }
    public RelayCommand PickColumnBgColorCommand { get; }
    public RelayCommand ClearHeaderTextColorCommand { get; }
    public RelayCommand ClearHeaderBgColorCommand { get; }
    public RelayCommand ClearColumnBgColorCommand { get; }

    // ── Konstruktor ─────────────────────────────────────────────────────────

    public DepartureGroupEditorDialogViewModel(
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles)
    {
        var styleMap = new Dictionary<string, DepartureGroupColumnStyle>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in currentStyles)
            styleMap[s.SkupinaDnuId] = s;

        foreach (var skupina in availableGroups)
        {
            styleMap.TryGetValue(skupina.Id, out var existing);
            GroupVMs.Add(DepartureGroupColumnVM.FromData(skupina, existing));
        }

        if (GroupVMs.Count > 0) SelectedGroupVM = GroupVMs[0];

        PickHeaderTextColorCommand  = Command(() => PickColor(g => g.HeaderTextColor, (g, c) => g.HeaderTextColor = c));
        PickHeaderBgColorCommand    = Command(() => PickColor(g => g.HeaderBgColor,   (g, c) => g.HeaderBgColor = c));
        PickColumnBgColorCommand    = Command(() => PickColor(g => g.ColumnBgColor,   (g, c) => g.ColumnBgColor = c));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.HeaderTextColor = ""; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.HeaderBgColor = ""; });
        ClearColumnBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.ColumnBgColor = ""; });
    }

    private void PickColor(Func<DepartureGroupColumnVM, string> getter, Action<DepartureGroupColumnVM, string> setter)
    {
        if (SelectedGroupVM is null || RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(SelectedGroupVM));
        if (result is not null)
            setter(SelectedGroupVM, result);
    }

    // ── OK ──────────────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        ResultGroupColumns = GroupVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
