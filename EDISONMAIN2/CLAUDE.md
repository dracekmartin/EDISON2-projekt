# EDISONMAIN2 — poznámky pro vývojáře

Hlavní WPF shell — login, výběr firmy, strom modulů, spouštění legacy `.exe` agend.
Nahradí staré VB.NET WinForms `EDISONMAIN`. Pilot-ready MVP definovaný v
[MVP_PLAN.md](MVP_PLAN.md) a [RESPONSIBILITIES.md](RESPONSIBILITIES.md).

## Co je hotové

- **Login** — `LoginWindow` + `UserAuthenticator` volá `ALVA.SYS.Login.Login`.
- **Výběr firmy** — combobox v toolbaru s textovým názvem firmy + IDFa v `[]`,
  abecedně řazeno (server vrací řazené přes `SortedDictionary`).
  `EdisonMainViewModel.SelectedCompanyIndex` setter mění `MLRest.Idfa` /
  `DataNamespace`, refreshuje strom modulů, re-volá VerifyLic přes
  `LicenceVerifier` (per-IdFa ACTIVITY zámek + cílové firmy licence). Při
  rejection revert na původní IdFa.
- **Licence** — `LicenceService` (wrapper kolem legacy `EDLicence.dll`); filtrace
  stromu modulů přes `EdisonMainViewModel.TryFilter` + `ExecutableModule.IsLicensed/ElementId`.
- **`LicenceVerifier`** — sdílený retry loop pro `VerifyLic`. Když je při
  out-of-license uživatel admin, nabídne spawnout legacy `USERS.exe formUSERSActivity`
  (přes EDISONMAIN2API), retry po zavření. Non-admin / Cancel → caller revertuje
  (App.xaml.cs Shutdown, VM revert). Outcome enum `Accepted` / `Cancelled` /
  `Refused` umožňuje context-specifický popup.
- **Strom modulů** — `ModuleTreeGetter` parsuje wire formát z
  `ALVA.EDISONMAIN2.API.Tree.GetTree` (FAMILY / EXE / FAMILY_END).
- **Spouštění legacy modulů** — `IWindowNavigator.ShowLegacy(exe, fqn)` skládá
  10-arg payload z `EDServices.MLRest`, propaguje shellův GUID → moduly sdílejí
  ACTIVITY zámek. Trackuje child procesy, `CloseAllLegacy()` při shell close.
- **ACTIVITY zámek** — vytvořen `VerifyLic` při loginu. `EdisonActivityFailed` event
  → graceful shutdown.
- **Pamatuje poslední firmu** — `LocalPrefs` (JSON v `%APPDATA%\m-line\EDISONMAIN2\local.json`)
  ukládá `LastIdfa` po loginu / přepnutí. App.xaml.cs ho čte při startu jako prioritu
  před `IsDefault` flagem. Funguje i v `#if DEBUG` modu.
- **Persistence stavu oken** — automatická pro každý `EDViewModel`-derived VM
  (pozice, velikost, Maximized) přes existující legacy endpoint
  `ALVA.LIBRARY.Form.SaveParams` / `LoadParams`, per-MLP × IDFa. Default formID
  odvozen z názvu VM třídy (`EdisonMainViewModel` → `"EDISON2.EdisonMainWindow"`).
  Detaily v root [CLAUDE.md](../CLAUDE.md) (sekce "Persistence stavu").
- **App startup** — `Startup/App.xaml(.cs)` orchestruje celý bootstrap.

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

Setup wizard, reconnect UX, admin Activity list, update lock broadcast, LDAP UI,
cooperative logout countdown. Detaily fází F1–F8 v [MVP_PLAN.md](MVP_PLAN.md).

## Struktura

```text
EDISONMAIN2/dotnet/
├── Startup/                    bootstrap: App.xaml(.cs), SettingsLoader, LocalPrefs,
│                               LicenceService (wrapper EDLicence.dll), LicenceVerifier
│                               (VerifyLic retry loop + USERS.exe spawn)
└── Windows/
    ├── LoginWindow/            login dialog + UserAuthenticator
    ├── EdisonMainWindow/       hlavní okno (toolbar, tree, status bar)
    │   └── Modules/            IModule, ExecutableModule, ModuleFamily, ModuleTreeGetter
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
// Index musí odpovídat MLRest.Idfa — tam má App.xaml.cs vytvořený ACTIVITY zámek.
_selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i))
    .First(x => x.c.Id == mlRest.Idfa).i;
```

Bez komentáře by čtenář zkusil `companies.FirstOrDefault(c => c.IsDefault)` a rozbil
by koexistenci shell ↔ moduly.
