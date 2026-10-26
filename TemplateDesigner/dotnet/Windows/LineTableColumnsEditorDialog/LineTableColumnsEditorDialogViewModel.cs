using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce linkového JŘ ────────────────────────────────

/// <summary>
/// Řádek sloupce v editoru linkového JŘ - sdílí model <see cref="StopColumn"/> i UI infrastrukturu
/// (<see cref="Controls.ColumnListEditor"/> + <see cref="Controls.CellStyleEditor"/>) se StopList.
/// Zarovnání se prosazuje přes HAlign v CellStyleEditor; <see cref="Alignment"/> je jen passthrough
/// per-column default (StopName vlevo, ostatní na střed).
/// </summary>
public class LineTableColumnVM : INotifyPropertyChanged, Controls.IColumnRowVM
{
    private string _key = "";
    private string _header = "";
    private float? _width;
    private bool _visible = true;

    public string Key     { get => _key;     set { _key = value;     OnChanged(); } }
    public string Header  { get => _header;  set { _header = value;  OnChanged(); OnChanged(nameof(DisplayName)); } }
    public float? Width   { get => _width;   set { _width = value;   OnChanged(); OnChanged(nameof(WidthText)); OnChanged(nameof(ColumnWidth)); } }
    public bool   Visible { get => _visible; set { _visible = value; OnChanged(); } }

    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Left;

    public string WidthText
    {
        get => Controls.ColumnWidthText.Format(_width);
        set => Width = Controls.ColumnWidthText.Parse(value);
    }

    // IColumnRowVM - čitelný název dle klíče (Header je tištěná zkratka)
    public string DisplayName => FriendlyName(_key, _header);
    public float? ColumnWidth => Width;

    private static string FriendlyName(string key, string header) => key switch
    {
        ColumnKeys.Zone     => "Zóna",
        ColumnKeys.Km       => "Kilometry",
        ColumnKeys.Tc       => "Tarifní číslo (TČ)",
        ColumnKeys.OriginalZone => "Zóna původní verze",
        ColumnKeys.OriginalTc   => "Tarifní číslo (TČ) původní verze",
        ColumnKeys.StopName => "Zastávka",
        ColumnKeys.Mhd      => "MHD / IDS",
        ColumnKeys.Trip     => "Spoje (časové sloupce)",
        ColumnKeys.Arrow    => "Šipka směru",
        _                   => string.IsNullOrWhiteSpace(header) ? key : header,
    };

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
        Alignment = Alignment,
        Style     = BuildStyle()
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

