# EDISONMAIN2 - poznámky pro vývojáře

Hlavní WPF shell - login, výběr firmy, strom modulů, spouštění legacy `.exe` agend.
Nahrazuje VB.NET `EDISONMAIN`.

Bootstrap orchestruje `Startup/AppBootstrapper` (template method: setup wizard → prefs →
login → MLRest → firma → VerifyLic → module tree → update check → font → shell okno).
**Po návratu z `Run` bootstrapper dožil** - co běží dál, má vlastní session-scoped objekt
(`SessionSwitcher` = přihlášení jiného uživatele, `ShellShutdown` = vynucené ukončení);
skládání session sdílí obě cesty přes `SessionFactory.Establish`.
Login okno (jen Release dialog path) zůstává po OK otevřené jako progress podklad přes
post-login kroky a zavírá se až na shell `ContentRendered`; `HandOffLoginWindow` musí
přiřadit `Application.MainWindow` na shell **před** restore `OnMainWindowClose` - implicitní
MainWindow je login okno a špatné pořadí buď zabije app, nebo nechá proces bez konce.
`Services/` složka neexistuje záměrně - single-consumer infrastruktura je ko-lokovaná
se svým consumerem, cross-cutting věci jdou do `EDControls`.

## Session / ACTIVITY zámek

- Zámek vytváří `VerifyLic` při loginu; je v **temp globálu** - nezaniká se spojením,
  hard-kill ho neuvolní (uklidí až server idle timeout). V dev to tlumí deterministický
  session GUID (`#if DEBUG` v `EstablishSession`) - re-run přepíše týž klíč.
- `SelectedCompanyIndex` musí odpovídat `MLRest.Idfa` - tam má bootstrapper zámek;
  jiná volba rozbije koexistenci shell ↔ moduly. Startupní výběr:
  `CompanySwitcher.PickInitial` (priorita `LastIdfa` → `IsDefault` → první).
- `SessionLogout.Logout` se volá na **klonu** `IMLRest.With(...)` - lze odhlásit
  starou firmu / session bez mutace sdílené instance.
- `LogoutWatcher` pollí `maSeUkoncit` s `CacheLocks=2`, aby poll neobnovoval
  server-side idle timer.
- Session-scoped stav (access, tři stromy, preference, výchozí okno) načítá
  `Modules/SessionDataLoader` **na jeden zátah přes klony** (`RestCallRunner`, viz root
  CLAUDE.md) a `EdisonMainViewModel.ApplySessionData` ho pak promítne **synchronně** - guardy
  `_suppressXxx` neplatí přes `await` a selhání loadu tak nechá předchozí stromy naživu.
  Ctor VM data nenačítá: shell se ukáže hned a `InitializeAsync()` běží z `ContentRendered`
  **před** `OpenStartupWindowIfAny` (výchozí okno se hledá ve stromu). Do prvního initu je
  `_modulePrefs` null, takže save cesty musí zůstat null-safe.
- Přepnutí firmy dělá `SwitchCompanyCommand` (`SelectedCompanyId` setter ho jen spustí);
  index se nastaví teprve po `Accepted`, takže odmítnutí nepotřebuje rollback. Sám
  `CompanySwitcher.Switch` zůstává synchronní - potřebuje UI thread na dialogy licenčního
  retry; VM ho obaluje `ActionInProgress` + yield, aby se overlay stihl vykreslit.
  Zavírání dvoufázově: WPF okna shellu **před** swapem Idfa (volají server přes sdílený
  MLRest), legacy moduly až po `Accepted` těsně před logoutem staré firmy - neúspěšné
  přepnutí je tak nezabije. Confirm se ukazuje jen s otevřenými moduly.
- DEBUG build má verzi `999999.9` - netriggeruje update check.
- Zavření shellu (= ukončení aplikace i všech agend) se potvrzuje v `Closing` přes
  `EdisonMainViewModel.ConfirmShellClose`, takže **každé programové ukončení po vzniku shellu
  musí jít přes `ShellShutdown.Force` / `SuppressCloseConfirm`** - jinak force logout
  nebo version gate uvízne na dialogu, který smí uživatel odmítnout.

## UI pasti

- `EDFilterComboBox` (výběr firmy) **musí být mimo `<Menu>`** - WPF Menu krade fokus
  editovatelnému poli uvnitř; proto je v `DockPanel` vedle Menu. Horní lišta je proto
  rozdělená na **dva `Menu` controly** (`EdisonMenu` = jen položka EDISON, pak combo, pak
  `TopMenu` se zbytkem), aby hlavní nabídka stála první; cena je, že šipkami se mezi oběma
  menu nepřejde. `x:Name="TopMenu"` drží bindingy submenu (`ElementName`).
