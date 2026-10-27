using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce ────────────────────────────────────────────

public class StopColumnVM : INotifyPropertyChanged, Controls.IColumnRowVM
{
    private string _key = "";
    private string _header = "";
    private float? _width = 10f;
    private bool   _visible = true;
    private string _alignmentLabel = "Vlevo";

    public string Key            { get => _key;            set { _key = value;            OnChanged(); } }
    public string Header         { get => _header;         set { _header = value;         OnChanged(); OnChanged(nameof(DisplayName)); } }
    public float? Width          { get => _width;          set { _width = value;          OnChanged(); OnChanged(nameof(WidthText)); OnChanged(nameof(ColumnWidth)); } }
    public bool   Visible        { get => _visible;        set { _visible = value;        OnChanged(); } }
    public string AlignmentLabel { get => _alignmentLabel; set { _alignmentLabel = value; OnChanged(); } }

    /// <summary>Šířka jako text pro editor (prázdné = auto). Parsuje na <see cref="Width"/>.</summary>
    public string WidthText
    {
        get => Controls.ColumnWidthText.Format(_width);
        set => Width = Controls.ColumnWidthText.Parse(value);
    }

    // IColumnRowVM - sdílený seznam sloupců. Srozumitelný název dle Key (Header zůstává tištěná zkratka).
    public string  DisplayName => FriendlyName(_key, _header);
    public float?  ColumnWidth => Width;

    /// <summary>Čitelný název sloupce do editoru - odvozený z field ID, ať uživatel nemusí luštit zkratku záhlaví.</summary>
    private static string FriendlyName(string key, string header) => key switch
    {
        StopFields.TravelTime         => "Čas jízdy",
        StopFields.TravelTimeVariants => "Varianty tras",
        StopFields.StopName           => "Název zastávky",
        StopFields.Zone               => "Zóny",
        StopFields.Poradi             => "Pořadí",
        StopFields.JeAktualni         => "Je aktuální zastávka",
        _                             => string.IsNullOrWhiteSpace(header) ? key : header,
    };

    /// <summary>Jedna položka seznamu, ze které při tisku vznikne víc sloupců podle dat.
    /// Uživatel to musí vidět dřív, než se diví, proč mu jedno zaškrtnutí udělalo dvanáct sloupců.</summary>
    public bool ExpandsByData => _key == StopFields.TravelTimeVariants;

    // `B150`: záhlaví rozvíjené položky je od 11. 9. editovatelné (dřív zamčené) - je to společný
    // popisek celého bloku pro případ, že jsou písmena variant vypnutá.

    public string ExpandHint =>
        "Rozvine se podle dat: linka dostane tolik sloupců, kolik má variant jízdních dob, a každý "
        + "bude takto široký (i když má varianta jednu). Záhlaví nesou značky variant; zadané "
        + "záhlaví se použije jako jeden společný popisek přes celý blok, když v parametrech sestavy "
        + "vypnete Písmena variant tras. Vypněte si k tomu sloupec Čas jízdy - je to týž údaj a "
        + "zapnutý vedle by se opakoval. Součet šířek vlevo počítá jeden.";

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
    // Tab 1 „Obecné" = embedded EDInputForm (StopListElementEdit.cls) - řeší code-behind
    // přes ElementFormBinder. VM drží jen per-sloupec editaci (tab 2).

    // ── Tab 2: Sloupce ──────────────────────────────────────────────────

    public ObservableCollection<StopColumnVM> ColumnVMs { get; } = [];

    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    // ── UI omezení editace - co CellStyleEditor zobrazí per zóna ─────────
    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;
    public CellStyleEditOptions DataEditOptions   { get; } = CellStyleEditOptions.AllEnabled;

    // ── Overflow detekce (součet šířek viditelných sloupců vs element Width) ──

    public float ElementWidth => _element.Width;

    /// <summary>Součet pevných šířek viditelných sloupců (mm); auto (null) se nepočítá.</summary>
    public float TotalVisibleColumnWidth =>
        ColumnVMs.Where(c => c.Visible).Sum(c => c.Width ?? 0f);

    /// <summary>True pokud sloupce přesahují šířku elementu (ořízne se content).</summary>
    public bool HasColumnOverflow =>
        TotalVisibleColumnWidth > ElementWidth + 0.01f;

