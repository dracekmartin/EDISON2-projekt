using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Placeholder ověřující cross-assembly pipeline (provider + VM + View + DataTemplate → shell).
/// </summary>
public sealed class MhdSampleMiniAppViewModel : EDMiniAppViewModel
{
    public MhdSampleMiniAppViewModel() { }

    public MhdSampleMiniAppViewModel(EDServices services) : base(services) { }

    public override string Title => "MHD (ukázka)";

    public string Message => "Placeholder mini-app z projektu legacy agend. Sem přijde MHD nad daty.";
}
