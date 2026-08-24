# Mini-apps shellu - poznámky pro vývojáře

Dlaždicové widgety hlavního okna. Framework je v `EDControls/MiniApps`, shell je hostuje
jako AvalonDock `LayoutDocument` v centrálním pane (`AvalonDockHost`).

- `DockMinWidth/Height` razítkuje `AvalonDockHost` **každému** pane po `Layout.Updated`
  (hook nutno obnovovat přes `LayoutChanged` - Deserialize mění celý LayoutRoot); bez toho
  AvalonDock pane pustí na svých 25 px a miniaplikace fakticky zmizí. Bez otevřené mini-app
  Plocha (`HasOpenDesktop`) se pod stromem ukáže `EDInfoBar` s akcí na její zobrazení.
- Widgety `podpora.m-line.cz` v docku **nejsou** - kanál IT → uživatelé nese fixní pruh
  v shellu (`EdisonMainWindow`, `SupportStrip`), mimo dock, aby ho nešlo zavřít ani zúžit.
  Hostí se přes `FontZoomWebView : WebView2` - **HWND, ne composition**, ta crashuje na dragu
  tabu do plovoucího okna. Airspace: WPF veil dimu HWND nepokryje, kartu „Akce probíhá" nad
  ním kreslí owned okno
  (viz [EDActionInProgress/CLAUDE.md](../../../EDControls/dotnet/EDActionInProgress/CLAUDE.md)).
- Font widgetu: `ZoomFactor = FontService.BodySize / 14` (základní text je 14 CSS px), rodina
  se do HTML nepropaguje. Dark mode widget sám neumí (natvrdo `bg-white`) - injektuje se
  inverzní CSS filtr (`ThemeService.Applied`) + tmavé `DefaultBackgroundColor` proti bílému
  záblesku. Chrome docku EDTheme tokeny nečte, dark řeší swap `DockManager.Theme` na VS2013
  Light/Dark v code-behind okna.
- **Vlastní okna docku (overlay s drop terči, plovoucí okna) nikdo zvenčí nesmí zavírat ani
  k nim věšet owned okna** - AvalonDock si overlay cachuje a cizí `Close()` shodí každý další
  drag (hlídá `FrameworkWindows` v EDControls, detail v jeho
  [EDViewModel/CLAUDE.md](../../../EDControls/dotnet/EDViewModel/CLAUDE.md)).
  Odtržené mini-apps zavírej přes `document.Close()` (`MiniAppManager`), ne přes okno.
- Layout per IDFa v `LocalPrefs.MiniAppLayouts` (se šířkou stromu v `LocalPrefs.TreeWidths`,
  stejný lifecycle přes `AttachTreeLayout`); chybí-li nebo je poškozený → výchozí rozložení
  (jen Plocha). Prázdný dock ukáže placeholder s tlačítkem na výchozí sadu.
- Menu „Miniaplikace" je seskupené podle providerů (`IMiniAppProvider.GroupTitle`, pořadí =
  pořadí registrace v `AppBootstrapper`). Statické akce dole (Zavřít/Resetovat) jsou
  v `CompositeCollection` - bindingy tam jdou jen přes `BindingProxy` (`VmProxy`):
  `x:Reference` na TopMenu při BAML loadu cyklí, `ElementName`/DataContext jsou mimo
  namescope/strom; lokální `Style` na nich blokuje `ItemContainerStyle` skupin.
