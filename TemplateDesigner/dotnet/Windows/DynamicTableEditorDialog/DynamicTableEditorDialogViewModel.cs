using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using EDControls.EDViewModel;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

// ─── ViewModel pro řádek sloupce (mapuje TableColumn) ────────────────────────

public class DynamicTableColumnVM : INotifyPropertyChanged, Controls.IColumnRowVM
{
    private string _header = "";
    private string _field = "";
    private string _expression = "";
    private string _format = "";
    private float? _width;              // null = auto (rozdělí zbývající šířku)
    private bool   _visible = true;

    public string Header  { get => _header;  set { _header = value;  OnChanged(); OnChanged(nameof(DisplayName)); } }
    public string Field   { get => _field;   set { _field = value;   OnChanged(); OnChanged(nameof(DisplayName)); } }
    public string Expression { get => _expression; set { _expression = value; OnChanged(); OnChanged(nameof(DisplayName)); } }
    public string Format  { get => _format;  set { _format = value;  OnChanged(); } }
    public float? Width   { get => _width;   set { _width = value;   OnChanged(); OnChanged(nameof(WidthText)); OnChanged(nameof(ColumnWidth)); } }
    public bool   Visible { get => _visible; set { _visible = value; OnChanged(); } }

    /// <summary>Šířka jako text (prázdné = auto).</summary>
    public string WidthText
    {
        get => Controls.ColumnWidthText.Format(_width);
        set => Width = Controls.ColumnWidthText.Parse(value);
    }

    // IColumnRowVM - sdílený seznam sloupců
    public string DisplayName => !string.IsNullOrWhiteSpace(_header) ? _header
                               : !string.IsNullOrWhiteSpace(_field) ? "{" + _field + "}"
                               : !string.IsNullOrWhiteSpace(_expression) ? "= " + _expression
                               : "(nový sloupec)";
    public float? ColumnWidth => Width;

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
    private void OnChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public TableColumn ToModel() => new()
    {
        Header      = Header,
        Field       = string.IsNullOrWhiteSpace(Field) ? null : Field,
        Expression  = string.IsNullOrWhiteSpace(Expression) ? null : Expression.Trim(),
        Format      = string.IsNullOrWhiteSpace(Format) ? null : Format.Trim(),
        Width       = Width ?? 0f,               // 0 = auto (rozdělí šířku elementu)
        WidthMode   = ColumnWidthMode.Mm,
        Visible     = Visible,
        HeaderStyle = HeaderStyle.ToModel(),
        DataStyle   = DataStyle.ToModel()
    };

    public static DynamicTableColumnVM FromModel(TableColumn c,
        ResolvedTextStyle headerInherited, ResolvedTextStyle dataInherited) => new()
    {
        Header      = c.Header,
        Field       = c.Field ?? "",
        Expression  = c.Expression ?? "",
        Format      = c.Format ?? "",
        Width       = c.Width > 0f ? c.Width : (float?)null,   // 0 → auto
        Visible     = c.Visible,
        HeaderStyle = CellStyleVM.FromModel(c.HeaderStyle, headerInherited),
        DataStyle   = CellStyleVM.FromModel(c.DataStyle,   dataInherited)
    };
}

// ─── Dialog ViewModel ────────────────────────────────────────────────────────

class DynamicTableEditorDialogViewModel : EDViewModel
{
    // Tab 1 „Obecné" = embedded EDInputForm (DynamicTableElementEdit.cls) - řeší code-behind
    // přes ElementFormBinder. VM drží jen per-sloupec editaci (tab 2).

    public ObservableCollection<DynamicTableColumnVM> ColumnVMs { get; } = [];

