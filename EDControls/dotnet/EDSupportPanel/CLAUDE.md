# EDSupportPanel — poznámky pro vývojáře

Vysouvací panel „PODPORA" u pravého okraje okna (úchyt se šipkou → karta se třemi
tlačítky). Port legacy `ALVA.EDFORMS.SideMenu` + `BaseForm.AttachSideMenu`
(`c:\MLineRepos\EDISON\EDFORMS\`). Připojuje se sám z `EDViewModel.AttachToWindow`, takže
ho má **každé okno s ViewModelem** bez zásahu do svého XAML; opt-out
`EDViewModel.ShowSupportPanel = false`. Okna bez VM (LoginWindow) a mini-apps
(`EDMiniAppViewModel` záměrně `AttachToWindow` nevolá) ho nemají.

## Karta žije v owned okně (`SupportPanelWindow`)

- **Proč**: pravý okraj shellu je plocha miniaplikací s WebView2 (HWND) — WPF prvek
  v gridu okna by za widgety zmizel (airspace). Owned okno drží window manager nad
  ownerem **bez `Topmost`** (žádný únik nad cizí aplikace při Alt+Tab; WPF `Popup` by
  topmost byl). Detailní rozbor pastí: [EDActionInProgress/CLAUDE.md](../EDActionInProgress/CLAUDE.md).
- `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (SourceInitialized): klik na tlačítko nesebere
  aktivaci ownerovi (myší zprávy okno dostává dál, klávesnici panel nepotřebuje) a panel
  je mimo Alt+Tab. **`WS_EX_TRANSPARENT` tady nejde** (na rozdíl od EDActionInProgress) —
  klik musí dopadnout.
- **Slide mění `Left` + `Width` okna**, obsah se neposouvá: úchyt i karta jsou zarovnané
  vlevo a okno roste od pravé hrany doleva, takže v zavřeném stavu prořízne hranice okna
  vše kromě úchytu. Posouvat obsah je past — s translací obsahu doleva zbyde v zavřeném
  stavu vidět sliver karty, ne úchyt. Pravá hrana (`Left + Width`) proto musí zůstat
  přilepená ke klientské oblasti v každé fázi animace (hlídá to test).
- **Pozice přes `PointToScreen(0,0)` + `GetClientRect` + `TransformFromDevice`** —
  maximalizovaný owner má v `Left/Top` restore bounds.
- Minimalizace ownera → **explicitní** `Hide()`; OS auto-hide owned oken je na HWND úrovni
  a WPF `IsVisible` ho nevidí, `Reposition` by pak počítal s neplatnými souřadnicemi.
- Owner ještě nezobrazený (`AttachToWindow` běží **před** `Show()`) → `EDSupportPanel`
  odloží vytvoření na `IsVisibleChanged`; setter `Owner` na nezobrazený owner hází.
- **Panel je `IOverlayWindow`** — bez toho by ho `WindowNavigator` bral jako okno modulu:
  ukázal by ho v menu „Spuštěné moduly" (bez titulku, jako `SupportPanelWindow`) a
  `CloseAll()` by ho zavřel **nevratně** (attach běží jen jednou per okno).
- **Do klientské oblasti ownera se musí vejít celá karta** (`SupportPanelGeometry.Fits`),
  jinak se panel nepřipojí. Karta je samostatné okno, takže u malého dialogu nepřečnívá
  jen opticky — kreslila by se mimo něj, přes footer s tlačítky. `Compute` navíc drží
  spodní hranu v klientské oblasti i při pozdějším zmenšení okna.
- Na ownera (`Owner`, eventy) se panel váže až v `TryShowPanel` po testu velikosti —
  `Owner` zapisuje do `owner.OwnedWindows`, nezobrazený panel by tam zůstal viset.
- `Root` má **pevnou `Width`** — odpojuje měření obsahu od měnící se šířky okna, jinak by
  `SizeToContent="Height"` počítal výšku pro 12px široký obsah.

## Ostatní neodvoditelné

- Úchyt je 2px širší než `TabWidth` a leží nad kartou — přesah zakryje šev s jejím levým
  rámečkem (mirror legacy `TabFillOverlap`).
- Barvy: tokeny `EDTheme.Support.*` (hodnoty z legacy SideMenu; accent úchytu je jiná
  zelená než tlačítko Portal — tak to má legacy). Hover/pressed tlačítek jsou průhledné
  overlaye `EDTheme.OnAccent.*`, ne dopočítaný světlejší odstín; styl
  `EDTheme.SupportPanelButton` je v `EDControlStyles.xaml`.
- **Vzhled bere všechno přes `DynamicResource`, i `Style` tlačítek a hover úchytu** —
  panel se vytváří z EDControls do libovolného hosta a `StaticResource`/`FindResource`
  na hosta, který `EDTheme.xaml` nebo `EDControlStyles.xaml` nenamergoval, hází výjimku
  (u hoveru dokonce při každém přejezdu myší). S `DynamicResource` chybějící klíč jen
  nic neobarví.
- „Informace o verzi" čte `EDServices.AppDisplayName` / `AppVersion` — `EDControls`
  nedosáhne na `VERZE.INF` (čte ho shell), nenastaveno → verze assembly. Týž text
  ukazuje i menu Nápověda shellu (`SupportActions.BuildVersionInfo`).
- Feedback URL nese `idfa`, `user`, `agenda` (= assembly ownera) a `context`
  (`form=|title=|ns=|v=`, tvrdě na 255 znaků — limit widget API). Prázdné parametry se
  vynechávají.
- Base + klíč widget API drží `SupportActions` a sdílí je `WidgetUrls` shellu — jako
  **`static readonly`, ne `const`**: `const` se inlinuje do konzumenta, takže samostatně
  nasazená `EDControls.dll` by shellu klíč nezměnila.
- Popisky tlačítek jsou zalamovací `TextBlock` a styl má `MinHeight` (ne `Height`) —
  karta má pevnou šířku, takže při user škálování fontu se text musí zalomit, ne ořezat.
