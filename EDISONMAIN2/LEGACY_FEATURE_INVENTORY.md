# EDISONMAIN — úplný inventář funkcí (replacement checklist pro EDISONMAIN2)

> **Účel:** exhaustivní katalog **všech** funkcí starého VB.NET WinForms shellu `EDISONMAIN`,
> aby ho EDISONMAIN2 mohl plnohodnotně nahradit. Pro každou funkci: co dělá, kde v legacy
> žije, a jaký je její **stav v EDISONMAIN2**. Slouží jako **replacement checklist** —
> co je hotové, co částečně, co chybí, a co se vědomě neportuje.

> **Scope:** celý shell `c:\MLineRepos\EDISON\EDISONMAIN\` (44 formů/tříd, ~24 tis. řádků).
> **Nezahrnuje** business logiku hostovaných agend (MHD, MZDY, JR, …) — ty mají vlastní kód.
> Sestaveno hloubkovým čtením všech formů (per-form), ne odhadem.

> **Vztah k ostatním dokumentům:**
> - [`RESPONSIBILITIES.md`](RESPONSIBILITIES.md) — hloubkový rozbor odpovědností + „studnice nápadů" (1:1 port úvah z legacy, hodně aspirativního). Organizovaný po odpovědnostech.
> - **Tento dokument** — exhaustivní **per-form inventář** + jednotná **status matice**. Komplementární: kde RESPONSIBILITIES jde do hloubky principů, tady je „máme pokryté úplně všechno?".

> **⚠️ Snímek k datu (2026-06-30).** Stav vychází z tehdejšího plánu / `CLAUDE.md` +
> čtení kódu. Než na něco navážeš, ověř proti aktuálnímu kódu — některé `file:line` reference
> zestárnou.

## Legenda stavu

| Symbol | Význam |
|---|---|
| ✅ **Hotovo** | V EDISONMAIN2 implementováno (funkčně ekvivalentní, byť jinou technologií). |
| 🟡 **Částečně** | Existuje jádro / ekvivalent jinou cestou / s odchylkami nebo přes legacy bridge. |
| ❌ **Chybí** | Není implementováno a má (nebo může mít) reálnou hodnotu pro náhradu. |
| ⏭️ **Neportovat** | Vědomě mimo scope (dead code, legacy-specifické, nahrazeno jiným modelem, nebo nízká hodnota). |

---

## Executive summary

**Celkový stav náhrady:** jádro shellu (startup, login, session/ACTIVITY zámek, přepínání firmy,
spouštění legacy modulů, updater, cooperative logout, mini-app infrastruktura) je **hotové nebo
funkčně pokryté**. Největší zbývající mezery jsou v **uživatelských UI funkcích** (plocha se
zástupci, startup formulář, vyhledávání ve stromu, změna hesla) a **admin UI** (správa nastavení,
práv, uživatelů, úloh) — to je z velké části vědomě delegováno na legacy `USERS.exe` / `frmSetup`
nebo odloženo post-MVP.

**TOP mezery (❌, mají reálnou hodnotu pro plnou náhradu):**

1. **Změna hesla** (`frmChangePassword`) — flag `ChangePasswordNeeded` se parsuje, ale dialog ani vynucený-reset cyklus (-108) neexistuje.
2. **Runtime přepnutí připojení** (`mnuPrgConnect` / multi-connection list) — stub `NotYetImplemented` (S1).
3. **Plocha se zástupci** (EDDesktop: drag-drop, persist, uspořádat) + **startup formulář** (auto-otevření po loginu).
4. **Per-firma barevné schéma** shellu (`RefreshMainformScheme`).
5. **Mini-app rendering detaily** — ikony řádků (parsují se, nerenderují), číselné formátování buněk, typové třídění, tisk.
6. **Lokalizační infrastruktura** (feature #4, Open) — dnes bridge na legacy `formEDISONMAINLanguageList`.
7. **Remember-me s heslem** (AES) — EDISONMAIN2 pamatuje jen jméno + firmu.

**Vědomě NEportujeme:** secured-login/reconnect (#3 zrušeno), cmdline login (nahrazeno `FormToRunGetter`),
klientská idle detekce (server-side), TRAY ikona, sezónní okna `frmW8*`, Debug menu, MDI specifika,
iexplore nápověda, TeamViewer/RunEdisonCopy, helpdesk, C1 reporting, server-init wizard část (firma/
licence/admin — zůstává na legacy `frmSetup`).

---

## 1. Startup, hlavní okno, životní cyklus

Legacy: `frmNoMDI.vb` (~4456 ř., hostitelské okno), `ApplicationEvents.vb`, `modMain.vb`, `frmSplash.vb`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Kompletní startup workflow | `frmNoMDI.frmMDI_Load` | Sekvenčně: launch-reason check → splash → init check → setup wizard → read settings → connection → login → user settings → company menu → licence → update check → load tree → changelog. Exception → CloseEdison. | ✅ Přepsáno do testovatelného `AppBootstrapper.Run` (template method s kroky). | PLAN C2; CLAUDE „App startup" |
| App entry / OnRun (most do legacy) | `ApplicationEvents.OnRun` | EDISONMAIN2 retrofit: `args(0)="EDISONMAIN2"` → `FormToRunGetter` → ShowDialog; bez argů legacy shell. | ✅ Vlez do legacy `.exe`; EDISONMAIN2 sám má `App.xaml.cs`→`AppBootstrapper`. | PLAN C2; CLAUDE „Spouštění legacy" |
| Unhandled exception handler | `ApplicationEvents.UnhandledException` | Globální `MLError.ShowError`. | ✅ `DispatcherUnhandledException` + `PopupDisplayer`. | root CLAUDE „App.xaml" |
| Kultura/jazyk z config souboru | `frmNoMDI.New` (`GetLanguageFromFile`) | Nastaví `Thread.Culture` dle configu (fallback cs-CZ). | 🟡 Kultura není konfigurovatelná ze souboru (i18n = #4 Open). | PLAN #4 |
| Splash s textem postupu | `frmSplash.vb` | Okno s `lblStatus` (text startu nastavován zvenčí), bez progress baru. DEMO-splash varianta při demo licenci. | ❌ Bootstrap běží bez splashe. Nízká priorita. | RESP §1 (TBD) |
| Zavírací handler + confirm | `frmNoMDI.frmMDI_FormClosing` | `MESSAGE_PRGEXIT` „Ukončit práci?" confirm → `CloseEdison`. | 🟡 Graceful close (uvolní zámek, persist oken) hotový; confirm prompt při zavření neověřen. | CLAUDE „ACTIVITY zámek" |
| `CloseEdison` — centrální ukončení | `frmNoMDI.CloseEdison` | StopTimer → save settings → `EDUser.Logout` (zámek) → kill standalone modulů → Application.Exit. | ✅ Graceful close (`SessionLogout.Logout`), `CloseAllLegacy()`, `KillOnExit` Job Object i při hard-kill. | PLAN M6; CLAUDE |
| Titulek + verze do statusbaru | `frmNoMDI.frmMDI_MLStatusUpdate` | Titulek `M-line EDISON - <IdFaName> [<IdFa>]`, `Verze: X`. | ✅ `WindowTitle` binding s `[Idfa]`, `Verze: X` ze `VerzeInf`. | PLAN #1 |
| Reaktivace aktivního EDForm | `frmNoMDI.frmMDI_Activated`, `UsableClientSize` | MDI child reaktivace, výpočet plochy minus menu/toolbar. | ⏭️ WPF nemá MDI; layout řeší Grid/DockPanel. | RESP §4 |

---

## 2. Login / autentizace

Legacy: `frmLoginEx.vb` (hlavní dialog), `frmLogin.vb`, `frmSecuredLogin.vb`, `frmChangePassword.vb`, login orchestrace ve `frmNoMDI.DoLogin`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Hlavní login dialog | `frmLoginEx.vb` | Jméno + heslo + combo firem + checkbox „pamatuj" + status label. OK enabled při neprázdných polích, Enter=OK, Esc=Cancel. | 🟡 `LoginWindow` má jméno/heslo/error/OK/Cancel/Enter/Esc. **Chybí combo firem (přesunuto do toolbaru), checkbox „pamatuj".** | `LoginWindow.xaml.cs` |
| Login orchestrace + retry | `frmNoMDI.DoLogin` | Smyčka: dialog → `Login` → naplní MLProps/modMain → při chybě mapuje kódy → při ChangePasswordNeeded cyklus. | ✅ Jádro: `UserAuthenticator.Login` → `ALVA.SYS.Login.Login`. | PLAN #7; CLAUDE „Login" |
| Volání backendu + parsing | `frmLoginEx`/`Login` | `mlp~fullname~admin~dir~changePassword~mayUpdate~startupForm~email~…~username`. | ✅ `UserAuthenticator` parsuje totéž (poziční). | `UserAuthenticator.cs` |
| Login módy EDISON/LDAP/LDAP-AUTO | server feature #23 | Klient mód nelookupuje, heslo nikdy neskrývá — rozhoduje server v `Authenticate()`. | 🟡 Funkčně podporováno (prázdné heslo se neblokuje). Mode-aware skrývání hesla + `GetLoginType` endpoint chybí (customer-driven). | PLAN #7 |
| Auto-login (Windows SSO) | `frmNoMDI` + `EDUsers.Login` | `/reason=2` AND Windows jméno == cílové → jen jméno, prázdné heslo; server dle #23. | ✅ `AppBootstrapper.TryAutoLogin` (gated `TryWindowsAutoLogin`, default off), fallback na dialog. | PLAN #7; CLAUDE „Login" |
| Mapování chybových kódů | `frmNoMDI` (-1…-107) | -1 neexistuje, -10/-11 prázdné jméno/heslo, -101..-108 (ID/Caché/EDISON/čas/deaktivace/autologin/heslo/změna hesla). | 🟡 `GetErrorMessage` mapuje -1, -101..-108. **Chybí -10/-11** (řeší klientská validace jména). | `UserAuthenticator.cs:50` |
| **Změna hesla** | `frmChangePassword.vb` | Staré + nové 2× heslo; `DenyOriginalPassword` skryje staré při vynuceném resetu; cyklus dokud backend vrací changePassword=1 (-108). | ❌ **Dialog neexistuje.** `ChangePasswordNeeded` se parsuje, ale nikdo nečte. Nejvýraznější login díra. | `UserAuthenticator.cs:45` |
| „Pamatuj si mě" (AES) | `frmLoginEx` Encrypt/Decrypt | `user+server+heslo` šifrované Rijndael AES-CBC-PKCS7 do registru; decrypt jen při shodě jména+serveru. | ❌ EDISONMAIN2 pamatuje jen **jméno** (`938cfd7`) a **firmu** (`LastIdfa`), ne heslo. | `LocalPrefs.cs` |
| Prefill posledního jména | `mInput.User` | Login dostane předvyplněné jméno. | 🟡 `LoginWindow(mlRest, initialUsername)` — remember last login name. | `LoginWindow.xaml.cs:14` |
| `frmSecuredLogin` — reconnect dialog | `frmSecuredLogin.vb` | Není login mód — reconnect při `securedMode=0`: předvyplněné jméno, znovu heslo. | ⏭️ Reconnect UX zrušeno (#3 „Nepotřebné"); EDISONMAIN2 (IRIS-only) nepoužívá MLServer secured vrstvu. | PLAN #3; RESP §2.6 |
| Command-line login | `frmNoMDI` `/userName /userPass /reason` | Neinteraktivní login z cmdline (AES heslo), spuštění z jiné app. | ⏭️ Nahrazeno `FormToRunGetter` 10-arg kontraktem; „spustit kopii EDISONu" se neportuje. | PLAN princip #2; RESP §2.7 |
| `frmLogin.vb` (zjednodušená varianta) | `frmLogin.vb` | Minimalistický legacy dialog, dnes nepoužívaný. | ⏭️ `LoginWindow` je ekvivalent. | RESP §2.2 |

---

## 3. Připojení k serveru, setup, výběr databáze

Legacy: `frmConnection.vb`, `frmConnectionDetails.vb`, `frmSetup.vb`, `formEDISONMAINSelectDatabase.vb`, `formEDISONMAINInit.vb`, `formEDISONMAINSettingsDB.vb`, `frmUsersDBSettings.vb`, `frmSelectComputer.vb`, `frmRemotePcList.vb`, `frmSelectPath.vb`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Vytvoření MLRest spojení | `frmNoMDI.OpenConnection` | Vytvoří MLServer (ne IRIS) + MLRest, napojí eventy, načte verzi serveru, detekuje VersionMismatch, naplní MLProps. | ✅ `AppBootstrapper.EstablishSession` (IRIS-only, jediná MLRest instance, napojí eventy). | CLAUDE „App startup" |
| First-launch detekce + setup wizard | `frmSetup.vb`, `EDISONini.xml/initialized` | Chybí config → `frmSetup`; po dokončení zapíše initialized=1 a **app ukončí** (restart). | ✅ `SettingsLoader.Exists()` → `SetupWindow`. **Odchylka:** po uložení in-process re-load (neukončuje). | PLAN #5; `SetupWindow` |
| Wizard — connection část | `frmSetup` (A) | ConnectionName/Server/Port/RestPort + test → uloží do registru. | ✅ `SetupWindow` (server + REST port + test připojení → `appsettings.json`). | `SetupWindow.xaml.cs` |
| Test konektivity | `frmSetup.ConnectionPassed` | `new MLRest(...)` + kontrola RestStatus. | ✅ `DefaultProbe`: `MLRest` + `Database.GetCompanyList`, Save enabled až po úspěšném testu. | `SetupWindow.xaml.cs:33` |
| Wizard — server-init (firma/licence/admin/DB) | `frmSetup` (B) | Výběr DB, `Firma.Write`, `Activation.Add` (licence), `CreateSuperUser` (první admin). | ⏭️ Greenfield server-init — zůstává na legacy `frmSetup.exe` (běží jednou při zakládání serveru, ne per stanice). | PLAN #5 (mimo scope B) |
| Seznam připojení (multi-connection) | `frmConnection.vb` | ListView serverů z registru, default flag, ikona, prompt na uložení. CRUD (Add/Edit/Delete/SetDefault), `DenyEditDefaultConnection` (centrálně řízený default). | ❌ Jen **single** connection v `appsettings.json`. Multi-list + runtime přepnutí = stub **S1 (Open)**. Registry → JSON (vědomě). | PLAN S1 |
| Runtime „Připojit k databázi" | `frmNoMDI.mnuPrgConnect_Click` | `EstablishConnection` → DoLogin → reload. | ❌ Stub `NotYetImplemented` (S1). | PLAN S1 |
| Detail připojení | `frmConnectionDetails.vb` | Editor: Name/Server/Port/RestPort, validace. | 🟡 `SetupWindow` edituje server + REST port (bez Name, jen REST port). | `SetupWindow.xaml.cs` |
| IRIS flag, separátní super-server Port | `frmSetup.SetupParams.IRIS` | Boolean: Caché/IRIS bez MLServeru; separátní Port vs. RestPort. | 🟡 EDISONMAIN2 IRIS-only, jen REST port. IRIS flag/super-server port nejsou. | `SettingsLoader.cs` |
| Kontrola verze serveru | `frmConnection.VersionMismatch` | Před přihlášením kompatibilita klient↔Caché. | ✅ Pokryto Updaterem (runtime gate `EdisonVersion`). | PLAN #1 |
| DB/firma picker (combo) | `formEDISONMAINSelectDatabase.vb` | ComboBox firem z `LoadSettings` (checkAccess), owner-drawn. | ✅ Ekvivalent = toolbar `EDFilterComboBox` (filtr dle IDFa i názvu). | `CompanySwitcher.cs` |
| Nastavení DB↔firma (admin) | `formEDISONMAINSettingsDB.vb` | Add/Edit DB↔firma, nadřízená firma, barevné schéma (ColorDialog). | ⏭️ Admin/server konfigurace — mimo MVP shellu. | RESP §3.1 |
| Uživatelská DB nastavení (admin) | `frmUsersDBSettings.vb` | ListView uživatelů × DB, detekce chybného nastavení, hromadná oprava. | ⏭️ Admin nástroj (patří do USERS agendy). | — |
| Změna čísla verze EDISONu | `formEDISONMAINInit.vb` | Admin: edituje verzi (`yyMMdd.por.patch`) → Caché + VERZE.INF, restart. | ⏭️ Admin/server nástroj. Verze read-only ve status baru. | CLAUDE „Updater" |
| Remote-PC (TeamViewer proxy) | `frmSelectComputer.vb`, `frmRemotePcList.vb` | Výběr vzdáleného PC (`Network.GetComputers`), multi-select, ignorované PC. | ⏭️ Remote-PC scénář není v EDISONMAIN2 (RESP §1 zmiňuje, PLAN neuvádí). | RESP §1 |
| Výběr cesty | `frmSelectPath.vb` | TextBox + FolderBrowser, validace UNC/lokální + zapisovatelnosti. | ⏭️ Obecná utilita (typicky pro moduly), ne core feature. | — |

---

## 4. Session, ACTIVITY zámek, auto-logout

Legacy: `frmNoMDI` (MainTimer, WaitFortimeToClose, EdisonActivityFailed), `modMain` (globals).

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Globální session kontext | `modMain` (Mlp/UserName/Admin/registry cesty) | Zdroj pravdy o uživateli/firmě/registru. | ✅ `EDServices.Current` (singleton: IMLRest, IsAdmin, LicenceLoader, PopupDisplayer, WindowNavigator). | PLAN princip #4 |
| MainTimer 60 s + maSeUkoncit poll | `frmNoMDI.MainTimerProc` | Každých 60 s `Updater.maSeUkoncit`; poll je sám MLRest request → keepalive. ukoncit=1 → countdown → CloseEdison. | ✅ `LogoutWatcher` (DispatcherTimer 60 s). **Divergence:** `CacheLocks=2` během pollu (neobnovovat idle timer). | PLAN #2; CLAUDE „Cooperative logout" |
| Cooperative logout countdown | `frmNoMDI.WaitFortimeToClose` | Tray balloon „za X sekund ukončení", při <5 s zavře. Nelze zrušit/prodloužit. | ✅ `LogoutCountdownWindow` (non-modální top-most odpočet) místo tray balloon. | PLAN #2 |
| Abrupt logout (zmizelý zámek) | `frmNoMDI.EdisonActivityFailed` | Server „Lock exist failed!" → MessageBox → CloseEdison (bez save, bez countdownu). | ✅ `EdisonActivityFailed` event → graceful shutdown. | CLAUDE „ACTIVITY zámek" |
| Uvolnění zámku při logoutu | `EDUser.Logout` (LockDel + business LockClear) | Graceful uvolní ACTIVITY + business zámky zobrazených modulů. | ✅ `SessionLogout.Logout` na klonu MLRest (i pro cizí firmu při switch). | CLAUDE „ACTIVITY zámek" |
| Sdílený MLRest GUID (1 per proces) | `MLRest.cs` GUID | Známý bug: modul s vlastním MLRest → nový GUID → LockExist fail. | ✅ Jeden `IMLRest` přes `EDServices`, GUID se při switch firmy nemění. | RESP §3.7 |
| Klientská idle detekce (Windows) | — (NEEXISTUJE v legacy) | Legacy nesleduje Windows idle; vše server-side. | ⏭️ Záměrně neportovat (legacy to nedělá). | PLAN #2 (mimo scope) |
| Per-user timeout (#30) | feature #30 (bez UI) | Per-uživatel override idle timeoutu — UI nemá ani legacy. | ❌ Editor je feature #6 (Open), váže na #2. | PLAN #6 |
| Ztráta připojení UX | `frmNoMDI.NotifyQueryError`/`RestConectionFail` | `MESSAGE_ConnectionLostEx` + countdown / DB-licence popup. | ❌ Konkrétní ConnectionLost/RestConectionFail popup neportován (eventy napojeny). | RESP §3 |
| Neukončená transakce (TLevel) | `frmNoMDI.NotifyQueryTLevelWarning` | Detekce otevřené Caché transakce → MESSAGE_QueryTLevelError → CloseEdison. | ❌ Transaction-level safeguard neportován. | RESP §16 |

---

## 5. Navigace — stromy, NavBar, spouštění modulů, plocha, oblíbené

Legacy: `frmMain.vb` (~2948 ř.), `EDDesktop.vb`, `EDDockingPanel.vb`, navigace/launch ve `frmNoMDI`.

### 5.1 Navigační stromy a filtrace

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| 5 sekcí stromu | `frmMain.CreateNavBar` | Oblíbené(-1) / Hlavní nabídka(-2) / Správa systému(-3) / Partnerská řešení(-4) / skrytý Systém(-101). | 🟡 Záložky: **Hlavní nabídka / Správa systému / Partnerská řešení + Oblíbené (★) + Používané** (poslední dvě nad rámec legacy). Skrytý Systém(-101) není. WPF TreeView, ne C1NavBar. | PLAN H1, S7 |
| Zdroj + úplnost stromu | `frmMain.LoadTreeFromXML` | XML (`edisonnavbar.xml` + `edisontree.xml`), větvení dle verze licence. | ✅ Přesunuto do Caché `Tree.cls` (wire FAMILY/EXE/FAMILY_END), `ModuleTreeGetter`. Strom je **plně zmaterializovaný** (~520 EXE / 98 rodin, všechny s exe+FQN+elementId), ne stuby. Caché-authored (ne auto-migrace z XML). | PLAN „dva zdroje stromu"; `Tree.cls` |
| 4-vrstvá filtrace uzlů | `frmMain.BuildTreeXML` | (1) licence (whitelist ≥1.003 / blacklist <1.003), (2) hidden, (2b) mlineOnly, (3) access table, (4) admin fallback. | 🟡 3 vrstvy (licence + hidden + AccessTable) + admin fallback. Blacklist (<1.003) a mlineOnly vědomě vynechány. | PLAN S8, C3; RESP §5.2 |
| Admin fallback (anti-lockout) | `frmMain.AddAdminRights` | Admin vždy dostane SettingsShow + (s licencí) USERS/TMList — doplňuje jen accessTable, ne licenseRights. | ✅ `AccessLoader` (GetEdisonAccess + admin fallback). | PLAN C3 |
| Licenční práva z licence | `frmMain.GetLicenseRights` | Z `license.Elements` postaví whitelist/blacklist. | ✅ `LicenceService` (wrapper EDLicence.dll) + `ExecutableModule.IsLicensed/ElementId`. | CLAUDE „Licence" |
| Vyhledávání ve stromu | `frmMain` `txtSearch` + `HyperTreeView.Filter` | Textové pole „Hledat formulář…", filtr uzlů (text contains), expand při filtru, šipka/Enter → fokus do stromu. | ✅ Search box nad TabControlem (`TreeSearchText`) filtruje **všechny** stromy: uzel viditelný když název matchne NEBO má viditelného potomka; nalezená rodina se rozbalí. | `EdisonMainViewModel.ApplySearchToModule`, `EdisonMainWindow.xaml` |
| Hyperlink hover styl uzlů | `frmMain.HyperTreeView` | Hover → podtržení + modrá + kurzor Hand. | ❌ Chybí (WPF má vlastní hover/selection). | — |
| Barvy uzlů stromu | `frmMain.BuildTreeXML` color | XML `color="R,G,B"` → ForeColor. | ✅ `Tree.cls` `color="#RRGGBB"` + `HexToBrushConverter`; `#FFA500` = stub. | PLAN S9 |

