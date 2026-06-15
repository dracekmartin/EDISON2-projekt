# EDISONMAIN2 — poznámky pro vývojáře

Hlavní WPF shell — login, výběr firmy, strom modulů, spouštění legacy `.exe` agend.
Nahradí staré VB.NET WinForms `EDISONMAIN`. Aktuální stav + roadmap v
[PLAN.md](PLAN.md); doménový kontext (legacy responsibilities + idea pool) v
[RESPONSIBILITIES.md](RESPONSIBILITIES.md).

## Co je hotové

- **Login** — `LoginWindow` + `UserAuthenticator` volá `ALVA.SYS.Login.Login`. Prázdné heslo
  povolené (LDAP-AUTO / SSO) — o módu (EDISON/LDAP/LDAP-AUTO, feature #23) rozhoduje server.
  Windows SSO auto-login: `AppBootstrapper.TryAutoLogin` (gated `ILocalPrefs.TryWindowsAutoLogin`,
  default off) zkusí `Environment.UserName` + prázdné heslo před dialogem; fallback na dialog.
- **Výběr firmy** — combobox v toolbaru s textovým názvem firmy + IDFa v `[]`,
  abecedně řazeno (server vrací řazené přes `SortedDictionary`).
  `EdisonMainViewModel.SelectedCompanyIndex` setter deleguje na `CompanySwitcher.Switch`
  (sdílená logika startup + runtime — výměna `MLRest.Idfa`/`DataNamespace`, re-volání
  VerifyLic přes `LicenceVerifier`, rollback při rejection, persist `LastIdfa` po Accepted).
  `CompanySwitcher.PickInitial` řeší startupní výběr (priorita: `LocalPrefs.LastIdfa` →
  `IsDefault` → `companies[0]`).
- **Licence** — `LicenceService` (wrapper kolem legacy `EDLicence.dll`); filtrace
  stromu modulů přes `EdisonMainViewModel.TryFilter` + `ExecutableModule.IsLicensed/ElementId`.
- **`LicenceVerifier`** — sdílený retry loop pro `VerifyLic`. Když je při
  out-of-license uživatel admin, nabídne spawnout legacy `USERS.exe formUSERSActivity`
  (přes EDISONMAIN2API), retry po zavření. Non-admin / Cancel → caller revertuje
  (AppBootstrapper vrátí null = no window shown → App.xaml.cs Shutdown, VM revert). Outcome enum `Accepted` / `Cancelled` /
  `Refused` umožňuje context-specifický popup.
- **Strom modulů** — `ModuleTreeGetter` parsuje wire formát z
  `ALVA.EDISONMAIN2.API.Tree.GetTree` (FAMILY / EXE / FAMILY_END).
- **Spouštění legacy modulů** — `IWindowNavigator.ShowLegacy(exe, fqn)` skládá
  10-arg payload z `EDServices.MLRest`, propaguje shellův GUID → moduly sdílejí
  ACTIVITY zámek. Trackuje child procesy, `CloseAllLegacy()` při shell close.
- **ACTIVITY zámek** — vytvořen `VerifyLic` při loginu. `EdisonActivityFailed` event
  → graceful shutdown. Uvolnění zámku přes `SessionLogout.Logout` (`ALVA.SYS.Login.Logout`,
  best-effort) — je v temp globálu, nezaniká se spojením. Volá se na **klonu** `IMLRest.With(...)`
  (žádná mutace sdílené instance), takže lze odhlásit i jinou firmu (company switch) bez dotčení
  běžící session. Místa: graceful close (`Application.Exit`), switch-user (stará session),
  company switch (zámek staré firmy). Hard-kill zámek neuvolní (uklidí server idle timeout);
  v dev to tlumí deterministický session GUID (`#if DEBUG` v `EstablishSession`) → re-run přepíše týž klíč.
- **Cooperative logout** — `LogoutWatcher` (60 s `DispatcherTimer`) pollí
  `ALVA.SYSTEM.Main.maSeUkoncit` (`CacheLocks=2`, aby poll neobnovoval idle timer); při
  signálu ukáže `LogoutCountdownWindow` (non-modální odpočet) → po vypršení shutdown.
  Pre-warning před tvrdým `EdisonActivityFailed`. Detekce idle je server-side.
- **Pamatuje poslední firmu** — `LocalPrefs` (JSON v `%APPDATA%\m-line\EDISONMAIN2\local.json`)
  ukládá `LastIdfa` po loginu / přepnutí. `AppBootstrapper` ho čte při startu jako prioritu
  před `IsDefault` flagem (přes `CompanySwitcher.PickInitial`). Funguje i v `#if DEBUG` modu.
  Přístup přes injektovaný `ILocalPrefs` (composition root vytvoří jednu instanci v
  `AppBootstrapper.LoadLocalPrefs`, propaguje do `CompanySwitcher` a `EdisonMainViewModel`
  přes ctor — testy substituují mock).
- **Persistence stavu oken** — automatická pro každý `EDViewModel`-derived VM
  (pozice, velikost, Maximized) přes existující legacy endpoint
  `ALVA.LIBRARY.Form.SaveParams` / `LoadParams`, per-MLP × IDFa. Default formID
  odvozen z názvu VM třídy (`EdisonMainViewModel` → `"EDISON2.EdisonMainWindow"`).
  Detaily v root [CLAUDE.md](../CLAUDE.md) (sekce "Persistence stavu").
- **App startup** — `Startup/App.xaml(.cs)` jen dispatchne na `AppBootstrapper`, který
  orchestruje celý bootstrap workflow (setup wizard → load prefs → login → MLRest → pick firmu →
  VerifyLic → ModuleTree → update check → font → return shell okno). Každý krok testovatelný v izolaci.
- **Setup wizard** — `AppBootstrapper.EnsureConfigured` (volaný v `Run` první): chybí
  `appsettings.json` (`SettingsLoader.Exists()`) → `SetupWindow` (server + REST port + test
  připojení přes `Database.GetCompanyList`) → `SettingsLoader.Save()`, pak pokračuje in-process.
  Jen konfigurace připojení (každá stanice); greenfield server-init (firma/licence/admin)
  zůstává na legacy `frmSetup`. Viz PLAN.md #5.
- **Updater** — klientský update mechanismus. `AppBootstrapper.CheckForUpdate` po loginu
  porovná lokální verzi (`VerzeInf`) proti serverové instalaci + Caché verzi (`Updater`,
  `ALVA.SETUP.Main.LoadSettings`); při rozdílu spawnne legacy `EDUPDATE.EXE` a ukončí shell.
  Runtime gate: `MLRest.EdisonVersion` z `VERZE.INF` → server-side per-request kontrola,
  `EdisonVersionFailed` → graceful shutdown. DEBUG build = verze `999999.9` (netriggeruje
  update). Status bar zobrazuje `Verze: X`. Detaily v [docs/UPDATE_FLOW.md](docs/UPDATE_FLOW.md).

## Spouštění legacy modulů — EDISONMAIN2API

Most do legacy světa: knihovna `EDISONMAIN2API.dll`
(`c:\MLineRepos\EDISON\EDISONMAIN2API\`), .NET Framework 4.8, deploy do
`C:\Edison\`. Obsahuje:

- `FormToRunGetter.TryGetFormFromArgs(args, out Form)` — parsne 10 cmdline args
  (sentinel `"EDISONMAIN2"`, server, port, ns, idfa, login, fullName, mlp, **formName** = FQN třídy WinForms formy, **shellGuid**),
  reflexí najde typ ve volajícím assembly, instancuje ho, naplní `MLRest`
  (sdílí shellův GUID + `CacheLocks=1` → ACTIVITY shell ↔ modul) a `MLProps`.

**Kontrakt:** legacy `.exe` ve své `Sub Main` (nebo `Program.Main`) zavolá
`TryGetFormFromArgs` a předá výsledek do `Application.Run(form)`. Bez argů
spustí původní standalone chování.

EDISONMAIN2 strana posílá args přes [WindowNavigator](../EDControls/dotnet/EDViewModel/WindowNavigator.cs)
— `ShowLegacy(exe, fqn)` pro modeless, `ShowLegacyAndWait` pro blokující (pre-login admin override
v `LicenceVerifier`). Libovolný legacy `.exe` se napojí přidáním `EDISONMAIN2API.dll` reference
a úpravou entry pointu na `FormToRunGetter` pattern (referenční implementace:
`MHD2PlanDopravy/Program.cs`, `USERS/Users-v4/Program.vb`, `EDISONMAIN/ApplicationEvents.vb` `OnRun`).

## Mimo MVP (zatím)

Reconnect UX (save-and-exit), admin Activity list, update lock broadcast, lokalizace,
greenfield server-init (firma/licence/admin — zůstává na legacy `frmSetup`). Detaily
prioritizace v [PLAN.md](PLAN.md) sekci „Features v pořadí priority".

## Struktura

```text
EDISONMAIN2/dotnet/
├── Startup/                    bootstrap workflow:
│                               - App.xaml(.cs) (tenký dispatcher → AppBootstrapper)
│                               - AppBootstrapper (template method s protected virtual hooky)
│                               - SettingsLoader, ILocalPrefs/LocalPrefs (injected do consumerů)
│                               - LicenceService (wrapper EDLicence.dll)
│                               - LicenceVerifier (VerifyLic retry loop + USERS.exe spawn)
│                               - CompanySwitcher (PickInitial + Switch — sdílená logika
│                                 startup × runtime přepnutí firmy)
│                               - AccessLoader (GetEdisonAccess + admin fallback)
│                               - Updater + VerzeInf (klientský update check + verze)
│                               - LogoutWatcher (cooperative logout poll maSeUkoncit)
└── Windows/
    ├── LoginWindow/            login dialog + UserAuthenticator
    ├── EdisonMainWindow/       hlavní okno (toolbar, tree, status bar)
    │   └── Modules/            IModule, ExecutableModule, ModuleFamily, ModuleTreeGetter,
    │                           ModuleLaunchabilityCheck (barva+suffix v názvu dle
    │                           launchovatelnosti: stub / chybí exe / neretrofitnuto)
    ├── SetupWindow/            first-launch konfigurace připojení (server + REST port)
    ├── LogoutCountdownWindow/  non-modální odpočet před force-logoutem
    └── ED*TestWindow/          dema EDControls komponent
```

`Services/` neexistuje záměrně — single-consumer infrastruktura je ko-lokovaná
se svým consumerem, cross-cutting věci jdou do `EDControls`.

## Komentáře

Defaultně nekomentovat. Komentář přidávej **jen** když:

- WHY je netriviální — skrytá invarianta, workaround konkrétního bugu, chování
  které by čtenáře překvapilo.
- Sémantika legacy systému (Caché global, ACTIVITY zámek, licenční flag) —
  chování serveru, které není zřejmé z kódu klienta.

**Nekomentovat:**

- WHAT kód dělá — pojmenování to říká.
- Reference na aktuální task / fix / commit (rotí, patří do commit message).
- "Used by X" / "added for Y" — patří do PR popisu.
- Stock VS šablony (`Interaction logic for...`).

**Délka:** preferuj 1 řádek, max 2–3. Dlouhé `<remarks>` bloky a multi-paragraph
docstringy jsou špatně. Pokud potřebuješ delší kontext, odkaz na MVP_PLAN.md
nebo RESPONSIBILITIES.md.

**Příklad** — komentář popisuje neobvyklou invariantu:

```csharp
// Index musí odpovídat MLRest.Idfa — tam má AppBootstrapper vytvořený ACTIVITY zámek.
_selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i))
    .First(x => x.c.Id == mlRest.Idfa).i;
```

Bez komentáře by čtenář zkusil `companies.FirstOrDefault(c => c.IsDefault)` a rozbil
by koexistenci shell ↔ moduly.
