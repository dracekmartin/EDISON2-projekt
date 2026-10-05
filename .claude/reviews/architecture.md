# Architektonický review: živý dokument nálezů

| | |
| --- | --- |
| **Poslední běh** | 2026-10-01 (aktualizace; první běh 2026-09-20 nad `c04d0d6f`) |
| **Commit** | `19f62712` (branch `main`, clean) |
| **Scope běhu** | `EDISONMAIN2/` (+ `.LegacyMiniApps`, `.Tests`, `CACHE/`), `EDControls/` (+ `.Tests`, `CACHE/`), `EDUPDATE2/` (+ `.Tests`) |
| **Mimo scope** | `TemplateDesigner*` (jen jako konzument EDControls), `ServiceGtfsRt`, `EDTesting`, `tools/`, `EDISON2.IntegrationTests` (jen jako pokrytí kontraktů) |
| **Odvozeno** | 2026-10-01: otevřené nálezy ověřili 3 read-only subagenti (shell, EDControls, kontrakty a update) proti HEAD, nový kód deploye (Startup, licence, updater, připojení) prošel ručně; každá změna stavu ověřena citací kódu. 2026-09-20: 4 subagenti na ~220 souborů, EDUPDATE2 ručně. |

**Hotovo: 19 / 47**

**Běh 2026-10-01.** Od `c04d0d6f` přibylo 213 commitů, většinou deploy shellu (nálezy I01-I25
v [2026-09-29-integrace-shellu.md](2026-09-29-integrace-shellu.md)). Žádný otevřený nález se
nevyřešil celý, A42 je napůl. Zhoršily se A5 (VM 1723 ř.), A7, A8, A12 (21 polí) a A36 (8 parserů),
tvar změnily A9, A10 a A37. A44 jde z P3 na P2, protože brání plánovanému přepnutí na EDUPDATE2.
Nový je A45 (víc instancí shellu nad jedním `local.json`). Z nasazení a post mortemu vydání přibyly
A46 (obsah exe shellu podle `C:\Edison` publikujícího stroje) a A47 (runtime se rozbaluje jen pro
nový shell, TemplateDesigner na něm závisí taky).

## Jak s dokumentem zacházet

- Jeden dokument na repo, žije napříč běhy review. ID `A1`, `A2`… jsou stabilní a nikdy se
  nepřidělují znovu; další běh přidá nová ID a u existujících jen ověří stav.
- **Opravný chat řeší jedno ID**, nebo jeden proud ID v pořadí ze sekce
  „Proudy pro paralelní řešení". Nález je samonosný: nemá kontext review chatu, všechno
  potřebné (důkaz `path:line`, dopad, návrh, omezení) je u něj. Čísla řádků otevřených nálezů
  jsou ukotvená v commitu `19f62712`, u vyřešených platí důkaz z `c04d0d6f`; při posunu hledej
  podle jména symbolu.
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

## Proudy pro paralelní řešení (2026-10-01)

Proud řeší svá ID v uvedeném pořadí a mění jen své soubory. Proudy jedné vlny se v souborech
nepřekrývají, takže můžou běžet současně.

- **Worktree** pro každý proud, který builduje .NET (vlastní `bin/obj`, slití přes `/wtm`).
  Sdílený strom jen pro proud bez buildu.
- Proud začne, až jeho soubory nemají necommitnuté změny. Potřebuje soubor jiného proudu →
  zastaví a řekne to; počká na vlnu, ve které soubor nikdo nedrží.
- Jedno ID = jeden commit přes `/rwcm`. Po ID proud přepíše jeho `**Stav:**`; počítadlo **Hotovo**
  a tuhle sekci mění jen koordinační chat po slití.
- S1 drží ctor `EdisonMainViewModel` stabilní (nové třídy skládá uvnitř VM), protože ho skládá
  `AppBootstrapper` ze S2. S4 nemění veřejné členy `Updater`, které volá `AppBootstrapper`.
- Veřejné API EDControls (S5, S6, S8, S9) jen aditivně. Odebrání členů (A22, A25, A38) jde ve vlně 3
  jedním koordinovaným publishem EDControls + TemplateDesigner + shell.
- S6 a S8 sdílí Caché package `ALVA.EDControls` na dev serveru: jejich Caché testy můžou vidět
  rozpracovanou třídu druhého proudu.

| Proud | ID v pořadí | Vlastní soubory | Vlna | Forma |
| --- | --- | --- | --- | --- |
| S1 Shell VM a plocha | A4 → A20 → A28 → A5 kroky 1-3 | `EdisonMainViewModel.cs`, `EdisonMainWindow.xaml(.cs)`, `MiniApps/Desktop*`, `Modules/*` kromě `InternalDiagnostics.cs`, testy VM a harness | 1 | chat |
| S2 Startup a login | A10 → A18 → A19 → A34 → A6 bod 3 | `App.xaml.cs`, `Startup/*` kromě `LocalPrefs`, `TransientLocalPrefs`, `Updater`, `VerzeInf`; `Modules/InternalDiagnostics.cs`, `LoginWindow/*`, `UserProfileWindow/*`, jejich testy | 1 | chat |
| S3 Lokální preference | A45, sloučení v `LocalPrefs.Save` bez změny volajících (klíč `server\|idfa` až po A37) | `Startup/LocalPrefs.cs`, `Startup/TransientLocalPrefs.cs`, `LocalPrefsTests` | 1 | subagent |
| S4 Update a vydání | A44 → A46 (`global.json` slít až na konci vlny, mění SDK všem) → A47 varianta b | `Startup/Updater.cs`, `Startup/VerzeInf.cs`, `EDUPDATE2/*`, nový projekt kontraktu, csproj, `EDISON2.sln`, `SingleFilePublish.targets`, `global.json`, `docs/deploy.md`, EDLOADER v repu EDISON | 1 | chat |
| S5 Launch kontrakt EDControls | A7 → A8 → A12 body 1-3 → A6 body 1-2 | `EDViewModel/WindowNavigator.cs`, `IWindowNavigator.cs`, `EdisonLaunchArgs.cs`, `WindowStateStore.cs`, `EDViewModel/EDViewModel.cs`, `EDErrorPopup.xaml.cs`, `InfChangelogSource.cs`, TD `App.xaml.cs`, jejich testy, fixture u `FormToRunGetter` v repu EDISON | 1 | chat |
| S6 EDList | A16 → A17 → A42 → A14 → A27 část EDList → A23 | `EDList/*`, `EDFilter/EDFilter.xaml.cs`, `EDControls/CACHE/.../EDList/*`, `ListTest.cls`, `MockList.cls`, jejich testy | 1 | chat |
| S7 Kontrakty šva | A13 body 2-4 | `EDISON2.IntegrationTests/*`, `EDISONMAIN2/CACHE/.../Tests/*.cls` | 1 | subagent |
| S8 EDInputForm | A15 🔸 → A27 část formulář (`Format`, `UserHidden`) | `EDInputForm/*`, `Form.cls`, `EDIFExample.cls`, jejich testy | 2 | chat |
| S9 Infrastruktura EDControls | A39 → A40 → A41 🔸 | `UserFonts/*`, `EDTheme/UserThemePreference.cs`, `ErrorReporting/*`, `PopupDisplayer.cs`, `RelayCommand.cs`, `AsyncRelayCommand.cs`, jejich testy | 2 | chat |
| S1 pokračuje | A9 (s S2) → A5 kroky 4-5 → A21 → A29 → A30 → A31 🔸 → A32 🔸 | VM, `Modules/*`, `MiniApps/Desktop*`; A9 i `SessionFactory`, `CompanySwitcher`, `LicenceVerifier` | 2 | chat |
| Úklid | A36, A33, A22 + A25 🔸 + A38 (jeden publish), A26 🔸, A24, A43, A37 🔸 (klíče z A45), A6 bod 4, `ALVA.EDTesting.TestRunner` počítá skipped (status 2) zvlášť a vypíše důvod (dnes FAIL s prázdnou hláškou, brání `$$$AssertSkipped` v TD), mrtvý `OpenMiniAppCommand` ve VM (bez bindingu), `docs/zastupci-na-plose.md:62` odkazuje na zrušené `ReloadShortcutsAsync` | napříč | 3 | po slití vln 1 a 2 |

Rozhodnutí vlastníka padla 2026-10-05, u každého dotčeného ID je řádek **Rozhodnutí vlastníka**.
Otevřený zůstává jen konkrétní vydávací stroj (A46 bod 3).

## Mapa repa (stav k 2026-10-01)

| Projekt | Typ | Reference | Poznámka |
| --- | --- | --- | --- |
| EDControls | lib | MLRest.dll, EDLicence.dll (`C:\Edison`) | volná DLL vedle exe, sdílená se zákaznickým TemplateDesigner |
| EDISONMAIN2 | WinExe | EDControls, LegacyMiniApps, EDISONAPI/EDLicence/EDUsers/MLRest.dll, WinForms | shell; source-link `EDUPDATE2/dotnet/ForcedStamp.cs`; `EDUsers.dll` kvůli stažení licence z M-line |
| EDISONMAIN2.LegacyMiniApps | lib | EDControls, MLRest.dll | dashboardy Dispečink |
| EDUPDATE2 | WinExe | žádná (záměr) | source-link `EDISONMAIN2/dotnet/Startup/VerzeInf.cs`; shell ho zatím nespouští (`Updater.ActiveKind`) |
| EDTesting | lib | EDControls, MLRest.dll | Caché test framework |
| EDISON2.IntegrationTests | test | EDISONMAIN2, EDControls, MLRest/EDLicence.dll | šev .NET ↔ Caché přes skutečné MLRest, mimo hlavní běh (`Category!=Integration`) |
| TemplateDesigner | WinExe | EDControls, MLRest.dll | mimo scope, běží u zákazníků |

Composition root shellu: `App.Application_Startup` → start bez značky `reason=2` přesměruje přes
`EDISON.exe` (`RedirectToLoader`) → `new AppBootstrapper(app, ConnectionOverride.Parse(args),
testRun).Run()` (template method, `protected virtual` kroky) → `InitializeServices` (`WindowNavigator`,
`PopupDisplayer`, `EDServices.Initialize` = statický singleton `Current`) → `PrepareLogin` →
`PassesPreLoginGates` → login → `AcquireSession` → `CheckForUpdate` → `LicenceVerifier.LogLogin` →
`BuildShellWindow` (`EdisonMainViewModel`: ctor 13 povinných + 2 volitelné parametry, `CompanySwitcher`,
`SessionSwitcher`, `LogoutWatcher`, `MiniAppCatalog`, `ModuleShortcutsService`).
EDUPDATE2: `App.OnStartup` → `UpdaterArgs.Parse` → `UpdateWindow` → `UpdateEngine.Run` na `Task.Run`.

Externí hranice: Caché přes `IMLRest`/`MLRestAdapter` (wire `^||ML`); legacy WinForms host
`EDISONMAIN2API.exe` (`WindowNavigator.BuildLauncherArguments` ↔ `EdisonLaunchArgs` ↔
`FormToRunGetter` v repu EDISON, pole [0]-[20]); `podpora.m-line.cz` HTTPS (ErrorReporting); WebView2
widgety; AvalonDock; release server HTTP (`Updater.GetReleaseNotice`); M-line licence přes
`EDUsers.UpdateLicFromMlineServer`; zdroj stanic jako UNC nebo HTTP URL (`Updater.IsUri`, stažení
updateru s kontrolou `MZ`); legacy `EDUPDATE.exe` jako aktivní updater; EDLOADER `EDISON.exe`
(redirect přes `EDISONMAIN2_REDIRECTED`, restart po updatu, uzel `^EDISON("System","NewShell")`);
příkazová řádka `server=`/`restport=` (`ConnectionOverride`, nová instance shellu); souborový systém
`C:\Edison` (`EDISONini.xml`, `VERZE.INF`, `EDISONCRC.bin`, aktivní `CliUp.ini`, `CliUp2.ini` jen pro EDUPDATE2).

Persistence: `LocalPrefs` JSON per **Windows uživatel** (`%APPDATA%\m-line\EDISONMAIN2\local.json`,
nově i `ShellWindowBounds`, `Connections`, `SupportStripHeights`; E2E běh `TransientLocalPrefs`);
`IUserPrefsService` (`CacheUserPrefsService`, klon per request) a `CacheWindowStateStore` do Caché
per-user × IDFa (geometrie shellu a login okna je v `local.json`, `ShellWindowStateStore`);
`%APPDATA%\m-line\common` cross-process (font, theme).

Největší soubory: `EDControlStyles.xaml` 1887 ř., `EdisonMainViewModel.cs` 1723, `EDList.xaml.cs`
729, `EDInputForm.xaml.cs` 720, `AppBootstrapper.cs` 565, `WindowNavigator.cs` 504, `UpdateEngine.cs`
476, `Updater.cs` 427, `LicenceService.cs` 201.

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

- [x] **Stav:** vyřešeno 5fcfa28e (2026-09-20) - (1) `CacheUserPrefsService` jede přes
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

- [x] **Stav:** vyřešeno b793d9c4 (2026-09-20) - `WireToolbarSearch` v konstruktoru
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

- [x] **Stav:** vyřešeno 64f22346 (2026-10-02) - VM vystavuje `SupportWidgetUrl` a notifikuje ho po přepnutí
  firmy i před rebuildem po přihlášení jiného uživatele (od 569b3454 i přes `ReplaceCompanies` →
  `RaiseSessionPropertiesChanged`); okno shellu `EDServices.Current` nečte.
- **P1 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** View si skládá URL widgetu samo z `EDServices.Current` a přenačítá ho jen na
  notifikaci `Idfa`; VM neví, že na tom view závisí, a při switch user `Idfa` nenotifikuje.
- **Důkaz:**
  - `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs:61-64` `if (e.PropertyName == nameof(EdisonMainViewModel.Idfa)) FillSupportStrip();`.
  - `:71-73` `var services = EDServices.Current; var ml = services.MLRest; var url = WidgetUrls.Edison2(ml?.Idfa ?? "", ml?.LoginName ?? "", services.UserEmail);`.
  - `EdisonMainViewModel.cs:1453-1466` po `SwitchUserResult.Success` notifikuje jen `UserName`, `FullName`, `Mlp`, `UserDisplay`; `ReplaceMiniAppSession(Idfa)` (`:1458`) index firmy nemění.
  - `f821f574` udělal pruh roztahovatelný, skládání URL nechal; `683ee5b1` smazal z `MiniApps/CLAUDE.md` větu, že widget re-naviguje na změnu `Idfa`.
- **Dopad:** „Přihlásit jiného uživatele" (stejná firma) nechá v pruhu URL s `user=`/`email=`
  původního uživatele; podpora cílí sdělení na špatného člověka, dokud se nepřepne firma nebo
  nerestartuje shell.
- **Návrh opravy:** VM vystaví `SupportWidgetUrl` (z `Rest.Idfa`, `Rest.LoginName`,
  `_services.UserEmail`) a notifikuje ho v `RaiseSessionPropertiesChanged` (`:1022`) i po switch
  user; code-behind poslouchá tu jednu property a `EDServices.Current` z view zmizí.
- **Hotovo když:** VM test „`LoginAsAnotherUserAsync` Success → `PropertyChanged(SupportWidgetUrl)`
  a hodnota nese nový `LoginName`".
- **Omezení:** `WidgetUrls` sdílí `LoginWindow` (posílá jen IdFa), nechat jako čistou funkci.

### A5 · `EdisonMainViewModel` je God class shellu

- [ ] **Stav:** částečně (2026-10-05) - kroky 1-3 hotové: instanční `ModuleTreeState` (c113e3fb),
  `FavoritesCoordinator` (d2b3c6b1, řeší i A28 ve VM), `MiniAppSessionController` (24f497ef, nese A20);
  VM má 1253 ř. Zbývá krok 4 (licence session, `_sessionGeneration` a `SameSession()` v
  `ReloadAllowedCompaniesAsync` z 569b3454, který přidal i ctor parametr `loadAllowedCompanies` a seznam
  povolených firem) a krok 5 (menu commandy).
