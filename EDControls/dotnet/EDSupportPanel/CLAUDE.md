# EDSupportPanel - poznámky pro vývojáře

Vysouvací panel „PODPORA" u pravého okraje okna (úchyt se šipkou → karta se třemi
tlačítky). Port legacy `ALVA.EDFORMS.SideMenu` + `BaseForm.AttachSideMenu`
(`c:\MLineRepos\EDISON\EDFORMS\`).

## Připojení: class handler na `Window.Loaded`, ne ViewModel

Registruje ho `EDAppearance.Initialize` (`EventManager.RegisterClassHandler(typeof(Window), …)`,
stejný vzor jako `ThemeService`) → panel dostane **každé okno včetně odděděných tříd**, bez
zásahu do jeho XAML a bez per-app drátování.

**Na `EDViewModel.AttachToWindow` to viset nemůže**: TemplateDesigner ho nevolá vůbec (hlavní
okno i dialogy otevírá přímo, ne přes `WindowNavigator`) - panel tam pak nebyl nikde. Ze
stejného důvodu visí na témže class handleru i usage telemetrie (`EDUsageTelemetryHook`).

Přeskočí se (`ShouldAttach`): `IOverlayWindow`, **okna frameworku** (`FrameworkWindows` - chrome
AvalonDocku, viz [EDViewModel/CLAUDE.md](../EDViewModel/CLAUDE.md)), `EDSupportPanel.Suppress="True"`
(opt-out pro okna bez VM - nastaveno na `EDPopup`/`EDErrorPopup`, ten je 640×440 a guard velikosti
by ho propustil), `EDViewModel.ShowSupportPanel = false`, a okno, které panel už má.

## Karta žije v owned okně (`SupportPanelWindow`)

- **Proč owned okno**: karta se kreslí nad WebView2 miniaplikacemi u pravého okraje shellu,
  které WPF obsah kvůli airspace nepřekreslí. Pasti tohohle vzoru rozebírá
  [EDActionInProgress/CLAUDE.md](../EDActionInProgress/CLAUDE.md) - platí i tady, včetně
  „žádný `Topmost`" a explicitního `Hide()` při minimalizaci ownera.
- `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`: klik nesebere aktivaci ownerovi a panel je mimo
  Alt+Tab. **`WS_EX_TRANSPARENT` tady nejde** (na rozdíl od EDActionInProgress) - klik musí
  dopadnout.
- **Slide mění `Left` + `Width` okna**, obsah se neposouvá: úchyt i karta jsou zarovnané vlevo
  a okno roste od pravé hrany doleva. Translace obsahu je past - pak je v zavřeném stavu vidět
  sliver karty místo úchytu. Pravá hrana (`Left + Width`) musí zůstat u klientské oblasti
  v každé fázi animace (hlídá test).
- **Pozice přes `PointToScreen(0,0)` + `GetClientRect` + `TransformFromDevice`** -
  maximalizovaný owner má v `Left/Top` restore bounds.
- **Panel je `IOverlayWindow`** - jinak by ho `WindowNavigator` bral jako okno modulu: nabídl
  by ho v „Spuštěné moduly" a `CloseAll()` by ho zavřel **nevratně** (attach je jednorázový).
- **Do klientské oblasti ownera se musí vejít celá karta** (`SupportPanelGeometry.Fits`), jinak
  se panel nepřipojí - samostatné okno by u malého dialogu kreslilo kartu mimo něj, přes footer
  s tlačítky. `Compute` drží spodní hranu v klientské oblasti i po zmenšení okna.
- Na ownera (`Owner`, eventy) se panel váže až v `TryShowPanel` po testu velikosti - `Owner`
  zapisuje do `owner.OwnedWindows`, nezobrazený panel by tam zůstal viset. Odložení na
  `IsVisibleChanged` je pojistka pro přímé `Attach` před `Show()`.

## Ostatní neodvoditelné

- **Vzhled bere všechno přes `DynamicResource`** (i `Style` tlačítek a hover úchytu) - panel se
  vytváří do libovolného hosta a `StaticResource`/`FindResource` na hosta bez `EDTheme.xaml`
  hází výjimku, u hoveru při každém přejezdu myší.
- Barvy: tokeny `EDTheme.Support.*` (hodnoty z legacy SideMenu; accent úchytu je jiná zelená než
  tlačítko Portal - tak to má legacy), hover/pressed přes `EDTheme.OnAccent.*`, styl tlačítka
  `EDTheme.SupportPanelButton` v `EDControlStyles.xaml`. Popisky jsou zalamovací `TextBlock`
  a styl má `MinHeight` - karta má pevnou šířku, při větším fontu se text zalomí, neořeže.
- „Informace o verzi" čte `EDServices.AppDisplayName` / `AppVersion` - `EDControls` nedosáhne na
  `VERZE.INF` (čte ho shell), nenastaveno → verze assembly. Týž text ukazuje menu Nápověda shellu.
- `context` ve feedback URL je tvrdě zkrácený na 255 znaků - limit widget API.
- Base + klíč widget API drží `SupportActions` a sdílí je `WidgetUrls` shellu jako
  **`static readonly`, ne `const`**: `const` se inlinuje do konzumenta, takže samostatně
  nasazená `EDControls.dll` by shellu klíč nezměnila.