    public float[] FontSizeOptions { get; } = [4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    /// <summary>Dostupná datová pole aktuální kolekce (pro našeptávání „Pole").</summary>
    public ObservableCollection<string> AvailableFields { get; } = [];

    public CellStyleEditOptions HeaderEditOptions { get; } = CellStyleEditOptions.AllEnabled;
    public CellStyleEditOptions DataEditOptions   { get; } = CellStyleEditOptions.AllEnabled;

    // ── Overflow detekce ────────────────────────────────────────────────────

    public float ElementWidth => _element.Width;

    public float TotalVisibleColumnWidth =>
        ColumnVMs.Where(c => c.Visible).Sum(c => c.Width ?? 0f);

    public bool HasColumnOverflow => TotalVisibleColumnWidth > ElementWidth + 0.01f;

    public string ColumnOverflowText =>
        $"⚠ Součet šířek sloupců ({TotalVisibleColumnWidth:0.#} mm) přesahuje šířku prvku ({ElementWidth:0.#} mm). Obsah se oříže.";

    public List<TableColumn> ResultColumns { get; private set; } = [];

    // ── Výběr ─────────────────────────────────────────────────────────────────

    private DynamicTableColumnVM? _selectedColumnVM;
    public DynamicTableColumnVM? SelectedColumnVM
    {
        get => _selectedColumnVM;
        set { Set(ref _selectedColumnVM, value); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedColumnVM is not null;

    // ── Commands ─────────────────────────────────────────────────────────────────

    public RelayCommand MoveUpCommand   { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand AddColumnCommand    { get; }
    public RelayCommand RemoveColumnCommand { get; }

    public RelayCommand ResetStyleCommand           { get; }
    public RelayCommand ResetAllColumnStylesCommand { get; }
    public RelayCommand FitColumnsToWidthCommand    { get; }

    // ── Konstruktor ────────────────────────────────────────────────────────────────

    private readonly DynamicTableElement _element;
    private readonly ResolvedTextStyle _headerInherited;
    private readonly ResolvedTextStyle _dataInherited;

    public DynamicTableEditorDialogViewModel(
        DynamicTableElement element,
        IReadOnlyList<DataFieldCollection> collections)
    {
        _element = element;

        _headerInherited = element.ResolveColumnHeaderParent();
        _dataInherited   = element.ResolveColumnDataParent();

        foreach (var col in element.Columns)
            ColumnVMs.Add(Wire(DynamicTableColumnVM.FromModel(col, _headerInherited, _dataInherited)));

        // Nabídka polí „Pole" pro aktuální zdroj (načtený element) - jen našeptávání, neseeduje sloupce.
        var current = collections.FirstOrDefault(c => c.CollectionKey == element.DataSource);
        foreach (var fld in current?.Fields ?? [])
            AvailableFields.Add(fld.FieldName);

        if (ColumnVMs.Count > 0) SelectedColumnVM = ColumnVMs[0];

        MoveUpCommand   = Command(MoveUp);
        MoveDownCommand = Command(MoveDown);
        AddColumnCommand    = Command(AddColumn);
        RemoveColumnCommand = Command(RemoveColumn);

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

        FitColumnsToWidthCommand = Command(() =>
        {
            var visible = ColumnVMs.Where(c => c.Visible).ToList();
            if (visible.Count == 0) return;
            float total = visible.Sum(c => c.Width ?? 0f);
            if (total <= 0.01f) return;
            float scale = ElementWidth / total;
            foreach (var vm in visible)
                if (vm.Width is float w) vm.Width = (float)Math.Round(w * scale, 2);
        });
    }

    private DynamicTableColumnVM Wire(DynamicTableColumnVM vm)
    {
        vm.PropertyChanged += OnColumnChanged;
        return vm;
    }

    /// <summary>Zdroj, ze kterého byly sloupce naposledy auto-seedovány (null = sloupce jsou ruční /
    /// z uložené šablony, ne auto). Řídí re-seed při změně zdroje.</summary>
    private string? _seededFromKey;

    /// <summary>
    /// Reakce na výběr zdroje dat: obnoví nabídku polí (našeptávání „Pole") a naseeduje sloupce z polí
    /// kolekce (Header = label, Field = název, auto šířka). Sloupce nahradí jen když jsou prázdné, nebo
    /// když byly auto-seedovány z JINÉHO zdroje (uživatel přepnul zdroj). Ruční / z uložené šablony
    /// (bez auto-seed značky) nikdy nepřepíše.
    /// </summary>
    public void SeedColumnsFromCollection(IReadOnlyList<DataField> fields, string sourceKey)
    {
        AvailableFields.Clear();
        foreach (var f in fields) AvailableFields.Add(f.FieldName);

        bool autoSeeded   = _seededFromKey is not null;
        bool sourceChanged = _seededFromKey != sourceKey;

        // Nesahat: prázdné není (máme sloupce) A nejde o přepnutí auto-seedovaného zdroje.
        if (ColumnVMs.Count > 0 && !(autoSeeded && sourceChanged)) return;

        foreach (var vm in ColumnVMs.ToList()) vm.PropertyChanged -= OnColumnChanged;
        ColumnVMs.Clear();

        foreach (var col in DynamicTableElement.BuildColumnsFromFields(fields))
            ColumnVMs.Add(Wire(DynamicTableColumnVM.FromModel(col, _headerInherited, _dataInherited)));
        _seededFromKey = sourceKey;

        SelectedColumnVM = ColumnVMs.Count > 0 ? ColumnVMs[0] : null;
        OnPropertyChanged(nameof(TotalVisibleColumnWidth));
        OnPropertyChanged(nameof(HasColumnOverflow));
        OnPropertyChanged(nameof(ColumnOverflowText));
    }

    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DynamicTableColumnVM.Width) ||
            e.PropertyName == nameof(DynamicTableColumnVM.Visible))
        {
            OnPropertyChanged(nameof(TotalVisibleColumnWidth));
            OnPropertyChanged(nameof(HasColumnOverflow));
            OnPropertyChanged(nameof(ColumnOverflowText));
        }
    }

    // ── Přidání / odebrání ───────────────────────────────────────────────────────

    private void AddColumn()
    {
        var vm = Wire(DynamicTableColumnVM.FromModel(new TableColumn { Header = "", Width = 0f },
            _headerInherited, _dataInherited));
        ColumnVMs.Add(vm);
        SelectedColumnVM = vm;
    }

    private void RemoveColumn()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        SelectedColumnVM.PropertyChanged -= OnColumnChanged;
        ColumnVMs.Remove(SelectedColumnVM);
        SelectedColumnVM = ColumnVMs.Count > 0 ? ColumnVMs[Math.Min(idx, ColumnVMs.Count - 1)] : null;
        OnPropertyChanged(nameof(TotalVisibleColumnWidth));
        OnPropertyChanged(nameof(HasColumnOverflow));
        OnPropertyChanged(nameof(ColumnOverflowText));
    }

    // ── Přesouvání ──────────────────────────────────────────────────────────────

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

    // ── OK ───────────────────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        // Element-level styl + ostatní general props řeší embedded EDInputForm (code-behind).
        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