### 5.2 Spouštění modulů a běžící okna

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Typy uzlů (0/1/4) | `frmMain` TreeObjectEnum | 0=skupina, 1=CSP/web link, 4=VB formulář. | 🟡 FAMILY (skupina) + EXE (modul). **Type 1 (CSP/web link) chybí.** | `ExecutableModule.cs` |
| Spuštění modulu | `frmNoMDI.mFrmMain_ShowFormDemand` | Načte assembly, changelog, duplikát check, dialog vs. okno toggle. | ✅ `IWindowNavigator.ShowLegacy(exe, fqn)` (10-arg payload, sdílí GUID). | CLAUDE „Spouštění legacy" |
| Menu „Spuštěná okna" | `frmNoMDI.ObnovDropdawn…` + `BringProcessToFront` | Menu běžících modulů, klik → SetForegroundWindow, mrtvý proces relaunch. | ✅ S5: `WindowNavigator.GetActiveModules()` (legacy procesy + WPF okna), aktivace restore/Focus/SetForegroundWindow. | PLAN S5 |
| Kill modulu | `frmNoMDI.KillStandAloneProcess` | `p.Kill()` + WaitForExit(5 s). | ✅ `CloseAllLegacy()` + `KillOnExit` Job Object. | PLAN M6 |
| Dialog vs. modeless toggle | `frmNoMDI.mnuCheckVolatoknaJakoDialogy` | Uživatelská volba hostovaná okna modální/modeless. | ❌ `ShowWindowsAsDialogs` toggle persistuje, **bez consumera** (S3). | PLAN S3 |
| Skryté zkratky (Ctrl+W+W) | `frmMain.tvw_KeyDown` | Chord → `formUTILITIESMain`. | ✅ S6: chord v `PreviewKeyDown`, `IsChordWaiting`/`ChordHintText`, spustí `UTILITIES.exe`. + Ctrl+Shift+B. | PLAN S6 |
| Refresh stromu po změně nastavení | `frmNoMDI.NotifyDataSent` | Po zavření SettingsShow/USERSMain refresh stromu + company menu. | ❌ Callback z hostovaných oken zpět do shellu neportován. | RESP §3 |

