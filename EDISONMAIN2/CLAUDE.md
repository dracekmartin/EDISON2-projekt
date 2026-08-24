# EDISONMAIN2 - poznámky pro vývojáře

Hlavní WPF shell - login, výběr firmy, strom modulů, spouštění legacy `.exe` agend.
Nahrazuje VB.NET `EDISONMAIN`.

Bootstrap orchestruje `Startup/AppBootstrapper` (template method: prefs → vzhled/font -
záměrně před loginem, ať je login dialog themovaný → setup wizard → firma + pre-login
MLRest → login → session MLRest → VerifyLic → update check → shell okno; strom modulů
načítá až shell VM asynchronně). **Po návratu z `Run` bootstrapper dožil** - co běží dál,
má vlastní session-scoped objekt (`SessionSwitcher` = přihlášení jiného uživatele,
`ShellShutdown` = vynucené ukončení); skládání session sdílí obě cesty přes
`SessionFactory.Establish`.
Login okno (jen Release dialog path) po OK zůstává jako progress podklad, zavírá se až na shell
`ContentRendered`; `HandOffLoginWindow` musí dát `Application.MainWindow` na shell **před**
restore `OnMainWindowClose` (implicitní je login okno) - jinak app umře, nebo proces nemá konec.
`Services/` složka neexistuje záměrně - single-consumer infrastruktura je ko-lokovaná
se svým consumerem, cross-cutting věci jdou do `EDControls`.

## Session / ACTIVITY zámek

- Zámek vytváří `VerifyLic` při loginu; je v **temp globálu** - nezaniká se spojením,
  hard-kill ho neuvolní (uklidí až server idle timeout). V dev to tlumí deterministický
  session GUID (`#if DEBUG` v `EstablishSession`) - re-run přepíše týž klíč.
- Po `Accepted` nastaví `LicenceVerifier` sdílené session **`CacheLocks=1`** - requesty obnovují
  idle timer (jinak aktivní shell vypadá pro server jako idle) a detekují zaniklý zámek/verzi.
  Přepnutí firmy jede s `CacheLocks=0` po dobu verify **i logoutu staré firmy** (zámek nové
  firmy ještě / už neexistuje, LockExist by session shodil; `LicenceLoader` je taky request
  na sdílené instanci). Version gate běží jen při Locks=1; DEBUG posílá prázdnou
  `EdisonVersion` (dev verze `999999.9` by gate shodila; táž verze netriggeruje ani update check).
- `SelectedCompanyIndex` musí odpovídat `MLRest.Idfa` - tam má bootstrapper zámek;
  jiná volba rozbije koexistenci shell ↔ moduly. Startupní výběr:
  `CompanySwitcher.PickInitial` (priorita `LastIdfa` → `IsDefault` → první).
- `SessionLogout.Logout` se volá na **klonu** `IMLRest.With(...)` - lze odhlásit starou
  firmu / session bez mutace sdílené instance. Kromě ACTIVITY zámku uklízí i **business
  zámky session** (`ALWA.SYS.Lock.LockClearExt` dle GUID, parita s legacy `EDUsers.Logout`) -
  pod shell GUID zamykají i hostované moduly a ty se zavírají killem. Ten úklid je ale
  **GUID-wide, ne per-IdFa** (`^CacheTempSYSLock` drží i `ACTIVITY/<idfa>~…`) a shell GUID
  přepnutí firmy přežije, proto `CompanySwitcher` po logoutu obnovuje zámek nové firmy
  (`LicenceVerifier.ReacquireLock` s už načtenou licencí); guard `Tests/SessionLockTest`.
- `LogoutWatcher` pollí `maSeUkoncit` s `CacheLocks=2`, aby poll neobnovoval idle timer.
- Session-scoped stav (access, stromy, preference, výchozí okno) načítá
  `Modules/SessionDataLoader` na jeden zátah přes klony (`RestCallRunner`, viz root CLAUDE.md);
  `EdisonMainViewModel.ApplySessionData` ho promítne **synchronně** - guardy `_suppressXxx`
  neplatí přes `await` a selhání loadu nechá předchozí stromy naživu. Ctor VM data nenačítá:
  `InitializeAsync()` běží z `ContentRendered` **před** `OpenStartupWindowIfAny` (výchozí
  okno se hledá ve stromu); do prvního initu je `_modulePrefs` null → save cesty null-safe.
- Přepnutí firmy: `SwitchCompanyCommand` (`SelectedCompanyId` setter ho jen spustí); index se
  nastaví až po `Accepted`, odmítnutí nepotřebuje rollback. `CompanySwitcher.Switch` je synchronní
  (UI thread na dialogy licenčního retry), VM ho obaluje `ActionInProgress` + yield. Zavírání
  dvoufázově: WPF okna shellu **před** swapem Idfa (volají server přes sdílený MLRest), legacy
  moduly až po `Accepted` - neúspěšné přepnutí je nezabije. Veto okna (`e.Cancel` v Closing)
  switch ruší - přeživší okno by mluvilo s cizí firmou; totéž switch user; confirm jen s moduly.