- **P1 · L** · Závisí na: - · Blokuje: A20, A21, A28 (řešit v rámci extrakcí) · Původ: [backlog R1](arch-backlog.md#r1---edisonmainviewmodel-je-god-class-shellu)
- **Problém:** VM má 1723 řádků (2026-09-20: 1525) a 42 commandů, ctor 15 parametrů (13 povinných,
  volitelné `restRunner` a `licenceSync`; harness `EdisonMainViewModelHarness.cs:17` tvrdí 13). Statické
  algoritmy jsou venku (`ModuleTreeState` jen jako `internal static` helpery, `SessionDataLoader`,
  `ModulePreferencesService`, `ModuleShortcutsService`, `TreeTab`, `ModuleLaunchabilityCheck`,
  `ChangelogPromptService`, `LicenceNotice`), instanční koordinace zůstává v jedné třídě a deploy
  do ní přidal další odpovědnosti.
- **Důkaz (co ve VM zůstává, `EdisonMainViewModel.cs`):** mini-app session controller (picker,
  výchozí layout, `EnsureDesktopOpen`, persist/restore layoutu včetně výšky pruhu podpory,
  `ReplaceMiniAppSession`; ř. 121-242); ctor s commandy a wiringem plochy (244-384); orchestrace
  rebuildu session a výchozího okna (393-577), v ní **nově** licenční chip `RefreshLicenceNoticeAsync`
  (442) a stažení licence z M-line `SyncMlineLicenceAfterLoginAsync` (466); koordinátor
  oblíbených/plochy/používaných (`ReloadShortcutsAsync`, `SeedShortcuts`, `ApplyPersonalization`,
  `RebuildFavoritesTree`, `ToggleFavorite`, `SyncFavorites`, `PersistShortcuts`; 586-665, 1201-1324);
  persistence rozbalení včetně snímku před hledáním `_expansionBeforeSearch` (667-769); debounce
  hledání (771-827); firmy + `SwitchCompanyAsync` (1007-1138); launch + novinky (1140-1199);
  zavření/exit/switch user (1411-1470); menu včetně přímého `Rest.QueryOrThrow` v `ResetForms`
  (1477) a **nově** `ConnectToDatabase`/`ConnectionLaunchInfo` (1499-1524) a `EdisonCopyStartInfo`
  (1489); labely spuštěných modulů (1606-1687); Ctrl+W (1689-1721).
  Dva mechanismy „probíhá změna session": `_sessionGeneration` (47) s ručním
  `InvalidatePendingSessionWork()` ve třech cestách (1057, 1440, 1555) a `_sessionSwapInProgress`
  (183) nastavovaný jen v přepnutí firmy (1087-1103). Testy na VM: 6 souborů, 2404 řádků, všechny
  přes jeden `EdisonMainViewModelHarness`.
- **Dopad:** každá změna session workflow, plochy nebo mini-apps prochází jednou třídou;
  guardy `_suppressXxx` sdílí několik odpovědností; testy se dělí podle VM, ne podle chování.
  Nová cesta, která mění session, musí vědět o obou guardech, jinak pozaďový výsledek (licence,
  počty) přestaví stav nad jinou firmou.
- **Návrh opravy (pořadí platí, každý krok vlastní commit s přesunutými testy):**
  1. `ModuleTreeState` instanční část: kolekce stromů, filtrace, `ApplySearch`, persistence
     rozbalení (dnes `SuppressExpansionSave()` refcount scope a `_expansionBeforeSearch`; zachovat).
  2. `FavoritesCoordinator`: `_shortcuts` wiring, `SeedShortcuts`, `SyncFavorites`,
     `RefreshFamilyFavoriteFlags`, `RebuildFavoritesTree`, `PersistShortcuts`, `_shortcutsPersistable`.
     **Vyřeš u toho A28.**
  3. `MiniAppSessionController`: `AttachMiniAppHost`, `AttachTreeLayout`, `AttachSupportLayout`,
     `PersistMiniAppLayout`, `RestoreMiniAppLayout`, `ReplaceMiniAppSession`. **Vyřeš u toho A20.**
     Pozor: persist při zavření shellu jde přes `EventTrigger Closed` → `CloseAllCommand` v XAML,
     ze strany VM není vidět.
  4. Licence session: `RefreshLicenceNoticeAsync`, `SyncMlineLicenceAfterLoginAsync`,
     `RefreshLicenceAsync` a `_sessionGeneration` do vlastní třídy (např. `SessionLicenceCoordinator`).
  5. Menu commandy (font, theme, `RunEdisonCopy`, `ConnectToDatabase`, `OpenWebUrl`, `ShowAppInfo`,
     chord) do vlastní partial třídy nebo `ShellMenuCommands`.
- **Hotovo když:** VM pod ~400 řádky a orchestruje; sdílené `_suppressXxx` flagy a čítač session
  patří třídě, které se týkají; testy dělené podle nových tříd.
- **Omezení:** `ApplySessionData` musí zůstat **synchronní** (guardy neplatí přes `await`);
  po rozpadu poběží přes hranici dvou tříd a musí zůstat jeden nepřerušený průběh, jinak
  selhání loadu přestane nechávat předchozí stromy naživu. `_modulePrefs` je null do prvního
  `InitializeAsync` → sub-třídy chtějí lazy accessor. `RebuildSessionStateAsync` +
  `SessionDataLoader` zachovat jako jediný rozšiřovací bod session-scoped stavu.

## P2

### A6 · `EDServices.Current` prosakuje do infrastruktury a vypnul paralelizaci `EDISONMAIN2.Tests`

- [ ] **Stav:** otevřeno (2026-10-01: seam pro geometrii oken existuje, default čte dál singleton)
- **P2 · M** · Závisí na: A7 (stejný soubor, dělat před) · Blokuje: - · Původ: [backlog R5](arch-backlog.md#r5---edservicescurrent-prosakuje-do-infrastruktury-a-vypnul-paralelizaci-testů)
- **Problém:** Singleton je záměr u controls (`EDList`, `EDInputForm`, `EDFilter` nesmí záviset
  na `Window.DataContext`), ale šíří se do infrastruktury a oken, a `EDISONMAIN2.Tests` proto
  běží sériově.
- **Důkaz:** `EDControls/dotnet/EDViewModel/WindowNavigator.cs:392-393,411,421-422` (sedm členů
  ve statické `LaunchLegacy`, nově `MayUpdate` a licence pro pole 18-20);
  `EDControls/dotnet/WindowStateStore.cs:62` `internal static Func<IMLRest?> MLRestProvider { get; set; }
  = () => EDServices.Current.MLRest;` (přesunuto z `WindowStatePersistence` v `83cb2472`);
  `EDISONMAIN2/dotnet/Windows/UserProfileWindow/UserProfileWindow.xaml.cs:34,66,70,73` (okno
  i zapisuje `UserDirectory`); `EdisonMainWindow.xaml.cs:71`; nově `Startup/LicenceService.cs:27`
  (fallback `EDServices.Current.PopupDisplayer` v infrastruktuře) a `MiniApps/FontZoomWebView.cs:174`;
  miniaplikace bezparametrickými ctory (`DesktopMiniAppViewModel.cs:19` a další);
  `EDISONMAIN2.Tests/AssemblyInfo.cs:6` `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
  `EDServices` má 13 mutable properties (nově `SessionState`, `24881dad`). Vedle toho
  `EdisonMainViewModel.cs:1511` `SettingsLoader.Load()` z menu handleru přepisuje statické
  `SettingsLoader.Current`.
- **Co už je:** `WindowStatePersistence.Attach(Window, IWindowStateStore)` a
  `EDViewModel.WindowStateStore` (virtual, `83cb2472`) jsou injekční seam blízký bodu (2) návrhu;
  default `CacheWindowStateStore` ale bere MLRest pořád ze singletonu.
- **Dopad:** `LaunchLegacy` a `WireUp` netestovatelné; testy shellu neběží paralelně; vedle
  toho existují další ambientní statiky (`SettingsLoader.Current`, `EDInputForm.PickColorImpl`,
  `ModuleTreeState.IsMlineDomainStation`, nově `SessionFactory.SessionTag`,
  `InternalDiagnostics.Enabled`; viz A10, A21).
- **Návrh opravy:** singleton nerušit. (1) `WindowNavigator` (resp. `LegacyModuleHost` z A7)
  dostane `EDServices` nebo `Func<EDServices>` konstruktorem; composition root ho vytváří první,
  `EDServices` se plní settery, stačí předat instanci. (2) `CacheWindowStateStore(formId, Func<IMLRest?>)`
  jako aditivní ctor, `EDViewModel` mu předá `() => Services.MLRest`. (3) `LicenceService` bez
  fallbacku na singleton (`showInfo` povinný, předá `SessionFactory`). (4) Paralelizaci
  `EDISONMAIN2.Tests` zapnout, `[Collection]` jen na harnessech, které `EDServices.Initialize` reálně volají.
- **Hotovo když:** `EDServices.Current` čtou už jen controls, `RelayCommand` fallback a
  bezparametrické ctory VM; `EDISONMAIN2.Tests` běží paralelně.
- **Omezení:** `EDServices`, `WindowNavigator` a `CacheWindowStateStore` jsou veřejné API EDControls,
  změny aditivní.

### A7 · `WindowNavigator` je tři třídy v jedné, polovina stavu je statická

- [x] **Stav:** vyřešeno cf2895fa (2026-10-05, proud S5) - legacy část přešla do internal `LegacyModuleHost`
  (launch, tracking, dedup, hlášení pádu, ztmavení, kill) se závislostmi v ctoru (`Func<EDServices>`,
  továrna procesu, `fileExists`, `reportExit`, `trackUsage`, UI marshal, owner dialogu, focus); proces
  za šev `ILegacyProcess`, `KilledByShell` je příznak spuštění místo statické `ConditionalWeakTable`.
  Navíc oprava závodu: `Process.Exited` fíruje jednou a `WatchForExit`/`WireDialogBehavior` se
  přihlašovaly až po `TrackExit`, proces skončený mezi tím nechal shell ztmavený; `SystemLegacyProcess`
  odebírá jednou a pozdní odběratel dostane konec hned. `WindowNavigator.cs` 152 ř. bez `Process`, veřejné
  API beze změny, nové `WpfOnly()` používá TD. Testy `LegacyModuleHostTests` (fake proces: dedup,
  `KilledByShell`, `ReportLegacyExit`, PSI kontrakt), `SystemLegacyProcessTests` (reálný `cmd /c exit`).
- **P2 · M** · Závisí na: - · Blokuje: A6 · Původ: nový (2026-09-20)
- **Problém:** WPF navigace (Show/ShowDialog/WireUp), launch legacy procesů (argv, resolve cest,
  pre-flight existence souborů, direct vs. host) a lifecycle child procesů (tracking, dedup, exit
  watch, hlášení pádu, dim shellu, CloseAll) žijí v jedné 504řádkové třídě (2026-09-20: 472);
  launch je statická metoda sahající na singleton, telemetrii i error reporting.
- **Důkaz:**
  - `EDControls/dotnet/EDViewModel/WindowNavigator.cs:34` `private readonly List<LegacyProcessEntry> _legacyProcesses` (instance) vs `:40` `private static readonly ConditionalWeakTable<Process, ExitWatch> _exitWatches` (static): dvě instance by sdílely exit watch, ale ne seznam procesů.
  - `:389` `private static Process? LaunchLegacy(...)` čte `EDServices.Current` (`:392,:411,:421-422`), volá `EDUsageTelemetry.Track("EDISON2.LegacyLaunch", ...)` (`:396`) a přes `TrackExit` → `ErrorReports.ReportLegacyExit` (`:83`).
  - `:93-105` WPF část je 13 řádků; legacy část `:107-243`, `:254-355`, `:389-491` (nově `EnsureExecutableExists` `:486-491`, `45e0835d`; `ShellGuidFor` `:445-446`, `87123386`; pole 18-20, `40684fe4`). `IWindowNavigator.cs:12-67`: 2 členy WPF, 6 legacy + `LegacyModuleExited`.
  - `:212` a `:499` (též `:288-289`, `:340-342`) owner = `Application.Current.MainWindow` (během startu shellu login okno).
  - TD `TemplateDesigner/dotnet/App.xaml.cs:40` `new WindowNavigator()` dostává celý legacy host, který nepoužije.
  - Nové testy pokrývají zase jen statické helpery (`EDControls.Tests/WindowNavigatorTests.cs:115-119` `ShellGuidFor`, `:308-323` `EnsureExecutableExists`).
- **Dopad:** testy `LaunchLegacy`/`WireDialogBehavior` nejde napsat bez `Process.Start`
  a `EDServices.Current`; A6 by injektovalo `EDServices` do 504 řádků.
- **Návrh opravy:** vyseknout `LegacyModuleHost` (launch + tracking + close) s vlastním
  rozhraním a injekcemi (`Func<EDServices>`, `Action<string,string,int> reportExit`,
  `Action<string,string> trackUsage`, factory procesu, `Func<string,bool> fileExists`);
  `_exitWatches` instanční. `WindowNavigator` zůstane fasáda, `IWindowNavigator` beze změny (deleguje).
- **Hotovo když:** `WindowNavigator.cs` pod ~150 řádků bez `Process`; `LegacyModuleHost` má
  test s fake process factory na dedup, `KilledByShell` a `ReportLegacyExit`; TD referencuje
  jen `IWindowNavigator`.
- **Omezení:** veřejné API (`CloseAll`, `TryCloseAll`, `GetActiveModules`, `ResolveExecutablePath`,
  `EdisonDeployRoot`) ponechat jako delegující fasádu; launch kontrakt se nedotýká.

### A8 · EDControls zná deployment a branding EDISONMAIN2 natvrdo

- [x] **Stav:** vyřešeno 9c9ad8af (2026-10-05, proud S5) - `C:\Edison` a `EDISONMAIN2API.exe` jsou jen jako
  defaulty v `LegacyModuleHost`; launcher jde přebít internal parametrem ctoru, deploy adresář vědomě ne
  (launch a `WindowNavigator.ResolveExecutablePath` musí hledat na stejném místě). Veřejný setter není,
  žádná aplikace jinou hodnotu nepotřebuje. `WindowNavigator.EdisonDeployRoot` je `static readonly`. Argv
  začíná `EdisonLaunchArgs.LaunchMarker`, telemetrie launche jde pod `FormIdPrefix` (shell dál
  `EDISON2.LegacyLaunch`), uložené hlášení chyby `<FormIdPrefix>-chyba-…` (TD `TD-chyba-…`), hláška
  o chybějícím souboru jmenuje místo „EDISONu" oba prohledané adresáře (vedle exe i deploy). Zbývá
  fallback `"EDISON2"` v `ErrorReporting/ErrorReports.cs:141` (soubor proudu S9). Testy v
  `LegacyModuleHostTests` (prefix, launcher z deploy adresáře, hláška) a `EDErrorPopupTests`.
- **P2 · M** · Závisí na: - · Blokuje: - · Původ: [backlog R4](arch-backlog.md#r4---edcontrols-natvrdo-zná-deployment-a-branding-edisonmain2)
- **Hotovo mezitím:** `EDServices.FormIdPrefix` (`EDControls/dotnet/EDViewModel/EDServices.cs:61`,
  TD nastavuje `"TD"` v `App.xaml.cs:42`, čte `EDViewModel.cs:146`).
- **Důkaz zbytku:** `WindowNavigator.cs:23` `public const string EdisonDeployRoot = @"C:\Edison"`
  (jediný `public const` v EDControls, který konzument reálně čte:
  `EDISONMAIN2/dotnet/Windows/ChangelogWindow/InfChangelogSource.cs:31`, tedy se inlinuje
  do starého exe; odporuje pravidlu „`static readonly`, ne `const`" v `EDControls/CLAUDE.md:13-15`);
  `:27` `LauncherExe = "EDISONMAIN2API.exe"`; `:396` `"EDISON2.LegacyLaunch"` ignoruje `FormIdPrefix`;
  `:469` literál `EDISONMAIN2` v interpolaci místo `EdisonLaunchArgs.LaunchMarker`;
  `EDErrorPopup.xaml.cs:63` název souboru `EDISON2-chyba-*`; nově `:489-490` hláška „v adresáři
  EDISONu chybí soubor … Kontaktujte správce IS" (`45e0835d`).
- **Návrh opravy:** statická konfigurace nastavovaná v composition rootu (`EDAppIdentity`
  nebo rozšíření `EDServices`: `DeployRoot`, `LegacyLauncherExe`, `LaunchSentinel`, `ProductName`)
  s defaulty dnešních hodnot; `EdisonDeployRoot` převést na `static readonly`; `BuildLauncherArguments`
  používat `EdisonLaunchArgs.LaunchMarker`; telemetrie a název souboru přes `FormIdPrefix`.
- **Hotovo když:** v EDControls není hardcoded `C:\Edison`, `EDISONMAIN2API.exe`, sentinel ani
  `"EDISON2"`/`EDISONu` mimo defaulty konfigurace.
- **Omezení:** aditivní změna API; launch kontrakt beze změny.

### A9 · Session stav v `EDServices` má několik zapisovatelů; licence a `LicenceLoader` putují dvěma kanály

- [ ] **Stav:** otevřeno (2026-10-01: dopad širší, `ActiveLicence` nese i launch pole 18-20)
- **P2 · M** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20), sloučeno ze dvou oblastí
- **Problém:** Ambientní singleton plní bootstrap (`SessionFactory`), VM (`MLRest` znovu,
  `ActiveLicence`, `AccessTable`) i code-behind okna profilu (`UserDirectory`). `LicenceLoader`
  jde zároveň přes `EDServices` a přes `SessionContext`; aktivní licence jde návratem
  z `AcquireSession` do VM, který ji do singletonu zapíše až v rebuildu.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/SessionFactory.cs:52-63` plní `IsAdmin`, `MayUpdate` (nové),
  `UserDirectory`, `UserEmail`, `MLRest`, `UserPrefs`, `LicenceLoader` a týž loader vrací v `SessionContext`;
  `AppBootstrapper.cs:441` a `SessionSwitcher.cs:109` používají `session.LicenceLoader`,
  `CompanySwitcher.cs:84` `services.LicenceLoader!`; VM `EdisonMainViewModel.cs:368` `_services.MLRest = mlRest;`
  (redundantní; harness `EDISONMAIN2.Tests/EdisonMainViewModelHarness.cs:94-95` musí MLRest do
  services dát ještě před ctorem, protože ho `CompanySwitcher` čte lazily); `:418`
  `_services.ActiveLicence = licence;` až v `RebuildSessionStateAsync`; `:497`
  `_services.AccessTable = data.AccessTable;`; nově `:475` `RebuildSessionStateAsync(_services.LicenceLoader?.Invoke())`
  (M-line sync, `c3804594`) vedle `:1559`; `UserProfileWindow.xaml.cs:73` `EDServices.Current.UserDirectory = dir;`.
  Konzument: `EDControls/dotnet/EDViewModel/WindowNavigator.cs:411` `var licence = EDServices.Current.ActiveLicence;`
  plní elementy [12] i nová pole 18-20 (`:423-424`, `40684fe4`), komentář `:412-413` přiznává, že během
  licenčního retry při přepnutí firmy je to „ještě licence původní firmy".
- **Dopad:** mezi `Accepted` (switch, `EdisonMainViewModel.cs:1070`) a `:418` ukazuje `ActiveLicence`
  na starou firmu, zatímco `MLRest.Idfa` už na novou; `Closing` handlery, `BuildLauncherArguments`
  (host z polí 18-20 skládá licenci místo `GetActiveLic`) a `HasAccess` čtou nekonzistentní pár.
  Nový session-scoped údaj = ad hoc rozhodnutí, kdo ho zapisuje (deploy přidal `MayUpdate` do
  `SessionFactory`, licenci do launch kontraktu). Testovací bootstrapper `EstablishSession`
  `services.LicenceLoader` nenastavuje, `CompanySwitcherTests` `ActiveLicence` neasertují.
- **Návrh opravy:** jeden zapisovatel: `SessionFactory`, `LicenceVerifier`/`CompanySwitcher`
  zapíší licenci do `EDServices` hned po `Accepted`, `SessionContext` jen vrací, VM čte;
  smazat ř. 368. Alternativa: `EDServices.LicenceLoader/ActiveLicence` zrušit a předávat kontext
  (breaking, publish).
- **Hotovo když:** `CompanySwitcherTests` asertují `services.ActiveLicence` po `Accepted`;
  harness nepotřebuje předplnit `MLRest`; `grep LicenceLoader` ukáže jednoho zapisovatele.
- **Omezení:** `EDServices` je API EDControls (TD ho sdílí); `AccessLoader` běží až
  v `SessionDataLoader` na klonu, access buď přejde do switcheru, nebo zůstane u VM s explicitním
  komentářem. Pole 18-20 jsou append-only launch kontrakt (A12).

### A10 · Režim startu rozhoduje `#if DEBUG` na devíti místech, runtime volby a settable statiky

- [x] **Stav:** vyřešeno b4b4adf7 (2026-10-01) - `Startup/StartupOptions` (record, defaulty = Release): `DevLogin`,
  `SessionGuidTag`, `SendsVersion`, `DevMiniApps`, `UseDevDefaults`, `InternalDiagnosticsEnabled`, `TestRun`;
  `Development(args, EDISON2_DEVLOGIN)` / `Production()` vybírá jediný `#if` v `App.xaml.cs`, statiky
  (`UseDevDefaults`, `InternalDiagnostics.Enabled`, E2E vypnutí hlášení/telemetrie) nastavuje jen
  `ApplyToProcess`. `SessionFactory.Establish` i `SessionSwitcher` berou options, `SessionTag` zanikl;
  `TestRunOptions` jen parsuje. Nad návrh: `EDISON2_DEVLOGIN=0` pustí v Debug login dialog (Release cestu).
  **Výjimka:** `VerzeInf.Current` drží `#if` dál (soubor S4, source-link do EDUPDATE2). Testy
  `StartupOptionsTests`, `SessionFactoryTests` a `AppBootstrapperTests` bez `#if`, `SessionSwitcherTests`
  (GUID nové session podle options), `MiniAppProviders`.
- **P2 · M** · Závisí na: - · Blokuje: regresní test pro A1 · Původ: nový (2026-09-20)
- **Problém:** Debug a Release jsou dva různé programy: hardcoded login, deterministický GUID,
  prázdná `EdisonVersion`, `DevVersion`, neukládané `LastLoginName`, dev miniaplikace. Release
  cesta (login dialog, progress, handoff) se ve vývoji ani v testech nikdy nespustí. Deploy přidal
  třetí a čtvrtý mechanismus místo sjednocení: runtime `TestRunOptions` (jen pro E2E, parsuje se
  jen v DEBUG) a settable statiky, které nastavuje kdo chce.
- **Důkaz:**
  - `#if` (9 míst, 2026-09-20: 6): `EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:344` (`TryLogin` celý DEBUG/else), `:447` `#if !DEBUG` (`SavePostLoginState`), `:503` (`DevSampleMiniApps`); `SessionFactory.cs:35,44`; `VerzeInf.cs:25`; nově `App.xaml.cs:23` (`TestRunOptions.Parse`, `fb239eb2`), `SettingsLoader.cs:40` (`UseDevDefaults`, `b0f2a38b`), `Windows/EdisonMainWindow/Modules/InternalDiagnostics.cs:12` (`9500b872`); test `EDISONMAIN2.Tests/AppBootstrapperTests.cs:475`.
  - `_startupLoginWindow` je v Debug vždy null (`AppBootstrapper.cs:219` komentář „= null kvůli CS0649 v DEBUG").
  - Runtime volba: `TestRunOptions` předaná bootstrapperu (`AppBootstrapper.cs:37,40`), rozhoduje `:109`, `:249`, `:515-522`; efekty ale aplikuje přes ambientní statiky `TestRunOptions.cs:32-37` (`ErrorReports.Enabled`, `EDUsageTelemetry.Enabled`, `SessionFactory.SessionTag` `SessionFactory.cs:26`, env proměnná widgetu). E2E běh navíc spoléhá na DEBUG hardcoded login (`:109`).
- **Dopad:** A1 leží přesně v Release-only větvi a regresní test na ni nejde napsat bez Release
  buildu; každá změna login dialogu / progress fáze / `HandOffLoginWindow` se ověří jen
  publishem do `C:\Edison\`. Nový režim (E2E, připojení z příkazové řádky) = nové `_testRun is null`
  podmínky rozeseté po bootstrapperu a nové statiky, které testy musí resetovat.
- **Návrh opravy:** `TestRunOptions` rozšířit na `StartupOptions` (`DevLogin?`, `SessionTag`,
  `SendVersion`, `DevMiniApps`, `UseDevDefaults`, `InternalDiagnostics`, `E2E`), sestavit na jednom
  místě (`App.xaml.cs` podle DEBUG / argumentů), předat bootstrapperu a `SessionFactory`; statiky
  nastavuje jen tohle místo. `#if` zůstane jen tam, kde se options tvoří. Release cesta pak jde
  spustit i v dev (např. `EDISON2_DEVLOGIN=0`).
- **Hotovo když:** `grep '#if DEBUG' EDISONMAIN2/dotnet` vrátí jen místo tvorby options;
  `AppBootstrapperTests` bez `#if`; `SessionFactory.SessionTag` není settable static.
- **Omezení:** DEBUG GUID a prázdná verze jsou dokumentované pasti (ACTIVITY zámek na sdíleném
  dev serveru, version gate, `Startup/CLAUDE.md`), sémantiku zachovat, jen přesunout rozhodnutí
  do runtime. Release nesmí dostat skrytý vypínač hlášení chyb (`App.xaml.cs:24`).

### A11 · Setup wizard v produkci předvyplní dev server M-line

- [x] **Stav:** vyřešeno b0f2a38b (2026-09-21) - dev host jen pod
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

- [ ] **Stav:** otevřeno (2026-10-01: kontrakt narostl na 21 tokenů, escapování jen u nových polí)
- **Rozhodnutí vlastníka (2026-10-05):** bod 3 jednostranně v producentovi: `BuildLauncherArguments` odstraní `"` a koncové `\` ze všech textových polí (jako `Plain()` u [20]); host beze změny, kódování jako u [13] se nedělá. Patří do S5 spolu s body 1-2.
- **P2 · M** (escapování s hostem L) · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Pozice 21 tokenů (`[0]` značka + pole `[1]-[20]`) jsou literály ve třech souborech
  ve dvou repech (producent, WPF konzument, WinForms konzument + `ShortcutRunner`) a popsané na
  čtyřech místech; synchronizaci hlídají testy počítající segmenty. Hodnoty z DB (jméno, e-mail,
  adresář) jdou bez escapování a posun jediného pole přehodí identitu session.
- **Důkaz:**
  - Producent: `EDControls/dotnet/EDViewModel/WindowNavigator.cs:469` jeden interpolovaný string 21 tokenů s literálem `EDISONMAIN2`; `40684fe4` přidal pole 18-20 se sanitizací jen pro ně (`:474` `Version()` jen číslice a tečky, `:476` `Plain()` `value.Replace("\"", "").TrimEnd('\\')`); `FullName` [6], vstup [10], elementy [12], e-mail [17] jdou raw, `Dir()` (`:472`) řeší jen koncové zpětné lomítko.
  - Konzument TD: `EDControls/dotnet/EdisonLaunchArgs.cs:36-51` parsuje `args[1..10]`, pole `[11]-[20]` nezná (`:9-11`).
  - Konzument host: `c:\MLineRepos\EDISON\EDISONMAIN2API\FormToRunGetter.cs:48-78` `args[1]`..`args[20]` (EDISON `201f968ca`), `:39` literál `"EDISONMAIN2"`, `ShortcutRunner.cs:51` `args[13]`.
  - Popis pozic: `WindowNavigator.cs:448-463`, `EdisonLaunchArgs.cs:4-11`, `EDISONMAIN2/CLAUDE.md:114,130`, komentáře `FormToRunGetter.cs:57-75`.
  - Strážci: `EDControls.Tests/WindowNavigatorTests.cs:200,212,224,240,256` `Assert.Equal(21, args.Split(" \"").Length)`; `:154` `BuildLauncherArguments_EmbeddedQuote_NotEscaped` dokumentuje, že uvozovka ve `FullName` projde neescapovaná.
- **Dopad:** `FullName` ze serveru s `"` → host načte `Mlp`, `formName`, `shellGuid` z posunutých
  pozic, modul se buď nespustí, nebo pojede s cizím GUID a `CacheLocks=1`. Přidání pole =
  editace 3 souborů + 4 popisů bez kompilátorové vazby (deploy to udělal právě takhle pro 18-20);
  když TD bude potřebovat `isAdmin`/`userEmail`/licenci, musí `EdisonLaunchArgs` doparsovat podle
  jiného souboru.
- **Návrh opravy:** (1) `EdisonLaunchArgs` = jediný kodek: pojmenované indexy (`internal static
  class Field { const int Server = 1; ... }`), `Format(...)` producent i `TryParse` konzument,
  record aditivně o `[11]-[20]`; `BuildLauncherArguments` deleguje a sanitizace (`Plain`) platí pro
  každé textové pole. (2) Sdílená fixture: jeden vzorový argv string commitnutý do testů obou rep.
  (3) 🔸 Politika escapování: buď producent odmítne/nahradí `"` ve všech polích (jednostranné,
  `Plain()` už to pro [20] dělá), nebo se pole přenáší kódovaná jako `[13]` (změna hosta),
  rozhodnutí vlastníka hosta.
- **Hotovo když:** `grep args\[` v `EdisonLaunchArgs.cs`/`WindowNavigator.cs` dá nulu mimo
  tabulku indexů; round-trip test `Format` → `CommandLineToArgv` → `TryParse` s uvozovkou
  ve `FullName`.
- **Omezení:** kontrakt je append-only a zrcadlí `FormToRunGetter` v repu EDISON; bod (3) bez
  vlastníka hosta nedělat; `EdisonLaunchArgs` record je public API TD (aditivní OK).

### A13 · Kontrakty k legacy Caché endpointům: pozicní parsování, testy šva jen pro část

- [ ] **Stav:** částečně (3c6a0e94, 52dff7b1, c3804594, e44439f0, body 2-4 ef05128a; ověřeno 2026-10-01) - integrační
  testy šva (`EDISON2.IntegrationTests`, skutečné MLRest proti dev serveru, při nedostupném serveru
  skutečný skip) pokrývají `ALVA.SYSTEM.Main.LoginAllowed`, `ALVA.SETUP.Main.LoadSettings` (klíče
  `update`, `pending`, `version`, `edisonPath`, `sourceServer`, `sourcePort`, řádky `localPath`),
  `GetEdisonAccess` (index 1 = apId), `GetUserDefaultForm`, `maSeUkoncit`,
  `ALVA.VOZIDLA.LCC.PocetProvozovanychVozidel`, `ALVA.LIBRARY.Form` Load/SaveParams, `Login` jen
  chybovou cestu a `LogLogin` jako smoke. Body 2-4 návrhu hotové 2026-10-01 (z výčtu v Problému
  odpadají klíče `NucenyUpdatClentu`/`localSourcePath`, `LockClearExt` a skip-as-pass):
  (2) `PreLoginTests`: `NucenyUpdatClentu` povinný na DEV (razítko vlny se po prvním spuštění nemaže),
  `localSourcePath` proti řádku `system~updateLocalSourcePath` z `ALVA.EDISON.Main.LoadSettings` (táž podmínka).
  (3) `SessionLockTest` uklízí přes `LockClearExt` s `^||ML("IN",1)=guid` jako `SessionLogout`, zámek cizího GUIDu musí přežít.
  (4) skip je `$$$AssertSkipped` (stav skipped v %UnitTest); `ALVA.EDTesting.TestRunner` ho zatím počítá jako failed
  s prázdnou hláškou (oprava ve vlně 3, sekce Proudy).
  Bod 1 (úspěšný `Login`, testovací účet na DEV) zůstává otevřený. Zbývá viz Problém.
- **P2 · M** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Bez testu šva zůstává: úspěšný `ALVA.SYS.Login.Login` (pozice 1,2,3,4,5,7,10),
  `ChangePassword`, `VerifyLic`, `LoadUser/SaveUser`, `ALVA.EDISON.Main.LoadSettings`,
  `LoadDatabaseList`, `SetUserDefaultForm`, `LockClearExt`, `ResetSettings`, klíče `LoadSettings`
  `NucenyUpdatClentu` (překlep je kontrakt) a `localSourcePath`. Deploy přidal další legacy volání:
  `ALVA.SYSTEM.Main.ChangeLoginState` (`LoginGate.cs:46`, jen unit test), `ALVA.MZDY2.MLAPI.gPocZam`
  (`LicenceService.cs:184`, jen unit test; server přepočítává a zapisuje, proto ho integrační sada
  záměrně nevolá), `ALVA.LICENCE.Activation.GetList` (`LicenceService.cs:78`, bez testu, slouží jako
  otisk) a `EDUsers.UpdateLicFromMlineServer` (`LicenceService.cs:72`, bez testu, volá M-line).
  Caché contract testy hlásí při chybějící třídě pass: 5 míst ve 3 souborech. Guard `SessionLockTest`
  volá `LockClear`, shell posílá `LockClearExt`.
- **Důkaz:** `EDISONMAIN2/dotnet/Windows/LoginWindow/UserAuthenticator.cs:45-53` (pozice 10 =
  UserName, 7 = Email; úspěch kryje jen unit test `UserAuthenticatorTests.cs:42` proti `Login.cls:69`);
  `Startup/Updater.cs:108` `case "NucenyUpdatClentu"`; `EDISON2.IntegrationTests/PreLoginTests.cs:31-45`
  (seznam klíčů bez `NucenyUpdatClentu`/`localSourcePath`); `LoginContractTests.cs:14-30` (jen neznámý
  uživatel); `EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/Tests/LogoutContractTest.cls:20`
  `if '..HasSystemMain() { do $$$AssertTrue(1, "... skip") quit }` (totéž `TelemetryContractTest.cls:29,55`
  a nově `NewShellSwitchContractTest.cls:63,73`, `d69b8efb`); `SessionLockTest.cls:54,67`
  `##class(ALWA.SYS.Lock).LockClear(..#TESTGUID)` vs `Startup/SessionLogout.cs:40`
  `clone.QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", ...)`.
- **Dopad:** změna v repu EDISON (přeuspořádání polí `Login`, přejmenování klíče `NucenyUpdatClentu`)
  se projeví až u uživatele (vynucená vlna by se tiše přestala spouštět); drift `LockClear` vs
  `LockClearExt` by guard nezachytil; skip-as-pass maskuje, že se kontrakt na daném serveru vůbec
  neověřil.
- **Návrh opravy:** (1) `LoginContractTests` o úspěšné přihlášení testovacího uživatele (pozice
  1,2,3,4,5,7,10; potřebuje účet na dev serveru). (2) `PreLoginTests` o `NucenyUpdatClentu`
  a `localSourcePath`. (3) `SessionLockTest` rozšířit o `LockClearExt` přes `^||ML("IN",1)=guid`.
  (4) Skip v Caché testech realizovat explicitním logem místo `AssertTrue(1)`.
- **Hotovo když:** integrační sada hlásí úspěšný `Login` a všechny klíče `LoadSettings`, které
  shell čte; `pwsh -File tools/run-cache-tests.ps1 -Package ALVA.EDISONMAIN2.Tests` má drift test
  na `LockClearExt` a žádné `AssertTrue(1, "... skip")`.
- **Omezení:** endpointy žijí v repu EDISON, shell je nesmí měnit; testy jen ověřují. `gPocZam`
  a `UpdateLicFromMlineServer` mají vedlejší efekty, integrační test je volat nesmí.

### A14 · `EDList` nemá controller seam

- [ ] **Stav:** otevřeno, částečně (wire vrstva vytažena; ověřeno 2026-10-01, beze změny)
- **P2 · M** · Závisí na: - · Blokuje: A16, A17, A27 (dělat spolu) · Původ: [backlog R3](arch-backlog.md#r3---edlist-nemá-controller-seam)
- **Stav v kódu:** `EDListParsing` (commit d8a75403), `EDListSort`, `EDListFilter`, `ColumnLayout`
  jsou vytažené; `EDList.xaml.cs` má 729 řádků, volá MLRest přímo (`:269,:280,:295,:313`), stav
  (`_sortSpecs` `:503`, `_checkAnchor` `:216`, layout persist, checked state) žije jen ve view.
- **Návrh opravy:** `EDListController(IMLRest, string cacheClass)` po vzoru `EDInputFormController`:
  `InitializeList()` → sloupce, `LoadList(filter)` → řádky (formátování, sort keys),
  `LoadLayoutAsync`/`SaveLayoutAsync` nad `IUserPrefsService` a `ColumnLayout` DTO. V code-behind
  zůstane mapování na `DataGrid.Columns` a event wiring. Hlavní přínos: testy na lifecycle
  `LoadList` (reset checked state, unsubscribe starých řádků, re-apply search a layoutu), které
  `EDList/CLAUDE.md` dnes vypisuje jako pasti a nic je nehlídá.
- **Hotovo když:** instanční logika testovatelná proti `IMLRest` mocku; code-behind pod ~400 ř.
- **Omezení:** veřejné API `EDList` aditivně.

### A15 · `EDInputForm` je celý synchronní na UI vlákně, `EDList` má async cestu

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01)
- **Rozhodnutí vlastníka (2026-10-05):** aplikuje se jen odpověď posledního `ValidateAsync`/`InitAsync` (generation guard); hodnotu pole, které uživatel právě edituje, odpověď nepřepíše.
- **P2 · M** · Závisí na: A3 · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `EDList` má `InitializeListAsync/LoadListAsync` s gate a generation guardem,
  `EDInputForm` volá Caché výhradně synchronně včetně validace na každou změnu pole.
- **Důkaz:** `EDControls/dotnet/EDInputForm/EDInputFormController.cs:29` Init, `:47` Save,
  `:70` Validate, žádný `Task`; `EDInputForm.xaml.cs:716-717` `Dispatcher.BeginInvoke(new Action(() =>
  Controller.Validate(field.Key)));` (odložené, stále blokující HTTP na UI) a `:561`
  `if (field.ValidateOnChange) Controller.Validate(field.Key);` (přímo). Kontrast
  `EDList.xaml.cs:276-282,306-317,324-332` (semafor + `_loadGeneration`) a `EDList/CLAUDE.md:8-10`
  „synchronní varianty zmrazí celou aplikaci".
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

- [x] **Stav:** vyřešeno d6acfadd (2026-10-02) - bez sloupců `LoadList` hází `InvalidOperationException`
  (text neviní hosta, vede k tomu i selhaný `InitializeListAsync`). Async cesta kontroluje až po
  odpovědi, protože TD picker pouští `InitializeListAsync` a refresh `EDFilter`u souběžně a pořadí
  drží `_queryGate`; sync `LoadList` frontu obchází, kontroluje před dotazem. Vedlejší efekt
  z důkazu: přestavba sloupců řazení **drží** a šipky obnoví (`RestoreSortArrows`), spec zmizelého
  sloupce zahodí; `ApplyLayout` nově před sortem z layoutu vynuluje všechny šipky (jinak by po
  obnově svítila i šipka řazení, které layout přebil, a další save by ji zapsal). Testy
  `EDListLoadTests` (sync i async bez init, souběh init a load), `EDListSetDataTests` (šipka
  a pořadí po přepnutí `SelectionMode`, zahození řazení, layout s jiným sortem). **Publish
  EDControls.dll** mění chování jen hostům bez init, v repu žádný.
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `LoadList`/`LoadListAsync`/`SetData` bez předchozích sloupců projde bez chyby,
  řádky se naplní do `Rows`, ale `DataGrid.Columns` zůstane prázdný.
- **Důkaz:** `EDControls/dotnet/EDList/EDList.xaml.cs:292-296` `LoadList` → `ApplyRows`; `:356`
  `ParseRows(output, _columns)` s `_columns == null`; `EDListParsing.cs:96-104,113` tolerantně vrací
  raw; `RebuildColumns` (`:374-388`, `if (_columns is null) return;` `:382`) se volá jen z `ApplyColumns`,
  `SetData`, `OnSelectionModeChanged` (`:147-148`); `EnsureConfigured` (`:555-560`) kontroluje jen
  MLRest + `CacheClass`. Vedlejší efekt téže třídy stavu: `OnSelectionModeChanged` → `RebuildColumns`
  nenuluje `_sortSpecs` ani `SortDirection` (`:503-535`).
- **Dopad:** nový host zavolá jen `LoadListAsync` a dostane prázdný seznam bez výjimky; ladí se
  to na serveru, kde je všechno v pořádku. `EDListLoadTests` to nechytí, kontrolují `Rows`.
- **Návrh opravy:** v `ApplyRows` při `_columns is null` vyhodit `InvalidOperationException`
  s hláškou „zavolej InitializeList/SetData" (všichni tři dnešní hosté inicializují správně:
  TD pickery, `EDListTestWindow`, `DashboardListSource`).
- **Hotovo když:** test `LoadList_WithoutInitialize_Throws`.
- **Omezení:** výjimka místo ticha je breaking jen pro chybné hosty, v repu žádný.

### A17 · Wire slovník `EDList` nemá sdílenou definici ani parity test

- [x] **Stav:** vyřešeno 5f21bf27 (2026-10-05) - nový `CACHE/EDList.inc` (`$$$ListColumnType*`, nad rámec
  návrhu i `$$$ListAlign*`, protože `ParseAlign` padá na Auto stejně tiše) s výčty `$$$ListColumnTypes`/
  `$$$ListAligns`; `List` neznámý typ i zarovnání odmítne `$$$ThrowStatus` v `AddColumn` i ve
  `WriteColumnsToOut` (typ jde nastavit i přímo na vráceném sloupci), porovnání bez ohledu na velikost
  písmen (C# parsuje stejně), prázdné = default. `MockList` jede na makrech, `Column.cls` odkazuje na
  `.inc`. C# literály v jedné mapě `EDListParsing.ColumnTypeWire`. Testy `ListTest` (parita konstant,
  odmítnutí typu a zarovnání, typ nastavený přímo, přijetí všech konstant i malých písmen),
  `EDListParsingTests` (`[Theory]` přes literály vč. fallbacku, každá hodnota enumu má literál,
  `ParseAlign`). Nasazeno z worktree, voláním ověřené `MockList` i TD `EDList.Sablony`/`Instance`;
  `EDList.inc` přidán do Caché projektu `ALVA_EDControls` na DEV (bez něj se patch u zákazníka
  nezkompiluje), se souhlasem uživatele doplněn i do seznamu členů v `docs/deploy.md` (soubor S4).
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** Pro `EDInputForm` existuje `EDInputForm.inc`, `FormTest.TestWireConstantsMatchCSharp`
  a C# `[InlineData]`; pro `EDList` je jediným kontraktem komentář v `Column.cls` a `switch` v C#,
  který neznámý typ tiše mapuje na TEXT.
- **Důkaz:** `EDControls/CACHE/ALVA/EDControls/EDList/Column.cls:9-10` `/// Datový typ: TEXT |
  NUMBER | DATE | DATETIME | BOOL | IMAGE | COLOR | HHMM | CODEBOOK` + `Property type As %String`
  bez validace; `List.cls:16` volný string; `EDControls/dotnet/EDList/EDListParsing.cs:58`
  `_ => ListColumnType.Text`; C# testy pinují jen TEXT/NUMBER/BOOL/DATE/HHMM/COLOR
  (`EDControls.Tests/EDListFormattingTests.cs:101-127`); Caché `ListTest.cls` jen echo stringů
  (`:8-23`); formáty hodnot (NUMBER s tečkou, DATE ISO, BOOL 1/0, HHMM int, COLOR `R;G;B`) žijí
  jen v `EDList/CLAUDE.md:34-36` (`docs/wire-formaty.md:150-151` na něj jen odkazuje).
- **Dopad:** překlep na serveru = sloupec jako raw ISO text s lexikografickým řazením bez jediné
  chyby; nový typ přidaný na jedné straně se na druhé neprojeví.
- **Návrh opravy:** `CACHE/EDList.inc` s `$$$ListColumnType*` konstantami; `List.AddColumn`
  validuje proti nim (throw); Caché `ListTest` parity test podle vzoru `FormTest:311-333`;
  C# `[Theory]` přes všechny hodnoty `ListColumnType` včetně `ParseType` fallbacku.
- **Hotovo když:** oba testy zelené, `MockList.cls` používá makra.
- **Omezení:** hodnoty literálů jsou wire kontrakt, nemění se (aditivní `.inc`).

### A18 · Login workflow rozprostřený do čtyř míst, bez VM

- [x] **Stav:** vyřešeno 8032163f (2026-10-05) - `LoginWindow/LoginViewModel` (`EDViewModel`, `PersistedFormId` null):
  režimy `ForStartup` (předvyplnění z prefs, zůstává jako progress podklad) a `ForReauth` (modální
  dialog), `Login(password)` a `ApplyRememberedPassword` (zapomenutí odmítnutého i uložení po úspěchu
  na jednom místě; do prefs až po uložení geometrie, ta ukládá celé `local.json`), `IsInProgress` (veto zavření), `ShowProgress`,
  `CompleteHandoff`, `Cancel`. Okno jen bindingy, `PasswordBox` a fokus. Bootstrapper drží VM
  (progress, handoff) a okno už jen jako owner dialogů a pro geometrii; vykreslení progressu
  (`Dispatcher.Invoke(Render)`) dělá on, protože blokuje UI thread. **Odchylka od návrhu:** login
  synchronně, ne `LoginAsync` - pre-login MLRest není klon a async by otevřel souběh s Cancel.
  Testy `LoginViewModelTests` (19); ručně Debug s `EDISON2_DEVLOGIN=0`: předvyplnění a Storno.
- **P2 · M** · Závisí na: A10 · Blokuje: - · Původ: [backlog N3](arch-backlog.md#n3---login-workflow-rozprostřený-do-čtyř-míst-bez-vm)
- **Stav v kódu:** `LoginWindowLogic.cs` (31 ř., rozhodnutí fokus/zapomenout heslo) vytažen;
  `LoginWindow.xaml.cs:105` provádí autentizaci, `:123-127` politiku zapamatovaného hesla,
  `:139-165` progress state machine startupu (`EnterProgressState`, `ShowProgress`,
  `CloseAfterHandoff`); bootstrapper ho řídí zvenčí přes `DispatcherFrame`
  (`AppBootstrapper.cs:360-364`), sdílí s ním geometrii shellu (`:357-358` `Attach` read-only,
  `:382` `Save`, `83cb2472`) a `_startupLoginWindow` čte 6 míst (`:124,:145,:155,:170,:406,:562`).
- **Dopad:** jediný netriviální workflow shellu bez VM a bez testu; A1 (chybová hranice)
  a A10 (Release cesta) na něj přímo navazují.
- **Návrh opravy:** `LoginViewModel` s `LoginAsync`, `RememberPassword`, progress stav; okno
  bindingy; bootstrapper komunikuje s VM, ne s oknem. Detail a historie: backlog N3.
- **Omezení:** login okno zůstává jako progress podklad do `ContentRendered` shellu
  (`EDISONMAIN2/dotnet/Startup/CLAUDE.md`); `HandOffLoginWindow` pořadí MainWindow → ShutdownMode;
  geometrii login okno jen čte, zapisuje shell a jednou bootstrapper.

### A19 · `UserProfileWindow` opakuje vzor A18: workflow v code-behind, VM dostává callback z okna

- [x] **Stav:** vyřešeno 2f23a5b2 (2026-10-05) - plnění formuláře (`FillForm`), uložení (`OnOk` → server validace →
  `ValidateExportDir` → `SaveExportDir` → `UserDirectory`) a validace složky jsou ve VM, singleton čte jen
  bezparametrický ctor VM. Místo delegátu `SaveRequested` port `IUserProfileForm` (`Save`, `Validate`,
  `Get`, `Set`), který okno implementuje nad `TheForm.Items`; okno jen mapuje pole a tlačítka formuláře.
  Popup o neexistující složce má nově za ownera dialog profilu, ne shell. Testy `UserProfileViewModelTests`.
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

- [x] **Stav:** vyřešeno bc42f82d (2026-10-02) - jinak než návrh: místo `BeginSessionSwap()` se layout ukládá
  jen při `Closed` shellu a jen pod firmou, jejíž layout dock drží (`_dockIdfa`, null = dock vyprázdněný
  výměnou session); `CloseAllCommand` a `_sessionSwapInProgress` zrušeny, přepnutí uživatele z 569b3454
  jede stejnou cestou. Od 24f497ef v `MiniAppSessionController`.
- **P2 · S** · Závisí na: - · Blokuje: A5 krok `MiniAppSessionController` · Původ: [backlog N2](arch-backlog.md#n2---persistence-layoutu-miniaplikací-má-tři-vstupy-a-guard-jen-v-jednom)
- **Stav v kódu:** guard `_sessionSwapInProgress` je uvnitř `PersistMiniAppLayout`
  (`EdisonMainViewModel.cs:187`), takže kryje všechny vstupy. Vstupy zůstávají tři:
  `Closing` → `ConfirmShellClose` (`:1421-1426`, persist `:1424`), XAML `EventTrigger Closed` →
  `CloseAllCommand` (`:284`, `EdisonMainWindow.xaml:20-21`), `SwitchCompanyAsync`/`ReplaceMiniAppSession`
  (`:1084-1087,1105`, `:236-242`); při normálním zavření se layout ukládá dvakrát (Closing i Closed).
  Persist nově nese i výšku pruhu podpory (`:194-195`, restore `:209-210`, `f821f574`). Vedle guardu
  přibyl druhý mechanismus téhož významu: `_sessionGeneration` + `InvalidatePendingSessionWork()`
  (`:47`, `:488`, volání `:1057,:1440,:1555`, `c3804594`).
- **Návrh opravy:** sjednotit do jedné cesty s jedním guardem **před** extrakcí controlleru,
  jinak extrakce tiše ztratí jednu větev; `BeginSessionSwap()` vracející `IDisposable`, který
  zároveň zneplatní pozaďovou práci (nahradí ruční `InvalidatePendingSessionWork()`).
- **Hotovo když:** test „zavření shellu uloží layout právě jednou"; `InvalidatePendingSessionWork`
  volá jen `BeginSessionSwap`.
- **Omezení:** persist při zavření shellu musí přežít i cestu přes `EventTrigger Closed`.

### A21 · Strom modulů závisí na ambientních faktech stanice bez injekce

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01; přibyla statika `InternalDiagnostics.Enabled`)
- **P2 · S** · Závisí na: A5 (ideálně součást extrakce) · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** `FillFilteredTree` míchá do datového modelu výsledky `File.Exists` a DNS/registry
  detekce domény; ani jedno nejde přes ctor VM podstrčit.
- **Důkaz:** `EdisonMainViewModel.cs:836-840` `ModuleTreeState.TryFilter(module,
  _services.ActiveLicence, _services.AccessTable, ModuleTreeState.IsMlineDomainStation)` a
  `ModuleLaunchabilityCheck.Mark(filtered)`; `Modules/ModuleTreeState.cs:16` `internal static
  readonly bool IsMlineDomainStation = MlineDomainDetector.IsDomainMline();` (type init, nastavení
  `DomainNameProvider` v testu po prvním dotyku nic nezmění); `Modules/ModuleLaunchabilityCheck.cs:95-96`
  `File.Exists(WindowNavigator.ResolveExecutablePath(exe.ExecutablePath))` (fallback `C:\Edison`);
  suffixy nově za `InternalDiagnostics.Enabled` (`ModuleLaunchabilityCheck.cs:89`, `InternalDiagnostics.cs:11-16`,
  settable static s Release defaultem `IsMlineDomainStation`, `860b9ffe`); testy ho přepínají
  (`ModuleLaunchabilityCheckTests.cs:15`), VM testy dál jen `StartsWith`
  (`EDISONMAIN2.Tests/EdisonMainViewModelTests.cs:46-50`).
- **Dopad:** test s `mlineOnly` modulem projde na stroji v doméně m-line a spadne jinde;
  test na přesný `DisplayedName` nejde napsat; `File.Exists` pro každý modul běží synchronně
  na UI vlákně při každém rebuildu (login, přepnutí firmy, refresh licence, nově i M-line sync).
- **Návrh opravy:** `FillFilteredTree` dostane strategii (`Func<IModule, IModule> decorate` +
  `bool isMlineDomain`) přes ctor s produkčním defaultem v `AppBootstrapper`;
  `IsMlineDomainStation` a `InternalDiagnostics.Enabled` z statiky na parametr (souvisí s A10).
- **Hotovo když:** harness předá identitu a test může asertovat přesný název.
- **Omezení:** `MlineDomainDetector.Suffix` a barvy stub/broken jsou vizuální parita s legacy;
  ctor VM má 15 parametrů, další přidávat spíš jako součást A5.

### A22 · Dokončit odstranění `EDInputForm.Initialized`

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01)
- **P2 · S** · Závisí na: koordinovaný publish obou aplikací · Blokuje: - · Původ: [backlog R6b](arch-backlog.md#r6b---dokončit-odstranění-edinputforminitialized)
- **Stav v kódu:** `EDInputForm.xaml.cs:367-368` `[Obsolete] public new event Action? Initialized;`,
  fíruje `:390-392`; jediný odběratel je `EDControls.Tests/EDInputFormViewTests.cs:61`, TD ani shell
  žádný. Brzdí už jen koordinovaný publish; dávkovat s A25.
- **Hotovo když:** event je pryč, oba exe publikované ze stejného commitu.

### A23 · Mechanismus persistence preferencí zkopírovaný v `EDList` a `EDFilter`

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01, kód beze změny)
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-09-20)
- **Problém:** identický kód (500 ms `DispatcherTimer`, `Stop/Start`, `await Save` v `Unloaded`,
  `catch { /* best-effort */ }`) ve dvou controls, každý s vlastním `PrefsScope` a vlastním
  `ServicesOverride` seamem (třetí v `EDInputForm`).
- **Důkaz:** `EDControls/dotnet/EDList/EDList.xaml.cs:172-181,562-629` (timer `:610`, tichý catch
  `:628`, `:665`) vs `EDControls/dotnet/EDFilter/EDFilter.xaml.cs:148-150,184-193,302-331`; seamy
  `EDList.xaml.cs:29-30`, `EDInputForm.xaml.cs:30-31`, `EDFilter.xaml.cs:43-44`. Od `5fcfa28e`
  `CacheUserPrefsService.Load` při selhání transportu hází a obě kopie výjimku tiše polykají.
- **Dopad:** změna politiky (interval, chování při chybě, logování místo tichého catch, flush
  při zavření okna) se dělá na dvou místech a rozejde se.
- **Návrh opravy:** `internal sealed class DebouncedUserPrefsWriter(IUserPrefsService, scope,
  Func<JsonNode> snapshot)` se `Schedule()` a `FlushAsync()`; oba controls ho použijí.
- **Hotovo když:** stávající `EDListSetDataTests`/`EDFilterTests` zelené + jeden test debounceru
  bez WPF.
- **Omezení:** žádné veřejné API, formát blobů beze změny.

### A44 · Kontrakt update mechanismu nemá vlastní modul, žije v source-lincích a literálech dvou updaterů

- [x] **Stav:** vyřešeno df49af95 (2026-10-05) - projekt `EDUpdateContract` bez referencí nese `VerzeInf`,
  `ForcedStamp`, `UpdaterArgs` (+ `ToTokens`, klíče z `nameof`) a `UpdaterContract` (`ClientUpdaterKind`,
  exe, soubor stopy, `IsUri`, `FileUrl`, `BuildArguments` obou variant); referencují ho obě exe, test projekty
  přes ně (globální `using`), source-linky zmizely, publish obou exe ho zabalí. Změna návrhu u http:
  vlastník zvolil, že se EDUPDATE2 naučí http zdroj, fallback na legacy proto není. Parita s legacy
  `UPDATERCL`: manifest povinný, vynucená vlna = celý manifest bez kontroly hashe (hotfix manifest nemění),
  chyba stažení `EDISONini.xml`/`MLServerConfig-data.xml` přeskočena; nad rámec legacy kontrola hlavičky `MZ`
  u `.dll`/`.exe` vynucené vlny a tělo souboru čtené s tokenem a idle timeoutem 60 s. Test „pro každý `ClientUpdaterKind`" je dvojice `UpdaterTests.Check_SourceLetThroughToUpdate_*`
  (UNC, lokální, http, https projdou `Check` beze změny do argumentů obou updaterů) a `UpdateEngineHttpTests`
  se švem `HttpSourceSeamTests` proti lokálnímu HTTP serveru. Literál `"EDISON.exe"` v `App.xaml.cs` (S2) zůstal.
- **P2 · M** · Závisí na: - · Blokuje: přepnutí `Updater.ActiveKind` na EDUPDATE2 · Původ: nový (2026-09-20)
- **Problém:** Kontrakt shell ↔ updater žije ve vzájemných source-lincích dvou exe a v literálech.
  Od `b4ec0b3b` shell spouští legacy EDUPDATE (repo EDISON) a EDUPDATE2 drží jen pro budoucí přepnutí
  konstantou; oba kontrakty (argumenty, kódování, soubor stopy vlny) jsou ve stejné třídě `Updater`
  bez společného modelu a EDUPDATE2 neumí všechno, co shell mezitím začal posílat.
- **Důkaz:**
  - Source-linky beze změny: `EDUPDATE2/dotnet/EDUPDATE2.csproj:21` kompiluje `EDISONMAIN2/dotnet/Startup/VerzeInf.cs` (namespace `EDISONMAIN2`); `EDISONMAIN2/dotnet/EDISONMAIN2.csproj:27` kompiluje `EDUPDATE2/dotnet/ForcedStamp.cs` (namespace `EDUPDATE2`); `EDISONMAIN2.Tests/EDISONMAIN2.Tests.csproj:31` kompiluje `EDUPDATE2/dotnet/UpdaterArgs.cs`; `EDUPDATE2/dotnet/UpdateEngine.cs:3` `using EDISONMAIN2;`.
  - Přepínač: `EDISONMAIN2/dotnet/Startup/Updater.cs:79` `public const ClientUpdaterKind ActiveKind = ClientUpdaterKind.LegacyEdupdate;`; větve `Edupdate2` jsou dosažitelné jen z testů (`EDUPDATE2/CLAUDE.md:4` „Shell ho zatím nevolá").
  - HTTP zdroj: shell ho přijímá a updater stahuje sám (`Updater.cs:219-221` `IsUri`, `:315-332`, `00de556b`), `UpdateEngine.cs:176-178` `Validate` ho odmítá („HTTP zdroj není podporován"); kontrola `http:`/`https:` je napsaná dvakrát.
  - Argumenty: legacy sada `Updater.cs:361-373` (`FormMode=1`, `Async=1`, `RunEdison=1`, …, `WaitProcess`, `RunExe=EDISON.exe`), konzument v repu EDISON, strážce jen test imitující legacy parser (`EDISONMAIN2.Tests/UpdaterTests.cs:712-742`); sada EDUPDATE2 `Updater.cs:375-384` vs `EDUPDATE2/dotnet/UpdaterArgs.cs:38-44`, round-trip `UpdaterTests.cs:774`. `NucenyUpdate` má dvě kódování (`True/False` `:369`, `1/0` `:382`, `UpdaterArgs.cs:43` `value == "1"`).
  - Stopa vlny: `EDUPDATE2/dotnet/ForcedStamp.cs:14` `LegacyFileName = "CliUp.ini"`, čte ji `Updater.cs:290-307`; source-link tedy slouží i formátu souboru legacy updateru.
  - Literály dvakrát: `"EDISON.exe"` (`App.xaml.cs:18`, `Updater.cs:82`), `"EDISONMAIN2"` (`UpdaterArgs.cs:13`, `Updater.cs:83`).
- **Dopad:** přepnutí `ActiveKind` na EDUPDATE2 (plánované, `docs/deploy.md:32,37`) pošle stanicím
  s http zdrojem `SourcePath=http://…`, EDUPDATE2 skončí na validaci a shell při každém startu
  znovu vyhodnotí `UpdateRequired`, takže se stanice neaktualizuje ani nespustí; deploy.md tuhle
  podmínku nezná. Změna `VerzeInf` pro potřeby shellu (závislost na EDControls nebo MLRest) rozbije
  invariant „EDUPDATE2 bez závislostí" až při buildu updateru; nový argument = 2-3 soubory ve dvou
  exe a jednom cizím repu + tabulka v `EDUPDATE2/CLAUDE.md`; assembly EDUPDATE2 nese cizí namespace.
- **Návrh opravy:** malý projekt bez závislostí (např. `EDUpdateContract`: `VerzeInf`, `ForcedStamp`,
  `UpdaterArgs` + konstanty klíčů a exe, `BuildArguments` pro obě varianty vedle `Parse`, `IsUri`),
  referencovaný z obou exe i z obou test projektů. Single-file publish ho zabalí do exe
  (`SingleFilePublish.targets` uvolňuje jen `EDControls`), takže invariant „žádná volná DLL
  z `C:\Edison`" platí dál. Před přepnutím `ActiveKind`: buď EDUPDATE2 naučit http zdroj, nebo
  shell u http zdroje dál spouští legacy EDUPDATE; podmínku zapsat do `docs/deploy.md`.
- **Hotovo když:** žádný `<Compile Include="..\..\<jiný projekt>` v csproj; `EDUPDATE2` bez
  `using EDISONMAIN2`; `dotnet publish` EDUPDATE2 dá jeden exe; test „pro každý `ClientUpdaterKind`
  je každý zdroj, který `Updater.Check` pustí k updatu, přijatelný pro daný updater".
- **Omezení:** EDUPDATE2 nesmí referencovat nic z `C:\Edison`; klíče argumentů a exit kódy
  0/1/100/200 jsou kontrakt; argumenty legacy EDUPDATE a formát `CliUp.ini` určuje repo EDISON.

### A45 · `local.json` sdílí víc instancí shellu, každý zápis přepíše celý soubor

- [x] **Stav:** vyřešeno a5774bd5 (2026-10-01) - oprava jen uvnitř `LocalPrefs`, bez `ILocalPrefs.Update` a beze změny
  volajících: `Load` si drží snímek, `Save` pod pojmenovaným mutexem (`Local\` + hash cesty, bere ho i `Load`)
  soubor znovu načte a tříbodově sloučí - property změněná proti snímku vyhrává naše, jinak disk; slovníky
  (podle JSON kontraktu) po klíčích včetně smazaných, seznamy a objekty celé (`Connections` tedy dál vyhrává
  celý seznam); klíče novější verze se zachovají; zápis přes dočasný soubor + `File.Move`; paměť i snímek pak
  = výsledek. Poškozený disk zastoupí snímek (zapíše se celý náš stav); nečitelný (I/O, přístup) zápis
  odloží na příští `Save`, `Load` vrátí defaulty s prázdným snímkem. Klíč `server|idfa` a jeho test
  z „Hotovo když" nedělány, čekají na A37. Testy `LocalPrefsTests` (dvě instance nad jedním souborem, souběh
  vláken, zamčený soubor). **Přijaté riziko:** `LastLoginName` a `RememberedPassword` se slučují zvlášť, takže
  dvě instance přihlášené současně pod různými jmény můžou nechat jméno jedné a heslo druhé; následek je jen
  nepředvyplněné heslo (blob je vázaný na jméno).
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-10-01)
- **Problém:** `LocalPrefs` se načte jednou při startu a `Save()` zapíše celý objekt. Shell přitom
  běží ve víc instancích pod jedním Windows účtem („Spustit kopii EDISONu", nově „Připojit
  k databázi" s jiným serverem) a každá drží vlastní snímek. Klíče `MiniAppLayouts`, `TreeWidths`
  a `SupportStripHeights` jsou jen IDFa, bez serveru.
- **Důkaz:**
  - `EDISONMAIN2/dotnet/Startup/LocalPrefs.cs:90-102` `Load()` celého souboru, `:104-108` `Save()` = `File.WriteAllText` celého objektu, bez zámku a bez sloučení.
  - Zápisy celého snímku: `EdisonMainViewModel.cs:196` (`PersistMiniAppLayout`, i při každém zavření shellu), `:227`, `:308`; `Windows/ConnectionWindow/ConnectionViewModel.cs:97-103` (uložená připojení hned po změně); `Startup/CompanySwitcher.cs:113`; `Startup/ShellWindowStateStore.cs:29`; `AppBootstrapper.cs:372,452`.
  - Druhá instance: `EdisonMainViewModel.cs:1481-1492` (`RunEdisonCopy`), `:1499-1524` (`ConnectToDatabase` → nová instance s `server=`/`restport=`, `51112233`).
  - Klíč bez serveru: `EdisonMainViewModel.cs:191-195` `(_prefs.MiniAppLayouts ??= new())[idfa] = ...`.
- **Dopad:** instance B přidá uložené připojení, instance A se pak zavře a `PersistMiniAppLayout`
  zapíše svůj starší snímek → připojení zmizí. Totéž layout docku, šířka stromu, výška pruhu,
  geometrie okna a `LastIdfa`: vyhrává instance zavřená poslední. Instance připojená k jinému serveru
  se stejnou IDFa (typicky testovací a produkční databáze téže firmy) čte a přepisuje layout cizí
  databáze.
- **Návrh opravy:** `ILocalPrefs.Update(Action<LocalPrefs> mutate)` = pojmenovaný mutex + reload
  souboru + mutace + zápis; volající mění jen svůj klíč, `Save()` celého snímku zmizí. Per-IDFa klíče
  rozšířit o server (`server|idfa`) se čtením starého klíče jako fallback (bez migrace, shell zatím
  nikde neběží).
- **Hotovo když:** test dvou instancí `LocalPrefs` nad jedním dočasným souborem: změna každé
  přežije zápis té druhé; test, že layout pro `serverA|1` nečte instance proti `serverB|1`.
- **Omezení:** nullable property kompatibilita `local.json` (root `CLAUDE.md`); `ShellWindowBounds`
  sdílí login okno (jen čte, `Startup/CLAUDE.md`); rozhodnutí o scope layoutu je A37.
- Priorita P2, ne P1: je to chyba, ale ztrácí jen lokální preference, které jdou nastavit znovu.

### A46 · Obsah exe shellu závisí na `C:\Edison` stroje, kde se publikuje 🔸

- [ ] **Stav:** otevřeno, částečně (a4025673, 2026-10-05) - body 1 a 2 hotové: target `RejectDebugLegacyAssemblies`
  v `SingleFilePublish.targets` čte `DebuggableAttribute` legacy DLL z `C:\Edison` (inline task, bajty
  ve sběratelném `AssemblyLoadContext`, `System.Reflection.Metadata` factory nedostane) a Release
  publish s Debug buildem shodí, nepodepsanou DLL s nečitelnými atributy taky (podepsané cizí C1 přeskočí),
  pod MSBuildem z VS odmítne publish s legacy DLL; ventil `-p:AllowDebugLegacyAssemblies=true`, test
  `SingleFilePublishTargetsTests` s vyrobenou DLL a skutečným `dotnet publish`. `global.json` 10.0.302
  `latestPatch`. Bod 3 (vydávací stroj) vlastník 2026-10-01 odložil.
- **Rozhodnutí vlastníka (2026-10-05):** bod 3: shell a EDControls se vydávají jen z jednoho stroje, který vlastník zapíše do `docs/deploy.md`.
- **P2 · S** (kontrola a pin SDK; vydávací stroj 🔸 M) · Závisí na: - · Blokuje: - · Původ: nový (2026-10-01), incident při nasazení 29. 9.
- **Problém:** Legacy knihovny jsou reference přes `HintPath` do `C:\Edison` a single-file publish je
  zabalí do `EDISONMAIN2.exe`. Exe tak nese kopie, které na publikujícím stroji zrovna leží,
  v jakékoli konfiguraci a verzi, a build to nijak neověřuje. SDK v repu není připnuté, takže dva
  stroje ze stejného commitu vyrobí různé binárky.
- **Důkaz:**
  - `EDISONMAIN2/dotnet/EDISONMAIN2.csproj:37-51` `HintPath` `C:\Edison\EDISONAPI.dll`, `EDLicence.dll`, `EDUsers.dll`, `MLRest.dll`; `EDCore` a `EDLibrary` přicházejí tranzitivně a bundle je nese taky (ověřeno bajtově 29. 9.); `SingleFilePublish.targets:11-12` uvolňuje z bundlu jen `EDControls`.
  - Při nasazení 29. 9. ležely v `C:\Edison` Debug buildy šesti legacy DLL (`DebuggableAttribute` 263). Shell z nich ukazoval dialogy `ERROR`/`IO ERROR` a licenci z M-line stahoval z `127.0.0.1:57772` (`EDISON/Licence/EDLicence/Update.vb:58-61`, `Api.vb:51-61`, `EDISON/EDUsers/EDUsers/MUtils.vb:27-35`). Zachyceno ručně před vydáním, publish proběhl bez varování.
  - Bundle zastarává proti volným DLL vydání: TEST FULL 261002.0 (2. 10.) nese novější volnou `EDCore.dll`, `EDComponents.dll` a `EDAPI.dll` od kolegů, `EDISONMAIN2.exe` v ní je dál build z 29. 9. se zabalenou starší `EDCore` (ověřeno porovnáním instalace serveru se stanicí 260929.4.1).
  - V kořeni repa chybí `global.json`; `EDControls.dll` téhož zdroje postavená jiným strojem nese WPF 10.0.11 proti 10.0.10 ([post mortem](2026-09-29-vydani-shellu-post-mortem.md) K3.7). Compiler Debug build single-file exe nepozná ([audit integrace](2026-09-29-integrace-shellu.md) I24).
- **Dopad:** vydání může nést Debug legacy kód (jiné chování, hlášky, adresy) nebo jinou verzi legacy
  knihovny, než jakou mají zákazníci vedle exe, a nikdo to nepozná. Vydaný build jde reprodukovat jen
  na stejném stroji.
- **Návrh opravy:** (1) target v `SingleFilePublish.targets` po `ComputeResolvedFilesToPublishList`,
  který u referencí z `C:\Edison` přečte `DebuggableAttribute` (`System.Reflection.Metadata`) a při
  `Configuration=Release` shodí publish se jménem DLL; (2) `global.json` s pinem SDK
  (`rollForward: latestPatch`); (3) 🔸 jeden stroj nebo build server pro vydání shellu a EDControls,
  zapsaný v `docs/deploy.md`.
- **Hotovo když:** publish s Debug `EDLicence.dll` v `C:\Edison` skončí chybou; `global.json` v kořeni.
- **Omezení:** legacy DLL staví repo EDISON do `C:\Edison` (`OutputPath`), do EDISON2 se nekopírují
  a reference zůstává `HintPath`; volba vydávacího stroje je na vlastníkovi.

### A47 · Runtime pro .NET 10 exe rozbaluje EDLOADER jen pro nový shell, TemplateDesigner na něm závisí taky 🔸

- [ ] **Stav:** otevřeno, ověřeno 2026-10-02 ve Windows Sandbox (`tools/edloader-sandbox/`): bez uzlu
  a bez .NET 10 TD ze starého shellu ukáže dialog .NET (S1), po rozbalení `dotnet\` (S4) nebo instalaci
  .NET 10 (S1b) naběhne. Volbu varianty a/b/c vlastník 2026-10-05 odložil, proud „Update a vydání" ji nevyřešil.
- **Rozhodnutí vlastníka (2026-10-05):** varianta b: spouštěče TD ve starém shellu (`TiskJR`, `ISYJR2`, `MHD2Linky`) ověří `dotnet\` nebo globální .NET 10 a ukážou českou hlášku; `docs/deploy.md` popíše, kdy TD runtime potřebuje.
- **P2 · S** · Závisí na: - · Blokuje: - · Původ: nový (2026-10-01)
- **Problém:** Všechna exe ze `SingleFilePublish.targets` hledají runtime nejdřív v `dotnet\` vedle
  sebe, pak globálně. `dotnet\` rozbaluje jen EDLOADER a jen při zapnutém novém shellu. TemplateDesigner
  leží ve stejném adresáři a starý shell ho spouští přímo: na stanici bez globálního .NET 10 a bez
  uzlu `NewShell` se nespustí, i když `EDISONRUNTIME.zip` leží vedle, a spouštěč chybu nepozná.
- **Důkaz:**
  - `SingleFilePublish.targets:20-21` `AppHostRelativeDotNet=dotnet`, `AppHostDotNetSearch=AppRelative;Global` (importují EDISONMAIN2, EDUPDATE2, TemplateDesigner a EDControls).
  - `EDISON/EDLOADER/clsStartup.vb:759-760` `NewShellFilesPresent() AndAlso NewShellIsEnabled(connection) AndAlso EnsureLocalRuntime() ...`: rozbalení (`:436-456`) je až za uzlem.
  - Spouštěče TD ve starém shellu runtime nekontrolují: `EDISON/TiskJR/formTISKJRTemplateDesigner.vb:67-70` `Process.Start(... UseShellExecute = True)`, `EDISON/ISYJR2/formISYJR2HlavniOkno.cs:3656`, `EDISON/MHD2Linky/formMHD2LinkyHlavniOkno.cs:2134`.
  - Ostrá FULL 260929.0 nese `EDISONRUNTIME.zip` i `TemplateDesigner.exe` všem zákazníkům ([post mortem](2026-09-29-vydani-shellu-post-mortem.md) K4.8 a sekce 6).
- **Dopad:** zákazník bez nového shellu má po FULL runtime na disku, TD ho ale nevyužije a bez ručně
  nainstalovaného .NET 10 ukáže anglický dialog o chybějícím runtime. Po prvním zapnutí shellu
  (rozbalený `dotnet\`) TD funguje i ze starého shellu a i po vypnutí uzlu: chování závisí na
  historii stanice. Totéž čeká každé další .NET 10 exe vedle EDISONu.
- **Návrh opravy:** 🔸 (a) EDLOADER rozbalí runtime nezávisle na uzlu, když vedle leží zip a některé
  .NET 10 exe (cena: zápis 74 MB a zámek u všech zákazníků při prvním startu); (b) spouštěče TD
  zkontrolují `dotnet\` nebo globální runtime a ukážou českou hlášku; (c) TD self-contained.
  Rozhodnutí vlastníka vydání.
- **Hotovo když:** v sandboxu bez .NET 10 a bez uzlu TD ze starého shellu naběhne nebo ukáže
  srozumitelnou hlášku; `docs/deploy.md` popisuje, kdy TD potřebuje runtime.
- **Omezení:** EDLOADER a spouštěče TD žijí v repu EDISON; TD je mimo scope review, nález se týká
  sdíleného publish mechanismu a rozbalování runtime.

## P3

### A24 · `tools/GenerateTree` mimo sln, žádný parita test

- [ ] **Stav:** otevřeno, částečně (advisory drift hook 2026-08-26, CI `tree-sync.yml` 2026-08-27)
- **Rozhodnutí vlastníka (2026-10-05):** směr „master" (`Tree.cls` vs. XML) se teď nerozhoduje, parity test ani projekt v sln ho nepotřebují.
- **P3 · M** · Závisí na: - · Blokuje: - · Původ: [backlog R10](arch-backlog.md#r10---toolsgeneratetree-mimo-build-žádný-drift-guard)
- **Stav v kódu:** `tools/GenerateTree.cs` (461 ř.) je file-based app, v `EDISON2.sln` není. Drift
  hlídá `.github/workflows/tree-sync.yml` (`957e5f51`: generátor staví a pouští v CI podle cronu
  a `repository_dispatch` a otevírá PR, to je autoritativní guard) a lokálně advisory
  `.claude/hooks/tree-drift.sh` + `tools/tree-sources.sha256`. Drift ze 17. 9. regeneroval `4b83027e`.
  `a7eee5d7` bere FQN formy nejdřív z legacy enumu, hodnota v `Tree.cls` je jen fallback; z override
  tabulky jdou už jen `exe` a `input`.
- **Zbývá:** `tools/GenerateTree` jako `.csproj` v sln + parita test, že vygenerovaný výstup je
  identický s `Tree.cls` (jen část odvozená z dat, `ResolveExe` sonduje `C:\Edison`). Po `a7eee5d7`
  je parita test cennější: rozdíl enum vs. `Tree.cls` už generátor řeší sám.
- **Omezení a kontext:** `Tree.cls` je ztrátová projekce XML; druhý konzument
  `ALWA.LICENCE.API.Catalog` v repu EDISON; směr „master do Caché" nerozhodnutý 🔸. Detail:
  backlog R10.

### A25 · Veřejný povrch EDControls bez konzumenta 🔸

- [ ] **Stav:** otevřeno (2026-10-01: `2e8d88fe` odebral navigaci nálezů, zbytek beze změny)
- **Rozhodnutí vlastníka (2026-10-05):** nepoužité veřejné členy odebrat (export v `EDList` se neplánuje) v jednom publishi s A22 a A38; opravit `EDControls/CLAUDE.md`.
- **P3 · S** (evidence) / koordinovaný publish (odstranění) · Závisí na: - · Blokuje: - · Původ: [backlog R13a](arch-backlog.md#r13a---edcomponenttoolbar-export-a-print), rozšířeno
- **Důkaz (grep přes EDISONMAIN2, LegacyMiniApps, TD, bez `/obj/`):**
  - `EDComponentToolbar`: `ShowExport` (`EDComponentToolbar.xaml.cs:106-114`), `ShowPrint` (`:116-124`), `ExportCommand` (`:146-155`), `PrintCommand` (`:157-165`), `SettingsCommand` (`:177-185`), `SearchPlaceholder` (`:96-104`), 0 konzumentů. `2e8d88fe` odebral `CurrentMatchIndex`, `TotalMatches`, `FindNext/FindPreviousCommand` a `IntZeroToCollapsedConverter` (konzument jen uvnitř EDControls, publish TD nebyl nutný).
  - `EDFilter`: `FilterCommand` (`:67-75`), `AutoTrigger` (`:81-89`), `RefreshText` (`:106-114`), `ShowRefresh` (`:118-126`), `FilterChanged` (`:146`), `BuildFilterDict` (`:233`) public, 0 konzumentů; `IEDFilterTarget` implementuje jen `EDList`.
  - `EDList`: `CheckAll/UncheckAll`, `CellLinkClicked` jen `EDListTestWindow` (`EDListTestWindow.xaml.cs:19,44-45`); `isLink` bez produkčního konzumenta (`MockList.cls:12`).
  - `EDToolbar` jen `EDComponentAndViewModelTestWindow.xaml:18`; `ValidationContext.cs:7` veřejný typ bez použití; `MLRestJson` viz A38.
  - Oprava z 2026-10-01: `xmlns:edct` v `EdisonMainWindow.xaml:15` mrtvý není, používá ho `:34` `NonEmptyStringToVisibleConverter`.
- **Dopad:** každý člen je závazek „breaking change = publikovat konzumenty" za nic; toolbar má
  tlačítka pro funkce, které neexistují; `EDControls/CLAUDE.md:104` tvrdí „(search, export, ...)".
- **Návrh opravy:** rozhodnutí vlastníka per člen (export v `EDList` implementovat, nebo
  deprekovat); odstranění v jednom koordinovaném publish s A22; opravit CLAUDE.md.
- **Omezení:** TD běží u zákazníků, odstranění veřejných členů, které TD používá, = koordinovaný publish.

### A26 · Sdílená DLL nese aplikačně specifické ikony 🔸

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01, `EDIcons.xaml` beze změny)
- **Rozhodnutí vlastníka (2026-10-05):** ikony jen pro TD přesunout do TD (`TDIcons.xaml`, zpočátku alias), ikony jen pro shell do shellu, v EDControls zůstane sdílený zbytek; odebrání z EDControls až v koordinovaném publishi.
- **P3 · M** · Závisí na: - · Blokuje: - · Původ: [backlog R4b](arch-backlog.md#r4b---td-ikony-ve-sdílené-knihovně), upřesněno
- **Důkaz (grep klíčů `EDIcons.xaml` do projektů):** z 64 ikon 39 jen TemplateDesigner nebo vnitřek
  EDControls (36 TD: New, Open, Save, Print, Undo, Redo, *Element, Align*, Distribute*, Z*, Sections,
  Fields, …; 3 jen `EDViewModel.cs:253-265`: Add, Edit, Delete), 18 jen EDISONMAIN2 (Company, Server,
  Module, User, Tools, Star, Car, …), **4 nikde** (`EDIcons.AddElement`, `EDIcons.Barcode`,
  `EDIcons.Snap`, `EDIcons.PageProperties`), 3 sdílené (Molecule, Help, MlineLogo). Ikony jdou přes
  `{StaticResource}`: starý TD exe + nová DLL bez klíče = výjimka při načtení okna
  (`EDControls/CLAUDE.md:17-19` řeší jen `DynamicResource`).
- **Dopad:** každá nová ikona TD toolbaru vyžaduje změnu EDControls csproj + xaml a publish DLL
  i pro shell.
- **Návrh opravy:** aditivně `TDIcons.xaml` v TD (může zatím aliasovat na EDIcons), shell-only
  dictionary v shellu, v EDControls zůstane sdílený zbytek; odstranění až koordinovaným publish;
  test „každý klíč `EDIcons.*` má konzumenta v EDControls nebo je ve výjimkách". Pokud je
  sdílený katalog záměr, zapsat to do `EDControls/CLAUDE.md`.
- **Omezení:** klíče měnit aditivně.

### A27 · Wire deklaruje, co UI nerenderuje: `IMAGE`/`CODEBOOK`, `Field.Format`, `UserHidden` 🔸

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01; přestavba XAML formuláře `Format` nezapojila)
- **Rozhodnutí vlastníka (2026-10-05):** rezervovat: `IMAGE`/`CODEBOOK` zůstávají platné konstanty, renderují se jako text a C# i `Column.cls` je označí jako rezervované; `Field.Format` a `UserHidden` označit jako rezervované a `format="N2"` vyřadit z `EDIFExample`. Implementace až pro skutečného konzumenta.
- **P3 · S** · Závisí na: A14 (dělat v rámci nebo po) · Blokuje: - · Původ: [backlog R13d](arch-backlog.md#r13d---edlist-typy-image-a-codebook-bez-renderingu), rozšířeno
- **Důkaz:** `EDListParsing.cs:54,57` parsuje IMAGE/CODEBOOK, `BuildDataGridColumn`
  (`EDList.xaml.cs:427-501`) větví jen `IsLink` (`:435`) a `Color` (`:462`), fallback na text.
  `EDInputForm/Field.cs:109-111` „Formátovací string pro zobrazení Decimal hodnoty… Aplikuje se
  v UI při ztrátě focusu", grep `Format` v `EDInputForm.xaml(.cs)` (mimo helpery) prázdný i po
  `b03fe2c3`/`703fc7b9`; server ho vystavuje jako featuru (`Form.cls:85-86 AddDecimal(..., format)`,
  `EDIFExample.cls:27 "N2"`). `ItemVisibility.cs:7` `UserHidden` bez UI, které by ho nastavilo
  (`ShowSettings="False"` v `EDInputForm.xaml:35` a `EDList.xaml:14`).
- **Dopad:** autor `.cls` nastaví `format="N2"` podle příkladu nebo `type: "IMAGE"` a nic se
  nestane, bez varování.
- **Návrh opravy:** per položka: implementovat (converter v Decimal template s parse-back,
  rendering IMAGE), nebo fail-fast na `InitializeList`/označit „rezervováno" v C# doc
  i `EDIFExample`.
- **Omezení:** pozice v CList a `.inc` konstanty jsou protokol, nemazat.

### A28 · `ModuleShortcutsService` mutuje sdílený stav, guardy jsou ve VM, handlery se neodhlašují

- [x] **Stav:** vyřešeno a759081a (2026-10-02) - guardy patří službě: `MembershipChanged` jen při skutečném
  Add/Remove (ne při Seed), `Batch` sloučí toggle rodiny do jednoho eventu, `IsLoaded` nahrazuje persistable
  flag VM; handlery VM jsou pojmenované a při zavření shellu se odhlásí (od d2b3c6b1 ve `FavoritesCoordinator`).
- **P3 · S** · Závisí na: - · Blokuje: A5 krok `FavoritesCoordinator` · Původ: [backlog N7](arch-backlog.md#n7---moduleshortcutsservice-mutuje-sdílený-stav-guard-je-v-jiné-třídě)
- **Důkaz:** `EdisonMainViewModel.cs:372-381` anonymní handlery na `_shortcuts.Items.CollectionChanged`,
  `PositionsChanged`, `LaunchRequested`, `ChangelogRequested` a `WindowNavigator.LegacyModuleExited`
  (nikdy odhlášené; jediné `-=` ve VM je `:741`); `_suppressFavoritesSync` `:990`, `:604/608`
  (`SeedShortcuts`), `:1233/1235`; druhý guard pro tutéž službu `_shortcutsPersistable` `:594,:622,:1249,:1254`
  (`5fcfa28e`); navíc `MiniApps/DesktopMiniAppView.xaml.cs:90` zapisuje `svc.GridColumns` z view do
  sdílené služby.
- **Návrh opravy:** guard patří k tomu, kdo kolekci mutuje: seed/batch režim ve službě, který
  event nefíruje, nebo odhlásitelný handler v koordinátoru; `_shortcutsPersistable` jako stav
  služby („blob načten").

### A29 · `DesktopMiniAppViewModel.LaunchCommand` nemá binding, view volá službu přímo 🔸

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01, beze změny)
- **Rozhodnutí vlastníka (2026-10-05):** `LaunchCommand` smazat.
- **P3 · S** · Původ: [backlog N4](arch-backlog.md#n4---desktopminiappviewmodellaunchcommand-nemá-ani-jeden-binding)
- **Důkaz:** `MiniApps/DesktopMiniAppViewModel.cs:24,40` command definován; žádný XAML binding;
  `MiniApps/DesktopMiniAppView.xaml.cs:33` `Vm?.Shortcuts.RequestLaunch(s)`, zatímco sourozenecký
  `RemoveCommand` bindovaný je (`DesktopMiniAppView.xaml:75`).
- **Návrh opravy:** command zapojit, nebo smazat.

### A30 · Drag&drop vzor ve dvou code-behindech

- [ ] **Stav:** otevřeno, částečně (práh sdílený; ověřeno 2026-10-01)
- **P3 · S** · Původ: [backlog N5](arch-backlog.md#n5---duplikované-dragdrop-workflow-ve-dvou-code-behindech)
- **Důkaz:** práh je sdílený (`DesktopDragFormats.ThresholdExceeded`, commit ef11e045); zbývá
  `_dragStart`/`_dragModule`/flag „potlač klik po tažení" + `DoDragDrop` v
  `EdisonMainWindow.xaml.cs:170,182-203` (`_treeDragActive`) i `DesktopMiniAppView.xaml.cs:21-51`
  (`_suppressClick`), obě bez testu.
- **Návrh opravy:** attached behavior nebo helper `DragSource` s callbackem na payload.

### A31 · `DisplayedName` nese tři věci a persistuje se do blobu 🔸

- [ ] **Stav:** otevřeno, částečně (860b9ffe, 2026-09-21) - suffixy stavu modulu
  (`- stub`, `- chybí exe`, `- rozbité`, `- stuby`) přidává `ModuleLaunchabilityCheck` jen pod
  `InternalDiagnostics.Enabled` (DEBUG / doména M-line), u zákazníka se do blobu nedostanou;
  mlineOnly suffix a oddělení `Name`/`DisplayedName` zůstávají 🔸. Ověřeno 2026-10-01, beze změny.
- **Rozhodnutí vlastníka (2026-10-05):** oddělit `Name` (data, klíč) od `DisplayedName` (zobrazení). Ověřeno 2026-10-05: legacy host (`EDISON/EDISONMAIN2API/CacheShortcutSink.cs`) blob jen zapisuje (Add/Remove/Exists podle klíče `Kind|ElementId|Id`), `Name` zpět nečte; čte ho jen shell.
- **P3 · S** · Původ: nový (2026-09-20)
- **Problém:** dekorace pro zobrazení mutuje doménový model (`with { DisplayedName = ... }`),
  který dál slouží jako text hledání, zdroj persistovaného `Name` dlaždice a základ cesty stromem.
- **Důkaz:** `Modules/ModuleLaunchabilityCheck.cs:85-86` `exe with { Color = StubColor,
  DisplayedName = exe.DisplayedName + Suffix(" - stub") }` / `" - chybí exe"`, `:72,:78` rodiny
  `" - rozbité"` / `" - stuby"`; `Modules/ModuleTreeState.cs:28,68` mlineOnly suffix. Spotřebitelé:
  `Modules/DesktopShortcut.cs:126` `DisplayedName = module.DisplayedName`, `:135-136` `ToDto()`
  (blob `Shortcuts`, který čtou legacy agendy), `ModuleTreeState.cs:89` hledání. Cesta stromem se
  počítá dvakrát z různých vstupů: `Modules/ModuleTreeLoader.cs:62` `FamilyPath` z názvů před
  `Mark`/`TryFilter` (→ `ExecutableModule.TreePath`, tooltip v Používaných, `309bfc32`) a
  `EdisonMainViewModel.cs:1630-1655` `BuildModuleLabels` z dekorovaných `DisplayedName`
  (→ `ModuleShortcutsService.cs:138` tooltip dlaždice, labely spuštěných modulů);
  `ExecutableModule.cs:29-31` přiznává „stejné pravidlo jako ResolveRunningLabel".
- **Dopad:** uživatel s chybějícím exe na stanici M-line přidá modul na plochu → v Caché `Name =
  "Zaměstnanci - chybí exe"`; hledání „stub" vrací moduly; tooltip téhož modulu se v Používaných
  a na dlaždici liší o suffix; nová dekorace = kontrola 5+ konzumentů.
- **Návrh opravy:** oddělit `Name` (data, klíč) od `DisplayedName` (view); `DesktopShortcut.ForModule`,
  hledání a obě cesty stromem berou `Name`; `BuildModuleLabels` čte `TreePath` místo vlastního skládání.
- **Hotovo když:** test „ToDto po Mark(broken) nese původní název"; `TreePath` a label dlaždice
  téhož modulu se shodují.
- **Omezení:** zda legacy `Name` z blobu zobrazuje, musí říct vlastník.

### A32 · `ExecutableModule` je record s hodnotovou rovností a mutabilním observable stavem 🔸

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01)
- **Rozhodnutí vlastníka (2026-10-05):** `ExecutableModule` převést na třídu s referenční rovností (`Clone` místo `with`), oba workaroundy smazat.
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `Modules/ExecutableModule.cs:5` `public record ExecutableModule : IModule,
  INotifyPropertyChanged` s mutabilními poli `:66,:84,:102` (`_isFavorite`, `_isStartupWindow`,
  `_isVisibleBySearch`); `309bfc32` přidal `FamilyPath` (init), který se taky účastní rovnosti;
  obcházení `EdisonMainWindow.xaml.cs:267-268,275` („moduly jsou record s hodnotovou rovností, takže
  dva shodně vyplněné uzly by si kontejnery prohodily", `ReferenceEquals`),
  `DesktopMiniAppView.xaml.cs:79`; `ModuleFamily` je naopak `class` (`ModuleFamily.cs:6`).
- **Dopad:** WPF `SelectedItem`, `List.Remove`, `Distinct` se chovají jinak podle toho, zda uzel
  někdo označil; dva typy pod jedním `IModule` mají dvě sémantiky rovnosti.
- **Návrh opravy:** rozhodnout: `class` + `Clone(...)` místo `with`, nebo record s referenční
  rovností; smazat oba workaroundy.

### A33 · Složka `dotnet/MiniApps` drží infrastrukturu pruhu podpory a docku; namespace cyklické

- [ ] **Stav:** otevřeno (2026-10-01: složka přibrala `DockThemeBrushes.cs`)
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDISONMAIN2/dotnet/MiniApps/SupportWidgetHost.cs`, `FontZoomWebView.cs`, `WidgetUrls.cs`
  používá jen `EdisonMainWindow.xaml.cs:73,77`, `LoginWindow.xaml.cs:75-76` a nově
  `Startup/TestRunOptions.cs:37` (`SupportWidgetHost.ScreenshotModeVariable`); nový
  `MiniApps/DockThemeBrushes.cs` (`5d10d7dc`) používá jen `EdisonMainWindow.xaml.cs:95`; přitom
  `MiniApps/CLAUDE.md:13` říká „Widgety podpora.m-line.cz v docku nejsou". Cyklus:
  `MiniApps/DesktopMiniAppProvider.cs:2`, `DesktopMiniAppViewModel.cs:6` `using
  EDISONMAIN2.Windows.EdisonMainWindow.Modules;` vs `EdisonMainViewModel.cs:17` `using
  EDISONMAIN2.MiniApps;` a `:166,:175-176` `DesktopMiniAppProvider.DesktopId`.
- **Dopad:** vytažení mini-app vrstvy do samostatného projektu (jako `LegacyMiniApps`) je kvůli
  cyklu nemožné bez přestavby.
- **Návrh opravy:** `SupportWidget*` + `WidgetUrls` do `Windows/EdisonMainWindow/SupportStrip/`,
  `DockThemeBrushes` k `AvalonDockHost`, plochu (`DesktopMiniApp*`, `TileGridPanel`,
  `DesktopDragFormats`, `ModuleShortcutsService`, `DesktopShortcut`) do jedné složky `Desktop/`;
  `DesktopId` vlastní ten, kdo Plochu registruje.
- **Hotovo když:** `MiniApps` nereferencuje `Windows.EdisonMainWindow.Modules`.

### A34 · Dvě serverové cesty k „výchozí okno", jedna mrtvá

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01, beze změny)
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `Windows/UserProfileWindow/UserProfileService.cs:11` record s `DefaultWindowId`,
  `:41-43` parsování řádku `form` z `ALVA.EDISON.Main.LoadUser`; okno (`UserProfileWindow.xaml.cs:37-44`)
  z něj čte jen dir pole, `DefaultWindowId` čte jen `UserProfileServiceTests.cs:44,93`; shell
  používá `:77-81` `LoadDefaultWindow` přes `ALVA.USERS.ApiEdison.GetUserDefaultForm`
  (`Modules/SessionDataLoader.cs:48`).
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

### A36 · Parsování legacy delimited výstupů zdvojené v osmi třídách se třemi konvencemi

- [ ] **Stav:** otevřeno (2026-10-01: deploy přidal dvě další kopie)
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `Startup/Updater.cs:95-97` (`for i=1..GetUpperBound(0)`), `Startup/LogoutWatcher.cs:76-78`
  (`for i=0..Length`, projde i ERR slot), `UserProfileService.cs:93-96` a `SetupWindow.xaml.cs:71-73`
  (`for i=1..Length`), `Startup/AccessLoader.cs:42-44` (od 2), `UserAuthenticator.cs:36,71` a
  `LicenceVerifier.cs:45,91` (`output[1]` napřímo); nově `Startup/LoginGate.cs:33-35` (`d6427024`)
  a `Startup/LicenceService.cs:193-195` `LicenceNotice.ParseNamedCount` (`c3804594`), obě `i=1..Length`.
  V EDControls stejný vzor `WindowStateStore.cs:97-108` `CacheWindowStateStore.ParseOutput`.
- **Dopad:** změna wire konvence = 9 míst; rozdíly `Length` vs `GetUpperBound` skryjí off-by-one
  při prázdné odpovědi; každý nový legacy endpoint (deploy jich přidal pět) dostane vlastní smyčku.
- **Návrh opravy:** jeden helper v EDControls vedle `MLRestAdapter` (shell i `CacheWindowStateStore`
  ho použijí): `IEnumerable<string[]> Rows(object[] output, char del)` + `string? Named(output, del, key)`.
- **Hotovo když:** `grep "output\[i\]?.ToString()\|raw\[i\]?.ToString()" EDISONMAIN2/dotnet` jen v helperu;
  stávající testy zelené.
- **Omezení:** `object[]` je u těchto volání povolený legacy vzor, wire beze změny; helper v EDControls
  je aditivní veřejné API (nebo `internal` s `InternalsVisibleTo` shellu).

### A37 · Lokální stav shellu je per Windows-účet × IDFa, ostatní per Edison-uživatel × IDFa 🔸

- [ ] **Stav:** otevřeno (2026-10-01: změna tvaru, `local.json` přibral další klíče)
- **Rozhodnutí vlastníka (2026-10-05):** lokální stav docku klíčovat `server|idfa|login` v `local.json` (navazuje na A45, starý klíč jako fallback při čtení); `ShellWindowBounds` zůstává per Windows účet.
- **P3 · S** (rozhodnutí) až M (přesun) · Původ: nový (2026-09-20)
- **Důkaz:** `Startup/LocalPrefs.cs:22-36` tři slovníky klíčované jen IDFa (`MiniAppLayouts`, `TreeWidths`,
  nově `SupportStripHeights`, `f821f574`) a nově `Connections` (`51112233`) a `ShellWindowBounds`
  (`83cb2472`, zdůvodnění scope v `ShellWindowStateStore.cs:6-11`: login okno nemá session);
  `EdisonMainViewModel.cs:191-195` `(_prefs.MiniAppLayouts ??= new())[idfa] = ...`;
  `:1458` `ReplaceMiniAppSession(Idfa);   // přihlášení jiného uživatele: stejná firma, jiná data`;
  root `CLAUDE.md:112-115` uvádí obě úložiště, ale proč je dock layout per Windows účet, nezdůvodňuje.
- **Dopad:** sdílené terminály: uživatel B rozhází dock uživateli A; na jiné stanici žádný layout;
  souběh instancí a klíč bez serveru viz A45.
- **Návrh opravy:** rozhodnout (vlastnost stanice vs. uživatele) a zapsat do CLAUDE.md; pokud
  stanice, klíčovat `server|idfa|login`; pokud uživatel, přesun do UserPrefs blobu (velikost
  AvalonDock XML zvážit). `ShellWindowBounds` zůstává per Windows účet (login okno).
- **Omezení:** nullable property kompatibilita `local.json`.

### A38 · `MLRestJson` je veřejný wire kontrakt bez serverové ani klientské strany

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01, beze změny)
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/MLRestJson.cs:28-30` `input["payload"] = ...; CVar output =
  await Task.Run(() => mlRest.QueryOrThrow(cacheClass, method, input));` bez klonu; volající
  pouze `EDControls.Tests/MLRestJsonTests.cs`; grep `"payload"` přes `**/*.cls` = 0;
  `EDControls/CLAUDE.md:87` ho popisuje jako živý kontrakt („kontrakt na obou stranách").
- **Dopad:** dokumentace tvrdí kontrakt, který nikdo neplní; první reálný uživatel dostane
  RestStatus race, protože helper vypadá jako hotová bezpečná cesta. Je to poslední cesta
  v EDControls, která pouští request mimo UI thread na sdílené instanci.
- **Návrh opravy:** smazat (formálně odebrání public členu, konzument žádný), nebo přepsat na
  `IRestCallRunner` a v CLAUDE.md označit „bez serverové strany, vzor pro budoucí endpointy".

### A39 · Sdílená JSON preference s cross-process watcherem existuje dvakrát

- [x] **Stav:** vyřešeno 76866634 (2026-10-02) - `EDControls/dotnet/SharedJsonPreference.cs` (internal
  `SharedJsonPreference<T>` instance: `FilePath`, `Current`, `Changed`, Load/Save/watcher, volitelná
  `afterLoad` migrace) a `MlineCommonPaths.cs` (`Folder`, `File(name)`); obě veřejné třídy drží
  statickou instanci a jen delegují (font předává `MigrateLegacy`, `Save` dál razítkuje `Version`).
  Veřejné API ověřené reflexí beze změny (zmizel jen privátní setter `Current`), `Save` staré
  i nové DLL zapíše bajtově stejný soubor; `FilePath` seam zůstal, stávající testy beze změny smyslu,
  nový `SharedJsonPreferenceTests` (watcher mezi dvěma instancemi, bez statiky a WPF).
  `ErrorFallbackLog` bere cestu z `MlineCommonPaths`.
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/UserFonts/UserFontPreference.cs:46-48` (FilePath), `:60-72`
  (Load), `:93-101` (Save), `:103-129` (watcher `:114` + `Thread.Sleep(50)` `:127`) vs
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

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01)
- **P3 · M** · Závisí na: A39 · Původ: nový (2026-09-20)
- **Důkaz:** seamy `ErrorReporting/ErrorReports.cs:15`, `ErrorReporter.cs:37`,
  `ErrorFallbackLog.cs:13,18`, `MlineEnvironment.cs:51`, `WindowStateStore.cs:62` (dříve
  `WindowStatePersistence.cs:28`), `UserFontPreference.cs:46`, `UserThemePreference.cs:29`,
  `EDUsageTelemetry.cs:17-18`, `EDServices.cs:76,97`. Kolekce `EDControls.Tests/EDUsageTelemetryTests.cs:12-13`
  `[CollectionDefinition(SharedStaticStateCollection.Name, DisableParallelization = true)]`
  s 8 členy (`WindowStatePersistenceAttach` mutuje `CacheWindowStateStore.MLRestProvider`);
  `UserFontPreferenceTests.cs:24-29` a `UserThemePreferenceTests.cs:27-32,83-91` mutují statiku
  **mimo** kolekci; `TemplateDesigner.Tests/xunit.runner.json:6-8` totéž selhání
  popsané („udržovat seznam tříd je zdroj téhle chyby"). V shellu přibyly další settable statiky
  (`SessionFactory.SessionTag`, `SettingsLoader.UseDevDefaults`, `InternalDiagnostics.Enabled`, A10).
- **Dopad:** nový test na `FontService` běžící paralelně s `UserFontPreferenceTests` =
  nedeterministický pád; `ErrorReports` má 4 seamy, které každý test resetuje ve správném pořadí
  (`ErrorReportsTests.cs:43-55`).
- **Návrh opravy:** `ErrorReportingPipeline(ErrorThrottle, HttpClient, fallbackPath, isInternal)`
  instance za statickou fasádou `ErrorReports.Pipeline` (jeden seam); preference seam zmizí
  s A39; zbylé statiky (`EDServices`, telemetry buffer) = A6, shell statiky = A10.
- **Hotovo když:** `ErrorReportsTests`/`ErrorReporterTests` bez `[Collection]` a `finally` resetů.
- **Omezení:** veřejná statická API `ErrorReports.Report*`, `Enabled`, `ErrorReporter.IngestUrl/ApiKey`
  zůstávají jako fasáda.

### A41 · Odeslání hlášení chyby je skryté v implementaci dialogu 🔸

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01)
- **Rozhodnutí vlastníka (2026-10-05):** jen kontrakt v dokumentaci `IPopupDisplayer.Error` (implementace hlásí do `ErrorReports`), chování beze změny.
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/EDViewModel/PopupDisplayer.cs:26-33` `var refId =
  ErrorReports.Report(ex, resolvedOwner); EDErrorPopup.Show(...)`; dialog sám posílá i doplnění
  (`EDErrorPopup.xaml.cs:47` `ErrorReports.ReportFollowUp`); `IPopupDisplayer.cs:23-24` slibuje jen
  „Scrollovatelný error dialog"; `EDISONMAIN2/dotnet/App.xaml.cs:91` `new PopupDisplayer(owner).Error(e.Exception);`
  druhá instance kvůli owneru; `RelayCommand.cs:18`, `AsyncRelayCommand.cs:34` chytají výjimky
  výhradně přes displayer.
- **Dopad:** kdo dosadí jiný `IPopupDisplayer` (headless, toast), reporting tiše ztratí; dnes
  žádná alternativní implementace není, proto P3.
- **Návrh opravy:** doc kontrakt „implementace `Error` hlásí do `ErrorReports`" na rozhraní, nebo
  reporting vytáhnout do `RelayCommand`/`AsyncRelayCommand` catch před displayer (aditivní
  `Error(ex, owner, refId)`).
- **Omezení:** `IPopupDisplayer` implementují mocky v ~20 test souborech, jen aditivní změny.

### A42 · Dva vyhledávací algoritmy pod stejným toolbarem

- [x] **Stav:** vyřešeno 3e6422a2 (2026-10-05) - toolbar přišel o šipky a počítadlo nálezů už v 2e8d88fe;
  teď `EDListFilter.Matches` normalizuje hledaný text i hodnoty přes `SearchNormalization` (jako
  formulář a `EDFilterComboBox`), takže „sablona" najde „Šablona". Test `EDListFilterTests.IgnoresDiacritics`.
  **Publish EDControls.dll** = hledání v TD pickerech nově ignoruje diakritiku.
- **P3 · S** · Původ: nový (2026-09-20)
- **Důkaz:** `EDControls/dotnet/EDList/EDListFilter.cs:21` `value.Contains(needle,
  StringComparison.OrdinalIgnoreCase)` (bez diakritiky) vs `EDInputForm.xaml.cs:348`
  `SearchNormalization.NormalizeForSearch(s)` a `EDFilterComboBox/EDFilterComboBoxLogic.cs:29-31`.
- **Dopad:** „sablona" ve formuláři najde „Šablona", v seznamu ne.
- **Návrh opravy:** `EDListFilter.Matches` přes `SearchNormalization`.
- **Hotovo když:** `EDListFilterTests` s případem s diakritikou.

### A43 · Koncept „barva" má dva wire formáty a tři konvertory

- [ ] **Stav:** otevřeno (ověřeno 2026-10-01)
- **P3 · M** · Původ: nový (2026-09-20)
- **Důkaz:** `EDList.xaml.cs:710-728` `RgbStringToBrushConverter` (`R;G;B`);
  `EDInputForm/ColorPreviewHexToBrushConverter.cs:12` (`#AARRGGBB`);
  `EDViewModel/HexToBrushConverter.cs:13`; formáty popsané jen v CLAUDE.md (`EDList/CLAUDE.md:36`,
  `EDInputForm/CLAUDE.md:18`), `Column.cls` o formátu nic.
- **Dopad:** master-detail host (barva v seznamu, editace ve formuláři) musí konvertovat; nový
  server list zvolí hex a tiše dostane `Transparent`.
- **Návrh opravy:** jeden wire formát pro nové listy (hex, shodně s formulářem), konvertor umí
  oba a je sdílený.
- **Omezení:** `R;G;B` existujících `LoadList` implementací musí zůstat čitelné.

---

## Co je v pořádku (ověřeno, není nález)

- `RestCallRunner`, `SessionDataLoader`, `LogoutWatcher.Poll`, `SessionLogout`,
  `LicenceVerifier.ReacquireLock` jedou na klonech, od `5fcfa28e` i `IUserPrefsService`; nové
  pozaďové requesty deploye (počty vozidel a zaměstnanců, stažení licence z M-line) jdou přes
  `RestCallRunner` (`EdisonMainViewModel.cs:448,472`) a update check na klonu `Locks=0`
  (`AppBootstrapper.cs:187`). Mimo UI thread na sdílené instanci zbývá jen `MLRestJson` (A38).
- `ApplySessionData` je synchronní, guardy uvnitř neawaitují; `CompanySwitcher.Switch`
  a `SessionSwitcher.Switch` drží dokumentované pořadí včetně veta okna a rollbacku.
- `HandOffLoginWindow` nastaví `MainWindow` před `OnMainWindowClose`; `ForcePasswordChange`
  restoruje předchozí mode; `ShellShutdown.Force` je jediná programová cesta ukončení **po vzniku
  session** (`App.xaml.cs:33` volá `Shutdown()` po přesměrování přes EDLOADER, ještě bez session).
- `AppBootstrapper` `protected virtual` + `protected` ctor bez `Application` funguje
  (`EDAppearance.Initialize(null)` má null guard).
- `Updater.Check` pořadí je komentované a pokryté včetně nových bran (`pending`, serverová instalace,
  http zdroj); `UpdateEngine` je jeden pipeline s virtuálními seamy a 1100+ řádky scénářových testů,
  ne God class; `UpdateWindow` má rozhodnutí ve statických `DecideClose`/`ShouldRunExe`.
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
  `EdisonDeployRoot` (A8); launch kontrakt je dnes synchronní na obou stranách včetně polí 18-20
  (A12 je o tom, čím je synchronizace hlídaná).
- Čtyři class handlery na `Window.Loaded` (`EDSupportPanel`, `EDUsageTelemetryHook`, `ThemeService`,
  nově `TaskbarIcon`) mají guardy a sdílejí `FrameworkWindows`; dva telemetrické kanály (HTTPS vs
  MLRest) jsou záměr a hlášení chyb má jednu fasádu.
- `LegacyDashboardMiniAppProvider` dává všem deskriptorům tutéž instanci loaderu;
  `MiniAppManager` je testovatelný přes `IMiniAppHost`; `MiniAppCatalog.Build` fail-fast
  na duplicitní Id, DEBUG-only providery Release layout nerozbijí.
- `WindowDimmer` refcount + `ForceRelease` na `Closed`; `ThemeService.IsDark` vs `Current`
  se rozcházejí jen milisekundy.
- `ConnectionOverride` má párové `Parse`/`ToArguments` a cizí tokeny (`reason=2`, `e2e=1`) ignoruje;
  přesměrování přes EDLOADER brání smyčce proměnnou `EDISONMAIN2_REDIRECTED`.
