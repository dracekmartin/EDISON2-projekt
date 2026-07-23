# EDISONMAIN2 — poznámky pro vývojáře

Hlavní WPF shell — login, výběr firmy, strom modulů, spouštění legacy `.exe` agend.
Nahrazuje VB.NET `EDISONMAIN`. Doménový kontext: [RESPONSIBILITIES.md](RESPONSIBILITIES.md);
update flow: [docs/UPDATE_FLOW.md](docs/UPDATE_FLOW.md). `PLAN.md` je stale — nereferencovat.

Bootstrap orchestruje `Startup/AppBootstrapper` (template method: setup wizard → prefs →
login → MLRest → firma → VerifyLic → module tree → update check → font → shell okno).
`Services/` složka neexistuje záměrně — single-consumer infrastruktura je ko-lokovaná
se svým consumerem, cross-cutting věci jdou do `EDControls`.

## Session / ACTIVITY zámek

- Zámek vytváří `VerifyLic` při loginu; je v **temp globálu** — nezaniká se spojením,
  hard-kill ho neuvolní (uklidí až server idle timeout). V dev to tlumí deterministický
  session GUID (`#if DEBUG` v `EstablishSession`) — re-run přepíše týž klíč.
- `SelectedCompanyIndex` musí odpovídat `MLRest.Idfa` — tam má bootstrapper zámek;
  jiná volba rozbije koexistenci shell ↔ moduly. Startupní výběr:
  `CompanySwitcher.PickInitial` (priorita `LastIdfa` → `IsDefault` → první).
- `SessionLogout.Logout` se volá na **klonu** `IMLRest.With(...)` — lze odhlásit
  starou firmu / session bez mutace sdílené instance.
- `LogoutWatcher` pollí `maSeUkoncit` s `CacheLocks=2`, aby poll neobnovoval
  server-side idle timer.
- DEBUG build má verzi `999999.9` — netriggeruje update check.

## UI pasti

- `EDFilterComboBox` (výběr firmy) **musí být mimo `<Menu>`** — WPF Menu krade fokus
  editovatelnému poli uvnitř; proto je v `DockPanel` vedle Menu.
- `Windows/EdisonMainWindow/Modules/ModuleLaunchabilityCheck` — barva + suffix názvu
  modulu dle spustitelnosti (stub / chybí exe / neretrofitnuto).
- **Toolbar nad stromy** (Rozbalit/Sbalit vše + oblíbené/výchozí okno nad vybraným uzlem):
  Rozbalit/Sbalit vše cílí na **aktivní** záložku (`ActiveExpandableTree` dle
  `SelectedTreeTabIndex`; Používané = ListBox → vyp), bulk změna obalena
  `_suppressExpansionSave` + jeden `SaveExpansionAsync`. `SelectedModule` (řídí toolbar
  akce) plní code-behind — `TreeView.SelectedItem` nelze bindovat, jede přes
  `SelectedItemChanged` + `TabControl.SelectionChanged`. Ikonové tlačítko = sdílený styl
  `EDTheme.ToolbarButton` z EDControls.
