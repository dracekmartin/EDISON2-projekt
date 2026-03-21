using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

// ─── CellStyleVM — sdílený ViewModel pro CellStyle ──────────────────────────

/// <summary>
/// ViewModel pro jednu CellStyle zónu (záhlaví NEBO data).
/// Všechny properties jsou nullable/defaultní — null = dědit z elementu.
/// Reusable across all column-style dialogs.
/// </summary>
public class CellStyleVM : INotifyPropertyChanged
{
    private string  _fontFamily = "";
    private float?  _fontSize;
    private bool    _bold;
    private bool    _italic;
    private string  _textColor = "";
    private string  _bgColor = "";
    private int     _hAlignmentIndex;   // 0=Vlevo, 1=Střed, 2=Vpravo, -1=(nenastaveno)
    private float?  _paddingTop;
    private float?  _paddingRight;
    private float?  _paddingBottom;
    private float?  _paddingLeft;

    public string  FontFamily      { get => _fontFamily;      set { _fontFamily = value;      OnChanged(); } }
    public float?  FontSize        { get => _fontSize;        set { _fontSize = value;        OnChanged(); } }
    public bool    Bold            { get => _bold;            set { _bold = value;            OnChanged(); } }
    public bool    Italic          { get => _italic;          set { _italic = value;          OnChanged(); } }
    public string  TextColor       { get => _textColor;       set { _textColor = value;       OnChanged(); } }
    public string  BgColor         { get => _bgColor;         set { _bgColor = value;         OnChanged(); } }
    public int     HAlignmentIndex { get => _hAlignmentIndex; set { _hAlignmentIndex = value; OnChanged(); } }
    public float?  PaddingTop      { get => _paddingTop;      set { _paddingTop = value;      OnChanged(); } }
    public float?  PaddingRight    { get => _paddingRight;    set { _paddingRight = value;    OnChanged(); } }
    public float?  PaddingBottom   { get => _paddingBottom;   set { _paddingBottom = value;   OnChanged(); } }
    public float?  PaddingLeft     { get => _paddingLeft;     set { _paddingLeft = value;     OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    // ── Mapovací tabulky ──────────────────────────────────────────────────

    private static readonly TextHorizontalAlignment[] HAlignMap =
        [TextHorizontalAlignment.Left, TextHorizontalAlignment.Center, TextHorizontalAlignment.Right];

    // ── Konverze Model ↔ VM ───────────────────────────────────────────────

    public CellStyle? ToModel()
    {
        var s = new CellStyle
        {
            FontFamily      = NullIfEmpty(FontFamily),
            FontSize        = FontSize,
            Bold            = _bold ? true : null,
            Italic          = _italic ? true : null,
            TextColor       = NullIfEmpty(TextColor),
            BackgroundColor = NullIfEmpty(BgColor),
            HAlignment      = _hAlignmentIndex >= 0 && _hAlignmentIndex < HAlignMap.Length ? HAlignMap[_hAlignmentIndex] : null,
            PaddingTop      = PaddingTop,
            PaddingRight    = PaddingRight,
            PaddingBottom   = PaddingBottom,
            PaddingLeft     = PaddingLeft,
        };
        return s.IsEmpty ? null : s;
    }

    public static CellStyleVM FromModel(CellStyle? s, bool defaultBold = false) => new()
    {
        FontFamily      = s?.FontFamily ?? "",
        FontSize        = s?.FontSize,
        Bold            = s?.Bold ?? defaultBold,
        Italic          = s?.Italic ?? false,
        TextColor       = s?.TextColor ?? "",
        BgColor         = s?.BackgroundColor ?? "",
        HAlignmentIndex = s?.HAlignment switch
        {
            TextHorizontalAlignment.Left   => 0,
            TextHorizontalAlignment.Center => 1,
            TextHorizontalAlignment.Right  => 2,
            _ => -1
        },
        PaddingTop    = s?.PaddingTop,
        PaddingRight  = s?.PaddingRight,
        PaddingBottom = s?.PaddingBottom,
        PaddingLeft   = s?.PaddingLeft,
    };

    /// <summary>Resetuje všechny properties na výchozí (null/prázdné).</summary>
    public void Reset()
    {
        FontFamily = "";
        FontSize = null;
        Bold = false;
        Italic = false;
        TextColor = "";
        BgColor = "";
        HAlignmentIndex = -1;
        PaddingTop = null;
        PaddingRight = null;
        PaddingBottom = null;
        PaddingLeft = null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

// ─── DepartureGroupColumnVM — per-column ViewModel ──────────────────────────

public class DepartureGroupColumnVM : INotifyPropertyChanged
{
    private string _skupinaDnuId = "";
    private string _name = "";
    private bool   _isVisible = true;

    public string SkupinaDnuId { get => _skupinaDnuId; set { _skupinaDnuId = value; OnChanged(); } }
    public string Name         { get => _name;         set { _name = value;         OnChanged(); } }
    public bool   IsVisible    { get => _isVisible;    set { _isVisible = value;    OnChanged(); } }

    public CellStyleVM Header { get; set; } = new();
    public CellStyleVM Data { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public DepartureGroupColumnStyle ToModel() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = IsVisible,
        Style = new ColumnVisualStyle
        {
            Header = Header.ToModel(),
            Data = Data.ToModel()
        }
    };

    public static DepartureGroupColumnVM FromData(
        SkupinaDnuData skupina, DepartureGroupColumnStyle? s) => new()
    {
        SkupinaDnuId = skupina.Id,
        Name = skupina.Nadpis + (skupina.Nadpis2.Length > 0 ? " " + skupina.Nadpis2 : ""),
        IsVisible = s?.IsVisible ?? true,
        Header = CellStyleVM.FromModel(s?.Style.Header, defaultBold: true),
        Data = CellStyleVM.FromModel(s?.Style.Data)
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class DepartureGroupEditorDialogViewModel : EDViewModel
{
    // ── Tab 1: Obecné — property descriptors z elementu ──────────────────

    public IReadOnlyList<PropertyDescriptor> GeneralDescriptors { get; }

    // ── Tab 2: Sloupce ──────────────────────────────────────────────────

    public ObservableCollection<DepartureGroupColumnVM> GroupVMs { get; } = [];

    public string[] AlignmentOptions { get; } = ["Vlevo", "Na střed", "Vpravo"];
    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    public List<DepartureGroupColumnStyle> ResultGroupColumns { get; private set; } = [];
    public ColumnVisualStyle? ResultHourColumnStyle { get; private set; }

    /// <summary>Rezervované ID pro sloupec hodin v GroupVMs.</summary>
    internal const string HourColumnId = "__HOUR__";

    // ── Výběr ──────────────────────────────────────────────────────────

    private DepartureGroupColumnVM? _selectedGroupVM;
    public DepartureGroupColumnVM? SelectedGroupVM
    {
        get => _selectedGroupVM;
        set { Set(ref _selectedGroupVM, value); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedGroupVM is not null;

    // ── Callbacks ──────────────────────────────────────────────────────

    public Func<string, string?>? RequestColorPicker { get; set; }

    // ── Commands ──────────────────────────────────────────────────────

    public RelayCommand PickHeaderTextColorCommand { get; }
    public RelayCommand PickHeaderBgColorCommand { get; }
    public RelayCommand PickDataTextColorCommand { get; }
    public RelayCommand PickDataBgColorCommand { get; }
    public RelayCommand ClearHeaderTextColorCommand { get; }
    public RelayCommand ClearHeaderBgColorCommand { get; }
    public RelayCommand ClearDataTextColorCommand { get; }
    public RelayCommand ClearDataBgColorCommand { get; }
    public RelayCommand ResetStyleCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    // ── Konstruktor ──────────────────────────────────────────────────

    public DepartureGroupEditorDialogViewModel(
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles,
        ColumnVisualStyle? hourColumnStyle,
        IReadOnlyList<PropertyDescriptor> generalDescriptors)
    {
        GeneralDescriptors = generalDescriptors;

        // Sloupec hodin — vždy první v seznamu
        GroupVMs.Add(new DepartureGroupColumnVM
        {
            SkupinaDnuId = HourColumnId,
            Name = "Hodiny",
            IsVisible = true,
            Header = CellStyleVM.FromModel(hourColumnStyle?.Header, defaultBold: true),
            Data = CellStyleVM.FromModel(hourColumnStyle?.Data)
        });

        var styleMap = new Dictionary<string, DepartureGroupColumnStyle>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in currentStyles)
            styleMap[s.SkupinaDnuId] = s;

        foreach (var skupina in availableGroups)
        {
            styleMap.TryGetValue(skupina.Id, out var existing);
            GroupVMs.Add(DepartureGroupColumnVM.FromData(skupina, existing));
        }

        if (GroupVMs.Count > 0) SelectedGroupVM = GroupVMs[0];

        // Color picker commands — záhlaví
        PickHeaderTextColorCommand  = Command(() => PickColor(g => g.Header.TextColor, (g, c) => g.Header.TextColor = c));
        PickHeaderBgColorCommand    = Command(() => PickColor(g => g.Header.BgColor,   (g, c) => g.Header.BgColor = c));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Header.TextColor = ""; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Header.BgColor = ""; });

        // Color picker commands — data
        PickDataTextColorCommand  = Command(() => PickColor(g => g.Data.TextColor, (g, c) => g.Data.TextColor = c));
        PickDataBgColorCommand    = Command(() => PickColor(g => g.Data.BgColor,   (g, c) => g.Data.BgColor = c));
        ClearDataTextColorCommand = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Data.TextColor = ""; });
        ClearDataBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Data.BgColor = ""; });

        // Reset
        ResetStyleCommand = Command(() =>
        {
            if (SelectedGroupVM is null) return;
            SelectedGroupVM.Header.Reset();
            SelectedGroupVM.Data.Reset();
        });

        // Move up/down (sloupec Hodiny zůstává vždy na indexu 0)
        MoveUpCommand = Command(() =>
        {
            if (SelectedGroupVM is null) return;
            var idx = GroupVMs.IndexOf(SelectedGroupVM);
            if (idx <= 1) return; // 0 = Hodiny (nepřesouvat), 1 = první skupina (nejde výš)
            GroupVMs.Move(idx, idx - 1);
        });
        MoveDownCommand = Command(() =>
        {
            if (SelectedGroupVM is null) return;
            if (SelectedGroupVM.SkupinaDnuId == HourColumnId) return; // Hodiny nepřesouvat
            var idx = GroupVMs.IndexOf(SelectedGroupVM);
            if (idx < 0 || idx >= GroupVMs.Count - 1) return;
            GroupVMs.Move(idx, idx + 1);
        });
    }

    private void PickColor(Func<DepartureGroupColumnVM, string> getter, Action<DepartureGroupColumnVM, string> setter)
    {
        if (SelectedGroupVM is null || RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(SelectedGroupVM));
        if (result is not null)
            setter(SelectedGroupVM, result);
    }

    // ── OK ──────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        var hourVm = GroupVMs.FirstOrDefault(vm => vm.SkupinaDnuId == HourColumnId);
        if (hourVm is not null)
        {
            var style = new ColumnVisualStyle { Header = hourVm.Header.ToModel(), Data = hourVm.Data.ToModel() };
            ResultHourColumnStyle = style.IsEmpty ? null : style;
        }

        ResultGroupColumns = GroupVMs
            .Where(vm => vm.SkupinaDnuId != HourColumnId)
            .Select(vm => vm.ToModel())
            .ToList();
        CloseDialog(true);
    }
}
