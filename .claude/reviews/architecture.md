# Architektonický review: dokument nálezů

| | |
| --- | --- |
| **Poslední běh** | 2026-10-07 (předchozí 2026-10-01 nad `19f62712`, první 2026-09-20) |
| **Commit** | `559e9803` (branch `main`, clean) |
| **Scope běhu** | `EDISONMAIN2/` (+ `.LegacyMiniApps`, `.Tests`, `CACHE/`), `EDControls/` (+ `.Tests`, `CACHE/`), `EDUPDATE2/` (+ `.Tests`, `EDUpdateContract`) |
| **Mimo scope** | `TemplateDesigner*` (jen jako konzument EDControls), `ServiceGtfsRt`, `EDTesting`, `tools/`, `EDISON2.IntegrationTests` (jen jako pokrytí kontraktů) |
| **Odvozeno** | `EDUpdateContract` patří k EDUPDATE2 (vznikl z něj a ze Startup shellu v A44), `LegacyMiniApps` k EDISONMAIN2. Čtyři read-only subagenti prošli Startup shellu, okno shellu s plochou a miniaplikacemi, infrastrukturu EDControls a controls EDControls. Update vrstvu a otevřené nálezy prošel review chat. Každý nález ověřen citací kódu. Dokument převeden ze starého tvaru (1599 ř.). |

## Jak s dokumentem zacházet

Opravný chat řeší jedno ID a čte jen jeho sekci: `grep -n '^##' .claude/reviews/architecture.md`
dá začátek sekce i další nadpis, pak Read s `offset` a `limit`. Po opravě přepíše **Stav** na
`vyřešeno <commit> (datum) - co se udělalo`, přijaté riziko dostane `přijato <důvod>`. Další běh
review sbalí uzavřenou sekci do tabulky Uzavřené. ID se nikdy nepřidělují znovu. Čísla řádků
platí pro `559e9803`, při posunu hledej podle symbolu.

🔸 = rozhodnutí vlastníka. **Omezení** = co se bez vlastníka měnit nesmí: veřejné API
`EDControls.dll` (vydává se i samostatně, TemplateDesigner běží u zákazníků, breaking change =
publikovat i konzumenty), wire kontrakty Caché, launch kontrakt legacy hosta, ACTIVITY zámek.
Priorita **P1** chyba nebo blokované testy, **P2** zesílení změn nebo vazba bránící změně,
**P3** hygiena a drift. Pracnost **S** jeden chat, **M** několik chatů, **L** plán a rozhodnutí.
Starší snímky `arch-EDISONMAIN2-EDControls-2026-07-29.md` a `arch-backlog.md` se neudržují.

**Souvislosti pro opravné chaty:**
- A50 → A63 → A62 v jednom chatu (`AppBootstrapper`, `SessionSwitcher`), A51 s nimi nebo před A62.
- A54 před A55: zápisy na pozadí potřebují snímek session dřív, než opustí UI vlákno.
- A56 určí, jak se odebírá veřejný povrch. Odebrání jen jedním publishem EDControls + TD + shell.
- A58-A61 mění controls EDControls, k TD se chování dostane publishem samotné DLL.
- A64, A65, A66 mění konstruktor a code-behind okna shellu, jeden chat. A69 jen v klidném okně (přesun souborů).

## Mapa repa (2026-10-07)

| Projekt | Typ | Reference | Poznámka |
| --- | --- | --- | --- |
| EDControls | lib | MLRest.dll, EDLicence.dll (`C:\Edison`) | volná DLL vedle exe, konzument TD u zákazníků |
| EDISONMAIN2 | WinExe | EDControls, LegacyMiniApps, EDUpdateContract, EDISONAPI/EDLicence/EDUsers/MLRest.dll | shell, v produkci zatím neběží |
| EDISONMAIN2.LegacyMiniApps | lib | EDControls, MLRest.dll | dashboardy a Dispečink |
| EDUPDATE2 | WinExe | EDUpdateContract | shell ho zatím nespouští (`Updater.ActiveKind` = legacy EDUPDATE) |
| EDUpdateContract | lib net10.0 | žádná | `VerzeInf`, `ForcedStamp`, `UpdaterArgs`, `UpdaterContract` |

Composition root shellu: `App.Application_Startup` → `StartupOptions` (jediný `#if DEBUG`) →
přesměrování přes `EDISON.exe` → `AppBootstrapper.Run` → `InitializeServices` (`EDServices.Initialize`
= statický `Current`) → setup wizard → pre-login brány (`LoginGate`, `LoadSettings.Pending`) →
auto-login nebo dialog → vynucená změna hesla → `PickForSession` → `SessionFactory` → `LicenceVerifier`
→ update check → `BuildShellWindow` (`EdisonMainViewModel`, `CompanySwitcher`, `SessionSwitcher`,
`MiniAppCatalog`, `ModuleShortcutsService`, `LogoutWatcher`). Druhá cesta k session je
`SessionSwitcher.Switch` (A50). EDUPDATE2: `App.OnStartup` → `UpdaterArgs.Parse` → `UpdateWindow` →
`UpdateEngine.Run` na `Task.Run`.

Hranice: Caché přes `IMLRest`/`MLRestAdapter` (`^||ML`, legacy `object[]` přes `LegacyOutput`),
legacy DLL chtějí konkrétní `MLRest` (A67), host `EDISONMAIN2API.exe` (`EdisonLaunchArgs`), blob
plochy sdílený s agendami (`DesktopShortcuts.cls`, A53), `podpora.m-line.cz`, release server HTTP,
EDLOADER, soubory v `C:\Edison`, registry (`LegacyLastCompany`, `MlineDomain`). Persistence:
`local.json` per Windows účet, Caché `UserData` per user × IDFa, `ALVA.LIBRARY.Form` geometrie oken,
`%APPDATA%\m-line\common` font a theme.

---

### A50 Přihlášení jiného uživatele obchází vynucenou změnu hesla, cesta login → session je napsaná dvakrát

- **Stav:** otevřeno · **P1 · S** (díra), M (sdílený krok)
- **Problém:** Start (`AppBootstrapper.RunCore`) a „Přihlásit jiného uživatele" (`SessionSwitcher.Switch`) skládají login → výběr firmy → `SessionFactory` → `LicenceVerifier` → `LogLogin` každý zvlášť, se stejnými virtual obaly. Vynucená změna hesla (Caché flag #17) se dostala jen do startu.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:119` `if (login.ChangePasswordNeeded && !ForcePasswordChange(...))`. `Startup/SessionSwitcher.cs:65-95` jde po `PromptLogin` rovnou na `PickForSession` → `closeAllWindows` → `EstablishSession`. `ChangePasswordNeeded` čte jen `AppBootstrapper.cs:119` a `LoginViewModel.cs:152`. Zdvojené kroky `AppBootstrapper.cs:127-134` / `SessionSwitcher.cs:70-76` (i text hlášky), obaly `AppBootstrapper.cs:475-482` / `SessionSwitcher.cs:121-128`. Legacy vynucuje i po přepnutí: `C:\MLineRepos\EDISON\EDISONMAIN\Rozhrani (mainform)\frmNoMDI.vb:3320` → `:734`.
- **Dopad:** Admin nastaví „změnit heslo při přihlášení". Uživatel se přes Přihlásit jiného uživatele dostane do shellu se starým heslem. Každý nový krok po loginu se přidává na dvě místa a do dvou testovacích podtříd.
- **Návrh opravy:** Hned `ForcePasswordChange` v `SessionSwitcher.Switch` za `PromptLogin` a před `closeAllWindows`, zrušení vrátí `Cancelled` a okna zůstanou. Pak jeden objekt (např. `SessionOpener`: změna hesla → `PickForSession` → `Establish` → `Acquire`) pro obě cesty, kroky jen pro start zůstanou v bootstrapperu.
- **Jak ověřit:** `SessionSwitcherTests`: login s `ChangePasswordNeeded`, zrušený dialog → `Cancelled` a `closeAllWindows` se nezavolá.
- **Omezení:** Pořadí veto oken → flush → logout → establish, `LogLogin` jen po startu a switch user. 🔸 Legacy mění heslo bez starého hesla a po switchi volá i update check (`frmNoMDI.vb:3324`), parita je na vlastníkovi.

### A51 Při auto-loginu zůstane výchozí ShutdownMode, první dialog před shellem aplikaci ukončí

