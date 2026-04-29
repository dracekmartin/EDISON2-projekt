using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Rendering;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

// ─── ViewModels pro DataGrid ─────────────────────────────────────────────────

public class ColumnVM : INotifyPropertyChanged
{
    private string  _header        = "";
    private string? _field;
    private float   _width         = 30f;
    private string  _widthMode     = "mm";
    private string  _alignmentLabel = "Vlevo";
    private string? _format;
    private bool    _visible       = true;
    private IReadOnlySet<string>? _validFieldNames;

    public string  Header         { get => _header;         set { _header = value;         OnChanged(); } }
    public string? Field          { get => _field;          set { _field = value;           OnChanged(); OnChanged(nameof(IsFieldValid)); } }
    public float   Width          { get => _width;          set { _width = value;           OnChanged(); } }
    public string  WidthMode      { get => _widthMode;      set { _widthMode = value;       OnChanged(); } }
    public string  AlignmentLabel { get => _alignmentLabel; set { _alignmentLabel = value;  OnChanged(); } }
    public string? Format         { get => _format;         set { _format = value;          OnChanged(); } }
    public bool    Visible        { get => _visible;        set { _visible = value;         OnChanged(); } }

    private CellStyleVM _headerStyle = null!;
    /// <summary>Per-column styl záhlaví (kaskáda override).</summary>
    public CellStyleVM HeaderStyle
    {
        get => _headerStyle;
        private init { _headerStyle = value; _headerStyle.PropertyChanged += OnChildChanged; }
    }

    private CellStyleVM _dataStyle = null!;
    /// <summary>Per-column styl datových buněk (kaskáda override).</summary>
    public CellStyleVM DataStyle
    {
        get => _dataStyle;
        private init { _dataStyle = value; _dataStyle.PropertyChanged += OnChildChanged; }
    }

    /// <summary>True pokud sloupec má per-column override v HeaderStyle nebo DataStyle.</summary>
    public bool HasAnyOverride => HeaderStyle.HasAnyExplicit || DataStyle.HasAnyExplicit;

