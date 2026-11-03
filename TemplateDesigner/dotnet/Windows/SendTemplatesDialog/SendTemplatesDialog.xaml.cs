using System.Windows;

namespace TemplateDesigner;

public partial class SendTemplatesDialog : Window
{
    private readonly SendTemplatesDialogViewModel _vm;

    public SendTemplatesDialog(SendTemplatesDialogViewModel vm)
    {
        DataContext = _vm = vm;
        InitializeComponent();
        _vm.AttachToWindow(this);
        _vm.RequestClose += result => DialogResult = result;
        Loaded += async (_, _) => await _vm.LoadAsync();
    }
}
