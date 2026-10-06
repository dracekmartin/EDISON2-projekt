# EDControls - poznámky pro vývojáře

Sdílená WPF knihovna pro m-line aplikace (EDISONMAIN2, TemplateDesigner): ovládací
prvky, base třída ViewModelů a infrastruktura. `.NET` strana v `dotnet/`, serverová
Caché strana v `CACHE/` (ALVA projekt `ALVA_EDControls`), hlubší dokumentace
v `docs/wire-formaty.md` (wire formáty Caché ↔ .NET).

**Breaking change veřejného API ⇒ publikovat i konzumenty.** Konzumenti v `EDISON2.sln`
(EDISONMAIN2, TemplateDesigner, EDISONMAIN2.LegacyMiniApps, EDTesting a testy) se buildí spolu,
takže odebraný člen je hned compile error - API se měnit smí; `tools/ThemeRender` stojí mimo sln
a buildí se zvlášť. Nebezpečné je samostatné vydání
`EDControls.dll` (`PublishOwnAssemblyOnly`, viz root CLAUDE.md): ta potká starý exe a shodí ho
až za běhu. **TemplateDesigner běží u zákazníků**, EDISONMAIN2 zatím ne. Hodnoty sdílené přes
samostatně nasazenou DLL proto drž jako **`static readonly`, ne `const`** - `const` se inlinuje
do konzumenta a nepřebuildovaný exe změnu nedostane (`SupportActions`, `ErrorReporter`).

**Klíče `EDTheme.*` / `EDIcons.*` / `EDStrings.*` měň aditivně.** Ty nezachytí ani plný
rebuild - `{DynamicResource}` na neznámý klíč tiše nevrátí nic. Přejmenování = ruční grep
XAML všech konzumentů.

**Z legacy `EDFORMS` se vědomě nemigruje** BDG (komerční C1FlexGrid), atomické kontrolky
(nativní WPF stačí), `BaseDocument` ani centrální katalog codebooků; `EDInputBox`
a `EDMessageBox` (legacy `EDComponents2`) nahrazuje `IPopupDisplayer`.

## Nová m-line aplikace

`EDAppearance.Initialize(app)` jednou před prvním oknem (a **až po** `EDServices.Initialize`):
napojí `FontService` + `ThemeService` a class handlery na `Window.Loaded`, které rozdají panel
Podpora, usage telemetrii a obnovu ikony v liště (`TaskbarIcon`). Zbytek povinného nastavení
(resource dictionaries, exception handler, ikona exe) je v root CLAUDE.md; navíc vlastní
`EDServices.FormIdPrefix`, ať se okna nové aplikace nemíchají se shellem.

## Vzhled: EDTheme + EDControlStyles

- `EDTheme/` - design tokeny (`EDTheme.xaml`, dark `EDThemeDark.xaml`) + `ThemeService`.
  Dark dictionary se přidává na konec MergedDictionaries a přebíjí klíče, proto dark mode
  vidí jen barvy čtené přes `DynamicResource` (pravidlo v root CLAUDE.md). Preference je cross-process
  (`%APPDATA%\m-line\common\theme.json` + FileSystemWatcher) - shell i TemplateDesigner
  se přepnou spolu. Stejný mechanismus má font (`UserFonts/`, `font.json`).
  Klíč je `EDTheme.<Skupina>.<Varianta>` **podle účelu, ne podle hodnoty**; ke každé barvě
  patří dvojice `EDTheme.X` (brush) a `EDTheme.X.Color` (`Color` pro gradient stopy).
- **Font: `font.json` je v pt, tokeny v px** - konverze (×4/3) je jen ve `FontService`,
  nikde jinde ji nepiš. Výchozí Body 14 px drží legacy standard (Segoe UI 9,75 pt);
  volitelné rodiny jsou **whitelist** ve `FontCatalog` (systémový výčet nabízel
  i symbolové fonty a uživatel si tím rozbil UI včetně cesty zpět).
