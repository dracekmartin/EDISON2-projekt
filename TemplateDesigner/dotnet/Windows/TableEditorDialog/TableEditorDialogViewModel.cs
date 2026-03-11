using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

class TableEditorDialogViewModel : EDViewModel
{
    private readonly TableElement            _element;
    private readonly IReadOnlySet<string>?   _validFieldNames;
    private readonly ITableTemplateProvider? _templateProvider;

    public ObservableCollection<ColumnVM> ColumnVMs { get; } = [];
    public ObservableCollection<RowVM>    RowVMs    { get; } = [];

    public string[] WidthModeOptions  { get; } = ["mm", "%"];
    public string[] AlignmentOptions  { get; } = ["Vlevo", "Střed", "Vpravo"];
    public string[] FieldNameOptions  { get; }

    /// <summary>Výsledné sloupce po OK — čteny volajícím.</summary>
    public List<TableColumn> ResultColumns { get; private set; } = [];

    /// <summary>Výsledné řádky po OK — null pro Dynamic mód.</summary>
    public List<TableRow>? ResultRows { get; private set; }

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

    public bool IsRowSelected => SelectedRowVM is not null;

    // ── Viditelnost záložky a tlačítka ───────────────────────────────────────

    public bool ShowRowsTab      => _element.Mode == TableMode.Fixed;
    public bool ShowLoadTemplate => _templateProvider is not null && _element.Mode == TableMode.Fixed;

    // ── Callbacks pro View-only operace ──────────────────────────────────────

    /// <summary>Callback: po přidání sloupce scrolluj do view. Wired v code-behind.</summary>
    public Action<ColumnVM>? RequestScrollColumnIntoView { get; set; }

    /// <summary>Callback: před potvrzením commitni editaci DataGridů. Wired v code-behind.</summary>
    public Action? RequestCommitEdits { get; set; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand AddColumnCommand      { get; }
    public RelayCommand RemoveColumnCommand   { get; }
    public RelayCommand MoveColumnUpCommand   { get; }
    public RelayCommand MoveColumnDownCommand { get; }

    public RelayCommand AddRowCommand         { get; }
    public RelayCommand RemoveRowCommand      { get; }
    public RelayCommand MoveRowUpCommand      { get; }
    public RelayCommand MoveRowDownCommand    { get; }

    public RelayCommand LoadTemplateCommand   { get; }

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public TableEditorDialogViewModel(TableElement element,
        IReadOnlyList<DataField>? collectionFields = null,
        ITableTemplateProvider? templateProvider = null)
    {
        _element          = element;
        _templateProvider = templateProvider;

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
            ColumnVMs.Add(WithValidation(ColumnVM.FromModel(col)));

        if (element.Mode == TableMode.Fixed)
        {
            var cols = element.Columns;
            foreach (var row in element.Rows ?? [])
                RowVMs.Add(RowVM.FromModel(row, cols));
        }

        AddColumnCommand      = new RelayCommand(AddColumn);
        RemoveColumnCommand   = new RelayCommand(RemoveColumn);
        MoveColumnUpCommand   = new RelayCommand(MoveColumnUp);
        MoveColumnDownCommand = new RelayCommand(MoveColumnDown);

        AddRowCommand         = new RelayCommand(AddRow);
        RemoveRowCommand      = new RelayCommand(RemoveRow);
        MoveRowUpCommand      = new RelayCommand(MoveRowUp);
        MoveRowDownCommand    = new RelayCommand(MoveRowDown);

        LoadTemplateCommand   = new RelayCommand(LoadTemplate);
    }

    // ── Sloupce ───────────────────────────────────────────────────────────────

    private void AddColumn()
    {
        var vm = WithValidation(new ColumnVM { Header = $"Sloupec {ColumnVMs.Count + 1}", Width = 30f });
        ColumnVMs.Add(vm);
        SelectedColumnVM = vm;
        RequestScrollColumnIntoView?.Invoke(vm);

        if (_element.Mode == TableMode.Fixed)
            foreach (var row in RowVMs)
            {
                row.Cells.Add(new CellVM { ColumnHeader = vm.Header, Content = "" });
                row.NotifySummary();
            }
    }

    private void RemoveColumn()
    {
        if (SelectedColumnVM is not ColumnVM vm) return;
        int idx = ColumnVMs.IndexOf(vm);
        ColumnVMs.Remove(vm);

        if (_element.Mode == TableMode.Fixed && idx >= 0)
            foreach (var row in RowVMs)
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

    // ── Ze šablony ────────────────────────────────────────────────────────────

    private void LoadTemplate()
    {
        if (_templateProvider is null) return;

        var templates = _templateProvider.GetAvailableTemplates();
        if (templates.Count == 0)
        {
            MessageBox.Show("Nejsou dostupné žádné šablony tabulek.", "Načíst šablonu",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show("Šablonu se nepodařilo načíst.", "Načíst šablonu",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ColumnVMs.Clear();
        foreach (var col in def.Columns)
            ColumnVMs.Add(WithValidation(ColumnVM.FromModel(col)));

        RowVMs.Clear();
        foreach (var row in def.Rows)
            RowVMs.Add(RowVM.FromModel(row, def.Columns));
    }

    // ── OK / Storno ───────────────────────────────────────────────────────────

    /// <summary>Commitne editaci (volá View callback), sestaví výsledky a zavře dialog.</summary>
    protected override void OnOk()
    {
        RequestCommitEdits?.Invoke();

        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        if (_element.Mode == TableMode.Fixed)
            ResultRows = RowVMs.Select(rvm => rvm.ToModel()).ToList();

        CloseDialog(true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ColumnVM WithValidation(ColumnVM vm)
    {
        vm.ValidFieldNames = _validFieldNames;
        return vm;
    }
}
