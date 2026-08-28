using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using EDControls.EDViewModel;
// Aliasy proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

/// <summary>Ukázkový EDViewModel - StandardActions, RelayCommand, ActionInProgress, Set(), vyhození chyby.</summary>
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

    public AsyncRelayCommand SimulateActionCommand => _simulateActionCommand
        ??= CommandAsync(SimulateActionAsync);
    private AsyncRelayCommand? _simulateActionCommand;

    public RelayCommand ThrowCommand => _throwCommand
        ??= Command(() => throw new InvalidOperationException("Testovací výjimka z RelayCommand."));
    private RelayCommand? _throwCommand;

    private async Task SimulateActionAsync()
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
