using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

// ─── DepartureGroupColumnVM — per-column ViewModel ──────────────────────────

public class DepartureGroupColumnVM : INotifyPropertyChanged, Controls.IColumnRowVM
{
    private string _skupinaDnuId = "";
    private string _name = "";
    private bool   _visible = true;

    private string _headerOverride = "";
    private float? _widthOverride;

    public string SkupinaDnuId { get => _skupinaDnuId; set { _skupinaDnuId = value; OnChanged(); } }
    public string Name         { get => _name;         set { _name = value;         OnChanged(); OnChanged(nameof(DisplayName)); } }
    public bool   Visible      { get => _visible;      set { _visible = value;      OnChanged(); } }

    /// <summary>Override nadpisu sloupce v šabloně. Prázdné = výchozí (nadpis z dat / „Hod").</summary>
    public string HeaderOverride { get => _headerOverride; set { _headerOverride = value; OnChanged(); OnChanged(nameof(DisplayName)); } }

    /// <summary>Pevná šířka skupiny v mm (override). Null = auto (z obsahu + proporční dofill).</summary>
    public float? WidthOverride { get => _widthOverride; set { _widthOverride = value; OnChanged(); OnChanged(nameof(WidthText)); OnChanged(nameof(ColumnWidth)); } }

    /// <summary>Šířka jako text pro editor (prázdné = auto). Parsuje na <see cref="WidthOverride"/>.</summary>
    public string WidthText
    {
        get => Controls.ColumnWidthText.Format(_widthOverride);
        set => WidthOverride = Controls.ColumnWidthText.Parse(value);
    }

    // IColumnRowVM — sdílený seznam sloupců (efektivní záhlaví = override, jinak zdroj; šířka pin/auto)
    public string DisplayName => string.IsNullOrWhiteSpace(HeaderOverride) ? Name : HeaderOverride;
    public float? ColumnWidth => WidthOverride;

    private CellStyleVM _header = null!;
    public CellStyleVM Header
    {
        get => _header;
        init { _header = value; _header.PropertyChanged += OnChildChanged; }
    }

    private CellStyleVM _data = null!;
    public CellStyleVM Data
    {
        get => _data;
        init { _data = value; _data.PropertyChanged += OnChildChanged; }
    }

    /// <summary>True pokud sloupec má aspoň jeden per-column override v Header nebo Data.</summary>
    public bool HasAnyOverride => Header.HasAnyExplicit || Data.HasAnyExplicit;

