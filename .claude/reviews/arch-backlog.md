# Architektonický backlog: EDISONMAIN2 + EDControls

Živý tracker nálezů z architektonického review z 2026-07-29
([arch-EDISONMAIN2-EDControls-2026-07-29.md](arch-EDISONMAIN2-EDControls-2026-07-29.md), commit `007f62c`).
Ten dokument je snapshot analýzy (evidence, dopad, návrh) a **nemění se**. Tady je plán
a stav.

**Hotovo: 12 / 22 z review** (15 nálezů rozpadlých do 22 úkolů; `R1` má navíc 4 vnořené kroky)
**+ 3 / 8 z pozdějšího skenu** (sekce „Nálezy mimo původní review" dole)

## Jak s dokumentem pracovat

- Pořadí sekcí = **doporučené pořadí implementace**, ne priorita. Priorita (P1-P3)
  je u každé položky uvedená zvlášť.
- Jeden úkol = jeden `/wt` worktree = jeden chat. Detail nálezu si vytáhni z odkazu
  na review, tenhle dokument ho neopakuje.
- Hotovo: odškrtni `- [x]` a přepiš `**Stav:**` na
  `Done (YYYY-MM-DD) - co se reálně udělalo` (když se návrh během implementace změnil,
  napiš čím a proč, ne původní plán).
- Odložení: `**Deferred:** po review (YYYY-MM-DD) - důvod`.
- 🔸 = vyžaduje produktové rozhodnutí, ne jen technickou práci. Nezačínej bez odpovědi.
- Aktualizuj počítadlo „Hotovo" nahoře.

## Proč právě tohle pořadí

1. **Vlna 0** je pět malých nezávislých úkolů, které vyčistí warning stream a zapnou
   `WarningsAsErrors`. Od té chvíle každá další vlna dostane od kompilátoru zpětnou vazbu,
   kterou dnes nemá. Proto je to první, i když jsou to samé P2/P3.
2. **Vlna 1** jsou mechanické přesuny bez změny chování. Připraví terén (čistý accessor
   místo `!`, wire kodek mimo view, menší `AppBootstrapper`), aby velké refaktory
   nezačínaly v bordelu.
3. **Vlna 2** řeší P1 s přímým uživatelským dopadem (zamrzlé UI při loginu a přepnutí
   firmy) a hned na to navazuje zbytek session-switch workflow, protože se to všechno
   týká stejných tří metod.
4. **Vlna 3** je dekompozice `EdisonMainViewModel`. Až tady, protože ji předchozí vlny
   výrazně zlevní: async signatury a model záložek už budou hotové, takže se přesouvá
   hotový kód, ne rozpracovaný.
5. **Vlna 4** je EDControls a doúklid. Je zvlášť kvůli deploy modelu: `EDControls.dll`
   se nasazuje jako jedna volná DLL vedle obou exe a její API se smí měnit jen aditivně
   (root `CLAUDE.md`), takže se změny v ní vyplatí **dávkovat do jednoho publish okna**
   místo publikovat pětkrát za sebou.

Pořadí není dogma - když se objeví zákaznický tlak na konkrétní věc, přeskoč. Jediné
tvrdé závislosti jsou vypsané u položek v poli **Závisí na**.

---

## Vlna 0 - vyčistit warningy a zapnout gate

### R15a - CS8602 v `PeriodicRefreshMiniAppViewModel`

- [x] **Stav:** Done (2026-07-30) - řádky v konstruktoru prohozené dle návrhu, CS8602
  v `EDISONMAIN2` zmizel. Bez testu: přesun je čistě warning fix, chování hlídá kompilátor.
- **P3 · S** · Závisí na: - · Blokuje: R15b
- **Co udělat:** Prohodit dva řádky v konstruktoru - `RefreshNowCommand = Command(RefreshNow);`
  musí být před `_timer.Tick += ...`. Za běhu je to dnes neškodné (lambda se vyhodnotí až
  při `Tick`), jde čistě o warning.
- **Hotovo když:** `dotnet build` nehlásí CS8602 v `EDISONMAIN2`.
- **Detail:** [nález 15](arch-EDISONMAIN2-EDControls-2026-07-29.md#15-warning-stream-není-čistý-chybí-treatwarningsaserrors)

### R6 - `EDInputForm.Initialized` stíní `FrameworkElement.Initialized`

- [x] **Stav:** Done (2026-07-30) - přidán `SchemaLoaded`, `Initialized` je `new` +
  `[Obsolete]` a fajruje dál (invoke obalen `#pragma warning disable CS0618`).
  Migrováno **10** call sites, ne 8: 9 v TemplateDesigneru + `UserProfileWindow`
  v EDISONMAIN2. Bez xUnit testu - `EDInputForm` je WPF control a test assembly nemá
  STA harness; migraci hlídá build (nulová tolerance CS0618).
- **P2 · S** · Závisí na: - · Blokuje: R15b
- **Co udělat:** Aditivně přidat `SchemaLoaded` (nebo `FormInitialized`), vyvolávat obojí,
  na `Initialized` dát `new` + `[Obsolete]`. Migrovat 8 call sites v TemplateDesigneru.
  Aktualizovat `EDControls/dotnet/EDInputForm/CLAUDE.md`.
- **Pozor:** Odstranění `Initialized` je breaking change sdílené DLL - **v tomhle úkolu
  ho neodstraňuj**, jen deprekuj. Smazání až při koordinovaném publish obou aplikací
  (kandidát: konec vlny 4).
- **Hotovo když:** CS0108 zmizel z buildu, TD používá nový event, starý pořád funguje.
- **Detail:** [nález 6](arch-EDISONMAIN2-EDControls-2026-07-29.md#6-edinputforminitialized-stíní-frameworkelementinitialized)

### R15b - Zapnout `WarningsAsErrors`

- [x] **Stav:** Done (2026-07-30) - `<WarningsAsErrors>Nullable;CS0108</WarningsAsErrors>`
  v obou csproj, ověřeno probe souborem (nullable deref → `error CS8602`, build FAILED).
  Nad zadání doklizeno i satelitní test projekty, aby byl stream reálně nulový:
  5× CS8602 v `EDControls.Tests/UserPrefsTests.cs` a xUnit2031
  v `EDISONMAIN2.Tests/EdisonMainViewModelTests.cs`. Gate je ale jen v těch dvou
  csproj dle zadání (test projekty ho nedostaly).
- **P3 · S** · Závisí na: **R15a, R6** · Blokuje: - (ale chrání všechny další vlny)
- **Co udělat:** Do `EDControls.csproj` a `EDISONMAIN2.csproj` přidat
  `<WarningsAsErrors>Nullable;CS0108</WarningsAsErrors>`. Začít úzce, `TreatWarningsAsErrors`
  na celý projekt zvážit později - WPF `_wpftmp` projekty se buildí zvlášť a umí překvapit
  generovaným kódem.
- **Mimo scope:** `TemplateDesigner` a `ServiceGtfsRt` (14 dalších warningů včetně
  `NU1903` na `Newtonsoft.Json` 7.0.1) - samostatné zadání, tady scope nemíchat.
- **Hotovo když:** Build oba projekty projde s nulou warningů a nový nullable warning
  build shodí.
- **Detail:** [nález 15](arch-EDISONMAIN2-EDControls-2026-07-29.md#15-warning-stream-není-čistý-chybí-treatwarningsaserrors)

### R13c - „MHD (ukázka)" placeholder v Release buildu

- [x] **Stav:** Done (2026-07-30) - descriptor obalen `#if DEBUG`; třída
  `MhdSampleMiniAppViewModel` zůstává (nesmazána, jen se neregistruje). Test
  `LegacyMiniAppProviderTests.Sample_IsDebugOnly` ověřuje obě konfigurace, Release run
  potvrzen.
- **P3 · S** · Závisí na: - · Blokuje: -
- **Co udělat:** `Mhd.Sample` z `LegacyMiniAppProvider` obalit `#if DEBUG` (jako
  `DevSampleMiniApps`), nebo smazat - cross-assembly pipeline už ověřuje reálná
  `Mhd.Dispecink`, takže placeholder svůj účel splnil.
- **Proč brzy:** Je to jediný nález viditelný přímo v produkčním UI a je to pětiminutová
  práce.
- **Hotovo když:** V Release buildu se v menu Miniaplikace neobjeví.
- **Detail:** [nález 13](arch-EDISONMAIN2-EDControls-2026-07-29.md#13-mrtvý-a-nezapojený-povrch)

### R11 - `QueryOrThrow` existuje dvakrát + neodůvodněné `object[]`

- [x] **Stav:** Done (2026-07-30) - (a) extension deleguje na `MLRestAdapter`, kde
  `ThrowIfError` zůstal v jednom privátním `Checked<T>` helperu (4 opsaná těla → 1).
  (b) Odůvodnění doplněno tam, kde chybělo: `LicenceVerifier`, `SessionLogout`,
  `LogoutWatcher`, `WindowStatePersistence` (class-level, platí pro všechny 3 sity
  v souboru); `AccessLoader`, `Updater`, `UserAuthenticator`, `UserProfileService` už
  důvod měly. `ApiEdison` sity mají navíc poznámku, že jsou kandidát na `CVar`.
  Bez testu na delegaci: extension bere konkrétní `MLRest` (síť), nejde mockovat.
- **P3 · S** · Závisí na: - · Blokuje: -
- **Co udělat:** (a) `MLRestExtensions.QueryOrThrow` nechat delegovat na
  `MLRestAdapter` (jedno místo pravdy pro `ThrowIfError`, dnes je tělo opsané 4×).
  (b) Do všech 10 produkčních `object[]` call sites doplnit jednořádkové odůvodnění,
  jak vyžaduje root `CLAUDE.md`. Kde důvod není (typicky nové endpointy
  `ALVA.USERS.ApiEdison`), poznamenat kandidáta na převod na `CVar`.
- **Proč brzy:** Lehce se dotýká hodně souborů, takže je lepší to mít z cesty, než začnou
  velké refaktory a churn se začne překrývat.
- **Hotovo když:** Existuje jedna implementace `ThrowIfError` cesty; každé `object[]` volání
  má u sebe důvod.
- **Detail:** [nález 11](arch-EDISONMAIN2-EDControls-2026-07-29.md#11-imlrest-má-dvojí-api-a-queryorthrow-existuje-dvakrát)

---

## Vlna 1 - mechanické přesuny bez změny chování

### R8 - Wire kodek EDInputFormu mimo WPF control

- [x] **Stav:** Done (2026-07-30) - pět metod + oba separátory (`ListItemSep`,
  `ListKeyValSep`) v novém `EDInputFormWire`; `EDInputForm.xaml.cs` z 696 na 563 řádků.
  Přesměrováno 79 volání (controller 4, `EDInputFormParsingTests` 61,
  `ListItemsSerializationTests` 12, `EDInputFormControllerTests` 2) - testy zůstaly
  v původních souborech, jen míří na `EDInputFormWire`.
- **P2 · S** · Závisí na: - · Blokuje: R3 (jen jako vzor)
- **Co udělat:** Přesunout pět `internal static` metod (`SerializeItemsCVar`, `ParseItems`,
  `ParseHelpTextMode`, `SerializeListItems`, `ParseListItems`) z `EDInputForm.xaml.cs`
  do nové `EDInputFormWire` (nebo jako statické členy controlleru). Testy
  (`EDInputFormParsingTests.cs`, 1044 ř.) přesměrovat. `internal` viditelnost se nemění,
  `InternalsVisibleTo` už je nastavené. Aktualizovat odkazy v komponentovém `CLAUDE.md`.
- **Hotovo když:** `EDInputFormController` nereferencuje WPF typ `EDInputForm`; testy
  kodeku běží bez něj.
- **Detail:** [nález 8](arch-EDISONMAIN2-EDControls-2026-07-29.md#8-wire-kodek-edinputformu-žije-ve-wpf-controlu-controller-na-něm-závisí)

### R12 - `IMLRest?` nullability protéká do konzumentů (25× `!`)

- [x] **Stav:** Done (2026-07-30) - `EDServices.RequireMLRest` + `EDViewModel.Rest`;
  převedeno 18 `MLRest!` (ne 25 - část zmizela už dřív jinými změnami):
  `EdisonMainViewModel` 12, `UserProfileViewModel` 2, `AppBootstrapper` /
  `CompanySwitcher` / `UserProfileWindow` po jednom na `RequireMLRest`.
  **Odchylka od návrhu:** `EDList` nezůstal na `MLRest!` - jeho `EnsureConfigured()`
  teď vrací `IMLRest` (hláška pro hosta beze změny), takže v EDISONMAIN2 i EDControls
  je nula `MLRest!`, jak žádá „Hotovo když". Testy v `EDViewModelTests` (3).
- **P3 · S** · Závisí na: R15b (jinak nullable warningy nikdo neuvidí) · Blokuje: -
- **Co udělat:** Aditivně `EDServices.RequireMLRest` (throw s čitelnou hláškou) a
  v `EDViewModel` vedle `MLRest` přidat `protected IMLRest Rest => _services.RequireMLRest`.
  Post-login konzumenty (VM, services) převést z `MLRest!` na `Rest`. Controls
  (`EDList`, `EDInputForm`) nechat na jejich `EnsureConfigured` - mají vlastní hlášku
  pro hosta.
- **Proč před vlnou 2 a 3:** `EdisonMainViewModel` má 16 z těch 25 `!` a budeme ho celý
  přepisovat - lepší mít čistý accessor předem než refaktorovat a pak čistit.
- **Hotovo když:** Nula `MLRest!` v `EDISONMAIN2` a `EDControls`; selhání dá
  `InvalidOperationException`, ne NRE.
- **Detail:** [nález 12](arch-EDISONMAIN2-EDControls-2026-07-29.md#12-imlrest-nullability-protéká-do-konzumentů)

### R9a - WinForms color picker z `AppBootstrapper` ven

- [x] **Stav:** Done (2026-07-30) - přesunuto do `Startup/WinFormsColorPicker.cs`
  (`WinFormsColorPicker.Pick`), plné kvalifikace nahradily aliasy `Forms`/`Drawing`.
  `AppBootstrapper` z 540 na 505 řádků, wire-up v `InitializeServices` zůstal.
  Bez testu: bez WinForms dialogu ho nejde spustit headless.
- **P2 · S** · Závisí na: - · Blokuje: -
- **Co udělat:** ~40 řádků (`PickColorWithWinFormsDialog` + `Win32Window` shim) přesunout
  do vlastního souboru ve `Startup/`. Čistý přesun, wire-up v `InitializeServices`
  zůstává.
- **Proč teď:** Zmenší `AppBootstrapper` (534 ř.) před R2 a R9b, které v něm budou dělat
  strukturální změny.
- **Hotovo když:** `AppBootstrapper` neobsahuje WinForms dialog kód, color picker
  v EDInputFormu funguje.
- **Detail:** [nález 9](arch-EDISONMAIN2-EDControls-2026-07-29.md#9-appbootstrapper-míchá-bootstrap-runtime-session-switch-a-testovatelnost-dědičností)

---

## Vlna 2 - výkon a session workflow

### R2 - `RebuildSessionState` dělá 10 blokujících round-tripů na UI threadu

- [x] **Stav:** Done (2026-08-04) - **odchylky od návrhu, všechny vědomé:**
  (a) Load se vysekl do `Modules/SessionDataLoader` (+ `SessionData` DTO), VM ho jen
  aplikuje - `ApplySessionData` je záměrně **sync**, protože guardy `_suppressXxx` přes
  `await` neplatí. Přesun do `ModuleTreeState` v R1 tím zlevní podruhé.
  (b) **Jedna vlna, ne dvě**: na hotových stromech závisí až aplikace dat, ne jejich načtení.
  Prefs jdou sekvenčně v jedné větvi (`IUserPrefsService` drží sdílenou instanci MLRest,
  klon jí nejde podstrčit bez změny EDControls) → wall-clock ~4 round-tripy místo 9.
  (c) **Paralelní volání jen na klonech** (`RestCallRunner`): `QueryOrThrow` čte
  `RestStatus` až po `Query` a ten je per-instance, takže `Task.WhenAll` nad sdílenou
  instancí by tiše prohazoval chyby - to zadání nevědělo. Probe proti dev serveru potvrdila
  process-per-request (6 souběžných requestů = 6 různých `$JOB`, 956 ms vs 5041 ms
  sekvenčně), takže `^||ML` nekoliduje.
  (d) Ctor už nenačítá - `InitializeAsync()` z `ContentRendered`, shell je vidět hned.
  `LoginAsAnotherUser` a `RefreshLicense` jsou `CommandAsync`.
  (e) `GetAwaiter().GetResult()` je z EDISONMAIN2 pryč až na `Updater.Fetch` (HTTP na
  release server, běží před vznikem shellu, nesouvisí se session rebuildem) a dvě testovací
  `InitializeAsync()` v harnessech.
  (f) **Zbývá jeden blokující round-trip**: `CompanySwitcher.Switch` (VerifyLic) musí na UI
  threadu kvůli dialogům licenčního retry. Overlay ho nekryje - kandidát na follow-up.
  (g) Ze skeptického review vypadly dvě reálné pasti volného UI threadu, obě opravené:
  layout mini-apps se ukládá **před** posunem `Idfa` a po dobu swapu je persist potlačený
  (zavření shellu během rebuildu jinak uložilo dock staré firmy pod novou), a selhání
  rebuildu po už přepnuté session čistí stav (`ClearSessionState`) - držet stará data
  znamenalo spustitelné moduly mimo licenci nové firmy a zápis cizích preferencí do jejích
  blobů. Selhání při refreshi licence naopak předchozí strom drží (táž firma) - dva testy.
  (h) **Zbytkové riziko:** preference pořád jedou na sdílené instanci MLRest (klon jim nejde
  podstrčit bez změny EDControls), takže se teoreticky mohou potkat s `WindowStatePersistence`
  / `SessionLogout` na zavření okna během loadu. Oba konce chyby polykají, takže dopad je
  tichý (neuložená pozice okna, ACTIVITY zámek do idle timeoutu). Fix = `Func<IMLRest>`
  přetížení `CacheUserPrefsService` → **vlna 4** (tam se dávkuje publish EDControls).
  Testy: `SessionDataLoaderTests` (4, proti ručně psanému thread-safe fake `IMLRest` -
  NSubstitute není thread-safe) + 4 VM testy (prázdné stromy do initu, idempotence initu,
  overlay po dobu loadu, selhání loadu nechá předchozí strom).
- **P1 · M** · Závisí na: R12 (kosmeticky) · Blokuje: R14, výrazně zlevňuje R1
- **Co udělat:** Tři kroky, dají se commitovat zvlášť:
  1. `RebuildSessionState` na `async Task`, tři `GetTree` a pět `UserData.Load` paralelně
     (`Task.WhenAll`), celé pod `ActionInProgress()` (mechanika už existuje a používá se
     u launchů modulů). Pozor: `LoadPersonalization` závisí na hotových stromech
     (`AllExecutables()`), takže jsou to **dvě vlny requestů, ne jedna**.
  2. `ModulePreferencesService` celý na async (`LoadFavoritesAsync` atd.) - tím zmizí
     `GetAwaiter().GetResult()` z obou míst.
  3. Stromy stavět do lokálních kolekcí a `target.Clear()` + naplnění dělat až po úspěšném
     requestu, aby selhání nenechalo prázdný strom.
- **Proč tady a ne po R1:** Je to jediný nález s přímým uživatelským dopadem (zamrzlé UI
  na VPN při každém přepnutí firmy) a je M, ne L. Async signatury přežijí pozdější přesun
  do `ModuleTreeState` v R1 beze změny, takže se práce nezahodí.
- **Hotovo když:** Přepnutí firmy ukáže overlay a neblokuje UI thread; nula
  `GetAwaiter().GetResult()` v `EDISONMAIN2`; test pokrývá, že selhání requestu nenechá
  prázdný strom.
- **Detail:** [nález 2](arch-EDISONMAIN2-EDControls-2026-07-29.md#2-rebuildsessionstate-dělá-10-blokujících-caché-round-tripů-na-ui-threadu)

### R14 - `SelectedCompanyIndex` setter dělá modální dialog, síť a rollback

- [x] **Stav:** Done (2026-08-04) - dělané spolu s R2 (setter nemůže awaitovat, takže to
  jinak nešlo). `SelectedCompanyIndex` je čistý stav s private setterem, workflow je
  `SwitchCompanyCommand` = `CommandAsync<int>(SwitchCompanyAsync)`; `internal` seam kvůli
  testům (`AsyncRelayCommand` nemá awaitovatelný Execute). Rollback **zmizel** místo aby se
  sjednotil: index se nastaví teprve po `Accepted`, takže Refused/cancel jen notifikuje
  derived properties (`RaiseSessionPropertiesChanged`, jeden seznam místo dvou opsaných).
  **Odchylka:** binding zůstal na `SelectedCompanyId` (TwoWay), setter jen spouští command -
  `EDFilterComboBox` veřejný commit event nemá a doplnit ho = změna `EDControls.dll`, kterou
  backlog dávkuje do vlny 4.
- **P3 · S** · Závisí na: **R2** · Blokuje: -
- **Co udělat:** Property nechat jako čistý stav (`Set` + notifikace), workflow přesunout
  do `SwitchCompanyCommand` (`AsyncRelayCommand` - dostane zdarma double-click guard
  a napojí se na async z R2), bindovat z view na `SelectionChanged`. Rollback vyjádřit
  jedním `RaiseSessionPropertiesChanged()` helperem, ať existuje jeden seznam derived
  properties místo dvou opsaných.
- **Pozor:** Extrakce `CompanySwitcher` už proběhla dřív, tenhle úkol ji neopakuje -
  jde o zbytek workflow, který v setteru zůstal (confirm, 7× `OnPropertyChanged`,
  rollback).
- **Hotovo když:** Setter neobsahuje `Confirm` ani síťové volání; druhá vstupní cesta
  `SelectedCompanyId` nespoléhá na side effect závěrečného `OnPropertyChanged`.
- **Detail:** [nález 14](arch-EDISONMAIN2-EDControls-2026-07-29.md#14-selectedcompanyindex-setter-dělá-modální-dialog-síť-a-rollback)

### R9b - `SwitchUser` z `AppBootstrapper` do `SessionSwitcher`

- [x] **Stav:** Done (2026-08-11) - (a) `SwitchUser` je `SessionSwitcher.Switch()`; sdílené
  skládání session vytáhnuto do `SessionFactory.Establish` (+ top-level `ConnectionInfo`
  / `SessionContext`, dřív nested v bootstrapperu), takže startup i switch mají jednu
  implementaci a `SessionSwitcher` je testovatelný stejným `protected virtual` vzorem.
  (b) `CheckForUpdate` vrací `(bool ShouldExit, Task<ReleaseNoticeResult>? Notice)`,
  `_releaseNoticeTask` zrušen (jde parametrem do `BuildShellWindow`).
  **Nad zadání:** kritérium „žádné metody volané po startu" si vyžádalo i `ShellShutdown`
  (drží suppress hook shellu + `Application`) - `ShutdownForced`/`_suppressShellCloseConfirm`
  byly volané z activity/version gate handleru a z odpočtu odhlášení, tedy dlouho po `Run`.
  `BuildActivityFailedWire` a logout countdown proto berou lokální kopie polí, ať closure
  nedrží bootstrapper. **Zbývá:** `_startupLoginWindow` je pořád mutable pole, ale žije jen
  uvnitř `Run` (vytáhnout by znamenalo změnit signaturu `TryLogin` hooku) - vědomě ponecháno.
  Testy: `SwitchUser` sada přesunuta do `SessionSwitcherTests` (6, +2 nové: wire míří na
  novou session, connection dědí firmu). `ShellShutdown` bez testu - `Application.Shutdown`
  nejde headless.
- **P2 · M** · Závisí na: R9a, R14 (stejná oblast) · Blokuje: -
- **Co udělat:** (a) `SwitchUser` do vlastního `SessionSwitcher` (paralela ke
  `CompanySwitcher`, který přesně tuhle roli už má) - tím `AppBootstrapper` přestane žít
  po startu aplikace. (b) `_releaseNoticeTask` předat jako návratovou hodnotu
  `CheckForUpdate` (`(bool ShouldExit, Task<ReleaseNoticeResult>? Notice)`) místo mutable
  pole sdíleného mezi dvěma kroky workflow.
- **Záměrně NE:** Přepis hooků `protected virtual` na plnou DI. Dnešní varianta je
  zdokumentovaná a funkční, refaktor by byl velký. Přidej ale k test subclassům komentář
  o tom, co se stane, když někdo do `Run` přidá krok sahající na `_application`
  (dnes: NRE až za běhu testu).
- **Hotovo když:** `AppBootstrapper` nemá mutable stav ani metody volané po startu;
  `AppBootstrapperTests` procházejí.
- **Detail:** [nález 9](arch-EDISONMAIN2-EDControls-2026-07-29.md#9-appbootstrapper-míchá-bootstrap-runtime-session-switch-a-testovatelnost-dědičností)

---

## Vlna 3 - dekompozice shellu

### R7 - Identita stromu ve 3 paralelních kódováních na 7 místech

- [x] **Stav:** Done (2026-08-17) - **nález byl podhodnocený**: mezi review a implementací
  přibylo commitem `416970b` (regroup launcher panel) **čtvrté** kódování - pětice
  `Is*TabActive` + ordinálně řazené `TabActiveProperties`; coupling points ~11 místo 7.
  Zaveden `Modules/TreeTab` s **šesti** členy, ne čtyřmi z návrhu: přibyl `IconKey`
  (aby deskriptor nemusel do VM tahat WPF `ImageSource`) a `ServerLoaded` (odlišuje tři
  stromy z `GetModuleTree` od odvozených Oblíbené/Používané - bez toho nešlo `_sourceTrees`
  odvodit z téhož seznamu). Zmizely oba `switch 0-4`, `Is*TabActive`, `TabActiveProperties`
  i oba literály `"favorites"`; XAML má `ItemsSource` místo pěti ručních `TabItem`;
  code-behind má **jeden** handler na kartě místo 9 wiringů podle jmen `TreeView`
  (`SelectedItemChanged` i `KeyDown` bublají).
  **Odchylka:** `TreeTab_SelectionChanged` nezmizel jen tak - „výběr viditelné záložky"
  se přesunul do VM jako mapa výběru **per klíč záložky** (`_selectionByTab`), protože číst
  `TreeView.SelectedItem` po přepnutí by znamenalo zpátky vázat identitu na jméno controlu.
  Přestavba stromů mapu čistí (uzly jsou nové instance).
  Persistence `SelectedTab` je `string` klíč; migrační kód žádný - starý `int` shodí
  `GetValue<string>()` a existující catch degraduje na výchozí záložku.
  Testy: 8 indexových nahrazeno 5 klíčovými (neznámý klíč → fallback, unikátnost klíčů,
  obnovení výběru per záložka). Ověřeno i **reálným spuštěním shellu** - chybný XAML
  binding testy nechytí.
- **P2 · M** · Závisí na: - · Blokuje: **R1** (dělej před ním)
- **Co udělat:** Zavést deskriptor
  `record TreeTab(string Key, string DisplayName, ObservableCollection<IModule>? Tree, bool Expandable)`
  a odvodit z něj `ActiveExpandableTree()` (zmizí `switch 0-3` ve VM), `_sourceTrees`
  a `ExpandableTrees` (zmizí literál `"favorites"`). V XAML `TabControl` bindovat na
  `TreeTabs` místo pěti ručních `TabItem` - odstraní i 4× ruční wiring
  `SelectedItemChanged` a druhý `switch 0-3` v code-behind. Záložka Používané je `ListBox`,
  takže `Tree` je nullable a `Expandable=false` - právě to dnes kóduje `_ => null`
  v obou switchích.
- **Migrace:** `SelectedTreeTabIndex` persistovat jako `Key` string místo indexu.
  Jednorázová ztráta uložené volby záložky, dnes bezplatná (EDISONMAIN2 nikde neběží).
- **Proč před R1:** `ModuleTreeState` v R1 chce mít model záložek k dispozici. Bez toho
  se oba switche jen přestěhují a pak přepíšou znovu.
- **Hotovo když:** Nula magických indexů záložek v C#; přeuspořádání záložek je změna
  jednoho seznamu.
- **Detail:** [nález 7](arch-EDISONMAIN2-EDControls-2026-07-29.md#7-identita-stromu-je-rozprostřená-do-7-míst-ve-3-paralelních-kódováních)

### R1 - `EdisonMainViewModel` je God class shellu

- [ ] **Stav:** Rozpracováno (2026-08-17) - **příprava hotová, čtyři extrakce zbývají.**
  VM mezitím narostl z 1315 na 1552 řádků a testů na něj je 2067 v 5 souborech, takže nález
  je dnes větší, ne menší. Hotové mezikroky (každý vlastní commit):
  - Test harness VM byl duplikovaný **4×** (ne 2×) → jeden `EdisonMainViewModelHarness`.
    Bez toho by dělení testů podle nových tříd znamenalo opravovat totéž sedmkrát.
  - Guard `_suppressExpansionSave` → **refcountovaný** `SuppressExpansionSave()` scope.
    Nested-safe bylo dřív jen jedno z pěti míst (přes lokální `prev`); po rozpadu na tři
    třídy by plochý bool tiše odemkl persistenci uprostřed vnějšího bloku. Test na vnoření
    ověřen probem (s plochým resetem padá).
  - Viz taky **N1** a **N6** níž - obojí uklizeno před extrakcemi, protože by se jinak
    jen přestěhovalo.
  **Zbývá** (pořadí platí):
- **P1 · L** · Závisí na: **R7** (hotovo), těží z R2 a R12 · Blokuje: -
- **Co udělat:** Čtyři samostatné kroky, každý s přesunutými testy, každý vlastní commit
  (klidně i vlastní worktree). Doporučené pořadí od nejmechaničtějšího:
  - [ ] `ModuleTreeState` - kolekce stromů, filtrace, `ApplySearch`, persistence rozbalení.
        Většina logiky už je `internal static`, takže přesun je hlavně mechanický.
  - [ ] `FavoritesCoordinator` - `_shortcuts` wiring, `SyncFavorites`,
        `RefreshFamilyFavoriteFlags`, `RebuildFavoritesTree`, `PersistShortcuts`
        (v backlogu dřív `PersistFavoriteSlots`, tak se to nejmenuje). **Vyřeš u toho N7.**
  - [ ] `MiniAppSessionController` - `AttachMiniAppHost`, `AttachTreeLayout`,
        `PersistMiniAppLayout`, `RestoreMiniAppLayout`, `ReplaceMiniAppSession`.
        **Pozor:** persist při zavření shellu jde přes `EventTrigger Closed` →
        `CloseAllCommand` v XAML, což ze strany VM není vidět - nerozbij to.
        **Vyřeš u toho N2** (tři vstupy persistu, guard jen v jednom).
  - [ ] Menu commandy (font, theme, `RunEdisonCopy`, `OpenWebUrl`, `ShowAppInfo`, chord)
        do vlastní partial třídy nebo `ShellMenuCommands`.
- **Zachovat:** `RebuildSessionStateAsync` (+ `SessionDataLoader`) jako jediný rozšiřovací bod
  pro session-scoped stav.
  To je dobrý vzor, jen dnes volá příliš mnoho věcí přímo.
- **Tvrdý invariant, který review nezmiňuje:** `ApplySessionData` musí zůstat **synchronní**
  (guardy `_suppressXxx` neplatí přes `await`). Po rozpadu poběží přes hranici dvou tříd -
  musí to zůstat jeden nepřerušený synchronní průběh, jinak selhání loadu přestane nechávat
  předchozí stromy naživu. Podobně `_modulePrefs` je `null` do prvního `InitializeAsync`
  a čte ho 5 skupin → sub-třídy chtějí lazy accessor, ne vlastní kopii.
- **Hotovo když:** `EdisonMainViewModel` je pod ~400 řádky a orchestruje; sdílené
  `_suppressXxx` flagy patří té třídě, které se týkají; testy jsou rozdělené podle
  nových tříd, ne podle jednoho VM.
- **Detail:** [nález 1](arch-EDISONMAIN2-EDControls-2026-07-29.md#1-edisonmainviewmodel-je-god-class-shellu)

---

## Vlna 4 - EDControls a doúklid

**Dávkuj publish.** R4, R5, R3 a případné dokončení R6 mění `EDControls.dll`. Nasazuje se
jako jedna volná DLL vedle obou exe, takže se vyplatí je udělat za sebou a publikovat
jednou, ne po každém úkolu.

### R4 - EDControls natvrdo zná deployment a branding EDISONMAIN2

- [ ] **Stav:** Open
- **P2 · M** · Závisí na: - · Blokuje: R5 (stejný soubor)
- **Co udělat:**
  1. `EDControls.EDAppIdentity` - statická konfigurace nastavovaná v composition rootu:
     `DeployRoot`, `PersistedFormIdPrefix`, `LegacyLauncherExe`, `LegacyLaunchSentinel`.
     Defaulty na dnešních hodnotách, takže EDISONMAIN2 nemusí nastavovat nic a chování
     se nemění.
  2. `EDViewModel.PersistedFormId` čte prefix z `EDAppIdentity`; TemplateDesigner si
     v `App.xaml.cs` nastaví `"TD."`. **Dnes okna TD persistují pod `EDISON2.*` formID** -
     tohle je opraví. Je to migrace persistovaného stavu TD oken, dnes akceptovatelná,
     ale ať je to vědomé rozhodnutí, ne side effect.
  3. `BuildLauncherArguments` má používat `EdisonLaunchArgs.Sentinel`, ne literál
     `"EDISONMAIN2"` v interpolovaném stringu.
- **Hotovo když:** V EDControls není hardcoded `C:\Edison`, `EDISONMAIN2API.exe`,
  sentinel ani prefix `"EDISON2."`.
- **Detail:** [nález 4](arch-EDISONMAIN2-EDControls-2026-07-29.md#4-edcontrols-natvrdo-zná-deployment-a-branding-edisonmain2)

### R4b 🔸 - TD ikony ve sdílené knihovně

- [ ] **Stav:** Open
- **P3 · S** · Závisí na: - · Blokuje: -
- **Rozhodnout:** `EDControls.csproj` a `EDIcons.xaml` obsahují ~40 ikon, které používá
  výhradně TemplateDesigner (`rect_element`, `qr_code`, `departure_grid`, `align_*`,
  `distribute_*`, `snap`, `page_properties`, ...). Buď je přesunout do vlastního
  `ResourceDictionary` v TD, nebo (pokud je sdílený katalog záměr) doplnit do
  `EDControls/CLAUDE.md` explicitně, že `EDIcons` je katalog napříč aplikacemi včetně
  TD-specifických ikon - dnes to vypadá jako omyl a každá nová TD ikona nutí publikovat
  `EDControls.dll` i pro shell.
- **Detail:** [nález 4](arch-EDISONMAIN2-EDControls-2026-07-29.md#4-edcontrols-natvrdo-zná-deployment-a-branding-edisonmain2)

### R5 - `EDServices.Current` prosakuje do infrastruktury a vypnul paralelizaci testů

- [ ] **Stav:** Open
- **P2 · M** · Závisí na: R4 (stejný soubor) · Blokuje: -
- **Co udělat:** Singleton nerušit - u `EDList` a `EDInputForm` je opravdu potřeba
  (nesmí záviset na `Window.DataContext`). Zastavit jeho šíření:
  1. `WindowNavigator` dostane `EDServices` konstruktorem (composition root ho vytváří
     první, `EDServices` se plní mutable settery, takže stačí předat instanci; případně
     `Func<EDServices>`). Otevře to `LaunchLegacy` a `WireUp` testům.
  2. `WindowStatePersistence.Attach(window, formId, IMLRest?)` jako nové přetížení,
     staré ponechat (aditivní).
  3. Ověřit, jestli `EDISONMAIN2.Tests` můžou zapnout paralelizaci zpět a nahradit
     assembly-wide `DisableTestParallelization` per-collection atributem jen na
     harnessech, které `EDServices.Initialize` reálně volají.
- **Hotovo když:** `EDServices.Current` čtou už jen controls a `RelayCommand` fallback;
  `EDISONMAIN2.Tests` běží paralelně.
- **Detail:** [nález 5](arch-EDISONMAIN2-EDControls-2026-07-29.md#5-edservicescurrent-static-singleton-vypnul-paralelizaci-celé-test-assembly)

### R3 - `EDList` nemá controller seam

- [ ] **Stav:** Open
- **P2 · M** · Závisí na: R8 (jako vzor) · Blokuje: -
- **Co udělat:** Vyseknout `EDListController(IMLRest, string cacheClass)` po vzoru
  `EDInputFormController`: `InitializeList()` → `IReadOnlyList<ListColumn>`,
  `LoadList(filter)` → `IReadOnlyList<EDListRow>` (včetně formátování a sort keys),
  `LoadLayoutAsync`/`SaveLayoutAsync` nad `IUserPrefsService` a `ColumnLayout` DTO
  (bez `DataGridColumn`). V code-behind zůstane mapování `ColumnLayout` ↔
  `DataGrid.Columns` a event wiring. `ColumnLayoutSerializer` už je oddělený, takže
  půlka práce je hotová.
- **Hlavní přínos:** Testy na lifecycle `LoadList` - reset checked state, unsubscribe
  starých řádků, re-apply search a layoutu. Dnes to `EDList/CLAUDE.md` vypisuje jako
  pasti a nic to nehlídá.
- **Hotovo když:** Instanční logika `EDList` je testovatelná proti `IMLRest` mocku;
  code-behind je pod ~400 řádky.
- **Detail:** [nález 3](arch-EDISONMAIN2-EDControls-2026-07-29.md#3-edlist-nemá-controller-seam-na-rozdíl-od-edinputform)

### R10 - `tools/GenerateTree` mimo build, žádný drift guard

- [ ] **Stav:** Částečně hotovo (2026-08-26), zbytek Open
- **P3 · M** · Závisí na: - · Blokuje: -
- **Hotovo 2026-08-26:** advisory detekce driftu (`.claude/hooks/tree-drift.sh` +
  fingerprint `tools/tree-sources.sha256`, který píše generátor), `tools/sync-tree.ps1`
  (regenerace + vynucený deploy + diff), nenulový exit generátoru při nezapsání,
  a poznámka v `EDISONMAIN2/CLAUDE.md`. Blokující pre-push brána **vědomě ne**:
  fingerprint se porovnává proti lokální kopii legacy repa, takže by se mýlila oběma
  směry (kolega nepushnul / já nepullnul) a padala by na cizí commit.
- **Zbývá:** `tools/GenerateTree` jako `.csproj` v `EDISON2.sln` + parita test, že
  vygenerovaný výstup je identický s `Tree.cls`. Pozor, `ResolveExe` sonduje `C:\Edison`,
  takže bit-parita je závislá na stroji - test musí porovnávat jen část odvozenou z dat.
- **Proč to není kosmetika (incident 2026-08-26):** splice anchor byl doc komentář
  `/// Naplní strom ...` (s em dashem); comment audit (`9f68304`) ho smazal jako WHAT
  narraci s zakázaným em dashem a generátor tím **tiše rozbil na 6 dní** - nezapsal
  a skončil s exit 0. Snapshot byl mezitím stale o tři legacy commity. Anchor je teď
  provenance komentář a nezapsání vrací nenulový kód, ale parita test by to chytil dřív.
- **Vstupní podmínka migrace do Caché:** dnešní `Tree.cls` je **ztrátová** projekce XML -
  chybí `info` (343 uzlů), `form` (31), `color` (28), `icon`/`iconFav` (7 v navbaru)
  a roots `-1` Oblíbené + `-101` Systém (generují se jen `-2`/`-3`/`-4`). Caché nemůže být
  master, dokud se to nedoplní.
- **Druhý konzument (jiné repo!):** `ALWA.LICENCE.API.Catalog` (repo EDISON) volá
  `ALVA.EDISONMAIN2.API.Tree.GetTree` pro všechny tři stromy, tj. Caché jako master už
  bere - a chybějící `-101` si drátuje ručně (`..push` na 999 / 999.1 / 15). První
  konkrétní krok migrace je proto doplnit `-101` do `Tree.cls` a ten hardcode smazat.
  Drift testy EDISON2 tenhle konzument **nevidí**; změna wire formátu `GetTree` ho rozbije.
- **Záměrně NE (revidovat):** přesun katalogu do Caché globálu byl dřív zamítnut do
  příchodu reálného požadavku. Směr „master do Caché" je teď žádaný, ale cesta
  (obrácený generátor vs. legacy čte z Caché za běhu) je k 2026-08-26 **nerozhodnutá**.
- **Hotovo když:** Ruční editace `Tree.cls` shodí kontrolu.
- **Detail:** [nález 10](arch-EDISONMAIN2-EDControls-2026-07-29.md#10-treefillmain-je-656řádková-generovaná-classmethod-generátor-mimo-build)

### R13a 🔸 - `EDComponentToolbar` Export a Print

- [ ] **Stav:** Open
- **P3 · S** · Závisí na: - · Blokuje: -
- **Rozhodnout:** `ShowExport`/`ShowPrint`/`ExportCommand`/`PrintCommand`/`SettingsCommand`
  nikdo nikde nezapíná (0 hitů napříč všemi projekty včetně TD), buttony existují.
  Buď export doimplementovat v `EDList` (reálně užitečné, `SnapshotLayout` k tomu má data),
  nebo API deprekovat a smazat při koordinovaném publish. V obou případech opravit
  `EDControls/CLAUDE.md`, které dnes tvrdí, že toolbar má „(search, export, ...)".
- **Detail:** [nález 13](arch-EDISONMAIN2-EDControls-2026-07-29.md#13-mrtvý-a-nezapojený-povrch)

### R13b 🔸 - `ShowWindowsAsDialogs` toggle bez efektu

- [x] **Stav:** Done (2026-09-17) - toggle odstraněn z menu, VM i LocalPrefs.
- **P3 · S** · Závisí na: - · Blokuje: -
- **Rozhodnout:** Pref se persistuje a má UI toggle v menu, ale nikdo ho nečte. Buď
  doimplementovat (`WindowNavigator.Show` → `ShowDialog` podle prefu), nebo odstranit
  z menu. Toggle, který nic nedělá, je horší než chybějící toggle.
- **Detail:** [nález 13](arch-EDISONMAIN2-EDControls-2026-07-29.md#13-mrtvý-a-nezapojený-povrch)

### R13d - `EDList` typy IMAGE a CODEBOOK bez renderingu

- [ ] **Stav:** Open
- **P3 · S** · Závisí na: R3 (dělej v rámci nebo po něm) · Blokuje: -
- **Co udělat:** Buď rendering doimplementovat, nebo při `InitializeList` na deklarovaný
  ale neimplementovaný typ hlásit chybu (fail fast na wire kontraktu) místo tichého
  fallbacku na text. Dnes může Caché autor poslat `type: "IMAGE"` a dostane raw název
  resource bez jakéhokoli varování.
- **Detail:** [nález 13](arch-EDISONMAIN2-EDControls-2026-07-29.md#13-mrtvý-a-nezapojený-povrch)

### R6b - Dokončit odstranění `EDInputForm.Initialized`

- [ ] **Stav:** Open
- **P2 · S** · Závisí na: **R6** + koordinovaný publish obou aplikací · Blokuje: -
- **Co udělat:** Smazat deprekovaný `Initialized` z `EDInputForm`. Až tady, protože je to
  breaking change public API sdílené DLL, takže musí jít do produkce společně s publish
  všech konzumentů (root `CLAUDE.md`).
- **Hotovo když:** Event je pryč, TD používá výhradně nový, oba exe jsou publikované
  ze stejného commitu.
- **Detail:** [nález 6](arch-EDISONMAIN2-EDControls-2026-07-29.md#6-edinputforminitialized-stíní-frameworkelementinitialized)

---

## Nálezy mimo původní review (sken 2026-08-17)

Před vlnou 3 proběhl nezávislý sken na strukturální dluh, který review z 2026-07-29 nezná
(kód se mezitím posunul o 16 commitů v `Windows/EdisonMainWindow/`). **Žádná nová God class
nevznikla** - všech 7 tříd přidaných po review je malých, jednoúčelových a otestovaných.
Tyhle věci ale review nezachytil:

### N1 - Dvě implementace vynuceného ukončení

- [x] **Stav:** Done (2026-08-17) - VM měl vlastní `ShutdownWithoutConfirm` volající
  `Application.Current.Shutdown()`, přestože `ShellShutdown.Force` z R9b má být jediné místo
  pravdy (a `EDISONMAIN2/CLAUDE.md` to tvrdilo). Tři exit cesty (`ExitApplication` + dvě
  licenční větve `LoginAsAnotherUser`) proto neměly test - sahaly na statický
  `Application.Current`. Teď jde ukončení delegátem z composition rootu; 5 nových testů.
- **P2 · S** · Vyřešeno v rámci přípravy na R1.

### N6 - Volitelné ctor parametry VM s produkčně významnými defaulty

- [x] **Stav:** Done (2026-08-17) - `shortcuts = null` tiše vyrobilo vlastní
  `ModuleShortcutsService` (★ ve stromu odpojené od plochy), `changelogSource = null` tiše
  vyplo novinky. Zapomenutý parametr v composition rootu tedy **nespadl**, jen degradoval
  featuru. Obojí je teď povinné; `restRunner` zůstává volitelný (je to skutečný test seam).
- **P3 · S** · Vyřešeno v rámci přípravy na R1.

### N8 - Test harness VM duplikovaný 4×

- [x] **Stav:** Done (2026-08-17) - dvě `CreateVM` + dvě ruční konstrukce v testech.
  Sjednoceno do `EDISONMAIN2.Tests/EdisonMainViewModelHarness`.
- **P3 · S** · Vyřešeno v rámci přípravy na R1.

### N2 🔸 - Persistence layoutu miniaplikací má tři vstupy a guard jen v jednom

- [ ] **Stav:** Open
- **P2 · S** · Závisí na: - · Blokuje: R1 krok `MiniAppSessionController`
- **Co udělat:** Cesty jsou `Closing` → `ConfirmShellClose`, `EventTrigger Closed` →
  `CloseAllCommand`, a `SwitchCompanyAsync` / `ReplaceMiniAppSession`. Při normálním zavření
  shellu se layout ukládá **dvakrát**; `_sessionSwapInProgress` kryje jen switch větev.
  Sjednotit do jedné cesty s jedním guardem **před** extrakcí controlleru - jinak extrakce
  tiše ztratí jednu větev. `_sessionSwapInProgress` vyjádřit jako `BeginSessionSwap()`
  vracející `IDisposable`, ne pole VM ani privátní bool controlleru.

### N7 - `ModuleShortcutsService` mutuje sdílený stav, guard je v jiné třídě

- [ ] **Stav:** Open
- **P3 · S** · Závisí na: - · Blokuje: R1 krok `FavoritesCoordinator`
- **Co udělat:** Službu drží VM i `DesktopMiniAppProvider`; mutace probíhá uvnitř služby
  (`Seed` přepisuje `Items` i `module.IsFavorite`), ale jediná ochrana proti save-loopu je
  `_suppressFavoritesSync` ve VM - a ten se z `Items.CollectionChanged` nikdy neodhlašuje
  (anonymní lambda v ctoru). Guard patří k tomu, kdo kolekci mutuje: buď „seed/batch" režim
  ve službě, který event nefajruje, nebo odhlásitelný handler v koordinátoru.

### N3 - Login workflow rozprostřený do čtyř míst, bez VM

- [ ] **Stav:** Open
- **P2 · M** · Závisí na: - · Blokuje: -
- **Co udělat:** `Windows/LoginWindow/LoginWindow.xaml.cs` v code-behind **provádí
  autentizaci**, drží politiku zapamatovaného hesla, hostuje tři WebView2 widgety a od
  2026-08-06 i **progress state machine startupu** (`EnterProgressState`, `ShowProgress`,
  `CloseAfterHandoff`). Bootstrapper ho řídí zvenčí a čeká na něj vlastní `DispatcherFrame`.
  Je to jediný netriviální workflow shellu bez VM a bez testu na UI vrstvě, a po review
  vyrostl. Review ho neřešil - v `007f62c` byl login jen `ShowDialog`.
- **Souvisí:** zbytek R9b („`_startupLoginWindow` je pořád mutable pole") je ve skutečnosti
  tenhle nález - pole čte pět míst a `ShowProgress` z něj dělá živý UI driver až do
  `ContentRendered` shellu.

### N4 🔸 - `DesktopMiniAppViewModel.LaunchCommand` nemá ani jeden binding

- [ ] **Stav:** Open
- **P3 · S** · Závisí na: - · Blokuje: -
- **Rozhodnout:** View volá službu přímo (`Vm?.Shortcuts.RequestLaunch(s)`), zatímco
  sourozenecký `RemoveCommand` bindovaný je - dva příkazy téže třídy jedou dvěma kanály.
  Buď command zapojit, nebo smazat. Stejná třída nálezu jako R13, jen vznikla **po** review.

### N5 - Duplikované drag&drop workflow ve dvou code-behindech

- [ ] **Stav:** Open
- **P3 · S** · Závisí na: - · Blokuje: -
- **Co udělat:** `EdisonMainWindow.xaml.cs` a `MiniApps/DesktopMiniAppView.xaml.cs` mají
  identický vzor `_dragStart` / práh `SystemParameters.Minimum*DragDistance` / `DoDragDrop`
  plus flag „potlač klik po tažení" (`_treeDragActive` vs `_suppressClick`). Dvě kopie téže
  politiky ve dvou vrstvách, obě bez testu.

---

## Mimo scope tohoto backlogu

Zjištěno při review, ale patří jinam - nezakládej na to úkol tady:

- `TemplateDesigner` táhne `Newtonsoft.Json` 7.0.1 s high-severity CVE (`NU1903`),
  plus 12 dalších build warningů (CS0108, CS8602, CS4014). Samostatné zadání pro TD.
- `ServiceGtfsRt`: CS8604 v `Program.cs`.
