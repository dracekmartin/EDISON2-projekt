# Architektonický review: živý dokument nálezů

| | |
| --- | --- |
| **Poslední běh** | 2026-09-20 |
| **Commit** | `c04d0d6f` (branch `main`, clean) |
| **Scope běhu** | `EDISONMAIN2/` (+ `.LegacyMiniApps`, `.Tests`, `CACHE/`), `EDControls/` (+ `.Tests`, `CACHE/`), `EDUPDATE2/` (+ `.Tests`) |
| **Mimo scope** | `TemplateDesigner*` (jen jako konzument EDControls), `ServiceGtfsRt`, `EDTesting`, `tools/` |
| **Odvozeno** | 4 read-only subagenti na oblasti shell / startup / EDControls infra / EDControls controls (~220 souborů), EDUPDATE2 ručně; každý nález ověřen citací kódu na HEAD |

**Hotovo: 5 / 44**

## Jak s dokumentem zacházet

- Jeden dokument na repo, žije napříč běhy review. ID `A1`, `A2`… jsou stabilní a nikdy se
  nepřidělují znovu; další běh přidá nová ID a u existujících jen ověří stav.
- **Opravný chat řeší jedno ID.** Nález je samonosný: nemá kontext review chatu, všechno
  potřebné (důkaz `path:line`, dopad, návrh, omezení) je u něj. Čísla řádků jsou ukotvená
  v commitu `c04d0d6f`; při posunu hledej podle jména symbolu.
- Po opravě odškrtni `- [x]` a přepiš `**Stav:**` na `vyřešeno <commit> (YYYY-MM-DD) - co se
  reálně udělalo` (když se návrh během implementace změnil, napiš čím a proč). Přijaté
  riziko dostane `přijato <důvod>`. Aktualizuj počítadlo **Hotovo** nahoře.
- 🔸 = vyžaduje rozhodnutí vlastníka (produkt, wire kontrakt, legacy), ne jen technickou práci.
- **Omezení** u nálezu = zmrazená plocha, které se opravný chat nesmí dotknout bez rozhodnutí
  vlastníka. Nejčastější: veřejné API `EDControls.dll` (samostatně nasazovaná DLL, breaking
  change = publikovat i TemplateDesigner, viz root `CLAUDE.md`), wire kontrakty Caché,
  launch kontrakt legacy hosta (append-only), ACTIVITY zámek.
- Řazení je podle priority: **P1** chyba, blokované testy nebo dodávka · **P2** zesílení změn,
  vazba bránící plánované změně · **P3** hygiena, drift, mrtvý modul. Pracnost **S** jeden
  chat · **M** několik chatů · **L** potřebuje plán a rozhodnutí.
- Předchozí review (2026-07-29) žije v [arch-EDISONMAIN2-EDControls-2026-07-29.md](arch-EDISONMAIN2-EDControls-2026-07-29.md)
  (snapshot analýzy) a [arch-backlog.md](arch-backlog.md) (stav do 2026-09-17). Otevřené
  položky backlogu jsou převzaté sem pod novými ID, hotové se nepřenáší. Backlog se dál
  needituje.

### Mapa převzatých položek

| Backlog | Zde | Backlog | Zde | Backlog | Zde |
| --- | --- | --- | --- | --- | --- |
| R1 | A5 | R3 | A14 | R4 | A8 |
| R4b | A26 | R5 | A6 | R6b | A22 |
| R10 | A24 | R13a | A25 | R13d | A27 |
| N2 | A20 | N3 | A18 | N4 | A29 |
| N5 | A30 | N7 | A28 | | |

## Mapa repa (stav k 2026-09-20)

| Projekt | Typ | Reference | Poznámka |
| --- | --- | --- | --- |
| EDControls | lib | MLRest.dll, EDLicence.dll (`C:\Edison`) | volná DLL vedle exe, sdílená se zákaznickým TemplateDesigner |
| EDISONMAIN2 | WinExe | EDControls, LegacyMiniApps, EDISONAPI/EDLicence/MLRest.dll, WinForms | shell; source-link `EDUPDATE2/dotnet/ForcedStamp.cs` |
| EDISONMAIN2.LegacyMiniApps | lib | EDControls, MLRest.dll | dashboardy Dispečink |
| EDUPDATE2 | WinExe | žádná (záměr) | source-link `EDISONMAIN2/dotnet/Startup/VerzeInf.cs` |
| EDTesting | lib | EDControls | Caché test framework |
| TemplateDesigner | WinExe | EDControls | mimo scope, běží u zákazníků |

Composition root shellu: `App.xaml.cs` → `AppBootstrapper.Run()` (template method, `protected
virtual` kroky) → `InitializeServices` (`WindowNavigator`, `PopupDisplayer`, `EDServices.Initialize`
= statický singleton `Current`) → `BuildShellWindow` (`EdisonMainViewModel` se 13 ctor parametry,
`CompanySwitcher`, `SessionSwitcher`, `LogoutWatcher`, `MiniAppCatalog`, `ModuleShortcutsService`).
EDUPDATE2: `App.OnStartup` → `UpdaterArgs.Parse` → `UpdateWindow` → `UpdateEngine.Run` na `Task.Run`.

Externí hranice: Caché přes `IMLRest`/`MLRestAdapter` (wire `^||ML`); legacy WinForms host
`EDISONMAIN2API.exe` (`WindowNavigator.BuildLauncherArguments` ↔ `EdisonLaunchArgs` ↔
`FormToRunGetter` v repu EDISON); `podpora.m-line.cz` HTTPS (ErrorReporting); WebView2 widgety;
AvalonDock; release server HTTP (`Updater.GetReleaseNotice`); souborový systém `C:\Edison`
(`EDISONini.xml`, `VERZE.INF`, `EDISONCRC.bin`, `CliUp2.ini`).

Persistence: `LocalPrefs` JSON per-machine; `IUserPrefsService` (`CacheUserPrefsService`) a
`WindowStatePersistence` do Caché per-user × IDFa; `%APPDATA%\m-line\common` cross-process
(font, theme).

Největší soubory: `EdisonMainViewModel.cs` 1525 ř., `EDControlStyles.xaml` 1887, `EDList.xaml.cs`
729, `EDInputForm.xaml.cs` 604, `WindowNavigator.cs` 472, `AppBootstrapper.cs` 456,
`UpdateEngine.cs` 466.

---

## P1

### A1 · Mezi `AcquireSession` a `HandOffLoginWindow` není chybová hranice

- [x] **Stav:** vyřešeno 88c1d986 (2026-09-20) - hranice je širší než návrh: `Run()` obaluje celý
  `RunCore()` (kryje i výjimku z `VerifyLic`/`EstablishSession`, ne jen od `AcquireSession`),
  catch = `ReportStartupFailure` (dialog nad login oknem) + `Shutdown()`. Login okno se explicitně
  nezavírá: `Application.Shutdown` jeho `Closing` cancel ignoruje. Exit hook s logoutem přesunut
  do `WireSessionLogout` hned za `Accepted`. 8 testů (`AppBootstrapperTests`), Release ručně
  neověřeno (A10).
- **P1 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `AppBootstrapper.Run()` po `AcquireSession` (ACTIVITY zámek existuje, `CacheLocks=1`)
  volá `CheckForUpdate` a `BuildShellWindow` bez try/catch. `CheckForUpdate` chrání jen kopii
  a spawn updateru, ne serverový request `LoadSettings`. Výjimka propadne z `Application_Startup`
  do `DispatcherUnhandledException`, který ji označí za handled a aplikace běží dál se
  `ShutdownMode = OnExplicitShutdown` a login oknem v progress stavu, které každé zavření ruší.
- **Důkaz:**
  - `EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:142-143` `var settings = updater.LoadSettings(session.MLRest); var result = updater.Check(settings);` mimo try (ten začíná až na ř. 150).
  - `AppBootstrapper.cs:263` `_application.ShutdownMode = ShutdownMode.OnExplicitShutdown;`, restore až v `HandOffLoginWindow` ř. 130.
  - `EDISONMAIN2/dotnet/Windows/LoginWindow/LoginWindow.xaml.cs:47` `Closing += (_, e) => e.Cancel = _inProgress;`; ř. 161-165 `CloseAfterHandoff` je „jediná cesta ven z progress fáze".
  - `EDISONMAIN2/dotnet/App.xaml.cs:34-35` `new PopupDisplayer(owner).Error(e.Exception); e.Handled = true;`.
  - Exit hook s `SessionLogout.Logout` se registruje až v `BuildShellWindow` (`AppBootstrapper.cs:447-451`); cesty `Shutdown()` na ř. 97 (Refused) a 109 (update) ho nemají.
- **Dopad:** Release stanice: `ALVA.SETUP.Main.LoadSettings` hodí (síť, chybějící metoda po
  částečném deployi) → uživatel vidí chybový popup a pak login okno s textem „Kontroluji
  aktualizace…", Alt+F4 ani X nefungují, proces končí jen Task Managerem. Totéž pro výjimku
  kdekoli v `BuildShellWindow` (`ModuleTreeLoader`, `MiniAppCatalog.Build`). Cesta
  `UpdateRequired`/`Broken` ukončí aplikaci se založeným ACTIVITY zámkem bez logoutu (drží se
  do server idle timeoutu).
- **Návrh opravy:** v `Run()` obalit úsek od `AcquireSession` po `HandOffLoginWindow` do
  try/catch: `SessionLogout.Logout(session.MLRest)`, `_startupLoginWindow?.CloseAfterHandoff()`,
  chybový popup, `Shutdown()`. Exit hook s logoutem registrovat hned po `Accepted`, ne až
  v `BuildShellWindow` (pak zámek uvolní i cesta update).
- **Hotovo když:** `AppBootstrapperTests` s `CheckForUpdate` házejícím výjimku ověří shutdown
  i volání logoutu; ručně v Release s nedostupným `edisonPath` se aplikace čistě ukončí.
- **Omezení:** `SessionLogout` musí zůstat na klonu s `CacheLocks=0`; pořadí `SavePostLoginState`
  před update checkem (LastIdfa) zachovat. Regresní test na Release větev dnes nejde napsat (A10).

### A2 · Preference jedou na sdílené instanci MLRest bez klonu; selhání loadu plochy ji vyprázdní

- [x] **Stav:** vyřešeno (2026-09-20, commit s touto změnou) - (1) `CacheUserPrefsService` jede přes
  `IRestCallRunner` (klon per request), nové ctory `(IRestCallRunner|Func<IMLRest>, Action<Exception>?)`,
  starý `(IMLRest)` deleguje; **rozhodnuto nad rámec návrhu:** `Load` při selhání transportu hází
  (null = jen chybějící klíč), všichni konzumenti v repu (`EDList`, `EDFilter`, `ModulePreferencesService`,
  TD `TdPreferencesService`) už výjimku chytali; Save/Delete/poškozený JSON hlásí
  `ErrorReports.ReportBackground`. (2) `ModulePreferencesService.LoadShortcutsAsync` vrací null při
  selhání, `SessionData.Shortcuts` nullable; VM při null neseeduje reload a po selhání session loadu
  (i `ClearSessionState`) drží `_shortcutsPersistable=false`, dokud se blob nenačte. Composition
  rooty beze změny (starý ctor). `SessionDataLoader` preference dál sekvenčně (server je stejný).
  Testy: `UserPrefsTests` (klon, propagace, report), `FavoritesAndUsageTests` (reload/session
  selhání, obnova persistence), `SessionDataLoaderTests`. **Publish EDControls.dll** = chování
  Load v TD (výjimka místo null; TD ji chytá).
- **P1 · S-M** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20); backlog to znal jen jako poznámku (h) u R2
- **Problém:** `CacheUserPrefsService` volá `QueryOrThrow` přes `Task.Run` na injektované sdílené
  instanci `IMLRest`, ne na klonu `With()`. Shell na ni pouští souběžná fire-and-forget volání
  z několika nezávislých cest (save po změně plochy, reload po exitu legacy modulu, session
  load, save rozbalení), UI thread paralelně `WindowStatePersistence` a `EDUsageTelemetry.Flush`.
  Invariant „souběžné requesty jen na klonech" (root `CLAUDE.md`, `RestStatus` je per-instance)
  tu nikdo nedrží. Druhá vrstva: `ReloadShortcutsAsync` nerozlišuje „load selhal" od „prázdná
  plocha" a seeduje prázdno.
