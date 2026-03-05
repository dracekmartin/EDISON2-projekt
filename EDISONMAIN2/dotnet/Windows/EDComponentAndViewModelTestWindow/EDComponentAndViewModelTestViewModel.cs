using ALWA.SYS;
using EDControls.EDViewModel;
using System.Threading.Tasks;

namespace EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow;

/// <summary>
/// Demonstrates EDViewModel usage: inherits MLRest, overrides OnOk() for validation,
/// and uses the Set() helper for property change notification.
/// The save delegate is injected from the code-behind so the ViewModel stays View-agnostic.
/// </summary>
class EDComponentAndViewModelTestViewModel(MLRest mlRest, Func<bool> save) : EDViewModel(mlRest)
{
    public RelayCommand SimulateActionCommand => _simulateActionCommand
        ??= new RelayCommand(SimulateAction);
    private RelayCommand? _simulateActionCommand;

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

    protected override void OnOk()
    {
        if (!save())
        {
            Status = "Uložení selhalo — opravte chyby ve formuláři.";
            return;   // window stays open
        }
        base.OnOk();  // fires RequestClose(true) → EDWindow closes with DialogResult = true
    }
}
