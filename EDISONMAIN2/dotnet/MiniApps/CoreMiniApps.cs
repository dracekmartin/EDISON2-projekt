using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Vestavěné shell mini-apps (na rozdíl od dev placeholderů v <see cref="DevSampleMiniApps"/>).
/// Widgety podpora.m-line.cz tu už nejsou - kanál IT → uživatelé nese fixní pruh nahoře
/// v shellu, mimo dokovací plochu.
/// </summary>
public sealed class CoreMiniApps : IMiniAppProvider
{
    public string GroupTitle => "Nástroje";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Core.AutoRefresh", "Automatická obnova", () => new PeriodicRefreshMiniAppViewModel()),
    ];
}
