using EDControls.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Registruje miniaplikaci „Plocha" (launcher zástupců). Sdílí
/// <see cref="ModuleShortcutsService"/> se shellem (strom ★ ⇄ dlaždice).
/// </summary>
public sealed class DesktopMiniAppProvider(ModuleShortcutsService shortcuts) : IMiniAppProvider
{
    public const string DesktopId = "Core.Plocha";

    public string GroupTitle => "Moduly";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new(DesktopId, "Plocha", () => new DesktopMiniAppViewModel(shortcuts)),
    ];
}
