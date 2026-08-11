# EDActionInProgress - poznámky pro vývojáře

Overlay „Akce probíhá" s pixel-art maskotem (kapybara / autobus, náhodně per akce).
`EDActionInProgress` je **neviditelný
controller** (FrameworkElement) - vlož kamkoli do okna (konvence: poslední child root
Gridu). Na `Loaded` se sám naváže na `EDViewModel.IsActionInProgress` - v XAML se
`IsActive` binding **nepíše**. Aktivace z VM: `using var _ = ActionInProgress();`.

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
- Minimalizace ownera → **explicitní** `Hide()` + stop storyboardu; OS auto-hide owned
  oken probíhá na HWND úrovni, WPF `IsVisible` ho nevidí a Forever animace by tikala dál
  (držela dispatcher vzhůru). Restore → Show + Reposition + restart.
- Owner ještě nezobrazený (akce před `ShowDialog`) → controller odloží show přes
  `IsVisibleChanged` (setter `Owner` na nezobrazený owner hází).
- **Okno se vytváří per akci** (`EDActionInProgress` ho na konci akce `Close()`), ne
  recykluje přes Hide/Show - skryté okno by na příštím `Show()` přehrálo poslední složený
  frame (sprite v půli cesty z minulé akce). Čerstvé okno žádný starý frame nemá.

## Ostatní neodvoditelné

- **Sprity jsou modulární**: každý = keyed resource `Sprite.*` (Canvas **60×48**, čelem
  doprava) v `ActionInProgressWindow.xaml`. Code-behind si klíče podle prefixu najde sám
  a náhodně jeden vybere per akci → **přidat maskota = jen nový `<Canvas x:Key="Sprite.Xxx">`**,
  žádná změna kódu. Rozměr 60×48 je povinný (flip `CenterX=30`, slide `From=-60`). Animace
  cílí transform hosta (`SpriteHost`), ne sprite → je společná všem. `x:Shared="False"` -
  `FindResource` vrací novou instanci (UIElement smí mít jen jednoho rodiče).
- **Storyboard startuje/zastavuje code-behind okna**, ne `Loaded` trigger -
  `RepeatBehavior=Forever` by jinak tikal i skrytý. `Begin(..., isControllable: true)`
  při aktivaci resetuje maskota na start.
- **Závoj nekreslí sám** - drží `WindowDimmer.Dim(window)`; refcount zaručí jediný tint
  i při překryvu s dialogovým dimem ze shellu. U nezobrazeného okna dimmer přeskočí
  vizuál a nechá jen blokaci vstupu.
- Stín karty je na samostatné vrstvě pod kartou - `DropShadowEffect` se tak
  nepřerendrovává každý frame animace.
- `RenderOptions.EdgeMode="Aliased"` + `SnapsToDevicePixels` na pixel-artu brání
  sub-pixelovým mezerám. Geometrii animace čti přímo z XAML.
