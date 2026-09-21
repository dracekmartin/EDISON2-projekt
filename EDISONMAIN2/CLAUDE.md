# EDISONMAIN2 - poznámky pro vývojáře

Hlavní WPF shell - login, výběr firmy, strom modulů, spouštění legacy `.exe` agend. Nahrazuje
VB.NET `EDISONMAIN`.

Bootstrap orchestruje `Startup/AppBootstrapper` (template method; vzhled/font záměrně před
loginem, ať je login dialog themovaný; strom modulů načítá až shell VM asynchronně). `Run` má
jednu chybovou hranici → `Shutdown()`: bez ní výjimka skončí v `DispatcherUnhandledException`
(Handled) a app žije dál s `OnExplicitShutdown` a login oknem, které během progress fáze zavření
ruší. Logout hook (`WireSessionLogout`) se zapojuje hned po `Accepted`, ne až se shellem, aby zámek
uvolnil i exit kvůli updatu. **Po návratu z `Run` bootstrapper dožil** - co běží dál, má vlastní
session-scoped objekt (`SessionSwitcher` = přihlášení jiného uživatele, `ShellShutdown` = vynucené
ukončení); skládání session sdílí obě cesty přes `SessionFactory.Establish`.
Login okno (jen Release dialog path) po OK zůstává jako progress podklad, zavírá se až na shell
`ContentRendered`; `HandOffLoginWindow` musí dát `Application.MainWindow` na shell **před**
restore `OnMainWindowClose` (implicitní je login okno) - jinak app umře, nebo proces nemá konec.
`Services/` složka neexistuje záměrně - single-consumer infrastruktura je ko-lokovaná se svým
consumerem, cross-cutting věci jdou do `EDControls`.
Připojení čte `SettingsLoader` z `EDISONini.xml` vedle exe (sdílený s legacy, EDUPDATE ho roznáší
ze serveru): **`connection@restport`, ne `@port`** (to je superserver); wizard zapisuje plnou
legacy paritu (`initialized`/`forceChange`=1), uzly mimo ni (`securedMode`) zachovává. Dev kopii
v repu **publish nesmí vydat** - v `C:\Edison\` leží živý soubor legacy aplikací
(`CopyToPublishDirectory="Never"` v csproj).

## Strom modulů = generovaný snapshot

`Fill*` v `CACHE/ALVA/EDISONMAIN2/API/Tree.cls` **needitovat ručně** - generuje je
`tools/GenerateTree.cs` z legacy XML (`edisontree`/`edisonnavbar`) + `EDFormInterfaceLic.vb`
v repu EDISON; sync i **vynucený** deploy dělá `tools/sync-tree.ps1` (generátor zapisuje mimo
Edit/Write, takže `compile-objectscript.sh` sám nefiruje). Splice anchor je komentář
`/// Generováno z legacy edisonnavbar.xml id=` - smazat ho znamená tiše rozbitý generátor.

Kořeny package 0/1 přejmenovává `GetTree` až za běhu (`"EDISON"` / název IdFa přes
`$$gIdFa^MlJFapi`) - parita s legacy `ApplicationTree.UpdateXmlTree`; do generovaných dat
se to zapsat nedá, závisí na přihlášené firmě. Ruční metody patří **nad** splice anchor.

⚠️ **Deploy `Tree.cls` musí být atomický.** Strom se načítá při loginu, takže nezkompilovaná
třída (nebo volání metody, která ještě není nahraná) shodí start shellu **všem na sdíleném
dev serveru**. Nikdy nenasazovat po částech a vždy si přečíst odpověď compile endpointu -
`compile-objectscript.sh` končí exit 0 i při selhaném uploadu a chyby píše jen na stderr.

## Session / ACTIVITY zámek

- Zámek vytváří `VerifyLic` při loginu; je v **temp globálu** - nezaniká se spojením, hard-kill
  ho neuvolní (uklidí až server idle timeout). V dev to tlumí deterministický session GUID
  (`#if DEBUG` v `SessionFactory.Establish`) - re-run přepíše týž klíč.
- Po `Accepted` nastaví `LicenceVerifier` sdílené session **`CacheLocks=1`** - requesty obnovují
  idle timer (jinak aktivní shell vypadá serveru jako idle) a detekují zaniklý zámek/verzi.
  Přepnutí firmy jede s `CacheLocks=0` po dobu verify **i logoutu staré firmy** (zámek nové
  firmy ještě/už neexistuje, LockExist by session shodil; `LicenceLoader` je taky request na
  sdílené instanci). Version gate jen při Locks=1; DEBUG posílá prázdnou `EdisonVersion`
  (dev verze `999999.9` by gate shodila; táž netriggeruje ani update check).
