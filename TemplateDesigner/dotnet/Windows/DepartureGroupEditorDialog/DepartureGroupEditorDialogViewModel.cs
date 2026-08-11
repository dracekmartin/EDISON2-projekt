using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── DepartureGroupColumnVM - per-column ViewModel ──────────────────────────

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

    // IColumnRowVM - sdílený seznam sloupců (efektivní záhlaví = override, jinak zdroj; šířka pin/auto)
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

// ─── CustomColumnVM - řádek definice vlastního sloupce (tab „Definice sloupců") ──

public class CustomColumnVM : INotifyPropertyChanged
{
    public string Id { get; init; } = "";

    private string _header = "";
    public string Header { get => _header; set { _header = value; OnChanged(); OnChanged(nameof(DisplayName)); } }

    private string _header2 = "";
    public string Header2 { get => _header2; set { _header2 = value; OnChanged(); } }

    // Dny v týdnu - zaškrtávátka 1:1 s abecedou platnosti (1-7, + = svátek)
    private bool _po, _ut, _st, _ct, _pa, _so, _ne, _svatek;
    public bool Po     { get => _po;     set { _po = value;     OnChanged(); } }
    public bool Ut     { get => _ut;     set { _ut = value;     OnChanged(); } }
    public bool St     { get => _st;     set { _st = value;     OnChanged(); } }
    public bool Ct     { get => _ct;     set { _ct = value;     OnChanged(); } }
    public bool Pa     { get => _pa;     set { _pa = value;     OnChanged(); } }
    public bool So     { get => _so;     set { _so = value;     OnChanged(); } }
    public bool Ne     { get => _ne;     set { _ne = value;     OnChanged(); } }
    public bool Svatek { get => _svatek; set { _svatek = value; OnChanged(); } }

    private string _includeMarks = "";
    public string IncludeMarks { get => _includeMarks; set { _includeMarks = value; OnChanged(); } }

    private string _excludeMarks = "";
    public string ExcludeMarks { get => _excludeMarks; set { _excludeMarks = value; OnChanged(); } }

    private string _suppressedMarks = "";
    public string SuppressedMarks { get => _suppressedMarks; set { _suppressedMarks = value; OnChanged(); } }

    private string _period = "";
    public string Period { get => _period; set { _period = value; OnChanged(); } }

    public string DisplayName => string.IsNullOrWhiteSpace(Header) ? "(bez nadpisu)" : Header;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public DepartureColumnDefinition ToModel() => new()
    {
        Id = Id,
        Header = Header.Trim(),
        Header2 = Header2.Trim(),
        Days = (Po ? "1" : "") + (Ut ? "2" : "") + (St ? "3" : "") + (Ct ? "4" : "")
             + (Pa ? "5" : "") + (So ? "6" : "") + (Ne ? "7" : "") + (Svatek ? "+" : ""),
        IncludeMarks = IncludeMarks.Trim(),
        ExcludeMarks = ExcludeMarks.Trim(),
        SuppressedMarks = SuppressedMarks.Trim(),
        Period = Period.Trim()
    };