### 5.3 Oblíbené, startup formulář, plocha (desktop)

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Oblíbené (přidat/odebrat/strom) | `frmMain.CreateFavouriteItems`/`AddFavouriteItem`/`DeleteFavouriteItem` | Insert/Delete klávesy + context menu; strom oblíbených z nodeID + rodičů + potomků. | 🟡 PLAN „favorites partly done". Sekce Oblíbené v `Tree.cls` chybí; per-user dynamický strom (-1) v shellu neimplementován. | PLAN; modMain |
| Notifikace přidání (blikání) | `frmMain.NotifyFavouriteItemAdded` | Timer problikne ikonu panelu Oblíbené. | ⏭️ Kosmetika. | — |
| Startup formulář (nastavit/zrušit) | `frmMain.SetStartupForm`/`MarkStartupForm` | Označí uzel jako startovací (ikona), uloží `SetUserDefaultForm`. | ❌ Žádné nastavení startovacího modulu. | modMain `StartupForm` |
| Auto-otevření startup formuláře | `frmMain.RunStartupForm` | Po loginu otevře uživatelovo domovské okno. | ❌ Chybí. | — |
| Plocha se zástupci (EDDesktop) | `EDDesktop.vb` | ListView plocha, vlastní OnPaint ikon, `DesktopShortcut` struct. | ❌ EDISONMAIN2 nemá plochu se zástupci. | RESP §6 |
| Vytvořit/odebrat zástupce | `frmMain.CreateShortcut`/`RemoveShortcut`, `formEDISONMAINShortcutList` | Přidání/odebrání zástupce na plochu (check-list typů). | ❌ Chybí. | — |
| Drag-drop ze stromu na plochu | `EDDesktop.OnDragDrop` + `tvw_ItemDrag` | Tažení form uzlu → zástupce na pozici dropu; přesun v rámci plochy. | ❌ Chybí. | — |
| Spuštění/smazání z plochy | `EDDesktop` DoubleClick/Enter/Delete | Double-click → ShowFormDemand; Delete → odebrat. | ❌ Chybí. | — |
| Uspořádat ikony / snap-to-grid | `frmMain.mnuArrangeIcons`, `EDDesktop.OnSizeChanged` | Context menu „Uspořádat ikony", re-arrange při resize. | ❌ Chybí. | — |
| Persist zástupců | `frmMain.Load/SaveUserSettings` | Filtruje dle licence+access, obnovuje pozice/výběr. | ❌ Chybí. | modMain `DesktopShortcuts` |
| WebInfo / news ticker | `EDDesktop.DrawWebInfoData`, `LoadInformation` | „Informace M-line" + „Tipy" (střídání 10 s), odkazy, daňový kalendář, notifikace verze/licence ze síťového `WebInfo.txt`. | ⏭️/🟡 News ticker neportován; částečně nahrazeno DashPanel widgety + S4 INFO label. | PLAN S4 |
| DashPanel web widgety (info/wiki/školení) | `frmMain.InitializeForm` | 3 webové dashboard panely (m-line podpora) + InfoPanel s licenčním/verzním varováním. | ❌ Web dashboard widgety chybí; varování částečně v status baru. | PLAN #1 |

