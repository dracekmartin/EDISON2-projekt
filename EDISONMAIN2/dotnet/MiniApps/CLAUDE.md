# Mini-apps shellu - poznámky pro vývojáře

Dlaždicové widgety hlavního okna. Framework je v `EDControls/dotnet/MiniApps`, shell je hostuje
jako AvalonDock `LayoutDocument` v centrálním pane (`AvalonDockHost`).

- `DockMinWidth/Height` razítkuje `AvalonDockHost` **každému** pane po `Layout.Updated`
  (hook nutno obnovovat přes `LayoutChanged` - Deserialize mění celý LayoutRoot); bez toho
  AvalonDock pane pustí na svých 25 px a miniaplikace fakticky zmizí.
- **Plocha je `MiniAppDescriptor.Pinned`**: bez křížku a bez odtržení (`CanClose/CanFloat/CanMove`),
  picker ji nenabízí, `CloseClosable` ji přeskočí (`CloseAll` pro shutdown/reset ne - programové
  `Close()` dock pustí i tak). `CanClose` se při restore bere z deskriptoru, ne z uloženého XML,
  a shell připnuté po obnově **doplní** (`EnsurePinnedOpen`) - starý layout je mít nemusí. Shell je
  pozná podle `Pinned`, id Plochy zná jen `Desktop/DesktopMiniAppProvider`, který ji registruje.
- Widgety `podpora.m-line.cz` v docku **nejsou**, pruh podpory má vlastní
  [CLAUDE.md](../Windows/EdisonMainWindow/SupportStrip/CLAUDE.md). Chrome docku EDTheme tokeny
  nečte: dark řeší swap `DockManager.Theme` na VS2013 Light/Dark, tab strip navrch dotahuje `DockThemeBrushes`.
  Záložka dokumentu má v tématu VS2013 pevných 19 px, s písmem ji uvolňuje `DockTabHeader` class handlerem
  na `Border.SizeChanged`. `Loaded` nejde: WPF ho do větve bez instančního handleru nepošle, class handler se nepočítá.
- **Vlastní okna docku (overlay s drop terči, plovoucí okna) nikdo zvenčí nesmí zavírat ani
  k nim věšet owned okna** (proč: `FrameworkWindows`, [EDViewModel/CLAUDE.md](../../../EDControls/dotnet/EDViewModel/CLAUDE.md)).
  Odtržené mini-apps zavírej přes `document.Close()` (`MiniAppManager`), ne přes okno.
- **Legacy dashboardy jedou přes sdílený `LegacyDashboardLoader`** (`EDISONMAIN2.LegacyMiniApps`):
  požadavky panelů, které se přihlásí v téže dispatcher operaci, odejdou jako **jeden**
  `MiniAplikaceAPI.LoadData` s N parametry `appType` (parita s legacy `clsMiniApplication.LoadData`),
  na klonu mimo UI vlákno. Obnova layoutu běží z `Loaded` = **před prvním paintem**, takže
  blokující load tam stál N round-tripů bez progresu. Provider proto musí dát všem descriptorům
  **tutéž** instanci loaderu - vlastní per panel dávkování zruší. Mini-app nepředělané agendy je
  vždy nová WPF view nad týmiž Caché daty, **nikdy `WindowsFormsHost`** (focus a z-order kolidují s dockem).
- **`refresh=0` při otevření panelu, `refresh=1` jen z „Načíst" / hromadné obnovy.** Data
  předpočítává serverová úloha (`ALVA.EDISON.Task`, `TaskTypeALVAEDISONMiniAppRefresh`); přepočet
  při každém otevření layoutu byl hlavní zdroj pomalého startu. Flag je společný pro celou dávku.
  Chybu ukazuje panel sám (`ErrorText`), ne popup - dávka o N panelech by jich zvedla N.
  `MiniAppManager.RefreshAll` proto vrací názvy **spuštěných**, ne dokončených obnov.
- **Sekce `ObjectSectionTable` nese deskriptor typů sloupců**, ne jen marker tabulky: `"T:N2#"` =
  text, číslo se 2 desetinnými a oddělovačem tisíců (grammar v legacy `EDMiniApplication.GetFormat`).
  Bez něj se `N` neformátuje, nezarovná vpravo a řadí lexikograficky; `D` je TEXT, wire posílá datum hotové.
- **Label těsně nad tabulkou je její nadpis** - parser ho spotřebuje do `DashboardTable.Title`
  (nese ho pruh EDListu), i když legacy tam dává i souhrny; ostatní labely zůstávají sekcí.
- **Pruh miniaplikace je `EDControls.MiniApps.EDMiniAppBar`**, ne vlastní StackPanel. Barvu
  řádku ze serveru nese `CellStyle` s `BasedOn` nad skrytým sloupcem `_fore` (proč ne `RowStyle`:
  EDControls/CLAUDE.md). V dark režimu ji `LegacyForeColorConverter` zesvětlí na kontrast 4,5:1
  (legacy palety počítají s bílým pozadím); motiv čte v okamžiku konverze, proto view po
  `ThemeService.Applied` sekce refreshuje.
- **Kolečko myši v panelu**: vnořený seznam si MouseWheel nechá i bez místa k rolování, na kraji ho
  předá ven `EDControls.ScrollWheelBubbling.IsEnabled`; prázdná plocha Plochy musí mít `Background`,
  jinak není hit-testable a kolečko jde mimo ScrollViewer.
- Stav docku (`LocalPrefs.MiniAppLayouts`, šířka stromu `TreeWidths`, výška pruhu `SupportStripHeights`) má klíč
  `server:port|idfa|login` (`DockScope`: sdílený terminál, uživatel nerozhází dock jinému), starý klíč jen IDFa se
  čte jako fallback; chybí-li nebo je poškozený → jen Plocha. Ukládá se v `Closed` shellu a při výměně session, pod tu, jejíž dock je.
  Přepnutí firmy i uživatele dock zavře **před** výměnou session (dashboardy v `Unloaded` ukládají přes sdílený MLRest).
- Výběr miniaplikace je okno `Windows/MiniAppPickerWindow` s **jednoúrovňovým** seznamem seřazeným
  abecedně podle titulku (česky, nezávisle na kultuře stanice); z `IMiniAppProvider.GroupTitle` zbyl jen popisek řádku.
- **Tabulka legacy dashboardu má strop na výšku panelu**: s neomezenou výškou DataGrid nevirtualizuje a „Údržba
  vozidel" (tisíce řádků) se staví při načtení i seřazení. Strop je `DynamicResource` plněný ze `ScrollChanged`, ne binding
  na `ViewportHeight` předka - ten se v šabloně vyhodnotí až po prvním měření a postavené řádky DataGrid už neuklidí.
- Picker vybírá i **umístění** (`MiniAppPlacement`). Cíle jsou **jen ukotvená** pod-okna:
  `Descendents()` vrací i ta v plovoucích oknech, filtruje se `IsHostedInFloatingWindow`.
  **Poslední document pane AvalonDock neodstraní** - po zavření všeho zbude prázdný přes celou
  plochu, takže bez obsazeného pod-okna picker volbu nenabídne a panel jde cestou `Fill`.
  Horní pás sloupců po restore nemusí existovat (zahozené panely starého layoutu ho vyprázdní
  a AvalonDock prázdný `LayoutPanel` smaže) - `InsertColumn` ho dohledá nebo založí.
