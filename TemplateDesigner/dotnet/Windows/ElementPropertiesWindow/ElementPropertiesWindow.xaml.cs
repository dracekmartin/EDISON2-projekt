using System.Windows;

namespace TemplateDesigner;

public partial class ElementPropertiesWindow : Window
{
    public ElementPropertiesWindow(ElementPropertiesWindowViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        // Zobraz deskriptory připravené voláním PrepareFor* před vytvořením okna
        if (vm.CurrentDescriptors is not null)
        {
            PropertiesPanel.SetContext(vm.ExecuteCommand, vm.CurrentInvalidate);
            PropertiesPanel.ShowDescriptors(vm.CurrentDescriptors);
        }

        // Interní refresh panelu (např. při změně TableMode v EnhanceTableEditor)
        vm.RequestShowDescriptors = descriptors =>
        {
            PropertiesPanel.SetContext(vm.ExecuteCommand, vm.CurrentInvalidate);
            PropertiesPanel.ShowDescriptors(descriptors);
        };

        // Standardní EDViewModel dialog pattern — VM zavírá dialog přes CloseDialog()
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
