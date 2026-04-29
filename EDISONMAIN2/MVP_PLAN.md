# EDISONMAIN2 MVP — implementační plán

## Context

EDISONMAIN2 (C# / WPF) má v budoucnu nahradit starou EDISONMAIN (VB.NET WinForms).
Aktuální stav EDISONMAIN2 je ranný (~10-15 % core funkcionalit): základní WPF
shell s EDControls knihovnou, login (jen EDISON mód), IdFa switching, module tree,
process launcher. **Chybí**: licence, ACTIVITY zámky, setup wizard, LDAP, reconnect,
menu, persistence, update, koexistenční infrastruktura.

**Cíl tohoto plánu:** dovést EDISONMAIN2 do stavu **pilot-ready MVP** — tedy do
bodu, kdy lze klienta bezpečně paralelně nasadit vedle stávajícího EDISONMAIN
proti stejné Caché instanci. "Pilot-ready" definujeme 6 konkrétními kritérii
z `c:\MLineRepos\EDISON2\EDISONMAIN2\RESPONSIBILITIES.md` § 18.3.

**Co pilot NENÍ**: plná feature-parita s EDISONMAIN. Hostované moduly (JR, MZDY,
TICKETS…) zůstávají jako legacy `.exe` pouštěné přes existující `ProcessLauncher`.
Menu, desktop, mini-aplikace, lokalizace a auto-update jsou mimo MVP.

**Definition of Done (6 kritérií):**

1. Oba klienti se přihlásí do stejné Caché instance bez chyby `"EdisonVersionFailed!"`.
2. ACTIVITY zámky obou klientů se v `^||ML(...)` neprolínají; admin vidí obě
   sessiony a umí je individuálně odhlásit.
3. Business zámek vytvořený v EDISONMAIN2 uvidí uživatel v EDISONMAIN (a naopak).
4. Update mechanismus — admin může vydat update bez rozbíjení sessionů druhého klienta.
5. Licence funguje pro oba klienty (EDISONMAIN2 dostane stejnou licenci).
6. Legacy workaround `UNDONE: 2021-07-07` v `formUSERSActivity.cls` ověřen /
   zdokumentován.

---

## Strategická logika pořadí

1. **Backend-first pro vše, co vyžaduje deployment do Caché** (F1). Změny
   v `Rest.cls`, `Activity.cls`, `Login.cls` mají delší turnaround a blokují
   klient testování.
2. **Koexistence před novými featurami.** DoD 1/2/3/5 jsou blokující pro pilot.
   Teprve pak setup wizard, reconnect, update (4, 6).
3. **Každá fáze demoovatelná** — UI obrazovka nebo měřitelná vlastnost.
4. **LDAP, menu, update klienta = MVP-light.** Minimum aby DoD prošlo.

### Mapování fází na DoD kritéria

| Fáze | DoD |
|---|---|
| F1 Caché backend rozšíření | 1 (backend), 2 (metadata), 6 |
| F2 ACTIVITY lifecycle + version check | 1 (klient), 2 (klient) |
| F3 Licenční vrstva | 5 |
| F4 Setup wizard + EDISONini + business lock smoke test | 3 |
| F5 Reconnect + auto-logout UX | — (robustnost) |
| F6 Admin Activity list + kick-and-login | 2 (admin), 4 (část) |
| F7 Update lock broadcast | 4 |
| F8 Pilot hardening + regression | všech 6 verifikováno |

**Kritická cesta:** F1 → F2 → F3 / F4 (paralelně) → F6 → F7 → F8. F5 může jít
paralelně po F2.

---

## F1 — Caché backend rozšíření pro koexistenci (1-2 týdny)

**Cíl:** Server umí rozlišit EDISONMAIN vs. EDISONMAIN2, přijme obě verze ve
`CheckVersion`, ukládá `ClientKind` do ACTIVITY metadata, má nový update-lock
global a nový endpoint pro admin kick. **Všechno zpětně kompatibilní** se starým
klientem.

**Tasky:**

1. `Rest.cls` → `CheckVersion` rozšířit o volitelný parametr `clientKind` (default
   `"EDISONMAIN"` pro backward compat). Nová helper metoda
   `IsVersionSupported(clientKind, clientVersion)` čtoucí z globalu
   `^ALVA.SYS("Client","supportedVersions",<kind>,<versionKey>)`.
2. `MLRest.cs` → přidat property `ClientKind` (default `"EDISONMAIN"`), posílat
   v `sys` parametrech. **Sdílené DLL** — obě verze ho pak používají.
3. `Activity.cls` → `UpdateInfo` přijímá `clientKind`, ukládá do
   `^||ML(<idfa>,<ip>,<guid>,"meta","clientKind")` (append-only, staré VB konzole ignorují).
4. `Login.cls` → `ClientExecutableName` podle `clientKind` ("EDISONMAIN" /
   "EDISONMAIN2"), fallback "EDISON".
5. Nová třída `ALVA.SYS.UpdateLock` s `Acquire(initiator)`, `Release()`,
   `IsActive()`. Global `^EDISON("System","Update","InProgress")`.
6. Nová metoda `ALVA.SYS.Login.KickAndLogin(targetGuid)` — server-side ověří
   admin credentials, smaže cílový lock, zapíše audit.
7. **Legacy UNDONE workaround (DoD 6)**: ověřit, že EDISONMAIN2 vždy volá
   s `$$$IsMLR=1`, takže workaround se nikdy netriggeruje; zdokumentovat,
   neodstraňovat (rozbije staré VB volání).

**Soubory (EDISON repo — Caché):**

- `c:\MLineRepos\EDISON\MLRest\MLRest\CACHE\ALWA\SYS\Rest.cls`
- `c:\MLineRepos\EDISON\MLRest\MLRest\CACHE\ALWA\SYS\Activity.cls`
- `c:\MLineRepos\EDISON\EDUsers\EDUsers\CACHE\ALVA\SYS\Login.cls`
- `c:\MLineRepos\EDISON\MLRest\MLRest\MLRest.cs` (sdílené .NET DLL)
- **nový** `c:\MLineRepos\EDISON\MLRest\MLRest\CACHE\ALWA\SYS\UpdateLock.cls`

**DoD pokryto:** 1, 2, 6.

---

## F2 — ACTIVITY lifecycle a version check (1-2 týdny)

**Cíl:** EDISONMAIN2 se přihlásí, ohlásí `ClientKind="EDISONMAIN2"`, vytvoří si
vlastní ACTIVITY zámek (stabilní GUID per proces), udržuje ho keepalivem,
korektně reaguje na `EdisonActivityFailed`, při logoutu smaže zámek.

**Tasky:**

1. Rozšířit `IMLRest` (`EDControls/IMLRest.cs`) o `Guid`, `ClientKind`.
2. `MLRestAdapter` — proxy na `Inner.GUID`, set `ClientKind="EDISONMAIN2"` při
   konstrukci.
3. Nová služba `ActivitySession`:
   - Po `UserAuthenticator.Login` zavolat `ALVA.SYS.Login.VerifyLic(CountsLOGIN)`
     (server vytvoří `LockSet` + `UpdateInfo`).
   - `System.Threading.Timer` (default 60 s, configurable) → `Rest.Refresh()`
     (viz § 3.6).
   - Subscribe na `MLRest.EdisonActivityFailed` → popup + shutdown/reconnect
     (reconnect je F5).
   - Při logoutu volá `ALVA.SYS.Login.Logout`.
4. `App.xaml.cs` — vytvořit `ActivitySession` po loginu, vložit do
   `EDServices.Current`; při `Exit` best-effort `Logout()`.
5. **Singleton invariant (§ 3.7):** XML doc na `IMLRest` + `MLRestAdapter` — žádný
   modul nesmí volat `new MLRest(...)`; vše jde přes `EDServices.Current.MLRest`.
6. `maSeUkoncit` poll integrovaný do keepalive — pokud flag nastaven, zobrazit
   countdown overlay (F5 pro full UX).

**Soubory (EDISON2):**

- `c:\MLineRepos\EDISON2\EDControls\dotnet\IMLRest.cs` (rozšíření)
- `c:\MLineRepos\EDISON2\EDControls\dotnet\MLRestAdapter.cs` (rozšíření)
- **nový** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\ActivitySession.cs`
- **nový** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\IActivitySession.cs`
- `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\App.xaml.cs`
- Testy v `EDISONMAIN2.Tests`

**Reuse:** chování z `EDUsers.vb:283-310` (VerifyLic), `clsUpdater.vb:2073-2103`
(`maSeUkoncit`), `frmNoMDI.vb:5023-5032` (activity failed handler). Portovat
chování, nekopírovat kód.

**DoD pokryto:** 1 (klient), 2 (klient).

**Závislosti:** F1 nasazená na testovací Caché.

---

## F3 — Licenční vrstva (1-2 týdny)

**Cíl:** EDISONMAIN2 načítá licenci z Caché, respektuje `Elements` whitelist,
blokuje login při `OutOfLicense`, filtruje modul tree. Sdílí úložiště s EDISONMAIN.

**Tasky:**

1. `Lic` POCO v `EDControls/Licence/` — všechny properties z § 13.1 (`Version`,
   `Type`, `Idfa`, `ValidityFrom/To`, `ActivateTo`, `Elements`, `CountsLOGIN`,
   `CountsUSERS`, `CountsRZ`, `CountsZAM`, `CountsTELMAX`, `CountsCACHE`, atd.).
2. `LicState` enum (`E01_OK`, `E02_NOT_ACTIVATE`, `E03_NOT_INITKEY`, `E04_OUT_OF_DATE`).
3. `LicenceLoader` → volá `ALVA.LICENCE.Activation.GetActiveLic` přes MLRest.
   Parser tilde+pipe+newline formátu (§ 13.3), **podpora jen verzí 1.003 – 2.002**.
   Legacy < 1.003 explicitně odmítnout se srozumitelnou hláškou.
4. Default fallback licence (§ 13.4) když server vrátí prázdno: `Version="1.003"`,
   `Type="Nezadána"`, `CountsLOGIN=1`, `Elements = { formEDISONMAINSettingsShow, SpolecnaEvidence }`.
5. Integrace do login flow: po přihlášení → `LicenceLoader` → uložit do
   `EDServices.Current.ActiveLicense`. Pokud `VerifyLic=-1` → zobrazit chybu
   (dialog v F6 nahradí za admin kick flow).
6. `LicenceGate` helper — předat `Lic.Elements` do `ModuleTreeGetter` pro filtraci
   stromu (§ 5.2 whitelist logika).
7. Expiration check — pokud `ValidityTo < NOW + 15 dní`, best-effort
   `ALVA.LICENCE.EDLicence.Update.UpdateLicFromMlineToCustomer` (fallback neblokuje).
8. Status bar binding — typ licence + `ValidityTo` jako TextBlock.
9. Minimální `AddLicenseDialog` — TextBox na Base64 + Ok → `ALVA.LICENCE.Activation.Add`.
   Vystaveno přes skrytou zkratku (`Ctrl+Shift+L`) — plný admin UI mimo MVP.

**Soubory (EDISON2):**

- **nové** `c:\MLineRepos\EDISON2\EDControls\dotnet\Licence\Lic.cs`
- **nové** `c:\MLineRepos\EDISON2\EDControls\dotnet\Licence\LicState.cs`
- **nové** `c:\MLineRepos\EDISON2\EDControls\dotnet\Licence\LicenceLoader.cs`
- **nové** `c:\MLineRepos\EDISON2\EDControls\dotnet\Licence\LicenceGate.cs`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\AddLicenseDialog\AddLicenseDialog.xaml(.cs)`
- Úprava: `c:\MLineRepos\EDISON2\EDControls\dotnet\EDViewModel\EDServices.cs` (property `ActiveLicense`)
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\Modules\ModuleTreeGetter.cs`

**Reuse:** logika z `Licence/EDLicence/Lic.vb`, `Api.vb:GetLicFromCodeLic`,
`Activation.vb:GetActiveLic` — portovat na C# (parser je jednoduchý string-split).
**Nepoužívat `EDLicence.dll`** (Framework 4.8 vs. .NET 10 inkompatibilní).

**DoD pokryto:** 5.

**Závislosti:** F2.

---

## F4 — Setup wizard + EDISONini + business lock smoke test (1-2 týdny)

**Cíl:** First-launch wizard vytvoří `EDISONini.xml`. EDISONMAIN2 se spustí na
čerstvé stanici bez ručního editování configu. Ověření cross-client business
locks (DoD 3).

**Tasky:**

1. `EdisonIniService` — čtení/zápis `EDISONini.xml` (schéma z § 11.1). Pro MVP
   **share s EDISONMAIN** (`C:\Edison\EDISONini.xml`) v read-only módu; zápis
   jen při setup wizardu.
2. First-launch detekce v `App.xaml.cs` před loginem: neexistuje nebo
   `<initialized>0</initialized>` → spustit Setup Wizard.
3. Setup Wizard UI (`SetupWindow`) — 3 kroky: Connection, Database+IdFa, Test+první
   login. Po úspěchu `MESSAGE_SetupChanged` + shutdown. Zrušení = `MESSAGE_SetupMissing`
   + exit bez XML.
4. `SettingsLoader` fallback order: `EDISONini.xml` → `appsettings.local.json`
   → `appsettings.json`.
5. **Business lock smoke test (DoD 3):** integration test scénář:
   - EDISONMAIN otevře edit uživatele X → business lock v Caché.
   - EDISONMAIN2 pokusí o edit → musí dostat "Záznam je zamčený".
   - Sdílený `MLRest.dll` + stejný volací pattern to zajistí implicitně — cíl
     je verifikovat, ne implementovat.

**Soubory (EDISON2):**

- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\EdisonIniService.cs`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\SetupWindow\SetupWindow.xaml(.cs)`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\SetupWindow\SetupViewModel.cs`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\SettingsLoader.cs`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\App.xaml.cs`

**Reuse:** `frmSetup.vb`, `frmNoMDI.vb:1870-2011` (XML schéma 1:1),
`frmNoMDI.vb:1819-1833` (`EdisonIsInitialized` property).

**DoD pokryto:** 3 (smoke test).

**Závislosti:** F2, F3.

---

## F5 — Reconnect + auto-logout UX (1 týden)

**Cíl:** Ztráta spojení nespadne aplikaci — nabídne reconnect. Při timeoutu /
admin force-logout elegantní countdown dialog.

**Tasky:**

1. `ReconnectWindow` (ekvivalent `frmSecuredLogin`) — modální dialog s předvyplněným
   jménem, password boxem. Po úspěchu vytvoří novou `MLRest` session (**nový GUID**
   ale stejný user), nahradí `EDServices.Current.MLRest`.
2. `ActivitySession` (F2) — místo okamžitého shutdown na `EdisonActivityFailed`
   nabídnout reconnect.
3. Cooperative logout UX (§ 3.9) — modální dialog "Ukončení za X s — Uložit
   a odhlásit / Zahodit a odhlásit" s countdownem (vylepšení oproti EDISONMAIN
   balloon-only).
4. `OnBeforeLogout` event v `EDViewModel` pro hostované moduly (pro MVP stačí
   event existuje; moduly ho budou využívat později).
5. Secured mode — přečíst `<securedMode>` z `EDISONini.xml`. Pro MVP stačí režim
   `0` (uživatel znovu zadá heslo); `1` (transparentní reconnect z paměti) = iterace 2.

**Soubory (EDISON2):**

- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\ReconnectWindow\ReconnectWindow.xaml(.cs)`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\LogoutCountdownWindow\LogoutCountdownWindow.xaml(.cs)`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\ActivitySession.cs`
- Úprava: `c:\MLineRepos\EDISON2\EDControls\dotnet\EDViewModel\EDViewModel.cs`

**Reuse:** `frmSecuredLogin.vb`, `frmNoMDI.vb:4853-4866`, `frmNoMDI.vb:4907-4929`.

**DoD pokryto:** — (robustnost pilotu, nepatří přímo pod DoD).

**Závislosti:** F2.

---

## F6 — Admin Activity list + kick-and-login (1-2 týdny)

**Cíl:** Admin v EDISONMAIN2 vidí všechny aktivní sessiony (rozlišené podle
`ClientKind`) a umí je kicknout. Admin může kicknout při `OutOfLicense` aby se
dostal sám dovnitř.

**Tasky:**

1. Minimální menu bar v `EdisonMainWindow.xaml` — sekce "Nástroje" → "Správa
   Activity" (další položky mimo MVP).
2. `ActivityListWindow` — volá `formUSERSActivity.LoadData` z Caché. Sloupce:
   IdFa, Uživatel, FullName, IP, GUID, login time, **ClientKind**.
3. Akce: "Odhlásit" (`VzdaleneUkonceniEdisona(guid, sekundy)`), "Force-kick"
   (nová `KickAndLogin` z F1).
4. **Admin override v LoginWindow (§ 2.9)**: když `VerifyLic=-1` → dialog:
   - User není admin → jen chybová hláška.
   - User je admin (`LoginResult.IsAdmin=1`) → `ActivityListWindow` se
     specializovaným tlačítkem "Odpojit a přihlásit se místo něj" → volá
     `KickAndLogin(targetGuid)` → retry `VerifyLic` → pokračuje.
5. Implementace jako `EDViewModel` subclass s `EDList`, `EDToolbar`; reuse
   existujících komponent.

**Soubory (EDISON2):**

- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\ActivityListWindow\ActivityListWindow.xaml(.cs)`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\ActivityListWindow\ActivityListViewModel.cs`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\ActivityListWindow\ActivitySessionRow.cs`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\EdisonMainWindow\EdisonMainWindow.xaml` (menu bar)
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\LoginWindow\LoginWindow.xaml.cs`

**Reuse:** `formUSERSActivity.cls:LoadData`, logika z `formUSERSActivity.vb:547`
(`VzdaleneUkonceniEdisona` call), nová `KickAndLogin` z F1.

**DoD pokryto:** 2 (admin vidí a umí odhlásit), 4 (část — force logout).

**Závislosti:** F1 (KickAndLogin, ClientKind), F2, F3.

---

## F7 — Update lock broadcast (1-2 týdny)

**Cíl:** Admin pouští update libovolného klienta bez rizika poškození druhého.

**Tasky:**

1. `UpdateLockService` klient-side — `Acquire(initiator)`, `Release()`, `IsActive()`
   nad Caché z F1.
2. `ActivitySession` (F2) keepalive poll kromě `maSeUkoncit` kontroluje i
   `UpdateLock.IsActive()`. Pokud aktivní → broadcast notifikace "Probíhá update,
   uložte rozpracovanou práci" s countdownem.
3. Login blokace při aktivním update locku — `LoginWindow` / `VerifyLic` wrapper.
4. **Cleanup business zámků při force-logout** — rozšířit server-side
   `VzdaleneUkonceniEdisona` (v `ALVA.EDISONAPI.Updater`) o `Lock.Clear(guid)`
   pro každou kickovanou session. (Server-side úprava — malé, ale v EDISON repu.)
5. Admin UI: "Zahájit update režim" tlačítko v Nástrojích (jen admin) → acquire
   `UpdateLock`, dialog s progress ("Čekám na odhlášení N uživatelů"), po vyčistění
   release.
6. **Auto-update klienta EDISONMAIN2 = OUT OF SCOPE MVP.** Single-file publish,
   update ruční copy binárky. Runbook dokumentuje.

**Soubory (EDISON2):**

- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\UpdateLockService.cs`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\UpdateModeWindow\UpdateModeWindow.xaml(.cs)`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Services\ActivitySession.cs`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\Windows\LoginWindow\LoginWindow.xaml.cs`

**Soubory (EDISON repo — Caché):**

- `c:\MLineRepos\EDISON\EDUsers\EDUsers\CACHE\ALVA\EDISONAPI\Updater.cls`
  (`VzdaleneUkonceniEdisona` — přidat cleanup business locků)

**Reuse:** `clsUpdate.vb:547`. System-wide lock je **nová funkcionalita** (§ 12.3).

**DoD pokryto:** 4.

**Závislosti:** F1, F2, F6.

---

## F8 — Pilot hardening, regression a dokumentace (0.5-1.5 týdne)

**Cíl:** Všech 6 DoD kritérií verifikováno na dual-client scénáři. Pilot stanice
nasazena. Runbook pro M-line support tým.

**Tasky:**

1. **DoD checklist walkthrough** — pro každé ze 6 kritérií reprodukovatelný test:
   - K1: dva klienti na stejné Caché, žádný `EdisonVersionFailed!`.
   - K2: Caché terminál ukazuje obě session s `ClientKind`; `ActivityListWindow`
     je také ukazuje; kick funguje.
   - K3: EDISONMAIN2 otevře business zámek → EDISONMAIN detekuje.
   - K4: Update run — admin pustí `UpdateMode`, klienti dostanou notifikaci,
     neleaknou zámky.
   - K5: import licence v EDISONMAIN → aktivní v EDISONMAIN2 po refresh.
   - K6: regression — starý EDISONMAIN funguje stejně.
2. **Logging** — rozšířit `PopupDisplayer.Error` o zápis do Caché globalu pro
   diagnostiku paralelního provozu.
3. **LDAP / LDAP-AUTO minimum** — aby login flow neselhal u uživatelů s feature
   #23 ≠ EDISON. `UserAuthenticator.Login` pošle prázdné heslo při LDAP-AUTO.
   Full LDAP config UI = OUT OF SCOPE MVP.
4. **Runbook** — `PILOT_RUNBOOK.md`: nasazení na pilotní stanici, ověření, kontakty.
5. **Performance check** — 5+ modulů, 30+ min provozu, inspekce `^||ML` globalu —
   musí ukazovat **jednu** session per klient (verifikace § 3.7 fix).
6. **Odstranit `#if DEBUG` hardcoded login** v `App.xaml.cs:46-48`.

**Soubory:**

- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\docs\PILOT_RUNBOOK.md`
- **nové** `c:\MLineRepos\EDISON2\EDISONMAIN2\docs\DOD_TESTS.md`
- Úprava: `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\App.xaml.cs`

**DoD pokryto:** všech 6 verifikováno.

**Závislosti:** všechny předchozí.

---

## Kritické soubory (cheatsheet)

**Backend (EDISON repo, Caché):**

- `c:\MLineRepos\EDISON\MLRest\MLRest\CACHE\ALWA\SYS\Rest.cls` — `CheckVersion`
- `c:\MLineRepos\EDISON\MLRest\MLRest\CACHE\ALWA\SYS\Activity.cls` — `UpdateInfo` s `ClientKind`
- `c:\MLineRepos\EDISON\EDUsers\EDUsers\CACHE\ALVA\SYS\Login.cls` — `Login`, `VerifyLic`, nová `KickAndLogin`
- `c:\MLineRepos\EDISON\MLRest\MLRest\MLRest.cs` — sdílené .NET DLL (nová property `ClientKind`)
- **nová** `UpdateLock.cls`

**Klient EDISONMAIN2 (EDISON2 repo):**

- `c:\MLineRepos\EDISON2\EDISONMAIN2\dotnet\App.xaml.cs` — bootstrap, drátování všeho
- **nový** `Services\ActivitySession.cs` — srdce koexistence (F2)
- **nový** `Services\UpdateLockService.cs` (F7)
- **nový** `Services\EdisonIniService.cs` (F4)
- **nové** `EDControls\Licence\{Lic, LicenceLoader, LicenceGate, LicState}.cs` (F3)
- **nová okna** Setup, Reconnect, LogoutCountdown, ActivityList, UpdateMode, AddLicense

**Reuse z existujícího EDISON (VB.NET) — portovat chování, ne kód:**

- `frmNoMDI.vb:1819-1833` — `EdisonIsInitialized` property (F4)
- `frmNoMDI.vb:1870-2011` — Save/ReadSettingsFromFile XML schéma (F4)
- `frmNoMDI.vb:4853-4866, 4907-4929, 5023-5032` — `maSeUkoncit` handler, countdown, `EdisonActivityFailed` (F5)
- `frmSetup.vb` — wizard UX (F4)
- `frmSecuredLogin.vb` — reconnect dialog (F5)
- `EDUsers.vb:283-310` — VerifyLic volání (F2)
- `clsUpdater.vb:2073-2103` — `maSeUkoncit` poll (F2/F5)
- `Licence\EDLicence\Lic.vb`, `Api.vb`, `Activation.vb` — licenční parser (F3)
- `formUSERSActivity.cls:LoadData`, `formUSERSActivity.vb:547` — admin kick (F6)
- `clsUpdate.vb:547` — `VzdaleneUkonceniEdisona` (F7)

**Reuse z EDISONMAIN2 (již hotové):**

- `EDControls` komponenty (EDViewModel, EDFooter, EDToolbar, EDInputForm, EDList,
  EDActionInProgress, EDIcons, EDStrings, PopupDisplayer, WindowNavigator, EDServices)
- `IMLRest` / `MLRestAdapter` singleton kontejner
- `UserAuthenticator`, `ModuleTreeGetter`, `ProcessLauncher`, `SettingsLoader`

---

## Pracnostní odhad

| # | Fáze | Pracnost |
|---|------|----------|
| F1 | Caché backend rozšíření | M (1-2 týdny) |
| F2 | ACTIVITY lifecycle + version check | M (1-2 týdny) |
| F3 | Licenční vrstva | M (1-2 týdny) |
| F4 | Setup wizard + EDISONini + business lock test | M (1-2 týdny) |
| F5 | Reconnect + auto-logout UX | S-M (1 týden) |
| F6 | Admin Activity list + kick-and-login | M (1-2 týdny) |
| F7 | Update lock broadcast | M (1-2 týdny) |
| F8 | Pilot hardening + regression | S-M (0.5-1.5 týdne) |

**Celkem: 8-14 týdnů jednoho inženýra full-time.** Paralelizací F3+F5 po F2 lze
zkrátit o 1-2 týdny.

---

## Out of scope MVP (post-pilot iterace)

- Plný auto-update klienta EDISONMAIN2 (pouze manuál copy pro pilot).
- LDAP / LDAP-AUTO UI a konfigurace (jen minimum aby login prošel).
- Desktop shortcuts, mini-aplikace, lokalizace, easter eggs, seasonal windows.
- Plný admin menu systém (`formUSERSMain`, `formUSERSLocks`, `formLICENCEINFOManage`).
- Runtime license lock při expiraci za běhu.
- Moderní šifrování licence (legacy `mline2k9` klíč zůstává).
- Grace period pro expirovanou licenci.
- `ReconnectWindow.securedMode=1` (transparentní reconnect z paměti).
- Ctrl+W+W / Ctrl+Shift+B shortcut proxy na legacy VB okna.
- Save-and-exit hook s persistencí rozdělané práce.
- Resume session po re-loginu.
- Windows idle detection.

---

## Verifikace (end-to-end)

### Testovací prostředí

- **Testovací Caché instance** s aplikovanou F1 deployment.
- **Dvě pilotní stanice**: jedna s EDISONMAIN (legacy .exe), druhá s EDISONMAIN2
  (fresh publish).
- **Common user account** v obou klientech (např. interní M-line testovací user).

### DoD checklist

| # | Kritérium | Ověření |
|---|-----------|---------|
| 1 | Oba klienti se přihlásí | Login v EDISONMAIN2 vrací `mlp > 0` bez "EdisonVersionFailed!". Login v EDISONMAIN vrací totéž. |
| 2 | ACTIVITY kompat | Caché terminál `zw ^\|\|ML(...)` ukáže obě session s různými GUIDy a `ClientKind` v `"meta"`. `ActivityListWindow` v EDISONMAIN2 zobrazí obě s rozlišením. Kick jednoho klienta nezabije druhého. |
| 3 | Business lock | EDISONMAIN otevře edit záznamu → EDISONMAIN2 pokus o edit stejného → "Záznam je zamčený uživatelem X". A naopak. |
| 4 | Update koexistence | Admin v EDISONMAIN2 pustí UpdateMode → EDISONMAIN klient dostane broadcast notifikaci, uloží, odhlásí se. Žádné zombie business locks v Caché po force-logoutu. |
| 5 | Licence | Import licence přes EDISONMAIN → EDISONMAIN2 po refresh vidí stejné `Elements`. `CountsLOGIN` limit přihlášení funguje v obou. |
| 6 | Legacy workaround | Regression test: starý EDISONMAIN pouští stejně jako dnes. Zdokumentováno v `DOD_TESTS.md`. |

### Automatizace

- **Integrační testy** v `EDISONMAIN2.Tests` projektu pro F2-F7 — mock `IMLRest`,
  verify metoda volání a parametrů.
- **End-to-end manuál** dle `DOD_TESTS.md` — protokolovaný test pro každé DoD
  kritérium s screenshoty.

### Sign-off

- Pilot stanice běží 1 týden bez regressu.
- Žádný zombie lock v `^||ML(...)` po 24h.
- Žádné `EdisonVersionFailed!` / `ACTIVITY: Lock exist failed!` errory v logu.
- Admin akce (kick, UpdateMode) fungují proti oběma klientům.
- Dokumentace `PILOT_RUNBOOK.md` předána M-line support týmu.

Po sign-off lze pilot rozšířit na širší sadu stanic a začít iteraci 2 (full
admin menu, auto-update, LDAP UI, mini-apps).
