# EDControls — poznámky pro vývojáře

Sdílená WPF knihovna pro m-line aplikace (EDISONMAIN2, TemplateDesigner): ovládací
prvky, base třída ViewModelů a infrastruktura. `.NET` strana v `dotnet/`, serverová
Caché strana v `CACHE/` (ALVA projekt `ALVA_EDControls`), hlubší dokumentace
v `DOCS/` (wire formáty → `DOCS/01-wire-formats.md`).

Komponenty s vlastním CLAUDE.md: `EDViewModel` (+ EDServices, WindowNavigator,
RelayCommand), `EDInputForm`, `EDList`, `EDFooter`, `EDToolbar`, `EDActionInProgress`.

## Nová m-line aplikace

`EDAppearance.Initialize(app)` jednou před prvním oknem — napojí `FontService`
+ `ThemeService`. Dále merge `EDIcons.xaml` + `EDStrings.xaml` a
`DispatcherUnhandledException` handler (viz root CLAUDE.md).

## Vzhled: EDTheme + EDControlStyles

- `EDTheme/` — design tokeny (`EDTheme.xaml`, dark `EDThemeDark.xaml`) + `ThemeService`.
  **Dark mode funguje jen pro barvy čtené přes `DynamicResource`** — dark dictionary se
  přidává na konec MergedDictionaries a přebíjí klíče. Preference je cross-process
  (`%APPDATA%\m-line\common\theme.json` + FileSystemWatcher) — shell i TemplateDesigner
  se přepnou spolu. Stejný mechanismus má font (`UserFonts/`, `font.json`).
- `EDControlStyles/` — implicitní styly standardních controls (Button, Menu, TabControl,
  TreeView, ScrollBar, ...). **Úpravy vzhledu standardního controlu patří sem, ne do
  lokálního overridu v okně** — lokální override často ani nefunguje (themed templaty
  hostují items přes panel s `IsItemsHost="True"`, takže `ItemsPanel` apod. WPF
  ignoruje) a rozbil by globální propagaci fontu (styly čtou font tokeny
  přes `DynamicResource`).

## EDIcons / EDStrings

- Nová ikona: PNG do `EDIcons/png/` + **explicitní** `<Resource>` v csproj (jen PNG —
  `.xaml` SDK zahrnuje sám, ruční `<Page>` = duplikát) + `BitmapImage` v `EDIcons.xaml`
  se sémantickým klíčem (`EDIcons.MyName`, ne název souboru).
- Nový text: `<sys:String x:Key="EDStrings.X">` v `EDStrings.xaml`, bez změny csproj.

## Root-level infrastruktura (`dotnet/*.cs`)

- `IMLRest` / `MLRestAdapter` — abstrakce nad MLRest. `With(...)` vrací klon pro
  jednorázové requesty; **ručně kopíruje pole identity** — při přidání pole do MLRest
  nutno doplnit i tam, jinak se v klonech ztratí.
- `MLRestJson` — JSON-envelope volání přes sdílený klíč `"payload"` (kontrakt na obou
  stranách); `CallAsync` = `Task.Run` nad blokujícím MLRest.
- `EdisonLaunchArgs` — parser launch kontraktu shellu; **zrcadlí
  `FormToRunGetter` v repu EDISON** (`c:\MLineRepos\EDISON\EDISONMAIN2API\`) — pořadí
  a počet polí musí zůstat synchronní.
- `WindowDimmer` — jednotné ztmavení okna, refcount per okno; UI-thread only.
- `WindowStatePersistence` — persistence stavu oken (detaily v EDViewModel doc).
- `ListItem` — sdílený `record (Key, Label)` pro všechny controly s volbami.

## Ostatní podsložky

- `EDComponentToolbar/` — sdílený toolbar EDInputForm/EDList (search, export, ...).
  Past: registruje window-level **Ctrl+F** binding v `OnLoaded` a musí ho odregistrovat
  v `OnUnloaded` — jinak se při re-loadu akumulují kopie.
- `EDFilterComboBox/` — combo s našeptáváním; logika oddělená v
  `EDFilterComboBoxLogic` (testovatelná bez WPF). `EDFilter/` — skládání filtrů.
- `UserPrefs/` — `IUserPrefsService`; **default je no-op `NullUserPrefsService`** —
  dokud composition root nenastaví `CacheUserPrefsService` (po vzniku MLRest),
  preference se tiše neukládají.
- `MiniApps/` — framework dlaždicových widgetů shellu.

## CACHE strana

`CACHE/EDInputForm.inc` — `#define` konstanty wire kontraktu (typy polí, separátory,
viditelnosti) — **musí sedět s C# stranou**. Třídy per control v `CACHE/ALVA/EDControls/`
(`EDInputForm/`, `EDList/`, `UserData.cls`) + testy v `Tests/` (viz root CLAUDE.md,
sekce Testy).
