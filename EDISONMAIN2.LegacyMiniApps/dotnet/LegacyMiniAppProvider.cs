using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Mini-apps nepředělaných (legacy) agend — čistě WPF nad Caché daty přes MLRest, bez WinForms
/// hostování. Registruje se v shellu (<c>AppBootstrapper</c>). Zatím jen MHD placeholder.
/// </summary>
public sealed class LegacyMiniAppProvider : IMiniAppProvider
{
    public string GroupTitle => "MHD";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Mhd.Dispecink", "MHD dispečink", () => new DispecinkMiniAppViewModel()),
        new("Mhd.Sample", "MHD (ukázka)", () => new MhdSampleMiniAppViewModel()),
    ];
}
