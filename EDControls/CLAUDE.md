# EDControls - poznámky pro vývojáře

Sdílená WPF knihovna pro m-line aplikace (EDISONMAIN2, TemplateDesigner): ovládací
prvky, base třída ViewModelů a infrastruktura. `.NET` strana v `dotnet/`, serverová
Caché strana v `CACHE/` (ALVA projekt `ALVA_EDControls`), hlubší dokumentace
v `DOCS/` (wire formáty → `DOCS/01-wire-formats.md`).

**Breaking change veřejného API ⇒ publikovat i konzumenty.** Uvnitř repa se všichni
(EDISONMAIN2, TemplateDesigner, LegacyMiniApps, EDTesting, ThemeRender) buildí spolu, takže
odebraný člen je hned compile error - API se měnit smí. Nebezpečné je samostatné vydání
`EDControls.dll` (`PublishOwnAssemblyOnly`, viz root CLAUDE.md): ta potká starý exe a shodí ho
až za běhu. **TemplateDesigner běží u zákazníků**, EDISONMAIN2 zatím ne. Hodnoty sdílené přes
samostatně nasazenou DLL proto drž jako **`static readonly`, ne `const`** - `const` se inlinuje
do konzumenta a nepřebuildovaný exe změnu nedostane (`SupportActions`, `ErrorReporter`).

**Klíče `EDTheme.*` / `EDIcons.*` / `EDStrings.*` měň aditivně.** Ty nezachytí ani plný
rebuild - `{DynamicResource}` na neznámý klíč tiše nevrátí nic. Přejmenování = ruční grep
XAML všech konzumentů.

## Nová m-line aplikace

- `EDAppearance.Initialize(app)` jednou před prvním oknem (a **až po** `EDServices.Initialize`):
  napojí `FontService` + `ThemeService` a class handlery na `Window.Loaded`, které rozdají
  panel Podpora a usage telemetrii.
- Zbytek dělá konzument sám: merge `EDIcons.xaml` + `EDStrings.xaml` v App.xaml,
  `DispatcherUnhandledException` handler (viz root CLAUDE.md) a `EDServices.FormIdPrefix`,
  ať se okna nové aplikace nemíchají se shellem.

## Vzhled: EDTheme + EDControlStyles

- `EDTheme/` - design tokeny (`EDTheme.xaml`, dark `EDThemeDark.xaml`) + `ThemeService`.
  **Dark mode funguje jen pro barvy čtené přes `DynamicResource`** - dark dictionary se
  přidává na konec MergedDictionaries a přebíjí klíče. Preference je cross-process
  (`%APPDATA%\m-line\common\theme.json` + FileSystemWatcher) - shell i TemplateDesigner
  se přepnou spolu. Stejný mechanismus má font (`UserFonts/`, `font.json`).
- **Font: `font.json` je v pt, tokeny v px** - konverze (×4/3) je jen ve `FontService`,
  nikde jinde ji nepiš. Výchozí Body 14 px drží legacy standard (Segoe UI 9,75 pt);
  volitelné rodiny jsou **whitelist** ve `FontCatalog` (systémový výčet nabízel
  i symbolové fonty a uživatel si tím rozbil UI včetně cesty zpět).
- `EDControlStyles/` - implicitní styly standardních controls (Button, Menu, TabControl,
  TreeView, ScrollBar, ...) + keyed `EDTheme.ToolbarButton` (ploché ikonové tlačítko
  toolbaru, opt-in, hover/pressed z theme tokenů). **Úpravy vzhledu standardního controlu patří sem, ne do
  lokálního overridu v okně** - lokální override často ani nefunguje (themed templaty
  hostují items přes panel s `IsItemsHost="True"`, takže `ItemsPanel` apod. WPF
  ignoruje) a rozbil by globální propagaci fontu (styly čtou font tokeny
  přes `DynamicResource`).
