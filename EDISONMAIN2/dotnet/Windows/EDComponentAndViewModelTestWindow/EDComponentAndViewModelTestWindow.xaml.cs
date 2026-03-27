using System.Windows;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

public partial class EDComponentAndViewModelTestWindow : Window
{
    public EDComponentAndViewModelTestWindow()
    {
        DataContext = new EDComponentAndViewModelTestViewModel();
        InitializeComponent();
        TheInputForm.CacheClass = "ALVA.EDControls.EDInputForm.EDIFExample";
    }
}
