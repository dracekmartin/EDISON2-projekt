using System.Windows;
using TemplateDesigner.Commands;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

public partial class StopColumnsEditorDialog : Window
{
    private readonly StopColumnsEditorDialogViewModel _vm;

    /// <summary>Výsledné sloupce po OK — pass-through z VM.</summary>
    public List<StopColumn> ResultColumns => _vm.ResultColumns;

    public StopColumnsEditorDialog(
        List<StopColumn> columns,
        IReadOnlyList<PropertyDescriptor> generalDescriptors,
        Action<IDesignerCommand> executeCommand,
        Action invalidate)
    {
        _vm = new StopColumnsEditorDialogViewModel(columns, generalDescriptors);
        DataContext = _vm;
        InitializeComponent();

        GeneralPanel.SetContext(executeCommand, invalidate);
        GeneralPanel.ShowDescriptors(generalDescriptors);

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
