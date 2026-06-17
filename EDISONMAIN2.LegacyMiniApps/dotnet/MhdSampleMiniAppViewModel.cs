using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Placeholder mini-app pro MHD (nepředělaná agenda) — zatím jen stub, ověřuje cross-assembly
/// pipeline (provider + VM + View + DataTemplate z této assembly → shell). Nahradí ji reálná
/// MHD mini-app nad daty (přes MLRest z <c>EDServices</c>).
/// </summary>
public sealed class MhdSampleMiniAppViewModel : EDMiniAppViewModel
{
    public MhdSampleMiniAppViewModel() { }

    public MhdSampleMiniAppViewModel(EDServices services) : base(services) { }

    public override string Title => "MHD (ukázka)";

    public string Message => "Placeholder mini-app z projektu legacy agend. Sem přijde MHD nad daty.";
}