- Zavření shellu (= ukončení aplikace i všech agend) se potvrzuje v `Closing` přes
  `EdisonMainViewModel.ConfirmShellClose`, takže **každé programové ukončení po vzniku shellu
  musí jít přes `ShellShutdown.Force` / `SuppressCloseConfirm`** - jinak force logout
  nebo version gate uvízne na dialogu, který smí uživatel odmítnout.

## UI pasti

- `EDFilterComboBox` (výběr firmy) **musí být mimo `<Menu>`** - WPF Menu krade fokus
  editovatelnému poli. Horní lišta je proto rozdělená na dva `Menu` controly (`EdisonMenu` =
  jen položka EDISON, pak combo v `DockPanel`, pak `TopMenu` se zbytkem); cena: šipkami se
  mezi menu nepřejde. `x:Name="TopMenu"` drží bindingy submenu (`ElementName`).
- **Spuštěné moduly** se značí dlouhým názvem modulu, jinak cestou agendou (`BuildModuleLabels`,
  klíč `exe|forma|param`) - krátký název mají dvě agendy běžně shodný. Fallback (mimo strom,
  WPF okno) = titulek bez koncového `[IDFa]`; **titulky oken si `[IDFa]` drží** (`WindowNavigator`).
- Hledání ve stromu: shoda na názvu **skupiny** zpřístupní celý její podstrom, jinak by byl
  nález slepá ulička (skupinu spustit nejde a potomci hledané slovo v názvu nemají).
- **Status bar** = segmenty ikona + hodnota, technika jen v ToolTipu. `DockPanel.Dock="Right"`
  i `Visibility="Collapsed"` fungují jen na **explicitním `<StatusBarItem>`** (na surový
  `TextBlock` se nepropíší); `<Separator/>` jen mezi vždy viditelné (u sbalitelného by visel).
- **Launcher panel** = zapuštěná plocha (`EDTheme.Bg.Sunken`) + jedna spojitá karta
  z `Bg.Surface` (taby → hledání → strom) - v light je `Bg.Base` == `Bg.Surface`, bez
  zapuštění panel splyne s obsahem; akční toolbar zůstává **vedle karty** na chrome (ikonové
  taby a ikonové akce by splynuly v jednu řadu). `TreeTabControl` nese jen tab strip
  a content skrývá **prázdný `ContentTemplate`** (generátor kontejnerů nastavuje `Content`
  na datový record a lokální hodnota by přebila Setter ve stylu); stromy jsou sourozenci
  v kartě přepínané dle `SelectedTab.Key` → všech pět žije pořád → do `Tag` datových šablon
  (most pro `ContextMenu`) nejde `AncestorType=TabControl`, jde tam `VmProxy`.
- **Identita záložky = `Modules/TreeTab`** - jeden seznam `TreeTabs` drží pořadí, klíč
  (server + persistence), ikonu, `Expandable`/`ServerLoaded`; přidání záložky = změna
  seznamu (+ control v kartě). `Key` je persistovaný: **přejmenování = ztráta uložené volby**.
- **Toolbar nad stromy** (Rozbalit/Sbalit vše + oblíbené/výchozí okno): Rozbalit/Sbalit cílí
  na **aktivní** záložku (`ActiveExpandableTree`; Používané strom nemá → vyp), bulk změna
  v `SuppressExpansionSave()` + jeden `SaveExpansionAsync`. Scope je **refcountovaný** -
  cesty se vnořují a plochý bool by vnitřní blok odemkl dřív než vnější. `SelectedModule`
  plní code-behind jedním handlerem na kartě (`TreeView.SelectedItem` nejde bindovat,
  eventy bublají); VM ho drží **per záložku** a přestavba stromů ho zahazuje (nové instance).

Mini-apps (AvalonDock, WebView2 widgety, persistence layoutu, karusel): [dotnet/MiniApps/CLAUDE.md](dotnet/MiniApps/CLAUDE.md).

## Plocha se zástupci

Plocha je **nadmnožina oblíbených**: `ModuleShortcutsService` drží `DesktopShortcut` všech
`ShortcutKind`, ★ ve stromu umí jen `MenuModule`; identita = `Key` (`Kind|ElementId|Id`).
Položka, kterou aktuální strom nezná (licence, `hidden`), se **nezobrazí, ale uloží zpátky** -
jinak by ji první změna členství tiše smazala. Ostatní druhy (tiskové sestavy, pohledy DS)
zapisují agendy přímo do Caché (`ALVA.EDControls.DesktopShortcuts`, týž blob, který shell
čte) a spouští se 14. argem hosta; shell blob přenačte až po `LegacyModuleExited`. Dlaždice
ukazuje **dlouhý název** (`LongName`, legacy `nameEx`; fallback krátký) + cestu agendou
v ToolTipu - obojí odvozené ze stromu, nepersistuje se do `DesktopShortcutDto`; zmenšení
textu drží `Viewbox` (`DownOnly`), `MaxHeight` `TextBlocku` je jeho podlaha (z tokenu
`FontSize` se spočítat nedá). Pozice = **absolutní `Col`/`Row`** (`TileGridPanel`) - mřížka
se nepřeskládává, přesah řeší scroll; `GridColumns` jen přiděluje pozice novým položkám
a Caché `Add` musí při přepisu zachovat **obě** složky, jinak shodí uživatelovo rozložení.

