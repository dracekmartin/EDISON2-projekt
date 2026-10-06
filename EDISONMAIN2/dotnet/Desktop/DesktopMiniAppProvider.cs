using EDControls.MiniApps;

namespace EDISONMAIN2.Desktop;

/// <summary>
/// Registruje miniaplikaci „Plocha" (launcher zástupců). Sdílí
/// <see cref="ModuleShortcutsService"/> se shellem (strom ★ ⇄ dlaždice); je <c>Pinned</c>,
/// takže ji uživatel nezavře a picker ji nenabízí.
/// </summary>
public sealed class DesktopMiniAppProvider(ModuleShortcutsService shortcuts) : IMiniAppProvider
{
    public const string DesktopId = "Core.Plocha";

    public string GroupTitle => "Moduly";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new(DesktopId, "Plocha", () => new DesktopMiniAppViewModel(shortcuts), Pinned: true),
    ];
}