- `Windows/EdisonMainWindow/Modules/ModuleLaunchabilityCheck` - barva + suffix názvu
  modulu dle spustitelnosti (stub / chybí exe / neretrofitnuto).
- **Spuštěné moduly** se značí dlouhým názvem modulu, jinak cestou agendou
  (`BuildModuleLabels`, klíč `exe|forma|param`) - krátký název mají dvě agendy běžně shodný.
  Fallback (modul mimo strom, WPF okno) = titulek bez koncového `[IDFa]`; **titulky oken si
  `[IDFa]` naopak drží** (přidává ho `WindowNavigator`).
- Hledání ve stromu: shoda na názvu **skupiny** zpřístupní celý její podstrom, jinak by byl
  nález slepá ulička (skupinu spustit nejde a potomci hledané slovo v názvu nemají).
- **Status bar** = segmenty ikona + hodnota, technika (namespace, port, login, Mlp) jen
  v ToolTipu. Stojí na `LastChildFill="False"` z EDControlStyles; `DockPanel.Dock="Right"`
  funguje **jen na explicitním** `<StatusBarItem>` (na surovém `TextBlock` se nepropíše na
  generovaný kontejner), stejně tak `Visibility="Collapsed"` bez zbytkové mezery.
  `<Separator/>` patří jen mezi vždy viditelné segmenty - u sbalitelného by zůstal viset.
- **Launcher panel** je zapuštěná plocha (`EDTheme.Bg.Sunken`), na které leží **jedna spojitá
  karta** z `Bg.Surface`: pruh záložek → hledání → strom (navazující `BorderThickness`,
  zakulacení jen na krajích). V light je totiž `Bg.Base` == `Bg.Surface` a bez toho panel
  splyne s obsahem. Akční toolbar zůstává **vedle karty** na zapuštěném chrome - ikonové taby
  a ikonové akce by jinak splynuly v jednu řadu, navíc obě skupiny obsahují ★.
  `TreeTabControl` nese **jen tab strip**: `TabItem`y jsou ikonové (text v ToolTipu, ikona
  v `Header`) a content skrývá **prázdný `ContentTemplate`** - generátor kontejnerů nastavuje
  `Content` na datovou položku (record ToString) a lokální hodnota by přebila Setter ve stylu;
  stromy jsou sourozenci v kartě a přepínají se podle `SelectedTab.Key`. Důsledek: všech pět stromů žije pořád → do `Tag`
  datových šablon (most pro `ContextMenu`) nejde brát `AncestorType=TabControl`, jde tam `VmProxy`.
- **Identita záložky je `Modules/TreeTab`** - jeden seznam `TreeTabs` ve VM drží pořadí, klíč
  (server + persistence), ikonu a příznaky `Expandable`/`ServerLoaded`. Přidání záložky je
  změna toho seznamu (+ control v kartě), ne koordinovaná editace indexů. `Key` je
  persistovaný, takže **přejmenování = ztráta uložené volby** uživatelů.
- **Toolbar nad stromy** (Rozbalit/Sbalit vše + oblíbené/výchozí okno nad vybraným uzlem):
  jedna instance vpravo v řádku se záložkami. Rozbalit/Sbalit vše cílí na **aktivní** záložku
  (`ActiveExpandableTree`; Používané nemá strom → vyp), bulk změna obalena
  `SuppressExpansionSave()` + jeden `SaveExpansionAsync`. Ten scope je **refcountovaný** -
  cesty se vnořují (přestavba Oblíbených uvnitř loadu, re-filtrace při hledání) a plochý bool
  by vnitřní blok odemkl dřív než vnější. `SelectedModule` (řídí toolbar akce)
  plní code-behind jedním handlerem na kartě - `TreeView.SelectedItem` nelze bindovat a
  `SelectedItemChanged` i `KeyDown` bublají. VM si výběr pamatuje **per záložku**, aby akce
  cílily na to, co je vidět; přestavba stromů ho zahazuje (uzly jsou nové instance).

Mini-apps (AvalonDock, WebView2 widgety, persistence layoutu, karusel) mají vlastní
[dotnet/MiniApps/CLAUDE.md](dotnet/MiniApps/CLAUDE.md).

## Plocha se zástupci