- **Klávesový focus nese `FocusVisualStyle` (`EDTheme.FocusVisual`)**, ne triggery - WPF ho kreslí
  jen po Tab/Alt (`IsKeyboardFocused` svítí i po kliknutí myší). **Každý styl si ho musí nastavit
  sám**: theme style Aera přebíjí globální override `SystemParameters.FocusVisualStyleKey`
  (u `TextBox` na null), takže bez setteru zůstane systémový tečkovaný rámeček, v dark neviditelný.
  Items ve scrollerech `EDTheme.FocusVisual.Inset` (vnější ring přetéká hranu ScrollVieweru);
  z jiné dictionary `DynamicResource` (odložený `StaticResource` = `UnsetValue`); v popupech
  adorner nefunguje, cue nesou `IsHighlighted`/`IsSelected`.
- **Lokální `Style`/`ItemContainerStyle` na themovaném controlu musí mít
  `BasedOn="{StaticResource {x:Type Xxx}}"`** - theming (font, dark mode, template dle
  `Role`) žije v implicitním stylu a explicitní styl ho *nedědí, nahrazuje celý*.
  Bez `BasedOn` = systémový default template → v dark modu bílé pozadí/text. Past nejvíc
  bije u submenu/`ContextMenu`/`ToolTip` popupů (vlastní visual tree).
- **`DataGrid.RowStyle` s `Foreground` nefunguje** - implicitní `DataGridCell` styl si
  `Foreground` nastavuje setterem a ten přebíjí hodnotu děděnou z řádku (ve stock WPF setter
  není, proto to legacy vzor dělal přes řádek). Barvu řádku dej na `CellStyle` (s `BasedOn`) -
  ale pozor, legacy barvy jsou navržené na bílé pozadí a v dark modu jsou nečitelné.

## EDIcons / EDStrings

- Nová ikona: PNG do `EDIcons/png/` + **explicitní** `<Resource>` v csproj (jen PNG -
  `.xaml` SDK zahrnuje sám, ruční `<Page>` = duplikát) + `BitmapImage` v `EDIcons.xaml`
  se sémantickým klíčem (`EDIcons.MyName`, ne název souboru).
- Nový text: `<sys:String x:Key="EDStrings.X">` v `EDStrings.xaml`, bez změny csproj.
- Výjimka: `EDIcons.MlineLogo` je `DrawingImage` (vektor) s brand barvou zapečenou v geometrii -
  rastr se v běžné velikosti rozpadá a v dark modu se značka záměrně nepřebarvuje.

## Root-level infrastruktura (`dotnet/*.cs`)

- `IMLRest` / `MLRestAdapter` - abstrakce nad MLRest. `With(...)` vrací klon pro
  jednorázové requesty; **ručně kopíruje pole identity** - nové pole v MLRest doplň
  i tam (hlídá reflection test `MLRestAdapterWithTests`).
- `IRestCallRunner` / `RestCallRunner` - blokující MLRest volání mimo UI thread, vždy na **klonu**
  (proč: root CLAUDE.md, sekce MLRest); instanci snapshotuje na volajícím threadu, takže celá
  dávka jede proti téže session.
- `MLRestJson` - JSON-envelope volání přes sdílený klíč `"payload"` (kontrakt na obou
  stranách); `CallAsync` = `Task.Run` nad blokujícím MLRest.
