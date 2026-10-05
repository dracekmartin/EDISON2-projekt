# EDISONMAIN2 - poznámky pro vývojáře

Hlavní WPF shell - login, výběr firmy, strom modulů, spouštění legacy `.exe` agend. Nahrazuje
VB.NET `EDISONMAIN`. Bootstrap, login, připojení a session s ACTIVITY zámkem:
[dotnet/Startup/CLAUDE.md](dotnet/Startup/CLAUDE.md).

`Services/` složka neexistuje záměrně - single-consumer infrastruktura je ko-lokovaná se svým
consumerem, cross-cutting věci jdou do `EDControls`.

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

## Session na straně VM

- Session-scoped stav (`Modules/SessionDataLoader`) promítá `EdisonMainViewModel.ApplySessionData`
  **synchronně** - guardy `_suppressXxx` neplatí přes `await` a selhání loadu nechá předchozí
  stromy naživu. `InitializeAsync()` běží z `ContentRendered` **před** `OpenStartupWindowIfAny`
  (výchozí okno se hledá ve stromu); před prvním initem je `_modulePrefs` null → save cesty null-safe.
- **Podpora (`LoginName == UserID`, `_mline`) jede bez `VerifyLic` a zámku s `CacheLocks=0`** (parita
  legacy `EDUsers.vb:287`: licenci nezabírá, server nekontroluje LockExist ani verzi); agendy pak
  shell GUID nedostanou (`LegacyModuleHost.ShellGuidFor`), jinak by pod ním padaly na LockExist.
- `SelectedCompanyIndex` musí odpovídat `MLRest.Idfa` - tam má bootstrapper zámek; jiná volba
  rozbije koexistenci shell ↔ moduly. Při přepnutí firmy se index nastaví až po `Accepted`
  (setter `SelectedCompanyId` jen spustí `SwitchCompanyCommand`).
- **Přepínač nabízí jen povolené firmy** (`CompanySwitcher.LoadAllowed`, legacy `checkAccess=True`):
  server filtruje jen se známým MLP, předloginový seznam je proto úplný a slouží jen k Idfa loginu.
  Seznam se načítá znovu po přepnutí uživatele i firmy (funkční omezení DB je per IDFa).
- Potvrzení zavření shellu (`ConfirmShellClose`) obchází jen `ShellShutdown.Force` /
  `SuppressCloseConfirm` - programové ukončení musí jít přes ně (proč: Startup/CLAUDE.md).

## UI pasti

- `EDFilterComboBox` (výběr firmy) **musí být mimo `<Menu>`** - WPF Menu krade fokus editovatelnému
  poli; horní lišta jsou proto dva `Menu` controly + combo v `DockPanel` mezi nimi (cena: šipkami
  se mezi menu nepřejde). `x:Name="TopMenu"` drží bindingy submenu (`ElementName`).