- `SelectedCompanyIndex` musí odpovídat `MLRest.Idfa` - tam má bootstrapper zámek; jiná volba
  rozbije koexistenci shell ↔ moduly. Startupní výběr: `CompanySwitcher.PickInitial`.
- `SessionLogout.Logout` jede na **klonu** `IMLRest.With(...)` - odhlášení staré firmy/session
  bez mutace sdílené instance. Kromě ACTIVITY zámku uklízí i **business zámky session**
  (`ALWA.SYS.Lock.LockClearExt` dle GUID, parita s legacy `EDUsers.Logout` - pod shell GUID
  zamykají i hostované moduly, zavírané killem). Úklid je **GUID-wide, ne per-IdFa**
  (`^CacheTempSYSLock` drží i `ACTIVITY/<idfa>~…`) a shell GUID přepnutí firmy přežije, proto
  `CompanySwitcher` po logoutu obnovuje zámek nové firmy (`LicenceVerifier.ReacquireLock`);
  guard `Tests/SessionLockTest`.
- `LogoutWatcher` pollí `maSeUkoncit` s `CacheLocks=2`, aby poll neobnovoval idle timer.
- Session-scoped stav načítá `Modules/SessionDataLoader` jedním zátahem přes klony
  (`RestCallRunner`); `EdisonMainViewModel.ApplySessionData` ho promítne **synchronně** - guardy
  `_suppressXxx` neplatí přes `await` a selhání loadu nechá předchozí stromy naživu.
  `InitializeAsync()` běží z `ContentRendered` **před** `OpenStartupWindowIfAny` (výchozí okno
  se hledá ve stromu); před prvním initem je `_modulePrefs` null → save cesty null-safe.
- Přepnutí firmy: index `SelectedCompanyIndex` se nastaví až po `Accepted` (`SelectedCompanyId`
  setter jen spustí `SwitchCompanyCommand`). `CompanySwitcher.Switch` je synchronní (UI thread
  na dialogy licenčního retry). Zavírání dvoufázově: WPF okna shellu **před** swapem Idfa (volají
  server přes sdílený MLRest), legacy moduly až po `Accepted`. Veto okna (`e.Cancel` v Closing)
  switch ruší - přeživší okno by mluvilo s cizí firmou; totéž switch user; confirm jen s moduly.
- Zavření shellu (= ukončení aplikace i všech agend) se potvrzuje v `Closing` přes
  `EdisonMainViewModel.ConfirmShellClose`, takže **každé programové ukončení po vzniku shellu
  musí jít přes `ShellShutdown.Force` / `SuppressCloseConfirm` na VM** - jinak force logout
  nebo version gate uvízne na dialogu, který smí uživatel odmítnout.

## UI pasti

- `EDFilterComboBox` (výběr firmy) **musí být mimo `<Menu>`** - WPF Menu krade fokus editovatelnému
  poli; horní lišta jsou proto dva `Menu` controly + combo v `DockPanel` mezi nimi (cena: šipkami
  se mezi menu nepřejde). `x:Name="TopMenu"` drží bindingy submenu (`ElementName`).
- **Spuštěné moduly** se značí dlouhým názvem modulu, jinak cestou agendou (`BuildModuleLabels`,
  klíč `exe|forma|param`; krátký název mají dvě agendy běžně shodný). Fallback = titulek bez
  koncového `[IDFa]` (suffix si přidávají legacy okna sama, `clsForm.ShowIDFA`). **Firma v titulku
  je jen u shellu**; titulek jde do jména tiketu v hlášení chyb (firma zvlášť polem `firma`).
- Hledání ve stromu: shoda na názvu **skupiny** zpřístupní celý její podstrom, jinak by byl
  nález slepá ulička (skupinu spustit nejde a potomci hledané slovo v názvu nemají).
- **Status bar** = segmenty ikona + hodnota, technika jen v ToolTipu. `DockPanel.Dock="Right"`
  i `Visibility="Collapsed"` fungují jen na **explicitním `<StatusBarItem>`** (na surový
  `TextBlock` se nepropíší); `<Separator/>` jen mezi vždy viditelné (u sbalitelného by visel).
