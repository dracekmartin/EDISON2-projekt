using ALWA.SYS;
using EDControls.EDInputForm;
using System.Linq;
using System.Windows;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

public partial class EDComponentAndViewModelTestWindow : Window
{
    private readonly EDComponentAndViewModelTestViewModel? _vm;

    // Parameterless constructor for the WPF designer.
    public EDComponentAndViewModelTestWindow()
    {
        InitializeComponent();
    }

    public EDComponentAndViewModelTestWindow(MLRest mlRest)
    {
        DataContext = _vm = new EDComponentAndViewModelTestViewModel(mlRest, () => TheInputForm.Save());
        InitializeComponent();

        TheInputForm.MLRest = mlRest;
        TheInputForm.CacheClass = "ALVA.EDISONMAIN2.test";
    }

    private void Init_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;    // design time
        TheInputForm.Init();
        int count = TheInputForm.Items?.Count() ?? 0;
        _vm.Status = $"Init OK — {count} položek načteno";
    }
}
