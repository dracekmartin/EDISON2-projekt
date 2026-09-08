# EDActionInProgress - poznámky pro vývojáře

Overlay „Akce probíhá" s pixel-art maskoty ve dvou sadách: dopravní prostředky
(výchozí; kusy se během akce náhodně střídají) a peníze (finanční agendy, padají).
`EDActionInProgress` je **neviditelný controller** (FrameworkElement) - vlož kamkoli do okna
(konvence: poslední child root Gridu). Na `Loaded` se sám naváže na
`EDViewModel.IsActionInProgress` - v XAML se `IsActive` binding **nepíše**.
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

- **Sprity žijí v sadách `Sprite.<Sada>.<Kus>`** (pixel-art Canvas, čelem doprava)
  v `ActionInProgressWindow.xaml`. Výchozí sada `Vehicles` jede přes průhled a kus se
  při každém přejezdu náhodně vymění (bez okamžitého opakování); sada `Money` padá
  shora na náhodných X. **Režim sady = `Tag` prvního kusu** (`"Fall"` = pád, jinak
  jízda). Přidat kus = jen další `<Canvas>` se správným prefixem, žádná změna kódu.
  Jinou sadu vybere `ActionInProgress("Money")` na VM (`EDViewModel.ActionSpriteKey`); mapování
  agenda→sada drží shell (`EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/AgendaSprites.cs`).
- **Animace řídí code-behind, žádný storyboard** - náhodu per kolo (výběr kusu, pozice,
  tempo) storyboard s pevnými hodnotami neumí. Vzor: `DoubleAnimation` na transform +
  další kolo v `Completed` (guard `_running`); stop = `BeginAnimation(..., null)`, tím
  se transform vrátí na klidovou hodnotu (kus schovaný mimo průhled: jízda X=-60,
  pád Y=-výška). Start/stop vždy podle viditelnosti okna - clock skrytého okna by
  tikal dál. Flip `CenterX` (půlka šířky kusu) nastavuje code-behind per kus.
- **Závoj nekreslí sám** - drží `WindowDimmer.Dim(window)`; refcount zaručí jediný tint
  i při překryvu s dialogovým dimem ze shellu. U nezobrazeného okna dimmer přeskočí
  vizuál a nechá jen blokaci vstupu.
- Stín karty je na samostatné vrstvě pod kartou - `DropShadowEffect` se tak
  nepřerendrovává každý frame animace.
- `RenderOptions.EdgeMode="Aliased"` + `SnapsToDevicePixels` na pixel-artu brání
  sub-pixelovým mezerám.