- `EdisonLaunchArgs` - parser launch kontraktu shellu; **zrcadlí
  `FormToRunGetter` v repu EDISON** (`c:\MLineRepos\EDISON\EDISONMAIN2API\`) - pořadí
  a počet polí musí zůstat synchronní. Producent je `WindowNavigator.BuildLauncherArguments`;
  nové pole se **přidává na konec** (starší host ho ignoruje) a hodnoty se jen obalují
  uvozovkami, **neescapují** - co může obsahovat mezeru či uvozovku, posílej zakódované
  (viz Base64 payload zástupce na ploše).
- `WindowDimmer` - ztmavení okna, refcount per okno; UI-thread only.
- `OwnerWindowCentering` - vycentrování na klientskou oblast ownera (volat z `Loaded`).
  WPF `CenterOwner` u maximalizovaného ownera centruje podle restore bounds → mimo střed.
- `ErrorReporting/` - hlášení chyb na `podpora.m-line.cz` (přímý HTTPS POST, **ne** přes
  Caché jako usage telemetrie). Vlastní `CLAUDE.md`.

## Ostatní podsložky

- `EDComponentToolbar/` - sdílený toolbar EDInputForm/EDList (search, export, ...).
  Past: registruje window-level **Ctrl+F** binding v `OnLoaded` a musí ho odregistrovat
  v `OnUnloaded` - jinak se při re-loadu akumulují kopie. Kreslí si **vlastní** brandový pruh
  (`Accent.Header`, texty `OnAccent.*`) - hostitel ho nevsazuje do svého Borderu, jen ho
  položí nad obsah.
- `EDFilterComboBox/` - logika oddělená v `EDFilterComboBoxLogic` (testovatelná bez
  WPF). **Jediné místo pro filtrovaný editovatelný ComboBox - vlastní nepsat**; drží pasti
  pořadí WPF eventů (viz remarks třídy) a invariant „napsaný text = filtr, klidová hodnota
  filtr NE" (šipka proto nabídne celý seznam, ne jen nastavenou položku).
  Šipky/Enter/Esc obsluhuje **override `OnPreviewKeyDown`**: `ComboBox` je značí `Handled`
  už ve fázi class handlerů, takže instance handler by se s otevřeným dropdownem nikdy
  nezavolal. Navigace jede přes `SelectedIndex` (ne přes interní zvýraznění WPF), aby pick
  zachytil `SelectionChanged`. `EDFilter/` - skládání filtrů.
- `EDSearchTextBox/` - `TextBox` pro hledání (placeholder, ✕, Esc); styl v `EDControlStyles.xaml`.
- `EDInfoBar/` - info proužek (text + volitelná ikona + akční odkaz); prázdné → skryté,
  styl v `EDControlStyles.xaml`, viditelnost řídí konzument.
- `EDToolbar/` - ikonová tlačítka z `EDViewModel.Actions`; velikost ikon z tokenu
  `EDTheme.IconSize.Default`; v design-time záměrně prázdný (Actions až za běhu), ne bug.
- `UserPrefs/` - `IUserPrefsService`; **default je no-op `NullUserPrefsService`** -
  dokud composition root nenastaví `CacheUserPrefsService` (po vzniku MLRest),
  preference se tiše neukládají.
- `EDSupportPanel/` - vysouvací panel Podpora u pravého okraje okna; připojuje se sám ke
  **každému** oknu (class handler z `EDAppearance.Initialize`), karta žije v owned okně
  (airspace). Vlastní `CLAUDE.md`.
- `MiniApps/` - framework dlaždicových widgetů shellu. `EDMiniAppBar` = pruh nad obsahem
  (filtry, akce, status); mezeru a zarovnání filtrů rozdává code-behind, protože kontrolka je
  sama sobě kontejnerem a `ItemContainerStyle` na `ContentPresenter` na ni nesedne. Záměrně
  není rozšířením `EDComponentToolbar` - ten věší window-level Ctrl+F (N panelů = N bindingů)
  a kreslí brandový pruh, na kterém vsazené ComboBoxy vypadají rozbitě.

## CACHE strana

`CACHE/EDInputForm.inc` - `#define` konstanty wire kontraktu (typy polí, separátory,
viditelnosti) - **musí sedět s C# stranou**. Třídy per control v `CACHE/ALVA/EDControls/`
(`EDInputForm/`, `EDList/`, `UserData.cls`, `DesktopShortcuts.cls`) + testy v `Tests/` (viz root CLAUDE.md,
sekce Testy).
