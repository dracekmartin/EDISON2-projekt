# Architektonický backlog: EDISONMAIN2 + EDControls

Živý tracker nálezů z architektonického review z 2026-07-29
([arch-EDISONMAIN2-EDControls-2026-07-29.md](arch-EDISONMAIN2-EDControls-2026-07-29.md), commit `007f62c`).
Ten dokument je snapshot analýzy (evidence, dopad, návrh) a **nemění se**. Tady je plán
a stav.

**Hotovo: 5 / 22** (15 nálezů rozpadlých do 22 úkolů; `R1` má navíc 4 vnořené kroky)

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

- [ ] **Stav:** Open
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

- [ ] **Stav:** Open
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

- [ ] **Stav:** Open
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

- [ ] **Stav:** Open
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

- [ ] **Stav:** Open
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

- [ ] **Stav:** Open
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

- [ ] **Stav:** Open
- **P1 · L** · Závisí na: **R7**, těží z R2 a R12 · Blokuje: -
- **Co udělat:** Čtyři samostatné kroky, každý s přesunutými testy, každý vlastní commit
  (klidně i vlastní worktree). Doporučené pořadí od nejmechaničtějšího:
  - [ ] `ModuleTreeState` - kolekce stromů, filtrace, `ApplySearch`, persistence rozbalení.
        Většina logiky už je `internal static`, takže přesun je hlavně mechanický.
  - [ ] `FavoritesCoordinator` - `_shortcuts` wiring, `SyncFavorites`,
        `RefreshFamilyFavoriteFlags`, `RebuildFavoritesTree`, `PersistFavoriteSlots`.
  - [ ] `MiniAppSessionController` - `AttachMiniAppHost`, `AttachTreeLayout`,
        `PersistMiniAppLayout`, `RestoreMiniAppLayout`, `ReplaceMiniAppSession`.
        **Pozor:** persist při zavření shellu jde přes `EventTrigger Closed` →
        `CloseAllCommand` v XAML, což ze strany VM není vidět - nerozbij to.
  - [ ] Menu commandy (font, theme, `RunEdisonCopy`, `OpenWebUrl`, `ShowAppInfo`, chord)
        do vlastní partial třídy nebo `ShellMenuCommands`.
- **Zachovat:** `RebuildSessionState` jako jediný rozšiřovací bod pro session-scoped stav.
  To je dobrý vzor, jen dnes volá příliš mnoho věcí přímo.
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

- [ ] **Stav:** Open
- **P3 · M** · Závisí na: - · Blokuje: -
- **Co udělat:** `tools/GenerateTree` udělat `.csproj` v `EDISON2.sln`, vstupní
  `edisonnavbar.xml` (nebo jeho snapshot) do repa, a přidat kontrolu, že vygenerovaný
  výstup je identický s `Tree.cls` (parita test proti driftu; kandidát na pre-push).
  Do `EDISONMAIN2/CLAUDE.md` doplnit větu, že `Tree.cls` je generovaný a jak se
  regeneruje - dnes to ví jen doc komentář uvnitř třídy.
- **Záměrně NE:** Přesun katalogu modulů do Caché globálu (runtime editability).
  To bylo dřív explicitně zamítnuto do doby, než přijde reálný požadavek; tenhle úkol
  to neotvírá, řeší jen to, že generovaný soubor nikdo nehlídá a `elementId` v něm
  řídí licenční gate a aplikační přístup.
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

- [ ] **Stav:** Open
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

## Mimo scope tohoto backlogu

Zjištěno při review, ale patří jinam - nezakládej na to úkol tady:

- `TemplateDesigner` táhne `Newtonsoft.Json` 7.0.1 s high-severity CVE (`NU1903`),
  plus 12 dalších build warningů (CS0108, CS8602, CS4014). Samostatné zadání pro TD.
- `ServiceGtfsRt`: CS8604 v `Program.cs`.