    private void OnChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CellStyleVM.HasAnyExplicit))
            OnChanged(nameof(HasAnyOverride));
    }

    private string _alternatingRowColor = "";
    public string AlternatingRowColor
    {
        get => _alternatingRowColor;
        set { _alternatingRowColor = value; OnChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public DepartureGroupColumnStyle ToModel() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = Visible,
        AlternatingRowColor = string.IsNullOrWhiteSpace(AlternatingRowColor) ? null : AlternatingRowColor,
        HeaderOverride = string.IsNullOrWhiteSpace(HeaderOverride) ? null : HeaderOverride,
        WidthOverride = WidthOverride,
        Style = new ColumnVisualStyle
        {
            Header = Header.ToModel(),
            Data = Data.ToModel()
        }
    };

    public static DepartureGroupColumnVM FromData(
        SkupinaDnuData skupina, DepartureGroupColumnStyle? s,
        ResolvedTextStyle headerInherited, ResolvedTextStyle dataInherited) => new()
    {
        SkupinaDnuId = skupina.Id,
        Name = skupina.Nadpis + (skupina.Nadpis2.Length > 0 ? " " + skupina.Nadpis2 : ""),
        Visible = s?.IsVisible ?? true,
        AlternatingRowColor = s?.AlternatingRowColor ?? "",
        HeaderOverride = s?.HeaderOverride ?? "",
        WidthOverride = s?.WidthOverride,
        Header = CellStyleVM.FromModel(s?.Style.Header, headerInherited),
        Data   = CellStyleVM.FromModel(s?.Style.Data,   dataInherited),
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class DepartureGroupEditorDialogViewModel : EDViewModel
{
    private readonly DepartureGridElement _element;

    // ── Tab 1: Obecné — element-level styly + property descriptors ───────

    /// <summary>Výchozí styl záhlaví elementu (kaskáda: per-column → tento → ScheduleHeaderDefaults).</summary>
    public CellStyleVM ElementHeaderStyle { get; }

    /// <summary>Výchozí styl dat elementu (kaskáda: per-column → tento → ScheduleDataDefaults).</summary>
    public CellStyleVM ElementDataStyle { get; }

    public IReadOnlyList<PropertyDescriptor> GeneralDescriptors { get; }

    // ── Tab 2: Sloupce ──────────────────────────────────────────────────

    public ObservableCollection<DepartureGroupColumnVM> GroupVMs { get; } = [];

    // ── UI omezení editace — co CellStyleEditor zobrazí per zóna ─────────
    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;
    public CellStyleEditOptions DataEditOptions   { get; } = CellStyleEditOptions.AllEnabled;

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

    // Per-column (Sloupce tab)
    public RelayCommand PickHeaderTextColorCommand { get; }
    public RelayCommand PickHeaderBgColorCommand { get; }
    public RelayCommand PickDataTextColorCommand { get; }
    public RelayCommand PickDataBgColorCommand { get; }
    public RelayCommand ClearHeaderTextColorCommand { get; }
    public RelayCommand ClearHeaderBgColorCommand { get; }
    public RelayCommand ClearDataTextColorCommand { get; }
    public RelayCommand ClearDataBgColorCommand { get; }
    public RelayCommand ResetStyleCommand { get; }
    public RelayCommand ResetAllColumnStylesCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

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

    // ── Konstruktor ──────────────────────────────────────────────────

    public DepartureGroupEditorDialogViewModel(
        DepartureGridElement element,
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles,
        ColumnVisualStyle? hourColumnStyle,
        IReadOnlyList<PropertyDescriptor> generalDescriptors)
    {
        _element = element;
        GeneralDescriptors = generalDescriptors;

        // Element-level CellStyleVM (Obecné tab) — parent je ScheduleHeaderDefaults/ScheduleDataDefaults
        ElementHeaderStyle = CellStyleVM.FromModel(element.HeaderStyle, element.ResolveElementHeaderParent());
        ElementDataStyle   = CellStyleVM.FromModel(element.DataStyle,   element.ResolveElementDataParent());

        // Vyřešené rodičovské styly pro kaskádu (element-level → column override)
        var hourHeaderInherited  = element.ResolveHourHeaderParent();
        var hourDataInherited    = element.ResolveHourDataParent();
        var groupHeaderInherited = element.ResolveGroupHeaderParent();
        var groupDataInherited   = element.ResolveGroupMinuteParent();

        // Sloupec hodin — vždy první v seznamu
        GroupVMs.Add(new DepartureGroupColumnVM
        {
            SkupinaDnuId = HourColumnId,
            Name = "Hodiny",
            HeaderOverride = element.HourColumnHeader,   // override z elementu (prázdné = „Hod")
            WidthOverride = element.HourColumnWidth,      // sloupec hodin má vždy pevnou šířku (element-level)
            Visible = true,
            Header = CellStyleVM.FromModel(hourColumnStyle?.Header, hourHeaderInherited),
            Data   = CellStyleVM.FromModel(hourColumnStyle?.Data,   hourDataInherited),
        });

        var groupIds = availableGroups.Select(g => g.Id).ToList();
        var styleMap = GroupColumnIndexResolver.BuildStyleLookup(currentStyles, groupIds);

        // Pořadí skupin dle uloženého GroupColumns (uživatelské ▲▼ přežije reopen dialogu);
        // skupiny mimo seznam zachovají datové pořadí na konci. Zrcadlí DepartureGridElement.ResolveSkupiny.
        var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < currentStyles.Count; i++)
            if (!orderMap.ContainsKey(currentStyles[i].SkupinaDnuId))
                orderMap[currentStyles[i].SkupinaDnuId] = i;

        var orderedGroups = availableGroups
            .Select((g, dataIdx) => (g, key: orderMap.TryGetValue(g.Id, out var oi) ? oi : int.MaxValue, dataIdx))
            .OrderBy(t => t.key).ThenBy(t => t.dataIdx)
            .Select(t => t.g);

        foreach (var skupina in orderedGroups)
        {
            styleMap.TryGetValue(skupina.Id, out var existing);
            GroupVMs.Add(DepartureGroupColumnVM.FromData(skupina, existing, groupHeaderInherited, groupDataInherited));
        }

        if (GroupVMs.Count > 0) SelectedGroupVM = GroupVMs[0];

        // Color picker commands — záhlaví
        PickHeaderTextColorCommand  = Command(() => PickColor(g => g.Header.TextColor, (g, c) => g.Header.TextColor = c));
        PickHeaderBgColorCommand    = Command(() => PickColor(g => g.Header.BgColor,   (g, c) => g.Header.BgColor   = c));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Header.TextColor = null; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Header.BgColor   = null; });

        // Color picker commands — data
        PickDataTextColorCommand  = Command(() => PickColor(g => g.Data.TextColor, (g, c) => g.Data.TextColor = c));
        PickDataBgColorCommand    = Command(() => PickColor(g => g.Data.BgColor,   (g, c) => g.Data.BgColor   = c));
        ClearDataTextColorCommand = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Data.TextColor = null; });
        ClearDataBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Data.BgColor   = null; });

        // Reset vybraného sloupce
        ResetStyleCommand = Command(() =>
        {
            if (SelectedGroupVM is null) return;
            SelectedGroupVM.Header.Reset();
            SelectedGroupVM.Data.Reset();
        });

        // Reset všech sloupců — smaže per-column overrides, všechny sloupce budou dědit z Obecné
        ResetAllColumnStylesCommand = Command(() =>
        {
            foreach (var vm in GroupVMs)
            {
                vm.Header.Reset();
                vm.Data.Reset();
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

    private void PickColor(Func<DepartureGroupColumnVM, string?> getter, Action<DepartureGroupColumnVM, string?> setter)
    {
        if (SelectedGroupVM is null || RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(SelectedGroupVM) ?? "");
        if (result is not null)
            setter(SelectedGroupVM, result);
    }

    private void PickElementColor(CellStyleVM vm, Func<CellStyleVM, string?> getter, Action<CellStyleVM, string?> setter)
    {
        if (RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(vm) ?? "");
        if (result is not null)
            setter(vm, result);
    }

    // ── OK ──────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        // Element-level styl — zápis zpět do elementu (ScheduleElementBase.HeaderStyle/DataStyle)
        _element.HeaderStyle = ElementHeaderStyle.ToModel() ?? new TextStyle();
        _element.DataStyle   = ElementDataStyle.ToModel()   ?? new TextStyle();

        var hourVm = GroupVMs.FirstOrDefault(vm => vm.SkupinaDnuId == HourColumnId);
        if (hourVm is not null)
        {
            var style = new ColumnVisualStyle { Header = hourVm.Header.ToModel(), Data = hourVm.Data.ToModel() };
            ResultHourColumnStyle = style.IsEmpty ? null : style;
            _element.HourColumnHeader = hourVm.HeaderOverride?.Trim() ?? "";
            // Sloupec hodin má pevnou šířku (element-level); prázdné pole ponechá stávající.
            if (hourVm.WidthOverride is float hw && hw > 0) _element.HourColumnWidth = hw;
        }

        ResultGroupColumns = GroupVMs
            .Where(vm => vm.SkupinaDnuId != HourColumnId)
            .Select(vm => vm.ToModel())
            .ToList();

        // Pozn.: per-column typografii záhlaví (font/velikost/řez) ignoruje render
        // (DepartureGridElement.WithParentHeaderTypography) — element-level je autoritativní.
        // Per-column nese jen barvy a zarovnání. Není potřeba nic mazat.

        CloseDialog(true);
    }
}
