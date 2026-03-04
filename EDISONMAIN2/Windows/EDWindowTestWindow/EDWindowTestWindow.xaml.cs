using ALWA.SYS;
using EDControls.EDInputForm;
using System.Linq;
using System.Windows;

namespace EDISONMAIN2.Windows.EDWindowTestWindow;

public partial class EDWindowTestWindow : Window
{
    private readonly EDWindowTestViewModel? _vm;

    // Parameterless constructor for the WPF designer.
    public EDWindowTestWindow()
    {
        InitializeComponent();
    }

    public EDWindowTestWindow(MLRest mlRest)
    {
        DataContext = _vm = new EDWindowTestViewModel(mlRest, () => TheInputForm.Save());
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