## Výchozí okno (startup window)

Modul auto-otevřený po loginu (per-uživatel × IDFa, Caché feature #25).
`ExecutableModule.ElementId` = uložené `EDISFormEnum` id → picker/značení jede přes vlastní
strom, ne legacy AccessTree. Okno `Windows/UserProfileWindow` je EDInputForm
(`…EDInputForm.UserProfile`), ale **data drží klient, ne schema třída** -
`Main.LoadUser/SaveUser` jsou samy MLRest metody, volat je ze schematu by kolidovalo na `^||ML`.

## Novinky (changelog agend)

Auto-popup `Windows/ChangelogWindow` zrcadlí legacy infotexty: existence `<KLÍČ>_DIFF.INF`
vedle binárek = „jsou nové změny". Modální a **blokuje otvírání**: v `OpenModule` funnelu
před spuštěním agendy, shellový check před `OpenStartupWindowIfAny`; menu
`LaunchModeless/Dialog` záměrně ne. Max 1x za session per INF klíč (kryje i re-focus dedup
`ShowLegacyAsync`). Snooze (UserPrefs `ChangelogSnooze`) váže SHA-256 hash obsahu `_DIFF`,
ne verzi - Caché-only patch nechává stale `_DIFF` na disku a DEBUG verze je konstantní.
`InfKeyFor` zrcadlí legacy `EDConversion.Package.ToInformationFile` včetně výjimky NDMAIN.
Vynucená cesta `ShowAlwaysAsync` (Nápověda, kontextové menu stromu i dlaždice) obchází dedup
i snooze, bez `_DIFF` dá celou historii a bez INF vrací `false` - volající to **musí** říct
popupem, jinak klik nic neudělá. Snooze checkbox je proto dvoucestný a bez `_DIFF` zakázaný.
Okno se otvírá vždy uprostřed shellu: pozice se nepersistuje (`PersistedFormId => null`),
centruje ho `OwnerWindowCentering` (`CenterOwner` u maximalizovaného ownera míří na restore bounds).

## Legacy moduly - EDISONMAIN2API

Most do legacy světa: `EDISONMAIN2API.dll` (`c:\MLineRepos\EDISON\EDISONMAIN2API\`,
.NET Framework 4.8, deploy do `C:\Edison\`). Args: sentinel `"EDISONMAIN2"` + 13 polí
(server, port, ns, idfa, login, fullName, mlp, formName, shellGuid, inputParam, assemblyPath,
licensedElements, zástupce na ploše); poslední je Base64 XML zástupce - prázdné u běžného
launche, s `CallType=Procedura` host místo formy spustí `ProcedureIO` agendy. Dvě vstupní metody:

- `TryGetFormFromArgs` - typ hledá reflexí ve volajícím assembly; kontrakt pro retrofitnuté
  `.exe` (v `Sub Main` zavolat a předat do `Application.Run`; bez argů původní standalone
  chování). Reference: `MHD2PlanDopravy/Program.cs`, `USERS/Users-v4/Program.vb`.
- `TryRunFromArgs` - `Assembly.LoadFrom(assemblyPath)`; generický host `EDISONMAIN2API.exe`
  pro `.exe` bez retrofitu.

Pasti:

- Non-prázdný `shellGuid` → modul sdílí shellův GUID + `CacheLocks=1` (zánik shell zámku
  shodí i moduly). Prázdný (over-license retry před VerifyLic) → vlastní GUID, bez CacheLocks.
- `ConfigureForm` staví `MLProps.AccessTable` z `licensedElements` - bez nich moduly
  gate-ující obsah přes `RightGranted` (např. DSINPUT Plánování) nezobrazí data.

Shell strana: `WindowNavigator.ShowLegacy*` (viz
[EDControls/dotnet/EDViewModel/CLAUDE.md](../EDControls/dotnet/EDViewModel/CLAUDE.md)); admin
override při out-of-license řeší `LicenceVerifier` (`ShowLegacyAndWait(..., shareShellSession: false)`).

Test funkcí → „Spustit starý shell": `EDISONMAIN.exe` s `reason=2` (`RunEdison`), bez něj se
`frmNoMDI` odmítne spustit. Plain `Process.Start`, ne `ShowLegacy*` - starý shell nemá umřít
s tímhle (KillOnExit) ani být ve „Spuštěné moduly"; přes EDLOADER se k němu už nedostaneš.
