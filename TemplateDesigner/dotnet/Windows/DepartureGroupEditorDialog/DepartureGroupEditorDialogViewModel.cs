using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── DepartureGroupColumnVM - per-column ViewModel ──────────────────────────

public class DepartureGroupColumnVM : Controls.ColumnRowVM
{
    private string _alternatingRowColor = "";

    private DepartureGroupColumnVM(string skupinaDnuId, string name, CellStyleVM headerStyle, CellStyleVM dataStyle)
        : base(headerStyle, dataStyle)
    {
        SkupinaDnuId = skupinaDnuId;
        Name = name;
    }

    public string SkupinaDnuId { get; }

    /// <summary>Název skupiny dnů z dat (u hodin „Hodiny"). Seznam ho ukazuje i po přepisu záhlaví,
    /// ať je vidět, o kterou skupinu jde - jako čitelné názvy sloupců ostatních tabulek.</summary>
    public string Name { get; }

    public override string DisplayName => Name;

    public bool IsHourColumn => SkupinaDnuId == DepartureGroupEditorDialogViewModel.HourColumnId;

    /// <summary>Hodiny jsou osa minut: tisknou se vždy a vždy první. Do 28. 10. šly odškrtnout,
    /// OK zaškrtnutí ignorovalo a po otevření bylo zase zapnuté.</summary>
    public override bool CanHide => !IsHourColumn;
    public override bool CanMove => !IsHourColumn;

    /// <summary>Šířka hodin je vlastnost prvku a je vždy pevná; prázdné pole ji nechá, jaká je.</summary>
    public override string? WidthHint => IsHourColumn
        ? "Sloupec hodin má vždy pevnou šířku. Prázdné pole ji nezmění."
        : null;

    /// <summary>Má sloupec střídání řádků? Sloupec hodin ne - zebra se kreslí per skupina dnů
    /// a hodinový pruh si barvu bere od nich, takže nastavená hodnota by se nikde neprojevila.</summary>
    public bool SupportsZebra => !IsHourColumn;

    public string AlternatingRowColor
    {
        get => _alternatingRowColor;
        set { _alternatingRowColor = value; OnChanged(); }
    }

    public DepartureGroupColumnStyle ToModel() => new()
    {
        SkupinaDnuId = SkupinaDnuId,
        IsVisible = Visible,
        AlternatingRowColor = string.IsNullOrWhiteSpace(AlternatingRowColor) ? null : AlternatingRowColor,
        HeaderOverride = string.IsNullOrWhiteSpace(Header) ? null : Header,
        WidthOverride = Width,
        Style = new ColumnVisualStyle
        {
            Header = HeaderStyle.ToModel(),
            Data = DataStyle.ToModel()
        }
    };

    /// <summary>Řádek skupiny dnů; <see cref="ColumnRowVM.Header"/> je přepis záhlaví (prázdné = text z dat).</summary>
    public static DepartureGroupColumnVM FromData(
        SkupinaDnuData skupina, DepartureGroupColumnStyle? s,
        ResolvedTextStyle headerInherited, ResolvedTextStyle dataInherited) =>
        new(skupina.Id, skupina.Nadpis + (skupina.Nadpis2.Length > 0 ? " " + skupina.Nadpis2 : ""),
            CellStyleVM.FromModel(s?.Style.Header, headerInherited),
            CellStyleVM.FromModel(s?.Style.Data, dataInherited))
        {
            Visible = s?.IsVisible ?? true,
            AlternatingRowColor = s?.AlternatingRowColor ?? "",
            Header = s?.HeaderOverride ?? "",
            Width = s?.WidthOverride,
        };

