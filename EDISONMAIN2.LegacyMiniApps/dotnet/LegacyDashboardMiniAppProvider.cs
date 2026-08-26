using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Zpřístupní všech 15 legacy dashboardových miniaplikací (pravý panel starého shellu) jako
/// EDISONMAIN2 mini-apps. Všechny sdílí <see cref="LegacyDashboardMiniAppViewModel"/> - liší se jen
/// <c>appType</c> a titulkem. appType id + názvy = legacy <c>ALVAMINIAPLIKACE.inc</c> /
/// <c>EDMiniApplication.GetApplicationName</c>.
/// </summary>
public sealed class LegacyDashboardMiniAppProvider : IMiniAppProvider
{
    // Jeden dávkovač pro všechny descriptory - právě přes něj se požadavky panelů spojí do
    // jednoho requestu. Vlastní instance per panel by dávkování zrušila.
    private readonly LegacyDashboardLoader _loader;

    public LegacyDashboardMiniAppProvider() : this(new LegacyDashboardLoader()) { }

    public LegacyDashboardMiniAppProvider(LegacyDashboardLoader loader) => _loader = loader;

    public string GroupTitle => "Přehledy";

    // (appType, titulek) - pořadí dle ALVAMINIAPLIKACE.inc (52001..52015).
    private static readonly (string AppType, string Title)[] Apps =
    [
        ("52001", "Fluktuace zaměstnanců"),
        ("52002", "Aktualizace dat DS"),
        ("52003", "Lékařské prohlídky zaměstnanců"),
        ("52004", "Školení zaměstnanců"),
        ("52005", "Aktuální období"),
        ("52006", "Údržba vozidel"),
        ("52007", "Kalibrace měřidel"),
        ("52008", "Ujeté km pro dotovaná vozidla"),
        ("52009", "Evidence reklam"),
        ("52010", "Požadavky na údržbu"),
        ("52011", "Plán údržby"),
        ("52012", "Statistika faktur"),
        ("52013", "Statistika objednávek spedice"),
        ("52014", "Sklady MTZ - období a zásoby"),
        ("52015", "Motohodiny vozidel"),
    ];

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
        Apps.Select(a => new MiniAppDescriptor(
            $"Legacy.{a.AppType}", a.Title,
            () => new LegacyDashboardMiniAppViewModel(_loader, a.AppType, a.Title)));
}