- `EDControlStyles/` - implicitní styly standardních controls + keyed `EDTheme.ToolbarButton`.
  **Úpravy vzhledu standardního controlu patří sem, ne do lokálního overridu v okně** -
  lokální override často ani nefunguje (themed templaty hostují items přes panel
  s `IsItemsHost="True"`, takže `ItemsPanel` apod. WPF ignoruje) a rozbil by globální
  propagaci fontu (styly čtou font tokeny přes `DynamicResource`).
- **Sloupec ikony v submenu je `Auto` + `SharedSizeGroup`** - popup, kde nikdo nemá ikonu,
  ho smrští na nulu (jinak je celé menu odsazené naprázdno). Zaškrtnutí proto nesedí v něm,
  ale vpravo v pevném sloupci šipky submenu - zaškrtávací položka sloupec ikony nerezervuje.
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

## EDIcons

- Nová ikona: PNG do `EDIcons/png/` + **explicitní** `<Resource>` v csproj (jen PNG -
  `.xaml` SDK zahrnuje sám, ruční `<Page>` = duplikát) + `BitmapImage` v `EDIcons.xaml`
  se sémantickým klíčem (`EDIcons.MyName`, ne název souboru).
- Výjimka: `EDIcons.MlineLogo` je `DrawingImage` (vektor) s brand barvou zapečenou v geometrii -
  rastr se v běžné velikosti rozpadá a v dark modu se značka záměrně nepřebarvuje.

## Root-level infrastruktura (`dotnet/*.cs`)

- `IMLRest` / `MLRestAdapter` - abstrakce nad MLRest. `With(...)` vrací klon pro
  jednorázové requesty; **ručně kopíruje pole identity** - nové pole v MLRest doplň
  i tam (hlídá reflection test `MLRestAdapterWithTests`).
- `IRestCallRunner` / `RestCallRunner` - blokující MLRest volání mimo UI thread, vždy na **klonu**
  (proč: root CLAUDE.md, sekce MLRest); instanci snapshotuje na volajícím threadu, takže celá
  dávka jede proti téže session. ⚠️ Klon nemá odběratele `EdisonActivityFailed`/`EdisonVersionFailed`
  (visí na sdílené instanci): zánik session na klonu aplikaci neukončí, jen vyhodí výjimku (architecture.md A49).
- `LegacyOutput` - jediné místo produkčního kódu, které zná `object[]` výstup legacy endpointů (index 0 =
  ERR slot, řádky `klíč{del}…` od 1); nový legacy endpoint ho použije místo vlastní smyčky.
- `ColorValue` - jediná definice barvy na drátě (hex `#AARRGGBB`, legacy `R;G;B` EDListu; formulář jen
  hex, TD jiný tvar neumí); konvertory se liší jen fallbackem. Nový formát patří sem, ne do konvertoru.