### 5.4 Persistence stavu navigace

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Šířka stromu | `UserSettings.TreeWidth` | `pnlTree.Width` per uživatel. | ❌ Není jako explicitní persisted preference. | modMain |
| Rozbalené/vybrané uzly | `TreeExpandedNodes`/`TreeSelectedNodes` | Rekurzivně sebere/obnoví rozbalené + vybrané uzly. | 🟡 PLAN „expansion persistence partly done". | PLAN; modMain |
| Per-firma barevné schéma shellu | `frmMain.Scheme`, `frmNoMDI.RefreshMainformScheme` | Gradient pozadí z hex schématu firmy (`…"Scheme"` global). | ❌ **Shell se nepřebarvuje dle firmy** (barvy modulů ≠ shell scheme). | RESP §3.1 |
| `UserSettinsStruct` (zdroj pravdy) | `modMain` | TreeWidth, expanded/selected, NavBar, DesktopShortcuts, MiniApps, FavouriteItems, StartupForm. | 🟡 Rozpadlé do `LocalPrefs` (LastIdfa, layouty) + `WindowStatePersistence`. Většina polí bez ekvivalentu. | CLAUDE „Persistence stavu" |

---

## 6. Mini-aplikace

Legacy: `EDMiniApplication.vb` (~1713 ř.), `clsMiniApplication.vb`, `frmMiniAppList.vb`, Caché `MiniAplikaceAPI.cls`.
EDISONMAIN2: `EDISONMAIN2.LegacyMiniApps` (generická dashboard mini-app + 15 descriptorů), `MiniAppManager`, AvalonDock, `PeriodicRefreshMiniAppViewModel`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Datový kontrakt `LoadData(appType, refresh)` | `EDMiniApplication.LoadData` | Generický: labely + tabulky (sloupce/řádky/styly), časové razítko. | ✅ `DashboardData.Parse` (labely, tabulky, barvy, timestamp). | `DashboardData.cs` |
| 15 typů (52001–52015) + titulky | `ApplicationTypeEnum`, `GetApplicationName` | Fluktuace, lék. prohlídky, školení, údržba/kalibrace/motohodiny vozidel, aktuální období, statistiky faktur/objednávek, sklady MTZ… | ✅ `LegacyDashboardMiniAppProvider` (15 ID + názvy). | `LegacyDashboardMiniAppProvider.cs` |
| Hromadné načtení více typů 1 voláním | `clsMiniApplication.LoadData` (batch) | refresh + N×appType jedním requestem. | 🟡 Per-instance jeden appType (server umí oba; ztráta 1 volání pro N panelů). | `LegacyDashboardMiniAppViewModel.cs` |
| Vykreslení labelů + tabulek (dyn. sloupce) | `EDMiniApplication.ShowData` | ListView se sloupci za běhu. | ✅ `DataTemplate` (TextBlock) + `DashboardTableColumns` attached property (runtime DataGrid sloupce). | `LegacyDashboardMiniAppView.xaml` |
| Barva textu řádku | `ShowData` ForeColor | `ColorTranslator.FromHtml` → ForeColor řádku. | ✅ `DataGridRow.Foreground` přes `HexToBrushConverter`. | `LegacyDashboardMiniAppView.xaml` |
| **Ikony řádků (OK/Warning/Error + per-typ)** | `ShowData` ImageKey | Styl řádku nastaví ikonu stavu / typu (user/vozidlo/…). | ❌ `IconType` se **parsuje, ale View nerenderuje** žádné ikony. | `DashboardData.cs:14` |
| **Číselné formátování buněk (`N2#`)** | `ShowData` GetFormat | `N2#` → `#,##0.00` + pravé zarovnání. | ❌ Vše jako string; datový typ sloupce se ani neparsuje. | `EDMiniApplication.vb:217` |
| **Třídění klikem na hlavičku (typové)** | `EDMiniApplication.ListViewSorter` | Číselné/datumové porovnání, netříditelné sloupce, ikony směru. | ❌ DataGrid má default WPF sort (string), bez typového porovnání / „netříditelný". | `EDMiniApplication.vb:1778` |
| **Tisk miniaplikace** | `EDMiniApplication.PrintData` | Tisk tabulky přes EDListView. | ❌ Úplně chybí. | `EDMiniApplication.vb:950` |
| Manuální refresh | `RefreshData` (tlačítko v titulku + confirm) | Refresh ikona → dotaz → LoadData. | 🟡 Tlačítko „Načíst" v těle (ne titulek), bez confirm dialogu. | `LegacyDashboardMiniAppViewModel.cs` |
| Hromadný refresh všech | `clsMiniApplication.LoadData` (all) | Načte data všech otevřených. | ✅ `MiniAppManager.RefreshAll` (vrací názvy obnovených). | `MiniAppManager.cs` |
| **Periodický auto-refresh** | — (v legacy NENÍ) | — | ✅ **Nová feature** `PeriodicRefreshMiniAppViewModel` (10 s–60 min, on/off, „Obnovit teď"). | `CoreMiniApps.cs` |
| Dockovatelný panel (pin/order/width) | `EDDockingPanel` + `clsMiniApplication` | Vertikální stack se splittery, persist stav/šířka. | ✅ AvalonDock `DockingManager` (tabs/float/auto-hide). | MINIAPPS §4 |
| Plovoucí okno / multi-instance | — (legacy: max 1 per typ, ne float) | — | ✅ (lepší) `ContentId="{Id}#{n}"` + `LayoutFloatingWindowControl`. | `MiniAppManager.cs` |
| Picker dialog (add/remove/order) | `frmMiniAppList.vb` | ListView typů, checkboxy, MoveUp/Down. | 🟡 Nahrazeno menu „Miniaplikace" (po jednom); pořadí drag v docku. | MINIAPPS §10 |
| Zavření miniapky (+ confirm) | `clsMiniApplication.Remove` | Křížek → MESSAGE_CloseQuestion → odebrání. | 🟡 Křížek panelu → `OnClosed`; bez confirm dialogu. | `MiniAppManager.cs` |
| Persistence otevřených + layout | `UserSettings.MiniApplications` | Per-user server-side sada + pořadí + šířka. | ✅ `LocalPrefs.MiniAppLayouts` (JSON per IDFa) + AvalonDock `XmlLayoutSerializer`. | MINIAPPS §7 |
| Sdílení MLRest/session | `MainForm.MLRest` | In-process stejná session. | ✅ Mini-app dědí `EDViewModel` → sdílí shell MLRest. | MINIAPPS §2 |
| **Licenční gating (element 15, 52001-52999)** | `frmNoMDI` accessTable | Sada licencovaná zvlášť. | ❌ Odloženo — gating podle rodičovského modulu, katalog statický (nefiltruje se per firma). | MINIAPPS §6,§10 |
| **Funkční omezení (FO/UseFO)** | `MiniAplikaceAPI` input("FO") | Server filtruje data per-user dle funkčního omezení. | ❌ Klient FO neposílá → data za všechny (možný funkční rozdíl). | `MiniAplikaceAPI.cls:247` |

---

## 7. Administrace, uživatelé, práva, nastavení, úlohy, licence

Legacy: `formEDISONMAINSettingsShow.vb` (~1828 ř.), `formEDISONMAINAccessTree.vb` (~1196 ř.), `formEDISONMAINUserEdit.vb`, `formEDISONMAINSettingsApplication.vb`, `frmIdFaApplicationSettings.vb`, `formEDISONMAINTaskList.vb`, `formEDISONMAINSystemTaskEdit.vb`, `formEDISONMAINShortcutList.vb`, `frmAddLicense.vb`.

> **Souhrn:** drtivá většina admin UI z této oblasti **není a ani nemá být** v EDISONMAIN2 pro MVP.
> Server-init (firma/DB/licence/admin) zůstává na legacy `frmSetup`; správa uživatelů/práv/certifikátů
> na legacy `USERS.exe`. EDISONMAIN2 z této oblasti reálně dělá: runtime licenční **filtraci** stromu,
> kick-and-login (spawn `USERS.exe`), background tasks vstup (Ctrl+Shift+B), connection-only setup.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Globální nastavení (multi-tab) | `formEDISONMAINSettingsShow.vb` | Tabs: Databáze / Aplikace / Licence / Login / Update / WebService / SAP / log / export. CRUD přes `Main.LoadSettings`/`SaveSettings`. | ⏭️ Celý dialog mimo MVP / delegován legacy (server-init). | PLAN #5; RESP §9 |
| → Tab Login (auto-login/pamatovat) | `chkUseAutoLogin`/`chkRememberUser` | Toggle delegated auth + pamatovat. | 🟡 Auto-login funguje (gated), bez admin UI toggle. | PLAN #7 |
| → Globální idle timeout | `chkExitSystemOnTimeout`/`numExitTimeout` | Enabled + minuty (1-1440). | 🟡 Klientská strana hotová (`LogoutWatcher`), admin UI pro timeout chybí. | PLAN #2 |
| → Tab Update konfigurace | `chkUpdateEnabled`/cesty | Server/port/cesty updatu. | 🟡 Klientský updater hotový, konfigurační UI chybí (čte se ze serveru). | PLAN #1 |
| Editor přístupových práv (ECL) | `formEDISONMAINAccessTree.vb` | Hierarchický editor práv per-formulář (Číst/Zapisovat/Měnit/Úplné řízení, tri-state, kaskáda), export CSV. | ⏭️ **Editor není a není plánován pro MVP** — delegováno na legacy. | RESP §7 |
| Runtime kontrola práv + filtrace stromu | (ECL aplikované v `BuildTreeXML`) | ECL uživatele filtruje strom. | ✅ `AccessLoader` + `EdisonMainViewModel.IsAllowed`/`TryFilter` (3 vrstvy). | PLAN S8 |
| Editor uživatele (self-edit) | `formEDISONMAINUserEdit.vb` | Změna hesla, startup form, osobní export dir, certifikáty, seznam zákazníků. | ❌ Není (změna hesla chybí; startup form chybí; certifikáty/zákazníci delegováno). | PLAN #6, #7 |
| Feature flagy uživatele (#14-17,22,23,30) | (USERS.exe / Caché) | enabled, časové okno, vynucená změna hesla, hash, login mód, per-user timeout. | ⏭️ Editace přes legacy `USERS.exe`. #30 UI = feature #6 (Open). Server vyhodnocuje při loginu. | PLAN #6, #7 |
| Licence — seznam/detail | `SettingsShow.LoadLicense`, `formLICENCEINFOMain` | ListView licencí, detaily. | 🟡 `LicenceService` interně, bez UI seznamu. Licenční nástroje čtou legacy XML strom (post-MVP reconnect). | PLAN „dva zdroje" |
| Licence — přidat/import/refresh | `frmAddLicense.vb`, `RefreshLicense` | Base64 klíč → `Activation.Add` (3DES); refresh z M-line cloudu. | ❌ Import = server-init (legacy); refresh menu akce chybí. | PLAN #5; RESP §13 |
| Naplánované systémové úlohy | `formEDISONMAINSystemTaskEdit.vb` | Toggle auto úloh (transfer, update, licence, feedback, info) + notif. maily. | ❌ Editor chybí (některé akce řeší updater). Mimo MVP. | RESP §9 |
| Seznam úloh na pozadí (viewer) | `formEDISONMAINTaskList.vb` | Grid background tasks (refresh/delete/output/notes), Ctrl+Shift+B. | 🟡 Vstup hotový (S6), ale otevírá legacy okno; WPF viewer nepřepsán. | PLAN S6 |
| Desktop shortcuts (check-list) | `formEDISONMAINShortcutList.vb` | Výběr zástupců na plochu. | ❌ Plocha + oblíbené (-1) mimo MVP. | PLAN S7 |
| Per-firma/aplikace nastavení | `formEDISONMAINSettingsApplication.vb`, `frmIdFaApplicationSettings.vb` | Která aplikace běží v které DB s jakým zdrojem (per firma override). | ⏭️ Server-init, mimo MVP. | RESP §9 |
| Kick-and-login (admin over-license) | `frmNoMDI` → `formUSERSActivity` | Info seznam aktivních sessions (kick prakticky nefunkční). | 🟡 `LicenceVerifier` spawne legacy `USERS.exe formUSERSActivity`; WPF dialog = feature #8 (Open). | PLAN #8 |

---

## 8. Lokalizace

Legacy: `formEDISONMAINLanguageList.vb` (~704 ř.), `formEDISONMAINLanguageImport.vb`, `frmLanguageItemEdit.vb`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Správce jazyků / slovník | `formEDISONMAINLanguageList.vb` | Combo jazyků (`getJazyky`) + grid překladů (Text/Překlad/Původ), Add/Edit/Delete položek, import/export XML, smazat jazyk, výběr UI jazyka uživatele. cs_CZ = zdrojový (nelze mazat). | 🟡 **Bridge** — `ShowLanguageSettingsCommand` spouští legacy `formEDISONMAINLanguageList` jako dialog. Nativní WPF i18n = feature #4 (Open). | PLAN #4 |
| Editace položky překladu | `frmLanguageItemEdit.vb` | Text + Překlad dialog (Add/Edit). | 🟡 Uvnitř bridged dialogu; nativní = součást #4. | PLAN #4 |
| Import z Excelu | `formEDISONMAINLanguageImport.vb` | Import z .xls/.xlsx přes C1FlexGrid. (V legacy ani nepropojeno s language-list — XML import jde přímo.) | ❌ Neportováno; izolovaná polo-mrtvá cesta. | PLAN #4 |

---

## 9. Klientský update a verze

Legacy: `clsUpdate.vb` (~985 ř.), `frmUpdateDialog.vb`, `frmVersionDetails.vb`, `frmInformation.vb`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Update engine (detekce + spawn) | `clsUpdate.vb` | Verze z `VERZE.INF` (`yyMMdd.N.P`), typy Full/Patch/XMLPatch, server vs. klient větev, rozhodovací strom `UpdateNeeded`, spawn EDTRANSFER2/EDISONINS (server) nebo **EDUPDATE.EXE** (klient), self-update updateru. | ✅ Klientská větev: `Updater` + `VerzeInf`, `AppBootstrapper.CheckForUpdate` → spawn upraveného `EDUPDATE.EXE` (`WaitProcess`/`RunExe`) → shutdown. Mandatory-only. | PLAN #1; UPDATE_FLOW.md |
| Runtime version gate | `clsUpdate` + `MLRest.cs` | `EdisonVersion` v každém requestu, server porovná s `^EDISON("System","Update",2)` → mismatch → exit. | ✅ `MLRest.EdisonVersion` z `VERZE.INF` → `EdisonVersionFailed` → graceful shutdown. | PLAN #1 |
| Update dialog (3 režimy) | `frmUpdateDialog.vb` | Information/Question/ForceYes (Ne disabled), detaily, „příště neupozorňovat". | 🟡 Zjednodušeno na mandatory info-popup (klient stejně neměl volbu). `ignoreNotify` mimo scope. | PLAN #1 |
| Detail verze / changelog | `frmVersionDetails.vb` | Read-only popis verze (`POPIS_VERZE.INF`). | ⏭️ Mimo scope (jen `Verze: X` ve status baru). | PLAN #1 |
| Informace o aplikaci / changelog modulů | `frmInformation.vb` | Strom modulů + info/změny per modul z lokálních changelog souborů. | ❌/⏭️ Neportováno; nahrazeno status-bar verzí. | RESP §15 |

---

## 10. Logy

Legacy: `formEDISONMAINLogList.vb` (~491 ř.).

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Prohlížeč/odesílač logů | `formEDISONMAINLogList.vb` | Grid modulů (LOG_* soubory), výběr + datum, zazipuje (7za.exe) a odešle e-mailem per modul. | 🟡 **Bridge** — `SendSystemLogCommand` spouští legacy okno. Závisí na lokálních souborech + 7za.exe (jen v `C:\Edison\`). | RESP §9, §15 |

---

## 11. Status bar, info lišta, tray, font, sezónní, misc

Legacy: `frmNoMDI` (status bar, tray), `frmW8*.vb` (sezónní), `frmC1.vb`.

| Feature | Legacy | Co dělá | Stav | Ref |
|---|---|---|---|---|
| Status bar — připojení (DB/server/GUID) | `frmNoMDI.ShowConnectionStatus` | „Připojení: name/db", tooltip server/port/GUID. | 🟡 Status bar existuje, connection/GUID detail neověřen. | PLAN #1 |
| Status bar — licence countdown | `frmNoMDI.ShowLicenseStatus` | „Licence: Type ValidityTo (Counts)". | 🟡 Licence se čte, countdown zobrazení neověřeno. | CLAUDE „Licence" |
| Status bar — verze | `frmNoMDI.slbVersion` | `Verze: X`. | ✅ `Verze: X` ze `VerzeInf` (DEBUG=999999.9). | PLAN #1 |
| INFO lišta — datum + svátek | `frmNoMDI.RefreshInformation` | „Dnes je dd.MM.yyyy, svátek má X". | ✅ S4: `BuildInformationText` (statický `CzechNamedays`), bez Caché. | PLAN S4 |
| News ticker / WebInfo | `EDDesktop.DrawWebInfoData` | Novinky ze síťového `WebInfo.txt` + notifikace licence. | 🟡 Verze hotová; news ticker neportován. | PLAN #1 |
| Tray ikona + balloon | `frmNoMDI.ntfi` | Balloon při force-logout countdownu; většina TRAY integrace zakomentována. | ⏭️ Countdown nahrazen `LogoutCountdownWindow` (WPF); TRAY v legacy mrtvé. | PLAN #2 |
| Font management | `frmNoMDI.tsmiFont_Click`/`SetMlineFont` | FontDialog → registry → aplikace na UI. | ✅ `FontService`, `font.json` (sdílený s TemplateDesigner), aplikace i na Menu/StatusBar. | PLAN M5 |
| Reset pozic oken | `frmNoMDI.ToolResetForms` | `Form.ResetSettings`. | ❌ Persistence oken existuje (`WindowStatePersistence`), reset command ne. | CLAUDE „Persistence" |
| Sezónní uvítací okna | `frmW8.vb`/`Xmas`/`Easter`/`NewYear` | GDI+ animace autobusu (sníh/vajíčka/ohňostroj) dle data; Easter computus. | ⏭️ TBD, nekritické (uživatelsky milé). `EDActionInProgress` řeší „čekejte". | RESP §4 |
| TeamViewer / RunEdisonCopy / nápověda / helpdesk | `frmNoMDI` Tools menu | Remote support, druhá instance, .htm nápověda (iexplore), helpdesk (už deaktivováno). | ⏭️ Neportovat (support tooly, mrtvé / nízká priorita). | RESP §15 |
| Debug menu (~15 nástrojů) | `frmNoMDI.mnuDebug` | Vývojářské tooly (jen IsDomainMline). | ⏭️ EDISONMAIN2 má vlastní `ED*TestWindow` dema. | CLAUDE |
| `frmC1.vb` | `frmC1.vb` | Prázdná třída (2 ř.) — dead relikt. | ⏭️ N/A. | — |
| Error handling | `MLErrorHelper` / `ShowError` | Globální + per-form error display. | ✅ `PopupDisplayer` + auto error handling v `Command(...)`. | RESP §14 |

---

## Gap checklist — co dodělat pro plnou náhradu

Prioritizovaný seznam toho, co reálně chybí (❌) nebo je jen částečné (🟡) a má hodnotu.
Položky ⏭️ (vědomě neportovat) zde nejsou — viz Executive summary.

### Vysoká hodnota / pravděpodobně potřeba před plnou náhradou

1. **Změna hesla** (`frmChangePassword` ekvivalent) — vynucený reset (-108) i dobrovolná změna. Login dnes flag ignoruje. *(§2)*
2. **Runtime přepnutí připojení + multi-connection list** — S1 stub `NotYetImplemented`. *(§3)*
3. **Startup formulář** — nastavit „domovský" modul + auto-otevření po loginu. *(§5.3)*
4. **Mini-app rendering parita** — ikony řádků (parsují se, nerenderují), číselné formátování + pravé zarovnání, typové třídění. *(§6)*
5. **Per-firma barevné schéma shellu** (`RefreshMainformScheme`). *(§5.4)*
6. **Lokalizační infrastruktura** (feature #4) — dnes bridge na legacy. *(§8)*

### Střední hodnota

8. **Plocha se zástupci** (EDDesktop) — drag-drop, persist, uspořádat. Velká featura — ověřit, jestli ji uživatelé reálně používají, nebo stačí strom + oblíbené. *(§5.3)*
9. **Oblíbené (-1)** — per-user dynamický strom v shellu (PLAN „partly done"). *(§5.3)*
10. **Persistence navigace** — tree expanded/selected/width per uživatel. *(§5.4)*
11. **Dialog vs. modeless toggle** (`ShowWindowsAsDialogs`) — má consumera dodat. *(§5.2)*
12. **Mini-app: licenční gating (element 15) + per-firma re-filtr katalogu**. *(§6)*
13. **Mini-app: funkční omezení (FO/UseFO)** — klient neposílá → data za všechny. *(§6)*
14. **Background tasks WPF viewer** (dnes bridge legacy). *(§7)*
15. **Per-user timeout editor** (feature #6) + admin UI globálního timeoutu. *(§7)*
16. **Kick-and-login WPF dialog** (feature #8) — dnes spawn legacy `USERS.exe`. *(§7)*

### Nízká hodnota / edge

17. **Remember-me s heslem** (AES) — dnes jen jméno+firma. *(§2)*
18. **ConnectionLost / RestConectionFail / TLevel transaction** UX. *(§4)*
19. **Refresh stromu po změně nastavení** (`NotifyDataSent` callback). *(§5.2)*
20. **Splash screen**, **news ticker (WebInfo)**, **changelog viewer** (`frmInformation`/`frmVersionDetails`). *(§1, §9, §11)*
21. **Type 1 uzel (CSP/web link)** ve stromu. *(§5.2)*
22. **Reset pozic oken** command. *(§11)*
23. **Login error kódy -10/-11** (prázdné jméno/heslo) mapování. *(§2)*

### Vědomě delegováno na legacy (ne mezera, ale součást „plné náhrady" až po MVP)

- **Server-init** (firma/DB/licence/admin) — legacy `frmSetup`. *(§3)*
- **Editor práv (ECL)**, **plný editor uživatelů + feature flagy + certifikáty**, **správa nastavení DB/aplikace**, **import/refresh licence**, **systémové úlohy editor** — legacy `USERS.exe` / `SettingsShow`. *(§7)*
- **Licenční nástroje** (Licence/LicenceInfo) čtou legacy `edisontree.xml` — reconnect na Caché strom post-MVP. *(§7, PLAN „dva zdroje stromu")*

---

*Příloha — pokrytí podle legacy souborů: viz jednotlivé sekce výše. Soubory bez vlastní funkce
(`frmC1.vb`) nebo čistě designer (`*.designer.vb`) nejsou katalogizovány.*