- **Launcher panel**: zapuštěná plocha (`EDTheme.Bg.Sunken`) + jedna spojitá karta z `Bg.Surface` -
  v light je `Bg.Base` == `Bg.Surface`, bez zapuštění panel splyne s obsahem. `TreeTabControl`
  nese jen tab strip a content skrývá **prázdný `ContentTemplate`** (generátor kontejnerů
  nastavuje `Content` na datový record, lokální hodnota by přebila Setter ve stylu); stromy jsou
  sourozenci přepínaní dle `SelectedTab.Key` → všech pět žije pořád → do `Tag` datových šablon
  (most pro `ContextMenu`) nejde `AncestorType=TabControl`, jde tam `VmProxy`.
- **Identita záložky = `Modules/TreeTab`** - jeden seznam `TreeTabs` drží pořadí, klíč (server
  i persistence), ikonu, `Expandable`/`ServerLoaded`; přidání záložky = změna seznamu
  (+ control v kartě). `Key` je persistovaný: **přejmenování = ztráta uložené volby**.
- **Toolbar nad stromy**: Rozbalit/Sbalit cílí na aktivní záložku (`ActiveExpandableTree`); bulk
  změna v `SuppressExpansionSave()` + jeden `SaveExpansionAsync` - scope je **refcountovaný**
  (cesty se vnořují, plochý bool by vnitřní blok odemkl dřív). `SelectedModule` plní code-behind
  (`TreeView.SelectedItem` nejde bindovat); VM ho drží per záložku, přestavba stromů ho zahazuje.

Mini-apps (AvalonDock, pruh podpory, persistence layoutu): [dotnet/MiniApps/CLAUDE.md](dotnet/MiniApps/CLAUDE.md).

## Plocha se zástupci

Plocha je **nadmnožina oblíbených**: `ModuleShortcutsService` drží `DesktopShortcut` všech
`ShortcutKind`, ★ ve stromu umí jen `MenuModule`; identita = `Key` (`Kind|ElementId|Id`).
Položka, kterou aktuální strom nezná (licence, `hidden`), se **nezobrazí, ale uloží zpátky** -
jinak by ji první změna členství tiše smazala. Ostatní druhy (tiskové sestavy, pohledy DS)
zapisují agendy přímo do Caché (`ALVA.EDControls.DesktopShortcuts`, týž blob, který shell čte)
a spouští se 14. argem hosta; shell blob přenačte až po `LegacyModuleExited`. Název + cesta
dlaždice se odvozují ze stromu, do `DesktopShortcutDto` se nepersistují; text zmenšuje `Viewbox`
(`DownOnly`) a `MaxHeight` `TextBlocku` je jeho podlaha (z tokenu `FontSize` se spočítat nedá).
Pozice = absolutní `Column`/`Row` na `TileGridPanel` (v DTO `Col`/`Row`) - mřížka se
nepřeskládává, přesah řeší scroll; `ModuleShortcutsService.GridColumns` jen přiděluje pozice
novým a Caché `Add` musí při přepisu zachovat **obě** složky, jinak shodí uživatelovo rozložení.

## Výchozí okno (startup window)

