using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using EDControls.EDViewModel;
// Aliasy proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using Application = System.Windows.Application;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

/// <summary>Demo EDViewModel: StandardActions, RelayCommand, ActionInProgress, Set() helper, error throw.</summary>
class EDComponentAndViewModelTestViewModel : EDViewModel
{
    public EDComponentAndViewModelTestViewModel()
    {
        Actions =
        [
            StandardActions.Add(), StandardActions.Edit(), StandardActions.Delete(),
            new EDAction("Auto", Command(OnCar),
                (ImageSource)Application.Current.FindResource("EDIcons.Car")),
        ];
    }

    protected override void OnAdd() => Status = "Přidat [Ctrl+N]";
    protected override void OnEdit() => Status = "Upravit [Ctrl+E]";
    protected override void OnDelete() => Status = "Smazat [Ctrl+D]";
    private void OnCar() => Status = "Auto";

    public RelayCommand SimulateActionCommand => _simulateActionCommand
        ??= Command(SimulateAction);
    private RelayCommand? _simulateActionCommand;

    public RelayCommand ThrowCommand => _throwCommand
        ??= Command(() => throw new InvalidOperationException("Testovací výjimka z RelayCommand."));
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