- **Spuštěné moduly** i seznam **Používané** se značí dlouhým názvem modulu, jinak cestou agendou
  (krátký název mají agendy běžně shodný, „Doklady" jich má pět). Používané přepisují styl názvu
  sdílené šablony stromu (`ModuleNameText`, proto `DynamicResource`). Fallback Spuštěných =
  titulek bez koncového `[IDFa]` (suffix si přidávají legacy okna sama, `clsForm.ShowIDFA`).
  **Firma v titulku je jen u shellu**; titulek jde do jména tiketu v hlášení chyb (firma zvlášť
  polem `firma`).
- Hledání ve stromu: shoda na názvu **skupiny** zpřístupní celý její podstrom, jinak by byl
  nález slepá ulička (skupinu spustit nejde a potomci hledané slovo v názvu nemají). Rozbalení
  vynucené hledáním je **přechodné**: snímek před hledáním se po vymazání textu obnoví a změny
  rozbalení během hledání se nepersistují.
- **Status bar** = segmenty ikona + hodnota, technika jen v ToolTipu. `DockPanel.Dock="Right"`
  i `Visibility="Collapsed"` fungují jen na **explicitním `<StatusBarItem>`** (na surový
  `TextBlock` se nepropíší); `<Separator/>` jen mezi vždy viditelné (u sbalitelného by visel).
- **Launcher panel**: zapuštěná plocha (`EDTheme.Bg.Sunken`) + jedna spojitá karta z `Bg.Surface` -
  v light je `Bg.Base` == `Bg.Surface`, bez zapuštění panel splyne s obsahem. `TreeTabControl`
  nese jen tab strip a content skrývá **prázdný `ContentTemplate`** (generátor kontejnerů
  nastavuje `Content` na datový record, lokální hodnota by přebila Setter ve stylu); stromy jsou
  sourozenci přepínaní dle `SelectedTab.Key` → všech pět žije pořád → do `Tag` datových šablon
  (most pro `ContextMenu`) nejde `AncestorType=TabControl`, jde tam `VmProxy`.
- **Identita záložky = `Modules/TreeTab`** (seznam `TreeTabs`); `Key` je klíč serveru
  i persistence: **přejmenování = ztráta uložené volby**.
- **Toolbar nad stromy**: bulk změna rozbalení v `ModuleTreeState.SuppressExpansionSave()` + jeden
  `SaveExpansionAsync` - scope je **refcountovaný** (cesty se vnořují, plochý bool by vnitřní blok
  odemkl dřív). `SelectedModule` plní code-behind (`TreeView.SelectedItem` nejde bindovat).

Mini-apps (AvalonDock, pruh podpory, persistence layoutu): [dotnet/MiniApps/CLAUDE.md](dotnet/MiniApps/CLAUDE.md).

## Plocha se zástupci

Model, oba kanály k legacy modulům a jejich pasti: [docs/zastupci-na-plose.md](docs/zastupci-na-plose.md).
Plocha je **nadmnožina oblíbených** (★ ve stromu umí jen `MenuModule`). Položka, kterou aktuální
strom nezná (licence, `hidden`), se **nezobrazí, ale uloží zpátky** - jinak by ji první změna
členství tiše smazala. Název + cesta dlaždice se odvozují ze stromu, do `DesktopShortcutDto` se
nepersistují; text zmenšuje `Viewbox` (`DownOnly`) a `MaxHeight` `TextBlocku` je jeho podlaha
(z tokenu `FontSize` se spočítat nedá). Pozice = absolutní `Column`/`Row` na `TileGridPanel`
(v DTO `Col`/`Row`) - mřížka se nepřeskládává, přesah řeší scroll; `ModuleShortcutsService.GridColumns`
jen přiděluje pozice novým a Caché `Add` musí při přepisu zachovat **obě** složky, jinak shodí
uživatelovo rozložení.

## Výchozí okno (startup window)

Modul auto-otevřený po loginu (per-uživatel × IDFa, Caché feature #25). `ExecutableModule.ElementId`
= uložené `EDISFormEnum` id → picker/značení jede přes vlastní strom, ne legacy AccessTree.
Čte a zapisuje se jen přes `ApiEdison.Get/SetUserDefaultForm`; řádek `form` z `Main.LoadUser` je pro legacy shell.
`Windows/UserProfileWindow` je EDInputForm (`…EDInputForm.UserProfile`), ale **data drží klient,
ne schema třída** - `Main.LoadUser/SaveUser` jsou samy MLRest metody, volat je ze schematu by
kolidovalo na `^||ML`.

## Novinky (changelog agend)

Auto-popup `Windows/ChangelogWindow` zrcadlí legacy infotexty: existence `<KLÍČ>_DIFF.INF` vedle
binárek = „jsou nové změny". Modální a **blokuje otvírání**: v `OpenModule` funnelu před spuštěním
agendy, shellový check před `OpenStartupWindowIfAny`; menu `LaunchModelessAsync`/`LaunchDialogAsync`
záměrně ne. Max 1x za session per INF klíč (kryje i re-focus dedup `ShowLegacyAsync`). Snooze
(UserPrefs `ChangelogSnooze`) váže SHA-256 hash obsahu `_DIFF`, ne verzi - Caché-only patch nechává
stale `_DIFF` na disku a DEBUG verze je konstantní. `InfKeyFor` zrcadlí legacy
`EDConversion.Package.ToInformationFile` včetně výjimky NDMAIN. Vynucená cesta `ShowAlwaysAsync`
(Nápověda, kontextová menu) obchází dedup i snooze, bez `_DIFF` dá celou historii a bez INF vrací
`false` - volající to **musí** říct popupem, jinak klik nic neudělá.

## Legacy moduly - EDISONMAIN2API

Most do legacy světa: `EDISONMAIN2API.exe` (`c:\MLineRepos\EDISON\EDISONMAIN2API\`, .NET
Framework 4.8, deploy do `C:\Edison\`). Args: značka `"EDISONMAIN2"` + 20 polí (pořadí
`LegacyModuleHost.BuildLauncherArguments`), host je mapuje do `MLProps`: [13] zástupce z plochy
(viz `docs/zastupci-na-plose.md`), [14] admin → `UserIsAdmin` (klíč pro `CAccess(useAdmin)`
bypass práv), [15] → `UserMayUpdate` (gate serverového update v legacy Nastavení) - host bez pole
defaultuje na **false**; [16]/[17] osobní adresář a e-mail z loginu → `UserDirectory`/`UserEmail`
(bez nich agendy exportují do kořene disku); [18]-[20] verze, stav a konec platnosti licence shellu. Host nastavuje i `EDFormID` formy - agendy přes něj
čtou `AccessTable[EDFormID]`, bez něj jsou volby gate-ované právem zašedlé. Vstupy:
`TryGetFormFromArgs` (typ reflexí ve volajícím assembly - jediný retrofit
`EDISONMAIN/ApplicationEvents.vb`) a `TryRunFromArgs` (`Assembly.LoadFrom`, generický host).

Pasti:

- Chyby hostované formy: `HostedErrorReporter` (host i `ApplicationEvents.vb`) → legacy
  `MLError.ShowError` (telemetrie až na tlačítko, source `EDISON`); výjimku z `Form.Load` na x64
  spolkne sám Windows (kernel callback), nevidí ji ani try/catch kolem `ShowDialog`. Tvrdý pád
  hlásí shell z exit kódu (`LegacyModuleHost.Launch` → `ReportLegacyExit`); kódy hosta 2 =
  launch fail, 3 = fatal po dialogu. Ověření bez pádu: Debug host + `EDISONMAIN2API_CRASHTEST=ui|bg`.
- Non-prázdný `shellGuid` → modul sdílí shellův GUID + `CacheLocks=1` (zánik shell zámku shodí
  i moduly). Prázdný (over-license retry před VerifyLic) → vlastní GUID, bez CacheLocks.
  TemplateDesigner totéž bez hosta (`LaunchesDirectly`, viz `TemplateDesigner/CLAUDE.md`); legacy
  launchery TD (ISYJR2, MHD2Linky, TISKJR stub) guid propagují jen při `CacheLocks==1`.
- `ConfigureForm` skládá licenci z [12] a [18]-[20], jen je-li licence shellu v pořádku (E01) a ještě
  platná; jinak (i za starším shellem) volá `GetActiveLic` (~1 s na DEV) včetně hlášky. Z ní `LicenseTable`,
  `ReadOnlyMode` a `AccessTable`; bez `Version` editor práv v USERS obrátí filtr licencovaných práv a smaže je.
- Host roztáčí formu přes **`ShowDialog`, ne `Application.Run`** (parita se starým `EDPROCESS`):
  legacy formy se zavírají samy - `DialogResult`, nebo `Close()` před otevřením sesterské formy
  přes `ShowEdisonForm` (ISYJR → ISYJR2) - a v `Application.Run` loopu tím umře proces.

Shell strana: `WindowNavigator.ShowLegacy*` ([EDViewModel/CLAUDE.md](../EDControls/dotnet/EDViewModel/CLAUDE.md));
admin override při out-of-license dělá `LicenceVerifier`.

Nástroje → „Spustit starý shell": `EDISONMAIN.exe` s `reason=2` (`RunEdison`), bez něj se
`frmNoMDI` odmítne spustit. Plain `Process.Start`, ne `ShowLegacy*` - starý shell nemá umřít
s tímhle (KillOnExit) ani být ve „Spuštěné moduly"; přes EDLOADER se k němu už nedostaneš.