- **Stav:** otevřeno · **P1 · S**
- **Problém:** `App.xaml` `ShutdownMode` nenastavuje, platí WPF výchozí `OnLastWindowClose`. Bootstrapper režim přepíná jen kolem setup wizardu, login dialogu a změny hesla a `MainWindow` na shell předává jen po dialogovém loginu. Při auto-loginu (Windows SSO) a `DevLogin` je popup před shellem jediné okno a jeho zavřením WPF naplánuje `Shutdown`. Po wizardu platí `OnMainWindowClose` (`:299`) a popup se stane `MainWindow`, výsledek je stejný.
- **Důkaz:** `EDISONMAIN2/dotnet/App.xaml:1-7`. `Startup/AppBootstrapper.cs:110` auto-login přeskočí `TryLogin`, jediné `OnExplicitShutdown` pro login je `:392`. `:183` `if (_startupLogin is not { } login) return;` = handoff jen po dialogu. Popupy před shellem: `:347-354` (zákaz přihlašování), `Startup/LicenceService.cs:26-27` (hláška licence), `Startup/LicenceVerifier.cs:77` (admin retry). `EDControls/dotnet/EDViewModel/EDPopup.xaml.cs:52-53` `ShowDialog` s ownerem null.
- **Dopad:** LDAP-AUTO uživatel na instalaci s hláškou licence („Nejsou žádné licence!", prošlá aktivace) nebo admin po licenčním dotazu: shell se ukáže a hned skončí. Start přes „Připojit k databázi" na server se zákazem přihlašování: „Ano" povolí přihlašování a aplikace skončí. E2E jede s `DevLogin` bez popupů a test to nechytí.
- **Návrh opravy:** `OnExplicitShutdown` jednou na začátku `RunCore`. `HandOffLoginWindow` předá `MainWindow` a `OnMainWindowClose` ve všech cestách, login okno zavře jen když existuje. Přepínání v `EnsureConfigured`, `TryLogin` a `ForcePasswordChange` odpadne, všechny výstupy `RunCore` už volají `Shutdown()`.
- **Jak ověřit:** `grep 'ShutdownMode =' EDISONMAIN2/dotnet` dá dvě přiřazení. Ručně Release s auto-loginem a popupem před shellem, shell zůstane. Automatický test až s A62.
- **Omezení:** `MainWindow` na shell před `OnMainWindowClose` (`Startup/CLAUDE.md`), jedna chybová hranice `Run`. Kód je jistý, chování WPF dokumentované, spuštěním neověřeno.

### A52 Zaškrtávací záhlaví EDListu označí i řádky skryté hledáním

- **Stav:** otevřeno · **P1 · S** 🔸
- **Problém:** Hledání filtruje jen view (`CollectionView.Filter`), zaškrtávání drží `EDListController` a zobrazené řádky nezná. Klik na záhlaví („Označit / odznačit vše viditelné") zaškrtne všechny řádky včetně skrytých. Shift+klik přitom zobrazené řádky od view dostává, jeden control má pro rozsah dvě pravidla.
- **Důkaz:** `EDControls/dotnet/EDList/EDListColumnFactory.cs:27,32`. `EDList.xaml.cs:238` předá `_controller.ToggleAllFromHeader` bez řádků, `:256` Shift+klik předá `[.. DataGrid.Items.OfType<EDListRow>()]`. `EDListController.cs:166,169` `SetAllChecked` nad `Rows`, `HeaderCheckState` `:53-63` a `CheckedRowIds` `:50` ze všech `Rows`. `EDList.xaml.cs:96-110` filtr jen ve view. TD `InstancePickerDialog.xaml:34-38` (`SelectionMode="Check"` + hledání), výsledek `InstancePickerDialog.xaml.cs:108` `TheList.SelectedRowIds`.
- **Dopad:** Uživatel TD v pickeru linek napíše „12", klikne na záhlaví a šablona dostane všechny linky agendy. Po ručním zaškrtnutí všech nalezených ukazuje záhlaví „část".
- **Návrh opravy:** Rozhodnout pravidlo. Doporučení: záhlaví pracuje se zobrazenými řádky. View je předá jako u Shift+kliku do `ToggleAllFromHeader(displayed)` i do stavu záhlaví a stav přepočítá při změně `SearchText`. Zaškrtnuté skryté řádky zůstanou ve výběru.
- **Jak ověřit:** Test controlleru s podmnožinou zobrazených řádků. STA test EDListu: `SearchText`, klik na záhlaví, `SelectedRowIds` obsahuje jen nalezené.
- **Omezení:** Mění chování veřejného `SelectedRowIds`/`CheckedRowsChanged`, TD ho dostane už publishem samotné DLL.

### A53 Shell zapisuje bloby preferencí celé a bez pořadí, přepíše zástupce, které mezitím zapsala agenda

- **Stav:** otevřeno · **P2 · M** (bod 1 je S)
- **Problém:** Shell drží snímek blobu `Shortcuts` a po každé změně plochy (★, přesun dlaždice) pošle celý seznam generickým `UserData.Save`. Agenda do téhož blobu za běhu zapisuje přes `DesktopShortcuts.Add/Remove` (read-modify-write na serveru) a shell blob znovu načte až po jejím skončení. Zápisy shellu jdou fire-and-forget, každý na vlastním klonu v jiném Caché procesu, takže pořadí nemají ani mezi sebou.
- **Důkaz:** `EDControls/CACHE/ALVA/EDControls/UserData.cls:29`. `DesktopShortcuts.cls:6-9` („přes generické Save by si souběžné zápisy modulu a shellu přepisovaly celý blob"). `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/FavoritesCoordinator.cs:122-126` `_ = _prefs()?.SaveShortcutsAsync(_shortcuts.Persistable())` ze `SyncFavorites` i `PositionsChanged` (`:34-36`), reload jen na `LegacyModuleExited` (`:59-61`). `Desktop/DesktopMiniAppView.xaml.cs:63-69` drop = `svc.Add` + `svc.PlaceAt`, dva souběžné zápisy. `EDControls/dotnet/RestCallRunner.cs:22-23`. Stejný vzor má `TreeExpansion`, `ModuleUsage` a `SelectedTab`.
- **Dopad:** Uživatel v agendě dá sestavu na plochu, agendu nechá otevřenou a v shellu klikne ★. Sestava zmizí i z Caché. Drop na buňku: když `Add` doběhne po `PlaceAt`, sedí dlaždice po restartu na první volné buňce. Dva shelly téhož uživatele (Spustit další EDISON) si bloby přepisují. P2, ne P1: ztratí se položka, kterou jde přidat znovu (jako u A45).
- **Návrh opravy:** (1, S) `ModulePreferencesService` posílá zápisy jednoho klíče za sebou: jeden v letu, čekající hodnotu nahradí nejnovější. (2, M) Plocha posílá změny místo celku: členství přes `DesktopShortcuts.Add/Remove`, pozice přes nový aditivní `DesktopShortcuts.Place` (mění jen `Col/Row` podle `KeyOf`). `SaveShortcutsAsync` zmizí.
- **Jak ověřit:** Caché `DesktopShortcutsTest`: `Add` a `Place` jiné položky, v blobu zůstanou obě. Integrační `UserPrefsTests`: „agenda" `Add`, shell přepne ★ jiného modulu, blob nese obě. Unit test s ručně dokončovanými tasky: dvě rychlé změny, na server dojde poslední stav.
- **Omezení:** JSON tvar položky a klíč `Kind|ElementId|Id` sdílí `EDISONMAIN2API/CacheShortcutSink` v repu EDISON. `DesktopShortcuts.cls` jen nové metody, nasadit před shellem. `IUserPrefsService` je veřejné API, bod 1 patří do shellu.

### A54 Zápisy na pozadí během přepnutí firmy odejdou pod novou firmu

- **Stav:** otevřeno · **P2 · S-M**
- **Problém:** `RestCallRunner` si na volajícím vlákně vezme jen referenci na sdílenou instanci. Klon `With()` vzniká až v `Task.Run` a čte `Idfa`, `DataNamespace` a `CacheLocks` v tu chvíli. Přepnutí firmy tutéž instanci přepisuje na místě, takže zápis založený těsně před přepnutím odejde pod novou firmou. Druhá cesta: miniaplikace (dashboardy s EDListem) se zavírají až po `Accepted`, kdy je `Idfa` už nová.
- **Důkaz:** `EDControls/dotnet/RestCallRunner.cs:20-23` (komentář slibuje snímek session). `MLRestAdapter.cs:58,71`. `EDISONMAIN2/dotnet/Startup/CompanySwitcher.cs:103-112`: `closeShellWindows()` a hned `mlRest.Idfa = newIdfa; mlRest.DataNamespace = newNamespace;`. Snímek slibuje i `EDControls/CLAUDE.md:88-90` a `EDControls.Tests/RestCallRunnerTests.cs:8-10`, test `:39-59` ověřuje jen výměnu instance. `EDList.xaml.cs:162-165` a `EDFilter.xaml.cs:128` volají `FlushAsync` v `Unloaded`. `EdisonMainViewModel.cs:679-681` `_miniApps.CloseSession()` až po switchi.
- **Dopad:** Uživatel změní šířku sloupce nebo filtr a do 500 ms přepne firmu. Layout se uloží pod IdFa cizí firmy (`UserData.cls:29`). Okno je úzké, ale dokumentovaný kontrakt neplatí a zdědí ho každý další zápis přes runner (A55).
- **Návrh opravy:** Runner přečte hodnoty session synchronně v `RunAsync` a klon z nich postaví až na pool vlákně (konstruktor `MLRest` dělá DNS dotaz). Např. internal `MLRestAdapter.SnapshotSession()` se zachovaným přeposíláním z A49. Miniaplikace zavírat spolu s okny shellu před swapem. Opravit text v CLAUDE.md a v testu.
- **Jak ověřit:** Test nad skutečným `MLRestAdapter`: volání čeká na bránu, mezitím se přepíše `Idfa` a `DataNamespace`, klon má původní hodnoty. Ověřit mutací (vrátit `With()` do `Task.Run`).
- **Omezení:** `IMLRest` je veřejné (nový člen jen publishem s TD), sémantika přeposílání A49 zůstává, `MLRest` z repa EDISON beze změny.

### A55 Geometrie okna a usage telemetrie se ukládají synchronně na UI vlákně přes sdílenou instanci

- **Stav:** otevřeno · **P2 · S-M** · až po A54
- **Problém:** Uložení geometrie okna (`Closing`) a odeslání telemetrie (`Closed`) jdou synchronně přes sdílený MLRest na UI vlákně. Výsledek nikoho nezajímá a chyba se polyká, uživatel přesto čeká na síť. `MLRest` má timeout 8 h, při výpadku rozhoduje TCP timeout. Preference stejného druhu přitom jdou na pozadí přes `RestCallRunner`.
- **Důkaz:** `EDControls/dotnet/WindowStatePersistence.cs:43` → `WindowStateStore.cs:87-88` `QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", ...)`, load `:78` synchronně v `SourceInitialized`. `EDViewModel/EDViewModel.cs:176` store nad `_services.MLRest`. `EDViewModel/EDUsageTelemetryHook.cs:66` → `EDUsageTelemetry.cs:74`, doc `:11` „nesmí ... blokovat zavření okna". `C:\MLineRepos\EDISON\MLRest\MLRest\MLRest.cs:2194-2195` `req.Timeout = 28800000`.
- **Dopad:** Při výpadku VPN každé zavření agendového okna zmrazí shell na dva pokusy o spojení (SaveParams, LogUsageBatch), otevření okna na jeden.
- **Návrh opravy:** `Save` a `Flush` přes `IRestCallRunner`, geometrie se na UI vlákně počítá už dnes. `Load` synchronní kvůli blikání, ale na klonu s horní mezí kolem 1 s, pak XAML default. Při ukončení krátce počkat na rozjeté zápisy, synchronní flush v `AppBootstrapper.cs:606-610` zůstává.
- **Jak ověřit:** Fake `IMLRest` s klonem visícím na bráně: zavření okna se vrátí hned a oba zápisy dorazí na klon. `Load` s visícím fake vrátí do limitu prázdnou geometrii.
- **Omezení:** `IWindowStateStore` je veřejné se synchronní signaturou, asynchronita jen uvnitř `CacheWindowStateStore`. Wire `SaveParams` a `LogUsageBatch` beze změny.

### A56 Binární kompatibilitu samostatně vydávané EDControls.dll nic nehlídá, veřejný povrch je větší, než konzumenti potřebují

- **Stav:** otevřeno · **P2 · M**
- **Problém:** Starý exe TD potká novou `EDControls.dll`. Build sln to nezachytí, protože TD se v něm přeloží znovu. Co je pro starý exe binárně breaking (nepovinný parametr navíc, změna návratového typu, chráněný člen `EDViewModel`), posuzuje jen člověk. Veřejných typů bez konzumenta mimo knihovnu a testy jsou přitom desítky a každý je stejný závazek.
- **Důkaz:** `EDControls/CLAUDE.md:8-15`. `EDControls/dotnet/EDControls.csproj:13` `PublishOwnAssemblyOnly`, v repu není `PublicAPI.*.txt` ani ApiCompat. Bez konzumenta v EDISONMAIN2, LegacyMiniApps, TD a EDTesting (grep): `ErrorReporter`, `ErrorReport`, `ErrorReportBuilder`, `ErrorContext`, `ErrorThrottle`, `MlineEnvironment`, `WindowDimmer`, `TaskbarIcon`, `CacheWindowStateStore`, `UserThemePreference`, `UserFontPreference`, `FontCatalog`, `InMemorySessionState`, `NullUserPrefsService`, `FieldTypeWire` (`EDInputForm/FieldType.cs:35`), `ColumnLayout`, `EDFilterPrefs`, `EDListFilter`, `EDNumericInput`, konvertory `EDInputForm`. Pravidlo `static readonly` se drží naprázdno (`ErrorReporting/ErrorReporter.cs:12-24`).
- **Dopad:** Někdo přidá parametr do `IPopupDisplayer.Info` nebo `EDViewModel.Command`. Sln, testy i pre-push zůstanou zelené a samostatně vydaná DLL shodí TD u zákazníků `MissingMethodException` při otevření okna.
- **Návrh opravy:** Guard v buildu EDControls: `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt`, RS0016/RS0017 jako chyby), nebo ApiCompat proti DLL vydané s posledním TD. Typy bez konzumenta na `internal` (`InternalsVisibleTo` pro testy existuje), `EDInputFormDesignTimeData` může zůstat kvůli designeru.
- **Jak ověřit:** Zkusmo nepovinný parametr do veřejné metody: build EDControls selže. Po internalizaci build sln, `tools/ThemeRender` a designer `EDInputForm.xaml` v pořádku.
- **Omezení:** Odebrání veřejných typů jen jedním publishem s TD a shellem (jako A22, A25, A38). Chráněné členy `EDViewModel` jsou kontrakt podtříd v TD.

### A57 Identita aplikace nemá jedno místo, TemplateDesigner posílá hlášení chyb bez verze

- **Stav:** otevřeno · **P2 · S**
- **Problém:** `EDServices` nese `FormIdPrefix` (default `"EDISON2"`), `AppDisplayName` a `AppVersion` jako nepovinné settery. Chybějící hodnotu každé místo knihovny řeší jinak: hlášení chyb nijak, „Informace o verzi" bere entry assembly, feedback panelu Podpora verzi assembly okna. Aplikace, která hodnotu nenastaví, se to nedozví.
- **Důkaz:** `EDControls/dotnet/EDViewModel/EDServices.cs:53-54,61`. `ErrorReporting/ErrorReportingPipeline.cs:211` `AppId = services?.FormIdPrefix ?? "EDISON2"`, `:217` `AppVersion = services?.AppVersion`. `ErrorReporting/CLAUDE.md:23` („bez ní je hlášení k ničemu"). `EDSupportPanel/SupportActions.cs:65-66,88-89`. `EDSupportPanel/SupportPanelWindow.xaml.cs:107-113` `v=` z verze assembly (1.0.0.0, žádný csproj nemá `<Version>`). `TemplateDesigner/dotnet/App.xaml.cs:40-43` nastaví jen `FormIdPrefix = "TD"`, handlery chyb visí už v konstruktoru (`:18-26`).
- **Dopad:** TD u zákazníků posílá každé hlášení chyby s `version: null` a podpora nepozná build. Pád TD před `OnStartup` přijde se `source="EDISON2"`. Feedback obou aplikací nese `v=1.0.0.0`.
- **Návrh opravy:** Jeden záznam identity (prefix, jméno, verze) s jedním pořadím fallbacků: explicitní hodnota, `AssemblyInformationalVersion` entry assembly, „nezjištěna". Čte ho hlášení, „Informace o verzi" i feedback. Shell nastaví `FormIdPrefix = "EDISON2"` explicitně, pipeline před `Initialize` vezme jméno z entry assembly. Verzi TD doplní vlastník TD.
- **Jak ověřit:** Test pipeline bez `AppVersion`: `data.version` není null. Test feedback URL s `AppVersion` služeb.
- **Omezení:** `EDServices` jen aditivně. `"EDISON2"` je součást uložených klíčů (formID v `ALVA.LIBRARY.Form`), shell ho zachová.

### A58 EDInputForm drží každou instanci v paměti, EDList po návratu do vizuálního stromu přestane ukládat layout

- **Stav:** otevřeno · **P2 · S**
- **Problém:** `EDInputForm` napojuje hledání toolbaru v konstruktoru přes `DependencyPropertyDescriptor.AddValueChanged` a nikdy ho neodhlásí. WPF registraci drží ve statické tabulce se silnou referencí na toolbar, přes rodiče tedy i formulář a okno. A3 odstranil hromadění handlerů, únik instance zůstal. `EDList` odhlásí události sloupců v `Unloaded`, znovu je přihlásí jen při přestavbě sloupců.
- **Důkaz:** `EDControls/dotnet/EDInputForm/EDInputForm.xaml.cs:168` `WireToolbarSearch()` v konstruktoru, `:187` `dpd.AddValueChanged(Toolbar, ...)`, žádné `RemoveValueChanged` ani `Unloaded`. EDList váže tentýž toolbar bindingem (`EDList/EDList.xaml:16`). `EDList.xaml.cs:159-166` jen `Unloaded` → `UnhookColumnEvents()`, `HookColumnEvents()` (`:245`) volá jen `RebuildColumns`.
- **Dopad:** Každé otevření dialogu s formulářem v TD (`ElementEditDialog`, `PrintDialog`, `DynamicTableEditorDialog` a další) nechá okno a VM v paměti do konce procesu. EDList s `HostId` v `TabItem` nebo doku po prvním přepnutí přestane ukládat šířky a řazení (dnes latentní, takový host není). P2, ne P1: velikost úniku nikdo neměřil.
- **Návrh opravy:** `EDInputForm` dostane DP `SearchText` svázanou s toolbarem jako EDList, descriptor zmizí. EDList v `Loaded` přihlásí, v `Unloaded` odhlásí a `RebuildColumns` přihlašuje jen při `IsLoaded`.
- **Jak ověřit:** STA test s `WeakReference` na `new EDInputForm()`: po `GC.Collect()` je pryč (dnes živý). EDList Unloaded → Loaded → změna řazení → Unloaded = jeden `SaveAsync` (dnes nula).
- **Omezení:** Veřejné API jen přidává.

### A59 Čtečky drátu EDListu a EDInputFormu padají na tvaru, který kontrakt nevylučuje

- **Stav:** otevřeno · **P2 · S**
- **Problém:** Hodnoty řádků EDListu čte `GetString()`, který na JSON čísle hází. Server číslo zapíše, kdykoli volající nastaví pole z `$order` nebo z výpočtu. Řádky formuláře i seznamu se navíc poznávají vylučovacím seznamem pojmenovaných klíčů, takže nový pojmenovaný klíč na jedné straně shodí starší verzi druhé strany. `HasFormItems` ve stejném kodeku přitom bere jen číselné subscripty.
- **Důkaz:** `EDControls/dotnet/EDList/EDListParsing.cs:84-85` `raw[prop.Name] = prop.Value.GetString();`, `:78` přeskočí jen `"ERR"`. `CACHE/ALVA/EDControls/EDList/List.cls:54-60` („Volající nastaví pole přímo"). TD to obchází `%Set(..., "string")` (`TemplateDesigner/CACHE/ALVA/TemplateDesigner/EDList/Instance.cls:74-77`, commit `8eccd6b9`). `EDInputForm/EDInputFormWire.cs:60-61` `if (kvp.Key is "ERR" or "helpTextMode") continue; CList row = kvp.Value;` proti `EDInputFormController.cs:55-60` (`int.TryParse(kvp.Key, ...)`). `CACHE/ALVA/EDControls/EDInputForm/Form.cls:238-242` vyjmenuje `trigger`/`changedKey`/`helpTextMode`, jinak `$li` → `<LIST>`.
- **Dopad:** Nový `LoadList` s číselným ID skončí `InvalidOperationException` a prázdným seznamem, ladí se na serveru, kde je JSON v pořádku. Nový pojmenovaný výstup `Form.WriteToOut` shodí `Init` všem TD se starší DLL, nový vstup z nové DLL shodí starší `Form.cls`.
- **Návrh opravy:** Klient tolerantní k typu: `Number` → `GetRawText()`, `True`/`False` → `"1"`/`"0"`, `Null` → null. Všechny tři čtečky berou jen číselné subscripty (C# `int.TryParse` jako `HasFormItems`, Caché `continue:idx'=+idx`).
- **Jak ověřit:** `EDListParsingTests` s `{"id":1,"pocet":142.5,"aktivni":true}` a s `OUT["total"]`, `EDInputFormParsingTests` s `OUT["title"]`, `FormTest` s `^||ML("IN","x")` před `ReadFromIn`.
- **Omezení:** Kontrakt „hodnoty jako string" v dokumentaci zůstává, klient jen přijme víc. Tolerantní `Form.cls` pomůže až po vydání Caché balíčku, do té doby nepřidávat do IN pojmenované klíče.

### A60 Pozice řádku FIELD jsou ručně zrcadlené na čtyřech místech, round-trip nehlídá žádný obecný test

- **Stav:** otevřeno · **P2 · S**
- **Problém:** Pořadí 19 pozic je ručně zapsané ve čtyřech metodách (zápis a čtení v Caché i v C#) a paritu hlídají asserty po jednotlivých pozicích. Caché čtení vědomě vynechává pozici 9 (`validationError`), což `EDInputForm/CLAUDE.md` nezmiňuje.
- **Důkaz:** `EDControls/CACHE/ALVA/EDControls/EDInputForm/Field.cls:67`. `Form.cls:249-277` čte podle `$ll>=N`, pozici 9 ne, zastaralé komentáře `:207-212` a `:224-227`. `EDControls/dotnet/EDInputForm/EDInputFormWire.cs:26-45` zápis, `:77-113` čtení. Commit `be6683f8` (B44: pozice 19 se ztrácela na zpáteční cestě, statické kontroly to nechytily). `EDISON2.IntegrationTests/InputFormTests.cs:41,44` hlídá jen `Assert.Equal(19, row.Length)`.
- **Dopad:** Další atribut pole znamená čtyři úpravy. Zapomenuté čtení v `ReadFromIn` zopakuje B44: hodnota zmizí po prvním `Validate` a všechny testy projdou.
- **Návrh opravy:** Testy idempotence, které se s novým atributem nemění. Caché `WriteToOut → IN → ReadFromIn → WriteToOut` a `$listsame` pro plně vyplněné pole každého typu (výjimka pozice 2 a 9). C# `SerializeItemsCVar(ParseItems(raw)) == raw`. Výjimku pozice 9 zapsat do CLAUDE.md komponenty.
- **Jak ověřit:** Dočasně odebrat čtení pozice 19 z `ReadFromIn`, nový test spadne.
- **Omezení:** Pořadí pozic je protokol, test ho jen fixuje.

### A61 Objekty Field platí jen do dalšího dotazu na server, control nenabízí přístup podle klíče

- **Stav:** otevřeno · **P2 · S** (API + oprava TD), M (merge)
- **Problém:** Každý `Init`, `Validate` a `Save` s položkami nahradí `Items` novými objekty a control nemá přístup k poli podle klíče. Hosté si `Field` hledají sami a podrží si ho. Veřejnou DP `Items` jde nastavit zvenku, controller o tom neví a serializuje svoje položky.
- **Důkaz:** `EDControls/dotnet/EDInputForm/EDInputFormController.cs:115` `Items = EDInputFormWire.ParseItems(output);`, serializace `:64`. `EDInputForm.xaml.cs:89-100` settable DP. Konzument prochází položky sám na 20 místech (19 TD, 1 shell), např. `TemplateDesigner/dotnet/Windows/ElementEditDialog/ElementFormBinder.cs:38`. `TemplateDesigner/dotnet/Windows/DynamicTableEditorDialog/DynamicTableEditorDialog.xaml.cs:97` uloží `_dataSourceField`, `:105` `Validate()` položky vymění a pojistka `:155-156` před `Save` zapisuje do objektu, který už ve formuláři není.
- **Dopad:** Pojistka zdroje dat v editoru tabulky TD se do `Save` nedostane. Zda uživatel o volbu přijde, záleží na ComboBoxu (neověřeno). Každý další host píše vlastní hledání a vlastní tichý no-op.
- **Návrh opravy:** `Field? GetField(string key)` na `EDInputForm` i controlleru a věta „`Field` platí jen do příštího dotazu na server" do CLAUDE.md komponenty. `DynamicTableEditorDialog` hledá pole až v OK (vlastník TD). Setter `Items` zúžit nebo předat controlleru. Merge odpovědi do existujících objektů je směr A15 (odložený).
- **Jak ověřit:** Unit test `GetField`. Ručně v TD: vybrat zdroj, přepnout záložku, OK.
- **Omezení:** Zúžení setteru `Items` je breaking (publish s TD). Merge mění i synchronní `Validate` (pasti A15), bez vlastníka ne.

### A62 Testovací šev bootstrapperu je null Application a přepsání většiny kroků

- **Stav:** otevřeno · **P2 · M**
- **Problém:** Testovací konstruktor předává `null!` místo `Application` a testovací podtřída přepisuje většinu kroků, unit testy tak ověřují pořadí stubů. Kroky, které sahají na `Application` (ShutdownMode, `MainWindow`, dialogy) nebo dostávají konkrétní `MLRest`, nemají automatický test a E2E je nepokrývá. Větve pro null Application prosakují do produkčního kódu i do veřejného API EDControls.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:49-58` („derived class MUSÍ přepsat všechny metody"), `:56` `this(null!, null, options)`. `EDControls/dotnet/EDAppearance.cs:24` `if (app is null) return;`. `Startup/ShellShutdown.cs:17`. `EDISONMAIN2.Tests/AppBootstrapperTests.cs:23-24` (20 `protected override`). `PreLoginContext(MLRest LoginRest, ...)` `AppBootstrapper.cs:248-253`, `PassesPreLoginGates(MLRest ...)` `:340-342`.
- **Dopad:** A51 ani past ze `Startup/CLAUDE.md` („jinak app umře, nebo proces nemá konec") nemají regresní test. Nový krok, který sáhne na WPF, v testu skončí NRE, chytí ho hranice `Run` a test padne na jiném assertu.
- **Návrh opravy:** WPF stranu za injektované rozhraní (např. `IStartupHost`: `ShowSetup`, `ShowLogin(vm)`, `ShowChangePassword`, `RenderNow`, `HandOff(shell)`, `Shutdown`), produkční nad `Application`, testy s fake hostem bez podtřídy. `PreLoginContext.LoginRest` jako `IMLRest`, null konstruktor zrušit.
- **Jak ověřit:** `AppBootstrapperTests` bez přepisu `PassesPreLoginGates` a `PrepareLogin`. Test stejného předání okna pro auto-login i dialog. `grep 'null!' AppBootstrapper.cs` prázdný.
- **Omezení:** Null guard v `EDAppearance.Initialize` je veřejné API (TD), zůstává. Pořadí kroků a jedna chybová hranice beze změny.

### A63 Změna hesla má tři vstupy, zapamatované heslo každý řeší jinak

- **Stav:** otevřeno · **P2 · S** · patří do chatu s A50
- **Problém:** `ChangePasswordWindow` volá server sám a každý ze tří vstupů řeší zapamatované heslo po svém. Menu ho po změně maže, i když patří jinému uživateli. Vlastnosti uživatele ho nechají.
- **Důkaz:** `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.Menu.cs:230-240` `if (changed == true && _prefs.RememberedPassword != null) { _prefs.RememberedPassword = null; ... }`. `Windows/UserProfileWindow/UserProfileViewModel.cs:99-103` jen `Info("Heslo bylo změněno.")`. Vynucená změna `Startup/AppBootstrapper.cs:437-452`. Blob patří `LastLoginName` (`Windows/LoginWindow/PasswordProtector.cs:29-36`), přihlášení jiného uživatele `LastLoginName` nemění.
- **Dopad:** Po změně hesla ve Vlastnostech uživatele se při dalším startu předvyplní staré heslo, login spadne na „Špatné heslo." a blob se zahodí. Po Přihlásit jiného uživatele smaže změna hesla z menu zapamatované heslo uživatele ze startu. P2: náprava je jeden neúspěšný login.
- **Návrh opravy:** Jedna metoda (např. `PasswordChange.Run(nav, rest, userName, prefs)`), blob maže jen když `PasswordProtector.Unprotect(blob, userName)` vrátí heslo. Profil ji dostane delegátem od VM shellu.
- **Jak ověřit:** Unit test pravidla: vlastní blob se smaže, cizí zůstane. Grep `ChangePasswordWindow(` najde vynucenou změnu a jedno sdílené místo.
- **Omezení:** Formát DPAPI blobu, při vynucené změně heslo neukládat.

### A13 Kontrakty k legacy endpointům: test šva jen pro část

- **Stav:** otevřeno, částečně (3c6a0e94, 52dff7b1, c3804594, e44439f0, ef05128a). Šev kryje `LoginAllowed`, `ALVA.SETUP.Main.LoadSettings` (všechny čtené klíče včetně `NucenyUpdatClentu` a `localSourcePath`), `GetEdisonAccess`, `GetUserDefaultForm`, `maSeUkoncit`, `PocetProvozovanychVozidel`, `ALVA.LIBRARY.Form` Load/SaveParams, chybovou cestu `Login` a `LogLogin`. `SessionLockTest` uklízí přes `LockClearExt`, skip je `$$$AssertSkipped` a runner ho počítá zvlášť (cc03e2ef). `LegacyDerivedAccess` má parity test proti legacy (fb72e3e5).
- **P2 · M**
- **Problém:** Šev chybí pro úspěšný `ALVA.SYS.Login.Login` (pozice 1,2,3,4,5,7,10), `ChangePassword`, `VerifyLic`, `LoadUser/SaveUser`, `LoadDatabaseList`, `SetUserDefaultForm`, `ResetSettings`, `ChangeLoginState` a `ALVA.LICENCE.Activation.GetList`. Nově (2026-10-07): miniaplikace čtou legacy endpointy podle pozic bez švu a mapování agenda → INF klíč má test jen proti opsaným hodnotám, i když testy umí legacy zdroj číst (`TreeGeneratorTests`, `LegacyDerivedAccessTests` pod `EDISON_LEGACY_ROOT`).
- **Důkaz:** `EDISON2.IntegrationTests/LoginContractTests.cs:5-8` („vyhrazený IT účet není"). `EDISONMAIN2/dotnet/Windows/LoginWindow/UserAuthenticator.cs:45-53`. `EDISONMAIN2.LegacyMiniApps/dotnet/LegacyDashboardLoader.cs:82-90` (`LoadData`), `DispecinkMiniAppViewModel.cs:70,89` (`GetPracoviste`, `GetJizdy`), pole 1-10 podle pozic `:95-98`. `EDISONMAIN2/dotnet/Windows/ChangelogWindow/InfChangelogSource.cs:64-91` zrcadlí `EDLIBRARY/clsConversion.vb`. Grep `Dispecink|LoadData|InfKeyFor` v `EDISON2.IntegrationTests` = 0.
- **Dopad:** Přeskládaná pole `Login` nebo `GetJizdy` v repu EDISON se ukážou až u uživatele (prázdný nebo posunutý panel). Nový alias v `ToInformationFile` = agenda bez novinek nebo s novinkami jiné agendy.
- **Návrh opravy:** (1) 🔸 testovací účet na DEV pro úspěšný `Login`. (2) Integrační testy `LoadData` s `refresh=0` (tvar bloků `appType`/`param`/`data`) a `GetPracoviste`/`GetJizdy` (počet polí). (3) Parity test `InfKeyFor` proti `ToInformationFile` přes `LegacyTreeSources`. (4) Ostatní endpointy bez vedlejších efektů.
- **Jak ověřit:** Integrační sada hlásí úspěšný `Login` a uvedené endpointy, parity test běží v pre-push s `EDISON_LEGACY_ROOT`.
- **Omezení:** Endpointy žijí v repu EDISON, testy jen čtou. `gPocZam`, `UpdateLicFromMlineServer` a `LoadData` s `refresh=1` mají vedlejší efekty, testy je volat nesmí.

### A15 EDInputForm je synchronní na UI vlákně, EDList má async cestu

- **Stav:** otevřeno, odloženo k diskusi rozhodnutím vlastníka (2026-10-06). V kódu nic není, implementace s testy je ve větvi `odlozeno/a15-async-edinputform` (`66ee715e`). Při obnově smazat poznámky o odložení v `EDControls/dotnet/EDInputForm/CLAUDE.md:36-38`, `EDInputForm.xaml.cs:710-711` a `EDInputFormController.cs:106`.
- **P2 · M**
- **Problém:** `EDInputFormController` volá `Init`, `Save` a `Validate` synchronně, validace na změnu pole jde jen odloženě za handlery hosta. TD na pomalé lince zamrzá při každé změně pole s `ValidateOnChange`.
- **Rozhodnutí vlastníka:** aplikuje se jen poslední odpověď (generation guard), rozepsané pole odpověď nepřepíše, odpověď se zapisuje do existujících objektů (merge, souvisí s A61). Pak celé A15 odloženo.
- **Pasti z pokusu (platí pro každou async variantu):** Odpověď dnes nahrazuje celé `Items`, async by zahodil fokus i rozepsaný text (textová pole zapisují `Value` až na `LostFocus`). Echo serveru přepisuje klientské úpravy hosta (TD `PrintDialog` a `__EXPORT_SPLIT`, `ElementFormBinder` mění `Required`, klient doplňuje `ListItems`), takže serializace zůstane za handlery hosta a merge hlídá všechny vlastnosti proti snímku odeslaného stavu. „Pole s fokusem" není „uživatel edituje": chránit jen `TextBox` s `BindingExpression.IsDirty`. Chyba serveru u hodnoty, kterou server v téže odpovědi změnil, se musí zachovat. Synchronní `Save()` musí běžící async odpověď zneplatnit.
- **Otevřená otázka:** stojí to, že TD nezamrzá, za merge a jeho složitost?
- **Omezení:** Veřejné API jen přidává.

### A64 VM shellu bere služby ze singletonu a ze statik, testy VM běží sériově

- **Stav:** otevřeno · **P3 · S**
- **Problém:** `EdisonMainViewModel` nemá konstruktor s `EDServices` a implicitně volá `EDViewModel()` → `EDServices.Current`. Viditelnost menu čte settable statiku `InternalDiagnostics.Enabled`. Ostatní VM oblasti konstruktor s `EDServices` mají.
- **Důkaz:** `EDControls/dotnet/EDViewModel/EDViewModel.cs:119` `public EDViewModel() : this(EDServices.Current)`, konstruktor s `EDServices` `:125`. `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs:133-148` bez `EDServices`, `Startup/AppBootstrapper.cs:544-549` je má v rozsahu. Harness `EDISONMAIN2.Tests/EdisonMainViewModelHarness.cs:110-127` `EDServices.Initialize(svc); ... finally { EDServices.Reset(); }`. `EdisonMainViewModel.cs:69,71` `InternalDiagnostics.Enabled`.
- **Dopad:** Testové třídy VM (122 volání `CreateVM`) běží sériově v `SharedStaticStateCollection` a každá nová musí do kolekce, jinak náhodně padá (hlídá `SharedStaticStateGuardTests`).
- **Návrh opravy:** `EdisonMainViewModel(EDServices services, ...) : base(services)`, příznak diagnostiky jako parametr ze `StartupOptions`.
- **Jak ověřit:** Harness bez `EDServices.Initialize`, třídy VM mimo `SharedStaticStateCollection`, `dotnet test EDISON2.sln` zelený.
- **Omezení:** EDControls beze změny, konstruktor VM volá jen `AppBootstrapper` a harness.

### A65 Dialogy shellu vznikají ve VM, kód po dialogu nejde testovat

- **Stav:** otevřeno · **P3 · S-M**
- **Problém:** `IWindowNavigator.ShowDialog` bere instanci `Window`, VM shellu proto dělá `new XxxWindow(...)` a výsledek čte z okna. Okno potřebuje STA vlákno a aplikační resources, takže kód po dialogu nemá test na úrovni VM.
- **Důkaz:** `EDControls/dotnet/EDViewModel/IWindowNavigator.cs:8-15`. `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs:126-131` (picker miniaplikací, `picker.Picked`/`Placed`), `:798-804` (novinky, `wnd.SnoozeChecked`), `:191` (profil). `EdisonMainViewModel.Menu.cs:70-77` (připojení, `window.Picked` → `Process.Start`), `:230-240` (změna hesla, A63).
- **Dopad:** Umístění nové miniaplikace, zápis odložení novinek, mazání zapamatovaného hesla a start instance proti vybranému serveru nemají test VM. Každý další dialog shellu tuhle část zvětší.
- **Návrh opravy:** V shellu rozhraní `IShellDialogs` s metodami, které vrací výsledek (`PickMiniApp`, `ShowChangelog`, `PickConnection`, `ChangePassword`, `ShowUserProfile`). Produkční implementace přes `IWindowNavigator`, VM ho dostane konstruktorem. Vzor má delegát `showWindow` v `Windows/ChangelogWindow/ChangelogPromptService.cs:17`.
- **Jak ověřit:** Testy VM „picker vrátí deskriptor a umístění → `Open` je dostane" a „změna hesla vrátí true → `RememberedPassword` je null".
- **Omezení:** `IWindowNavigator` (veřejné API) beze změny.

### A66 Code-behind shellu řídí pořadí startu okna a mapuje klíče záložek

- **Stav:** otevřeno · **P3 · S**
- **Problém:** Pořadí po vykreslení (načtení session → novinky shellu → výchozí okno) a pořadí napojení panelů na dock řídí code-behind a hlídají ho jen komentáře. Klíč záložky převádí na `TreeView` switch v code-behind a literály v XAML, i když `TreeTab` je deklarovaný jako jediný zdroj.
- **Důkaz:** `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs:155-175` („Pořadí je invariant"). `EdisonMainViewModel.cs:378-386` nastaví `_startupWindowOpened = true` před hledáním ve stromu. Napojení `EdisonMainWindow.xaml.cs:179-193`, `EdisonMainViewModel.cs:116,120` („Volat před AttachMiniAppHost"). Klíče `EdisonMainWindow.xaml.cs:293-301`, `EdisonMainWindow.xaml:499-514`, `DesignTimeData/EdisonMainWindowDesignTimeData.cs:80-87` proti `Modules/ModuleTreeState.cs:235` („Přidání záložky je změna jen tady").
- **Dopad:** Přeskládaný code-behind nebo nový krok po přihlášení rozbije start a žádný test nezčervená (E2E jede s `OpensStartupWindow=false`). Prohozené `Attach*` tiše zahodí obnovenou šířku stromu a výšku pruhu. Na nové záložce Enter v hledání nic neudělá.
- **Návrh opravy:** `Task StartAsync()` ve VM volaný z `ContentRendered`. Jedno `AttachShell(...)` místo tří metod volaných v pořadí. `ActiveTreeView` hledat podle `ItemsSource == SelectedTab.Tree`.
- **Jak ověřit:** Test VM „`StartAsync` otevře výchozí okno až po načtení stromu a po novinkách" (harness s `OpensStartupWindow=true`).
- **Omezení:** `TreeView.SelectedItem` bindovat nejde, výběr zůstává v code-behind. Klíče záložek jsou wire `GetTree` i persistence.

### A67 Hranice k legacy DLL nemá jedno místo: přetypování na MLRestAdapter a GetCompanyList třikrát

- **Stav:** otevřeno · **P3 · M**
- **Problém:** EDISONAPI, EDLicence a EDUsers chtějí konkrétní `MLRest`. Shell proto na několika místech přetypovává `IMLRest` na `MLRestAdapter` a pre-login část pracuje s `MLRest` přímo. `GetCompanyList` je obalený třikrát se dvěma způsoby hlášení chyb.
- **Důkaz:** Přetypování `EDISONMAIN2/dotnet/Startup/SessionSwitcher.cs:64`, `Startup/CompanySwitcher.cs:47`, `Windows/EdisonMainWindow/EdisonMainViewModel.cs:231` (výchozí `licenceSync`), `Startup/SessionFactory.cs:49` (`.Inner`). `Startup/AppBootstrapper.cs:512` `if (mlRest is not MLRestAdapter adapter) return;`, force-logout se pro jiný `IMLRest` tiše nenapojí. `GetCompanyList`: `AppBootstrapper.cs:311-314` a `CompanySwitcher.cs:46-50` hází, `Windows/SetupWindow/SetupWindow.xaml.cs:49-52` vrací bool. `new MLRest(` v `AppBootstrapper.cs:308`, `SessionFactory.cs:28` a v okně `SetupWindow.xaml.cs:49,62` (proti root CLAUDE.md).
- **Dopad:** `CompanySwitcher.LoadAllowed(IMLRest)` s mockem hodí `InvalidCastException`, proto ho VM dostává jako `Func` a bootstrapper i `SessionSwitcher` ho obalují virtual metodou. `SessionSwitcherTests` staví skutečný `MLRest`. VM složené bez `licenceSync` nad mockem spadne a `SessionLicenceCoordinator` výjimku spolkne. Změna hlášky nedostupného serveru = tři místa.
- **Návrh opravy:** Jeden internal adaptér legacy API v `Startup/` (`CompanyList(IMLRest, bool checkAccess)`, `ActiveLicence`, `SyncFromMline`, `OnSessionEnd(IMLRest, Action)`), jediné místo s `.Inner`, jiný typ než adaptér = výjimka. Produkční `licenceSync` z `AppBootstrapper.LicenceSyncFor`. `PreLoginContext.LoginRest` jako `IMLRest`.
- **Jak ověřit:** `grep -E 'MLRestAdapter\)|\.Inner' EDISONMAIN2/dotnet` najde jen adaptér, `SessionSwitcherTests` jedou s `MLRestMocks`.
- **Omezení:** Signatury legacy DLL (repo EDISON) beze změny. `IMLRest` je veřejné API, přidání členů = publish s TD.

### A68 Setup wizard: data předává statika SettingsLoader, logika je v code-behind

- **Stav:** otevřeno · **P3 · M**
- **Problém:** Bootstrapper volá `SettingsLoader.Load()` jen kvůli naplnění statiky `Current`, kterou čte konstruktor `SetupWindow`. Dev předvyplnění je statický přepínač procesu `SettingsLoader.UseDevDefaults`. Logika wizardu (uložit až po úspěšném testu, změna serveru uložení zamkne) je v code-behind a parametry konstruktoru „pro testy" nikdo nepoužívá. A11 tohle oddělení odložil, protože bez A10 by šlo jen o přesun. A10 je hotový.
- **Důkaz:** `EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:291` `SettingsLoader.Load();   // naplní Current ...`. `Windows/SetupWindow/SetupWindow.xaml.cs:33` `(SettingsLoader.Current ?? new SettingsLoader()).Server`, konstruktor `:22-31`, logika `:73-152`. `Startup/SettingsLoader.cs:16,39,49,86`. `Startup/StartupOptions.cs:72` `SettingsLoader.UseDevDefaults = UseDevDefaults;`. `EdisonMainViewModel.Menu.cs:82` volá `Load()` za běhu. `EDISONMAIN2.Tests/SharedStaticStateGuardTests.cs:26-29` je vede jako mutátory.
- **Dopad:** Wizard zapisuje `EDISONini.xml` sdílený s EDLOADERem a legacy. Pravidlo „uložit jen otestované připojení" nemá test a bez STA okna ho napsat nejde. Testy nad `SettingsLoader.Load` musí do sériové kolekce.
- **Návrh opravy:** `SetupWizardViewModel(ServerSettings initial, probe, databaseLoader, save)` se stavem a `CanSave`, okno jen binduje. `SettingsLoader.Load(bool devDefaults)` s hodnotou ze `StartupOptions`, výsledek předat wizardu parametrem. `Current` a `UseDevDefaults` zrušit.
- **Jak ověřit:** `grep SettingsLoader.Current` prázdný, seznam mutátorů v `SharedStaticStateGuardTests` kratší, unit testy VM na odemčení a zamčení uložení.
- **Omezení:** Zápis `EDISONini.xml` v legacy paritě (`restport`, `initialized`/`forceChange`=1, cizí uzly zůstávají).

### A69 Adresářová struktura shellu neodpovídá vrstvám

- **Stav:** otevřeno · **P3 · S-M**
- **Problém:** `Windows/EdisonMainWindow/Modules` drží model modulů, službu per-user blobů, načítání session dat a fakta stanice, které používá Startup, plocha i okno novinek. `Desktop` a `Modules` na sebe odkazují oboustranně, stejný cyklus A33 odstranil mezi `MiniApps` a `Modules`. Naopak `Startup/` drží služby, jejichž jediný konzument je okno shellu, proti pravidlu `EDISONMAIN2/CLAUDE.md:7-8`.
- **Důkaz:** `Desktop/ModuleShortcutsService.cs:2`, `Desktop/DesktopShortcut.cs:2`, `Desktop/DesktopMiniAppView.xaml.cs:4` → `Modules`. `Modules/FavoritesCoordinator.cs:6`, `Modules/ModulePreferencesService.cs:8`, `Modules/SessionDataLoader.cs:7` → `Desktop`. `Modules` dál používá `Startup/AppBootstrapper.cs`, `Startup/StartupOptions.cs`, `Windows/ChangelogWindow/ChangelogPromptService.cs`, `SupportStrip` i `Windows/LoginWindow/LoginWindow.xaml.cs`. Opačně: `Startup/AccessLoader.cs` a `LegacyDerivedAccess.cs` (konzument jen `SessionDataLoader.cs:45`), `LicenceNotice` v `Startup/LicenceService.cs:85-193` (jen `SessionLicenceCoordinator`), `UserProfileService.LoadDefaultWindow/SetDefaultWindow` (jen `SessionDataLoader` a VM), `LoginResult` v `Windows/LoginWindow/UserAuthenticator.cs:6-14` (identita session pro `SessionFactory`, `CompanySwitcher`, `StartupOptions`).
- **Dopad:** Plochu nejde vyjmout do samostatného projektu (jako `EDISONMAIN2.LegacyMiniApps`). Podle složky se nedá poznat, co patří jen oknu, a `Startup/CLAUDE.md` pokrývá kód, který s bootstrapem nesouvisí. Chybu to nezpůsobí.
- **Návrh opravy:** `dotnet/Modules/` (namespace `EDISONMAIN2.Modules`) pro model a session data: `IModule`, `ExecutableModule`, `ModuleFamily`, `ModuleTreeLoader`, `ModulePreferencesService`, `DesktopShortcutDto`, `SessionDataLoader`, `AccessLoader`, `LegacyDerivedAccess`, `ModuleTreeStation`, `MlineDomainDetector`, `InternalDiagnostics`, `ModuleLaunchabilityCheck`, výchozí okno. U okna zůstane `ModuleTreeState`, `FavoritesCoordinator`, `TreeTab`. `LicenceNotice` k `SessionLicenceCoordinator`, `SupportStrip` do `dotnet/SupportStrip/`, `LoginResult` do `Startup/`. Směr závislostí `Windows.*` → `Desktop` → `Modules` a `Startup` → `Modules`.
- **Jak ověřit:** `grep 'using EDISONMAIN2.Desktop' EDISONMAIN2/dotnet/Modules` prázdný, build a testy zelené.
- **Omezení:** Jen přesun, wire beze změny. Upravit `xmlns:modules` v `EdisonMainWindow.xaml:10` a usingy v `EDISON2.IntegrationTests`. Přesun koliduje se souběžnými chaty, dělat v klidném okně.

### A70 Buffer usage telemetrie nezná session, firmu drží jen ruční flush shellu

- **Stav:** otevřeno · **P3 · S**
- **Problém:** Záznamy jdou do statického bufferu bez identity session a odejdou se session aktuální při flushi, server z ní doplní IdFa a uživatele. Knihovna flushuje jen při zavření okna. Shell flushuje ručně při přepnutí uživatele a při ukončení, při přepnutí firmy ne.
- **Důkaz:** `EDControls/dotnet/EDViewModel/EDUsageTelemetry.cs:8-9,15,50-74`. `EDUsageTelemetryHook.cs:66`. `EDViewModel/LegacyModuleHost.cs:249` (launch agendy jde do téhož bufferu). `EDISONMAIN2/dotnet/Startup/SessionSwitcher.cs:85-86` a `AppBootstrapper.cs:606-610` flushují, `CompanySwitcher.Switch` ne.
- **Dopad:** Uživatel ve firmě A spustí několik legacy agend bez WPF okna a přepne na B. Záznamy odejdou pod B a statistiky ISYCloud připíšou použití firmě B.
- **Návrh opravy:** Hned flush v `CompanySwitcher` před swapem `Idfa`. Systémově si `Track` zapamatuje `Idfa` a `DataNamespace` a `Flush` pošle skupiny jiné firmy přes `With(idfa:, dataNamespace:)`.
- **Jak ověřit:** `Track` pod A, přepnutí na B, `Flush` jde na klon s `idfa: A`.
- **Omezení:** Wire `LogUsageBatch` a strana ISYCloud beze změny, signatury `Track`/`Flush` zachovat.

### A71 Detekce interní stanice M-line existuje dvakrát a pokaždé jinak

- **Stav:** otevřeno · **P3 · S**
- **Problém:** Legacy `IsDomainMline` je portovaný dvakrát: v knihovně pro hlášení chyb a v shellu pro skryté uzly stromu. Registrovou hodnotu každá kopie čte jinak.
- **Důkaz:** `EDControls/dotnet/ErrorReporting/MlineEnvironment.cs:47-49` bere jen `"1"`/`"true"`. `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/MlineDomainDetector.cs:45-50` bere i nenulové číslo jako VB `CBool`, stejně jako legacy `C:\MLineRepos\EDISON\EDCORE\clsCore.vb:642-655`. Použití `ErrorReportingPipeline.cs:41` a `ModuleTreeState.cs:19`.
- **Dopad:** Hodnota `-1` zapne v shellu interní uzly, ale hlášení chyb z téhož stroje přijdou bez `-internal` mezi zákaznická data. Změna pravidla = dvě místa a dvě sady testů.
- **Návrh opravy:** Jedna implementace v EDControls (injektovatelné zdroje, parsování podle `CBool`, statická fasáda `IsInternal` zůstane). Shell ji použije a `Suffix` si nechá.
- **Jak ověřit:** Testy z `MlineDomainDetectorTests` na sdílené implementaci, `grep IPGlobalProperties EDISONMAIN2` prázdný.
- **Omezení:** `MlineEnvironment.IsInternal` je veřejné, registrový klíč zapisuje legacy.

### A26 Sdílená DLL nese aplikačně specifické ikony 🔸

- **Stav:** otevřeno, odloženo rozhodnutím vlastníka (2026-10-06): ikony zůstávají v jednom katalogu `EDIcons.xaml`, sada se ještě může měnit. Zapsáno v `EDControls/CLAUDE.md:73-75`.
- **P3 · M**
- **Problém:** Z 64 klíčů je 36 jen TD (`TemplateDesigner/dotnet/Windows/MainWindow/MainWindow.xaml`), 19 jen shell, 3 jen `EDViewModel` StandardActions, sdílené jen `Help` a `Molecule`, 4 bez konzumenta (`AddElement`, `Barcode`, `PageProperties`, `Snap`). Ikony jdou přes `StaticResource`, odebraný klíč shodí starší exe při načtení okna.
- **Dopad:** Nová ikona TD toolbaru = změna EDControls a publish DLL i pro shell.
- **Návrh opravy (až vlastník rozhodne):** `TDIcons.xaml` v TD (zpočátku alias), slovník jen pro shell v shellu, v EDControls sdílený zbytek. Test „každý klíč `EDIcons.*` má konzumenta nebo je ve výjimkách". Odebrání jen koordinovaným publishem.
- **Omezení:** Klíče jen aditivně.

---

## V pořádku (ověřeno 2026-10-07)

- Update vrstva: `UpdateEngine` je jeden pipeline s virtuálními seamy a scénářovými testy, okno updateru má rozhodnutí ve statických `DecideClose`/`ShouldRunExe`, `EDUpdateContract` bez referencí sdílí kontrakt obou updaterů. Čtení `VERZE.INF` a kontrolu `MZ` z http zdroje mají `Updater` i `UpdateEngine` každý zvlášť, liší se jen retry, nález to není.
- Session: `SessionFactory` je jediné místo vzniku session, `SessionDataLoader` jediné síťové místo session dat (`WhenAll`, stav VM nemění), `ApplySessionData` je synchronní a `SessionEpoch` hlídá výsledky na pozadí.
- ACTIVITY zámek: `SessionLogout`, `CompanySwitcher.Switch`, `LicenceVerifier.ReacquireLock` a `LogoutWatcher` (`CacheLocks=2`) dodržují klony i pořadí, kryje to `CompanySwitcherTests` a Caché `SessionLockTest`.
- `StartupOptions` je jediné místo rozdílu Debug/Release v shellu, výjimkou je `VerzeInf.Current` sdílený s EDUPDATE2.
- `LocalPrefs` slučuje pod mutexem, `ConnectionOverride` a přesměrování přes EDLOADER jsou čisté funkce s testy.
- Plocha: `ModuleShortcutsService` drží položky, které strom nezná, a `IsLoaded` brání zápisu prázdna. `MiniAppManager` stojí za `IMiniAppHost`, zástupce pro `EDISONMAIN2API.exe` má zmrazený tvar (`ShortcutLaunchPayload`).
- EDControls: `MLRestAdapter.With` hlídá reflection test, `ErrorReportingPipeline` je instance za fasádou. `EdisonLaunchArgs`, `LegacyOutput`, `ColorValue` a `SharedJsonPreference` jsou jediná místa své konvence.
- Controls: `EDListController` bez WPF s klonem pro async a generation guardem, `.inc` konstanty hlídané z obou stran, `DebouncedUserPrefsWriter` sdílený, Ctrl+F toolbaru jeden binding na okno, `EDFilterComboBox` jediný filtrovaný combo.

## Uzavřené

| ID | Stav | Commit / důvod | Co se udělalo |
| --- | --- | --- | --- |
| A1 | vyřešeno | 88c1d986 | `Run()` má jednu chybovou hranici kolem celého startu, logout hook hned po `Accepted`. |
| A2 | vyřešeno | 5fcfa28e | `CacheUserPrefsService` jede na klonu per request, `Load` při selhání transportu hází, plocha se při chybě nevyprázdní. |
| A3 | vyřešeno | b793d9c4 | `EDInputForm` volá auto-Init jednou per `CacheClass` a toolbar napojuje v konstruktoru (zbylý únik instance je A58). |
| A4 | vyřešeno | 64f22346 | URL widgetu podpory skládá VM (`SupportWidgetUrl`) a notifikuje ho i po přihlášení jiného uživatele. |
| A5 | vyřešeno | 48ed0e77 | VM shellu rozdělen do `ModuleTreeState`, `FavoritesCoordinator`, `MiniAppSessionController`, `SessionLicenceCoordinator` a partial menu. Vlastník: limit ~1000 ř. |
| A6 | vyřešeno | 504c5022 | `EDServices.Current` zmizel z infrastruktury, `EDISONMAIN2.Tests` běží paralelně, sériově jen `SharedStaticStateCollection` (zbytek A64). |
| A7 | vyřešeno | cf2895fa | Launch a lifecycle legacy procesů je v `LegacyModuleHost` se závislostmi v konstruktoru, `WindowNavigator` je fasáda. |
| A8 | vyřešeno | 9c9ad8af | `C:\Edison` a launcher jen jako defaulty `LegacyModuleHost`, telemetrie a soubor hlášení přes `FormIdPrefix`. |
| A9 | vyřešeno | dcd46df4 | Každý session údaj v `EDServices` má jednoho zapisovatele. |
| A10 | vyřešeno | b4b4adf7 | Debug/Release rozhoduje jen `StartupOptions`, výjimka `VerzeInf.Current`. |
| A11 | vyřešeno | b0f2a38b | Dev server se předvyplní jen v DEBUG (zbytek oddělení statiky je A68). |
| A12 | vyřešeno | 9f193419 | `EdisonLaunchArgs` je jediný kodek launch kontraktu se sdílenou fixture v obou repech. |
| A14 | vyřešeno | 7f229e95 | `EDListController` bez WPF drží stav a dotazy seznamu. |
| A16 | vyřešeno | d6acfadd | `LoadList` bez sloupců hází. |
| A17 | vyřešeno | 5f21bf27 | `EDList.inc` s parity testy, server odmítne neznámý typ i zarovnání. |
| A18 | vyřešeno | 8032163f | `LoginViewModel` drží login workflow. |
| A19 | vyřešeno | 2f23a5b2 | Workflow profilu je ve VM přes port `IUserProfileForm`. |
| A20 | vyřešeno | bc42f82d | Layout docku se ukládá jen při `Closed` a jen pod firmou, jejíž layout dock drží. |
| A21 | vyřešeno | d5b25de0 | Fakta stanice nese injektovatelný `ModuleTreeStation`. |
| A22 | vyřešeno | 0c32f8ff | `EDInputForm.Initialized` odebrán. Vydání EDControls jen s TD a shellem ze stejného commitu. |
| A23 | vyřešeno | b7eb2c07 | `DebouncedUserPrefsWriter` sdílí EDList a EDFilter. |
| A24 | vyřešeno | d7ebfb12 | `tools/GenerateTree` je v sln, parita hlídá `TreeGeneratorTests`. |
| A25 | vyřešeno | e815da60 | Nepoužitý veřejný povrch controls odebrán (dávka publishe s A22 a A38, pokračování A56). |
| A27 | vyřešeno | 1e6bcc79, 48a1fdf8 | IMAGE/CODEBOOK, `Field.Format` a `UserHidden` označené jako rezervované. |
| A28 | vyřešeno | a759081a | Guardy plochy patří `ModuleShortcutsService`, handlery se odhlašují. |
| A29 | vyřešeno | cecce7b5 | `DesktopMiniAppViewModel.LaunchCommand` smazán. |
| A30 | vyřešeno | 6c339c97 | Oba drag&drop sdílí `DesktopDragGesture`. |
| A31 | vyřešeno | 7c559f56 | `Name` oddělený od `DisplaySuffix`, blob nese nedekorovaný název. |
| A32 | vyřešeno | 6de2da75 | `ExecutableModule` je třída s referenční rovností. |
| A33 | vyřešeno | 41d26c84 | Pruh podpory v `SupportStrip/`, plocha v `Desktop/`, `MiniApps` bez cyklu (nový cyklus `Desktop` ↔ `Modules` je A69). |
| A34 | vyřešeno | 922a5ec3 | Výchozí okno jen přes `ApiEdison.Get/SetUserDefaultForm`. |
| A35 | vyřešeno | 9500b872 | „Test funkcí" jen při `InternalDiagnostics.Enabled`. |
| A36 | vyřešeno | 41b7eed0 | `LegacyOutput` je jediný parser legacy `object[]` výstupů. |
| A37 | vyřešeno | 8bb667bb | Stav docku se ukládá pod klíčem `server:port\|idfa\|login`. |
| A38 | vyřešeno | 320d9fbd | `MLRestJson` smazán. |
| A39 | vyřešeno | 76866634 | `SharedJsonPreference<T>` pro font i theme. |
| A40 | vyřešeno | b5db882b | `ErrorReportingPipeline` instance za statickou fasádou, testy bez `[Collection]`. |
| A41 | vyřešeno | c8aec420 | Kontrakt `IPopupDisplayer.Error` (hlásí do `ErrorReports`) zapsán v dokumentaci. |
| A42 | vyřešeno | 3e6422a2 | Hledání v EDListu ignoruje diakritiku. |
| A43 | vyřešeno | dd2b071a | `ColorValue` je jediná definice barvy na drátě. |
| A44 | vyřešeno | df49af95 | Projekt `EDUpdateContract`, source-linky mezi exe zmizely, EDUPDATE2 umí http zdroj. |
| A45 | vyřešeno | a5774bd5 | `LocalPrefs.Save` slučuje pod mutexem. Přijaté riziko: jméno a heslo dvou instancí se slučují zvlášť. |
| A46 | vyřešeno | a4025673 | Publish shodí Debug legacy DLL, `global.json` připíná SDK. Bod 3 přijat: vydává se z libovolného stroje. |
| A47 | přijato | vlastník 2026-10-06 | TD bez .NET 10 runtime ukáže nativní hlášku .NET a to stačí. |
| A48 | vyřešeno | f2cdf2a7 | Async EDList jede na klonu. Část TD předána vlastníkovi TD. |
| A49 | vyřešeno | 1e59f083, cd6ee3ce | Klon téže session přeposílá `EdisonActivityFailed`/`EdisonVersionFailed` sdílené instanci. |
