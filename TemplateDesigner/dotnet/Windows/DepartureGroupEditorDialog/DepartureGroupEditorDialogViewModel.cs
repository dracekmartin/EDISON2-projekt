using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

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
        IsVisible = IsVisible,
        AlternatingRowColor = string.IsNullOrWhiteSpace(AlternatingRowColor) ? null : AlternatingRowColor,
        Style = new ColumnVisualStyle
        {
            Header = Header.ToModel(),
            Data = Data.ToModel()
        }
    };

    public static DepartureGroupColumnVM FromData(
        SkupinaDnuData skupina, DepartureGroupColumnStyle? s,
        TextStyle? headerDefaults = null, TextStyle? dataDefaults = null) => new()
    {
        SkupinaDnuId = skupina.Id,
        Name = skupina.Nadpis + (skupina.Nadpis2.Length > 0 ? " " + skupina.Nadpis2 : ""),
        IsVisible = s?.IsVisible ?? true,
        AlternatingRowColor = s?.AlternatingRowColor ?? "",
        Header = CellStyleVM.FromModel(s?.Style.Header, defaultBold: true,
            defaultFontFamily: headerDefaults?.FontFamily, defaultFontSize: headerDefaults?.FontSize),
        Data = CellStyleVM.FromModel(s?.Style.Data,
            defaultFontFamily: dataDefaults?.FontFamily, defaultFontSize: dataDefaults?.FontSize)
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class DepartureGroupEditorDialogViewModel : EDViewModel
{
    // ── Tab 1: Obecné — property descriptors z elementu ──────────────────

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
        IReadOnlyList<PropertyDescriptor> generalDescriptors,
        TextStyle? headerDefaults = null,
        TextStyle? dataDefaults = null)
    {
        GeneralDescriptors = generalDescriptors;

        // Sloupec hodin — vždy první v seznamu
        GroupVMs.Add(new DepartureGroupColumnVM
        {
            SkupinaDnuId = HourColumnId,
            Name = "Hodiny",
            IsVisible = true,
            Header = CellStyleVM.FromModel(hourColumnStyle?.Header, defaultBold: true,
                defaultFontFamily: headerDefaults?.FontFamily, defaultFontSize: headerDefaults?.FontSize),
            Data = CellStyleVM.FromModel(hourColumnStyle?.Data,
                defaultFontFamily: dataDefaults?.FontFamily, defaultFontSize: dataDefaults?.FontSize)
        });

        var groupIds = availableGroups.Select(g => g.Id).ToList();
        var styleMap = GroupColumnIndexResolver.BuildStyleLookup(currentStyles, groupIds);

        foreach (var skupina in availableGroups)
        {
            styleMap.TryGetValue(skupina.Id, out var existing);
            GroupVMs.Add(DepartureGroupColumnVM.FromData(skupina, existing, headerDefaults, dataDefaults));
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
