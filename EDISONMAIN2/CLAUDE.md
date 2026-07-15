# EDISONMAIN2 — poznámky pro vývojáře

Hlavní WPF shell — login, výběr firmy, strom modulů, spouštění legacy `.exe` agend.
Nahrazuje VB.NET `EDISONMAIN`. Stav a roadmap: [PLAN.md](PLAN.md); doménový kontext:
[RESPONSIBILITIES.md](RESPONSIBILITIES.md); update flow: [docs/UPDATE_FLOW.md](docs/UPDATE_FLOW.md).

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
- **Mini-apps** (`dotnet/MiniApps/`): framework je v `EDControls/MiniApps`, shell je
  hostuje jako AvalonDock `LayoutDocument` v centrálním pane (`AvalonDockHost`).
  Core widgety (Novinky/Wiki/Školení) = WebView2 na `podpora.m-line.cz` — mirror
  legacy CefSharp plochy z `frmMain.vb`; Školení dostává IdFa + login v URL.
  Layout se persistuje per IDFa v `LocalPrefs.MiniAppLayouts`; chybí-li uložený →
  `OpenColumns` otevře výchozí 3 sloupce (Novinky/Wiki/Školení), prázdný dock ukáže
  placeholder s tlačítkem na výchozí sadu (`HasOpenMiniApps` / `InstancesChanged`).

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