    private void OnChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CellStyleVM.HasAnyExplicit))
            OnChanged(nameof(HasAnyOverride));
    }

    /// <summary>
    /// Sada platných názvů polí ze schématu. Null = validace vypnuta (schéma není dostupné).
    /// </summary>
    public IReadOnlySet<string>? ValidFieldNames
    {
        set { _validFieldNames = value; OnChanged(nameof(IsFieldValid)); }
    }

    /// <summary>
    /// True pokud je pole platné (v schématu) nebo validace není možná (schéma chybí).
    /// Prázdné pole je vždy platné.
    /// </summary>
    public bool IsFieldValid
    {
        get
        {
            if (_validFieldNames is null) return true;
            if (string.IsNullOrWhiteSpace(_field)) return true;
            var name = _field.Trim().Trim('{', '}').Trim();
            if (string.IsNullOrEmpty(name)) return false;  // "{}" nebo jen závorky
            return _validFieldNames.Contains(name);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public TableColumn ToModel() => new()
    {
        Header      = Header,
        Field       = string.IsNullOrWhiteSpace(Field) ? null : Field.Trim().Trim('{', '}'),
        Width       = Width,
        WidthMode   = WidthMode == "%" ? ColumnWidthMode.Percent : ColumnWidthMode.Mm,
        Alignment   = AlignmentLabel switch
        {
            "Střed"  => TextHorizontalAlignment.Center,
            "Vpravo" => TextHorizontalAlignment.Right,
            _        => TextHorizontalAlignment.Left
        },
        Format      = string.IsNullOrWhiteSpace(Format) ? null : Format,
        Visible     = Visible,
        HeaderStyle = HeaderStyle.ToModel(),
        DataStyle   = DataStyle.ToModel(),
    };

    public static ColumnVM FromModel(TableColumn c,
        ResolvedTextStyle headerInherited, ResolvedTextStyle dataInherited) => new()
    {
        Header         = c.Header,
        Field          = c.Field,
        Width          = c.Width,
        WidthMode      = c.WidthMode == ColumnWidthMode.Percent ? "%" : "mm",
        AlignmentLabel = c.Alignment switch
        {
            TextHorizontalAlignment.Center => "Střed",
            TextHorizontalAlignment.Right  => "Vpravo",
            _                              => "Vlevo"
        },
        Format      = c.Format,
        Visible     = c.Visible,
        HeaderStyle = CellStyleVM.FromModel(c.HeaderStyle, headerInherited),
        DataStyle   = CellStyleVM.FromModel(c.DataStyle,   dataInherited),
    };
}

public class RowVM : INotifyPropertyChanged
{
    private string? _visibilityExpression;

    public ObservableCollection<CellVM> Cells { get; } = [];

    public string? VisibilityExpression
    {
        get => _visibilityExpression;
        set { _visibilityExpression = value; OnChanged(); OnChanged(nameof(RowSummary)); }
    }

    /// <summary>⚑ příznak pokud má řádek podmínku viditelnosti.</summary>
    public string RowSummary
    {
        get
        {
            var content = Cells.Count > 0
                ? string.Join(" | ", Cells.Take(3).Select(c => c.Content))
                : "(prázdný řádek)";
            return string.IsNullOrWhiteSpace(VisibilityExpression) ? content : content + "  ⚑";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public void NotifySummary() => OnChanged(nameof(RowSummary));

    public TableRow ToModel() => new()
    {
        Cells                = Cells.Select(c => new TableCell { Content = c.Content }).ToList(),
        VisibilityExpression = string.IsNullOrWhiteSpace(VisibilityExpression) ? null : VisibilityExpression.Trim()
    };

    public static RowVM FromModel(TableRow row, IList<TableColumn> columns)
    {
        var vm = new RowVM { VisibilityExpression = row.VisibilityExpression };
        for (int i = 0; i < columns.Count; i++)
        {
            vm.Cells.Add(new CellVM
            {
                ColumnHeader = columns[i].Header,
                Content      = i < row.Cells.Count ? row.Cells[i].Content : ""
            });
        }
        return vm;
    }
}

public class CellVM : INotifyPropertyChanged
{
    private string _content = "";

    public string ColumnHeader { get; set; } = "";
    public string Content      { get => _content; set { _content = value; OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

// ─── Dialog ViewModel ────────────────────────────────────────────────────────

class TableEditorDialogViewModel : EDViewModel
{
    private readonly TableElement            _element;
    private readonly IReadOnlySet<string>?   _validFieldNames;
    private readonly ITableTemplateProvider? _templateProvider;

    public ObservableCollection<ColumnVM> ColumnVMs   { get; } = [];
    public ObservableCollection<RowVM>    RowVMs      { get; } = [];
    public ObservableCollection<RowVM>    SummaryVMs  { get; } = [];

    public string[] WidthModeOptions  { get; } = ["mm", "%"];
    public string[] AlignmentOptions  { get; } = ["Vlevo", "Střed", "Vpravo"];
    public string[] FieldNameOptions  { get; }

    // ── Overflow detekce (součet šířek viditelných sloupců vs element Width) ──

    public float ElementWidth => _element.Width;

    /// <summary>Součet šířek viditelných sloupců v mm (Percent se konvertuje na mm).</summary>
    public float TotalVisibleColumnWidth =>
        ColumnVMs.Where(c => c.Visible).Sum(c =>
            c.WidthMode == "%" ? c.Width * ElementWidth / 100f : c.Width);

    public bool HasColumnOverflow => TotalVisibleColumnWidth > ElementWidth + 0.01f;

    public string ColumnOverflowText =>
        $"⚠ Součet šířek sloupců ({TotalVisibleColumnWidth:0.#} mm) přesahuje šířku elementu ({ElementWidth:0.#} mm). Obsah se oříže.";

    // ── Tab Obecné: element-level styly (analogie k JŘ dialogům) ─────────

    /// <summary>Výchozí styl záhlaví tabulky (kaskáda: per-column → tento → TableHeaderParent).</summary>
    public CellStyleVM ElementHeaderStyle { get; }

    /// <summary>Výchozí styl datových buněk tabulky (kaskáda: per-column → tento → TableDataParent).</summary>
    public CellStyleVM ElementDataStyle { get; }

    // ── UI omezení editace — co CellStyleEditor zobrazí per zóna ─────────
    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;
    public CellStyleEditOptions DataEditOptions   { get; } = CellStyleEditOptions.AllEnabled;

    /// <summary>Výsledné sloupce po OK — čteny volajícím.</summary>
    public List<TableColumn> ResultColumns { get; private set; } = [];

    /// <summary>Výsledné řádky po OK — null pro Dynamic mód.</summary>
    public List<TableRow>? ResultRows { get; private set; }

    /// <summary>Výsledné souhrnné řádky po OK — null pokud žádné.</summary>
    public List<TableRow>? ResultSummaryRows { get; private set; }

    // ── Výběr ────────────────────────────────────────────────────────────────

    private ColumnVM? _selectedColumnVM;
    public ColumnVM? SelectedColumnVM
    {
        get => _selectedColumnVM;
        set => Set(ref _selectedColumnVM, value);
    }

    private RowVM? _selectedRowVM;
    public RowVM? SelectedRowVM
    {
        get => _selectedRowVM;
        set => Set(ref _selectedRowVM, value);
    }

    private RowVM? _selectedSummaryVM;
    public RowVM? SelectedSummaryVM
    {
        get => _selectedSummaryVM;
        set => Set(ref _selectedSummaryVM, value);
    }

    public bool IsRowSelected => SelectedRowVM is not null;

    // ── Viditelnost záložky a tlačítka ───────────────────────────────────────

    public bool ShowRowsTab      => _element.Mode == TableMode.Fixed;
    public bool ShowLoadTemplate => _templateProvider is not null && _element.Mode == TableMode.Fixed;

    // ── Callbacks pro View-only operace ──────────────────────────────────────

    /// <summary>Callback: po přidání sloupce scrolluj do view. Wired v code-behind.</summary>
    public Action<ColumnVM>? RequestScrollColumnIntoView { get; set; }

    /// <summary>Callback: před potvrzením commitni editaci DataGridů. Wired v code-behind.</summary>
    public Action? RequestCommitEdits { get; set; }

    /// <summary>Callback: zobraz color picker, vrať zvolenou barvu (nebo null při Storno).</summary>
    public Func<string, string?>? RequestColorPicker { get; set; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand AddColumnCommand      { get; }
    public RelayCommand RemoveColumnCommand   { get; }
    public RelayCommand MoveColumnUpCommand   { get; }
    public RelayCommand MoveColumnDownCommand { get; }

    public RelayCommand AddRowCommand         { get; }
    public RelayCommand RemoveRowCommand      { get; }
    public RelayCommand MoveRowUpCommand      { get; }
    public RelayCommand MoveRowDownCommand    { get; }

    public RelayCommand AddSummaryCommand     { get; }
    public RelayCommand RemoveSummaryCommand  { get; }

    public RelayCommand LoadTemplateCommand   { get; }

    // Per-column (tab Styl sloupců)
    public RelayCommand PickHeaderTextColorCommand  { get; }
    public RelayCommand PickHeaderBgColorCommand    { get; }
    public RelayCommand PickDataTextColorCommand    { get; }
    public RelayCommand PickDataBgColorCommand      { get; }
    public RelayCommand ClearHeaderTextColorCommand { get; }
    public RelayCommand ClearHeaderBgColorCommand   { get; }
    public RelayCommand ClearDataTextColorCommand   { get; }
    public RelayCommand ClearDataBgColorCommand     { get; }
    public RelayCommand ResetStyleCommand           { get; }
    public RelayCommand ResetAllColumnStylesCommand { get; }
    public RelayCommand FitColumnsToWidthCommand   { get; }

    // Element-level (tab Obecné)
    public RelayCommand PickElementHeaderTextColorCommand  { get; }
    public RelayCommand PickElementHeaderBgColorCommand    { get; }
    public RelayCommand PickElementDataTextColorCommand    { get; }
    public RelayCommand PickElementDataBgColorCommand      { get; }
    public RelayCommand ClearElementHeaderTextColorCommand { get; }
    public RelayCommand ClearElementHeaderBgColorCommand   { get; }
    public RelayCommand ClearElementDataTextColorCommand   { get; }
    public RelayCommand ClearElementDataBgColorCommand     { get; }
    public RelayCommand ResetElementStyleCommand           { get; }

    // ── Konstruktor ───────────────────────────────────────────────────────────

    private ResolvedTextStyle _headerInherited;
    private ResolvedTextStyle _dataInherited;

    public TableEditorDialogViewModel(TableElement element,
        IReadOnlyList<DataField>? collectionFields = null,
        ITableTemplateProvider? templateProvider = null)
    {
        _element          = element;
        _templateProvider = templateProvider;
        _headerInherited  = element.ResolveColumnHeaderParent();
        _dataInherited    = element.ResolveColumnDataParent();

        // Element-level CellStyleVM (tab Obecné) — parent je TableHeaderParent/TableDataParent (baked defaults)
        ElementHeaderStyle = CellStyleVM.FromModel(element.HeaderStyle, element.ResolveElementHeaderParent());
        ElementDataStyle   = CellStyleVM.FromModel(element.DataStyle,   element.ResolveElementDataParent());

        FieldNameOptions = collectionFields?.Select(f => f.FieldName).OrderBy(n => n).ToArray() ?? [];
        _validFieldNames  = FieldNameOptions.Length > 0
            ? new HashSet<string>(FieldNameOptions, StringComparer.OrdinalIgnoreCase)
            : null;

        // Naplnění sloupců — pro Dynamic tabulku bez sloupců předvyplnit ze schématu
        var sourceCols = element.Columns.Count == 0
                         && element.Mode == TableMode.Dynamic
                         && collectionFields is { Count: > 0 }
            ? collectionFields.Select(f => new TableColumn
                { Header = f.Label, Field = f.FieldName,
                  Width  = MathF.Round(element.Width / collectionFields.Count, 1) })
            : element.Columns.AsEnumerable();

        foreach (var col in sourceCols)
            ColumnVMs.Add(WithValidation(ColumnVM.FromModel(col, _headerInherited, _dataInherited)));

        if (element.Mode == TableMode.Fixed)
        {
            var cols = element.Columns;
            foreach (var row in element.Rows ?? [])
                RowVMs.Add(RowVM.FromModel(row, cols));
        }

        // Souhrnné řádky — načíst existující
        foreach (var row in element.SummaryRows ?? [])
            SummaryVMs.Add(RowVM.FromModel(row, element.Columns));

        AddColumnCommand      = Command(AddColumn);
        RemoveColumnCommand   = Command(RemoveColumn);
        MoveColumnUpCommand   = Command(MoveColumnUp);
        MoveColumnDownCommand = Command(MoveColumnDown);

        AddRowCommand         = Command(AddRow);
        RemoveRowCommand      = Command(RemoveRow);
        MoveRowUpCommand      = Command(MoveRowUp);
        MoveRowDownCommand    = Command(MoveRowDown);

        AddSummaryCommand     = Command(AddSummary);
        RemoveSummaryCommand  = Command(RemoveSummary);

        LoadTemplateCommand   = Command(LoadTemplate);

        // Color picker commands — cascade od SelectedColumnVM.HeaderStyle / DataStyle
        PickHeaderTextColorCommand  = Command(() => PickColor(c => c.HeaderStyle.TextColor, (c, v) => c.HeaderStyle.TextColor = v));
        PickHeaderBgColorCommand    = Command(() => PickColor(c => c.HeaderStyle.BgColor,   (c, v) => c.HeaderStyle.BgColor   = v));
        PickDataTextColorCommand    = Command(() => PickColor(c => c.DataStyle.TextColor,   (c, v) => c.DataStyle.TextColor   = v));
        PickDataBgColorCommand      = Command(() => PickColor(c => c.DataStyle.BgColor,     (c, v) => c.DataStyle.BgColor     = v));
        ClearHeaderTextColorCommand = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.HeaderStyle.TextColor = null; });
        ClearHeaderBgColorCommand   = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.HeaderStyle.BgColor   = null; });
        ClearDataTextColorCommand   = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.DataStyle.TextColor   = null; });
        ClearDataBgColorCommand     = Command(() => { if (SelectedColumnVM is not null) SelectedColumnVM.DataStyle.BgColor     = null; });

        ResetStyleCommand = Command(() =>
        {
            if (SelectedColumnVM is null) return;
            SelectedColumnVM.HeaderStyle.Reset();
            SelectedColumnVM.DataStyle.Reset();
        });

        // Reset všech sloupců
        ResetAllColumnStylesCommand = Command(() =>
        {
            foreach (var vm in ColumnVMs)
            {
                vm.HeaderStyle.Reset();
                vm.DataStyle.Reset();
            }
        });

        // Přizpůsobit sloupce k šířce elementu — proporcionální scale (zachová mód Mm/%)
        FitColumnsToWidthCommand = Command(() =>
        {
            var visible = ColumnVMs.Where(c => c.Visible).ToList();
            if (visible.Count == 0) return;
            float totalMm = visible.Sum(c => c.WidthMode == "%" ? c.Width * ElementWidth / 100f : c.Width);
            if (totalMm <= 0.01f) return;
            float scale = ElementWidth / totalMm;
            foreach (var vm in visible)
                vm.Width = (float)Math.Round(vm.Width * scale, 2);
        });

        // Subscribe na Width / Visible / WidthMode — refresh overflow warning
        foreach (var vm in ColumnVMs)
            vm.PropertyChanged += OnColumnChanged;

        // Element-level color picker commands (tab Obecné)
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

    private void PickColor(Func<ColumnVM, string?> getter, Action<ColumnVM, string?> setter)
    {
        if (SelectedColumnVM is null || RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(SelectedColumnVM) ?? "");
        if (result is not null)
            setter(SelectedColumnVM, result);
    }

    private void PickElementColor(CellStyleVM vm, Func<CellStyleVM, string?> getter, Action<CellStyleVM, string?> setter)
    {
        if (RequestColorPicker is null) return;
        var result = RequestColorPicker(getter(vm) ?? "");
        if (result is not null)
            setter(vm, result);
    }

    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnVM.Width) ||
            e.PropertyName == nameof(ColumnVM.Visible) ||
            e.PropertyName == nameof(ColumnVM.WidthMode))
        {
            OnPropertyChanged(nameof(TotalVisibleColumnWidth));
            OnPropertyChanged(nameof(HasColumnOverflow));
            OnPropertyChanged(nameof(ColumnOverflowText));
        }
    }

    // ── Sloupce ───────────────────────────────────────────────────────────────

    private void AddColumn()
    {
        var newCol = new TableColumn { Header = $"Sloupec {ColumnVMs.Count + 1}", Width = 30f };
        var vm = WithValidation(ColumnVM.FromModel(newCol, _headerInherited, _dataInherited));
        vm.PropertyChanged += OnColumnChanged;
        ColumnVMs.Add(vm);
        OnPropertyChanged(nameof(TotalVisibleColumnWidth));
        OnPropertyChanged(nameof(HasColumnOverflow));
        OnPropertyChanged(nameof(ColumnOverflowText));
        SelectedColumnVM = vm;
        RequestScrollColumnIntoView?.Invoke(vm);

        if (_element.Mode == TableMode.Fixed)
            foreach (var row in RowVMs)
            {
                row.Cells.Add(new CellVM { ColumnHeader = vm.Header, Content = "" });
                row.NotifySummary();
            }

        foreach (var row in SummaryVMs)
        {
            row.Cells.Add(new CellVM { ColumnHeader = vm.Header, Content = "" });
            row.NotifySummary();
        }
    }

    private void RemoveColumn()
    {
        if (SelectedColumnVM is not ColumnVM vm) return;
        int idx = ColumnVMs.IndexOf(vm);
        vm.PropertyChanged -= OnColumnChanged;
        ColumnVMs.Remove(vm);
        OnPropertyChanged(nameof(TotalVisibleColumnWidth));
        OnPropertyChanged(nameof(HasColumnOverflow));
        OnPropertyChanged(nameof(ColumnOverflowText));

        if (_element.Mode == TableMode.Fixed && idx >= 0)
            foreach (var row in RowVMs)
            {
                if (idx < row.Cells.Count) row.Cells.RemoveAt(idx);
                row.NotifySummary();
            }

        if (idx >= 0)
            foreach (var row in SummaryVMs)
            {
                if (idx < row.Cells.Count) row.Cells.RemoveAt(idx);
                row.NotifySummary();
            }
    }

    private void MoveColumnUp()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        if (idx > 0) ColumnVMs.Move(idx, idx - 1);
    }

    private void MoveColumnDown()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        if (idx >= 0 && idx < ColumnVMs.Count - 1) ColumnVMs.Move(idx, idx + 1);
    }

    // ── Řádky ─────────────────────────────────────────────────────────────────

    private void AddRow()
    {
        var vm = new RowVM();
        foreach (var col in ColumnVMs)
            vm.Cells.Add(new CellVM { ColumnHeader = col.Header, Content = "" });
        RowVMs.Add(vm);
        SelectedRowVM = vm;
    }

    private void RemoveRow()
    {
        if (SelectedRowVM is RowVM vm) RowVMs.Remove(vm);
    }

    private void MoveRowUp()
    {
        if (SelectedRowVM is null) return;
        int idx = RowVMs.IndexOf(SelectedRowVM);
        if (idx > 0) { RowVMs.Move(idx, idx - 1); }
    }

    private void MoveRowDown()
    {
        if (SelectedRowVM is null) return;
        int idx = RowVMs.IndexOf(SelectedRowVM);
        if (idx >= 0 && idx < RowVMs.Count - 1) { RowVMs.Move(idx, idx + 1); }
    }

    // ── Souhrnné řádky ────────────────────────────────────────────────────────

    private void AddSummary()
    {
        var vm = new RowVM();
        foreach (var col in ColumnVMs)
            vm.Cells.Add(new CellVM { ColumnHeader = col.Header, Content = "" });
        SummaryVMs.Add(vm);
        SelectedSummaryVM = vm;
    }

    private void RemoveSummary()
    {
        if (SelectedSummaryVM is RowVM vm) SummaryVMs.Remove(vm);
    }

    // ── Ze šablony ────────────────────────────────────────────────────────────

    private void LoadTemplate()
    {
        if (_templateProvider is null) return;

        var templates = _templateProvider.GetAvailableTemplates();
        if (templates.Count == 0)
        {
            PopupDisplayer.Info("Nejsou dostupné žádné šablony tabulek.");
            return;
        }

        // Picker — jednoduchý inline dialog s ListBoxem
        var picker = new Window
        {
            Title                 = "Vyberte šablonu tabulky",
            Width                 = 340,
            Height                = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = Application.Current.MainWindow,
            ResizeMode            = ResizeMode.NoResize,
            ShowInTaskbar         = false
        };

        var list = new System.Windows.Controls.ListBox { Margin = new Thickness(10, 10, 10, 0) };
        foreach (var t in templates)
            list.Items.Add(new System.Windows.Controls.ListBoxItem { Content = t.Label, Tag = t.Key });
        list.SelectedIndex = 0;

        var btnOk     = new Button { Content = "Použít", Width = 80, IsDefault = true,
                                     Margin = new Thickness(0, 8, 8, 10),
                                     HorizontalAlignment = System.Windows.HorizontalAlignment.Right};
        var btnCancel = new Button { Content = "Storno", Width = 80, IsCancel = true,
                                     Margin = new Thickness(0, 8, 0, 10),
                                     HorizontalAlignment = System.Windows.HorizontalAlignment.Right};

        btnOk.Click     += (_, _) => picker.DialogResult = true;
        btnCancel.Click += (_, _) => picker.DialogResult = false;

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 10, 0)
        };
        btnPanel.Children.Add(btnOk);
        btnPanel.Children.Add(btnCancel);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(list,     0);
        Grid.SetRow(btnPanel, 1);
        root.Children.Add(list);
        root.Children.Add(btnPanel);
        picker.Content = root;

        if (picker.ShowDialog() != true) return;
        if (list.SelectedItem is not System.Windows.Controls.ListBoxItem selected) return;

        var key = (string)selected.Tag!;
        var def = _templateProvider.GetTemplate(key);
        if (def is null)
        {
            PopupDisplayer.Info("Šablonu se nepodařilo načíst.");
            return;
        }

        ColumnVMs.Clear();
        foreach (var col in def.Columns)
            ColumnVMs.Add(WithValidation(ColumnVM.FromModel(col, _headerInherited, _dataInherited)));

        RowVMs.Clear();
        foreach (var row in def.Rows)
            RowVMs.Add(RowVM.FromModel(row, def.Columns));
    }

    // ── OK / Storno ───────────────────────────────────────────────────────────

    /// <summary>Commitne editaci (volá View callback), sestaví výsledky a zavře dialog.</summary>
    protected override void OnOk()
    {
        RequestCommitEdits?.Invoke();

        // Element-level styl — zápis zpět do elementu (TableElement.HeaderStyle/DataStyle jsou nullable)
        _element.HeaderStyle = ElementHeaderStyle.ToModel();
        _element.DataStyle   = ElementDataStyle.ToModel();

        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        if (_element.Mode == TableMode.Fixed)
            ResultRows = RowVMs.Select(rvm => rvm.ToModel()).ToList();

        ResultSummaryRows = SummaryVMs.Count > 0
            ? SummaryVMs.Select(rvm => rvm.ToModel()).ToList()
            : null;

        CloseDialog(true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ColumnVM WithValidation(ColumnVM vm)
    {
        vm.ValidFieldNames = _validFieldNames;
        return vm;
    }
}
