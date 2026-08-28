using EDControls.EDViewModel;

namespace EDISONMAIN2;

class EDInputFormTestViewModel : EDViewModel
{
    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }

    public EDInputFormTestViewModel()
    {
        ConfirmOnCancel = true;
    }
}