- **Mini-apps** (`dotnet/MiniApps/`): framework je v `EDControls/MiniApps`, shell je
  hostuje jako AvalonDock `LayoutDocument` v centrálním pane (`AvalonDockHost`).
  `MiniAppDescriptor.Pinned` = uživatel ji nezavře ani neodtrhne (programové `Close()`
  funguje dál — session switch OK), single-instance a **solo pane**: `AvalonDockHost`
  po `Layout.Updated` odsune cizí taby jinam a razítkuje pane `DockMinWidth/Height`
  (hook nutno obnovovat přes `LayoutChanged` — Deserialize mění celý LayoutRoot).
  „Zavřít miniaplikace" jde přes `CloseUnpinned`; `EnsurePinnedOpen` ji dootevře
  po restore / defaultu / resetu.
  Core widgety (Novinky/Wiki/Školení) = WebView2 na `podpora.m-line.cz` — mirror
  legacy CefSharp plochy z `frmMain.vb`; Školení dostává IdFa + login v URL.
  Hostují se přes `FontZoomWebView : WebView2` (HWND, ne composition — ta crashuje na dragu tabu
  do plovoucího okna AvalonDocku a tahá 23 MB WinRT projekci, viz TFM v csproj). Airspace: WPF
  veil dimu widgety nepokryje — záměrně zůstávají čitelné; kartu „Akce probíhá" nad nimi kreslí
  owned okno (viz `EDControls/dotnet/EDActionInProgress/CLAUDE.md`).
  User velikost fontu respektují přes `FontZoomWebView` (ZoomFactor =
  `FontService.BodySize` / 14 — základní text widgetů je 14 CSS px);
  rodina fontu se do HTML nepropaguje. Dark mode widgety samy neumí (natvrdo
  `bg-white`) — `FontZoomWebView` v dark injektuje inverzní CSS filtr
  (`ThemeService.Applied`) + tmavé `DefaultBackgroundColor` proti bílému záblesku.
  Chrome docku (taby, bordery, pozadí dokumentů) EDTheme tokeny nečte — dark řeší
  swap `DockManager.Theme` na VS2013 Light/Dark v code-behind okna.
  Layout se persistuje per IDFa v `LocalPrefs.MiniAppLayouts` (spolu se šířkou stromu
  v `LocalPrefs.TreeWidths`, stejný lifecycle přes `AttachTreeLayout`); chybí-li uložený →
  výchozí layout = pinned karusel `Core.Podpora` (1–3 sloupce dle šířky; při plném
  zobrazení cyklení stojí, jinak 60 s lišta; obsah lazy, drží se živý) + Oblíbené dole;
  prázdný dock ukáže
  placeholder s tlačítkem na výchozí sadu (`HasOpenMiniApps` / `InstancesChanged`).
  Menu „Miniaplikace" je seskupené podle providerů (`IMiniAppProvider.GroupTitle`,
  pořadí skupin = pořadí registrace v `AppBootstrapper`); `DevSampleMiniApps`
  se registruje jen v DEBUG buildu. Statické akce dole (Zavřít/Resetovat) jsou
  v `CompositeCollection` — bindingy tam jdou jen přes `BindingProxy` (`VmProxy`):
  `x:Reference` na TopMenu při BAML loadu cyklí, `ElementName`/DataContext jsou
  mimo namescope/strom; lokální `Style` na nich blokuje `ItemContainerStyle` skupin.

## Výchozí okno (startup window)

Modul auto-otevřený po loginu (per-uživatel × IDFa, Caché feature #25).
`ExecutableModule.ElementId` = uložené `EDISFormEnum` id → picker/značení jede přes vlastní
strom, ne legacy AccessTree.

Okno `Windows/UserProfileWindow` je EDInputForm (`…EDInputForm.UserProfile`), ale **data
drží klient, ne schema třída** — `Main.LoadUser/SaveUser` jsou samy MLRest metody, volat je
ze schematu by kolidovalo na `^||ML`.

## Legacy moduly — EDISONMAIN2API

Most do legacy světa: `EDISONMAIN2API.dll` (`c:\MLineRepos\EDISON\EDISONMAIN2API\`,
.NET Framework 4.8, deploy do `C:\Edison\`). Args: sentinel `"EDISONMAIN2"` + 12 polí
(server, port, ns, idfa, login, fullName, mlp, formName, shellGuid, inputParam,
assemblyPath, licensedElements). Dvě vstupní metody:

- `TryGetFormFromArgs` — typ hledá reflexí ve volajícím assembly; kontrakt pro
  retrofitnuté `.exe` (v `Sub Main` zavolat a předat do `Application.Run`; bez argů
  původní standalone chování). Reference: `MHD2PlanDopravy/Program.cs`,
  `USERS/Users-v4/Program.vb`.
- `TryRunFromArgs` — `Assembly.LoadFrom(assemblyPath)`; používá generický host
  `EDISONMAIN2API.exe` pro `.exe` bez retrofitu.

Pasti:

- Non-prázdný `shellGuid` → modul sdílí shellův GUID + `CacheLocks=1` (zánik shell
  zámku shodí i moduly). Prázdný (over-license retry před VerifyLic) → vlastní GUID,
  bez CacheLocks.
- `ConfigureForm` staví `MLProps.AccessTable` z `licensedElements` — bez nich moduly
  gate-ující obsah přes `RightGranted` (např. DSINPUT Plánování) nezobrazí data.

Shell strana: `WindowNavigator.ShowLegacy*` (viz
[EDControls/dotnet/EDViewModel/CLAUDE.md](../EDControls/dotnet/EDViewModel/CLAUDE.md));
admin override při out-of-license řeší `LicenceVerifier` přes `ShowLegacyAndWait(...,
shareShellSession: false)`.