    public static CustomColumnVM FromModel(DepartureColumnDefinition d) => new()
    {
        Id = d.Id,
        Header = d.Header,
        Header2 = d.Header2,
        Po = d.Days.Contains('1'), Ut = d.Days.Contains('2'), St = d.Days.Contains('3'),
        Ct = d.Days.Contains('4'), Pa = d.Days.Contains('5'), So = d.Days.Contains('6'),
        Ne = d.Days.Contains('7'), Svatek = d.Days.Contains('+'),
        IncludeMarks = d.IncludeMarks,
        ExcludeMarks = d.ExcludeMarks,
        SuppressedMarks = d.SuppressedMarks,
        Period = d.Period
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class DepartureGroupEditorDialogViewModel : EDViewModel
{
    private readonly DepartureGridElement _element;

    // Tab „Obecné" = embedded EDInputForm (DepartureGridElementEdit.cls) - řeší code-behind přes
    // ElementFormBinder (reflexe). VM drží jen Tab „Sloupce" (per-skupina/sloupec styly).

    // ── Tab 2: Sloupce ──────────────────────────────────────────────────

    public ObservableCollection<DepartureGroupColumnVM> GroupVMs { get; } = [];

    // ── UI omezení editace - co CellStyleEditor zobrazí per zóna ─────────
    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;
    public CellStyleEditOptions DataEditOptions   { get; } = CellStyleEditOptions.AllEnabled;

    public List<DepartureGroupColumnStyle> ResultGroupColumns { get; private set; } = [];
    public ColumnVisualStyle? ResultHourColumnStyle { get; private set; }

    // ── Tab 3: Definice sloupců (vlastní sloupce - dny + značky) ─────────

    public ObservableCollection<CustomColumnVM> CustomColumnVMs { get; } = [];

    private bool _useCustomColumns;
    public bool UseCustomColumns
    {
        get => _useCustomColumns;
        set { if (Set(ref _useCustomColumns, value)) OnPropertyChanged(nameof(AutomaticColumns)); }
    }

    /// <summary>Protějšek pro RadioButton „Automaticky" (bez converteru v XAML).</summary>
    public bool AutomaticColumns
    {
        get => !_useCustomColumns;
        set => UseCustomColumns = !value;
    }

    private CustomColumnVM? _selectedCustomVM;
    public CustomColumnVM? SelectedCustomVM
    {
        get => _selectedCustomVM;
        set { Set(ref _selectedCustomVM, value); OnPropertyChanged(nameof(HasCustomSelection)); }
    }

    public bool HasCustomSelection => _selectedCustomVM is not null;
    public bool CanAddCustomColumn => CustomColumnVMs.Count < DepartureGridElement.MaxCustomColumns;

    /// <summary>Ukázat tab „Vlastní definice sloupců"? Featura jde do verze zhasnutá (#13045b),
    /// rozsvítí ji lokální config. Skrytý tab definici v šabloně nemaže - OK ji vrátí beze změny.</summary>
    public bool ShowCustomColumnsTab => Config.AppSettings.CustomDepartureColumnsEnabled;

    /// <summary>Nabídka vzorů + prázdná varianta pro stavbu od začátku. Vzory drží server
    /// (pole PRESETY_SLOUPCU), takže bez načtených dat zůstane jen prázdná varianta - vzor
    /// složený naslepo by u prázdninových rozvržení neseděl na značky konkrétní linky.</summary>
    public IReadOnlyList<DepartureColumnPresets.Preset> Presets { get; }

    private DepartureColumnPresets.Preset? _selectedPreset;
    public DepartureColumnPresets.Preset? SelectedPreset
    {
        get => _selectedPreset;
        set => Set(ref _selectedPreset, value);
    }

    /// <summary>Výsledná definice: null = „Automaticky (dle linky)".</summary>
    public List<DepartureColumnDefinition>? ResultCustomColumns { get; private set; }

    private string? _customColumnsError;
    /// <summary>Chyba validace definice sloupců - OK dialog nezavře, dokud trvá. Null = v pořádku.</summary>
    public string? CustomColumnsError
    {
        get => _customColumnsError;
        private set => Set(ref _customColumnsError, value);
    }

    private readonly IReadOnlyDictionary<string, List<DepartureColumnDefinition>> _presety;

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

    // Definice sloupců (tab 3)
    public RelayCommand AddCustomColumnCommand { get; }
    public RelayCommand RemoveCustomColumnCommand { get; }
    public RelayCommand MoveCustomUpCommand { get; }
    public RelayCommand MoveCustomDownCommand { get; }
    public RelayCommand ApplyPresetCommand { get; }

    // ── Konstruktor ──────────────────────────────────────────────────

    public DepartureGroupEditorDialogViewModel(
        DepartureGridElement element,
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles,
        ColumnVisualStyle? hourColumnStyle,
        List<DepartureColumnDefinition>? customColumns = null,
        string? presetsJson = null)
    {
        _element = element;
        _presety = DepartureColumnPresets.Parse(presetsJson);
        Presets = [.. DepartureColumnPresets.All.Where(p => _presety.ContainsKey(p.Key)),
                   new("PRAZDNA", "Prázdná (sloupce si přidáte sami)")];

        // Tab 3: definice vlastních sloupců (null/prázdné = automaticky dle linky)
        foreach (var c in customColumns ?? []) CustomColumnVMs.Add(CustomColumnVM.FromModel(c));
        _useCustomColumns = CustomColumnVMs.Count > 0;
        SelectedCustomVM = CustomColumnVMs.FirstOrDefault();
        // Když aktuální sloupce odpovídají některému vzoru, předvyber ho - jinak prázdné combo
        // vypadá, jako by se použitý vzor neuložil (stav definice nese seznam sloupců, ne combo).
        SelectedPreset = DetectPreset();

        AddCustomColumnCommand = Command(() =>
        {
            if (!CanAddCustomColumn) return;
            var vm = new CustomColumnVM { Id = NextFreeCustomId(), Header = "Nový sloupec" };
            CustomColumnVMs.Add(vm);
            SelectedCustomVM = vm;
            OnPropertyChanged(nameof(CanAddCustomColumn));
        });
        RemoveCustomColumnCommand = Command(() =>
        {
            if (SelectedCustomVM is null) return;
            var idx = CustomColumnVMs.IndexOf(SelectedCustomVM);
            CustomColumnVMs.Remove(SelectedCustomVM);
            SelectedCustomVM = CustomColumnVMs.Count > 0 ? CustomColumnVMs[Math.Min(idx, CustomColumnVMs.Count - 1)] : null;
            OnPropertyChanged(nameof(CanAddCustomColumn));
        });
        MoveCustomUpCommand = Command(() =>
        {
            if (SelectedCustomVM is null) return;
            var idx = CustomColumnVMs.IndexOf(SelectedCustomVM);
            if (idx > 0) CustomColumnVMs.Move(idx, idx - 1);
        });
        MoveCustomDownCommand = Command(() =>
        {
            if (SelectedCustomVM is null) return;
            var idx = CustomColumnVMs.IndexOf(SelectedCustomVM);
            if (idx >= 0 && idx < CustomColumnVMs.Count - 1) CustomColumnVMs.Move(idx, idx + 1);
        });
        ApplyPresetCommand = Command(() =>
        {
            if (SelectedPreset is null) return;
            CustomColumnVMs.Clear();
            foreach (var c in _presety.TryGetValue(SelectedPreset.Key, out var vzor) ? vzor : [])
                CustomColumnVMs.Add(CustomColumnVM.FromModel(c.Clone()));
            SelectedCustomVM = CustomColumnVMs.FirstOrDefault();
            UseCustomColumns = true;
            OnPropertyChanged(nameof(CanAddCustomColumn));
        });

        // Vyřešené rodičovské styly pro kaskádu (element-level → column override)
        var hourHeaderInherited  = element.ResolveHourHeaderParent();
        var hourDataInherited    = element.ResolveHourDataParent();
        var groupHeaderInherited = element.ResolveGroupHeaderParent();
        var groupDataInherited   = element.ResolveGroupMinuteParent();

        // Sloupec hodin - vždy první v seznamu
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

        // Color picker commands - záhlaví
        PickHeaderTextColorCommand  = Command(() => PickColor(g => g.Header.TextColor, (g, c) => g.Header.TextColor = c));
        PickHeaderBgColorCommand    = Command(() => PickColor(g => g.Header.BgColor,   (g, c) => g.Header.BgColor   = c));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Header.TextColor = null; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedGroupVM is not null) SelectedGroupVM.Header.BgColor   = null; });

