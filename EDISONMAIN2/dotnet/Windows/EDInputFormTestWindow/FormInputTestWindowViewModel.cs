using ALWA.SYS;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

class FormInputTestWindowViewModel : EDViewModel
{
    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }

    public FormInputTestWindowViewModel(MLRest mlRest) : base(mlRest)
    {
        ConfirmOnCancel = true;
    }
}