    /// <summary>Řádek sloupce hodin - záhlaví a šířka jsou vlastnosti prvku (prázdné záhlaví = „Hod").</summary>
    public static DepartureGroupColumnVM Hours(DepartureGridElement element, ColumnVisualStyle? hourColumnStyle) =>
        new(DepartureGroupEditorDialogViewModel.HourColumnId, "Hodiny",
            CellStyleVM.FromModel(hourColumnStyle?.Header, element.ResolveHourHeaderParent()),
            CellStyleVM.FromModel(hourColumnStyle?.Data, element.ResolveHourDataParent()))
        {
            Header = element.HourColumnHeader,
            Width = element.HourColumnWidth,
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

    // Dny v týdnu - zaškrtávátka 1:1 s abecedou platnosti podle vyhlášky 122/2014
    // (1-7 = Po až Ne, X = pracovní dny, + = neděle a státem uznané svátky).
    private bool _po, _ut, _st, _ct, _pa, _so, _ne, _svatek, _pracovni;
    public bool Po     { get => _po;     set { _po = value;     OnChanged(); OnChanged(nameof(LzePracovni)); } }
    public bool Ut     { get => _ut;     set { _ut = value;     OnChanged(); OnChanged(nameof(LzePracovni)); } }
    public bool St     { get => _st;     set { _st = value;     OnChanged(); OnChanged(nameof(LzePracovni)); } }
    public bool Ct     { get => _ct;     set { _ct = value;     OnChanged(); OnChanged(nameof(LzePracovni)); } }
    public bool Pa     { get => _pa;     set { _pa = value;     OnChanged(); OnChanged(nameof(LzePracovni)); } }
    public bool So     { get => _so;     set { _so = value;     OnChanged(); } }
    public bool Ne     { get => _ne;     set { _ne = value;     OnChanged(); OnChanged(nameof(LzeSvatek)); } }
    public bool Svatek { get => _svatek; set { _svatek = value; OnChanged(); OnChanged(nameof(LzeNe)); } }

    /// <summary>„X" = pracovní dny (vyhláška 122/2014). Liší se od zaškrtnutého Po-Pá tím, že
    /// **nezahrnuje svátky** - spoj jedoucí „v pondělí" jede i o svátečním pondělí, spoj „v pracovní
    /// dny" ne. Proto to jsou dvě různé volby, ne zkratka jedné za druhou.</summary>
    public bool Pracovni { get => _pracovni; set { _pracovni = value; OnChanged(); OnChanged(nameof(LzePoPa)); } }

    // Vzájemně se vylučující dvojice - 1:1 se zákazem kombinací z vyhlášky (`X` s 1-5, `+` se `7`):
    //   „X" (svátky nikdy) × vypsané Po-Pá (svátky, které na ně padnou, ano) - odporují si
    //   „+" (neděle a svátky) × „Ne" - „+" neděli obsahuje, takže dvojice nic nepřidá
    // U druhé dvojice to donedávna zaslepené nebylo, protože vzor „Neděle a svátky" posílal `7+`.
    // Od `B82` posílá samotné `+` a server si `7` doplní sám, takže zaškrtnutí obojího je jen
    // matoucí - ověřeno během: sloupce `7`, `+` i `7+` dají nad toutéž linkou identický výstup.
    public bool LzePracovni => !(_po || _ut || _st || _ct || _pa);
    public bool LzePoPa     => !_pracovni;
    public bool LzeNe       => !_svatek;
    public bool LzeSvatek   => !_ne;

    private string _includeMarks = "";
    public string IncludeMarks { get => _includeMarks; set { _includeMarks = value; OnChanged(); } }

    private string _excludeMarks = "";
    public string ExcludeMarks { get => _excludeMarks; set { _excludeMarks = value; OnChanged(); } }

    private string _suppressedMarks = "";
    public string SuppressedMarks { get => _suppressedMarks; set { _suppressedMarks = value; OnChanged(); } }

    private string _period = "";
    public string Period { get => _period; set { _period = value; OnChanged(); } }

    public string DisplayName => string.IsNullOrWhiteSpace(Header) ? "(bez záhlaví)" : Header;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public DepartureColumnDefinition ToModel() => new()
    {
        Id = Id,
        Header = Header.Trim(),
        Header2 = Header2.Trim(),
        Days = (Pracovni ? "X" : "") + (Po ? "1" : "") + (Ut ? "2" : "") + (St ? "3" : "")
             + (Ct ? "4" : "") + (Pa ? "5" : "") + (So ? "6" : "") + (Ne ? "7" : "")
             + (Svatek ? "+" : ""),
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
        // Starší uložený sloupec může mít `7+` (dřívější tvar vzoru). „+" je z těch dvou širší
        // a znamená totéž, takže se načte jen ono - jinak by dialog otevřel stav, který sám zakazuje.
        Ne = d.Days.Contains('7') && !d.Days.Contains('+'), Svatek = d.Days.Contains('+'),
        Pracovni = d.Days.Contains('X'),
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

    public Controls.ColumnsTabViewModel Columns { get; }

    public List<DepartureGroupColumnStyle> ResultGroupColumns { get; private set; } = [];
    public ColumnVisualStyle? ResultHourColumnStyle { get; private set; }

    /// <summary>Záhlaví a šířka hodin po OK. Zapisuje je až volající jako součást vratného kroku -
    /// do 28. 10. je okno psalo rovnou do prvku a Ctrl+Z je nevrátil.</summary>
    public string ResultHourColumnHeader { get; private set; } = "";
    public float ResultHourColumnWidth { get; private set; }

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

    /// <summary>Ukázat tab „Vlastní definice sloupců"? Jen u agendy, která definici čte (vývěska MHD
    /// ne). Skrytý tab definici v šabloně nemaže ani nekontroluje - OK ji vrátí beze změny.</summary>
    public bool UkazatVlastniSloupce { get; }

    private readonly List<DepartureColumnDefinition>? _puvodniVlastniSloupce;

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

    /// <summary>Rezervované ID pro sloupec hodin v seznamu sloupců.</summary>
    internal const string HourColumnId = "__HOUR__";

    // ── Callbacks ──────────────────────────────────────────────────────

    public Func<string, string?>? RequestColorPicker { get; set; }

    // ── Commands ──────────────────────────────────────────────────────

    // Per-column (Sloupce tab) - barvy buněk drží CellStyleVM, tady zbývá jen zebra
    public RelayCommand PickZebraColorCommand { get; }
    public RelayCommand ClearZebraColorCommand { get; }

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
        string? presetsJson = null,
        bool ukazatVlastniSloupce = true)
    {
        _element = element;
        UkazatVlastniSloupce = ukazatVlastniSloupce;
        _puvodniVlastniSloupce = customColumns;
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
        var groupHeaderInherited = element.ResolveGroupHeaderParent();
        var groupDataInherited   = element.ResolveGroupMinuteParent();

        // Sloupec hodin - vždy první v seznamu, pevný (viz DepartureGroupColumnVM.CanMove)
        var rows = new List<DepartureGroupColumnVM> { DepartureGroupColumnVM.Hours(element, hourColumnStyle) };

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
            rows.Add(DepartureGroupColumnVM.FromData(skupina, existing, groupHeaderInherited, groupDataInherited));
        }

        // Minuty jsou pivot: šířky skupin se dopočítají z obsahu, přesah se proto nehlídá.
        Columns = new Controls.ColumnsTabViewModel(rows)
        {
            MultilineHeader = true,
            WidthHint = "Prázdná šířka = automaticky podle obsahu.",
        };

        // Střídání řádků per skupina - vlastní pole, ne součást stylu buňky: kreslí se pod buňky
        // a platí jen pro každý druhý řádek, takže do CellStyleEditoru nepatří.
        PickZebraColorCommand  = Command(() =>
        {
            if (Columns.SelectedRow is not DepartureGroupColumnVM skupina || RequestColorPicker is null) return;
            if (RequestColorPicker(skupina.AlternatingRowColor) is { } barva) skupina.AlternatingRowColor = barva;
        });
        ClearZebraColorCommand = Command(() =>
        {
            if (Columns.SelectedRow is DepartureGroupColumnVM skupina) skupina.AlternatingRowColor = "";
        });
    }

