using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

public partial class TableEditorDialog : Window
{
    private readonly TableEditorDialogViewModel _vm;

    /// <summary>Výsledné sloupce po OK - pass-through z VM.</summary>
    public List<TableColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Výsledné řádky po OK - pass-through z VM.</summary>
    public List<TableRow>? ResultRows => _vm.ResultRows;

    /// <summary>Výsledné souhrnné řádky po OK - pass-through z VM.</summary>
    public List<TableRow>? ResultSummaryRows => _vm.ResultSummaryRows;

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
            SummaryCellsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        };

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestPickTableTemplate = PickTableTemplate;
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    /// <summary>View-only picker šablon tabulky (WPF dialog) - VM ho volá přes callback (R4:
    /// ViewModel nesmí stavět WPF). Vrací klíč zvolené šablony, nebo null při Storno.</summary>
    private string? PickTableTemplate(IReadOnlyList<TableTemplateInfo> templates)
    {
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

        // Plná kvalifikace typů: code-behind má v scope i System.Windows.Forms (ColorDialog) → jinak
        // jsou ListBox/Orientation/HorizontalAlignment nejednoznačné.
        var list = new System.Windows.Controls.ListBox { Margin = new Thickness(10, 10, 10, 0) };
        foreach (var t in templates)
            list.Items.Add(new System.Windows.Controls.ListBoxItem { Content = t.Label, Tag = t.Key });
        list.SelectedIndex = 0;

        var btnOk     = new System.Windows.Controls.Button { Content = "Použít", Width = 80, IsDefault = true,
                                     Margin = new Thickness(0, 8, 8, 10),
                                     HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var btnCancel = new System.Windows.Controls.Button { Content = "Storno", Width = 80, IsCancel = true,
                                     Margin = new Thickness(0, 8, 0, 10),
                                     HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
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

        if (picker.ShowDialog() != true) return null;
        if (list.SelectedItem is not System.Windows.Controls.ListBoxItem selected) return null;
        return (string)selected.Tag!;
    }

    // Klávesová navigace v DataGridu - Enter=BeginEdit, Tab=CommitEdit
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
