using System.Windows;
using EDControls.MiniApps;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Vestavěné shell mini-apps (na rozdíl od dev placeholderů v <see cref="DevSampleMiniApps"/>).
/// Zatím nástěnka — dřív pevný sloupec v shellu s WebView2, teď dockovatelná lightweight mini-app.
/// </summary>
public sealed class CoreMiniApps : IMiniAppProvider
{
    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Core.Nastenka", "Nástěnka", () => new SimpleMiniAppViewModel(
            "Nástěnka",
            () => new WebView2 { Source = new Uri("https://www.m-line.cz/Nastenka1.html") })),
    ];
}
