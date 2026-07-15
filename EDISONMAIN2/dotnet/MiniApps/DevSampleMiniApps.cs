using System.Windows;
using System.Windows.Controls;
using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Dočasné dev mini-apps (placeholder, než přijdou reálné agendové providery). Ukazují obě cesty:
/// lightweight <see cref="SimpleMiniAppViewModel"/> a full-MVVM <see cref="DevCounterMiniAppViewModel"/>.
/// </summary>
public sealed class DevSampleMiniApps : IMiniAppProvider
{
    public string GroupTitle => "Dev";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Dev.Sample", "Test miniapp", () => new SimpleMiniAppViewModel(
            "Test miniapp",
            () => new TextBlock { Text = "Ahoj z miniapp", Margin = new Thickness(8) })),
        new("Dev.Counter", "Počítadlo", () => new DevCounterMiniAppViewModel()),
    ];
}