    public static LineTableColumnVM FromModel(StopColumn c,
        ResolvedTextStyle headerInherited, ResolvedTextStyle dataInherited) => new()
    {
        Key         = c.Key,
        Header      = c.Header,
        Width       = c.Width,
        Visible     = c.Visible,
        Alignment   = c.Alignment,
        HeaderStyle = CellStyleVM.FromModel(c.Style?.Header, headerInherited),
        DataStyle   = CellStyleVM.FromModel(c.Style?.Data,   dataInherited)
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class LineTableColumnsEditorDialogViewModel : EDViewModel
{
    // Tab 1 „Obecné" = embedded EDInputForm (LineTableElementEdit.cls) - řeší code-behind.
    // VM drží jen per-sloupec editaci (tab 2).

    public ObservableCollection<LineTableColumnVM> ColumnVMs { get; } = [];

    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;

    /// <summary>
    /// Co jde nastavit v <b>datové</b> zóně vybraného sloupce.
    /// </summary>
    /// <remarks>
    /// ⚠️ U <b>šipky směru</b> se v datech nekreslí text, ale <b>šipka</b>, takže font, řez, zarovnání
    /// ani odsazení nemají co ovlivnit - zbývá barva (kreslí se jí šipka) a pozadí buňky. Nález Jan
    /// 28. 8.: nastavil barvu ve „Stylu záhlaví", protože u sloupce bez textu se nedalo poznat, že
    /// šipku řídí zóna datových buněk. Skrytí nesmyslných polí i vlastní popisek zóny (viz
    /// <see cref="NazevZonyDat"/>) tu cestu ukazují.
    /// </remarks>
    public CellStyleEditOptions DataEditOptions => JeSipka
        ? new CellStyleEditOptions
        {
            AllowFontFamily = false, AllowFontSize = false, AllowBold = false, AllowItalic = false,
            AllowUnderline = false, AllowStrikethrough = false,
            AllowHAlign = false, AllowVAlign = false, AllowPadding = false,
            AllowTextColor = true, AllowBackgroundColor = true,
        }
        : CellStyleEditOptions.AllEnabled;

    /// <summary>Popisek datové zóny - u šipky se z ní kreslí šipka, ne buňky s textem.</summary>
    public string NazevZonyDat => JeSipka ? "Styl šipky" : "Styl datových buněk";

    /// <summary>Nápověda pod datovou zónou; prázdná u sloupců, kde není co vysvětlovat.</summary>
    public string NapovedaZonyDat => JeSipka
        ? "Šipka se kreslí barvou textu. Font ani zarovnání na ni nemají vliv."
        : "";

    /// <summary>Je nápověda pod datovou zónou neprázdná? (viditelnost v XAML)</summary>
    public bool MaNapoveduZonyDat => NapovedaZonyDat.Length > 0;

    private bool JeSipka => _selectedColumnVM?.Key == ColumnKeys.Arrow;

    public float ElementWidth => _element.Width;

    /// <summary>Součet pevných šířek viditelných sloupců (mm); auto (null) i spojový sloupec se nepočítá.</summary>
    public float TotalVisibleColumnWidth =>
        ColumnVMs.Where(c => c.Visible && c.Key != LineTableElement.TripKey).Sum(c => c.Width ?? 0f);

    public bool HasColumnOverflow => TotalVisibleColumnWidth > ElementWidth + 0.01f;

    public string ColumnOverflowText =>
        $"⚠ Součet šířek popisných sloupců ({TotalVisibleColumnWidth:0.#} mm) přesahuje šířku prvku ({ElementWidth:0.#} mm). Na spoje nezbude místo.";

    public List<StopColumn> ResultColumns { get; private set; } = [];

    private LineTableColumnVM? _selectedColumnVM;
    public LineTableColumnVM? SelectedColumnVM
    {
        get => _selectedColumnVM;
        set
        {
            Set(ref _selectedColumnVM, value);
            OnPropertyChanged(nameof(HasSelection));
            // Datová zóna se mění podle druhu sloupce - u šipky nabízí jen barvu a pozadí.
            OnPropertyChanged(nameof(DataEditOptions));
            OnPropertyChanged(nameof(NazevZonyDat));
            OnPropertyChanged(nameof(NapovedaZonyDat));
            OnPropertyChanged(nameof(MaNapoveduZonyDat));
        }
    }

    public bool HasSelection => _selectedColumnVM is not null;

    public RelayCommand MoveUpCommand   { get; }
    public RelayCommand MoveDownCommand { get; }



    public RelayCommand ResetStyleCommand { get; }
    public RelayCommand ResetAllColumnStylesCommand { get; }

    private readonly LineTableElement _element;

    public LineTableColumnsEditorDialogViewModel(LineTableElement element, List<StopColumn> columns)
    {
        _element = element;

        var headerInherited = element.ResolveColumnHeaderParent();
        var dataInherited   = element.ResolveColumnDataParent();

        foreach (var col in columns)
            ColumnVMs.Add(LineTableColumnVM.FromModel(col, headerInherited, dataInherited));

        // Doplň chybějící známé sloupce (uložená šablona může mít podmnožinu) - ať uživatel vidí
        // všechny dostupné sloupce a může je zapnout checkboxem. Doplněné jsou skryté.
        var presentKeys = columns.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var def in LineTableElement.GetDefaultColumns())
            if (!presentKeys.Contains(def.Key))
            {
                def.Visible = false;
                ColumnVMs.Add(LineTableColumnVM.FromModel(def, headerInherited, dataInherited));
            }

        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];

        MoveUpCommand   = Command(MoveUp);
        MoveDownCommand = Command(MoveDown);



        ResetStyleCommand = Command(() =>
        {
            if (SelectedColumnVM is null) return;
            SelectedColumnVM.HeaderStyle.Reset();
            SelectedColumnVM.DataStyle.Reset();
        });

        ResetAllColumnStylesCommand = Command(() =>
        {
            foreach (var vm in ColumnVMs)
            {
                vm.HeaderStyle.Reset();
                vm.DataStyle.Reset();
            }
        });

        foreach (var vm in ColumnVMs)
            vm.PropertyChanged += OnColumnChanged;
    }

    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LineTableColumnVM.Width) ||
            e.PropertyName == nameof(LineTableColumnVM.Visible))
        {
            OnPropertyChanged(nameof(TotalVisibleColumnWidth));
            OnPropertyChanged(nameof(HasColumnOverflow));
            OnPropertyChanged(nameof(ColumnOverflowText));
        }
    }

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

    protected override void OnOk()
    {
        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
