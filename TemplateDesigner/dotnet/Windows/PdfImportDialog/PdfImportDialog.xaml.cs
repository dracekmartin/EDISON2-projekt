using System.Windows;
using Microsoft.Win32;

namespace TemplateDesigner;

public partial class PdfImportDialog : Window
{
    public PdfImportDialog(PdfImportDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        vm.RequestClose += result => { DialogResult = result; Close(); };

        vm.RequestOpenFile = () =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = "PDF soubory (*.pdf)|*.pdf",
                Title = "Vyberte PDF soubor pro import"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        };

        vm.RequestShowError = (msg, title) =>
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        vm.RequestShowWarning = (msg, title) =>
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        vm.RequestShowInfo = (msg, title) =>
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
