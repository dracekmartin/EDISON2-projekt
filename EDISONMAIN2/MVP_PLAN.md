# EDISONMAIN2 MVP — pilot-ready plán

## Context

EDISONMAIN2 (C# / WPF) v budoucnu nahradí starou `EDISONMAIN` (VB.NET WinForms).
Cíl tohoto plánu je dovést EDISONMAIN2 do **pilot-ready** stavu — klient, který lze
bezpečně nasadit paralelně vedle stávajícího EDISONMAIN proti stejné Caché
instanci. Hostované moduly (JR, MZDY, …) zůstávají jako legacy `.exe` pouštěné
přes `IWindowNavigator.ShowLegacy` + `EDISONMAIN2API.FormToRunGetter`.

## Definition of Done

Kritéria pro pilot-ready (z [RESPONSIBILITIES.md § 18.3](RESPONSIBILITIES.md)):

1. Oba klienti se přihlásí do stejné Caché instance bez chyby `"EdisonVersionFailed!"`.
2. ACTIVITY zámky obou klientů se v `^||ML(...)` neprolínají; admin vidí obě
   sessiony a umí je individuálně odhlásit (přes existující legacy `USERS.exe formUSERSActivity`).
3. Business zámek vytvořený v EDISONMAIN2 uvidí uživatel v EDISONMAIN a naopak.
4. Update mechanismus — admin může vydat update bez rozbíjení sessionů druhého klienta.
5. Licence funguje pro oba klienty (EDISONMAIN2 dostane stejnou licenci).
6. Legacy `UNDONE: 2021-07-07` v `formUSERSActivity.cls` ověřen nebo zdokumentován.

---

## Hotové (status)

- Login → `LoginWindow` + `UserAuthenticator` (`ALVA.SYS.Login.Login`).
- ACTIVITY lock vytvořen `VerifyLic` při loginu (`LicenceVerifier.TryAcquireSession`).
- `EdisonActivityFailed` event → graceful shutdown.
- Shell ↔ moduly sdílí GUID + `CacheLocks=1` přes `EDISONMAIN2API.FormToRunGetter`.
- IdFa switching s re-VerifyLic per cílová firma.
- Licence loading (`LicenceService` přes `EDLicence.dll`) + module tree filter.
- Admin override při over-license (Confirm → spawn legacy `USERS.exe formUSERSActivity`
  → retry, jednotná logika pro login i company switch).
- `LocalPrefs` — JSON v `%APPDATA%\m-line\EDISONMAIN2\local.json`, pamatuje
  poslední IDFa per Windows-user.
- `WindowStatePersistence` — automatická persistence WPF window state
  (pozice, velikost, Maximized) přes legacy `ALVA.LIBRARY.Form.SaveParams`,
  per MLP × IDFa, FormID odvozený z názvu VM.
- USERS.exe retrofit: nový `Program.vb` s `Sub Main` → `FormToRunGetter` pattern.
- 199 testů (165 EDControls + 34 EDISONMAIN2).

---

## Zbývá pro pilot

### 1. Business lock smoke test (DoD 3)

**Cíl:** verifikace, že business zámky se sdílí mezi EDISONMAIN a EDISONMAIN2.

Žádná implementace — sdílený `MLRest.dll` + stejný volací pattern to zajišťuje
implicitně. Cíl je test scénář:

1. EDISONMAIN otevře edit záznamu X → business lock v Caché.
2. EDISONMAIN2 pokusí o edit téhož → musí dostat „Záznam je zamčený uživatelem X".
3. Opačně: EDISONMAIN2 první → EDISONMAIN druhé.

**Provedení:** ruční test na pilotní stanici. Záznam v `DOD_TESTS.md`.

### 2. Horní menu — postupná funkční implementace stub položek

**Stav:** struktura menu replikuje legacy `frmNoMDI.mnuMain` (EDISON / Firma /
Uživatel / Helpdesk / Spuštěné moduly / Nástroje / Nápověda / Styl / Test funkcí
/ INFO) v přesném pořadí. Většina položek je stub (`NotYetImplemented` popup).

**Funkční dnes:** Konec, Spustit další EDISON, Možnosti (3 toggles — persist),
Načítat nástěnky (live efekt na WebView2), Obnovit výchozí nastavení oken, Správa
zámků, Aktivita uživatelů, Aktualizovat licenci, Informace o verzi, Helpdesk Wiki,
Zvolený font, Použít font, plus combobox firem (replikuje legacy "Firma:" label).

**INFO** (right-aligned placeholder vpravo v menu): v legacy je to status label
plněný serverem přes `ALVA.EDISONAPI.InformationArea` — zobrazuje "Dnes je
dd.MM.yyyy, svátek má X" (NameDay). Není klikací. V EDISONMAIN2 je zatím
`IsEnabled=False` placeholder; pro pilot doplnit naplnění z téhož endpointu
(low priority — kosmetická feature).

**Stub položky** (poppou „Tato funkce zatím není v EDISONu 2 dostupná"):

| Položka | Cíl pro pilot |
| --- | --- |
| EDISON → Připojit k databázi | Otevřít vlastní WPF dialog pro výběr/přepnutí connection. |
| Uživatel → Přihlásit jiného uživatele | Otevřít LoginWindow s reconnect logic (závisí na novém endpointu nebo nepilot.) |
| Uživatel → Aktuální uživatel | `formEDISONMAINUserEdit` — žije v legacy `EDISONMAIN.exe`. Retrofit EDISONMAIN.exe stejně jako USERS.exe (`Program.vb` + FormToRunGetter) → spustit přes `WindowNavigator.ShowLegacy`. |
| Uživatel → Úlohy na pozadí | `formEDISONMAINTaskList` — totéž (sdílí retrofit EDISONMAIN.exe). |
| Nástroje → Nastavení jazyka | `formEDISONMAINLanguageList` — totéž (sdílí retrofit EDISONMAIN.exe). |
| Nástroje → Odeslat systémové soubory | `formEDISONMAINLogList` — totéž (sdílí retrofit EDISONMAIN.exe). |

**Možnosti — chybějící konzumenty (low priority pro pilot):**

- `ShowWindowsAsDialogs` — persistuje, ale žádný consumer neexistuje (legacy:
  ovlivnilo modal/modeless launch portovaných oken). Až bude port nebo
  WindowNavigator rozhodovat dialog vs. modeless, číst z `LocalPrefs.Current.ShowWindowsAsDialogs`.
- `ShowMiniApps` — persistuje, ale mini-apps v EDISONMAIN2 nejsou implementovány
  (mimo pilot scope).
- `LoadBoards` — funguje (toggluje viditelnost WebView2 s nástěnkou).

**Font — částečná aplikace:**

`ApplyFontToMainWindow` nastaví `FontFamily` / `FontSize` na hlavní okno; WPF
inheritance pokryje `TreeView` (strom modulů), ale **horní `Menu` se nemění**
(WPF výchozí styl pro `MenuItem` nastavuje vlastní `FontFamily`/`FontSize`,
což přebije inheritance). Pro pilot stačí; doladit přes implicit `Style` pro
`MenuItem` / `StatusBarItem` s `Setter` na `FontFamily`/`FontSize` bound na
`MainWindow`.

**Pravidlo pro pilot:** všechny stub položky lze nechat stub, **pokud uživatel jejich
denní funkčnost nepotřebuje**. Postupně implementovat na základě feedbacku z pilotu —
typicky volat legacy formu přes `FormToRunGetter`, pokud její .exe je retrofitnuté,
jinak port do WPF.

### 3. Klávesová zkratka Ctrl+W,W — Utilities (formUTILITIESMain)

Legacy reaguje na **dvojité stisknutí Ctrl+W** (chord) — spustí
`EDFormInterface.EDISFormEnum.formUTILITIESMain` z `Utilities.exe`
(`c:\MLineRepos\EDISON\Utilities\Utilities\formUTILITIESMain.vb`). Pro pilot
zachovat tuto zkratku v EDISONMAIN2.

**Kroky:**

1. Retrofitnout `Utilities.exe` s `EDISONMAIN2API.FormToRunGetter` (stejně jako
   USERS.exe — přidat `Program.vb` + EDISONMAIN2API reference + změna
   `StartupObject` v `.vbproj`).
2. EDISONMAIN2 shell: state-machine pro chord `Ctrl+W` → status text → druhé
   `Ctrl+W` v okně ~2 s → `WindowNavigator.ShowLegacy("Utilities.exe", ...)` +
   `ALVA.UTILITIES.formUTILITIESMain`. Escape přeruší.

### 4. Pilot runbook + verification doc

**Cíl:** dokumenty pro M-line support tým.

- **`PILOT_RUNBOOK.md`** — krok-za-krokem instalace EDISONMAIN2 vedle EDISONMAIN
  na pilotní stanici (DotNet 10 runtime, `C:\Edison\` deploy, editace
  `appsettings.json` pro server connection, ověření prvního loginu).
- **`DOD_TESTS.md`** — reprodukovatelný test pro každé z 6 DoD kritérií,
  protokolovaný se screenshoty.

---

## Vědomé škrty (mimo pilot)

Vše níže není v pilot scope. Reasoning u každého, ať je jasné proč.

| Položka | Důvod škrtu |
| --- | --- |
| **`ClientKind` ve stacku** (`MLRest.cs`, `Activity.cls`, `Rest.cls`) | Server v současné konfiguraci EDISONMAIN2 přijímá. Pokud na pilotním Caché narazíme na `EdisonVersionFailed!` nebo neidentifikovatelný admin audit, řešíme tehdy. |
| **ActivitySession (keepalive timer)** | Legacy taky nemá explicitní keepalive — ACTIVITY lock se osvěží pouze přes user-driven MLRest volání (`CacheLocks=1` → `UpdateHistory`). Idle shell vyprší stejně v EDISONMAIN i EDISONMAIN2. |
| **`maSeUkoncit` polling** | Legacy ho má pro brzkou detekci admin force-logoutu (60s). Bez něj uvidíme force-logout až při příštím MLRest volání přes `EdisonActivityFailed` event → shutdown. UX rozdíl, ne blocker. |
| **`Logout` on `Exit`** | Bez něj ACTIVITY lock zůstane do server-side timeoutu (default 30 min) a pak se sám smaže. Pilot s tím vyjde — zombie lock je krátký a server ho auto-cleane. Možno přidat 5-řádkový hook do `Application_Exit` později. |
| **`UpdateLock.cls` + broadcast (F7)** | M-line vydá update v offline časech (pilot stanice). Žádná koordinace přes server zatím netřeba. |
| **`KickAndLogin` v Caché (F6)** | Legacy `ALVA.EDISONAPI.Updater.VzdaleneUkonceniEdisona` funguje, admin přes EDISONMAIN2 ho vyvolá přes spawnutý `USERS.exe formUSERSActivity` (už máme). |
| **Setup wizard (F4)** | Pilot stanici nastavíme ručně přes `appsettings.json` při instalaci. UI wizard přidáme po pilotu. |
| **Reconnect UX (F5)** | Při ztrátě spojení → existující shutdown, uživatel restartuje. Akceptujeme. |
| **Cooperative logout countdown** | Nesnížíme UX, ale `EdisonActivityFailed` → shutdown stačí. |
| **Admin Activity list v EDISONMAIN2** | Spawnutý legacy `USERS.exe formUSERSActivity` to dělá. |
| **Expirační UI ve status baru** | Admin feature, lze doplnit po pilotu. |
| **`AddLicenceDialog`** | Admin importuje licenci přes existující EDISONMAIN. |
| **`EDViewModel.HasLicenceFor` / `CommandLicensed` helpery** | Žádný portovaný modul je nepoužívá. Až bude první port, doplníme. |
| **`ActiveLicenceChanged` event** | Licence se v rámci session nemění. Restart aplikace stačí. |
| **`LicenceGate` v EDControls** | Filtrace stromu je inlinovaná v `EdisonMainViewModel.TryFilter` — funguje. |
| **Fallback Lic objekt** | `LicenceService.GetActiveLicence` může vrátit null → shell běží bez licence (jen unrestricted forms). Cleaner než syntetická fallback Lic. |
| **LDAP UI** | Pilot uživatelé v `EDISON` módu. LDAP/LDAP-AUTO v jednom z pozdějších iterací. |
| **Auto-update klienta** | Single-file publish + ruční copy binárky na pilot stanici. |
| **Cross-thread test licence** | `LicenceService` se volá pouze ze single threadu (App startup, VM setter). Race condition prakticky nemůže nastat. |

---

## Verifikace

### Testovací prostředí

- **Pilotní Caché instance** (běžící produkční nebo dedikovaná).
- **Dvě pilotní stanice**: jedna EDISONMAIN (legacy `.exe`), druhá EDISONMAIN2
  (fresh publish do `C:\Edison\`).
- **Testovací uživatel** dostupný v obou klientech.

### DoD checklist

| # | Kritérium | Test |
| --- | --- | --- |
| 1 | Oba klienti se přihlásí | Login v EDISONMAIN2 vrací `mlp > 0` bez chyby. Login v EDISONMAIN funguje stejně jako dnes. |
| 2 | ACTIVITY kompat | Caché terminál `zw ^\|\|ML(...)` ukáže obě sessions s různými GUIDy. Admin přes spawnutý `formUSERSActivity` vidí a může kicknout kteroukoli. |
| 3 | Business lock | EDISONMAIN otevře edit záznamu → EDISONMAIN2 dostane "Záznam zamčený". A naopak. |
| 4 | Update koexistence | Manuální update binárky jednoho klienta — druhý běží dál bez rozbití. |
| 5 | Licence | Import licence v EDISONMAIN → EDISONMAIN2 po loginu vidí stejné `Elements`. `CountsLOGIN` limit funguje (admin přes USERS.exe odhlásí někoho, retry). |
| 6 | UNDONE workaround | Ověření / zdokumentování v `DOD_TESTS.md`. |

### Sign-off pilot

- Pilot stanice běží 1 týden bez regresse.
- Žádné `EdisonVersionFailed!` / `ACTIVITY: Lock exist failed!` v logu.
- Admin akce (kick přes USERS.exe) fungují proti oběma klientům.
- `PILOT_RUNBOOK.md` a `DOD_TESTS.md` předány M-line support týmu.

Po sign-off se může pilot rozšířit na širší sadu stanic a začít iterace 2
(plný admin menu, auto-update, LDAP UI, mini-apps, atd.).
