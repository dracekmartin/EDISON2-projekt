using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

/// <summary>
/// Demonstrates EDViewModel usage: inherits MLRest, overrides OnOk() for validation,
/// and uses the Set() helper for property change notification.
/// The save delegate is injected from the code-behind so the ViewModel stays View-agnostic.
/// </summary>
class EDComponentAndViewModelTestViewModel : EDViewModel
{
    public EDComponentAndViewModelTestViewModel()
    {
        Actions =
        [
            new EDAction((string)Application.Current.FindResource("EDStrings.Add"),    new RelayCommand(() => Status = "Přidat (Ins)"),  (ImageSource)Application.Current.FindResource("EDIcons.Add"),    Key.Insert),
            new EDAction((string)Application.Current.FindResource("EDStrings.Edit"),   new RelayCommand(() => Status = "Upravit (F2)"),  (ImageSource)Application.Current.FindResource("EDIcons.Edit"),   Key.F2),
            new EDAction((string)Application.Current.FindResource("EDStrings.Delete"), new RelayCommand(() => Status = "Smazat (Del)"),  (ImageSource)Application.Current.FindResource("EDIcons.Delete"), Key.Delete),
        ];
    }

    public RelayCommand SimulateActionCommand => _simulateActionCommand
        ??= new RelayCommand(SimulateAction);
    private RelayCommand? _simulateActionCommand;

    public RelayCommand ThrowCommand => _throwCommand
        ??= new RelayCommand(() => throw new InvalidOperationException("Testovací výjimka z RelayCommand."));
    private RelayCommand? _throwCommand;

    private async void SimulateAction()
    {
        using var _ = ActionInProgress();
        await Task.Delay(3000);
    }

    private string _status = "Stiskněte Init pro načtení formuláře.";
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }
}
