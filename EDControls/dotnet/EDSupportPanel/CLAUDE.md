# EDSupportPanel - poznámky pro vývojáře

Vysouvací panel „PODPORA" u pravého okraje okna (úchyt se šipkou → karta se čtyřmi
tlačítky: Zpětná vazba, Podpora M-line, Nápověda, Novinky). Port legacy `ALVA.EDFORMS.SideMenu` + `BaseForm.AttachSideMenu`
(`c:\MLineRepos\EDISON\EDFORMS\`), **vzhled obou drž stejný** (barvy, ikony, pořadí, popisky).

## Připojení: class handler na `Window.Loaded`, ne ViewModel

Registruje ho `EDAppearance.Initialize` (`EventManager.RegisterClassHandler(typeof(Window), …)`,
stejný vzor jako `ThemeService`) → panel dostane **každé okno včetně odděděných tříd**, bez
zásahu do jeho XAML a bez per-app drátování.

**Na `EDViewModel.AttachToWindow` to viset nemůže**: TemplateDesigner ho volá jen pro hlavní
okno (dialogy otevírá přímo, ne přes `WindowNavigator`) - panel by tam pak chyběl. Ze
stejného důvodu visí na témže class handleru i usage telemetrie (`EDUsageTelemetryHook`).

Přeskočí se (`ShouldAttach`): `IOverlayWindow`, **okna frameworku** (`FrameworkWindows` - chrome
AvalonDocku, viz [EDViewModel/CLAUDE.md](../EDViewModel/CLAUDE.md)), `EDSupportPanel.Suppress="True"`
(opt-out pro okna bez VM - nastaveno na `EDPopup`/`EDErrorPopup`, ten je dost velký, aby ho guard
velikosti propustil), `EDViewModel.ShowSupportPanel = false`, a okno, které panel už má.

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
- **Panel je `IOverlayWindow`** (proč: [EDViewModel/CLAUDE.md](../EDViewModel/CLAUDE.md)) - zavření
  přes `CloseAll()` by bylo **nevratné**, attach je jednorázový.
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
- Barvy: tokeny `EDTheme.Support.<Akce>.Bg/Border/Text/Icon` = paleta widgetu podpora.m-line.cz edison2,
  ikony `EDIcons.Support*` = jeho SVG kresby. Styl `EDTheme.SupportPanelButton` (`EDControlStyles.xaml`) dělá
  hover nádechem barvy textu. **Popisek má `Foreground` vázaný na tlačítko** - implicitní styl `TextBlock`
  by mu jinak vnutil `Text.Primary`. Popisky se zalamují a styl má `MinHeight` (pevná šířka karty, větší font).
- **Nápověda a Novinky neznají agendu**: otevřou `help/open` widget API s `agenda` = assembly okna (jako zpětná
  vazba) a MLWeb přesměruje do sekce Články / Novinky agendy podle **Kódů modulů v číselníku agend**. Okno bez
  shody vede na úvod Nápovědy - chybějící kód se opravuje v MLWebu, ne tady.
- `v=` ve feedbacku (i „Informace o verzi" v menu Nápověda shellu) bere jméno a verzi z `AppIdentity`
  jako hlášení chyb: `EDServices.AppDisplayName` / `AppVersion` (shell z `VERZE.INF`), nenastaveno → entry
  assembly (`1.0.0+<hash>`, TD), nikdy verze assembly okna (všude 1.0.0.0).
- `context` ve feedback URL je tvrdě zkrácený na 255 znaků - limit widget API.
- Base + klíč widget API drží `SupportActions` (sdílí je `WidgetUrls` shellu) jako
  **`static readonly`, ne `const`** - pravidlo samostatně nasazené DLL, viz `EDControls/CLAUDE.md`.
