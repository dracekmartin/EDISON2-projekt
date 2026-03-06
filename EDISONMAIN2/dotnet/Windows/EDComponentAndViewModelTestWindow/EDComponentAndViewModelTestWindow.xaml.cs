using EDControls.EDInputForm;
using System.Linq;
using System.Windows;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

public partial class EDComponentAndViewModelTestWindow : Window
{
    private readonly EDComponentAndViewModelTestViewModel _vm;

    public EDComponentAndViewModelTestWindow()
    {
        DataContext = _vm = new EDComponentAndViewModelTestViewModel(() => TheInputForm.Save());
        InitializeComponent();
        TheInputForm.CacheClass = "ALVA.EDISONMAIN2.test";
    }

    private void Init_Click(object sender, RoutedEventArgs e)
    {
        TheInputForm.Init();
        int count = TheInputForm.Items?.Count() ?? 0;
        _vm.Status = $"Init OK — {count} položek načteno";
    }
}