Plocha je **nadmnožina oblíbených**: `ModuleShortcutsService` drží `DesktopShortcut` všech
`ShortcutKind`, ★ ve stromu umí jen `MenuModule` (= záložka Oblíbené). Identita je `Key`
(`Kind|ElementId|Id`). Položka, kterou aktuální strom nezná (licence, `hidden`), se
**nezobrazí, ale uloží zpátky** - jinak by ji první změna členství tiše smazala. Ostatní druhy
už jde **spustit** (14. arg hosta, `ShortcutLaunchPayloadCodec`), ale ne vytvořit z agendy -
kanál modul→shell chybí, viz [docs/DESKTOP_SHORTCUTS.md](docs/DESKTOP_SHORTCUTS.md).
Dlaždice ukazuje **dlouhý název** (`ExecutableModule.LongName`, legacy `nameEx`; fallback
krátký) a cestu agendou v ToolTipu (`Seed` ji dostává delegátem) - mimo strom chybí kontext,
který krátký název zjednoznačňuje. Obojí odvozené ze stromu, nepersistuje se
do `DesktopShortcutDto`.

## Výchozí okno (startup window)

Modul auto-otevřený po loginu (per-uživatel × IDFa, Caché feature #25).
`ExecutableModule.ElementId` = uložené `EDISFormEnum` id → picker/značení jede přes vlastní
strom, ne legacy AccessTree.

Okno `Windows/UserProfileWindow` je EDInputForm (`…EDInputForm.UserProfile`), ale **data
drží klient, ne schema třída** - `Main.LoadUser/SaveUser` jsou samy MLRest metody, volat je
ze schematu by kolidovalo na `^||ML`.

## Novinky (changelog agend)

Auto-popup `Windows/ChangelogWindow` zrcadlí legacy infotexty: existence `<KLÍČ>_DIFF.INF`
vedle binárek = „jsou nové změny". Modální a **blokuje otvírání**: v `OpenModule` funnelu
před spuštěním agendy, shellový check před `OpenStartupWindowIfAny`; menu
`LaunchModeless/Dialog` záměrně ne. Max 1x za session per INF klíč (kryje i re-focus dedup
`ShowLegacyAsync`). Snooze (UserPrefs `ChangelogSnooze`) váže SHA-256 hash obsahu `_DIFF`,
ne verzi - Caché-only patch nechává stale `_DIFF` na disku a DEBUG verze je konstantní.
`InfKeyFor` zrcadlí legacy `EDConversion.Package.ToInformationFile` včetně výjimky NDMAIN
(vlastní soubor). Okno se vždy otvírá uprostřed shellu: pozice se nepersistuje
(`PersistedFormId => null`) a centruje ho `OwnerWindowCentering` - samotné `CenterOwner`
u maximalizovaného shellu centruje podle restore bounds.

## Legacy moduly - EDISONMAIN2API

Most do legacy světa: `EDISONMAIN2API.dll` (`c:\MLineRepos\EDISON\EDISONMAIN2API\`,
.NET Framework 4.8, deploy do `C:\Edison\`). Args: sentinel `"EDISONMAIN2"` + 13 polí
(server, port, ns, idfa, login, fullName, mlp, formName, shellGuid, inputParam,
assemblyPath, licensedElements, zástupce na ploše). Poslední pole je Base64 XML zástupce -
prázdné u běžného launche; s `CallType=Procedura` host místo formy spustí `ProcedureIO`
agendy (viz [docs/DESKTOP_SHORTCUTS.md](docs/DESKTOP_SHORTCUTS.md)). Dvě vstupní metody:

- `TryGetFormFromArgs` - typ hledá reflexí ve volajícím assembly; kontrakt pro
  retrofitnuté `.exe` (v `Sub Main` zavolat a předat do `Application.Run`; bez argů
  původní standalone chování). Reference: `MHD2PlanDopravy/Program.cs`,
  `USERS/Users-v4/Program.vb`.
- `TryRunFromArgs` - `Assembly.LoadFrom(assemblyPath)`; používá generický host
  `EDISONMAIN2API.exe` pro `.exe` bez retrofitu.

Pasti:

- Non-prázdný `shellGuid` → modul sdílí shellův GUID + `CacheLocks=1` (zánik shell
  zámku shodí i moduly). Prázdný (over-license retry před VerifyLic) → vlastní GUID,
  bez CacheLocks.
- `ConfigureForm` staví `MLProps.AccessTable` z `licensedElements` - bez nich moduly
  gate-ující obsah přes `RightGranted` (např. DSINPUT Plánování) nezobrazí data.

Shell strana: `WindowNavigator.ShowLegacy*` (viz
[EDControls/dotnet/EDViewModel/CLAUDE.md](../EDControls/dotnet/EDViewModel/CLAUDE.md));
admin override při out-of-license řeší `LicenceVerifier` přes `ShowLegacyAndWait(...,
shareShellSession: false)`.
