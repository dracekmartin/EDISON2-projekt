using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TemplateDesigner.Windows.AiGuideDialog;

public partial class AiGuidePanel : UserControl
{
    public AiGuidePanel()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is AiGuidePanelViewModel vm)
                vm.RequestScrollToEnd = () => Dispatcher.BeginInvoke(() => ChatScroll.ScrollToEnd());
        };

        // Enter ve vstupním poli = odeslat. IsDefault tlačítka v UserControlu uvnitř
        // hlavního okna nefunguje (není dialog scope) → explicitní handler.
        InputTextBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            if (DataContext is AiGuidePanelViewModel vm && vm.AskCommand.CanExecute(null))
            {
                vm.AskCommand.Execute(null);
                e.Handled = true;
            }
        };
    }
}
