# Mini-apps shellu - poznámky pro vývojáře

Dlaždicové widgety hlavního okna. Framework je v `EDControls/MiniApps`, shell je hostuje
jako AvalonDock `LayoutDocument` v centrálním pane (`AvalonDockHost`).

- `DockMinWidth/Height` razítkuje `AvalonDockHost` **každému** pane po `Layout.Updated`
  (hook nutno obnovovat přes `LayoutChanged` - Deserialize mění celý LayoutRoot); bez toho
  AvalonDock pane pustí na svých 25 px a miniaplikace fakticky zmizí. Bez otevřené mini-app
  Plocha (`HasOpenDesktop`) se pod stromem ukáže `EDInfoBar` s akcí na její zobrazení.
- Widgety `podpora.m-line.cz` v docku **nejsou** - kanál IT → uživatelé nese fixní pruh
  v shellu (`EdisonMainWindow`, `SupportStrip`), mimo dock, aby ho nešlo zavřít ani zúžit;
  URL nese IdFa + login + e-mail, po přepnutí firmy se widget naviguje znovu (změna `Idfa` VM).
  Oba hostitelé (pruh i login) jdou přes `SupportWidgetHost`; `EDISON_SCREENSHOT_MODE=1`
  nahradí widgety neutrálním popiskem (anonymizované screenshoty) - WebView2 se vůbec nevytvoří.
  Hostí se přes `FontZoomWebView : WebView2` - **HWND, ne composition**, ta crashuje na dragu
  tabu do plovoucího okna. Airspace: WPF veil dimu HWND nepokryje, kartu „Akce probíhá" nad
  ním kreslí owned okno ([EDActionInProgress/CLAUDE.md](../../../EDControls/dotnet/EDActionInProgress/CLAUDE.md)).
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
- **Legacy dashboardy jedou přes sdílený `LegacyDashboardLoader`** (`EDISONMAIN2.LegacyMiniApps`):
  požadavky panelů, které se přihlásí v téže dispatcher operaci, odejdou jako **jeden**
  `MiniAplikaceAPI.LoadData` s N parametry `appType` (parita s legacy `clsMiniApplication.LoadData`),
  na klonu mimo UI vlákno. Obnova layoutu běží z `Loaded` = **před prvním paintem**, takže
  blokující load tam stál N round-tripů bez progresu. Provider proto musí dát všem descriptorům
  **tutéž** instanci loaderu - vlastní per panel dávkování zruší.
- **`refresh=0` při otevření panelu, `refresh=1` jen z „Načíst" / hromadné obnovy.** Data
  předpočítává serverová úloha (`ALVA.EDISON.Task`, `TaskTypeALVAEDISONMiniAppRefresh`); přepočet
  při každém otevření layoutu byl hlavní zdroj pomalého startu. Flag je společný pro celou dávku.
  Chybu ukazuje panel sám (`ErrorText`), ne popup - dávka o N panelech by jich zvedla N.
  `MiniAppManager.RefreshAll` proto vrací názvy **spuštěných**, ne dokončených obnov.
- **Sekce `ObjectSectionTable` nese deskriptor typů sloupců**, ne jen marker tabulky: `"T:N2#"` =
  text, číslo se 2 desetinnými a oddělovačem tisíců (grammar v legacy `EDMiniApplication.GetFormat`).
  Bez něj se `N` neformátuje, nezarovná vpravo a řadí lexikograficky; `D` je TEXT, wire posílá datum hotové.
- **Pruh miniaplikace je `EDControls.MiniApps.EDMiniAppBar`**, ne vlastní StackPanel. Barvu
  řádku ze serveru nese `CellStyle` s `BasedOn` nad skrytým sloupcem `_fore` - přes `RowStyle` ji
  implicitní styl buňky přebije setterem a barvy se tiše přestanou zobrazovat. V dark režimu ji
  `LegacyForeColorConverter` zesvětlí na kontrast 4,5:1 (legacy palety počítají s bílým
  pozadím); motiv čte v okamžiku konverze, proto view po `ThemeService.Applied` sekce
  refreshuje.
- **Kolečko myši v panelu**: vnořený seznam si MouseWheel nechá i bez místa k rolování, na kraji ho
  předá ven `EDControls.ScrollWheelBubbling.IsEnabled`; prázdná plocha Plochy musí mít `Background`,
  jinak není hit-testable a kolečko jde mimo ScrollViewer.
- Layout per IDFa v `LocalPrefs.MiniAppLayouts` (se šířkou stromu v `LocalPrefs.TreeWidths`,
  stejný lifecycle přes `AttachTreeLayout`); chybí-li nebo je poškozený → výchozí rozložení
  (jen Plocha). Prázdný dock ukáže placeholder s tlačítkem na výchozí sadu.
- Výběr miniaplikace je vlastní okno (`Windows/MiniAppPickerWindow`) s **jednoúrovňovým**
  seznamem; `IMiniAppProvider.GroupTitle` (pořadí = pořadí registrace v `AppBootstrapper`)
  z něj zbyl jen popisek řádku. Menu „Miniaplikace" na okno jen odkazuje.
- Picker vybírá i **umístění** (`MiniAppPlacement`). Cíle jsou **jen ukotvená** pod-okna:
  `Descendents()` vrací i ta v plovoucích oknech, filtruje se `IsHostedInFloatingWindow`.
  **Poslední document pane AvalonDock neodstraní** - po zavření všeho zbude prázdný přes celou
  plochu, takže bez obsazeného pod-okna picker volbu nenabídne a panel jde cestou `Fill`.
