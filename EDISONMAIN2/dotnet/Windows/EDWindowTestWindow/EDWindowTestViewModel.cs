using ALWA.SYS;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.EDWindowTestWindow;

/// <summary>
/// Demonstrates EDViewModel usage: inherits MLRest, overrides OnOk() for validation,
/// and uses the Set() helper for property change notification.
/// The save delegate is injected from the code-behind so the ViewModel stays View-agnostic.
/// </summary>
class EDWindowTestViewModel(MLRest mlRest, Func<bool> save) : EDViewModel(mlRest)
{
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
