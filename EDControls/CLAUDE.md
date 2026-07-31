# EDControls — poznámky pro vývojáře

Sdílená WPF knihovna pro m-line aplikace (EDISONMAIN2, TemplateDesigner): ovládací
prvky, base třída ViewModelů a infrastruktura. `.NET` strana v `dotnet/`, serverová
Caché strana v `CACHE/` (ALVA projekt `ALVA_EDControls`), hlubší dokumentace
v `DOCS/` (wire formáty → `DOCS/01-wire-formats.md`).

**Veřejné API a klíče theme resource měň aditivně.** `EDControls.dll` se nasazuje jako
jedna volná DLL vedle obou exe (viz root CLAUDE.md), takže ji načte i exe, který se
nepřebuildil — odebraný/přejmenovaný public člen nebo resource key by shodil až za běhu,
build to nezachytí. Skutečný breaking change ⇒ publikovat všechny konzumenty.

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
- **Font: `font.json` je v pt, tokeny v px** — konverze (×4/3) je jen ve `FontService`,
  nikde jinde ji nepiš. Výchozí Body 14 px drží legacy standard (Segoe UI 9,75 pt);
  volitelné rodiny jsou **whitelist** ve `FontCatalog` (systémový výčet nabízel
  i symbolové fonty a uživatel si tím rozbil UI včetně cesty zpět).
- `EDControlStyles/` — implicitní styly standardních controls (Button, Menu, TabControl,
  TreeView, ScrollBar, ...) + keyed `EDTheme.ToolbarButton` (ploché ikonové tlačítko
  toolbaru, opt-in, hover/pressed z theme tokenů). **Úpravy vzhledu standardního controlu patří sem, ne do
  lokálního overridu v okně** — lokální override často ani nefunguje (themed templaty
  hostují items přes panel s `IsItemsHost="True"`, takže `ItemsPanel` apod. WPF
  ignoruje) a rozbil by globální propagaci fontu (styly čtou font tokeny
  přes `DynamicResource`).
- **Lokální `Style`/`ItemContainerStyle` na themovaném controlu musí mít
  `BasedOn="{StaticResource {x:Type Xxx}}"`** — theming (font, dark mode, template dle
  `Role`) žije v implicitním stylu a explicitní styl ho *nedědí, nahrazuje celý*.
  Bez `BasedOn` = systémový default template → v dark modu bílé pozadí/text. Past nejvíc
  bije u submenu/`ContextMenu`/`ToolTip` popupů (vlastní visual tree).

## EDIcons / EDStrings

- Nová ikona: PNG do `EDIcons/png/` + **explicitní** `<Resource>` v csproj (jen PNG —
  `.xaml` SDK zahrnuje sám, ruční `<Page>` = duplikát) + `BitmapImage` v `EDIcons.xaml`
  se sémantickým klíčem (`EDIcons.MyName`, ne název souboru).
- Nový text: `<sys:String x:Key="EDStrings.X">` v `EDStrings.xaml`, bez změny csproj.

## Root-level infrastruktura (`dotnet/*.cs`)

- `IMLRest` / `MLRestAdapter` — abstrakce nad MLRest. `With(...)` vrací klon pro
  jednorázové requesty; **ručně kopíruje pole identity** — nové pole v MLRest doplň
  i tam (hlídá reflection test `MLRestAdapterWithTests`).
- `MLRestJson` — JSON-envelope volání přes sdílený klíč `"payload"` (kontrakt na obou
  stranách); `CallAsync` = `Task.Run` nad blokujícím MLRest.
- `EdisonLaunchArgs` — parser launch kontraktu shellu; **zrcadlí
  `FormToRunGetter` v repu EDISON** (`c:\MLineRepos\EDISON\EDISONMAIN2API\`) — pořadí
  a počet polí musí zůstat synchronní.
- `WindowDimmer` — ztmavení okna, refcount per okno; UI-thread only.

## Ostatní podsložky

- `EDComponentToolbar/` — sdílený toolbar EDInputForm/EDList (search, export, ...).
  Past: registruje window-level **Ctrl+F** binding v `OnLoaded` a musí ho odregistrovat
  v `OnUnloaded` — jinak se při re-loadu akumulují kopie.
- `EDFilterComboBox/` — logika oddělená v `EDFilterComboBoxLogic` (testovatelná bez
  WPF). `EDFilter/` — skládání filtrů.
- `EDSearchTextBox/` — `TextBox` pro hledání (placeholder, ✕, Esc); styl v `EDControlStyles.xaml`.
- `EDInfoBar/` — info proužek (text + volitelná ikona + akční odkaz); prázdné → skryté,
  styl v `EDControlStyles.xaml`, viditelnost řídí konzument.
- `EDToolbar/` — ikonová tlačítka z `EDViewModel.Actions`; velikost ikon z tokenu
  `EDTheme.IconSize.Default`; v design-time záměrně prázdný (Actions až za běhu), ne bug.
- `UserPrefs/` — `IUserPrefsService`; **default je no-op `NullUserPrefsService`** —
  dokud composition root nenastaví `CacheUserPrefsService` (po vzniku MLRest),
  preference se tiše neukládají.
- `EDSupportPanel/` — vysouvací panel Podpora u pravého okraje okna; připojuje se sám ke
  **každému** oknu (class handler z `EDAppearance.Initialize`), karta žije v owned okně
  (airspace). Vlastní `CLAUDE.md`.
- `MiniApps/` — framework dlaždicových widgetů shellu.

## CACHE strana

`CACHE/EDInputForm.inc` — `#define` konstanty wire kontraktu (typy polí, separátory,
viditelnosti) — **musí sedět s C# stranou**. Třídy per control v `CACHE/ALVA/EDControls/`
(`EDInputForm/`, `EDList/`, `UserData.cls`) + testy v `Tests/` (viz root CLAUDE.md,
sekce Testy).
