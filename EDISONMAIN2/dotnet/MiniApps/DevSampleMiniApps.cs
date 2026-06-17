using System.Windows;
using System.Windows.Controls;
using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Dočasná dev mini-app (placeholder, než přijdou reálné agendové providery). Lightweight
/// <see cref="SimpleMiniAppViewModel"/> — ověřuje launcher + manager + persistenci end-to-end.
/// </summary>
public sealed class DevSampleMiniApps : IMiniAppProvider
{
    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Dev.Sample", "Test miniapp", () => new SimpleMiniAppViewModel(
            "Test miniapp",
            () => new TextBlock { Text = "Ahoj z miniapp", Margin = new Thickness(8) })),
    ];
}
