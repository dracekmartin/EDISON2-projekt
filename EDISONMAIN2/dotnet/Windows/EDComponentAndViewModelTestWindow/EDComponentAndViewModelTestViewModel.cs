using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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
            StandardActions.Add(), StandardActions.Edit(), StandardActions.Delete(),
            new EDAction("Auto", new RelayCommand(OnCar),
                (ImageSource)Application.Current.FindResource("EDIcons.Car")),
        ];
    }

    protected override void OnAdd() => Status = "Přidat [Ctrl+N]";
    protected override void OnEdit() => Status = "Upravit [Ctrl+E]";
    protected override void OnDelete() => Status = "Smazat [Ctrl+D]";
    private void OnCar() => Status = "Auto";

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

    private string _status = "Připraveno.";
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }
}