        // Color picker commands - data
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

        // Reset všech sloupců - smaže per-column overrides, všechny sloupce budou dědit z Obecné
        ResetAllColumnStylesCommand = Command(() =>
        {
            foreach (var vm in GroupVMs)
            {
                vm.Header.Reset();
                vm.Data.Reset();
            }
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

    /// <summary>Vzor, kterému aktuální sloupce přesně odpovídají (nadpisy, dny i značky); null =
    /// vlastní kombinace. Porovnává se proti vzoru postavenému z konfigurace téže linky.</summary>
    private DepartureColumnPresets.Preset? DetectPreset()
    {
        if (CustomColumnVMs.Count == 0) return null;
        foreach (var preset in Presets)
        {
            if (!_presety.TryGetValue(preset.Key, out var built)) continue;
            if (built.Count != CustomColumnVMs.Count) continue;
            var match = true;
            for (int i = 0; i < built.Count && match; i++)
            {
                var a = built[i];
                var b = CustomColumnVMs[i].ToModel();
                match = a.Header == b.Header && a.Header2 == b.Header2 && a.Days == b.Days
                     && a.IncludeMarks == b.IncludeMarks && a.ExcludeMarks == b.ExcludeMarks
                     && a.SuppressedMarks == b.SuppressedMarks && a.Period == b.Period;
            }
            if (match) return preset;
        }
        return null;
    }

    /// <summary>Nejmenší volné číselné id - stabilní přes přeskládání (styly sloupců na něm visí),
    /// u vzorů 1:1 s dnešními indexy sloupců, takže existující styly zůstanou na svém místě.
    /// Novým sloupcům se vyhýbá i id s uloženým stylem (jinak by nový sloupec po smazaném
    /// zdědil jeho vzhled).</summary>
    private string NextFreeCustomId()
    {
        var used = CustomColumnVMs.Select(vm => vm.Id)
            .Concat(_element.GroupColumns.Select(gc => gc.SkupinaDnuId))
            .ToHashSet(StringComparer.Ordinal);
        for (int i = 1; ; i++)
            if (!used.Contains(i.ToString())) return i.ToString();
    }

    // ── OK ──────────────────────────────────────────────────────────

    /// <summary>Seznam značek smí být jen čísla oddělená čárkou. Jiný vstup by server tiše přečetl
    /// po svém („abc" jako 0, z „10;20" i „52 53" jen první číslo) - proto se OK zastaví tady.</summary>
    private static bool JsouPlatneZnacky(string seznam)
    {
        if (string.IsNullOrWhiteSpace(seznam)) return true;
        return seznam.Split(',').All(p =>
        {
            var cislo = p.Trim();
            return cislo.Length == 0 || cislo.All(char.IsDigit);   // uvnitř čísla už nesmí být nic, ani mezera
        });
    }

    /// <summary>Období smí být jen rozsahy dat oddělené čárkou („14.2.-18.2., 1.7.-31.8.",
    /// s rokem i bez). Server nerozpoznaný rozsah tiše zahodí, takže by sloupec mlčky bral všechny
    /// spoje - proto se OK zastaví tady.</summary>
    internal static bool JePlatneObdobi(string obdobi)
    {
        if (string.IsNullOrWhiteSpace(obdobi)) return true;
        foreach (var cast in obdobi.Split(','))
        {
            var rozsah = cast.Trim();
            if (rozsah.Length == 0) continue;
            var meze = rozsah.Split('-');
            if (meze.Length > 2) return false;
            foreach (var mez in meze)
                if (!JeDatum(mez.Trim())) return false;
        }
        return true;
    }

    /// <summary>Datum „d.m", „d.m." nebo „d.m.rrrr" - rok nepovinný (bez roku = každý rok).
    /// Zrcadlí <c>JeDatumTvar</c> v ZastavkoveJrAd.cls; kdyby se rozešly, editor by pustil
    /// rozsah, který server zahodí.</summary>
    private static bool JeDatum(string text)
    {
        if (text.Length == 0) return false;
        var casti = text.Split('.');
        if (casti.Length is < 2 or > 3) return false;
        if (!int.TryParse(casti[0], out var den) || den is < 1 or > 31) return false;
        if (!int.TryParse(casti[1], out var mesic) || mesic is < 1 or > 12) return false;
        if (casti.Length == 3 && casti[2].Length > 0)
            if (!int.TryParse(casti[2], out var rok) || rok is < 1900 or > 2999) return false;
        return true;
    }

    protected override void OnOk()
    {
        // Validace definice sloupců - nevalidní seznam značek ani období nepustit na server
        if (UseCustomColumns)
        {
            var chybny = CustomColumnVMs.FirstOrDefault(vm =>
                !JsouPlatneZnacky(vm.IncludeMarks) || !JsouPlatneZnacky(vm.ExcludeMarks) || !JsouPlatneZnacky(vm.SuppressedMarks));
            if (chybny is not null)
            {
                CustomColumnsError = $"Sloupec „{chybny.DisplayName}“: seznamy značek smí obsahovat jen čísla oddělená čárkou (např. 52, 53).";
                return;
            }
            var chybneObdobi = CustomColumnVMs.FirstOrDefault(vm => !JePlatneObdobi(vm.Period));
            if (chybneObdobi is not null)
            {
                CustomColumnsError = $"Sloupec „{chybneObdobi.DisplayName}“: období smí být jen rozsahy dat oddělené čárkou (např. 1.7.-31.8., 29.10.-30.10.).";
                return;
            }
        }
        CustomColumnsError = null;

        // Element-level styl (HeaderStyle/DataStyle/MinuteStyle/…) řeší EDInputForm v tabu Obecné
        // (code-behind přes ElementFormBinder). Zde jen per-skupina/sloupec + hour.
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

        // Definice vlastních sloupců - null = automaticky (vypnuté, nebo zapnuté bez sloupců)
        ResultCustomColumns = UseCustomColumns && CustomColumnVMs.Count > 0
            ? CustomColumnVMs.Select(vm => vm.ToModel()).ToList()
            : null;

        // Pozn.: per-column typografii záhlaví (font/velikost/řez) ignoruje render
        // (DepartureGridElement.WithParentHeaderTypography) - element-level je autoritativní.
        // Per-column nese jen barvy a zarovnání. Není potřeba nic mazat.

        CloseDialog(true);
    }
}