    /// <summary>Text pro warning badge.</summary>
    public string ColumnOverflowText =>
        $"⚠ Součet šířek sloupců ({TotalVisibleColumnWidth:0.#} mm) přesahuje šířku prvku ({ElementWidth:0.#} mm). Obsah se oříže.";

    /// <summary>Výsledné sloupce po OK - čteny volajícím.</summary>
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

    // ── Commands ────────────────────────────────────────────────────────────

    public RelayCommand MoveUpCommand   { get; }
    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ResetStyleCommand { get; }
    public RelayCommand ResetAllColumnStylesCommand { get; }
    public RelayCommand FitColumnsToWidthCommand { get; }

    // ── Konstruktor ─────────────────────────────────────────────────────────

    private readonly StopListElement _element;

    /// <summary>Vypnuté sloupce, ke kterým agenda nemá data - nenabízejí se, ale ze šablony nezmizí.</summary>
    private readonly List<StopColumn> _nenabizene = [];

    /// <param name="nabizetSloupec">Které sloupce nabídnout (podle dat agendy); null = všechny.
    /// Zapnutý sloupec se nabídne vždy, aby šel vypnout.</param>
    public StopColumnsEditorDialogViewModel(
        StopListElement element,
        List<StopColumn> columns,
        Func<string, bool>? nabizetSloupec = null)
    {
        _element           = element;

        var headerInherited = element.ResolveColumnHeaderParent();
        var dataInherited   = element.ResolveColumnDataParent();

        foreach (var col in columns)
        {
            if (!col.Visible && nabizetSloupec?.Invoke(col.Key) == false) { _nenabizene.Add(col); continue; }
            ColumnVMs.Add(StopColumnVM.FromModel(col, headerInherited, dataInherited));
        }

        // Doplň chybějící známé sloupce (uložená šablona může mít jen podmnožinu) - ať uživatel
        // vidí VŠECHNY dostupné sloupce a může je zapnout checkboxem, místo aby zmizely z nabídky
        // a daly se vrátit jen přes "Výchozí". Doplněné jsou skryté (nezmění render, dokud je nezapne).
        var presentKeys = columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var def in StopListElement.GetDefaultColumns())
            if (!presentKeys.Contains(def.Key) && nabizetSloupec?.Invoke(def.Key) != false)
            {
                def.Visible = false;
                ColumnVMs.Add(StopColumnVM.FromModel(def, headerInherited, dataInherited));
            }

        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];

        MoveUpCommand   = Command(MoveUp);
        MoveDownCommand = Command(MoveDown);

    
    
        // Reset style - vybraný sloupec
        ResetStyleCommand = Command(() =>
        {
            if (SelectedColumnVM is null) return;
            SelectedColumnVM.HeaderStyle.Reset();
            SelectedColumnVM.DataStyle.Reset();
        });

        // Reset všech sloupců - smaže per-column overrides
        ResetAllColumnStylesCommand = Command(() =>
        {
            foreach (var vm in ColumnVMs)
            {
                vm.HeaderStyle.Reset();
                vm.DataStyle.Reset();
            }
        });

        // Přizpůsobit šířky sloupců k šířce elementu - proporcionální scale
        FitColumnsToWidthCommand = Command(() =>
        {
            var visible = ColumnVMs.Where(c => c.Visible).ToList();
            if (visible.Count == 0) return;
            float total = visible.Sum(c => c.Width ?? 0f);
            if (total <= 0.01f) return;
            float scale = ElementWidth / total;
            // Škáluje jen pevné šířky; auto (null) zůstává auto.
            foreach (var vm in visible)
                if (vm.Width is float w) vm.Width = (float)Math.Round(w * scale, 2);
        });

        // Subscribe na Width / Visible změny každého sloupce → refresh overflow warning
        foreach (var vm in ColumnVMs)
            vm.PropertyChanged += OnColumnChanged;

    }

    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StopColumnVM.Width) ||
            e.PropertyName == nameof(StopColumnVM.Visible))
        {
            OnPropertyChanged(nameof(TotalVisibleColumnWidth));
            OnPropertyChanged(nameof(HasColumnOverflow));
            OnPropertyChanged(nameof(ColumnOverflowText));
        }
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

    // ── OK ──────────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        RequestCommitEdits?.Invoke();

        // Element-level styl + ostatní general props řeší embedded EDInputForm (code-behind).
        ResultColumns = [.. ColumnVMs.Select(vm => vm.ToModel()), .. _nenabizene];
        CloseDialog(true);
    }
}