- **Důkaz:**
  - `EDControls/dotnet/UserPrefs/CacheUserPrefsService.cs:22-23` `LoadAsync(...) => Task.Run(() => LoadSync(scope, key))`, `:34` `_mlRest.QueryOrThrow(CacheClass, "Load", inVar)` na instanci z ctoru (`:18-20`), `:38-42` `catch (Exception ex) { Debug.WriteLine(...); return null; }` (stejně Save `:60-63`, Delete `:80-83`).
  - `EDISONMAIN2/dotnet/Startup/SessionFactory.cs:55-56` `services.MLRest = mlRest; services.UserPrefs = new CacheUserPrefsService(mlRest);`.
  - `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/SessionDataLoader.cs:62-64` komentář: „IUserPrefsService drží sdílenou instanci MLRest ... souběžně by si praly RestStatus", řeší jen svých pět loadů sekvenčně.
  - Souběžní volající mimo loader: `EdisonMainViewModel.cs:351` `WindowNavigator.LegacyModuleExited += () => _ = ReloadShortcutsAsync();` (fíruje per proces, po `CloseLegacyModules` při přepnutí firmy N× během `await RebuildSessionStateAsync`); `:342` `CollectionChanged` → `SyncFavorites` → `PersistShortcuts`; `:344` `PositionsChanged += PersistShortcuts`; `:604`, `:613`, `:640` `SaveExpansionAsync`; `:1172` `SaveUsageAsync`.
  - UI thread na téže instanci: `EDControls/dotnet/WindowStatePersistence.cs:71,99`, `EDControls/dotnet/EDViewModel/EDUsageTelemetry.cs:74`.
  - Degradace na prázdno: `Modules/ModulePreferencesService.cs:32-33` `LoadShortcutsAsync() => await LoadBlobAsync<...>(ShortcutsKey) ?? []`; `EdisonMainViewModel.cs:487-490` `var stored = await _modulePrefs.LoadShortcutsAsync(); ... _shortcuts.Seed(AllExecutables(), stored, ...)`.
  - TD: `TemplateDesigner/dotnet/.../TemplateDesignerViewModel.cs:890` a `App.xaml.cs:72` jsou dva adaptéry nad týmž `MLRest.Inner`; `RestStatus` je na Inner, race platí i tam.
  - Pravidlo: `EDControls/dotnet/RestCallRunner.cs:10-14` „Klon je nutný, ne kosmetika".
- **Dopad:** (a) Drop modulu ze stromu na buňku plochy (`MiniApps/DesktopMiniAppView.xaml.cs:76-80`
  `svc.Add` a hned `svc.PlaceAt`) = dva snapshoty blobu odejdou paralelně, pořadí doručení
  není dané → po restartu dlaždice může sedět na první volné buňce místo zvolené. (b) Zavření
  legacy modulu během jiného requestu na sdílené instanci (nebo obyčejný výpadek serveru
  v tu chvíli) → `LoadBlobAsync` vrátí null → `Seed(…, [])` vyprázdní plochu v paměti a shodí
  ★ všem modulům; první další toggle oblíbeného zapíše prázdný stav do Caché = **ztráta plochy
  uživatele**. (c) V TD tichá ztráta layoutu sloupců nebo pozice okna bez stopy (v Release
  `Debug.WriteLine` nic nezaznamená, ani `ErrorFallbackLog`).
- **Návrh opravy:** (1) EDControls, aditivně: ctor `CacheUserPrefsService(Func<IMLRest> rest)`
  (nebo `IRestCallRunner`), každý request na `rest().With()`; starý ctor deleguje `() => mlRest`.
  Selhání Save/Delete alespoň `ErrorReports.ReportBackground` (ne dialog). Composition rooty
  (shell `SessionFactory`, TD `App.xaml.cs`) na `() => services.MLRest`, čímž odpadne i nutnost
  znovu vytvářet službu při swapu session; `SessionDataLoader` pak může preference paralelizovat
  jako ostatní větve. (2) Shell: `ModulePreferencesService.LoadShortcutsAsync` vrací `null` při
  selhání (jako `LoadExpansionAsync`), `ReloadShortcutsAsync` při null nic neseeduje.
- **Hotovo když:** test po vzoru `RestCallRunnerTests` s thread-safe fake `IMLRest`: N souběžných
  `LoadAsync` → N volání `With()`, nula `QueryOrThrow` na sdílené instanci; VM test „reload po
  chybě loadu nechá `Items` beze změny a nezavolá `SaveAsync`".
- **Omezení:** `CacheUserPrefsService` je veřejné API EDControls (dávkovat publish s A22, A25);
  `IUserPrefsService`, `CacheClass` a wire `scope/key/data` beze změny; blob `Shortcuts` sdílí
  legacy agendy (`ALVA.EDControls.DesktopShortcuts`); sémantika null vs. [] je rozhodnutí
  vlastníka 🔸.

### A3 · `EDInputForm` volá Caché `Init` a registruje toolbar handlery při každém `Loaded`

- [x] **Stav:** vyřešeno (2026-09-20, commit s touto změnou) - `WireToolbarSearch` v konstruktoru
  (jednou), auto-Init z `Loaded` pod flagem `_autoInitDone` resetovaným setterem `CacheClass`;
  explicitní `Init()` běží vždy. TD workaround (`RefreshParamsTabInitParams`, čištění `CacheClass`)
  ponechán, je s guardem kompatibilní. Testy `EDInputFormViewTests` (dvojí Loaded, změna třídy,
  explicitní Init, search bez Loaded). Ruční ověření v TD (tab Parametry tam a zpět) zbývá.
- **P1 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `Loaded` fíruje při každém návratu do vizuálního stromu (přepnutí `TabItem`)
  a control při něm bezpodmínečně znovu volá synchronní Caché `Init` a znovu registruje
  handlery toolbaru. Není to jednorázová inicializace, ale lifecycle bez guardu; host to musí
  kompenzovat.
- **Důkaz:**
  - `EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs:114-118` `Loaded += (_, _) => { if (!string.IsNullOrEmpty(CacheClass)) Init(); WireToolbarSearch(); };`.
  - `:126-135` `WireToolbarSearch` dělá `dpd.AddValueChanged(Toolbar, ...)` a přepisuje `FindNext/FindPrevious/HelpCommand`; žádné `RemoveValueChanged`, žádný `Unloaded`.
  - Konzument to obchází: `TemplateDesigner/dotnet/Windows/MainWindow/MainWindow.xaml.cs:1150-1152` (čištění `CacheClass`, „ať se při příštím Loaded neprovede Init se stale InitParams") a `:1196-1200` („formulář se sám inicializuje na Loaded, tedy i při návratu na tab, a bere je odtud"). `ParamsTabForm` sedí v `TabItem` (`MainWindow.xaml:697-705`).
  - Kontrast správného vzoru: `EDControls/dotnet/EDFooter/EDFooter.xaml.cs:52-75` detach v `Unloaded`.
- **Dopad:** U zákazníka v TD každé přepnutí na tab Parametry = synchronní HTTP round-trip na UI
  vlákně (`EDInputFormController.cs:29`) a reset hodnot polí; TD to drží pohromadě jen ručním
  zrcadlením do `InitParams`. `AddValueChanged` se hromadí (N handlerů na `SearchText` po N
  přepnutích) a WPF `DependencyPropertyDescriptor.AddValueChanged` drží silnou referenci
  v globální tabulce, takže control neuvolní. Každý další host (`UserProfileWindow` spoléhá na
  auto-Init, `UserProfileWindow.xaml.cs:26`) musí past znát.
- **Návrh opravy:** Init jen jednou per `CacheClass` (flag resetovaný v setteru `CacheClass`,
  který už dnes nuluje controller, ř. 37-46); `WireToolbarSearch` do konstruktoru po
  `InitializeComponent()` nebo pod guard; explicitní `Init()` zůstává.
- **Hotovo když:** test v `EDInputFormViewTests` dvakrát vyvolá `Loaded` a assertne jeden
  `Query` na mocku a jeden handler; ručně v TD: přepnout tab Parametry tam a zpět, hodnota
  zůstane bez `RefreshParamsTabInitParams`.
- **Omezení:** veřejné API se nemění (aditivní změna chování); TD workaround může zůstat. Nikdo
  úmyslně na re-Init při návratu nespoléhá (TD ho aktivně potlačuje).

### A4 · Pruh podpory poslouchá jen `Idfa`; po přihlášení jiného uživatele nese widget starého

- [ ] **Stav:** otevřeno
- **P1 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** View si skládá URL widgetu samo z `EDServices.Current` a přenačítá ho jen na
  notifikaci `Idfa`; VM neví, že na tom view závisí, a při switch user `Idfa` nenotifikuje.
- **Důkaz:**
  - `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs:61-64` `if (e.PropertyName == nameof(EdisonMainViewModel.Idfa)) FillSupportStrip();`.
  - `:71-73` `var services = EDServices.Current; var ml = services.MLRest; var url = WidgetUrls.Edison2(ml?.Idfa ?? "", ml?.LoginName ?? "", services.UserEmail);`.
  - `EdisonMainViewModel.cs:1306-1311` po `SwitchUserResult.Success` notifikuje jen `UserName`, `FullName`, `Mlp`, `UserDisplay`; `ReplaceMiniAppSession(Idfa)` index firmy nemění.
- **Dopad:** „Přihlásit jiného uživatele" (stejná firma) nechá v pruhu URL s `user=`/`email=`
  původního uživatele; podpora cílí sdělení na špatného člověka, dokud se nepřepne firma nebo
  nerestartuje shell.
- **Návrh opravy:** VM vystaví `SupportWidgetUrl` (z `Rest.Idfa`, `Rest.LoginName`,
  `_services.UserEmail`) a notifikuje ho v `RaiseSessionPropertiesChanged` i po switch user;
  code-behind poslouchá tu jednu property a `EDServices.Current` z view zmizí.
- **Hotovo když:** VM test „`LoginAsAnotherUserAsync` Success → `PropertyChanged(SupportWidgetUrl)`
  a hodnota nese nový `LoginName`".
- **Omezení:** `WidgetUrls` sdílí `LoginWindow` (posílá jen IdFa), nechat jako čistou funkci.

### A5 · `EdisonMainViewModel` je God class shellu