    /// <summary>Vzor, kterému aktuální sloupce přesně odpovídají (nadpisy, dny i značky); null =
    /// vlastní kombinace. Porovnává se proti vzoru postavenému z konfigurace téže linky.</summary>
    private DepartureColumnPresets.Preset? DetectPreset()
    {
        if (CustomColumnVMs.Count == 0) return null;
        // Porovnání je sdílené s parametrem „Rozdělení sloupců" - kdyby si každé místo odpovídalo
        // po svém, editor by ukazoval vzor a parametr nad týmiž sloupci „Vlastní rozložení".
        var aktualni = CustomColumnVMs.Select(vm => vm.ToModel()).ToList();
        foreach (var preset in Presets)
        {
            if (_presety.TryGetValue(preset.Key, out var built)
                && DepartureColumnPresets.SameLayout(aktualni, built))
                return preset;
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
        if (UkazatVlastniSloupce && UseCustomColumns)
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
        var skupiny = Columns.Rows.Cast<DepartureGroupColumnVM>().ToList();
        var hourVm = skupiny.First(vm => vm.IsHourColumn);
        var style = new ColumnVisualStyle { Header = hourVm.HeaderStyle.ToModel(), Data = hourVm.DataStyle.ToModel() };
        ResultHourColumnStyle = style.IsEmpty ? null : style;
        ResultHourColumnHeader = hourVm.Header.Trim();
        // Sloupec hodin má pevnou šířku (element-level); prázdné pole ponechá stávající.
        ResultHourColumnWidth = hourVm.Width is float hw && hw > 0 ? hw : _element.HourColumnWidth;

        ResultGroupColumns = skupiny
            .Where(vm => !vm.IsHourColumn)
            .Select(vm => vm.ToModel())
            .ToList();

        // Definice vlastních sloupců - null = automaticky (vypnuté, nebo zapnuté bez sloupců)
        ResultCustomColumns = !UkazatVlastniSloupce ? _puvodniVlastniSloupce
            : UseCustomColumns && CustomColumnVMs.Count > 0 ? CustomColumnVMs.Select(vm => vm.ToModel()).ToList()
            : null;

        CloseDialog(true);
    }
}
