# Architektonický review: EDISONMAIN2 + EDControls

| | |
| --- | --- |
| **Datum** | 2026-07-29 |
| **Commit** | `007f62c` (branch `main`, clean) |
| **Scope** | `EDISONMAIN2/`, `EDISONMAIN2.Tests/`, `EDISONMAIN2.LegacyMiniApps/`, `EDControls/`, `EDControls.Tests/` + Caché strany (`EDISONMAIN2/CACHE/**`, `EDControls/CACHE/**`) |
| **Mimo scope** | `TemplateDesigner*`, `ServiceGtfsRt`, `EDTesting`, `tools/ThemeRender` (zmíněny jen jako konzumenti EDControls) |
| **Baseline** | `dotnet build EDISON2.sln` = 0 errors, 16 warnings (2 ve scope) |

Review je read-only. Každý nález je samostatný, aby šel předat do vlastního chatu
(typicky `/wt` worktree per nález). Nálezy jsou řazené podle priority.

**Tenhle dokument je snapshot analýzy a nemění se.** Plán, implementační pořadí a stav
řešení jsou v [arch-backlog.md](arch-backlog.md) - tam se odškrtává.

**Čísla řádků jsou ukotvená v commitu `007f62c`, ne v aktuálním working tree.** Review
běžel nad čistým checkoutem; od té doby se rozjela práce na EDSupportPanelu, která posunula
`EdisonMainViewModel.cs` (-6 ř.), `AppBootstrapper.cs` (+6), `EDViewModel.cs` (+14),
`EDServices.cs` (+8) a tři XAML v `EDTheme`/`EDControlStyles`. U těch souborů hledej podle
jména symbolu, nebo si vytáhni původní stav (`git show 007f62c:<file>`). Příklad posunu:
`EdisonMainViewModel.cs:1167` je v `007f62c` `public bool ShowWindowsAsDialogs`, ve
working tree už jen řádek komentáře.

**Co review záměrně NEobsahuje**: nálezy typu „takhle bych to nenapsal". Několik věcí,
co na první pohled vypadá jako bug, jsem verifikoval a jsou v pořádku - persist layoutu
miniaplikací při zavření shellu (jde přes `EventTrigger Closed` → `CloseAllCommand`
v `EdisonMainWindow.xaml:20-22`), `BasedOn` disciplína v XAML (dodržená všude),
`record` equality u `ExecutableModule` (má reference shortcut, `Favorites.Remove`
je bezpečný), shared DataTemplates ve stromech (nejsou duplikované).

---

## Přehled

| # | Nález | Effort | Prio |
| --- | --- | --- | --- |
| 1 | `EdisonMainViewModel` je God class shellu (1315 ř., 40 commandů, 9 odpovědností) | L | P1 |
| 2 | `RebuildSessionState` dělá ~10 blokujících Caché round-tripů na UI threadu | M | P1 |
| 3 | `EDList` nemá controller seam (na rozdíl od `EDInputForm`) | M | P2 |
| 4 | EDControls natvrdo zná deployment a branding EDISONMAIN2 | M | P2 |
| 5 | `EDServices.Current` static singleton vypnul paralelizaci celé test assembly | M | P2 |
| 6 | `EDInputForm.Initialized` stíní `FrameworkElement.Initialized` (CS0108) | S | P2 |
| 7 | Identita stromu rozprostřená do 7 míst ve 3 paralelních kódováních | M | P2 |
| 8 | Wire kodek EDInputFormu žije ve WPF controlu, controller na něm závisí | S | P2 |
| 9 | `AppBootstrapper` míchá bootstrap, runtime session switch a testovatelnost dědičností | M | P2 |
| 10 | `Tree.FillMain()` je 656řádková generovaná ClassMethod, generátor mimo build | M | P3 |
| 11 | `IMLRest` má dvojí API a `QueryOrThrow` existuje dvakrát | S | P3 |
| 12 | `IMLRest?` nullability protéká do konzumentů (25× `!`) | S | P3 |
| 13 | Mrtvý / nezapojený povrch (toolbar akce, `ShowWindowsAsDialogs`, MHD ukázka) | S | P3 |
| 14 | `SelectedCompanyIndex` setter dělá modální dialog, síť a rollback | S | P3 |
| 15 | Warning stream není čistý, chybí `TreatWarningsAsErrors` | S | P3 |

---

## 1. `EdisonMainViewModel` je God class shellu

