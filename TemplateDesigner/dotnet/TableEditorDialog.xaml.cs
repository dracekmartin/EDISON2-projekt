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
    public ObservableCollection<CellVM> Cells { get; } = [];

    public string RowSummary => Cells.Count > 0
        ? string.Join(" | ", Cells.Take(3).Select(c => c.Content))
        : "(prázdný řádek)";

    public event PropertyChangedEventHandler? PropertyChanged;
    public void NotifySummary() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowSummary)));

    public TableRow ToModel() => new()
    {
        Cells = Cells.Select(c => new TableCell { Content = c.Content }).ToList()
    };

    public static RowVM FromModel(TableRow row, IList<TableColumn> columns)
    {
        var vm = new RowVM();
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
    private readonly TableElement           _element;
    private readonly IReadOnlySet<string>?  _validFieldNames;
    private readonly ITableTemplateProvider? _templateProvider;

    public ObservableCollection<ColumnVM>  ColumnVMs { get; } = [];
    public ObservableCollection<RowVM>     RowVMs    { get; } = [];

    // Možnosti pro ComboBox sloupce
    public string[] WidthModeOptions  { get; } = ["mm", "%"];
    public string[] AlignmentOptions  { get; } = ["Vlevo", "Střed", "Vpravo"];

    /// <summary>Výsledné sloupce po kliknutí OK.</summary>
    public List<TableColumn> ResultColumns { get; private set; } = [];

    /// <summary>Výsledné řádky po kliknutí OK (null pro Dynamic mód).</summary>
    public List<TableRow>? ResultRows { get; private set; }

    /// <summary>Dostupná pole skupiny pro ComboBox v editoru sloupců.</summary>
    public string[] FieldNameOptions { get; }

    /// <param name="collectionFields">Dostupná pole kolekce (pro validaci + picker). Null = validace vypnuta.</param>
    /// <param name="templateProvider">Provider šablon tabulek. Null = tlačítko "Ze šablony..." se skryje.</param>
    public TableEditorDialog(TableElement element, IReadOnlyList<DataField>? collectionFields = null,
        ITableTemplateProvider? templateProvider = null)
    {
        _element          = element;
        _templateProvider = templateProvider;
        FieldNameOptions  = collectionFields?.Select(f => f.FieldName).OrderBy(n => n).ToArray() ?? [];
        _validFieldNames = FieldNameOptions.Length > 0
            ? new HashSet<string>(FieldNameOptions, StringComparer.OrdinalIgnoreCase)
            : null;
        InitializeComponent();
        DataContext = this;

        // Tlačítko "Ze šablony..." pouze pokud je provider dostupný a mód je Fixed
        BtnLoadTemplate.Visibility = (_templateProvider is not null && element.Mode == TableMode.Fixed)
            ? Visibility.Visible : Visibility.Collapsed;

        // Naplnění sloupců — pro Dynamic tabulku bez sloupců předvyplnit ze schématu
        var sourceCols = element.Columns.Count == 0 && element.Mode == TableMode.Dynamic && collectionFields is { Count: > 0 }
            ? collectionFields.Select(f => new TableColumn
                { Header = f.Label, Field = f.FieldName,
                  Width = MathF.Round(element.Width / collectionFields.Count, 1) })
            : element.Columns.AsEnumerable();

        foreach (var col in sourceCols)
        {
            var vm = ColumnVM.FromModel(col);
            vm.ValidFieldNames = _validFieldNames;
            ColumnVMs.Add(vm);
        }

        ColumnsGrid.ItemsSource = ColumnVMs;

        // Záložka Řádky pouze pro Fixed mód
        TabRows.Visibility = element.Mode == TableMode.Fixed
            ? Visibility.Visible : Visibility.Collapsed;

        if (element.Mode == TableMode.Fixed)
        {
            var cols = element.Columns;
            foreach (var row in element.Rows ?? [])
                RowVMs.Add(RowVM.FromModel(row, cols));

            RowsList.ItemsSource = RowVMs;
        }
    }

    // ─── Sloupce ─────────────────────────────────────────────────────

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        var vm = new ColumnVM { Header = $"Sloupec {ColumnVMs.Count + 1}", Width = 30f };
        vm.ValidFieldNames = _validFieldNames;
        ColumnVMs.Add(vm);
        ColumnsGrid.SelectedItem = vm;
        ColumnsGrid.ScrollIntoView(vm);

        // Pro Fixed mód: přidej buňku do každého existujícího řádku
        if (_element.Mode == TableMode.Fixed)
            foreach (var row in RowVMs)
            {
                row.Cells.Add(new CellVM { ColumnHeader = vm.Header, Content = "" });
                row.NotifySummary();
            }
    }

    private void RemoveColumn_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnsGrid.SelectedItem is not ColumnVM vm) return;
        int idx = ColumnVMs.IndexOf(vm);
        ColumnVMs.Remove(vm);

        if (_element.Mode == TableMode.Fixed && idx >= 0)
            foreach (var row in RowVMs)
            {
                if (idx < row.Cells.Count) row.Cells.RemoveAt(idx);
                row.NotifySummary();
            }
    }

    private void MoveColumnUp_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnsGrid.SelectedItem is not ColumnVM vm) return;
        int idx = ColumnVMs.IndexOf(vm);
        if (idx <= 0) return;
        ColumnVMs.Move(idx, idx - 1);
    }

    private void MoveColumnDown_Click(object sender, RoutedEventArgs e)
    {
        if (ColumnsGrid.SelectedItem is not ColumnVM vm) return;
        int idx = ColumnVMs.IndexOf(vm);
        if (idx < 0 || idx >= ColumnVMs.Count - 1) return;
        ColumnVMs.Move(idx, idx + 1);
    }

    // ─── Řádky ───────────────────────────────────────────────────────

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        var vm = new RowVM();
        foreach (var col in ColumnVMs)
            vm.Cells.Add(new CellVM { ColumnHeader = col.Header, Content = "" });
        RowVMs.Add(vm);
        RowsList.SelectedItem = vm;
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (RowsList.SelectedItem is RowVM vm)
            RowVMs.Remove(vm);
    }

    private void MoveRowUp_Click(object sender, RoutedEventArgs e)
    {
        if (RowsList.SelectedItem is not RowVM vm) return;
        int idx = RowVMs.IndexOf(vm);
        if (idx <= 0) return;
        RowVMs.Move(idx, idx - 1);
        RowsList.SelectedItem = vm;
    }

    private void MoveRowDown_Click(object sender, RoutedEventArgs e)
    {
        if (RowsList.SelectedItem is not RowVM vm) return;
        int idx = RowVMs.IndexOf(vm);
        if (idx < 0 || idx >= RowVMs.Count - 1) return;
        RowVMs.Move(idx, idx + 1);
        RowsList.SelectedItem = vm;
    }

    private void RowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RowsList.SelectedItem is RowVM row)
        {
            CellsGrid.ItemsSource = row.Cells;
        }
        else
        {
            CellsGrid.ItemsSource = null;
        }
    }

    // ─── OK / Storno ─────────────────────────────────────────────────

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Ukončení editace aktivní buňky v DataGridu
        ColumnsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        CellsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();

        if (_element.Mode == TableMode.Fixed)
            ResultRows = RowVMs.Select(rvm => rvm.ToModel()).ToList();

        DialogResult = true;
    }

    // ─── Ze šablony ──────────────────────────────────────────────────

    private const string DlgTitleLoadTemplate = "Načíst šablonu";

    private void LoadTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_templateProvider is null) return;

        var templates = _templateProvider.GetAvailableTemplates();
        if (templates.Count == 0)
        {
            MessageBox.Show("Nejsou dostupné žádné šablony tabulek.", DlgTitleLoadTemplate,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Picker — jednoduchý dialog s ListBoxem
        var picker = new Window
        {
            Title                 = "Vyberte šablonu tabulky",
            Width                 = 340,
            Height                = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = this,
            ResizeMode            = ResizeMode.NoResize,
            ShowInTaskbar         = false
        };

        var list = new System.Windows.Controls.ListBox
        {
            Margin            = new Thickness(10, 10, 10, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        foreach (var t in templates)
            list.Items.Add(new ListBoxItem { Content = t.Label, Tag = t.Key });
        list.SelectedIndex = 0;

        var btnOk = new Button
        {
            Content             = "Použít",
            Width               = 80,
            IsDefault           = true,
            Margin              = new Thickness(0, 8, 8, 10),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        var btnCancel = new Button
        {
            Content             = "Storno",
            Width               = 80,
            IsCancel            = true,
            Margin              = new Thickness(0, 8, 0, 10),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        btnOk.Click     += (_, _) => picker.DialogResult = true;
        btnCancel.Click += (_, _) => picker.DialogResult = false;

        var btnPanel = new StackPanel
        {
            Orientation         = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin              = new Thickness(10, 0, 10, 0)
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
        if (list.SelectedItem is not ListBoxItem selected) return;

        var key = (string)selected.Tag!;
        var def = _templateProvider.GetTemplate(key);
        if (def is null)
        {
            MessageBox.Show("Šablonu se nepodařilo načíst.", DlgTitleLoadTemplate,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Aplikovat šablonu — nahradit sloupce a řádky
        ColumnVMs.Clear();
        foreach (var col in def.Columns)
        {
            var vm = ColumnVM.FromModel(col);
            vm.ValidFieldNames = _validFieldNames;
            ColumnVMs.Add(vm);
        }

        RowVMs.Clear();
        foreach (var row in def.Rows)
            RowVMs.Add(RowVM.FromModel(row, def.Columns));
    }

    // ─── Klávesnicová navigace v DataGridu ───────────────────────────
    // Enter = začít editaci; Tab = přejít do další buňky (zajistí SelectionUnit=Cell)

    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid dg) return;

        if (e.Key == Key.Enter)
        {
            // Enter mimo editaci → začni editovat aktuální buňku
            if (dg.CurrentCell.Item != null)
            {
                dg.BeginEdit();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Tab)
        {
            // Tab v editaci → potvrď a přejdi na další buňku
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            // Výchozí Tab navigace DataGridu zajistí přesun buňky
        }
    }
}
