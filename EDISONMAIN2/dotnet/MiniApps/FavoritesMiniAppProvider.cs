using EDControls.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Registruje miniaplikaci „Oblíbené" (launcher připnutých modulů). Sdílí
/// <see cref="ModuleShortcutsService"/> se shellem (strom ★ ⇄ miniaplikace).
/// </summary>
public sealed class FavoritesMiniAppProvider(ModuleShortcutsService shortcuts) : IMiniAppProvider
{
    public const string FavoritesId = "Core.Oblibene";

    public string GroupTitle => "Moduly";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new(FavoritesId, "Oblíbené", () => new FavoritesMiniAppViewModel(shortcuts)),
    ];
}