- [ ] **Stav:** otevřeno (rozpracováno od 2026-08-17, příprava hotová)
- **P1 · L** · Závisí na: - · Blokuje: A20, A21, A28 (řešit v rámci extrakcí) · Původ: [backlog R1](arch-backlog.md#r1---edisonmainviewmodel-je-god-class-shellu)
- **Problém:** VM má 1525 řádků a ~45 commandů. Statické algoritmy jsou venku (`ModuleTreeState`
  jen jako `internal static` helpery, `SessionDataLoader`, `ModulePreferencesService`,
  `ModuleShortcutsService`, `TreeTab`, `ModuleLaunchabilityCheck`, `ChangelogPromptService`),
  instanční koordinace zůstává v jedné třídě.
- **Důkaz (co ve VM zůstává, `EdisonMainViewModel.cs`):** mini-app session controller (picker,
  výchozí layout, `EnsureDesktopOpen`, persist/restore layoutu, `ReplaceMiniAppSession`;
  ř. 106-214, 255-261); ctor s commandy a wiringem plochy (216-354); orchestrace rebuildu
  session a výchozího okna (356-475); koordinátor oblíbených/plochy/používaných
  (`ReloadShortcutsAsync`, `ApplyPersonalization`, `RebuildFavoritesTree`, `ToggleFavorite`,
  `SyncFavorites`, `PersistShortcuts`; 477-554, 1057-1162); persistence rozbalení (556-649);
  debounce hledání (651-699); firmy + `SwitchCompanyAsync` (864-995); launch + novinky
  (996-1055); zavření/exit/switch user (1260-1317); menu včetně přímého `Rest.QueryOrThrow`
  v `ResetForms` (1319-1401); labely spuštěných modulů (1403-1484); Ctrl+W (1491-1523).
  Testů na VM je 2000+ řádků v 5 souborech přes jeden `EdisonMainViewModelHarness`.
- **Dopad:** každá změna session workflow, plochy nebo mini-apps prochází jednou třídou;
  guardy `_suppressXxx` sdílí několik odpovědností; testy se dělí podle VM, ne podle chování.
- **Návrh opravy (pořadí platí, každý krok vlastní commit s přesunutými testy):**
  1. `ModuleTreeState` instanční část: kolekce stromů, filtrace, `ApplySearch`, persistence
     rozbalení (dnes `SuppressExpansionSave()` refcount scope; zachovat).
  2. `FavoritesCoordinator`: `_shortcuts` wiring, `SyncFavorites`, `RefreshFamilyFavoriteFlags`,
     `RebuildFavoritesTree`, `PersistShortcuts`. **Vyřeš u toho A28.**
  3. `MiniAppSessionController`: `AttachMiniAppHost`, `AttachTreeLayout`, `PersistMiniAppLayout`,
     `RestoreMiniAppLayout`, `ReplaceMiniAppSession`. **Vyřeš u toho A20.** Pozor: persist při
     zavření shellu jde přes `EventTrigger Closed` → `CloseAllCommand` v XAML, ze strany VM
     není vidět.
  4. Menu commandy (font, theme, `RunEdisonCopy`, `OpenWebUrl`, `ShowAppInfo`, chord) do vlastní
     partial třídy nebo `ShellMenuCommands`.
- **Hotovo když:** VM pod ~400 řádky a orchestruje; sdílené `_suppressXxx` flagy patří třídě,
  které se týkají; testy dělené podle nových tříd.
- **Omezení:** `ApplySessionData` musí zůstat **synchronní** (guardy neplatí přes `await`);
  po rozpadu poběží přes hranici dvou tříd a musí zůstat jeden nepřerušený průběh, jinak
  selhání loadu přestane nechávat předchozí stromy naživu. `_modulePrefs` je null do prvního
  `InitializeAsync` → sub-třídy chtějí lazy accessor. `RebuildSessionStateAsync` +
  `SessionDataLoader` zachovat jako jediný rozšiřovací bod session-scoped stavu.

## P2

### A6 · `EDServices.Current` prosakuje do infrastruktury a vypnul paralelizaci `EDISONMAIN2.Tests`

- [ ] **Stav:** otevřeno
- **P2 · M** · Závisí na: A7 (stejný soubor, dělat před) · Blokuje: - · Původ: [backlog R5](arch-backlog.md#r5---edservicescurrent-prosakuje-do-infrastruktury-a-vypnul-paralelizaci-testů)
- **Problém:** Singleton je záměr u controls (`EDList`, `EDInputForm`, `EDFilter` nesmí záviset
  na `Window.DataContext`), ale šíří se do infrastruktury a oken, a `EDISONMAIN2.Tests` proto
  běží sériově.
- **Důkaz:** `EDControls/dotnet/EDViewModel/WindowNavigator.cs:391,406,414-415` (šest členů
  ve statické `LaunchLegacy`); `EDControls/dotnet/WindowStatePersistence.cs:28`
  `MLRestProvider { get; set; } = () => EDServices.Current.MLRest`;
  `EDISONMAIN2/dotnet/Windows/UserProfileWindow/UserProfileWindow.xaml.cs:34,66,70,73` (okno
  i zapisuje `UserDirectory`); `EdisonMainWindow.xaml.cs:71`; miniaplikace bezparametrickými
  ctory (`DesktopMiniAppViewModel.cs:19` a další); `EDISONMAIN2.Tests/AssemblyInfo.cs:6`
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. `EDServices` má 12
  mutable properties.
- **Dopad:** `LaunchLegacy` a `WireUp` netestovatelné; testy shellu neběží paralelně; vedle
  toho existují další ambientní statiky (`SettingsLoader.Current`, `EDInputForm.PickColorImpl`,
  `ModuleTreeState.IsMlineDomainStation`; viz A10, A11, A21).
- **Návrh opravy:** singleton nerušit. (1) `WindowNavigator` (resp. `LegacyModuleHost` z A7)
  dostane `EDServices` nebo `Func<EDServices>` konstruktorem; composition root ho vytváří první,
  `EDServices` se plní settery, stačí předat instanci. (2) `WindowStatePersistence.Attach(window,
  formId, IMLRest?)` jako aditivní přetížení. (3) Paralelizaci `EDISONMAIN2.Tests` zapnout,
  `[Collection]` jen na harnessech, které `EDServices.Initialize` reálně volají.
- **Hotovo když:** `EDServices.Current` čtou už jen controls, `RelayCommand` fallback a
  bezparametrické ctory VM; `EDISONMAIN2.Tests` běží paralelně.
- **Omezení:** `EDServices` a `WindowNavigator` jsou veřejné API EDControls, změny aditivní.

### A7 · `WindowNavigator` je tři třídy v jedné, polovina stavu je statická

- [ ] **Stav:** otevřeno
- **P2 · M** · Závisí na: - · Blokuje: A6 · Původ: nový (2026-09-20)
- **Problém:** WPF navigace (Show/ShowDialog/WireUp), launch legacy procesů (argv, resolve cest,
  direct vs. host) a lifecycle child procesů (tracking, dedup, exit watch, hlášení pádu, dim
  shellu, CloseAll) žijí v jedné 472řádkové třídě; launch je statická metoda sahající na
  singleton, telemetrii i error reporting.
- **Důkaz:**
  - `EDControls/dotnet/EDViewModel/WindowNavigator.cs:33` `private readonly List<LegacyProcessEntry> _legacyProcesses` (instance) vs `:39` `private static readonly ConditionalWeakTable<Process, ExitWatch> _exitWatches` (static): dvě instance by sdílely exit watch, ale ne seznam procesů.
  - `:388` `private static Process? LaunchLegacy(...)` čte `EDServices.Current` (`:391,:406,:414-415`), volá `EDUsageTelemetry.Track("EDISON2.LegacyLaunch", ...)` (`:395`) a přes `TrackExit` → `ErrorReports.ReportLegacyExit` (`:82`).
  - `:92-104` WPF část je 13 řádků; `:106-242` a `:253-354` legacy část ~330 řádků. `IWindowNavigator.cs:12-67`: 2 členy WPF, 6 legacy.
  - `:211` a `:467` owner = `Application.Current.MainWindow` (během startu shellu login okno).
  - TD `TemplateDesigner/dotnet/App.xaml.cs:40` `new WindowNavigator()` dostává celý legacy host, který nepoužije.
- **Dopad:** testy `LaunchLegacy`/`WireDialogBehavior` nejde napsat bez `Process.Start`
  a `EDServices.Current` (dnes testované jen čisté statické helpery); A6 by injektovalo
  `EDServices` do 472 řádků.
- **Návrh opravy:** vyseknout `LegacyModuleHost` (launch + tracking + close) s vlastním
  rozhraním a injekcemi (`Func<EDServices>`, `Action<string,string,int> reportExit`,
  `Action<string,string> trackUsage`, factory procesu); `_exitWatches` instanční.
  `WindowNavigator` zůstane fasáda, `IWindowNavigator` beze změny (deleguje).
- **Hotovo když:** `WindowNavigator.cs` pod ~150 řádků bez `Process`; `LegacyModuleHost` má
  test s fake process factory na dedup, `KilledByShell` a `ReportLegacyExit`; TD referencuje
  jen `IWindowNavigator`.
- **Omezení:** veřejné API (`CloseAll`, `TryCloseAll`, `GetActiveModules`, `ResolveExecutablePath`,
  `EdisonDeployRoot`) ponechat jako delegující fasádu; launch kontrakt se nedotýká.

### A8 · EDControls zná deployment a branding EDISONMAIN2 natvrdo

- [ ] **Stav:** otevřeno, částečně (`FormIdPrefix` hotov)
- **P2 · M** · Závisí na: - · Blokuje: - · Původ: [backlog R4](arch-backlog.md#r4---edcontrols-natvrdo-zná-deployment-a-branding-edisonmain2)
- **Hotovo mezitím:** `EDServices.FormIdPrefix` (`EDControls/dotnet/EDViewModel/EDServices.cs:60`,
  TD nastavuje `"TD"` v `App.xaml.cs:42`, čte `EDViewModel.cs:146`).
- **Důkaz zbytku:** `WindowNavigator.cs:22` `public const string EdisonDeployRoot = @"C:\Edison"`
  (jediný `public const` v EDControls, který konzument reálně čte:
  `EDISONMAIN2/dotnet/Windows/ChangelogWindow/InfChangelogSource.cs:31`, tedy se inlinuje
  do starého exe); `:26` `LauncherExe = "EDISONMAIN2API.exe"`; `:395`
  `"EDISON2.LegacyLaunch"` ignoruje `FormIdPrefix`; `:449` literál `EDISONMAIN2` v interpolaci
  místo `EdisonLaunchArgs.LaunchMarker`; `EDErrorPopup.xaml.cs:54` název souboru
  `EDISON2-chyba-*`.
- **Návrh opravy:** statická konfigurace nastavovaná v composition rootu (`EDAppIdentity`
  nebo rozšíření `EDServices`: `DeployRoot`, `LegacyLauncherExe`, `LaunchSentinel`) s defaulty
  dnešních hodnot; `EdisonDeployRoot` převést na `static readonly`; `BuildLauncherArguments`
  používat `EdisonLaunchArgs.LaunchMarker`; telemetrie a název souboru přes `FormIdPrefix`.
- **Hotovo když:** v EDControls není hardcoded `C:\Edison`, `EDISONMAIN2API.exe`, sentinel ani
  `"EDISON2"` mimo defaulty konfigurace.
- **Omezení:** aditivní změna API; launch kontrakt beze změny.

### A9 · Session stav v `EDServices` má několik zapisovatelů; licence a `LicenceLoader` putují dvěma kanály

- [ ] **Stav:** otevřeno
- **P2 · M** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20), sloučeno ze dvou oblastí
- **Problém:** Ambientní singleton plní bootstrap (`SessionFactory`), VM (`MLRest` znovu,
  `ActiveLicence`, `AccessTable`) i code-behind okna profilu (`UserDirectory`). `LicenceLoader`
  jde zároveň přes `EDServices` a přes `SessionContext`; aktivní licence jde návratem
  z `AcquireSession` do VM, který ji do singletonu zapíše až v rebuildu.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/SessionFactory.cs:51-60` plní `UserDirectory`,
  `UserEmail`, `MLRest`, `UserPrefs`, `LicenceLoader` a týž loader vrací v `SessionContext`;
  `AppBootstrapper.cs:348` používá `session.LicenceLoader`, `CompanySwitcher.cs:84`
  `services.LicenceLoader!`; `EdisonMainViewModel.cs:338` `_services.MLRest = mlRest;`
  (redundantní; harness `EDISONMAIN2.Tests/EdisonMainViewModelHarness.cs:93-94` musí MLRest do
  services dát ještě před ctorem, protože ho `CompanySwitcher` čte lazily); `:374`
  `_services.ActiveLicence = licence;` až v `RebuildSessionStateAsync`; `:399`
  `_services.AccessTable = data.AccessTable;`; `UserProfileWindow.xaml.cs:73`
  `EDServices.Current.UserDirectory = dir;`.
- **Dopad:** mezi `Accepted` (switch, `:923`) a `:374` ukazuje `ActiveLicence` na starou firmu,
  zatímco `MLRest.Idfa` už na novou; `Closing` handlery, `BuildLauncherArguments` a `HasAccess`
  čtou nekonzistentní pár. Nový session-scoped údaj = ad hoc rozhodnutí, kdo ho zapisuje.
  Testovací bootstrapper `EstablishSession` `services.LicenceLoader` nenastavuje, testy
  dvojkolejnost nepokryjí.
- **Návrh opravy:** jeden zapisovatel: `SessionFactory`, `LicenceVerifier`/`CompanySwitcher`
  zapíší licenci do `EDServices` hned po `Accepted`, `SessionContext` jen vrací, VM čte;
  smazat ř. 338. Alternativa: `EDServices.LicenceLoader/ActiveLicence` zrušit a předávat kontext
  (breaking, publish).
- **Hotovo když:** `CompanySwitcherTests` asertují `services.ActiveLicence` po `Accepted`;
  harness nepotřebuje předplnit `MLRest`; `grep LicenceLoader` ukáže jednoho zapisovatele.
- **Omezení:** `EDServices` je API EDControls (TD ho sdílí); `AccessLoader` běží až
  v `SessionDataLoader` na klonu, access buď přejde do switcheru, nebo zůstane u VM s explicitním
  komentářem.

### A10 · `#if DEBUG` větví běhové chování startu na šesti místech

- [ ] **Stav:** otevřeno
- **P2 · M** · Závisí na: - · Blokuje: regresní test pro A1, A11, A35 · Původ: nový (2026-09-20)
- **Problém:** Debug a Release jsou dva různé programy: hardcoded login, deterministický GUID,
  prázdná `EdisonVersion`, `DevVersion`, neukládané `LastLoginName`, dev miniaplikace. Release
  cesta (login dialog, progress, handoff) se ve vývoji ani v testech nikdy nespustí.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:259` (`TryLogin` celý DEBUG/else),
  `:354` (`SavePostLoginState`), `:410` (`DevSampleMiniApps`); `SessionFactory.cs:32,41`;
  `VerzeInf.cs:25`; `EDISONMAIN2.Tests/AppBootstrapperTests.cs:403` `#if DEBUG`;
  `_startupLoginWindow` je v Debug vždy null (`AppBootstrapper.cs:175` komentář „= null kvůli
  CS0649 v DEBUG").
- **Dopad:** A1 leží přesně v Release-only větvi a regresní test na ni nejde napsat bez Release
  buildu; každá změna login dialogu / progress fáze / `HandOffLoginWindow` se ověří jen
  publishem do `C:\Edison\`.
- **Návrh opravy:** jeden runtime objekt `StartupOptions` (`DevLogin?`, `DeterministicGuid`,
  `SendVersion`, `DevMiniApps`, …) sestavený na jednom místě (`App.xaml.cs` podle DEBUG /
  env proměnné), předávaný bootstrapperu a `SessionFactory`; `#if` zůstane jen tam, kde se
  options tvoří. Release cesta pak jde spustit i v dev (např. `EDISON2_DEVLOGIN=0`).
- **Hotovo když:** `grep '#if DEBUG' EDISONMAIN2/dotnet/Startup` vrátí jen místo tvorby options;
  `AppBootstrapperTests` bez `#if`.
- **Omezení:** DEBUG GUID a prázdná verze jsou dokumentované pasti (ACTIVITY zámek na sdíleném
  dev serveru, version gate), sémantiku zachovat, jen přesunout rozhodnutí do runtime.

### A11 · Setup wizard v produkci předvyplní dev server M-line

- [x] **Stav:** vyřešeno (2026-09-21, commit s touto změnou) - dev host jen pod
  `SettingsLoader.UseDevDefaults` (DEBUG true, Release false, settable pro testy), REST port 57770
  zůstává default vždy; `ServerSettings.Host` default prázdný. Oddělení `ServerSettings` od statického
  `Current` (druhá část návrhu) záměrně ne - bez A10 by to byl jen přesun. Testy `SettingsLoaderTests`
  (obě větve bez `#if`).
- **P2 · S** · Závisí na: A10 (dev podmínka) · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `SettingsLoader.Load()` doplní prázdný host/port „dev defaulty" a bootstrapper
  ho volá právě proto, aby wizard měl co předvyplnit. Na stanici zákazníka bez `EDISONini.xml`
  se ukáže interní IP dev serveru.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/SettingsLoader.cs:137-138` `DefaultHost = "192.168.16.1";
  DefaultPort = 57770;`, `:39-40` doplnění v `Load`; `AppBootstrapper.cs:216`
  `SettingsLoader.Load();   // naplní Current dev defaulty pro předvyplnění polí`;
  `Windows/SetupWindow/SetupWindow.xaml.cs:33` čte `SettingsLoader.Current`.
- **Dopad:** kontrakt `EDISONini.xml` sdílený s legacy: uživatel klikne „Test připojení" proti
  cizí síti, nebo přes VPN partnera se připojí na dev server a zapíše ho do souboru, který
  přebírají legacy aplikace i EDUPDATE.
- **Návrh opravy:** defaulty jen pod dev podmínkou (A10), jinak prázdný host (REST port 57770
  jako rozumný default může zůstat). Zároveň oddělit neměnný obraz souboru (`ServerSettings`)
  od čtečky/zapisovače bez statického `Current`; `SetupWindow` dostane settings konstruktorem,
  odpadne implicitní pořadí `Load()` před `new SetupWindow()`.
- **Hotovo když:** `SettingsLoaderTests`/`SetupWizardLogicTests`: bez souboru → Host `""`.
- **Omezení:** zápis musí zůstat v legacy paritě (`initialized`/`forceChange`, zachování cizích
  uzlů); dev kopie v repu se nepublishuje (`CopyToPublishDirectory="Never"`).

### A12 · Launch kontrakt shellu nemá jediný zdroj pravdy pro pozice polí

- [ ] **Stav:** otevřeno
- **P2 · M** (escapování s hostem L) · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Pozice 18 polí jsou literály ve třech souborech ve dvou repech (producent, WPF
  konzument, WinForms konzument + `ShortcutRunner`); synchronizaci hlídá jeden test počítající
  segmenty. Hodnoty z DB (jméno, adresář) jdou bez escapování a posun jediného pole přehodí
  identitu session.
- **Důkaz:**
  - Producent: `EDControls/dotnet/EDViewModel/WindowNavigator.cs:449` jeden interpolovaný string 18 polí s literálem `EDISONMAIN2`.
  - Konzument TD: `EDControls/dotnet/EdisonLaunchArgs.cs:35-50` parsuje `args[1..10]`, pole `[11]-[17]` nezná (`:8-10`).
  - Konzument host: `c:\MLineRepos\EDISON\EDISONMAIN2API\FormToRunGetter.cs:47-71` `args[1]`..`args[17]`, `ShortcutRunner.cs:50-51` `args[13]`.
  - Jediný strážce: `EDControls.Tests/WindowNavigatorTests.cs:187` `Assert.Equal(18, args.Split(" \"").Length);`; `:142` `BuildLauncherArguments_EmbeddedQuote_NotEscaped` dokumentuje, že uvozovka projde neescapovaná.
  - `WindowNavigator.cs:441-442,452` řeší jen koncové zpětné lomítko (`Dir()`).
- **Dopad:** `FullName` ze serveru s `"` → host načte `Mlp`, `formName`, `shellGuid` z posunutých
  pozic, modul se buď nespustí, nebo pojede s cizím GUID a `CacheLocks=1`. Přidání pole =
  editace 3 souborů + 2 doc komentářů bez kompilátorové vazby; když TD bude potřebovat
  `isAdmin`/`userEmail`, musí `EdisonLaunchArgs` doparsovat podle jiného souboru.
- **Návrh opravy:** (1) `EdisonLaunchArgs` = jediný kodek: pojmenované indexy (`internal static
  class Field { const int Server = 1; ... }`), `Format(...)` producent i `TryParse` konzument,
  record aditivně o `[11]-[17]`; `BuildLauncherArguments` deleguje. (2) Sdílená fixture: jeden
  vzorový argv string commitnutý do testů obou rep. (3) 🔸 Politika escapování: buď producent
  odmítne/nahradí `"` v `FullName`/`UserDirectory` (jednostranné), nebo se pole přenáší
  kódovaná jako `[13]` (změna hosta), rozhodnutí vlastníka hosta.
- **Hotovo když:** `grep args\[` v `EdisonLaunchArgs.cs`/`WindowNavigator.cs` dá nulu mimo
  tabulku indexů; round-trip test `Format` → `CommandLineToArgv` → `TryParse` s uvozovkou
  ve `FullName`.
- **Omezení:** kontrakt je append-only a zrcadlí `FormToRunGetter` v repu EDISON; bod (3) bez
  vlastníka hosta nedělat; `EdisonLaunchArgs` record je public API TD (aditivní OK).

### A13 · Kontrakty k legacy Caché endpointům: pozicní parsování ~10 metod, contract testy pro 2

- [ ] **Stav:** částečně (2026-09-23) - integrační testy šva (`EDISON2.IntegrationTests`, skutečné MLRest
  proti dev serveru) pokrývají `GetEdisonAccess` (index 1 = apId), `ALVA.SETUP.Main.LoadSettings`
  (povinné klíče), `GetUserDefaultForm`, `maSeUkoncit` a `LoginAllowed`. Zbývá `Login`, `ChangePassword`,
  `VerifyLic`, `LoadUser/SaveUser`, `ALVA.EDISON.Main.LoadSettings`, `LoadDatabaseList`, `LockClearExt`
  (část potřebuje testovací účet na serveru) a skip-as-pass v Caché contract testech.
- **P2 · M** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Shell závisí na positional formátu `Login` (indexy 1,2,3,4,5,7,10),
  `ChangePassword`, `VerifyLic`, `GetEdisonAccess`, `LoadUser/SaveUser`, `ALVA.EDISON.Main.LoadSettings`,
  `ALVA.SETUP.Main.LoadSettings` (včetně překlepu `NucenyUpdatClentu`), `LoadDatabaseList`,
  `Get/SetUserDefaultForm`. Contract test na Caché straně existuje jen pro `maSeUkoncit`
  a `LogUsageBatch`, oba při chybějící třídě hlásí pass. Guard `SessionLockTest` volá
  `LockClear`, shell posílá `LockClearExt`.
- **Důkaz:** `EDISONMAIN2/dotnet/Windows/LoginWindow/UserAuthenticator.cs:45-53` (pozice 10 =
  UserName, 7 = Email); `Startup/Updater.cs:78` `case "NucenyUpdatClentu"`;
  `EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/Tests/LogoutContractTest.cls:20` `if '..HasSystemMain() { do $$$AssertTrue(1, "... skip") quit }`
  (totéž `TelemetryContractTest.cls:29,55`); `SessionLockTest.cls:54,67`
  `##class(ALWA.SYS.Lock).LockClear(..#TESTGUID)` vs `Startup/SessionLogout.cs:40`
  `clone.QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", ...)`.
- **Dopad:** změna v repu EDISON (přeuspořádání polí `Login`, přejmenování klíče) se projeví
  až u uživatele; drift `LockClear` vs `LockClearExt` by guard nezachytil; skip-as-pass
  maskuje, že se kontrakt na daném serveru vůbec neověřil.
- **Návrh opravy:** doplnit `Tests/LoginContractTest.cls` (formát `Login` pro testovacího
  uživatele, `GetEdisonAccess` index 1 = apId); `SessionLockTest` rozšířit o `LockClearExt`
  přes `^||ML("IN",1)=guid`; skip realizovat explicitním logem místo `AssertTrue(1)`.
- **Hotovo když:** `pwsh -File tools/run-cache-tests.ps1 -Package ALVA.EDISONMAIN2.Tests` hlásí
  nové testy; drift test na `LockClearExt`.
- **Omezení:** endpointy žijí v repu EDISON, shell je nesmí měnit; testy jen ověřují.

### A14 · `EDList` nemá controller seam

- [ ] **Stav:** otevřeno, částečně (wire vrstva vytažena)
- **P2 · M** · Závisí na: - · Blokuje: A16, A17, A27 (dělat spolu) · Původ: [backlog R3](arch-backlog.md#r3---edlist-nemá-controller-seam)
- **Stav v kódu:** `EDListParsing` (commit d8a75403), `EDListSort`, `EDListFilter`, `ColumnLayout`
  jsou vytažené; `EDList.xaml.cs` má 729 řádků, volá MLRest přímo (`:269,:295,:313`), stav
  (`_sortSpecs`, `_checkAnchor`, layout persist, checked state) žije jen ve view.
- **Návrh opravy:** `EDListController(IMLRest, string cacheClass)` po vzoru `EDInputFormController`:
  `InitializeList()` → sloupce, `LoadList(filter)` → řádky (formátování, sort keys),
  `LoadLayoutAsync`/`SaveLayoutAsync` nad `IUserPrefsService` a `ColumnLayout` DTO. V code-behind
  zůstane mapování na `DataGrid.Columns` a event wiring. Hlavní přínos: testy na lifecycle
  `LoadList` (reset checked state, unsubscribe starých řádků, re-apply search a layoutu), které
  `EDList/CLAUDE.md` dnes vypisuje jako pasti a nic je nehlídá.
- **Hotovo když:** instanční logika testovatelná proti `IMLRest` mocku; code-behind pod ~400 ř.
- **Omezení:** veřejné API `EDList` aditivně.

### A15 · `EDInputForm` je celý synchronní na UI vlákně, `EDList` má async cestu

- [ ] **Stav:** otevřeno
- **P2 · M** · Závisí na: A3 · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `EDList` má `InitializeListAsync/LoadListAsync` s gate a generation guardem,
  `EDInputForm` volá Caché výhradně synchronně včetně validace na každou změnu pole.
- **Důkaz:** `EDControls/dotnet/EDInputForm/EDInputFormController.cs:29` Init, `:47` Save,
  `:70` Validate, žádný `Task`; `EDInputForm.xaml.cs:600-601` `if (!field.ValidateOnChange)
  return; Dispatcher.BeginInvoke(new Action(() => Controller.Validate(field.Key)));` (odložené,
  stále blokující HTTP na UI). Kontrast `EDList.xaml.cs:276-282,306-317,324-332` (semafor +
  `_loadGeneration`) a `EDList/CLAUDE.md:9-12` „synchronní varianty zmrazí celou aplikaci".
- **Dopad:** TD u zákazníka na pomalé lince zamrzá při každé změně pole s `ValidateOnChange`;
  dvě odložené `Validate` po sobě sdílejí `^||ML` na téže instanci (bez klonu).
- **Návrh opravy:** aditivně `InitAsync/ValidateAsync/SaveAsync` na controlleru přes klon
  (`RestCallRunner` vzor) + generation guard jako v EDListu; view přepne `Field_PropertyChanged`
  na async variantu, sync API zůstane pro hosty.
- **Hotovo když:** `EDInputFormControllerTests` s pomalým mockem: dva `ValidateAsync` za sebou,
  aplikuje se jen odpověď posledního.
- **Omezení:** veřejné API jen přidává; 🔸 kdy zahodit odpověď při rozepsaném editu je návrhové
  rozhodnutí.

### A16 · Pořadí `InitializeList → LoadList` v `EDList` nic nehlídá

- [ ] **Stav:** otevřeno
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `LoadList`/`LoadListAsync`/`SetData` bez předchozích sloupců projde bez chyby,
  řádky se naplní do `Rows`, ale `DataGrid.Columns` zůstane prázdný.
- **Důkaz:** `EDControls/dotnet/EDList/EDList.xaml.cs:292-296` `LoadList` → `ApplyRows`; `:356`
  `ParseRows(output, _columns)` s `_columns == null`; `EDListParsing.cs:98-104` tolerantně vrací
  raw; `RebuildColumns` (`:374-388`) se volá jen z `ApplyColumns`, `SetData`,
  `OnSelectionModeChanged`; `EnsureConfigured` (`:555-560`) kontroluje jen MLRest + `CacheClass`.
  Vedlejší efekt téže třídy stavu: `OnSelectionModeChanged` → `RebuildColumns` nenuluje
  `_sortSpecs` ani `SortDirection` (`:503-535`).
- **Dopad:** nový host zavolá jen `LoadListAsync` a dostane prázdný seznam bez výjimky; ladí se
  to na serveru, kde je všechno v pořádku. `EDListLoadTests` to nechytí, kontrolují `Rows`.
- **Návrh opravy:** v `ApplyRows` při `_columns is null` vyhodit `InvalidOperationException`
  s hláškou „zavolej InitializeList/SetData" (všichni tři dnešní hosté inicializují správně:
  TD pickery, `EDListTestWindow`, `DashboardListSource`).
- **Hotovo když:** test `LoadList_WithoutInitialize_Throws`.
- **Omezení:** výjimka místo ticha je breaking jen pro chybné hosty, v repu žádný.

### A17 · Wire slovník `EDList` nemá sdílenou definici ani parity test

- [ ] **Stav:** otevřeno
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Pro `EDInputForm` existuje `EDInputForm.inc` + `FormTest.TestWireConstantsMatchCSharp`
  + C# `[InlineData]`; pro `EDList` je jediným kontraktem komentář v `Column.cls` a `switch` v C#,
  který neznámý typ tiše mapuje na TEXT.
- **Důkaz:** `EDControls/CACHE/ALVA/EDControls/EDList/Column.cls:9-10` `/// Datový typ: TEXT |
  NUMBER | DATE | DATETIME | BOOL | IMAGE | COLOR | HHMM | CODEBOOK` + `Property type As %String`
  bez validace; `List.cls:16` volný string; `EDControls/dotnet/EDList/EDListParsing.cs:48-59`
  `_ => ListColumnType.Text`; C# testy pinují jen TEXT/NUMBER/BOOL/DATE/HHMM/COLOR
  (`EDControls.Tests/EDListFormattingTests.cs:101-127`); Caché `ListTest.cls` jen echo stringů
  (`:8-23`); formáty hodnot (NUMBER s tečkou, DATE ISO, BOOL 1/0, HHMM int, COLOR `R;G;B`) žijí
  jen v `EDList/CLAUDE.md:35-37`.
- **Dopad:** překlep na serveru = sloupec jako raw ISO text s lexikografickým řazením bez jediné
  chyby; nový typ přidaný na jedné straně se na druhé neprojeví.
- **Návrh opravy:** `CACHE/EDList.inc` s `$$$ListColumnType*` konstantami; `List.AddColumn`
  validuje proti nim (throw); Caché `ListTest` parity test podle vzoru `FormTest:311-333`;
  C# `[Theory]` přes všechny hodnoty `ListColumnType` včetně `ParseType` fallbacku.
- **Hotovo když:** oba testy zelené, `MockList.cls` používá makra.
- **Omezení:** hodnoty literálů jsou wire kontrakt, nemění se (aditivní `.inc`).

### A18 · Login workflow rozprostřený do čtyř míst, bez VM

- [ ] **Stav:** otevřeno, částečně (`LoginWindowLogic` vytažen)
- **P2 · M** · Závisí na: A10 · Blokuje: - · Původ: [backlog N3](arch-backlog.md#n3---login-workflow-rozprostřený-do-čtyř-míst-bez-vm)
- **Stav v kódu:** `LoginWindowLogic.cs` (31 ř., rozhodnutí fokus/zapomenout heslo) vytažen;
  `LoginWindow.xaml.cs:105` provádí autentizaci, `:123-127` politiku zapamatovaného hesla,
  `:139-165` progress state machine startupu (`EnterProgressState`, `ShowProgress`,
  `CloseAfterHandoff`); bootstrapper ho řídí zvenčí přes `DispatcherFrame`
  (`AppBootstrapper.cs:270-274`) a `_startupLoginWindow` čte 5 míst (`:87,:105,:113,:128,:313`).
- **Dopad:** jediný netriviální workflow shellu bez VM a bez testu; A1 (chybová hranice)
  a A10 (Release cesta) na něj přímo navazují.
- **Návrh opravy:** `LoginViewModel` s `LoginAsync`, `RememberPassword`, progress stav; okno
  bindingy; bootstrapper komunikuje s VM, ne s oknem. Detail a historie: backlog N3.
- **Omezení:** login okno zůstává jako progress podklad do `ContentRendered` shellu
  (`EDISONMAIN2/CLAUDE.md`); `HandOffLoginWindow` pořadí MainWindow → ShutdownMode.

### A19 · `UserProfileWindow` opakuje vzor A18: workflow v code-behind, VM dostává callback z okna

- [ ] **Stav:** otevřeno
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Okno drží load, validaci existence složky, persist a mutaci `EDServices`; VM je
  jen most a dostává `SaveRequested` delegát od okna (obrácená závislost). Testovatelná logika
  je `internal static` na třídě `Window`.
- **Důkaz:** `EDISONMAIN2/dotnet/Windows/UserProfileWindow/UserProfileWindow.xaml.cs:23`
  `_vm.SaveRequested = SaveForm;`, `:34` `var ml = EDServices.Current.MLRest;`, `:70-73`
  `UserProfileService.SaveExportDir(EDServices.Current.RequireMLRest, use, dir); EDServices.Current.UserDirectory = dir;`,
  `:78` `internal static string? ValidateExportDir(...)`; `UserProfileViewModel.cs:28`
  `public Func<bool>? SaveRequested { get; set; }`.
- **Dopad:** save workflow (server validace → klient validace → persist → propagace do session)
  nejde otestovat bez WPF a bez singletonu; přidání pole = zásah do code-behind.
- **Návrh opravy:** `OnFormInitialized`/`SaveForm` logiku do VM (má `Rest`, `PopupDisplayer`,
  `EDServices` přes `EDViewModel`), okno jen mapuje `Field` hodnoty nad `TheForm.Items`;
  `ValidateExportDir` do VM.
- **Hotovo když:** `UserProfileViewModelTests` s mock `IMLRest` pokrývají happy path a „adresář
  neexistuje".
- **Omezení:** `Main.LoadUser/SaveUser` se nesmí volat ze schema třídy (kolize `^||ML`,
  `EDISONMAIN2/CLAUDE.md`); EDInputForm API (`SchemaLoaded`, `Save()`) beze změny.

### A20 · Persistence layoutu mini-apps má tři vstupy; běžné zavření persistuje dvakrát

- [ ] **Stav:** otevřeno, částečně posunuto
- **P2 · S** · Závisí na: - · Blokuje: A5 krok `MiniAppSessionController` · Původ: [backlog N2](arch-backlog.md#n2---persistence-layoutu-miniaplikací-má-tři-vstupy-a-guard-jen-v-jednom)
- **Stav v kódu:** guard `_sessionSwapInProgress` je dnes uvnitř `PersistMiniAppLayout`
  (`EdisonMainViewModel.cs:164`), takže kryje všechny vstupy. Vstupy zůstávají tři:
  `Closing` → `ConfirmShellClose` (`:1273`), XAML `EventTrigger Closed` → `CloseAllCommand`
  (`:255`, `EdisonMainWindow.xaml:21`), `SwitchCompanyAsync`/`ReplaceMiniAppSession` (`:940`, `:211`);
  při normálním zavření se layout ukládá dvakrát (Closing i Closed).
- **Návrh opravy:** sjednotit do jedné cesty s jedním guardem **před** extrakcí controlleru,
  jinak extrakce tiše ztratí jednu větev; `_sessionSwapInProgress` jako `BeginSessionSwap()`
  vracející `IDisposable`.
- **Hotovo když:** test „zavření shellu uloží layout právě jednou".
- **Omezení:** persist při zavření shellu musí přežít i cestu přes `EventTrigger Closed`.

### A21 · Strom modulů závisí na dvou ambientních faktech stanice bez injekce

- [ ] **Stav:** otevřeno
- **P2 · S** · Závisí na: A5 (ideálně součást extrakce) · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `FillFilteredTree` míchá do datového modelu výsledky `File.Exists` a DNS/registry
  detekce domény; ani jedno nejde přes ctor VM podstrčit.
- **Důkaz:** `EdisonMainViewModel.cs:708-712` `ModuleTreeState.TryFilter(module,
  _services.ActiveLicence, _services.AccessTable, ModuleTreeState.IsMlineDomainStation)` a
  `ModuleLaunchabilityCheck.Mark(filtered)`; `Modules/ModuleTreeState.cs:16` `internal static
  readonly bool IsMlineDomainStation = MlineDomainDetector.IsDomainMline();` (type init, nastavení
  `DomainNameProvider` v testu po prvním dotyku nic nezmění); `Modules/ModuleLaunchabilityCheck.cs:92-93`
  `File.Exists(WindowNavigator.ResolveExecutablePath(exe.ExecutablePath))` (fallback `C:\Edison`);
  důkaz z testů `EDISONMAIN2.Tests/EdisonMainViewModelTests.cs:46-48` „StartsWith -
  ModuleLaunchabilityCheck připojuje k názvu suffix".
- **Dopad:** test s `mlineOnly` modulem projde na stroji v doméně m-line a spadne jinde;
  test na přesný `DisplayedName` nejde napsat; `File.Exists` pro každý modul běží synchronně
  na UI vlákně při každém rebuildu (login, přepnutí firmy, refresh licence).
- **Návrh opravy:** `FillFilteredTree` dostane strategii (`Func<IModule, IModule> decorate` +
  `bool isMlineDomain`) přes ctor s produkčním defaultem v `AppBootstrapper`;
  `IsMlineDomainStation` z `static readonly` na parametr.
- **Hotovo když:** harness předá identitu a test může asertovat přesný název.
- **Omezení:** `MlineDomainDetector.Suffix` a barvy stub/broken jsou vizuální parita s legacy;
  ctor VM má 13 parametrů, další přidávat spíš jako součást A5.

### A22 · Dokončit odstranění `EDInputForm.Initialized`

- [ ] **Stav:** otevřeno
- **P2 · S** · Závisí na: koordinovaný publish obou aplikací · Blokuje: - · Původ: [backlog R6b](arch-backlog.md#r6b---dokončit-odstranění-edinputforminitialized)
- **Stav v kódu:** `EDInputForm.xaml.cs:265-267,288-290` `[Obsolete]` a fíruje; žádný konzument
  mimo EDControls (grep `\.Initialized` v TD i shellu prázdný). Brzdí už jen koordinovaný
  publish; dávkovat s A2 a A25.
- **Hotovo když:** event je pryč, oba exe publikované ze stejného commitu.

### A23 · Mechanismus persistence preferencí zkopírovaný v `EDList` a `EDFilter`

- [ ] **Stav:** otevřeno
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** identický kód (500 ms `DispatcherTimer`, `Stop/Start`, `await Save` v `Unloaded`,
  `catch { /* best-effort */ }`) ve dvou controls, každý s vlastním `PrefsScope` a vlastním
  `ServicesOverride` seamem (třetí v `EDInputForm`).
- **Důkaz:** `EDControls/dotnet/EDList/EDList.xaml.cs:172-181,562-629` vs
  `EDControls/dotnet/EDFilter/EDFilter.xaml.cs:148-150,184-193,302-331`; seamy
  `EDList.xaml.cs:29-32`, `EDInputForm.xaml.cs:29-32`, `EDFilter.xaml.cs:43-45`.
- **Dopad:** změna politiky (interval, chování při chybě, logování místo tichého catch, flush
  při zavření okna) se dělá na dvou místech a rozejde se.
- **Návrh opravy:** `internal sealed class DebouncedUserPrefsWriter(IUserPrefsService, scope,
  Func<JsonNode> snapshot)` se `Schedule()` a `FlushAsync()`; oba controls ho použijí.
- **Hotovo když:** stávající `EDListSetDataTests`/`EDFilterTests` zelené + jeden test debounceru
  bez WPF.
- **Omezení:** žádné veřejné API, formát blobů beze změny.

## P3

### A24 · `tools/GenerateTree` mimo build, žádný parita test

- [ ] **Stav:** otevřeno, částečně (advisory drift hook hotov 2026-08-26)
- **P3 · M** · Závisí na: - · Blokuje: - · Původ: [backlog R10](arch-backlog.md#r10---toolsgeneratetree-mimo-build-žádný-drift-guard)
- **Stav v kódu:** `tools/GenerateTree.cs` je file-based app, v `EDISON2.sln` není; drift hlídá
  `.claude/hooks/tree-drift.sh` + `tools/tree-sources.sha256` (advisory). **Strom právě
  driftuje** (startup hook 2026-09-17: `edisonnavbar.xml`, `edisontree.xml`), regenerace
  `powershell -File tools/sync-tree.ps1`.
- **Zbývá:** `tools/GenerateTree` jako `.csproj` v sln + parita test, že vygenerovaný výstup je
  identický s `Tree.cls` (jen část odvozená z dat, `ResolveExe` sonduje `C:\Edison`).
- **Omezení a kontext:** `Tree.cls` je ztrátová projekce XML; druhý konzument
  `ALWA.LICENCE.API.Catalog` v repu EDISON; směr „master do Caché" nerozhodnutý 🔸. Detail:
  backlog R10.

### A25 · Veřejný povrch EDControls bez konzumenta 🔸

- [ ] **Stav:** otevřeno
- **P3 · S** (evidence) / koordinovaný publish (odstranění) · Závisí na: - · Blokuje: - · Původ: [backlog R13a](arch-backlog.md#r13a---edcomponenttoolbar-export-a-print), rozšířeno
- **Důkaz (grep přes EDISONMAIN2, LegacyMiniApps, TD, bez `/obj/`):**
  - `EDComponentToolbar`: `ShowExport`, `ShowPrint`, `ExportCommand`, `PrintCommand`, `SettingsCommand`, `SearchPlaceholder` (`EDComponentToolbar.xaml.cs:96-104,148-166,188-227`), 0 konzumentů; `xmlns:edct` v `EdisonMainWindow.xaml:15` je mrtvý import.
  - `EDFilter`: `FilterCommand`, `AutoTrigger`, `ShowRefresh`, `RefreshText`, `FilterChanged`, `BuildFilterDict` public, 0 konzumentů; `IEDFilterTarget` implementuje jen `EDList`.
  - `EDList`: `CheckAll/UncheckAll`, `CellLinkClicked` jen `EDListTestWindow`; `isLink` bez produkčního konzumenta (`MockList.cls:12`).
  - `EDToolbar` jen `EDComponentAndViewModelTestWindow.xaml`; `ValidationContext.cs:7` veřejný typ bez použití; `MLRestJson` viz A38.
- **Dopad:** každý člen je závazek „breaking change = publikovat konzumenty" za nic; toolbar má
  tlačítka pro funkce, které neexistují; `EDControls/CLAUDE.md:100` tvrdí „(search, export, ...)".
- **Návrh opravy:** rozhodnutí vlastníka per člen (export v `EDList` implementovat, nebo
  deprekovat); odstranění v jednom koordinovaném publish s A2 a A22; opravit CLAUDE.md.
- **Omezení:** TD běží u zákazníků, odstranění veřejných členů = koordinovaný publish.

### A26 · Sdílená DLL nese aplikačně specifické ikony 🔸

- [ ] **Stav:** otevřeno
- **P3 · M** · Závisí na: - · Blokuje: - · Původ: [backlog R4b](arch-backlog.md#r4b---td-ikony-ve-sdílené-knihovně), upřesněno
- **Důkaz (grep klíčů `EDIcons.xaml` do projektů):** z 64 ikon 39 jen TemplateDesigner (New,
  Open, Save, Print, Undo, Redo, *Element, Align*, Distribute*, Z*, Sections, Fields, …),
  18 jen EDISONMAIN2 (Company, Server, Module, User, Tools, Star, Car, MlineLogo, …),
  **4 nikde** (`EDIcons.AddElement`, `EDIcons.Barcode`, `EDIcons.Snap`, `EDIcons.PageProperties`),
  ~4 sdílené. Ikony jdou přes `{StaticResource}`: starý TD exe + nová DLL bez klíče = výjimka
  při načtení okna (`EDControls/CLAUDE.md:16-18` řeší jen `DynamicResource`).
- **Dopad:** každá nová ikona TD toolbaru vyžaduje změnu EDControls csproj + xaml a publish DLL
  i pro shell.
- **Návrh opravy:** aditivně `TDIcons.xaml` v TD (může zatím aliasovat na EDIcons), shell-only
  dictionary v shellu, v EDControls zůstane sdílený zbytek; odstranění až koordinovaným publish;
  test „každý klíč `EDIcons.*` má konzumenta v EDControls nebo je ve výjimkách". Pokud je
  sdílený katalog záměr, zapsat to do `EDControls/CLAUDE.md`.
- **Omezení:** klíče měnit aditivně.

### A27 · Wire deklaruje, co UI nerenderuje: `IMAGE`/`CODEBOOK`, `Field.Format`, `UserHidden` 🔸

- [ ] **Stav:** otevřeno
- **P3 · S** · Závisí na: A14 (dělat v rámci nebo po) · Blokuje: - · Původ: [backlog R13d](arch-backlog.md#r13d---edlist-typy-image-a-codebook-bez-renderingu), rozšířeno
- **Důkaz:** `EDListParsing.cs:54,57` parsuje IMAGE/CODEBOOK, `BuildDataGridColumn`
  (`EDList.xaml.cs:427-501`) větví jen `IsLink` a `Color`, fallback na text.
  `EDInputForm/Field.cs:107-111` „Formátovací string pro zobrazení Decimal hodnoty… Aplikuje se
  v UI při ztrátě focusu", grep `Format` v `EDInputForm.xaml(.cs)` (mimo helpery) prázdný;
  server ho vystavuje jako featuru (`Form.cls:84-95 AddDecimal(..., format)`, `EDIFExample.cls:27
  "N2"`). `ItemVisibility.cs:7` `UserHidden` bez UI, které by ho nastavilo (`ShowSettings="False"`
  v obou hostech).
- **Dopad:** autor `.cls` nastaví `format="N2"` podle příkladu nebo `type: "IMAGE"` a nic se
  nestane, bez varování.
- **Návrh opravy:** per položka: implementovat (converter v Decimal template s parse-back,
  rendering IMAGE), nebo fail-fast na `InitializeList`/označit „rezervováno" v C# doc
  i `EDIFExample`.
- **Omezení:** pozice v CList a `.inc` konstanty jsou protokol, nemazat.

### A28 · `ModuleShortcutsService` mutuje sdílený stav, guard je ve VM, handlery se neodhlašují

- [ ] **Stav:** otevřeno
- **P3 · S** · Závisí na: - · Blokuje: A5 krok `FavoritesCoordinator` · Původ: [backlog N7](arch-backlog.md#n7---moduleshortcutsservice-mutuje-sdílený-stav-guard-je-v-jiné-třídě)
- **Důkaz:** `EdisonMainViewModel.cs:342-351` anonymní handlery na `_shortcuts.Items.CollectionChanged`
  a `LegacyModuleExited` (nikdy odhlášené); `_suppressFavoritesSync` `:489-493,:506-511,:1089-1091`;
  navíc `MiniApps/DesktopMiniAppView.xaml.cs:90` zapisuje `svc.GridColumns` z view do sdílené služby.
- **Návrh opravy:** guard patří k tomu, kdo kolekci mutuje: seed/batch režim ve službě, který
  event nefíruje, nebo odhlásitelný handler v koordinátoru.

### A29 · `DesktopMiniAppViewModel.LaunchCommand` nemá binding, view volá službu přímo 🔸

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: [backlog N4](arch-backlog.md#n4---desktopminiappviewmodellaunchcommand-nemá-ani-jeden-binding)
- **Důkaz:** `MiniApps/DesktopMiniAppViewModel.cs:24,40` command definován; žádný XAML binding;
  `MiniApps/DesktopMiniAppView.xaml.cs:33` `Vm?.Shortcuts.RequestLaunch(s)`, zatímco sourozenecký
  `RemoveCommand` bindovaný je (`DesktopMiniAppView.xaml:75`).
- **Návrh opravy:** command zapojit, nebo smazat.

### A30 · Drag&drop vzor ve dvou code-behindech

- [ ] **Stav:** otevřeno, částečně (práh sdílený)
- **P3 · S** · Původ: [backlog N5](arch-backlog.md#n5---duplikované-dragdrop-workflow-ve-dvou-code-behindech)
- **Důkaz:** práh je sdílený (`DesktopDragFormats.ThresholdExceeded`, commit ef11e045); zbývá
  `_dragStart`/`_dragModule`/flag „potlač klik po tažení" + `DoDragDrop` v
  `EdisonMainWindow.xaml.cs:179-200` (`_treeDragActive`) i `DesktopMiniAppView.xaml.cs:21-51`
  (`_suppressClick`), obě bez testu.
- **Návrh opravy:** attached behavior nebo helper `DragSource` s callbackem na payload.

### A31 · `DisplayedName` nese tři věci a persistuje se do blobu 🔸

- [ ] **Stav:** otevřeno, částečně (2026-09-21, commit s touto změnou) - suffixy stavu modulu
  (`- stub`, `- chybí exe`, `- rozbité`, `- stuby`) přidává `ModuleLaunchabilityCheck` jen pod
  `InternalDiagnostics.Enabled` (DEBUG / doména M-line), u zákazníka se do blobu nedostanou;
  mlineOnly suffix a oddělení `Name`/`DisplayedName` zůstávají 🔸.
- **P3 · S** · Původ: nový (2026-09-20)
- **Problém:** dekorace pro zobrazení mutuje doménový model (`with { DisplayedName = ... }`),
  který dál slouží jako text hledání a zdroj persistovaného `Name` dlaždice.
- **Důkaz:** `Modules/ModuleLaunchabilityCheck.cs:84-85` `exe with { Color = StubColor,
  DisplayedName = exe.DisplayedName + " - stub" }` / `" - chybí exe"`, `:71,:77` rodiny
  `" - rozbité"` / `" - stuby"`; `Modules/ModuleTreeState.cs:28` mlineOnly suffix. Spotřebitelé:
  `Modules/DesktopShortcut.cs:126` `DisplayedName = module.DisplayedName`, `:135-136` `ToDto()`
  (blob `Shortcuts`, který čtou legacy agendy), `ModuleTreeState.cs:89` hledání.
- **Dopad:** uživatel s chybějícím exe přidá modul na plochu → v Caché `Name = "Zaměstnanci -
  chybí exe"`; hledání „stub" vrací moduly; nová dekorace = kontrola 4+ konzumentů.
- **Návrh opravy:** oddělit `Name` (data, klíč) od `DisplayedName` (view); `DesktopShortcut.ForModule`
  a hledání berou `Name`.
- **Hotovo když:** test „ToDto po Mark(broken) nese původní název".
- **Omezení:** zda legacy `Name` z blobu zobrazuje, musí říct vlastník.

### A32 · `ExecutableModule` je record s hodnotovou rovností a mutabilním observable stavem 🔸

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `Modules/ExecutableModule.cs:5` `public record ExecutableModule : IModule,
  INotifyPropertyChanged` s mutabilními poli `:52,:70,:88` (`_isFavorite`, `_isStartupWindow`,
  `_isVisibleBySearch`); obcházení `EdisonMainWindow.xaml.cs:264-272` („moduly jsou record
  s hodnotovou rovností, takže dva shodně vyplněné uzly by si kontejnery prohodily",
  `ReferenceEquals`), `DesktopMiniAppView.xaml.cs:79`; `ModuleFamily` je naopak `class`.
- **Dopad:** WPF `SelectedItem`, `List.Remove`, `Distinct` se chovají jinak podle toho, zda uzel
  někdo označil; dva typy pod jedním `IModule` mají dvě sémantiky rovnosti.
- **Návrh opravy:** rozhodnout: `class` + `Clone(...)` místo `with`, nebo record s referenční
  rovností; smazat oba workaroundy.

### A33 · Složka `dotnet/MiniApps` drží infrastrukturu pruhu podpory; namespace cyklické

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDISONMAIN2/dotnet/MiniApps/SupportWidgetHost.cs`, `FontZoomWebView.cs`,
  `WidgetUrls.cs` používá jen `EdisonMainWindow.xaml.cs:73,77` a `LoginWindow.xaml.cs:76`,
  přičemž `MiniApps/CLAUDE.md:13` říká „Widgety podpora.m-line.cz v docku nejsou". Cyklus:
  `MiniApps/DesktopMiniAppProvider.cs:2`, `DesktopMiniAppViewModel.cs:6` `using
  EDISONMAIN2.Windows.EdisonMainWindow.Modules;` vs `EdisonMainViewModel.cs:17` `using
  EDISONMAIN2.MiniApps;` a `:143,:152-153` `DesktopMiniAppProvider.DesktopId`.
- **Dopad:** vytažení mini-app vrstvy do samostatného projektu (jako `LegacyMiniApps`) je kvůli
  cyklu nemožné bez přestavby.
- **Návrh opravy:** `SupportWidget*` + `WidgetUrls` do `Windows/EdisonMainWindow/SupportStrip/`,
  plochu (`DesktopMiniApp*`, `TileGridPanel`, `DesktopDragFormats`, `ModuleShortcutsService`,
  `DesktopShortcut`) do jedné složky `Desktop/`; `DesktopId` vlastní ten, kdo Plochu registruje.
- **Hotovo když:** `MiniApps` nereferencuje `Windows.EdisonMainWindow.Modules`.

### A34 · Dvě serverové cesty k „výchozí okno", jedna mrtvá

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `Windows/UserProfileWindow/UserProfileService.cs:11` record s `DefaultWindowId`,
  `:41-43` parsování řádku `form` z `ALVA.EDISON.Main.LoadUser`; okno (`UserProfileWindow.xaml.cs:37-44`)
  z něj čte jen dir pole; shell používá `:77-81` `LoadDefaultWindow` přes
  `ALVA.USERS.ApiEdison.GetUserDefaultForm` (`Modules/SessionDataLoader.cs:47`).
- **Dopad:** dva wire kontrakty k jedné hodnotě; čtenář neví, která je autoritativní.
- **Návrh opravy:** odstranit `DefaultWindowId` z `UserProfileData` a parsování `form`;
  do `EDISONMAIN2/CLAUDE.md` jedna věta „výchozí okno jen přes `ApiEdison.Get/SetUserDefaultForm`".
- **Omezení:** serverový řádek `form` v `LoadUser` nechat (legacy shell ho čte).

### A35 · Testovací okna a „Test funkcí" v produkčním exe a menu bez gate 🔸

- [x] **Stav:** vyřešeno 9500b872 (2026-09-21) - menu „Test funkcí" má `Visibility` z
  `InternalDiagnostics.Enabled` (DEBUG vždy, Release jen doména M-line - kolegové validují Release
  build), TemplateDesigner a „Spustit starý shell" přesunuty do Nástrojů. Test okna zůstávají v exe
  (ne `#if DEBUG`): gate je runtime, ať ho jde testovat bez Release buildu.
- **P3 · S** · Závisí na: A10 (dev flag) · Původ: nový (2026-09-20)
- **Důkaz:** `EdisonMainWindow.xaml:168-187` `<MenuItem Header="Test funkcí">` bez `Visibility`
  (Test EDInputForm, Test EDList, Test EDViewModel, countdown, overlay, „Vyvolat chybu");
  `EdisonMainViewModel.cs:245-247` konstrukce oken; `EDInputFormTestWindow.xaml.cs:15`
  `CacheClass = "ALVA.EDControls.EDInputForm.EDIFExample"`, `EDListTestWindow.xaml.cs:13-14`
  `MockList` + `HostId = "EDISONMAIN2.EDListTestWindow"` (persistuje layout do Caché); kontrast
  `AppBootstrapper.cs:410-412` `#if DEBUG new DevSampleMiniApps()`.
- **Dopad:** uživatel u zákazníka spustí mock formuláře proti produkčnímu serveru (chybový
  popup, když tam třídy nejsou), „Vyvolat chybu" založí tiket; TemplateDesigner a „Spustit starý
  shell" ve stejném menu jsou legitimní nástroje, smíchané kategorie.
- **Návrh opravy:** gate přes `ModuleTreeState.IsMlineDomainStation` nebo dev flag; testovací
  položky oddělit od nástrojů; test okna pod `#if DEBUG` konzistentně s `DevSampleMiniApps`.
- **Omezení:** TD a „starý shell" jsou dokumentované cesty, zůstávají dostupné.

### A36 · Parsování legacy delimited výstupů zdvojené v šesti třídách se třemi konvencemi

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `Startup/Updater.cs:66-80` (`for i=1..GetUpperBound(0)`), `Startup/LogoutWatcher.cs:74-85`
  (`for i=0..Length`, projde i ERR slot), `UserProfileService.cs:91-98` a
  `SetupWindow.xaml.cs:68-77` (`for i=1..Length`), `Startup/AccessLoader.cs:42-46` (od 2),
  `UserAuthenticator.cs:36-37,71` (`output[1]` napřímo).
- **Dopad:** změna wire konvence = 6 míst; rozdíly `Length` vs `GetUpperBound` skryjí off-by-one
  při prázdné odpovědi.
- **Návrh opravy:** jeden helper (`Startup/LegacyRows.cs` nebo EDControls vedle `MLRestAdapter`):
  `IEnumerable<string[]> Rows(object[] output, char del)` + `string? FirstField(output, key)`.
- **Hotovo když:** `grep "output\[i\]?.ToString()" EDISONMAIN2/dotnet` jen v helperu; stávající
  testy zelené.
- **Omezení:** `object[]` je u těchto volání povolený legacy vzor, wire beze změny.

### A37 · Dock layout je per Windows-účet × IDFa, ostatní stav per Edison-uživatel × IDFa 🔸

- [ ] **Stav:** otevřeno
- **P3 · S** (rozhodnutí) až M (přesun) · Původ: nový (2026-09-20)
- **Důkaz:** `Startup/LocalPrefs.cs:22-26` `MiniAppLayouts`/`TreeWidths` v `local.json` klíčované
  jen IDFa; `EdisonMainViewModel.cs:168-170` `(_prefs.MiniAppLayouts ??= new())[idfa] = ...`;
  `:1306` `ReplaceMiniAppSession(Idfa);   // přihlášení jiného uživatele: stejná firma, jiná data`;
  root `CLAUDE.md` rozdělení nezdůvodňuje.
- **Dopad:** sdílené terminály: uživatel B rozhází dock uživateli A; na jiné stanici žádný layout.
- **Návrh opravy:** rozhodnout (vlastnost stanice vs. uživatele) a zapsat do CLAUDE.md; pokud
  stanice, klíčovat `idfa|login`; pokud uživatel, přesun do UserPrefs blobu (velikost AvalonDock
  XML zvážit).
- **Omezení:** nullable property kompatibilita `local.json`.

### A38 · `MLRestJson` je veřejný wire kontrakt bez serverové ani klientské strany

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/MLRestJson.cs:28-30` `input["payload"] = ...; CVar output =
  await Task.Run(() => mlRest.QueryOrThrow(cacheClass, method, input));` bez klonu; volající
  pouze `EDControls.Tests/MLRestJsonTests.cs`; grep `"payload"` přes `**/*.cls` = 0;
  `EDControls/CLAUDE.md:84-85` ho popisuje jako živý kontrakt.
- **Dopad:** dokumentace tvrdí kontrakt, který nikdo neplní; první reálný uživatel dostane
  RestStatus race (A2), protože helper vypadá jako hotová bezpečná cesta.
- **Návrh opravy:** smazat (formálně odebrání public členu, konzument žádný), nebo přepsat na
  `IRestCallRunner` a v CLAUDE.md označit „bez serverové strany, vzor pro budoucí endpointy".

### A39 · Sdílená JSON preference s cross-process watcherem existuje dvakrát

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/UserFonts/UserFontPreference.cs:46-48` (FilePath), `:60-72`
  (Load), `:93-101` (Save), `:103-129` (watcher + `Thread.Sleep(50)`) vs
  `EDControls/dotnet/EDTheme/UserThemePreference.cs:29-31,40-52,54-58,60-81` řádek po řádku
  stejné včetně komentáře „stejná heuristika jako UserFontPreference" (`:78`);
  `ErrorReporting/ErrorFallbackLog.cs:20-22` skládá `%APPDATA%\m-line\common` potřetí.
- **Dopad:** oprava watcher pasti (dvojí `Changed` per zápis, sharing violation) 2×; přesun
  složky 3 místa; testy duplikují seam `FilePath`.
- **Návrh opravy:** `internal SharedJsonPreference<T>` (cesta z `MlineCommonPaths`, Load/Save/watch),
  obě veřejné třídy jako tenké wrappery se stejným API.
- **Hotovo když:** `FileSystemWatcher` konstruktor je v EDControls právě jednou; stávající testy
  obou preferencí bez změny.
- **Omezení:** veřejné `Current`/`Save` a formát `font.json`/`theme.json` beze změny (čtou je
  oba exe).

### A40 · Statické test seamy ErrorReportingu a persistence drží ručně udržovaný seznam `[Collection]`

- [ ] **Stav:** otevřeno
- **P3 · M** · Závisí na: A39 · Původ: nový (2026-09-20)
- **Důkaz:** seamy `ErrorReporting/ErrorReports.cs:15`, `ErrorReporter.cs:37`,
  `ErrorFallbackLog.cs:13,18`, `MlineEnvironment.cs:51`, `WindowStatePersistence.cs:28`,
  `UserFontPreference.cs:46`, `UserThemePreference.cs:29`, `EDUsageTelemetry.cs:17-18`,
  `EDServices.cs:69,90`. Kolekce `EDControls.Tests/EDUsageTelemetryTests.cs:12-13`
  `[CollectionDefinition(SharedStaticStateCollection.Name, DisableParallelization = true)]`
  s 8 členy; `UserFontPreferenceTests.cs:20-31` a `UserThemePreferenceTests.cs:21-33` mutují
  statiku **mimo** kolekci; `TemplateDesigner.Tests/xunit.runner.json:6-8` totéž selhání
  popsané („udržovat seznam tříd je zdroj téhle chyby").
- **Dopad:** nový test na `FontService` běžící paralelně s `UserFontPreferenceTests` =
  nedeterministický pád; `ErrorReports` má 4 seamy, které každý test resetuje ve správném pořadí
  (`ErrorReportsTests.cs:43-55`).
- **Návrh opravy:** `ErrorReportingPipeline(ErrorThrottle, HttpClient, fallbackPath, isInternal)`
  instance za statickou fasádou `ErrorReports.Pipeline` (jeden seam); preference seam zmizí
  s A39; zbylé statiky (`EDServices`, telemetry buffer) = A6.
- **Hotovo když:** `ErrorReportsTests`/`ErrorReporterTests` bez `[Collection]` a `finally` resetů.
- **Omezení:** veřejná statická API `ErrorReports.Report*`, `Enabled`, `ErrorReporter.IngestUrl/ApiKey`
  zůstávají jako fasáda.

### A41 · Odeslání hlášení chyby je skryté v implementaci dialogu 🔸

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/EDViewModel/PopupDisplayer.cs:26-33` `var refId =
  ErrorReports.Report(ex, resolvedOwner); EDErrorPopup.Show(...)`; `IPopupDisplayer.cs:23-24`
  slibuje jen „Scrollovatelný error dialog"; `EDISONMAIN2/dotnet/App.xaml.cs:34` `new
  PopupDisplayer(owner).Error(e.Exception);` druhá instance kvůli owneru; `RelayCommand.cs:18`,
  `AsyncRelayCommand.cs:34` chytají výjimky výhradně přes displayer.
- **Dopad:** kdo dosadí jiný `IPopupDisplayer` (headless, toast), reporting tiše ztratí; dnes
  žádná alternativní implementace není, proto P3.
- **Návrh opravy:** doc kontrakt „implementace `Error` hlásí do `ErrorReports`" na rozhraní, nebo
  reporting vytáhnout do `RelayCommand`/`AsyncRelayCommand` catch před displayer (aditivní
  `Error(ex, owner, refId)`).
- **Omezení:** `IPopupDisplayer` implementují mocky v ~20 test souborech, jen aditivní změny.

### A42 · Dva vyhledávací algoritmy pod stejným toolbarem

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/EDList/EDListFilter.cs:21` `value.Contains(needle,
  StringComparison.OrdinalIgnoreCase)` (bez diakritiky) vs `EDInputForm.xaml.cs:225`
  `SearchNormalization.NormalizeForSearch(s)` a `EDFilterComboBoxLogic.cs:29-31`; tooltipy
  `EDComponentToolbar.xaml:92,97` slibují „(Enter)" / „(Shift+Enter)", `EDInputForm/CLAUDE.md:61-62`
  přiznává, že nadrátované nejsou.
- **Dopad:** „sablona" ve formuláři najde „Šablona", v seznamu ne.
- **Návrh opravy:** `EDListFilter.Matches` přes `SearchNormalization`; tooltipy sladit nebo
  Enter nadrátovat.
- **Hotovo když:** `EDListFilterTests` s případem s diakritikou.

### A43 · Koncept „barva" má dva wire formáty a tři konvertory

- [ ] **Stav:** otevřeno
- **P3 · M** · Původ: nový (2026-09-20)
- **Důkaz:** `EDList.xaml.cs:710-728` `RgbStringToBrushConverter` (`R;G;B`);
  `EDInputForm/ColorPreviewHexToBrushConverter.cs:12-50` (`#AARRGGBB`);
  `EDViewModel/HexToBrushConverter.cs`; formáty popsané jen v CLAUDE.md (`EDList:37`,
  `EDInputForm:21`), `Column.cls` o formátu nic.
- **Dopad:** master-detail host (barva v seznamu, editace ve formuláři) musí konvertovat; nový
  server list zvolí hex a tiše dostane `Transparent`.
- **Návrh opravy:** jeden wire formát pro nové listy (hex, shodně s formulářem), konvertor umí
  oba a je sdílený.
- **Omezení:** `R;G;B` existujících `LoadList` implementací musí zůstat čitelné.

### A44 · Kontrakt update mechanismu nemá vlastní modul, žije ve vzájemných source-lincích dvou exe

- [ ] **Stav:** otevřeno
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDUPDATE2/dotnet/EDUPDATE2.csproj:21` kompiluje `EDISONMAIN2/dotnet/Startup/VerzeInf.cs`
  (namespace `EDISONMAIN2`); `EDISONMAIN2/dotnet/EDISONMAIN2.csproj:27` kompiluje
  `EDUPDATE2/dotnet/ForcedStamp.cs` (namespace `EDUPDATE2`); `EDISONMAIN2.Tests/EDISONMAIN2.Tests.csproj:31`
  kompiluje `EDUPDATE2/dotnet/UpdaterArgs.cs`; `EDUPDATE2/dotnet/UpdateEngine.cs:3` `using EDISONMAIN2;`.
  Klíče argumentů jsou literály na dvou místech (`EDISONMAIN2/dotnet/Startup/Updater.cs:155-161`,
  `EDUPDATE2/dotnet/UpdaterArgs.cs:38-44`), hlídané round-trip testem `UpdaterTests.cs:306`,
  který k tomu potřebuje ten source-link.
- **Dopad:** změna `VerzeInf` pro potřeby shellu (závislost na EDControls nebo MLRest) rozbije
  invariant „EDUPDATE2 bez závislostí" až při buildu updateru; nový argument = 2 soubory ve dvou
  exe + tabulka v `EDUPDATE2/CLAUDE.md`; assembly EDUPDATE2 nese cizí namespace.
- **Návrh opravy:** malý projekt bez závislostí (např. `EDUpdateContract`: `VerzeInf`,
  `ForcedStamp`, `UpdaterArgs` + konstanty klíčů, `BuildArguments` vedle `Parse`), referencovaný
  z obou exe i z obou test projektů. Single-file publish ho zabalí do exe
  (`SingleFilePublish.targets` uvolňuje jen `EDControls`), takže invariant „žádná volná DLL
  z `C:\Edison`" platí dál.
- **Hotovo když:** žádný `<Compile Include="..\..\<jiný projekt>` v csproj; `EDUPDATE2` bez
  `using EDISONMAIN2`; `dotnet publish` EDUPDATE2 dá jeden exe.
- **Omezení:** EDUPDATE2 nesmí referencovat nic z `C:\Edison`; klíče argumentů a exit kódy
  0/1/100/200 jsou kontrakt.

---

## Co je v pořádku (ověřeno, není nález)

- `RestCallRunner`, `SessionDataLoader`, `LogoutWatcher.Poll`, `SessionLogout`,
  `LicenceVerifier.ReacquireLock` jedou na klonech; race A2 se týká jen `IUserPrefsService`
  a `MLRestJson`.
- `ApplySessionData` je synchronní, guardy uvnitř neawaitují; `CompanySwitcher.Switch`
  a `SessionSwitcher.Switch` drží dokumentované pořadí včetně veta okna a rollbacku.
- `HandOffLoginWindow` nastaví `MainWindow` před `OnMainWindowClose`; `ForcePasswordChange`
  restoruje předchozí mode; `ShellShutdown.Force` je jediná programová cesta ukončení.
- `AppBootstrapper` `protected virtual` + `protected` ctor bez `Application` funguje
  (`EDAppearance.Initialize(null)` má null guard).
- `Updater.Check` pořadí je komentované a pokryté; `UpdateEngine` je jeden pipeline s virtuálními
  seamy a 1000+ řádky scénářových testů, ne God class; `UpdateWindow` má rozhodnutí ve
  statických `DecideClose`/`ShouldRunExe`.
- `SettingsLoader.Save` zachovává cizí uzly a legacy paritu; `Tree.cls` ruční metody jsou nad
  splice anchorem s `$$$Init`; `TreeTest.cls` kryje wire invarianty snapshotu; `UserProfile.cls`
  Save no-op je záměrný a pinovaný testem.
- `EDInputForm.inc` ↔ `FieldType`/`ItemVisibility`/`HelpTextMode` hlídané z obou stran; klíč
  `ERR` přeskakují oba parsery; `EDInputFormController` drží instanci, kterou `CompanySwitcher`
  mutuje, nevyměňuje, takže reference nestárne.
- `EDList` async (semafor + `_loadGeneration`) je konzistentní včetně sync bumpu; `SetData`
  a `CellStyle` mají reálné konzumenty (LegacyMiniApps).
- `EDComponentToolbar` Ctrl+F odregistrace, `EDFooter` detach handlerů a `EDFilter` pořadí
  Loaded → Restore → jeden refresh jsou správně; `EDMiniAppBar` je záměrně mimo
  `EDComponentToolbar`.
- `MLRestAdapter.With` drift hlídá reflection test; `const` vs `static readonly` sedí až na
  `EdisonDeployRoot` (A8); launch kontrakt je dnes synchronní na obou stranách (A12 je o tom,
  čím je synchronizace hlídaná).
- Tři class handlery na `Window.Loaded` mají guardy a sdílejí `FrameworkWindows`; dva
  telemetrické kanály (HTTPS vs MLRest) jsou záměr a hlášení chyb má jednu fasádu.
- `LegacyDashboardMiniAppProvider` dává všem deskriptorům tutéž instanci loaderu;
  `MiniAppManager` je testovatelný přes `IMiniAppHost`; `MiniAppCatalog.Build` fail-fast
  na duplicitní Id, DEBUG-only providery Release layout nerozbijí.
- `WindowDimmer` refcount + `ForceRelease` na `Closed`; `ThemeService.IsDark` vs `Current`
  se rozcházejí jen milisekundy.
