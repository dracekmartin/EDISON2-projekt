using System.Windows;
using TemplateDesigner.Commands;
using TemplateDesigner.Controls;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using PropertyDescriptor = TemplateDesigner.Models.PropertyDescriptor;

namespace TemplateDesigner;

public partial class DepartureGroupEditorDialog : Window
{
    private readonly DepartureGroupEditorDialogViewModel _vm;

    public List<DepartureGroupColumnStyle> ResultGroupColumns => _vm.ResultGroupColumns;
    public ColumnVisualStyle? ResultHourColumnStyle => _vm.ResultHourColumnStyle;

    public DepartureGroupEditorDialog(
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles,
        ColumnVisualStyle? hourColumnStyle,
        IReadOnlyList<PropertyDescriptor> generalDescriptors,
        Action<IDesignerCommand> executeCommand,
        Action invalidate,
        TextStyle? headerDefaults = null,
        TextStyle? dataDefaults = null)
    {
        _vm = new DepartureGroupEditorDialogViewModel(availableGroups, currentStyles, hourColumnStyle, generalDescriptors, headerDefaults, dataDefaults);
        DataContext = _vm;
        InitializeComponent();

        GeneralPanel.SetContext(executeCommand, invalidate);
        GeneralPanel.ShowDescriptors(generalDescriptors);

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
