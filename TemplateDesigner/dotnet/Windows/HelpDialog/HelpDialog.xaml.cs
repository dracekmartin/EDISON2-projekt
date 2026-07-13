using System.Windows;
using System.Windows.Input;

namespace TemplateDesigner;

public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        DataContext = new HelpDialogViewModel();
        InitializeComponent();

        // Ctrl+F fokusne hledání (stejný zvyk jako v EDInputForm)
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
        };
    }

    // Esc v hledání: neprázdné → smazat filtr, prázdné → nechat probublat (IsCancel zavře okno)
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
    }
}
