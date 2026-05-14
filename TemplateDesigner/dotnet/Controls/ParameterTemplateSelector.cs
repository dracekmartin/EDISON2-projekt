using System.Windows;
using System.Windows.Controls;

namespace TemplateDesigner.Controls;

/// <summary>
/// Vybírá DataTemplate pro ParameterControlVM podle typu parametru a HasCommit flagu.
/// Používá se s ItemsControl.ItemTemplateSelector v MainWindow (preview lišta) i PrintDialog.
/// Šablony jsou definované ve sdílené ResourceDictionary <c>Controls/ParameterTemplates.xaml</c>.
/// </summary>
public class ParameterTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BoolTemplate          { get; set; }
    public DataTemplate? EnumTemplate          { get; set; }
    public DataTemplate? CommitStringTemplate  { get; set; }
    public DataTemplate? DefaultTemplate       { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ParameterControlVM vm) return DefaultTemplate;

        // Parametry s commit callbackem (LINKA_ID v preview) → template s tlačítkem "Načíst"
        if (vm.HasCommit && CommitStringTemplate is not null)
            return CommitStringTemplate;

        return (vm.ParameterType switch
        {
            "bool" => BoolTemplate,
            "enum" => EnumTemplate,
            _      => DefaultTemplate
        }) ?? DefaultTemplate;
    }
}