- `EdisonLaunchArgs` - jediný kodek launch kontraktu shellu (`Format` producent, `TryParse` konzument,
  pozice `Field`); **zrcadlí `FormToRunGetter` v repu EDISON** (`c:\MLineRepos\EDISON\EDISONMAIN2API\`) -
  pořadí a počet polí musí zůstat synchronní, vzorový argv je `launch-args.fixture.txt` v obou repech
  (`EDControls.Tests/Fixtures/` a vedle `FormToRunGetter.cs`). Nové pole se **přidává na konec** (starší
  host ho ignoruje). Hodnoty se jen obalují uvozovkami, **neescapují**: `Format` z textových polí odstraní
  `"` a koncové `\`, co je nést musí, posílej zakódované (viz Base64 payload zástupce na ploše).
- `WindowDimmer` - ztmavení okna, refcount per okno; UI-thread only.
- `OwnerWindowCentering` - vycentrování na klientskou oblast ownera (volat z `Loaded`).
  WPF `CenterOwner` u maximalizovaného ownera centruje podle restore bounds → mimo střed.
- `TaskbarIcon` - po `Loaded` znovu pošle ikonu okna a dá ji i třídě okna: taskbar Win11 (od
  KB5051989) se na ikonu ptá jednou s krátkým timeoutem, blokovaný startup ho nechá na výchozí.
- `ErrorReporting/` - hlášení chyb na `podpora.m-line.cz` (přímý HTTPS POST, **ne** přes
  Caché jako usage telemetrie). Vlastní `CLAUDE.md`.

## Ostatní podsložky

- `EDComponentToolbar/` - sdílený toolbar EDInputForm/EDList (search, export, ...).
  Past: window-level **Ctrl+F** binding je **jeden na okno** (`CtrlFOknaProperty`, v shellu
  jsou toolbarů desítky) a cíl vybírá až při stisku - hledání komponenty s fokusem, jinak první
  viditelné. Dřív ho držel první načtený toolbar a zkratka mířila i do skryté záložky. Toolbar se
  v `OnUnloaded` musí odregistrovat (s posledním zmizí binding) - jinak se při re-loadu akumulují. Kreslí si **vlastní** brandový pruh
  (`Accent.Header`, texty `OnAccent.*`) - hostitel ho nevsazuje do svého Borderu, jen ho
  položí nad obsah.
- `EDFilterComboBox/` - logika oddělená v `EDFilterComboBoxLogic` (testovatelná bez
  WPF). **Jediné místo pro filtrovaný editovatelný ComboBox - vlastní nepsat**; drží pasti
  pořadí WPF eventů (viz remarks třídy) a invariant „napsaný text = filtr, klidová hodnota
  filtr NE" (šipka proto nabídne celý seznam, ne jen nastavenou položku).
  Šipky/Enter/Esc obsluhuje **override `OnPreviewKeyDown`**: `ComboBox` je značí `Handled`
  už ve fázi class handlerů, takže instance handler by se s otevřeným dropdownem nikdy
  nezavolal. Navigace jede přes `SelectedIndex` (ne přes interní zvýraznění WPF), aby pick
  zachytil `SelectionChanged`.
- `EDFilter/` - pruh filtračních polí; cíl je `IEDFilterTarget` (root namespace), ne konkrétní
  control. Vlastní `CLAUDE.md`.
- `EDSearchTextBox/`, `EDInfoBar/` - šablony v `EDControlStyles.xaml`.
- `EDToolbar/` - ikonová tlačítka z `EDViewModel.Actions`; v design-time záměrně prázdný
  (Actions až za běhu), ne bug.
- `SessionState/` - `ISessionState`, slovník v paměti procesu pro stav, který má přežít zavření
  dialogu, ale ne restart (filtr v `EDInputForm`); co restart přežít má, patří do `UserPrefs/`.
- `UserPrefs/` - `IUserPrefsService`; **default je no-op `NullUserPrefsService`** -
  dokud composition root nenastaví `CacheUserPrefsService` (po vzniku MLRest),
  preference se tiše neukládají. Služba volá **klon per request** (`IRestCallRunner`) a
  `Load` při selhání transportu **hází**, null = jen chybějící klíč: volající, který výpadek
  nerozliší od prázdna, by uživatelova data přepsal prázdnem (plocha shellu). Save/Delete
  selhání polykají a hlásí `ErrorReports.ReportBackground`. Scope je `"<App>"` pro sdílený
  stav aplikace, `"<App>.<Featura>"` když klíč škáluje s entitami; kolizím brání jen
  konvence, server blob neinterpretuje.
- `EDSupportPanel/` - vysouvací panel Podpora u pravého okraje každého okna. Vlastní `CLAUDE.md`.
- `MiniApps/` - framework dlaždicových widgetů shellu. `EDMiniAppBar` = pruh nad obsahem
  (filtry, akce, status); mezeru a zarovnání filtrů rozdává code-behind, protože kontrolka je
  sama sobě kontejnerem a `ItemContainerStyle` na `ContentPresenter` na ni nesedne. Záměrně
  není rozšířením `EDComponentToolbar` - ten věší window-level Ctrl+F a kreslí brandový pruh,
  na kterém vsazené ComboBoxy vypadají rozbitě.

## CACHE strana

`CACHE/EDInputForm.inc` - `#define` konstanty wire kontraktu (typy polí, separátory,
viditelnosti) - **musí sedět s C# stranou**. Třídy per control v `CACHE/ALVA/EDControls/`,
testy v `Tests/` (viz root CLAUDE.md, sekce Testy).