Modul auto-otevřený po loginu (per-uživatel × IDFa, Caché feature #25).
`ExecutableModule.ElementId` = uložené `EDISFormEnum` id → picker/značení jede přes vlastní
strom, ne legacy AccessTree. Okno `Windows/UserProfileWindow` je EDInputForm
(`…EDInputForm.UserProfile`), ale **data drží klient, ne schema třída** - `Main.LoadUser/SaveUser`
jsou samy MLRest metody, volat je ze schematu by kolidovalo na `^||ML`.

## Novinky (changelog agend)

Auto-popup `Windows/ChangelogWindow` zrcadlí legacy infotexty: existence `<KLÍČ>_DIFF.INF` vedle
binárek = „jsou nové změny". Modální a **blokuje otvírání**: v `OpenModule` funnelu před spuštěním
agendy, shellový check před `OpenStartupWindowIfAny`; menu `LaunchModeless/Dialog` záměrně ne.
Max 1x za session per INF klíč (kryje i re-focus dedup `ShowLegacyAsync`). Snooze (UserPrefs
`ChangelogSnooze`) váže SHA-256 hash obsahu `_DIFF`, ne verzi - Caché-only patch nechává stale
`_DIFF` na disku a DEBUG verze je konstantní. `InfKeyFor` zrcadlí legacy
`EDConversion.Package.ToInformationFile` včetně výjimky NDMAIN. Vynucená cesta `ShowAlwaysAsync`
(Nápověda, kontextová menu) obchází dedup i snooze, bez `_DIFF` dá celou historii a bez INF vrací
`false` - volající to **musí** říct popupem, jinak klik nic neudělá; snooze checkbox je proto
dvoucestný a bez `_DIFF` zakázaný. Pozice se nepersistuje (`PersistedFormId => null`), centruje
`OwnerWindowCentering`.

## Legacy moduly - EDISONMAIN2API

Most do legacy světa: `EDISONMAIN2API.dll` (`c:\MLineRepos\EDISON\EDISONMAIN2API\`, .NET
Framework 4.8, deploy do `C:\Edison\`). Args: značka `"EDISONMAIN2"` + 17 polí (pořadí
`WindowNavigator.BuildLauncherArguments`); [13] je Base64 XML zástupce - prázdné u běžného
launche, s `CallType=Procedura` host místo formy spustí `ProcedureIO` agendy; [14] admin flag
(`EDServices.IsAdmin` → `MLProps.UserIsAdmin`, klíč pro `CAccess(useAdmin)` bypass práv) a [15]
právo update (`MayUpdate` → `MLProps.UserMayUpdate`, gate serverového update v legacy Nastavení);
host bez pole defaultuje na **false**. [16] osobní adresář a [17] e-mail z loginu
(`EDServices.UserDirectory`/`UserEmail` → `MLProps.UserDirectory`/`UserEmail`; bez nich agendy
exportují do kořene disku). Host nastavuje i `EDFormID` formy - agendy přes něj čtou
`AccessTable[EDFormID]`, bez něj jsou volby gate-ované právem zašedlé.
Vstupy: `TryGetFormFromArgs` (typ reflexí ve volajícím
assembly - retrofit `.exe`, jediný `EDISONMAIN/ApplicationEvents.vb`) a `TryRunFromArgs`
(`Assembly.LoadFrom`, generický host).

Pasti:

- Chyby hostované formy: `HostedErrorReporter` (host i `ApplicationEvents.vb`) → legacy
  `MLError.ShowError` (telemetrie až na tlačítko, source `EDISON`); výjimku z `Form.Load` na x64
  spolkne sám Windows (kernel callback), nevidí ji ani try/catch kolem `ShowDialog`. Tvrdý pád
  hlásí shell z exit kódu (`WindowNavigator.TrackExit` → `ReportLegacyExit`); kódy hosta 2 =
  launch fail, 3 = fatal po dialogu. Ověření bez pádu: Debug host + `EDISONMAIN2API_CRASHTEST=ui|bg`.
- Non-prázdný `shellGuid` → modul sdílí shellův GUID + `CacheLocks=1` (zánik shell zámku shodí
  i moduly). Prázdný (over-license retry před VerifyLic) → vlastní GUID, bez CacheLocks. Totéž
  dělá **TemplateDesigner** (WPF, bez hosta - `LaunchesDirectly`, session přebírá
  `CacheSchemaWiring`); requesty TD tak drží ACTIVITY historii shellu (F17). Legacy launchery TD
  (ISYJR2, MHD2Linky, TISKJR stub) guid propagují jen při `CacheLocks==1`.
- `ConfigureForm` staví `MLProps.AccessTable` z `licensedElements` - bez nich moduly gate-ující
  obsah přes `RightGranted` (např. DSINPUT Plánování) nezobrazí data.
- Host roztáčí formu přes **`ShowDialog`, ne `Application.Run`** (parita se starým `EDPROCESS`):
  legacy formy se zavírají samy - `DialogResult`, nebo `Close()` před otevřením sesterské formy
  přes `ShowEdisonForm` (ISYJR → ISYJR2) - a v `Application.Run` loopu tím umře proces.

Shell strana: `WindowNavigator.ShowLegacy*` (viz [EDViewModel/CLAUDE.md](../EDControls/dotnet/EDViewModel/CLAUDE.md));
admin override při out-of-license řeší `LicenceVerifier` (`ShowLegacyAndWait(..., shareShellSession: false)`).

Nástroje → „Spustit starý shell": `EDISONMAIN.exe` s `reason=2` (`RunEdison`), bez něj se
`frmNoMDI` odmítne spustit. Plain `Process.Start`, ne `ShowLegacy*` - starý shell nemá umřít
s tímhle (KillOnExit) ani být ve „Spuštěné moduly"; přes EDLOADER se k němu už nedostaneš.
