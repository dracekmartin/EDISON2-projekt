# Mini-apps shellu - poznámky pro vývojáře

Dlaždicové widgety hlavního okna. Framework je v `EDControls/MiniApps`, shell je hostuje
jako AvalonDock `LayoutDocument` v centrálním pane (`AvalonDockHost`).

- `MiniAppDescriptor.Pinned` = uživatel ji nezavře ani neodtrhne (programové `Close()`
  funguje dál - session switch OK), single-instance a **solo pane**: `AvalonDockHost`
  po `Layout.Updated` odsune cizí taby jinam a razítkuje pane `DockMinWidth/Height`
  (hook nutno obnovovat přes `LayoutChanged` - Deserialize mění celý LayoutRoot).
  „Zavřít miniaplikace" jde přes `CloseUnpinned`; `EnsurePinnedOpen` ji dootevře
  po restore / defaultu / resetu. Bez otevřené mini-app Plocha (`HasOpenDesktop`) se pod
  stromem ukáže `EDInfoBar` s akcí na její zobrazení.
- Core widgety (Novinky/Wiki/Školení) = WebView2 na `podpora.m-line.cz`, mirror legacy
  CefSharp plochy z `frmMain.vb` (Školení dostává IdFa + login v URL). Hostují se přes
  `FontZoomWebView : WebView2` - **HWND, ne composition**, ta crashuje na dragu tabu do
  plovoucího okna. Airspace: WPF veil dimu je nepokryje, záměrně zůstávají čitelné; kartu
  „Akce probíhá" nad nimi kreslí owned okno
  (viz [EDActionInProgress/CLAUDE.md](../../../EDControls/dotnet/EDActionInProgress/CLAUDE.md)).
- Font widgetů: `ZoomFactor = FontService.BodySize / 14` (základní text je 14 CSS px), rodina
  se do HTML nepropaguje. Dark mode widgety samy neumí (natvrdo `bg-white`) - injektuje se
  inverzní CSS filtr (`ThemeService.Applied`) + tmavé `DefaultBackgroundColor` proti bílému
  záblesku. Chrome docku EDTheme tokeny nečte, dark řeší swap `DockManager.Theme` na VS2013
  Light/Dark v code-behind okna.
- **Vlastní okna docku (overlay s drop terči, plovoucí okna) nikdo zvenčí nesmí zavírat ani
  k nim věšet owned okna** - AvalonDock si overlay cachuje a cizí `Close()` shodí každý další
  drag (hlídá `FrameworkWindows` v EDControls, detail v jeho
  [EDViewModel/CLAUDE.md](../../../EDControls/dotnet/EDViewModel/CLAUDE.md)).
  Odtržené mini-apps zavírej přes `document.Close()` (`MiniAppManager`), ne přes okno.
- Layout per IDFa v `LocalPrefs.MiniAppLayouts` (se šířkou stromu v `LocalPrefs.TreeWidths`,
  stejný lifecycle přes `AttachTreeLayout`); chybí-li → pinned karusel `Core.Podpora` plus
  Plocha dole. Prázdný dock ukáže placeholder s tlačítkem na výchozí sadu.
- Karusel: 1-3 sloupce dle šířky, při plném zobrazení cyklení stojí, jinak 15 s lišta - krátká
  proto, že odpočet stojí při hoveru/fokusu. Hover se kvůli HWND WebView2 zjišťuje pozicí
  kurzoru + `WindowFromPoint` (ne `IsMouseOver`, a ne jen souřadnice - cizí okno shell
  překrývá). Pauza vyprší po 3 min klidu: WPF fokus je sticky, klik na šipku by cyklení
  zastavil navždy a krátký timeout tam nejde (čtení kolečkem/klávesami kurzorem nehýbe).
- Menu „Miniaplikace" je seskupené podle providerů (`IMiniAppProvider.GroupTitle`, pořadí =
  pořadí registrace v `AppBootstrapper`). Statické akce dole (Zavřít/Resetovat) jsou
  v `CompositeCollection` - bindingy tam jdou jen přes `BindingProxy` (`VmProxy`):
  `x:Reference` na TopMenu při BAML loadu cyklí, `ElementName`/DataContext jsou mimo
  namescope/strom; lokální `Style` na nich blokuje `ItemContainerStyle` skupin.