**Problém.** Jedna třída drží devět nezávislých odpovědností: přepínání firmy, načítání
a licenční filtrace tří stromů modulů, fulltext vyhledávání ve stromech, persistence
rozbalení, oblíbené, žebříček používanosti, výchozí okno, session a layout miniaplikací,
plus kompletní hlavní menu (font, theme, legacy launche, chord Ctrl+W+W, „O aplikaci").
40 `ICommand` properties, 8 `_suppressXxx` guard flagů, dvě samostatné debounce
či suppress mechaniky.

**Evidence.**

- [EdisonMainViewModel.cs](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs) - 1315 řádků, největší soubor v obou projektech (druhý je `AppBootstrapper` s 534).
- Commandy: [EdisonMainViewModel.cs:46-94](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L46-L94), konstruktor je 118 řádků wiringu ([:205-322](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L205-L322)) s 11 ctor parametry.
- Guard flagy: `_suppressExpansionSave`, `_suppressFavoritesSync`, `_suppressTabSave`, `_startupWindowOpened`, `_searchDebouncer` ([:562-578](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L562-L578), [:806-814](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L806-L814)).
- Testy jsou rozsekané do 4 souborů s vlastními harnessy: `EdisonMainViewModelTests.cs` (619 ř.), `EdisonMainViewModelFilterTests.cs` (393), `EdisonMainViewModelSearchTests.cs` (89), `EdisonMainViewModelExpansionTests.cs` (82), `FavoritesAndUsageTests.cs` (394) - 1577 řádků testů na jednu třídu.

**Dopad.** Change amplification: přidání jedné feature (např. další záložka stromu, další
per-session data) se dotkne konstruktoru, `RebuildSessionState`, `LoadPersonalization`
a nejméně jednoho guard flagu. Každý nový test musí postavit celý VM (11 parametrů,
`EDServices` s mocky, `IMiniAppHost` fake), takže testy jednotlivých feature na sobě
sedí a jsou pomalé na psaní. Riziko regrese při editaci je nadprůměrné právě proto,
že guard flagy jsou sdílené mezi nezávislými mechanikami (`_suppressExpansionSave`
používá `ApplySearch`, `ApplyTreeExpansion`, `RebuildFavoritesTree`,
`SetActiveTreeExpanded` i `SetSubtreeExpanded`).

**Návrh opravy.** Vyseknout ko-lokované service třídy tak, aby VM zůstal orchestrátorem:

- `ModuleTreeState` (kolekce tří stromů + filtrace + `ApplySearch` + persistence rozbalení; už teď je většina logiky `internal static`, takže přesun je mechanický),
- `FavoritesCoordinator` (`_shortcuts` wiring, `SyncFavorites`, `RefreshFamilyFavoriteFlags`, `RebuildFavoritesTree`, `PersistFavoriteSlots`),
- `MiniAppSessionController` (`AttachMiniAppHost`, `Attach/RestoreTreeLayout`, `PersistMiniAppLayout`, `ReplaceMiniAppSession`),
- `ShellMenuCommands` nebo rozdělení `Nastavení`/`Nápověda` commandů do vlastní partial třídy (font, theme, `RunEdisonCopy`, `OpenWebUrl`, `ShowAppInfo`, chord).

Krok po kroku, každý s přesunutými testy. Zachovat `RebuildSessionState` jako jediný
rozšiřovací bod pro session-scoped stav (to je dobrý vzor, jen dnes volá příliš mnoho
věcí přímo).

**Effort** L · **Priorita** P1

---

## 2. `RebuildSessionState` dělá ~10 blokujících Caché round-tripů na UI threadu

**Problém.** Login i každé přepnutí firmy / uživatele projde `RebuildSessionState`, který
synchronně, sériově a na UI threadu udělá deset HTTP requestů na Caché. Během toho je shell
zamrzlý bez indikace průběhu, akce nelze zrušit a částečné selhání nemá definované
zotavení (výjimka z prostředka nechá VM v napůl přestavěném stavu). Pět z těch requestů
jde přes `Task.Run(...)` a hned se na ně čeká `GetAwaiter().GetResult()`, tedy
sync-over-async, který jen obětuje thread-pool vlákno bez jakéhokoli přínosu.

**Evidence.** [EdisonMainViewModel.cs:330-340](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L330-L340) volá v řadě:

| # | Call | Kde |
| --- | --- | --- |
| 1 | `ALVA.SYS.Login.GetEdisonAccess` | [AccessLoader.cs:34](../../EDISONMAIN2/dotnet/Startup/AccessLoader.cs#L34) |
| 2-4 | `ALVA.EDISONMAIN2.API.Tree.GetTree` ×3 (main/system/partners) | [ModuleTreeGetter.cs:14](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/ModuleTreeGetter.cs#L14) přes [EdisonMainViewModel.cs:623-645](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L623-L645) |
| 5-9 | `ALVA.EDControls.UserData.Load` ×5 (Favorites, FavoriteSlots, ModuleUsage, TreeExpansion, SelectedTab) | [ModulePreferencesService.cs:65](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/ModulePreferencesService.cs#L65), [:81](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/ModulePreferencesService.cs#L81) přes [EdisonMainViewModel.cs:397-423](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L397-L423) |
| 10 | `ALVA.USERS.ApiEdison.GetUserDefaultForm` | [UserProfileService.cs:79](../../EDISONMAIN2/dotnet/Windows/UserProfileWindow/UserProfileService.cs#L79) |

Sync-over-async: [CacheUserPrefsService.cs:22-23](../../EDControls/dotnet/UserPrefs/CacheUserPrefsService.cs#L22-L23) obaluje blokující
`QueryOrThrow` do `Task.Run`, [ModulePreferencesService.cs:81](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/ModulePreferencesService.cs#L81) na to ihned čeká.
Komentář v `ModulePreferencesService` to přiznává („Load je sync ... konzistentní
s `ModuleTreeGetter`"), ale nekonzistence je opačná: `IUserPrefsService` je async
kontrakt, který se v tomto jediném konzumentovi degraduje na sync.

Navíc `WindowStatePersistence.TryRestore` dělá další blokující request v
`SourceInitialized` každého otevíraného okna ([WindowStatePersistence.cs:35-55](../../EDControls/dotnet/WindowStatePersistence.cs#L35-L55)),
takže cena se platí i mimo login.

**Dopad.** Na VPN nebo pomalé lince je login a přepnutí firmy viditelné zamrznutí UI
(3 tree cally nad `Tree.cls`, který vrací stovky řádků, nejsou zdaleka nejlehčí requesty).
Uživatel nemá zpětnou vazbu, že se něco děje, a nemůže akci zrušit. `AcquireSession`
retry loop nad tím ještě může přidat modální dialog. Selže-li request č. 5-9,
`ModulePreferencesService` chybu spolkne a stav zůstane prázdný (tichá ztráta oblíbených),
selže-li 1-4, výjimka projde `RelayCommand` handlerem, ale stromy už jsou vyprázdněné
(`target.Clear()` na [:634](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L634) proběhne před requestem).

**Návrh opravy.** Dvě nezávislé části, dají se dělat postupně:

1. **Paralelizace + overlay.** `RebuildSessionState` udělat `async Task`, tři `GetTree`
   a pět `UserData.Load` pustit paralelně (`Task.WhenAll` nad `Task.Run`), celé obalit
   `ActionInProgress()` (mechanika už existuje a používá se u launchů modulů,
   [:899](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L899)). Pozor na pořadí: `LoadPersonalization` závisí na hotových stromech
   (`AllExecutables()`), takže jde o dvě vlny, ne o jednu.
2. **Odstranit sync-over-async.** `ModulePreferencesService` udělat celý async
   (`LoadFavoritesAsync` atd.) a `LoadPersonalization` awaitovat. Tím zmizí
   `GetAwaiter().GetResult()` z obou míst.
3. Buildovat stromy do lokálních kolekcí a `target.Clear()` + naplnění dělat až po
   úspěšném requestu, aby selhání nenechalo prázdný strom.

Poznámka: `Tree.GetTree` by šel sloučit do jednoho requestu pro všechny tři stromy
(server je stejně naplní ze stejné třídy), to je ale změna wire kontraktu - viz nález 10.

**Effort** M · **Priorita** P1

---

## 3. `EDList` nemá controller seam (na rozdíl od `EDInputForm`)

**Problém.** Dvě sesterské komponenty EDControls řeší tentýž problém (dynamický obsah
řízený z Caché) dvěma různými architekturami. `EDInputForm` má vyseknutý
`EDInputFormController` - testovatelnou orchestraci bez WPF. `EDList` nemá nic:
828řádkové code-behind míchá volání MLRest, parsování wire formátu, formátování hodnot,
generování `DataGridColumn` přes `FrameworkElementFactory`, řazení, filtrování,
snapshot a apply persistovaného layoutu.

**Evidence.**

- [EDList.xaml.cs](../../EDControls/dotnet/EDList/EDList.xaml.cs) - 828 řádků, žádný controller. Instanční logika bez testovatelného seamu: `ParseRows` [:366](../../EDControls/dotnet/EDList/EDList.xaml.cs#L366), `FormatValue` [:397](../../EDControls/dotnet/EDList/EDList.xaml.cs#L397), `RebuildColumns` [:438](../../EDControls/dotnet/EDList/EDList.xaml.cs#L438), `BuildDataGridColumn` [:491](../../EDControls/dotnet/EDList/EDList.xaml.cs#L491), `RefreshFilter` [:106](../../EDControls/dotnet/EDList/EDList.xaml.cs#L106), `SnapshotLayout` [:706](../../EDControls/dotnet/EDList/EDList.xaml.cs#L706), `ApplyLayout` [:743](../../EDControls/dotnet/EDList/EDList.xaml.cs#L743), `HookColumnEvents` [:641](../../EDControls/dotnet/EDList/EDList.xaml.cs#L641).
- Protiklad: [EDInputFormController.cs](../../EDControls/dotnet/EDInputForm/EDInputFormController.cs) (132 ř.) + `EDControls.Tests/EDInputFormControllerTests.cs` (555 ř.) testuje Init/Validate/Save orchestraci proti `IMLRest` mocku.
- Testy `EDList` pokrývají jen to, co je `internal static`: `EDListFormattingTests.cs` (151), `EDListSortTests.cs` (169), `EDListFilterTests.cs` (81), `ColumnLayoutTests.cs` (146). Nepokryté zůstává úplně vše instanční, tedy i to, co má nejvíc netriviálních invariantů: reset checked state při `LoadList`, unsubscribe starých řádků, re-apply search a layoutu po `LoadList` ([:296-320](../../EDControls/dotnet/EDList/EDList.xaml.cs#L296-L320)), debounce a force-save layoutu na `Unloaded` ([:180-189](../../EDControls/dotnet/EDList/EDList.xaml.cs#L180-L189)), `_suppressLayoutSave` guard.

**Dopad.** Přesně ty invarianty, které `EDList/CLAUDE.md` vypisuje jako pasti
(„`LoadList` resetuje checked state, odhlásí `PropertyChanged` starých řádků a re-aplikuje
search + layout"), nejdou otestovat, takže drift mezi dokumentací a chováním se nezachytí.
Persist layoutu je best-effort s prázdným `catch` na dvou místech ([:703](../../EDControls/dotnet/EDList/EDList.xaml.cs#L703), [:740](../../EDControls/dotnet/EDList/EDList.xaml.cs#L740)),
takže regrese v serializaci je tichá. Dvojí architektura pro dvě sesterské komponenty
navíc znamená, že nový vývojář musí znát dva vzory.

**Návrh opravy.** Vyseknout `EDListController(IMLRest, string cacheClass)` po vzoru
`EDInputFormController`:

- `InitializeList()` → `IReadOnlyList<ListColumn>`, `LoadList(filter)` → `IReadOnlyList<EDListRow>` (vč. formátování a sort keys),
- `LoadLayoutAsync` / `SaveLayoutAsync` nad `IUserPrefsService` a `ColumnLayout` DTO (bez `DataGridColumn`),
- v code-behind zůstane jen mapování `ColumnLayout` ↔ `DataGrid.Columns` a event wiring.

`ColumnLayoutSerializer` už je oddělený, takže půlka práce je hotová. Testy přidat na
lifecycle `LoadList` (checked reset, unsubscribe, re-apply) - to je hlavní přínos.

**Effort** M · **Priorita** P2

---

## 4. EDControls natvrdo zná deployment a branding EDISONMAIN2

**Problém.** EDControls je deklarovaná jako sdílená knihovna pro m-line aplikace
(root `CLAUDE.md`, konzumenti EDISONMAIN2 + TemplateDesigner + `tools/ThemeRender`),
ale obsahuje neparametrizovatelné znalosti jednoho konkrétního shellu: cestu k deploy
rootu, jméno launcher exe, sentinel launch protokolu, prefix persistovaných formID
a klíč telemetrie. Ke tomu má `EDIcons.xaml` a `EDControls.csproj` desítky ikon, které
používá výhradně TemplateDesigner.

**Evidence.**

- [WindowNavigator.cs:22](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L22) - `public const string EdisonDeployRoot = @"C:\Edison"`.
- [WindowNavigator.cs:26](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L26) - `private const string LauncherExe = "EDISONMAIN2API.exe"`.
- [WindowNavigator.cs:305](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L305) - sentinel `"EDISONMAIN2"` zadrátovaný v interpolovaném stringu (`EdisonLaunchArgs.Sentinel` existuje, ale `BuildLauncherArguments` ho nepoužívá).
- [WindowNavigator.cs:267](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L267) - telemetrický form `"EDISON2.LegacyLaunch"`.
- [EDViewModel.cs:128](../../EDControls/dotnet/EDViewModel/EDViewModel.cs#L128) - `return $"EDISON2.{name}"` jako default `PersistedFormId`. Důsledek: **okna TemplateDesigneru persistují pod formID `EDISON2.XxxWindow`**, i když s EDISONem 2 nemají nic společného.
- `EDControls.csproj` `<Resource>` blok obsahuje ~40 TD-only ikon (`rect_element`, `qr_code`, `departure_grid`, `notes_legend`, `stop_list`, `z_forward`/`z_back`, `align_*`, `distribute_*`, `snap`, `page_properties`, ...), které EDISONMAIN2 nikdy nepoužije.

**Dopad.** Vzhledem k tomu, že se `EDControls.dll` nasazuje jako jedna volná DLL vedle
oba exe a její veřejné API se smí měnit jen aditivně (root `CLAUDE.md`), je každá z těch
konstant fakticky zamčená. Konkrétní cena už dnes:

- TemplateDesigner nemůže mít vlastní formID namespace, takže jeho window state koliduje
  v jednom jmenném prostoru se shellem.
- Změna deploy rootu (jiný zákazník, jiný disk) je změna sdílené knihovny, tedy publish
  obou aplikací.
- Každá nová TD ikona nutí publikovat `EDControls.dll` do produkce i pro shell.
- Nová m-line aplikace zdědí `WindowNavigator` s legacy launch aparátem, který nikdy
  nepoužije, a s ním i závislost na `EDISONMAIN2API.exe`.

**Návrh opravy.** Aditivně, bez breaking change:

1. Zavést `EDControls.EDAppIdentity` (statická konfigurace nastavená v composition rootu):
   `DeployRoot`, `PersistedFormIdPrefix`, `LegacyLauncherExe`, `LegacyLaunchSentinel`.
   Defaulty ponechat na dnešních hodnotách, takže existující chování se nemění a EDISONMAIN2
   nemusí nic nastavovat.
2. `EDViewModel.PersistedFormId` čte prefix z `EDAppIdentity`, TemplateDesigner si v
   `App.xaml.cs` nastaví `"TD."`. Pozor: to je migrace persistovaného stavu TD oken -
   podle `project_predeployment_no_migrations` je to dnes akceptovatelné, ale je to
   vědomé rozhodnutí, ne side effect.
3. `BuildLauncherArguments` má používat `EdisonLaunchArgs.Sentinel`, ne literál.
4. TD ikony: buď přesunout do vlastního `ResourceDictionary` v TemplateDesigneru,
   nebo (pokud je sdílení záměr) doplnit do `EDControls/CLAUDE.md` explicitně, že
   `EDIcons` je sdílený katalog napříč aplikacemi včetně TD-specifických ikon,
   aby to nevypadalo jako omyl.

**Effort** M · **Priorita** P2

---

## 5. `EDServices.Current` static singleton vypnul paralelizaci celé test assembly

**Problém.** `EDServices.Current` je mutable statický singleton. Není to jen convenience
pro bezparametrické ctory ViewModelů (to je zdokumentovaný a rozumný kompromis pro
controls) - sahá na něj i infrastruktura, která by dependency dostat mohla:
`WindowNavigator`, `WindowStatePersistence`, `RelayCommand` a `AsyncRelayCommand`
error handling. Cena se platí v testech.

**Evidence.**

- [EDServices.cs:44-59](../../EDControls/dotnet/EDViewModel/EDServices.cs#L44-L59) - `_current` + `Initialize` + `internal Reset()` (existuje jen pro testy).
- Skrytí dependency: [WindowNavigator.cs:263](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L263), [:278](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L278), [:321](../../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L321); [WindowStatePersistence.cs:39](../../EDControls/dotnet/WindowStatePersistence.cs#L39), [:61](../../EDControls/dotnet/WindowStatePersistence.cs#L61); [RelayCommand.cs:18](../../EDControls/dotnet/EDViewModel/RelayCommand.cs#L18), [:35](../../EDControls/dotnet/EDViewModel/RelayCommand.cs#L35); [AsyncRelayCommand.cs:34](../../EDControls/dotnet/EDViewModel/AsyncRelayCommand.cs#L34), [:63](../../EDControls/dotnet/EDViewModel/AsyncRelayCommand.cs#L63).
- [EDISONMAIN2.Tests/AssemblyInfo.cs:6](../../EDISONMAIN2.Tests/AssemblyInfo.cs#L6) - `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, komentář to přímo odůvodňuje mutací `EDServices.Current`. Jde o celou assembly, tedy ~6000 řádků a 37 test souborů běží sériově.
- V `EDControls.Tests` totéž řešeno per-třídu: `RelayCommandTests.cs:6` a `AsyncRelayCommandTests.cs:6` mají komentář o „serializované kolekci".

**Dopad.** Test suite EDISONMAIN2 nejde paralelizovat, což zpomaluje pre-push hook
(a to je jediné místo, kde testy reálně brání pushnout regresi). Testy, které chtějí
ověřit zachycení výjimky mockem, musí mock registrovat globálně přes `EDServices`
nebo předat 3. argumentem - past už zdokumentovaná v
`EDControls/dotnet/EDViewModel/CLAUDE.md`, ale pořád past. Do budoucna hlavní riziko je,
že se přidávání dalších consumerů `Current` normalizuje.

**Návrh opravy.** Nezrušit singleton (u controls je opravdu potřeba - `EDList`
a `EDInputForm` nesmí záviset na `Window.DataContext`), ale zastavit jeho šíření:

1. `WindowNavigator` dostane `EDServices` konstruktorem (composition root ho stejně
   vytváří první a `EDServices` se pak plní mutable settery, takže stačí předat instanci
   po `Initialize`, případně `Func<EDServices>`). Tím se testovatelně otevře
   `LaunchLegacy` a `WireUp`.
2. `WindowStatePersistence.Attach(window, formId, IMLRest?)` - přetížení navíc, staré
   ponechat (aditivní změna API).
3. Ověřit, jestli po 1+2 může `EDISONMAIN2.Tests` paralelizaci zapnout zpět a nahradit
   assembly-wide vypnutí per-collection atributem na těch harnessech, které `Initialize`
   opravdu volají.

**Effort** M · **Priorita** P2

---

## 6. `EDInputForm.Initialized` stíní `FrameworkElement.Initialized`

**Problém.** `EDInputForm` deklaruje `public event Action? Initialized`, čímž stíní
zděděný `FrameworkElement.Initialized`. Kompilátor to hlásí (CS0108) a warning je
v buildu dlouhodobě přítomný. Je to veřejné API knihovny, která se smí měnit jen
aditivně, takže se s tím žije.

**Evidence.**

- [EDInputForm.xaml.cs:254](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L254) - `public event Action? Initialized;`, vyvolané v [:274](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L274) po `Controller.Init(...)`.
- Build warning (2× kvůli `_wpftmp` projektu): `EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs(254,30): warning CS0108`.
- 8 call sites v TemplateDesigneru se na něj váže (`InlinePropertiesForm.xaml.cs:34`, `ElementEditDialog.xaml.cs:50`, `DepartureGroupEditorDialog.xaml.cs:48`, `DynamicTableEditorDialog.xaml.cs:59`, `LineTableColumnsEditorDialog.xaml.cs:38`, `MainWindow.xaml.cs:1096`, `PrintDialog.xaml.cs:27`, `SaveToCacheDialog.xaml.cs:28`).

**Dopad.** Dnes to funguje jen díky tomu, že všechny call sites mají statický typ
`EDInputForm`. Jakmile někdo drží referenci jako `FrameworkElement`, `UserControl`
nebo `object` (typicky generický helper, `FindVisualChild<T>`, DataTemplate hostování),
přihlásí se tiše na WPF event, který fíruje v úplně jinou dobu (při inicializaci
visual tree, ne po načtení schématu z Caché) a nikdy neřekne „schema je hotové".
Chyba se projeví až za běhu, kompilátor ji nezachytí, a debugging je nepříjemný
protože oba eventy se jmenují stejně. Zároveň platí, že event kvůli aditivnímu
API kontraktu nelze prostě přejmenovat.

**Návrh opravy.** Aditivně: přidat `public event Action? SchemaLoaded` (nebo
`FormInitialized`), vyvolávat obojí, `Initialized` označit `[Obsolete("Použij SchemaLoaded - stíní FrameworkElement.Initialized")]`
+ `new` klíčové slovo (tím zmizí CS0108 a intent je explicitní). Migrovat 8 call sites
v TemplateDesigneru. Odstranit `Initialized` teprve při příštím koordinovaném publish
obou aplikací (root `CLAUDE.md`: „skutečný breaking change ⇒ publikovat všechny konzumenty").
Aktualizovat `EDControls/dotnet/EDInputForm/CLAUDE.md`, kde je `Initialized` uvedený
v seznamu eventů.

**Effort** S · **Priorita** P2

---

## 7. Identita stromu je rozprostřená do 7 míst ve 3 paralelních kódováních

**Problém.** Jeden strom modulů je v kódu reprezentován třemi nezávislými způsoby:
názvem property (`MainMenuTree`), stringovým názvem pro server a persistenci (`"main"`),
a magickým indexem záložky (`0`). Žádný z nich není odvozený z ostatních, takže
přidání, odebrání nebo přeuspořádání záložky vyžaduje koordinovanou editaci
na sedmi místech ve třech souborech.

**Evidence.**

| Kódování | Místo |
| --- | --- |
| Property | [EdisonMainViewModel.cs:740-748](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L740-L748) |
| String názvy (server + persist klíč) | [:304](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L304) `_sourceTrees`, [:821-822](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L821-L822) `ExpandableTrees` s literálem `"favorites"` |
| Index záložky (VM) | [:467-474](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L467-L474) `ActiveExpandableTree()` switch 0-3 |
| Index záložky (VM, default) | [:802](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L802) `DefaultTreeTab()` vrací `3` nebo `0` |
| Index záložky (komentář) | [:786](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L786) - pořadí záložek popsané prózou |
| Index záložky (code-behind) | [EdisonMainWindow.xaml.cs:169-176](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs#L169-L176) druhý switch 0-3 mapující index → `TreeView` |
| Jména `TreeView` | [EdisonMainWindow.xaml.cs:32-35](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs#L32-L35) 4× ruční `SelectedItemChanged` wiring |
| Pořadí `TabItem` | `EdisonMainWindow.xaml:355-427` |

**Dopad.** Dva switche na indexy 0-3 (jeden ve VM, jeden v code-behind) musí zůstat
v sync s pořadím `TabItem` v XAML, jinak Rozbalit/Sbalit vše cílí na jiný strom než
uživatel vidí, případně `SelectedModule` neodpovídá aktivní záložce. Nic to nehlídá -
ani test, ani typový systém. `SelectedTreeTabIndex` se navíc persistuje jako číslo
(per-user × IDFa), takže přeuspořádání záložek tiše přepne uloženou volbu existujících
uživatelů na jiný strom.

**Návrh opravy.** Zavést jeden deskriptor a odvodit z něj ostatní:
```
record TreeTab(string Key, string DisplayName, ObservableCollection<IModule>? Tree, bool Expandable);
IReadOnlyList<TreeTab> TreeTabs { get; }   // main, system, partners, favorites, used
```

- `ActiveExpandableTree()` = `TreeTabs[SelectedTreeTabIndex]` s `Expandable` guardem (zmizí switch),
- `_sourceTrees` a `ExpandableTrees` se derivují z `TreeTabs` (zmizí literál `"favorites"`),
- XAML: `TabControl` bindovaný na `TreeTabs` s `ItemTemplate`/`ContentTemplate` místo pěti
  ručních `TabItem` (odstraní i 4× ruční wiring `SelectedItemChanged` - stačí jeden
  handler na `TabControl` úrovni),
- `SelectedTreeTabIndex` persistovat jako `Key` string, ne index (jednorázová migrace,
  dnes bezplatná - `project_predeployment_no_migrations`).

Záložka Používané je `ListBox`, ne `TreeView`, takže `Tree` je nullable a `Expandable=false` -
to je právě ten rozdíl, který dnes kóduje `_ => null` ve dvou switchech.

**Effort** M · **Priorita** P2

---

## 8. Wire kodek EDInputFormu žije ve WPF controlu, controller na něm závisí

**Problém.** `EDInputFormController` je dokumentovaný jako „testovatelná orchestrace
bez WPF závislosti", ale kompletní kodek wire formátu (serializace do `CVar`,
parsování `CList` řádků, parsování `helpTextMode`) je `internal static` na WPF
UserControlu `EDInputForm`, a controller ho odtud volá. Závislost tedy míří opačně,
než by měla: doménová orchestrace závisí na view.

**Evidence.**

- Kodek na view: [EDInputForm.xaml.cs:301](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L301) `SerializeItemsCVar`, [:337](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L337) `ParseItems`, [:400](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L400) `ParseHelpTextMode`, [:408-428](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L408-L428) `SerializeListItems`/`ParseListItems`.
- Controller na nich visí: [EDInputFormController.cs:56](../../EDControls/dotnet/EDInputForm/EDInputFormController.cs#L56), [:73](../../EDControls/dotnet/EDInputForm/EDInputFormController.cs#L73), [:122-123](../../EDControls/dotnet/EDInputForm/EDInputFormController.cs#L122-L123).
- Test suite kodeku (`EDControls.Tests/EDInputFormParsingTests.cs`, 1044 řádků - největší
  test soubor v repu) tím pádem referencuje WPF UserControl typ.

**Dopad.** Kodek nejde použít nikde, kde není WPF (např. headless kontraktový test proti
Caché, nebo budoucí web/service klient). Statická inicializace `EDInputForm` typu
(včetně `PickColorImpl` / `PickImageImpl` static state) se váže i na čistě datové
operace. A protože wire formát je „pořadí pozic = protokol, neměnit" (viz
`EDInputForm/CLAUDE.md`), je špatné umístění tím nepříjemnější, že se tam bude
sahat při každém novém poli.

**Návrh opravy.** Přesunout pět `internal static` metod do nové `EDInputFormWire`
(nebo do `EDInputFormController` jako statické členy) a v `EDInputForm.xaml.cs`
nechat jen delegující volání, pokud jsou někde potřeba. Čistě mechanický přesun,
`internal` viditelnost se nemění, `InternalsVisibleTo` už je nastavené, testy stačí
přesměrovat. Aktualizovat odkazy v `EDInputForm/CLAUDE.md`.

**Effort** S · **Priorita** P2

---

## 9. `AppBootstrapper` míchá bootstrap, runtime session switch a testovatelnost dědičností

**Problém.** Tři různé věci v jedné 534řádkové třídě:

1. **Startup workflow** (`Run`), poctivě rozdělený na pojmenované kroky - to je dobré.
2. **Runtime operace** `SwitchUser`, kterou volá `EdisonMainViewModel` za běhu přes
   `Func<SwitchUserOutcome>`. Bootstrapper tedy žije po celou dobu běhu aplikace,
   ne jen při startu, a drží mutable stav (`_releaseNoticeTask`).
3. **Testovatelnost dědičností** - 12 `protected virtual` hooků a druhý, chráněný
   konstruktor, který nastaví `_application = null!` a spoléhá na to, že test subclass
   přepíše přesně ty metody, které by `Application` použily.

Navíc tam žije nesouvisející UI detail: WinForms color picker pro `EDInputForm`
(66 řádků včetně `IWin32Window` shimu).

**Evidence.**

- [AppBootstrapper.cs](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs) - 534 řádků.
- Test-only ctor s `null!`: [:62-68](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L62-L68), doc komentář vypisuje pět metod, které subclass „MUSÍ" přepsat.
- Runtime lifetime: [:454-480](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L454-L480) `SwitchUser`, zadrátovaný do VM jako `Func` při stavbě shellu ([:498](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L498)) a volaný za běhu z [EdisonMainViewModel.cs:1106](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L1106); mutable `_releaseNoticeTask` [:171](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L171) nastavovaný v `CheckForUpdate` a čtený v `BuildShellWindow` [:507](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L507).
- Color picker: [:196-235](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L196-L235).
- `EDISONMAIN2.Tests/AppBootstrapperTests.cs` - 490 řádků, celé postavené na subclassech.

**Dopad.** Testovatelnost přes dědičnost je fragilní: přidání kroku do `Run`, který
sáhne na `_application`, tiše rozbije všechny test subclassy s `NullReferenceException`
až za běhu testu, ne při kompilaci. Sdílený mutable `_releaseNoticeTask` mezi dvěma
kroky workflow je implicitní vazba, kterou typový systém nehlídá. A protože bootstrapper
žije dál kvůli `SwitchUser`, není zřejmé, co v něm smí být stavové.

**Návrh opravy.** Tři oddělitelné kroky, každý samostatně:

1. Color picker vyseknout do `EDISONMAIN2/dotnet/Startup/WinFormsColorPicker.cs`
   (nebo `MiniApps`-style ko-lokace u wire-upu). Čistý přesun, ~40 řádků z bootstrapperu.
2. `SwitchUser` přesunout do vlastního `SessionSwitcher` (paralela ke `CompanySwitcher`,
   který přesně tuhle roli už má - přepnutí firmy). Bootstrapper by mu předal
   `EstablishSession`/`AcquireSession` jako delegáty nebo by se ta dvojice přesunula
   do `SessionSwitcher` a bootstrapper by ji volal. Tím `AppBootstrapper` přestane
   žít po startu.
3. `_releaseNoticeTask` předat jako návratovou hodnotu `CheckForUpdate`
   (`(bool ShouldExit, Task<ReleaseNoticeResult>? Notice)`) místo pole.

Hooky-přes-`virtual` bych nechal - refaktor na plnou DI by byl velký a dnešní varianta
je zdokumentovaná a funkční. Ale ten `null!` konstruktor stojí za komentář v testech
o tom, co se stane, když se přidá krok.

**Effort** M · **Priorita** P2

---

## 10. `Tree.FillMain()` je 656řádková generovaná ClassMethod, generátor mimo build

**Problém.** Katalog modulů (celá nabídka EDISONu) je zapsaný jako kód: tři
`ClassMethod`y s ~700 řádky imperativních `d ..AddExe(...)` / `d ..AddFamily(...)`
volání. `FillMain()` samotná má 656 řádků. Data jsou generovaná z legacy
`edisonnavbar.xml` skriptem `tools/GenerateTree.cs`, který ale není součástí žádného
projektu ani buildu - je to volný `.cs` soubor.

**Evidence.**

- [Tree.cls](../../EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls) - 811 řádků celkem; `FillMain()` [:81-737](../../EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls#L81-L737), `FillSystem()` [:741-770](../../EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls#L741-L770), `FillPartners()` [:774-809](../../EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls#L774-L809).
- Odkaz na generátor: [Tree.cls:80](../../EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls#L80) („Generováno z legacy edisonnavbar.xml (tools/GenerateTree.cs)").
- `tools/GenerateTree.cs` existuje, ale `find . -name "*.csproj"` ho nezahrnuje do žádného projektu - není v `EDISON2.sln`, nespustí ho build ani hook.

**Dopad.** Generovaný kód je v repu editovatelný ručně, takže se dřív nebo později
rozejde se zdrojovým XML a s generátorem (klasický „generated file checked in bez
guardu"). Nikdo dnes neumí ověřit, že `Tree.cls` odpovídá `edisonnavbar.xml` - to je
o to citlivější, že `elementId` v každém uzlu řídí licenční gate a aplikační přístup
(nálezy jsou hlídané `TreeTest.cls`, ale jen strukturálně, ne obsahově). Zároveň se
celý katalog rebuilduje a přenáší třemi requesty při každém přepnutí firmy (viz nález 2),
i když je per-instalaci konstantní.

**Návrh opravy.** Dvě možnosti, obě rozumné, doporučuji A:

- **A: udělat z generátoru součást repa.** `tools/GenerateTree` jako `.csproj`
  v `EDISON2.sln`, `edisonnavbar.xml` (nebo jeho snapshot) do repa jako vstup,
  a Caché test / pre-push kontrola, že vygenerovaný výstup je byte-identický
  s `Tree.cls` (parita test proti driftu). Ruční editace se tak zachytí.
- **B: udělat z katalogu data.** `Tree.cls` čte strom z globálu naplněného
  instalačním skriptem. Odstraní God-method i limit velikosti compiled ClassMethod,
  ale je to změna deploy procesu, tedy víc práce a rizika.

Nezávisle na volbě: přidat do `EDISONMAIN2/CLAUDE.md` větu o tom, že `Tree.cls`
je generovaný a jak se regeneruje - dnes to ví jen doc komentář uvnitř třídy.

**Effort** M · **Priorita** P3

---

## 11. `IMLRest` má dvojí API a `QueryOrThrow` existuje dvakrát

**Problém.** Wire abstrakce vystavuje dvě paralelní volací konvence (`CVar`
a `object[]`) a `QueryOrThrow` je implementované dvakrát v téměř identickém kódu -
raz jako metoda adaptéru, raz jako extension nad konkrétním `MLRest`.

**Evidence.**

- [IMLRest.cs:12-13](../../EDControls/dotnet/IMLRest.cs#L12-L13) - dvě `QueryOrThrow` overloady.
- [MLRestAdapter.cs:13-25](../../EDControls/dotnet/MLRestAdapter.cs#L13-L25) vs [MLRestExtensions.cs:7-19](../../EDControls/dotnet/MLRestExtensions.cs#L7-L19) - tělo obou je `Query` + `RestStatus.ThrowIfError()` + `return result`, čtyřikrát opsané.
- Reálné rozšíření `object[]` cesty: 10 produkčních call sites (`UserProfileService` 5×, `UserAuthenticator` 2×, `Updater`, `SessionLogout`, `LogoutWatcher`, `LicenceVerifier`, `AccessLoader`, `WindowStatePersistence`, `EDUsageTelemetry`) - tedy ne uzavřená legacy množina, ale živě používaná polovina API.

**Dopad.** Nový kód má dvě legální možnosti a rozhoduje se podle toho, co dělá
protistrana, takže pravidlo z root `CLAUDE.md` („vždy `CVar`, `object[]` jen v legacy
kódu s existujícími numerickými subscripty") se v praxi obchází bez zdokumentovaného
důvodu (mnohé z těch call sites důvod nemají uvedený, i když ho pravidlo vyžaduje).
Duplikované `QueryOrThrow` znamená, že změna error handlingu (např. přidání retry
nebo logování) se musí udělat na dvou místech a jedno se snadno zapomene.

**Návrh opravy.**

1. `MLRestExtensions.QueryOrThrow` přepsat tak, aby delegoval na `MLRestAdapter`
   (`new MLRestAdapter(ml).QueryOrThrow(...)`), případně obojí na jednu privátní
   implementaci. Jedno místo pravdy pro `ThrowIfError`.
2. Do každého `object[]` call site doplnit jednořádkové odůvodnění (proč legacy
   numerické subscripty), jak vyžaduje root `CLAUDE.md`. Tam, kde důvod není
   (typicky nové endpointy v `ALVA.USERS.ApiEdison`), zvážit převod na `CVar`
   společně se serverovou stranou.

**Effort** S · **Priorita** P3

---

## 12. `IMLRest?` nullability protéká do konzumentů

**Problém.** `EDServices.MLRest` je `IMLRest?`, protože v okně mezi
`EDServices.Initialize` a `EstablishSession` opravdu není. Ta pre-login nullability se
ale nikde neuzavře, takže se veze do všech post-login konzumentů, kteří ji řeší
null-forgiving operátorem.

**Evidence.**

- [EDServices.cs:23](../../EDControls/dotnet/EDViewModel/EDServices.cs#L23) - `public IMLRest? MLRest { get; set; }`; nastaveno na `null` v [AppBootstrapper.cs:193](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L193) a naplněno až v [:388](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L388).
- 25 výskytů `MLRest!` napříč oběma projekty + TemplateDesignerem; 16 z nich v jediném souboru [EdisonMainViewModel.cs](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs) (např. status bar properties [:713-717](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L713-L717), `ShowAppInfo` [:1238-1241](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L1238-L1241)).
- Controls to řeší runtime guardem místo `!`: [EDList.xaml.cs:629-633](../../EDControls/dotnet/EDList/EDList.xaml.cs#L629-L633) `EnsureConfigured`, [EDInputForm.xaml.cs:657-661](../../EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs#L657-L661) - tedy třetí, nekonzistentní vzor.

**Dopad.** `!` je vypnutá kontrola: kdyby se `MLRest` někdy stal null za běhu (např.
budoucí explicitní teardown session), vzniklo by 25 `NullReferenceException` míst,
o kterých kompilátor nic neřekne. Zároveň je to šum, který zakrývá skutečně
zajímavé nullability (`ActiveLicence`, `AccessTable`, `LicenceLoader` - ty null být
legitimně mohou a dnes vypadají stejně jako `MLRest`).

**Návrh opravy.** Aditivně: doplnit do `EDServices` non-null accessor
```
public IMLRest RequireMLRest => MLRest ?? throw new InvalidOperationException("MLRest není k dispozici před loginem.");
```
a v `EDViewModel` vedle `MLRest` (`IMLRest?`, ponechat) přidat `protected IMLRest Rest => _services.RequireMLRest`.
Konzumenty po loginu (VM, services) převést na `Rest`, controls nechat na dnešním
`EnsureConfigured` (mají vlastní chybovou hlášku pro hosta). Nic se nerozbije,
`!` zmizí a chyba se projeví jako srozumitelná výjimka, ne jako NRE.

**Effort** S · **Priorita** P3

---

## 13. Mrtvý a nezapojený povrch

**Problém.** Čtyři nezávislé kusy API a UI, které existují, ale nikdo je nepoužívá,
případně slibují chování, které nemají.

**Evidence.**

1. **`EDComponentToolbar` Export / Print / Settings.** Tři `Button`y +
   pět `DependencyProperty` ([EDComponentToolbar.xaml.cs:145-224](../../EDControls/dotnet/EDComponentToolbar/EDComponentToolbar.xaml.cs#L145-L224), `EDComponentToolbar.xaml:53-72`).
   Oba jediní konzumenti je explicitně vypínají: `EDList.xaml:17`
   (`ShowHelp="False" ShowSettings="False"`) a `EDInputForm.xaml:36` (`ShowSettings="False"`).
   `ShowExport` / `ShowPrint` nikdo nikde nezapíná (grep napříč všemi projekty
   včetně TemplateDesigneru = 0 hitů). Přitom `EDControls/CLAUDE.md` toolbar popisuje
   jako „(search, export, ...)" - stale tvrzení.
2. **`ShowWindowsAsDialogs`.** Persistuje se do `local.json`, má UI toggle, ale nikdo
   ho nečte ([LocalPrefs.cs:52](../../EDISONMAIN2/dotnet/Startup/LocalPrefs.cs#L52), [EdisonMainViewModel.cs:1167-1177](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L1167-L1177) - komentář to přiznává).
   Uživatel má v menu přepínač, který nic nedělá.
3. **`Mhd.Sample` „MHD (ukázka)" v Release buildu.** `LegacyMiniAppProvider` registruje
   self-described placeholder ([LegacyMiniAppProvider.cs:16-17](../../EDISONMAIN2.LegacyMiniApps/dotnet/LegacyMiniAppProvider.cs#L16-L17), VM říká
   „Placeholder mini-app ... Sem přijde MHD nad daty") a **není pod `#if DEBUG`** -
   na rozdíl od `DevSampleMiniApps`, které pod ním jsou ([AppBootstrapper.cs:490-496](../../EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs#L490-L496)).
   V produkčním menu „Miniaplikace → MHD" tedy uživatel uvidí ukázku.
4. **`EDList` typy IMAGE a CODEBOOK.** Deklarované v `EListColumnType`
   ([EDList.xaml.cs:345-356](../../EDControls/dotnet/EDList/EDList.xaml.cs#L345-L356)), ale v renderingu neimplementované -
   propadnou na text. `EDList/CLAUDE.md` to poctivě přiznává, takže jde spíš
   o nedokončený wire kontrakt než o skrytý bug.

**Dopad.** Body 1 a 2 jsou API, které vypadá jako feature a není: kdo se na
`ShowExport` spolehne, zjistí až za běhu, že export není implementovaný (`ExportCommand`
je jen DP, žádná default implementace). Bod 3 je viditelný v produkčním UI. Bod 4
je riziko na wire straně - Caché autor může poslat `type: "IMAGE"` a dostane raw text
bez chyby.

**Návrh opravy.** Každý bod samostatně, všechny malé:

1. Buď Export / Print doimplementovat v `EDList` (export do CSV/XLSX je reálně užitečný
   a `SnapshotLayout` k tomu má data), nebo DP a buttony odstranit. Rozhodnutí je
   produktové. Odstranění je breaking change public API EDControls, takže spíš
   `[Obsolete]` a smazat při příštím koordinovaném publish. `EDControls/CLAUDE.md`
   opravit tak, aby netvrdilo, že export existuje.
2. `ShowWindowsAsDialogs`: buď doimplementovat (`WindowNavigator.Show` → `ShowDialog`
   podle prefu), nebo odstranit z menu a nechat jen persistovaný klíč.
   Toggle, který nic nedělá, je horší než chybějící toggle.
3. `Mhd.Sample` obalit `#if DEBUG` (nebo smazat, `Mhd.Dispecink` už je reálná mini-app,
   takže cross-assembly pipeline je ověřená a placeholder svůj účel splnil).
4. IMAGE/CODEBOOK: doimplementovat rendering, nebo v `EDList` na neznámý implementovaný
   typ hlásit chybu při `InitializeList` (fail fast na wire kontraktu), místo tichého
   fallbacku.

**Effort** S (každý bod) · **Priorita** P3

---

## 14. `SelectedCompanyIndex` setter dělá modální dialog, síť a rollback

**Problém.** Setter TwoWay-bindované property spustí modální potvrzovací dialog,
deset síťových requestů, licenční retry loop (který může otevřít další proces
a čekat na jeho zavření) a při odmítnutí se sám rollbackne včetně ručního
přeposlání čtyř `OnPropertyChanged`.

**Evidence.** [EdisonMainViewModel.cs:827-863](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L827-L863) - 37 řádků v setteru:
`PopupDisplayer.Confirm` → `Set(...)` + 4× `OnPropertyChanged` → `_switcher.Switch`
(uvnitř `LicenceVerifier.TryAcquireSession` s `ShowLegacyAndWait`) →
`RebuildSessionState` + `ReplaceMiniAppSession`, nebo rollback + další 3×
`OnPropertyChanged`. Druhá vstupní cesta `SelectedCompanyId` [:878-887](../../EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L878-L887)
deleguje na tentýž setter a spoléhá na závěrečné `OnPropertyChanged`, které
combobox vrátí na původní hodnotu.

**Dopad.** WPF binding infrastruktura volá settery v kontextech, kde blokující modální
dialog není bezpečný (během layoutu, uprostřed `SelectionChanged`), takže je to latentní
reentrance riziko. Rollback je ruční a duplikuje seznam notifikovaných properties,
takže přidání páté derived property se snadno zapomene v jedné z obou větví.
Testovatelnost je nepřímá - test musí nastavovat property, aby spustil workflow.
Nejde o dnešní bug (testy `CompanySwitcherTests` 362 ř. logiku pokrývají a `Confirm`
je mockovaný), ale o strukturu, která na to čeká.

**Návrh opravy.** Rozdělit na property a command:

- `SelectedCompanyId` / `SelectedCompanyIndex` nechat jako čistý stav (`Set` + notifikace),
- `SwitchCompanyCommand(string idfa)` udělat celý workflow (confirm, switch, rebuild,
  rollback) a bindovat ho z view na `SelectionChanged` combboxu, případně použít
  `AsyncRelayCommand`, což zároveň dá double-click guard a napojí to na řešení nálezu 2,
- rollback vyjádřit jedním `RaiseSessionPropertiesChanged()` helperem, ať existuje
  jeden seznam derived properties místo dvou.

**Effort** S · **Priorita** P3

---

## 15. Warning stream není čistý, chybí `TreatWarningsAsErrors`

**Problém.** `dotnet build EDISON2.sln` projde s 16 warningy, z toho 2 ve scope tohoto
review. Žádný projekt nemá `TreatWarningsAsErrors` ani `WarningsAsErrors` pro nullable,
takže nový warning zapadne mezi existující.

**Evidence.**

- `EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs(254,30): warning CS0108` - viz nález 6.
- `EDISONMAIN2/dotnet/MiniApps/PeriodicRefreshMiniAppViewModel.cs(29,34): warning CS8602: Dereference of a possibly null reference` - [PeriodicRefreshMiniAppViewModel.cs:29](../../EDISONMAIN2/dotnet/MiniApps/PeriodicRefreshMiniAppViewModel.cs#L29) čte `RefreshNowCommand` v lambdě před jeho přiřazením na řádku 30. Za běhu je to neškodné (lambda se vyhodnotí až při `Tick`), ale je to signál, že se warningy nečtou.
- Oba projekty mají `<Nullable>enable</Nullable>`, tedy nullable analýza běží a její výstup se ignoruje.
- Zbylých 14 warningů je v `TemplateDesigner` a `ServiceGtfsRt` (mimo scope), včetně `NU1903` na `Newtonsoft.Json` 7.0.1 - to patří do samostatného zadání pro TD.

**Dopad.** Nullable warningy jsou v tomto kódu užitečné (viz nález 12, kde se
nullability obchází 25 `!`), ale ignorovaný warning stream je znehodnocuje: CS0108
je reálná API past (nález 6) a nikdo si jí několik commitů nevšiml. Pre-push hook
testuje, ale build warningy nekontroluje.

**Návrh opravy.**

1. Opravit ty dva warningy: `PeriodicRefreshMiniAppViewModel` přeuspořádat
   (`RefreshNowCommand = Command(RefreshNow);` před `_timer.Tick += ...`),
   CS0108 vyřešit v rámci nálezu 6.
2. Do `EDControls.csproj` a `EDISONMAIN2.csproj` přidat
   `<WarningsAsErrors>Nullable</WarningsAsErrors>` (nebo `$(WarningsAsErrors);CS0108`),
   případně `TreatWarningsAsErrors` pro celý projekt, jestli se podaří dostat warningy
   na nulu. Doporučuji začít úzce (nullable + CS0108) a rozšířit později -
   `TreatWarningsAsErrors` na WPF projektech umí překvapit generovaným kódem
   (`_wpftmp` projekty se buildí zvlášť, viz zdvojené warningy ve výstupu).
3. `TemplateDesigner` a `ServiceGtfsRt` řešit samostatně, aby se scope nemíchal.

**Effort** S · **Priorita** P3
