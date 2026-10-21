# EDActionInProgress - poznámky pro vývojáře

Overlay „Akce probíhá" se skládající se krychlí (logo EDISON). `EDActionInProgress` je **neviditelný controller**
(FrameworkElement) - vlož kamkoli do okna (konvence: poslední child root Gridu). Na `Loaded` se
sám naváže na `EDViewModel.IsActionInProgress` - v XAML se `IsActive` binding **nepíše**.
Aktivace z VM: `using var _ = ActionInProgress();`.

## Karta žije v owned okně (`ActionInProgressWindow`)

- **Proč**: HWND obsah (WebView2 mini-apps) WPF nikdy nepřekreslí (airspace) - karta
  v gridu okna by za widgety zmizela. Child HWND nepřekreslí cizí top-level a owned
  okno drží window manager vždy nad ownerem → karta je nad widgety **bez `Topmost`**
  (žádný únik nad cizí aplikace při Alt+Tab; WPF `Popup` by topmost byl - proto ne).
  Widgety tak při dimu zůstávají viditelné a čitelné (záměr - uživatel čeká a čte).
- Okno je **jen velikosti karty**, ne celoplošné - full-cover `AllowsTransparency`
  na 4K = ~33MB layered surface překreslovaná animací 60 fps (dim u legacy dialogu
  trvá minuty). Root Grid `Margin=24` = prostor pro DropShadow (SizeToContent ořeže).
- `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (SourceInitialized):
  klik propadá skrz (na DimAdorner / widgety), nekrade aktivaci, mimo Alt+Tab.
- **Centrování přes `PointToScreen(0,0)` + `GetClientRect` + `TransformFromDevice`** -
  maximalizovaný owner má v `Left/Top` restore bounds, `CenterOwner` je tam rozbité.
- Minimalizace ownera → **explicitní** `Hide()` + stop animací; OS auto-hide owned
  oken probíhá na HWND úrovni, WPF `IsVisible` ho nevidí a animační clocky by tikaly dál
  (držely dispatcher vzhůru). Restore → Show + Reposition + restart.
- Owner ještě nezobrazený (akce před `ShowDialog`) → controller odloží show přes
  `IsVisibleChanged` (setter `Owner` na nezobrazený owner hází).
- **Okno se vytváří per akci** (`EDActionInProgress` ho na konci akce `Close()`), ne
  recykluje přes Hide/Show - skryté okno by na příštím `Show()` přehrálo poslední složený
  frame (sprite v půli cesty z minulé akce). Čerstvé okno žádný starý frame nemá.

## Ostatní neodvoditelné

- **Krychle**: geometrie a časová osa jsou čistá logika v `CubeAssembly` (testy bez WPF), tvary staví
  code-behind jednou per okno. Smyčku pohání `DoubleAnimation` na privátní DP `CubeTime` (stop =
  `BeginAnimation(.., null)` jako u spritů, žádný render loop). Koule brand barvy jako `EDIcons.Molecule`,
  vazby `EDTheme.Border.Contrast` s průhledností podle hloubky - kontrast na kartě v light i dark.
  Vypnuté animace ve Windows (`ClientAreaAnimation`) = rovnou složená krychle.
- **Pixel-art maskoti jsou vypnutí, ne smazaní**: bez klíče sady (produkce) se skládá krychle,
  s klíčem (`ActionInProgress("Money")` → `EDViewModel.ActionSpriteKey`) běží sprity. Shell je
  pouští jen z Test funkcí; mapa agenda→sada `AgendaSprites` v shellu je, ale produkce ji nevolá.
  Sady `Sprite.<Sada>.<Kus>` (pixel-art Canvas, čelem doprava) žijí v `ActionInProgressWindow.xaml`;
  **režim sady = `Tag` prvního kusu** (`"Fall"` = pád shora na náhodných X, jinak jízda přes
  průhled s náhodnou výměnou kusu). Přidat kus = jen další `<Canvas>` se správným prefixem.
- **Animace řídí code-behind, žádný storyboard** - náhodu per kolo (výběr kusu, pozice, tempo)
  storyboard s pevnými hodnotami neumí; další kolo startuje v `Completed`, stop =
  `BeginAnimation(..., null)` vrátí transform na klidovou hodnotu (kus mimo průhled). Start/stop
  vždy podle viditelnosti okna - clock skrytého okna by tikal dál.
- **Závoj nekreslí sám** - drží `WindowDimmer.Dim(window)`; refcount zaručí jediný tint
  i při překryvu s dialogovým dimem ze shellu. U nezobrazeného okna dimmer přeskočí
  vizuál a nechá jen blokaci vstupu.
- Stín karty je na samostatné vrstvě pod kartou - `DropShadowEffect` se tak
  nepřerendrovává každý frame animace.
- `RenderOptions.EdgeMode="Aliased"` + `SnapsToDevicePixels` na pixel-artu brání
  sub-pixelovým mezerám.
