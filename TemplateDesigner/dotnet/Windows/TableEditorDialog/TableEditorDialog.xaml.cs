using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
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
        Header    = Header,
        Field     = string.IsNullOrWhiteSpace(Field) ? null : Field.Trim().Trim('{', '}'),
        Width     = Width,
        WidthMode = WidthMode == "%" ? ColumnWidthMode.Percent : ColumnWidthMode.Mm,
        Alignment = AlignmentLabel switch
        {
            "Střed"  => TextHorizontalAlignment.Center,
            "Vpravo" => TextHorizontalAlignment.Right,
            _        => TextHorizontalAlignment.Left
        },
        Format  = string.IsNullOrWhiteSpace(Format) ? null : Format,
        Visible = Visible
    };

    public static ColumnVM FromModel(TableColumn c) => new()
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
        Format  = c.Format,
        Visible = c.Visible
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

// ─── Dialog ──────────────────────────────────────────────────────────────────

public partial class TableEditorDialog : Window
{
    private readonly TableEditorDialogViewModel _vm;

    /// <summary>Výsledné sloupce po OK — pass-through z VM.</summary>
    public List<TableColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Výsledné řádky po OK — pass-through z VM.</summary>
    public List<TableRow>? ResultRows => _vm.ResultRows;

    public TableEditorDialog(TableElement element, IReadOnlyList<DataField>? collectionFields = null,
        ITableTemplateProvider? templateProvider = null)
    {
        _vm         = new TableEditorDialogViewModel(element, collectionFields, templateProvider);
        DataContext = _vm;
        InitializeComponent();

        _vm.RequestScrollColumnIntoView = vm => ColumnsGrid.ScrollIntoView(vm);
        _vm.RequestCommitEdits          = () =>
        {
            ColumnsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            CellsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        };

        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    // Klávesová navigace v DataGridu — Enter=BeginEdit, Tab=CommitEdit
    // Musí být v code-behind: přistupuje k DataGrid.BeginEdit() / CommitEdit()
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        if (e.Key == Key.Enter && dg.CurrentCell.Item != null)
        {
            dg.BeginEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
        }
    }
}
