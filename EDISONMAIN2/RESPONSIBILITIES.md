# EDISONMAIN2 — odpovědnosti aplikace

Tento dokument popisuje, za co je shell aplikace `EDISONMAIN2` (a její předchůdce
`EDISONMAIN`) zodpovědná. Neměl by popisovat **business agendy hostovaných modulů**
(ty mají své vlastní repo složky a dokumentace) — jen **infrastrukturu hostování**.

Zdrojem rešerše je sesterský repozitář `C:\MLineRepos\EDISON` (stará VB.NET verze),
zejména projekt `EDISONMAIN` a jeho backend v `CACHE\ALVA\EDISON\`.

## Co EDISONMAIN2 **není**
- Není to monolit s business logikou dopravy / mezd / fakturace / …
- Hostované moduly (JR, MZDY, TICKETS, …) mají vlastní kód v sousedních složkách;
  EDISONMAIN je pouze **startuje, autorizuje, dokuje a ukončuje**.

## Co EDISONMAIN2 **je**
- **Single-process shell** — spouštíme jednu `.exe`, uvnitř které se zobrazují všechna
  okna ostatních agend.
- **Bootstrap prostředí** pro komunikaci s Caché (MLRest / `IMLRest` / `EDServices`).
- **Kontext uživatele / firmy / databáze** — každé okno hostované agendy dostává
  pre-navázané připojení a identitu.
- **Jednotná navigace, UX a vizuální styl** (EDIcons, EDStrings, EDFooter, EDToolbar,
  EDInputForm, EDList, EDActionInProgress, EDViewModel).

---

## 1. Startup a připojení k serveru

| Oblast | Starý VB projekt | EDISONMAIN2 ekvivalent |
|---|---|---|
| Entry point / App.xaml | `ApplicationEvents.vb`, `My Project\Application.myapp` | `App.xaml.cs` |
| Splash | `frmSplash.vb` | (zatím TBD) |
| Výběr serveru | `frmConnection.vb` | Connection dialog |
| Detail serveru | `frmConnectionDetails.vb` | Connection editor |
| Výběr databáze | `formEDISONMAINSelectDatabase.vb` | DB picker |
| Kontrola verze serveru | `frmNoMDI.vb` (`MESSAGE_ConnectionVersionMismatch`) | musí zůstat |

**Odpovědnosti:**

- Zobrazit splash s postupem startu.
- Nabídnout / pamatovat si seznam dostupných serverů (Caché instancí) — persist
  v Windows Registry (`HKCU\Software\M-Line\EDISON\ConnectionList`).
- Umožnit editaci serverů (host, port, REST port, výchozí DB).
- Zvolit výchozí / pamatované připojení, s možností manuálně přepnout.
- Proxy-přes-TeamViewer scénáře (remote PC): `frmSelectComputer.vb`, `frmRemotePcList.vb`.
- Ověřit version-compatibility mezi klientem a Caché backendem před přihlášením.

### 1.1 První spuštění a `frmSetup` wizard

Startup má zvláštní větev pro **první spuštění aplikace** (anebo pro stanici
bez konfigurace). Detekuje se čistě přes existenci a obsah souboru
`EDISONini.xml` (viz § 11.1).

**Detekce** — property `EdisonIsInitialized` (`frmNoMDI.vb:1819-1833`):

1. Pokud `EDISONini.xml` **neexistuje** → `Return False` → first-launch.
2. Pokud existuje, ale `<edison/initialized>` je `0` nebo chybí → `Return False`
   → first-launch.
3. Pokud `<initialized>` je `1` → standardní start.

**First-launch flow** (`frmNoMDI.Load` → řádky 4560-4566):

1. Spustí se `frmSetup.vb` wizard.
2. Uživatel (typicky technik M-line při instalaci) vyplní:
   - Connection: `ConnectionName`, `Server`, `Port`, `RestPort`,
   - `Database`, `IdFa`,
   - `IRIS` (boolean — běží Caché/IRIS bez MLServeru?),
   - první uživatelské jméno + heslo.
3. Wizard validuje (test connectivity, formát IdFa, aktivuje licenci).
4. Po OK → `SaveSettingsToFile()` vytvoří `EDISONini.xml` s `<initialized>1</initialized>`.
5. **Aplikace se ukončí** s hláškou `MESSAGE_SetupChanged`
   (*"Bylo upraveno nastavení, změny se projeví po restartu systému EDISON.
   Aplikace bude nyní ukončena."*) — uživatel ji musí znovu spustit pro
   normální start.
6. Pokud wizard uživatel zruší → `MESSAGE_SetupMissing` (*"Nejsou zadány
   potřebné informace, aplikace bude nyní ukončena."*) → exit bez `EDISONini.xml`
   → další spuštění znovu spustí wizard.

**Znovu-vyvolání** setup wizardu za běhu: admin přes menu (Nástroje / EDISON /
Setup — přesná položka TBD), volá `ShowSetupWindow()`. Výsledek je stejný —
přepsání `EDISONini.xml` a nutný restart.

---

## 2. Login / autentizace

### 2.1 Podporované login módy

Způsob přihlášení je nastaven **per uživatel** v Caché jako feature #23
(`$$$FeatureTypeLogin`) v globalu
`^[MLINE]ALVA.USERS($$$SuperIdfa,"users",<mlp>,"features",23)`.
Konstanty jsou definované v `EDUsers/EDUsers/CACHE/ALVASYSLogin.inc`.

| Mód | Hodnota feature #23 | UX | Ověření | Lze měnit heslo v EDISONu? |
| --- | --- | --- | --- | --- |
| **EDISON** (lokální) | `"EDISON"` | jméno + heslo | SHA1 hash + sůl, porovnání s feature #22 v Caché | ✅ ano (`frmChangePassword`) |
| **LDAP** | `"LDAP"` | jméno + heslo | `%SYS.LDAP.Binds(...,$$$LDAPAUTHNEGOTIATE)` — Kerberos/NTLM proti AD; heslo se v Caché NEUKLÁDÁ | ❌ ne (heslo spravuje AD) |
| **LDAP-AUTO** | `"LDAP-AUTO"` | **jen jméno** (bez hesla) — Windows SSO / Kerberos ticket | LDAP bind bez hesla; pokud je heslo zadané, server přepne na `LDAP` | ❌ ne |

**Důležité detaily:**

- "Jen jméno" (bez hesla) = `LDAP-AUTO`. Uživatel zadá pouze jméno (nebo proběhne
  auto-login ze Windows username), heslo se neposílá.
- **EDISON sůl** (`$$$SaltPassword`): `"(!@#$;*\.k~'*8_)"` prefix i suffix kolem hesla
  před SHA1. Definováno v `ALVASYSLogin.inc` řádek 9.
- **Normalizace jména** (`$$$NormalizeUsername`): `<prefix>_<UPPER(username)>`; prefix
  (výchozí `"EDISON_"`) je v `^[MLINE]ALVA.USERS($$$SuperIdfa,"login","prefix")`.
- **LDAP konfigurace** (server, doména, BaseDN, flag povolení) je v
  `^[MLINE]ALVA.USERS($$$SuperIdfa,"login","LDAP",...)`. Flag `LDAPUpdate`
  říká, zda se při každém loginu synchronizují atributy (`displayName`, `description`,
  `mail`, `mobile`) do feature #3/18/12/24 uživatele.
- **Povolení loginu** — feature #14 (enabled), feature #15/#16 (časové okno
  `AllowFrom`/`AllowTo`). Backend to kontroluje v `ALVA.SYS.Login.Authenticate`
  (`EDUsers/EDUsers/CACHE/ALVA/SYS/Login.cls`).

### 2.2 UI dialogy

| Soubor | Role |
| --- | --- |
| `frmLoginEx.vb` | **Hlavní** login dialog — jméno, heslo, firma, "pamatuj si mě", auto-login |
| `frmLogin.vb` | Legacy / zjednodušená varianta (~28 řádků, moderně se nepoužívá) |
| `frmChangePassword.vb` | Změna hesla (jen pro mód `EDISON`) — staré heslo, nové heslo 2×; flag `DenyOriginalPassword` skrývá staré heslo při vynuceném resetu |
| `frmSecuredLogin.vb` | **NENÍ login mód**, ale reconnect dialog — viz § 2.6 |

### 2.3 Klientský tok

1. `frmNoMDI.OpenConnection()` → `SelectConnection()` → `DoLogin()`.
2. `frmLoginEx` sbírá jméno + heslo + firmu + flagy.
3. `EDUsers.Login()` (VB wrapper v `EDUsers/EDUsers/EDUsers.vb`, řádky 128-132) —
   při auto-loginu nahradí `username` za `Environment.UserName` a heslo za `""`.
4. Přes MLRest se volá `ALVA.SYS.Login.Login` (Caché), výstup
   `mlp ~ fullname ~ admin ~ dir ~ changePassword ~ updateEdison ~ startupForm ~ email ~ cacheWebLogin ~ cacheWebPassword ~ username`.
5. Pokud backend vrátil `changePassword=1` (feature #17), ukáže se `frmChangePassword`
   v cyklu dokud se heslo nezmění nebo uživatel neopustí přihlášení.
6. Po úspěchu naplní `MLRest.Mlp` (user ID) a `MLRest.Idfa` (firma).

### 2.4 Auto-login

Aktivuje se když:

- Aplikace je spuštěna s `/reason=2` (launch z jiné aplikace / Windows při probuzení),
  **a zároveň**
- `SystemInformation.UserName.ToLower = user.ToLower` — Windows uživatel musí odpovídat
  cílovému EDISON uživateli.

V klientu se heslo zahodí a odešle se jen jméno. Backend pak podle feature #23
rozhodne:

- `EDISON` — auto-login **není povolen**, vrací chybu -107 (špatné heslo).
- `LDAP-AUTO` — projde, protože žádné heslo není třeba.
- `LDAP` — v tomto módu auto-login taky nefunguje (vyžaduje heslo).

Flag zakázaného auto-loginu per uživatel → chyba -106
(`MESSAGE_UzivatelMaZakazanyAutoLogin`).

### 2.5 "Pamatuj si mě" (remember me)

Checkbox v `frmLoginEx` (povolený flagem `mInput.PamatovatUziv`) ukládá
`user + server + password` šifrované AES (Rijndael, CBC, PKCS7) do Windows
Registry pod `HKCU\Software\M-Line\EDISON\...\Login`. Klíč + IV jsou generované
per instalace. Decrypt při dalším spuštění, ale jen když se shoduje jméno a server.

### 2.6 `frmSecuredLogin` — reconnect + `securedMode` flag

**Flag `<securedMode>` v `EDISONini.xml`** (§ 11.1) — **`0` / `1`, plaintext**.
Řídí chování MLServer vrstvy a login flow. **NENÍ to šifrování
souboru** (soubor je uložený v plaintextu). Flag ovlivňuje dvě věci:

1. **`MLServer.Security = mSecuredLoginMode`** (`frmNoMDI.vb:610`, vlastnost
   čtená při loginu). Zapnuté `securedMode` zapíná bezpečnostní vrstvu
   MLServer (autentizace, předpokládaná TLS / ověřená komunikace s Caché
   instancí). Detail implementace v `MLServer.dll` (mimo scope shellu).
2. **Reconnect flow.** Když MLRest ztratí spojení a musí se znovu přihlásit:

   - **`securedMode = 1`** → uchovává credentials uživatele v paměti
     (`mSecuredUserID`, `mSecuredPassword` — plněné z
     `MLServer.CacheWebLoginAndPassword`) a reconnect proběhne
     bez interakce.
   - **`securedMode = 0`** → zobrazí se `frmSecuredLogin` s předvyplněným
     jménem a uživatel znovu zadá heslo. Chyba `MESSAGE_SecuredLoginFailed`
     ukončí aplikaci, pokud uživatel dialog zruší.

Je to **interní mechanismus**, ne samostatný login mód. Pro EDISONMAIN2 musí
existovat ekvivalent (transparentní reconnect nebo dialog), jinak by ztráta
spojení shodila celou session.

### 2.7 Command-line parametry pro neinteraktivní login

`frmNoMDI.vb` (řádky 212-224) parsuje:

| Parametr | Účel |
| --- | --- |
| `/userName=<jmeno>` | přednastaví uživatele |
| `/userPass=<AES>` | heslo zašifrované `ALVA.CORE.CryptText` s per-instalaci klíčem |
| `/reason=<int>` | `EdisonLaunchReasonEnum` — např. `2` = auto-login z jiné app |

Používá se při spouštění EDISONu z jiné aplikace (např. z Podpory M-line /
helpdesku) bez nového loginu.

### 2.8 Chybové stavy backendu

`ALVA.SYS.Login.Login` vrací numerický kód, který `frmNoMDI` mapuje na hlášku:

| Kód | Konstanta | Scénář |
| --- | --- | --- |
| -1 | `MESSAGE_UnknownUser` | Uživatel neexistuje |
| -10 | `MESSAGE_UzivatelNemaJmeno` | Prázdné jméno |
| -11 | `MESSAGE_UzivatelNemaHeslo` | Prázdné heslo (mód vyžadující heslo) |
| -101 | `MESSAGE_UzivatelNemaID` | Uživatel nemá `mlp` |
| -102 | `MESSAGE_UzivatelNeniPovolen_v_Cache` | Caché `%Service_*` pro uživatele neexistuje |
| -103 | `MESSAGE_UzivatelNeniPovolen_v_EDISONovi` | Feature #14 = 0 |
| -104 | `MESSAGE_UzivatelNeniPovolenNyni` | Mimo časové okno (feature #15/#16) |
| -105 | `MESSAGE_UzivatelJeDeaktivovan` | Deaktivován |
| -106 | `MESSAGE_UzivatelMaZakazanyAutoLogin` | Zakázaný auto-login |
| -107 | `MESSAGE_UzivatelSpatneZadalHeslo` | Špatné heslo (i pro `EDISON` při auto-loginu) |
| -108 | `MESSAGE_PasswordChangeNeeded` | Vynucená změna hesla (feature #17=1) |

Navíc jsou licenční stavy `MESSAGE_OutOfLicense`, `MESSAGE_OutOfLicenseVehicle`,
`MESSAGE_OutOfDatabaseLicense` — zabraňují přihlášení při překročení licence.

### 2.9 Vyčerpaný licenční limit: admin override ("kick-and-login")

**Scénář:** licence povoluje max. N simultánních přihlášení, aktuálně je přihlášeno
N uživatelů. Admin se chce přihlásit (N+1. session). `VerifyLic` (§ 13.8) vrátí
`-1` → `MESSAGE_OutOfLicense` → login selže.

**Požadované chování:**

- **Pro běžného uživatele:** login selže s hláškou *"Nelze se přihlásit, byl
  překročen počet licencí."* — uživatel musí počkat, až se někdo odhlásí.
- **Pro admina:** v dialogu s chybou se musí objevit **tlačítko / cesta
  k výběru jiné session a jejímu vynucenému odhlášení**, aby si uvolnil slot
  a pak pokračoval ve vlastním loginu. Bez toho může nastat deadlock situace,
  kdy admin nemá jak se dostat dovnitř (telefonická žádost o odhlášení
  uživatelů není přijatelné řešení v kritických situacích).

**Dnešní stav v EDISONMAIN (staré verzi):**

- `frmNoMDI.vb:1522-1531` — na `ActionStateEnum.OutOfLicense` zobrazí
  `MsgBox(MESSAGE_OutOfLicense)`; pokud `Me.MLProps.UserIsAdmin`, otevře navíc
  dialog `formUSERSActivity` přes `ShowEdisonFormDialog`.
- Po selhání licenční kontroly se adminovi sice otevře `formUSERSActivity` —
  ale je to jen **informativní seznam** "kdo je přihlášen". Kick akce uvnitř
  (`VzdaleneUkonceniEdisona`) vyžaduje aktivní MLRest session, kterou admin
  v tomto okamžiku nemá (autentizace sice proběhla, ale `LockSet` ne).
  **Prakticky tedy admin jen vidí, koho má zavolat telefonem.**
- Caché metoda typu `Login.ForceLogin` / `KickAndLogin` **neexistuje**.
- Workaround dnes: admin má jinou instanci v poolu, odhlásí tu, nebo přes
  přímý Caché terminál smaže zámek ručně.

**Odpovědnosti EDISONMAIN2:**

- **Server-side nová metoda** typu `ALVA.SYS.Login.KickAndLogin(targetGuid)`:
  - Ověří, že volající prokázal admin credentials (stejné ověření jako
    běžný login — `Authenticate` podle feature #23 + feature #14).
  - Ověří, že admin roli má (feature / ECL).
  - Provede `Activity.LockDel(targetGuid)` + případně oznámí cílové session
    přes `UkoncitEdisona` flag (aby UX odpovídal admin force-logout).
  - Vrátí úspěch → klient pak může standardně `VerifyLic` + `LockSet` pro sebe.
- **Klient-side UX**: místo prostého `MessageBox` otevřít dialog se seznamem
  aktivních sessions (jméno, IP, IdFa, čas od posledního dotazu, indikátor
  EDISONMAIN/EDISONMAIN2 — viz § 18.2 `ClientKind`) a tlačítkem "Odpojit
  a přihlásit se místo něj". Potvrzovací prompt s varováním *"Uživatel přijde
  o rozpracovanou práci"*.
- **Autorizace** musí být ověřena **server-side**, nikoli jen kontrolou
  `UserIsAdmin` v klientu (jinak lze obejít úpravou klientského kódu).
- **Audit log** — zaznamenat každý admin kick do `formUSERSLocksLogs` / auditního
  globalu s informací `kickedBy`, `kickedGuid`, `timestamp`, `reason`.
- **Edge cases**:
  - Admin nemůže kicknout sám sebe (kontrola `targetGuid != vlastni_guid`,
    ale tady ještě vlastní GUID neexistuje, takže podle `Mlp` + IP).
  - Kick nesmí být možný když cílový uživatel drží kritický business zámek
    v průběhu důležité transakce? (TBD — může způsobit rollback a ztrátu dat.)
  - Co když admin kicknul, ale jeho vlastní `LockSet` následně selže z jiného
    důvodu (např. síť)? Uvolněný slot by se měl vrátit, nebo ho může obsadit
    jiný zájemce.

### 2.10 Odpovědnosti EDISONMAIN2

- Podporovat všechny tři módy (`EDISON`, `LDAP`, `LDAP-AUTO`). Zvolit mód **nesmí**
  klient — rozhoduje o něm backend podle feature #23 uživatele. Klient jen ví,
  zda pole `heslo` má být viditelné (`LDAP-AUTO` → skryté / nevyžadované) a jestli
  povolit "změna hesla" dialog (pouze `EDISON`).
- Zachovat auto-login flow (command-line parametry + Windows username match).
- Zachovat "pamatuj si mě" s AES šifrováním.
- Mít reconnect mechanismus (ekvivalent `frmSecuredLogin` / secured mode).
- Mapovat všechny backend chyby na srozumitelné hlášky (minimálně stejný rozsah
  jako dnes).
- Kontrolovat licenční limity (uživatelé, vozidla, DB connections) a blokovat
  login odpovídajícími hláškami.
- Po úspěšném loginu naplnit `MLRest.Mlp`, `MLRest.Idfa` a inicializovat `EDServices`.

---

## 3. Session / globální kontext uživatele

Struktura `modMain.UserSettings` (`UserSettinsStruct`) je zdrojem pravdy pro:
- `Database` — aktuální Caché namespace s daty
- `IdFa` — aktuální firma (může se za běhu změnit)
- Strom: `TreeWidth`, `TreeExpandedNodes`, `TreeSelectedNodes`
- NavBar: `NavBarButtonCount`, `NavBarSelectedPanel`
- Desktop: pole `DesktopShortcuts`
- MiniApp panel: `MiniAppPanelState`, `MiniAppPanelWidth`, slovník `MiniApplications`
- `FavouriteItems` — oblíbené
- `StartupForm` — automaticky otevírané po loginu

**Odpovědnosti:**
- Načíst uživatelský profil po loginu z Caché + registry.
- Uložit stav při logoutu / zavření (pozice oken, oblíbené, otevřené mini-aplikace…).
- Za běhu umět **přepnout firmu** (`IdFa`) — zavře hostovaná okna a re-inicializuje
  kontext (`MESSAGE_UserDatabaseCannotBeChanged` blokuje změnu při vícenásobném
  přihlášení stejného uživatele).
- Detekovat ztrátu připojení a ukončit aplikaci s hlášením
  (`MESSAGE_ConnectionLost`, `MESSAGE_ConnectionLostEx`).

V EDISONMAIN2 je dnes ekvivalent řešen přes `EDServices.Current` (singleton kontejner
s `IMLRest` atd.) — viz `EDControls/IMLRest.cs`, `App.xaml.cs`.

### 3.1 Seznam dostupných firem (IdFa / DataDB)

EDISON má dva oddělené datové prostory:

| Koncept | Hodnota | Účel | Lokace |
| --- | --- | --- | --- |
| **Systémový namespace** | vždy `"MLINE"` | metadata, seznam firem, uživatelé, konfigurace | `^[MLINE]EDISON(...)` |
| **Datový namespace** | per firma (např. `"CBDP"`) | vlastní data té firmy | `^[CBDP]...` |

`MLRest.DataNamespace` drží aktuální datový namespace, `MLRest.Idfa` drží identifikátor
firmy (např. `"CB_DP"`). Vztah IdFa ↔ DataDB je **1:1**, uložený v Caché globalu:

```text
^[MLINE]EDISON("System","Database",<db>)            = isDefault (0/1)
^[MLINE]EDISON("System","Database",<db>,"IdFa")     = "CB_DP"
^[MLINE]EDISON("System","Database",<db>,"Scheme")   = barevné schéma firmy
```

**Načítání seznamu firem dostupných uživateli:**

- Caché metoda: `ALVA.EDISON.Main.LoadSettings()` (v `CACHE/ALVA/EDISON/Main.cls`,
  řádky 386-606) + wrapper `ALVA.EDISONAPI.Database.GetCompanyList()`.
- Výstupní řádky mají tvar
  `system~database~<database>~<idfa>~<isDefault>~<idfaText>~<parentIdFa>~<parentIdFaText>~<scheme>`.
- Filtrace podle uživatelských oprávnění:
  - `^[MLINE]ALVA.USERS("%","users",<userID>,"PovoleneDB")` — whitelist databází.
  - TFO (funkční omezení) — dodatečná práva per firma (parametr `checkAccess=1`).
- Volání z VB (stará verze): `formEDISONMAINSelectDatabase.GetDatabaseList()` +
  `frmNoMDI.CreateCompanyMenu()` (řádky 1359-1395).

### 3.2 UI pro přepínání firmy

Dva způsoby:

**a) Menu "Firma" vlevo nahoře v hlavním menu baru** — `mnuEdisonFirma`
(`ToolStripMenuItem`) se **dynamicky naplňuje** po loginu a po každém přepnutí:

- `ToolStripTextBox` nahoře jako rychlé vyhledávání,
- položka pro každou firmu, v `Tag` má `CompanyStruct` (ikona, název, schéma).
- Handler: `CompanyItem_Click()` v `frmNoMDI.vb` (řádky 1498-1551).

**b) Dialog "Nastavit databázi"** (`formEDISONMAINSelectDatabase.vb`) —
ComboBox varianta, voláno z menu EDISON → Nastavit databázi
(`mnuPrgSetDatabase_Click`, řádky 3167-3192).

**UX podmínka:** menu je relevantní pouze když uživatel **nemá otevřená žádná
agendová (hostovaná) okna**. Přepnutí firmy jako operace každopádně začíná
voláním `CloseMainWindow()` (zavře všechna agendová okna), takže s otevřenými
okny by přepnutí znamenalo ztrátu rozpracované práce.

> Poznámka: původní check na multi-login (`MESSAGE_UserDatabaseCannotBeChanged` —
> "Do systému EDISON jste přihlášení více než jednou") je v `frmNoMDI.vb` **zakomentovaný**
> (řádky 3172-3177). Pokud má být zachován v EDISONMAIN2, musíme to vědomě
> rozhodnout.

### 3.3 Mechanika přepnutí firmy

Sekvence v `SelectDatabaseHandler()` (`frmNoMDI.vb` řádky 2857-2903)
a `CompanyItem_Click()` (řádky 1498-1551):

1. `CloseMainWindow(errState, sourceDatabase)` — zavře všechna hostovaná agendová okna.
2. `OpenConnection(...)` — znovu otevře REST/Caché spojení (MLRest se recykluje).
3. Nastaví nové hodnoty `MLRest.DataNamespace`, `MLRest.Idfa`, `MLProps.DataDb`,
   `MLProps.IdFa`, `MLProps.IdFaName` na hodnoty cílové firmy (`targetDatabase.ID`
   resp. `targetDatabase.IdFa` / `targetDatabase.IdFaName`).
4. `DoLogin(mAutoLogin, True, mLoggedUser)` — re-autentizace uživatele v nové firmě
   (znovu-ověření oprávnění, licence, ECL).
5. `LoadMainWindow(...)` — reload navigačního stromu a uživatelských nastavení pro
   novou firmu.
6. `RefreshMainformScheme(errState, company)` — přebarví shell podle `Scheme` firmy.
7. `DoSaveUserSettings(errState)` — uloží do registry "poslední vybraná databáze".
8. `CreateCompanyMenu(errState)` — regeneruje menu `mnuEdisonFirma`.

**Co se NESTANE:** restart procesu. Aplikace pokračuje běžet.

**Odpovědnosti EDISONMAIN2:**

- Zajistit ekvivalentní flow přes `EDServices` / `IMLRest` (swap `Idfa` + `DataNamespace`
  bez restartu procesu).
- Zavřít všechna hostovaná okna před přepnutím + signalizovat jim šanci uložit
  rozpracovanou práci.
- Po přepnutí re-loadnout navigační strom, ECL, barevné schéma, uživatelské settings.
- UI: selector firmy umístit na stejné místo (levý horní roh / hlavní menu bar),
  s vyhledáváním a ikonami jako stará verze.

### 3.4 ACTIVITY zámek — session lifecycle

Každá aktivní session má v Caché tzv. **ACTIVITY zámek**, který slouží jako
"heartbeat" a jediný zdroj pravdy o tom, zda je session živá.

**Klíč zámku:** `ACTIVITY/<idfa>~<ip>~<guid>` (jmenný pattern v `^$LOCK`).

**Doprovodná data** v globalu `^||ML(<idfa>,<ip>,<guid>,…)`:

| Klíč | Obsah |
| --- | --- |
| `"info"` | `loginTime~mlp~userName~fullName~isAdmin` |
| `"history",<n>` | timestamp + `formId`, `className`, `methodName` posledních requestů |
| `"deny"` | 0 / 1 — zakázat další aktivitu (příznak "ukončit uživatele") |
| `"NadrizenyProces"` | `parentGUID~modulName` — pro podřízené procesy |
| `"PodrizeneProcesy",<guid>` | naopak — mapa dětí pro parent session |

**Tvorba zámku** — při loginu:

1. `ALVA.SYS.Login.VerifyLic` na Caché straně volá
   `ALWA.SYS.Activity.LockSet($$$Idfa, $$$IPCache, $$$GUID)` + `Activity.UpdateInfo(...)`
   (naplní `"info"`).
2. VB klient nastaví `_mlr.CacheLocks = 1` (`EDUsers.vb:299`) — signál, že
   každý další MLRest request má volat `Activity.UpdateHistory`.
3. GUID posílá klient — vygenerovaný při vytvoření instance `MLRest`
   (`MLRest.cs:68`, `Guid.NewGuid()`).

**Zdroj klíčových souborů:**

- `MLRest/MLRest/CACHE/ALWA/SYS/Activity.cls` — hlavní implementace (LockSet,
  LockDel, LockExist, Update, UpdateHistory, UpdateTimeout, UpdateInfo,
  CountIdfa).
- `MLRest/MLRest/CACHE/ALWA/SYS/Lock.cls` — nižší vrstva locks (generic).
- `MLRest/MLRest/CACHE/ALWA/SYS/Rest.cls` — REST endpoint, každý request volá
  Activity update.
- `EDUsers/EDUsers/CACHE/ALVA/SYS/Login.cls` — `VerifyLic` (create) + `Logout` (delete).
- `MLRest/MLRest/MLRest.cs` — C# klient s `Refresh()` metodou.

### 3.5 Timeout a kontroly

**Zdroje timeout nastavení:**

| Úroveň | Zdroj | Význam |
| --- | --- | --- |
| Globální | `^[systemDB]EDISON("System","Public","Settings")` — pozice 2, 3 | poz. 2 = enabled flag, poz. 3 = minuty neaktivity |
| Per-user | feature #30 `$$$F030UkoncitSystem` v `^[MLINE]ALVA.USERS(...)` | uživatelsky override (např. kiosk vs. běžný uživatel) |

Admin UI pro nastavení: globální `formEDISONMAINSettingsShow`, per-user `formUSERSUserEdit`.

**Kontrolní smyčka:** `Activity.Update()` se volá **na každém** REST requestu
(`Rest.cls:85-91`) pokud `$$$Locks = 2` (periodická varianta), plus jmenovitě
minimálně 1× za minutu z automatické Caché úlohy. Funkce `Activity.UpdateTimeout`
(`Activity.cls:133-205`):

1. Spočítá `mainSS` = sekund od posledního `"history"` záznamu.
2. Pokud `mainSS >= timeout*60`: nastaví v `^CacheTempEDISON("UkoncitEdisona",…)`
   flag, že uživatel **má být ukončen** — klient to uvidí jako `maSeUkoncit=1`
   v odpovědi a sám ukončí aplikaci.
3. Pokud `mainSS >= (timeout+3)*60`: zavolá `LockDel()` — zámek fyzicky zmizí.

**Detekce expirace na klientovi:** `Rest.cls:87` volá `Activity.LockExist()`
na začátku **každého** requestu. Pokud zámek neexistuje, hodí chybu
`"ACTIVITY: Lock exist failed!"`. C# klient to zachytí a vyvolá event
`EdisonActivityFailed` (`MLRest.cs:28-29`) — shell by měl reagovat ukončením
aplikace nebo nabídnutím re-loginu.

### 3.6 Keepalive přes MLRest

Zámek se **neudržuje explicitním heartbeatem v nastaveném intervalu**, ale
**každým** MLRest requestem:

- `Rest.cls` na začátku každé volané metody běží `Activity.UpdateHistory(idfa, ip, guid, formId, className, methodName)` — přidá nový řádek do `history` s aktuálním
  timestampem.
- Dokud uživatel aktivně pracuje a posílá dotazy, timeout se neposune.
- Pokud uživatel nějakou dobu neklikne, je tu explicitní `MLRest.Refresh()`
  (`MLRest.cs:2142-2159`) → triviální server metoda `ALWA.SYS.Rest.Refresh`
  která jen vrátí vstup, ale **vedlejším efektem** aktualizuje history.
- Volá se v `clsForm.vb:1800-1806` (`OnFormShown`) při zobrazení top-level okna
  (kde `Owner Is Nothing`). Jinými slovy — otevření hostovaného okna funguje
  jako de facto keepalive.

**Kritické:** protože keepalive není časovaný ale request-driven, **neaktivní
uživatel bez otevřeného dialogu vypadne jakmile uplyne nastavený timeout**.
Není to bug, je to design — EDISON nechce držet zombie sessiony.

**Paradox 60-sekundového MainTimeru:** `frmNoMDI.vb:86` má hardcoded
`MAIN_TIMER_Interval = 60 * 1000`. Každých 60 s klient volá
`ALVA.EDISONAPI.Updater.maSeUkoncit(...)` (viz § 3.9) aby zjistil, **jestli má
end sám ukončit**. Tento poll je ale sám MLRest request → server při něm
zavolá `Activity.UpdateHistory` → posune timestamp `mainSS` **zpět na 0**.

Důsledek: **dokud aplikace běží** a MainTimer tiká, session nikdy nevyprší
jen "samovolným čtením formuláře bez klikání" — 60-s poll na to stačí. Timeout
se uplatní prakticky jen když:

- Aplikace je zamrzlá nebo minimalizovaná do stavu, kdy MainTimer neběží
  (šetřič energie / hibernace).
- Síť vypadla a MLRest requesty selhávají.
- Server aktivně nastaví flag `UkoncitEdisona` (admin force-logout přes
  `VzdaleneUkonceniEdisona`, nebo při globální akci).

**Důsledek pro EDISONMAIN2:** je potřeba zachovat nějaký periodický
"are-we-still-alive" poll (nemusí to být rigidní 60 s interval, může to být
jemnější), protože na něm stojí i broadcast kanál pro force-logout a update
notifikace.

### 3.7 Známý bug: nový GUID při otevření modulu

**Symptom:** Občasné chyby `"ACTIVITY: Lock exist failed!"` při otevření nového
hostovaného okna, přestože uživatel aktivně pracoval.

**Kořenová příčina:** `MLRest.cs:68`:

```csharp
public string GUID { get; set; } = Guid.NewGuid().ToString();
```

Každá instance `MLRest` dostane při vytvoření čerstvý GUID. Pokud hostované
okno / modul vytváří **vlastní** instanci `MLRest` (např. `new ALWA.SYS.MLRest(server, port, db, idfa)`
ve startup kódu modulu), pošle server dotaz s novým GUIDem, pro který zámek
neexistuje → `LockExist` selže.

**Co má EDISONMAIN2 udělat jinak:**

- **Jeden sdílený `IMLRest` na proces**, injektovaný přes `EDServices.Current`
  do všech ViewModelů / hostovaných oken.
- Žádné okno (ani hostovaný modul) **nesmí** vytvořit vlastní `MLRest` — vše
  prochází singletonem. Dnes už EDISONMAIN2 pro to má základ (`IMLRest`
  abstrakce v `EDControls/IMLRest.cs`, `EDServices.Current` kontejner) — zbývá
  vymáhat, že to skutečně všichni používají a že `MLRest` konstruktor není
  volán přímo.
- Při přepnutí firmy (§ 3.3) se singleton re-inicializuje (swap `Idfa` /
  `DataNamespace`), ale GUID **nemá** znovu generovat — server rozpozná
  pokračující session.

**Viz TBD v § 23** — k vyřešení před migrací hostovaných modulů.

### 3.8 Lifecycle (shrnutí)

```text
LOGIN        VerifyLic → LockSet + UpdateInfo + _mlr.CacheLocks=1
  ↓
PRÁCE        každý MLRest request → LockExist + UpdateHistory
  │          MainTimer 60s → maSeUkoncit() → kontrola flagu "UkoncitEdisona"
  │          periodicky Activity.Update() kontroluje timeouty
  ↓
TIMEOUT      mainSS >= timeout*60       → flag "UkoncitEdisona"
  │                                     → klient: balloon countdown → CloseEdison
  │          mainSS >= (timeout+3)*60   → LockDel (fyzické smazání zámku)
  │                                     → pokud klient přežil: další request
  │                                       hodí "ACTIVITY: Lock exist failed!"
  │                                       → EdisonActivityFailed → instant exit
  ↓
LOGOUT       Login.Logout → LockDel($$$Idfa, $$$IPCache, $$$GUID)
             EDUser.Logout → _mlr.LockClear(guid) (business zámky)
  ↓
CRASH/KILL   zámek zůstává → admin ho uvidí v formUSERSActivity
             po přirozeném timeoutu zmizí sám
```

**Multi-login:** jeden uživatel může být přihlášen vícekrát současně — každá
session má vlastní GUID. `Activity.CountIdfa(idfa)` spočítá počty pro licenční
kontrolu (§ 13.8). Zakomentovaný check `MESSAGE_UserDatabaseCannotBeChanged`
(§ 3.2) by na tom mohl stát — TBD zda oživit v EDISONMAIN2.

### 3.9 Automatické odhlašování — klientská mechanika a UX

Timeout v § 3.5 popisuje serverovou stranu. Zde je pohled z klienta.

**Dva nezávislé kanály, kterými se klient dozví "máš skončit":**

1. **Cooperative (graceful):** server nastaví flag
   `^CacheTempEDISON("UkoncitEdisona",…)` → klient při příštím 60-s pollu
   `maSeUkoncit()` dostane `ukoncit=1` + `ukoncitZa=<sekundy>` → zobrazí
   balloon countdown a pak zavře aplikaci.
2. **Abrupt (instant):** server už zámek fyzicky smazal přes `LockDel`
   (typicky po timeout+3 min bez signálu) → další request selže na
   `LockExist` → MLRest.cs vyvolá event `EdisonActivityFailed` → klient
   okamžitě ukončí aplikaci.

**Cooperative flow (UX):**

- Poll: `ALVA.EDISONAPI.Updater.maSeUkoncit(errState, MLRest, ukoncitZa, cerpani, …)`
  — `clsUpdater.vb:2073-2103`. Vrací tuple `(ukoncit, ukoncitZa, cerpani)`
  oddělené delimiter — parse v `clsUpdater.vb:2089-2098`:

  ```text
  ukoncit ~ <bool> ~ <sekundy>
  cerpani ~ <info o licenci>
  ```

- Handler v `frmNoMDI.vb:4853-4866`:

  ```vb
  If ukoncit Then
      StopMainTimer()
      WaitFortimeToClose(ukoncitZa)
      CloseEdison(String.Empty, saveUserSettings:=True, exitApplication:=True)
  End If
  ```

- `WaitFortimeToClose()` (`frmNoMDI.vb:4907-4929`):
  - Tray **balloon tip** s ikonou Warning: *"Ukončete práci s programem, za X
    sekund bude automaticky ukončen."*
  - Při `secondsLeft = 10`: druhý balloon *"Programem bude automaticky ukončen
    za [Xs]."*
  - Při `secondsLeft < 5`: smyčka končí, `CloseEdison` se zavolá.
  - **Uživatel nemá žádný způsob, jak countdown zastavit, prodloužit, nebo
    "uložit a odejít"** — není tam dialog s tlačítkem. Jenom tray notifikace.

**Abrupt flow (UX):**

Handler `EdisonActivityFailed` (`frmNoMDI.vb:5023-5032`):

```vb
Private Sub EdisonActivityFailed(sender As Object, e As EventArgs)
    MessageBox.Show("Neaktivní připojení, software bude ukončen.",
                    "Chyba komunikace", MessageBoxButtons.OK, MessageBoxIcon.Error)
    CloseEdison(String.Empty, saveUserSettings:=False, exitApplication:=True)
End Sub
```

- Žádný countdown.
- Žádný save user settings (na rozdíl od cooperative).
- Uživatel ztratí i layout oken.

**Ukládání rozdělané práce: NEEXISTUJE.**

- `CloseEdison(saveUserSettings:=True, ...)` uloží jen **layout / pozice oken /
  startup form** — ne obsah editovaných formulářů.
- **Žádný `OnBeforeLogout` callback** nebo event, do kterého by hostované
  moduly mohly zavěsit "počkej, nejdřív uložím, co mám rozeditované".
- `clsForm.vb:28` má event `IamClosing`, ale ten se vyvolá **uvnitř** close
  phase — pozdě na smysluplný save s nabídkou uživateli.

**Business zámky při auto-logout:**

- Cooperative flow zavolá `CloseEdison` → které iteruje běžící moduly a pro
  každý "zobrazený" (`modul.Zobrazit = True`) volá `EDUser.Logout(GUID)`,
  což v `EDUsers.vb:200` spustí `_mlr.LockClear(GUID)` → vyčistí business zámky
  té session.
- **Hidden moduly** (skryté / minimalizované / odebrané z UI) se přeskočí —
  business zámky, které drží, **zůstávají v Caché**, dokud je nevyčistí
  server-side 3-minutový timeout nebo admin manuálně přes `formUSERSLocks`.
- Abrupt flow (`EdisonActivityFailed`) **nevolá** `Logout()` na žádném modulu —
  jen okamžitě zavře proces. Všechny business zámky drží dál.

**Admin-initiated force logout vs. timeout:**

- UX je identický — oba jdou přes stejný balloon countdown, protože oba
  fungují tak, že nastaví flag `UkoncitEdisona` na serveru.
- Admin akci volá z `formUSERSActivity.vb:547`:
  `ALVA.EDISONAPI.Updater.VzdaleneUkonceniEdisona(err, mlRest, guidList, ukoncitZaSekudy)`
  (`clsUpdater.vb:2059-2072`), minimum 60 s countdown.
- Uživatel nerozlišuje, jestli je odhlašován kvůli neaktivitě nebo ho admin
  kopnul — dostává stejnou hlášku *"Ukončete práci s programem…"*.

**Resume session: NEEXISTUJE.**

Po auto-logout se aplikace zavře úplně (`Application.Exit`). Žádná funkce
typu "znovu se přihlaš a obnovím ti otevřená okna". Uživatel projde celým
login flow znovu, layout se načte z registry/settings (takže se okna
znovu-otevřou, ale **bez rozdělané práce**).

**Idle detection v UI: NEEXISTUJE.**

Klient **nesleduje Windows idle time** (žádné `GetLastInputInfo`, žádný P/Invoke
na `user32.dll`). Skutečnost, že uživatel 30 minut nedotkl klávesnice ani myši,
nemá vliv — dokud MainTimer tiká a polluje `maSeUkoncit`, session žije dál.

**Konfigurace:**

- Globální timeout UI: `formEDISONMAINSettingsShow.vb:353-357`,
  `chkExitSystemOnTimeout` (on/off) + `numExitTimeout` (1-1440 minut, default
  vypnuto).
- Per-user timeout (feature #30 `$$$F030UkoncitSystem`): **UI pro editaci v repu
  nenalezen** — pravděpodobně se dnes neuplatňuje, nebo se nastavuje manuálně
  v Caché. V EDISONMAIN2 by měl být formulář v `formUSERSUserEdit` ekvivalentu.

### 3.10 Odpovědnosti EDISONMAIN2 (auto-logout)

**Musí zachovat:**

- 60-sekundový poll (nebo ekvivalent) pro `maSeUkoncit` kanál.
- Countdown UX s varováním uživatele před zavřením.
- Event handler pro `EdisonActivityFailed` (okamžité zavření při zmizelém zámku).
- Stejný server-side protokol (flag `UkoncitEdisona` + `VzdaleneUkonceniEdisona`
  admin akce) kvůli koexistenci (§ 18).
- Konfiguraci globálního timeoutu s UI v Nastavení (rozsah alespoň 1-1440 minut).
- Uvolnění business zámků držených zavíranou session (`LockClear(guid)`) —
  a to **pro všechny** moduly, nejen zobrazené.

**Musí vylepšit (dnes chybí):**

- **Save-and-exit dialog** místo pouhé balloon notifikace — modální dialog
  s tlačítky "Uložit a odhlásit", "Prodloužit session" (pokud admin povolí),
  "Zahodit a odhlásit".
- **`OnBeforeLogout` callback / event** pro hostované moduly (EDInputForm,
  EDList, custom ViewModely), aby mohly serializovat rozdělanou práci
  do nějakého temp úložiště (per-user preferences, Caché global
  `^EDISON("Drafts",<mlp>,…)` nebo lokální filesystem).
- **Windows idle detection** — integrovat `GetLastInputInfo` nebo WPF
  `InputManager.PreProcessInput` — pokud uživatel 5 min nedotkl vstupu,
  zobrazit pre-warning bez serverového zásahu.
- **Per-user timeout UI** — dnes existuje feature #30, ale nemá správcovský
  formulář. EDISONMAIN2 má doplnit editor.
- **Resume session** — po re-loginu nabídnout obnovu stavu (otevřená okna
  a rozdělané formuláře ze save-and-exit).
- **Konzistentní chování obou cest** — cooperative i abrupt flow by měly aspoň
  uložit layout (dnes abrupt ani to nedělá).

---

## 4. Hlavní okno shellu

Staré `frmNoMDI` je non-MDI hlavní okno, do kterého je embedovaná `frmMain`
s navigací. V EDISONMAIN2 je ekvivalentem `MainWindow`.

**Struktura:**
- Menu: **EDISON / Firma / Uživatel / Nástroje / Nápověda** (viz § 15).
- Toolbar (kontextově se mění podle vybrané položky).
- Status bar: aktuální DB, uživatel, čas, countdown licence.
- Content area: 3 docking zóny — **Navigation panel** vlevo, **Desktop** uprostřed,
  **Mini-application panel** vpravo.
- Info bar / news ticker dole: refresh ze síťového souboru
  (`\\192.168.16.1\Webis\EDISON\WebInfo\WebInfo.txt`) + notifikace o blížící se
  expiraci licence.
- Hostovaná okna agend se otvírají **jako child windows** (dříve MDI-style docking,
  dnes samostatná okna / docking layout TBD).

> **Drobnost:** ve staré verzi existují sezónní uvítací okna `frmW8.vb`,
> `frmW8Xmas.vb`, `frmW8Easter.vb`, `frmW8NewYear.vb`. U EDISONMAIN2 TBD, zda
> přenášet (ne kritické, ale uživatelsky milé).

---

## 5. Navigace a spouštění hostovaných modulů

### 5.1 Zdroj stromů (dnes XML, plán přesun do Caché)

Ve staré verzi jsou **všechny** navigační stromy definovány staticky v XML resource
souborech v projektu `EDCORE`:

| Soubor | Obsah |
| --- | --- |
| `EDCORE/Resources/edisonnavbar.xml` | Definice **panelů** NavBar + kostra stromu |
| `EDCORE/Resources/edisontree.xml` | Detailní metadata uzlů (názvy, ikony) — ~3 tis. řádků |

`edisonnavbar.xml` obsahuje **5 top-level kořenů**, nikoliv tři samostatné
stromy — je to **jeden XML se čtyřmi viditelnými sekcemi** (+ jedna skrytá):

| `id` | Sekce | Účel |
| --- | --- | --- |
| `-1` | Oblíbené | Zkratky na libovolný uzel z ostatních sekcí |
| `-2` | Hlavní nabídka | Agendové moduly (JR, MZDY, TICKETS, …) |
| `-3` | Správa systému | Admin / konfigurace (uživatelé, firmy, licence, …) |
| `-4` | Partnerská řešení | Custom řešení zákazníků (AURIS, TRANSDEV, …) |
| `-101` | Systém | Skrytá interní větev (licenční nástroje) |

Každý uzel má atributy: `id` (unikátní identifikátor mapovaný na
`EDCoreInterface.EDISFormEnum` / `EDMainPackageEnum` / `EDPackageEnum`), `type`
(`0` = skupina, `1` = CSP/externí odkaz, `4` = VB formulář), `licensed="1"`
(podléhá licenční kontrole — bez atributu se licence ignoruje), `hidden="1"`,
`mlineOnly="1"`, `icon`, `iconFav`, `info`, `color`. Uzly typu `0` obsahují
child `<node>` prvky rekurzivně.

> **Plán EDISONMAIN2:** definice stromů přesunout **z XML do Caché**, aby byly
> editovatelné per deployment / firmu a aby je šlo měnit bez redeploye klienta.
> Struktura uzlů a atributy by se měly zachovat (aby šlo migrovat stávající XML
> 1:1), ale úložiště bude Caché global + editor.

### 5.2 Načítání a filtrace — 4 vrstvy

`frmMain.LoadTreeFromXML()` (řádky 1019-1049) rozhoduje podle `license.Version`:

- Licence `< 2.0` → `ALVA.CORE.ApplicationTree.ToXML` (čte XML ze zdrojů).
- Licence `≥ 2.0` → `ALVA.EDISONAPI.ApplicationTree.ToXmlEx()` (stejný XML +
  `UpdateXmlTree()` který umí strom dekorovat — např. seznam dashboardů).

Filtrace pro každý `<node>` probíhá v `BuildTreeXML()` / `BuildTreeXMLEx()`
(řádky 1069-1152) v tomto **pořadí**:

**Krok 1 — licenční kontrola** (proti `licenseRights`, `GetLicenseRights()`
řádky 427-448; zdroj: `ALVA.LICENCE.EDLicence.Lic.Elements[]`):

- Pokud uzel **nemá** atribut `licensed="1"` → projde vždy.
- Pokud `licensed="1"` → závisí na verzi licence:
  - **Licence `< 1.003`** (legacy): `Elements` je **blacklist** — uzel projde, **pokud
    NENÍ** v `Elements`.
  - **Licence `≥ 1.003`** (dnes): `Elements` je **whitelist** — uzel projde, **pokud
    JE** v `Elements`.

**Krok 2 — hidden flag:** pokud `ignoreHidden=True` a uzel má `hidden="1"`, přeskočí
se (používá se pro `id="-101"` Systém a speciální admin uzly).

**Krok 3 — aplikační přístup uživatele** (proti `accessTable`, `EDUser.GetEdisonAccess(license)`,
`EDUsers.vb` řádky 535-600 → volá Caché `ALVA.SYS.Login.GetEdisonAccess`):

- Metoda vrací `apId` + seznam povolených ID.
- `apId = 3` → **admin** → `GetAllEdisonAccesses()` (řádky 723-758) vrací **všechny**
  položky z `EDISFormEnum` + `EDPackageEnum` + `EDMainPackageEnum` + celý obsah
  `license.Elements`. Tj. admin ze serveru už dostane plný user access.
- Jinak `accessTable` obsahuje jen to, co má uživatel explicitně povolené.

**Krok 4 — admin fallback** (`AddAdminRights()` řádky 765-775, voláno jen pokud
`modMain.UserIsAdmin = True`):

```text
accessTable += EDMainPackageEnum.SpolecnaEvidence       ' vždy
accessTable += EDISFormEnum.formEDISONMAINSettingsShow  ' vždy
if license.Type <> "Nezadána":
    accessTable += EDISFormEnum.formUSERSMain           ' když existuje licence
    accessTable += EDISFormEnum.formTMList              ' když existuje licence
```

**Klíčový invariant:** admin fallback **doplňuje pouze `accessTable` (uživatelská
práva), NE `licenseRights`**. Tj. admin stále **nevidí moduly mimo licenci firmy**,
ale vždy si zachová přístup k nastavení uživatelů a systémovým konfiguracím —
firma se tak nemůže dostat do stavu, kdy by nikdo neviděl nic (= zamknutá instalace).

**Rozhodovací tabulka (licence ≥ 1.003):**

| `licensed` attr | V `licenseRights` | V `accessTable` (po admin fallbacku) | Viditelný |
| --- | --- | --- | --- |
| nepřítomen | — | ano | ✅ |
| nepřítomen | — | ne | ❌ |
| `"1"` | ano | ano | ✅ |
| `"1"` | ano | ne | ❌ |
| `"1"` | ne | libovolné | ❌ (ani admin) |

### 5.3 NavBar panely a vykreslení

`CreateNavBar()` (řádky 2549-2698) iteruje top-level uzly a pro každý vytvoří
`C1.Win.C1Command.C1NavBarPanel` s vlastním `HyperTreeView` (`tvwNavBar`).
Oblíbené (`id="-1"`) mají speciální zpracování — nejsou strom, ale plochý seznam
pinnutých uzlů.

Prázdné panely se nezobrazují (`If tvwNavBar.Nodes.Count > 0 Then mNavBar.Panels.Add(...)`).

### 5.4 Spouštění uzlu

- Klik / double-click → `ShowFormDemandEvent` → `frmNoMDI.HandleShowFormDemand()`.
- Uzel typu `type="4"` (form) se mapuje přes `EDISFormEnum` na VB třídu a vytváří
  se reflexí.
- Uzel typu `type="1"` (CSP) otevírá webový link v embedded prohlížeči / shellu.
- Parametry se předávají strukturou `EDParamIn`.
- Pozice / velikost / stav oken se ukládají do registry
  (`HKCU\Software\M-Line\EDISON\Forms\…`).

### 5.5 Odpovědnosti EDISONMAIN2

**Musí zachovat:**

- Čtyři + jednu viditelnou sekci (Oblíbené / Hlavní nabídka / Správa systému /
  Partnerská řešení / skrytá Systém).
- 4-vrstvou filtraci v daném pořadí (licence → hidden → user access → admin fallback).
- Admin fallback s jasným pravidlem "jen do user-accessu, ne do licenčního rozsahu".
- Atributy uzlů: `licensed`, `hidden`, `mlineOnly`, `icon`, `type` (skupina / form / CSP).
- Spouštění hostovaných oken přes `WindowNavigator.Show` / `ShowDialog`
  (ekvivalent `HandleShowFormDemand`), předávání parametrů přes `EDWindowInput`.
- Perzistence layoutu / stavu oken per uživatel per databáze.
- "Reset nastavení oken" akce (`MESSAGE_RESETFORMS`, `MESSAGE_RESETFORMS_IncludeParams`).
- Oblíbené — uživatel může pinnout libovolný uzel z ostatních sekcí.

**Musí změnit (XML → Caché):**

- Zdroj definic stromu přesunout do Caché (místo EDCORE resource XML).
- Poskytnout admin UI pro editaci stromu.
- Zvážit, zda zachovat 1:1 mapping `id` ↔ `EDISFormEnum` (tj. číselné IDs), nebo
  přejít na string-based identifikátory typu `"EDISONMAIN.Users"` — ID v `EDISFormEnum`
  je křehké, protože to je enum natvrdo v kódu. **TBD.**
- Při migraci zachovat zpětnou kompatibilitu s licenčními Elements (očíslované IDs).
- Endpoint v Caché pro "dej mi filtrovaný strom pro tohoto uživatele s touto
  licencí" — buď server-side filtrace, nebo klient dostane plný strom a filtruje
  sám (zachovat stávající behaviour).

**Otevřené rozhodnutí:** logika licenčních verzí `< 1.003` (blacklist) je dnes
mrtvé dřevo — u všech živých instalací je licence ≥ 1.003. Pro EDISONMAIN2
pravděpodobně ponecháme jen whitelist variantu. Viz § 23.

---

## 6. Desktop / shortcuts

Komponenta `EDDesktop.vb` + `formEDISONMAINShortcutList.vb`.

**Odpovědnosti:**
- Drag-and-drop plocha se zástupci na libovolné okno / akci / web link.
- Struktura `DesktopShortcut`: ID, Name, typ (Form / External / Web), parametry,
  pozice, ikona.
- Persist v registry + user settings.
- Dialog pro správu (přidat / odebrat / přejmenovat).
- Integrace s news ticker / WebInfo.
- Ikony sdílené z `EDIcons`.

---

## 7. Access control (ECL — EDISON Control List)

Správa oprávnění: `formEDISONMAINAccessTree.vb` + Caché metoda `ALVA.EDISON.Main.LoadECL`.

**Odpovědnosti:**
- Hierarchická struktura oprávnění (strom) — mapuje se na formuláře, akce,
  funkční celky.
- Načítání ECL uživatele při loginu a **filtrování navigačního stromu** v § 5.
- Runtime kontrola "má uživatel právo otevřít toto okno?" před spuštěním.
- Admin UI pro editaci (role → oprávnění → uživatelé).
- Audit: kdo kdy co dostal.

---

## 8. Mini-aplikace (dockable widgets)

Komponenty: `EDMiniApplication.vb`, `clsMiniApplication.vb`, `EDDockingPanel.vb`,
`frmMiniAppList.vb`.

**Odpovědnosti:**
- Docking panel na straně hlavního okna.
- Známé typy (`ApplicationTypeEnum`): MZDY, REPORTING, VOZIDLA, AD, FAKTURACE, …
- Lifecycle: `Load` → `ComponentRefreshingEvent` (periodický refresh) →
  `ComponentClosingEvent` → `Close`.
- Oprávnění: mini-aplikace jsou licencované zvlášť
  (`MESSAGE_CannotShowMiniApplications`).
- Uživatel si může vybrat, které mini-aplikace se mají zobrazit
  (`frmMiniAppList.vb`), a pořadí + šířku panelu (persist v `UserSettings.MiniApplications`).
- Komunikace s hostem: eventy (např. klik v mini-aplikaci → otevři hostované okno).

---

## 9. Administrace a správa systému

| Dialog | Starý soubor | Role |
|---|---|---|
| Editace uživatele | `formEDISONMAINUserEdit.vb` | CRUD uživatelských účtů |
| Strom oprávnění | `formEDISONMAINAccessTree.vb` | ACL editor |
| Nastavení aplikací na namespace | `formEDISONMAINSettingsApplication.vb` | Které moduly v které DB |
| Nastavení DB | `formEDISONMAINSettingsDB.vb` | Connection string, default IdFa |
| Nastavení zobrazení | `formEDISONMAINSettingsShow.vb` | UI preference |
| Nastavení per-firma | `frmIdFaApplicationSettings.vb` | Aplikační setting pro konkrétní firmu |
| Seznam logů | `formEDISONMAINLogList.vb` | Audit trail |
| Seznam úloh na pozadí | `formEDISONMAINTaskList.vb` | Background task viewer |
| Editor systémové úlohy | `formEDISONMAINSystemTaskEdit.vb` | CRUD naplánovaných úloh |
| Inicializace systému | `formEDISONMAINInit.vb` | Jen správa verze EDISONu + "umělá doména M-line"; přístup přes Ctrl+W+W → `formUTILITIESMain` (§ 15.1) |
| Úlohy na pozadí | `formEDISONMAINTaskList.vb` | Seznam běžících Caché background tasks; Ctrl+Shift+B (§ 15.1) |
| User DB settings | `frmUsersDBSettings.vb` | Per-user DB preferences |

---

## 10. Lokalizace (multi-language UI)

| Soubor | Role |
|---|---|
| `formEDISONMAINLanguageList.vb` | Seznam / editace jazyků |
| `formEDISONMAINLanguageImport.vb` | Import překladů |
| `frmLanguageItemEdit.vb` | Editace jednotlivé položky |
| CACHE `Localization.cls`, `LangItem.cls`, `LangTranslation.cls` | Backend |
| Globals | `^[MLINE]EDISON("Language",{lang},{key})` |

**Odpovědnosti:**
- UI stringy tažené z Caché (ne RESX).
- Podpora více jazyků (min. CS / EN; další TBD).
- Editor pro správce pro překlady všech stringů ve všech modulech.
- Licenční kontrola (`MESSAGE_CannotShowLocalizations`).
- Fallback na výchozí jazyk, pokud překlad chybí.

> Poznámka k EDISONMAIN2: existující `EDStrings.xaml` pokrývá sdílené stringy
> jen částečně — kompletní lokalizační infrastruktura ještě není hotová a musí
> respektovat fakt, že zdrojem pravdy jsou překlady v Caché.

---

## 11. Konfigurace a persistence

**Zdroje konfigurace (podle priority při startu):**

1. **`EDISONini.xml`** — bootstrap konfigurace vedle EXE (viz § 11.1).
   Zdroj pravdy pro první spuštění, výchozí připojení a `securedMode`.
2. **Windows Registry** — `HKCU\Software\M-Line\EDISON\*` (preferované — per uživatel)
   a `HKLM\Software\M-Line\EDISON\*` (fallback — per stroj).
   Klíče: `ConnectionList`, `Config`, `Forms`, `AppList`, `TreeView`.
   Plní se při startu z `EDISONini.xml`, runtime změny jdou zpět do registry.
3. **app.config** — statické (framework version, diagnostics/logging, binding redirects).
4. **Caché globals** — systémová konfigurace, seznamy DB, aplikace, uživatel
   (`ALVA.EDISON.Main.LoadSettings`).
5. **Runtime dialogy** — uživatelem upravená nastavení proudí zpět do registry.

**Odpovědnosti:**

- Helper třídy pro čtení/zápis registry (`modMain` — `MLRegistry.HiveKey` rozhoduje
  mezi HKCU a HKLM podle instalace).
- Migrace registry při update verze.
- Možnost "reset" nastavení oken.

### 11.1 `EDISONini.xml` — bootstrap konfigurace

Soubor **vedle EXE aplikace** (`Application.StartupPath & "\EDISONini.xml"`,
typicky `C:\Edison\EDISONini.xml`). Konstanta `FILE_CONFIG = "EDISONini.xml"`
v `modMain.vb:61`, property `modMain.ConfigFilePath` na řádku 222-226.

**Role:**

- Zdroj pravdy o **výchozím připojení, databázi, jazyce, IRIS módu a
  `securedMode`** při startu aplikace.
- Signál first-launch (§ 1.1) — absencí souboru nebo `<initialized>0</initialized>`.
- **Plaintext XML** — nešifrované. Pokud má instalace sdílená data (např.
  síťový `C:\Edison\`), případný citlivý obsah je viditelný komukoli
  se čtecím přístupem. Heslo se v `EDISONini.xml` **nikdy neukládá**.

**Schéma (`SaveSettingsToFile` v `frmNoMDI.vb:1870-1911`, čtení
`ReadSettingsFromFile` v řádcích 1948-2011):**

```xml
<?xml version="1.0" encoding="utf-8"?>
<edison>
  <initialized>1</initialized>
  <forceChange>0</forceChange>
  <IRIS>0</IRIS>
  <securedMode>0</securedMode>
  <language name="cs-CZ" />
  <connection name="DefaultConnection"
              server="192.168.1.100"
              port="1972"
              restport="57772" />
  <database name="CACHE" idfa="1" />
</edison>
```

**Tagy:**

| Tag / atribut | Typ | Význam |
| --- | --- | --- |
| `<initialized>` | 0 / 1 | Setup wizard dokončen (§ 1.1) |
| `<forceChange>` | 0 / 1 | Zákaz změny výchozího připojení uživatelem (admin-lock) |
| `<IRIS>` | 0 / 1 | IRIS mód — Caché/IRIS bez `MLServer` prostředníka |
| `<securedMode>` | 0 / 1 | `MLServer.Security` flag + reconnect behavior (§ 2.6). **NENÍ to šifrování tohoto XML.** |
| `<language name="...">` | string | Výchozí jazyk UI (např. `cs-CZ`, `en-US`) |
| `<connection name server port restport>` | 4 atributy | Výchozí připojení; plní se do registry `ConnectionList` |
| `<database name idfa>` | 2 atributy | Výchozí databáze + firma |

**Co se v `EDISONini.xml` NEukládá** (to je v registry nebo Caché):

- User preferences (pozice oken, oblíbené, layout) → Registry `Config`, `Forms`.
- Heslo uživatele (session-level) → **nikdy** do XML; secured mode drží jen
  v paměti (§ 2.6).
- Seznam dalších připojení / databází → Registry `ConnectionList` (XML nese
  jen "default" záznam).
- Lokalizace obsahu, license state, mini-apps — Caché / registry.

**Lifecycle:**

- **Create** — jednou při first-launch (`frmSetup` wizard).
- **Read** — každý start aplikace, `ReadSettingsFromFile` kopíruje hodnoty
  do `MLRegistry.SetValues()` (runtime cache).
- **Update** — pouze explicitní re-setup přes `ShowSetupWindow()` + následný
  `SaveSettingsToFile()`. Za běhu se **XML nepřepisuje**; runtime změny jdou
  do registry.
- **Corruption / neplatný XML** — `Try/Catch` v `ReadSettingsFromFile`, chyba
  je ignorována; uživateli se pak ukáže connection dialog jako fallback.
- **Backup při update** — TBD (klient `clsUpdate` nemá explicitní backup XML;
  jednoduše zůstává na místě, protože update přepisuje jen EXE + DLLs).

### 11.2 Odpovědnosti EDISONMAIN2

- **First-launch detekce + setup wizard** — zachovat ekvivalent `frmSetup`
  a stejnou logiku `initialized` flagu.
- **`EDISONini.xml` reuse strategie** (§ 18) — rozhodnutí:
  - **Sdílet** s EDISONMAIN (čte stejný `C:\Edison\EDISONini.xml`) — výhoda:
    jedna konfigurace pro oba klienty; nevýhoda: verze-kompatibilita XML
    schématu, kolize když oba zapíšou.
  - **Separátní** `EDISONMAIN2ini.xml` — výhoda: nezávislost; nevýhoda:
    uživatel musí znova konfigurovat (nebo klient-side import z EDISONMAIN).
  - Preferovaný směr: **sdílet pro čtení**, **EDISONMAIN2 vlastní pro zápis**
    dokud neproběhne full cutover. TBD § 23.
- **Zachovat všech 7 tagů** pro kompatibilitu.
- **`securedMode`** honorovat stejně — `MLServer.Security` flag + reconnect
  chování.
- **`IRIS` flag** — přezkoumat jestli je dnes potřeba (může být mrtvé dřevo
  pro EDISONMAIN2, které mluví čistě MLRest přes HTTP a `MLServer` nepoužívá).

---

## 12. Automatická aktualizace klienta

Třída `clsUpdate.vb` (~51 KB) + externí `ClientUpdater` (`EDUPDATE.EXE`).

### 12.1 Typy updatů

- **Full** — kompletní přeinstalace
- **Patch** — inkrementální soubory
- **XMLPatch** — jen konfigurační XML
- **NucenyUpdate** — vynucený (nejde odložit)

### 12.2 Flow

1. Porovnat lokální `VERZE.INF` / `EDISON.INF` s verzí na serveru.
2. Pokud je novější → stáhnout balíček přes MLRest port 57772.
3. Zobrazit `frmUpdateDialog.vb` / `frmVersionDetails.vb`.
4. Spustit `EDUPDATE.EXE`, ukončit hlavní aplikaci, updater přepíše soubory,
   znovu spustí EDISONMAIN.
5. Registry klíče: `LAST_INSTALL_STATE`, `BlokovatAktualizace`.
6. Hlášení o selhání / nedostupnosti: `MESSAGE_UpdateError`, `MESSAGE_UpdateFailed`,
   `MESSAGE_UpdateStartError`, `MESSAGE_UpdateNeeded`.

### 12.3 Zámky a aktivní sessiony během updatu

Update se **musí** korektně zachovat k ACTIVITY zámkům (§ 3.4) i k business
zámkům, protože update instalace **přepisuje binárky** — jakákoli zapomenutá
session je ztracená práce a potenciálně korupce dat.

**Dnes (EDISONMAIN):**

- `clsUpdate.vb:547` volá `ALVA.EDISONAPI.Updater.VzdaleneUkonceniEdisona(err, myMLR, lProcess, 1)` — vynucený logout cílových sessions. Logout implicitně volá
  `Activity.LockDel`, takže ACTIVITY zámky se uvolní korektně.
- **Business zámky** (edit záznamu) se **explicitně neuklízí** — zůstávají
  v Caché. Admin je musí manuálně smazat přes `formUSERSLocks.vb` (§ 15).
- Admin flow v `formUSERSActivity.SetLogout(True, True)` má 3-krokovou sekvenci:
  1. `VzdaleneUkonceniEdisona()` — force logout všech sessionu.
  2. Volitelně: `SetLogin(EStates.E00_DISABLE)` — zakáže nové logins po dobu updatu.
  3. Volitelně: `TerminateProcesses()` — zabije pověřené Caché procesy.
- **Žádný system-wide "update in progress" flag v Caché neexistuje.** Pouze
  flag `BlokovatAktualizace=1` v `VERZE.INF` (soubor), který zabrání, aby si
  **klienti** sami nabídli update — nic víc.
- **Žádný broadcast** na uživatele — ten, kdo update spouští, je jediný, kdo
  dostane hlášku `MESSAGE_UpdateNotifyStart`. Ostatní dostanou "aplikace bude
  ukončena" až ve chvíli, kdy je `VzdaleneUkonceniEdisona` donutí zavřít.

**Race conditions — dnes:**

- **Zámek expiroval během updatu:** ošetřené — při novém loginu se zámek vytvoří
  od nuly, žádný konflikt.
- **Dlouhý Caché dotaz během updatu:** **NEOŠETŘENO** — klient může psát do staré
  verze binárky, která se právě přepisuje, s nepředvídatelnými následky.
- **Rozeditovaný formulář se zámkem během force logout:** admin volá explicitně
  ("Ukončit všechny procesy? Ano/Ne"); pokud klikne Ne, business zámky zůstávají.

### 12.4 Odpovědnosti EDISONMAIN2

**Musí vyřešit, co dnes není řešené:**

- **System-wide "update in progress" lock** v Caché (global typu
  `^EDISON("System","Update","InProgress") = 1~<startTime>~<initiator>`), který:
  - Zablokuje nové přihlášení (odmítne login v `ALVA.SYS.Login.Login` / `VerifyLic`).
  - Signalizuje existujícím sessionům "update začíná, uložte a zavřete".
  - Uvolní se po dokončení updatu (případně po timeoutu pokud se updater zasekne).
- **Broadcast notifikace** na všechny aktivní sessiony — využít existující
  ACTIVITY heartbeat jako kanál (stejný mechanismus jako `UkoncitEdisona` flag
  z § 3.5).
- **Explicitní cleanup business zámků** vlastněných logovanými sessionami —
  `VzdaleneUkonceniEdisona` by měl zavolat i `Lock.Clear(guid)` pro business locks.
- Zachovat kompatibilitu se starou `VzdaleneUkonceniEdisona` metodou (stejný
  mechanismus vynuceného logoutu) kvůli koexistenci (§ 18).

**Musí zachovat:**

- Po startu zjistit info o nové verzi (i když uživatel ještě není přihlášen? TBD).
- Umět odložit / vynutit update.
- Detekovat "již stažená verze" a nabídnout rychlou instalaci.
- Respektovat `BlokovatAktualizace` pro admin stanice.

> Otázka k migraci: ponechat vlastní updater, nebo nahradit standardním řešením
> (Squirrel / MSIX / ClickOnce)? EDISONMAIN2 dnes **nemá** žádný update
> mechanismus — viz § 18 (koexistence) a § 23 (TBD).

---

## 13. Licence

Licence je **cross-cutting concern** — dotýká se doslova celé aplikace od loginu
přes navigační strom po jednotlivé moduly. Tato sekce popisuje licenční objekt,
úložiště, flow aktualizace a **kompletní mapu všech míst, kde se licence
kontroluje**.

### 13.1 Struktura licenčního objektu (`ALVA.LICENCE.EDLicence.Lic`)

Zdroj: `Licence/EDLicence/Lic.vb`.

| Property | Typ | Význam |
| --- | --- | --- |
| `IsActive` | Boolean | Zda je licence aktivovaná |
| `State` | `EStates` enum | `E01_OK` / `E02_NOT_ACTIVATE` / `E03_NOT_INITKEY` / `E04_OUT_OF_DATE` |
| `Idfa` | String | Firma, pro kterou licence platí; speciální `"MLINE_ALL"` = všechny firmy (M-line admini) |
| `Version` | String | Formát licence — `"1.001"` … `"2.002"` (viz § 13.3) |
| `Type` | String | `NORMAL` / `ACTIVE` / `TRIAL` / `DEMO` / `OEM` / `PARTNER` / `"Nezadána"` |
| `ValidityFrom` / `ValidityTo` | String | Platnost `YYYY-MM-DD HH:mm:ss` |
| `ActivateTo` | String | Deadline pro aktivaci (po něm licence přechází do `E02`) |
| `Elements` | `String()` | Pole ID povolených (≥1.003) / zakázaných (<1.003) prvků — viz § 5.2 |
| `CountsLOGIN` | Integer | Limit simultánních přihlášení — **blokuje** login |
| `CountsUSERS` | Integer | Limit celkového počtu uživatelů |
| `CountsRZ` | Integer | Limit počtu vozidel (RZ = registrační značka) — **jen varuje** |
| `CountsZAM` | Integer | Limit počtu zaměstnanců — **jen varuje** |
| `CountsTELMAX` | Integer | Limit TELMAX (karty/odbavovač) |
| `CountsCACHE` | Integer | Limit Caché licencí |
| `Keys` | String | Inicializační klíče (v1.004+) |
| `Appendix` | String | Dodatek licence (v1.005+) |
| `Remark` | String | Poznámka (v1.003+) |
| `Creator` | String | Kdo licenci vystavil |
| `CodeLic` | String | Zašifrovaná licence (Base64) — samotný "licenční klíč" |
| `IsTrash` | Boolean | V archivu / koši |

### 13.2 Úložiště a šifrování

- **Caché global:** `^[ALVA.LICENCE]LICENCE` — **archiv** všech licencí (historie + aktivní).
  Filesystem úložiště **neexistuje** — licence je čistě v Caché.
- **Šifrování:** Triple DES (3DES) + MD5, statický klíč `"mline2k9"`
  (`Licence/EDLicence/MConsts.vb`), Base64 encoding pro textový přenos.
- **Dekryptování:** `ALVA.LICENCE.EDLicence.Api.GetLicFromCodeLic(err, cryptLicence)`
  → `Lic` objekt.
- **Šifrovací třída:** `ALVA.LICENCE.EDLicence.CryptText` (ne `ALVA.CORE.CryptText`
  z command-line parametrů v § 2.7 — to je jiná třída s jiným klíčem).

### 13.3 Podporované formáty licence (verze)

| Verze | Přibylo | Poznámka |
| --- | --- | --- |
| 1.001, 1.002 | základní formát | Elements = blacklist |
| 1.003 | `Remark`, **Elements = whitelist** | dnešní minimální verze |
| 1.004 | `Keys` (inicializační klíče) | |
| 1.005, 2.001 | `Appendix` | |
| 2.002 | struktura `Counts` tilde-separated `LOGIN~USERS~RZ~ZAM~TELMAX~CACHE` | aktuální |

Pipe (`|`) odděluje `Elements`, newline (`\n`) hlavní pole, tilda (`~`) podpole
v `Counts`. Parser v `Api.vb` má `Select Case` na `lic.Version` — novější verze
tam jednoduše přidaly další řádky.

> **Plán EDISONMAIN2:** pravděpodobně opustit legacy < 1.003 (blacklist logika).
> Viz § 23.

### 13.4 Default fallback licence

Když `GetActiveLicense()` nevrátí žádnou platnou licenci (chybí / expirovaná /
neaktivovaná), `frmNoMDI.vb` (řádky 401-431) vytvoří **default in-memory Lic**:

```text
Version      = "1.003"
Type         = "Nezadána"
CountsLOGIN  = 1
Elements     = { formEDISONMAINSettingsShow, SpolecnaEvidence }
```

Tím si uživatel (typicky admin) aspoň otevře Nastavení a importuje novou
licenci. Všechny ostatní moduly jsou skryté.

### 13.5 Načtení licence při startu

`frmNoMDI.GetActiveLicense()` (řádky 401-431):

1. `ALVA.LICENCE.EDLicence.Activation(mlRest).GetActiveLic()` čte archiv z Caché.
2. `GetActiveLic()` vybírá v tomto pořadí:
   - `ACTIVE` / `NORMAL` s `ValidityFrom < NOW < ValidityTo`,
   - `TRIAL` s `ValidityFrom < NOW < ValidityTo`,
   - fallback `DEMO`,
   - jinak `null` (→ default fallback, § 13.4).
3. Vrácený `Lic` se uloží jako `mFrmMain.ActiveLicense` (řádka 2538) a je přes
   shell dostupný všem hostovaným oknům.

Při loginu se navíc volá `EDUser.UpdateLicFromMlineServer()`
(`EDUsers/EDUsers/EDUsers.vb`, řádek 170) — pokus o čerstvou licenci z M-line
cloudu. Pokud licence expiruje za méně než **15 dní**, `Activation.vb:287-295`
kontaktuje server automaticky.

### 13.6 Refresh z M-line serveru

Menu **Nástroje → Aktualizovat licenci** →
`formEDISONMAINSettingsShow.RefreshLicense()` (řádky 1452-1461) →
`EDUser.UpdateLicFromMlineServer(errState, allUsers:=True)` →
`ALVA.LICENCE.EDLicence.Update.UpdateLicFromMlineToCustomer()`.

Flow:

1. Klient kontaktuje **M-line produkční server** (IP/port uložené v Caché).
2. Volá `ALVA.FEEDBACK.Update.LoadData` — vrací seznam aktualizovaných licencí
   pro všechny IdFa zákazníka.
3. Klient uloží nové licence přes `ALVA.LICENCE.Update.SaveLicsToCustom`.
4. Oznámí serveru přijetí přes `ALVA.LICENCE.Update.MakeLicActivated`.

**Kdo smí:** admin **nebo** uživatel s právem `UpdateEdison` (`EDUsers.vb:323`).
Dostupnost akce v UI je omezena (`UserIsAdmin OrElse UserMayUpdate`, `frmNoMDI.vb:4241`).

**Co když nová licence odebere přístup:** není explicitní kontrola — další
`LoadTreeFromXML` skryje blokované uzly. Restart není nutný.

### 13.7 Přidání / import licence (`frmAddLicense`)

Dialog `frmAddLicense.vb`, volaný z
`formEDISONMAINSettingsShow.AddLicense()` (řádky 1405-1437):

1. Uživatel vloží zašifrovaný licenční klíč (Base64 string).
2. `Activation.Add(err, key)`:
   - `Api.GetLicFromCodeLic` dekryptuje,
   - **validace:** `lic.Idfa == MLRest.Idfa` **nebo** `lic.Idfa == "MLINE_ALL"`,
     jinak chyba "licence je pro jinou firmu",
   - `ALVA.LICENCE.Activation.Add` uloží do Caché,
   - refresh `GetActiveLic()` → nová licence je okamžitě aktivní.

> **Bezpečnostní poznámka:** `AddLicense` **nemá explicitní kontrolu oprávnění** —
> kdo má přístup do formuláře Settings, ten může přidat licenci. `formEDISONMAINSettingsShow`
> je pro admina defaultně dostupný přes admin fallback (§ 5.2). Viz TBD v § 23.

### 13.8 Kompletní mapa kontrolních bodů

Tabulka je neúplná pro specifické moduly (VOZIDLA2, MZDY2 atd. mají vlastní interní
kontroly uvnitř svého kódu) — zde jsou **všechny touch pointy v shell vrstvě**.

**Blokující kontroly** (uživatel nepokračuje):

| Kde | Co | Kontroluje | Chyba |
| --- | --- | --- | --- |
| Login (`EDUsers.vb:283-310`) | `VerifyLic(CountsLOGIN)` | Caché `ALVA.SYS.Login.VerifyLic` spočítá aktivní sessions | `MESSAGE_OutOfLicense`, login selže; admin má v EDISONMAIN2 dostat možnost kick-and-login (§ 2.9) |
| Připojení (`frmNoMDI.vb:487-490`) | Database license count | Caché vrátí `OutOfDatabaseLicense` | `MESSAGE_OutOfDatabaseLicense(Exit)`, nelze se připojit |
| Aktivace licence (`Activation.Add:64`) | `lic.Idfa == MLRest.Idfa \|\| "MLINE_ALL"` | Porovnání stringů | "Licence pro jinou firmu" |
| Filtrace stromu (§ 5.2) | `Elements` whitelist | Uzel s `licensed="1"` musí být v `Elements` | Uzel se skryje |
| Mini-aplikace (`frmNoMDI.vb:3323-3350`) | `accessTable.ContainsKey(EDMainPackageEnum.MiniAplikace)` | — | `MESSAGE_CannotShowMiniApplications` |
| Jazyková nastavení (`frmNoMDI.vb:3369-3392`) | `LicenseTable.ContainsKey(LicenseRightEnum.AllowLocalization)` | — | `MESSAGE_CannotShowLocalizations` |
| Auto-update (`frmNoMDI.vb:4241`) | `UserIsAdmin OrElse UserMayUpdate` | — | Tlačítko disabled |

**Neblokující (varování):**

| Kde | Co | Kontroluje | Chování |
| --- | --- | --- | --- |
| `frmMain.vb:880-887` | Počet vozidel | `EDAPI.VOZIDLA.Licence.PocetProvozovanychVozidel()` vs. `CountsRZ` | InfoPanel: "Je překročen počet aktivních VOZIDEL…" |
| `frmMain.vb:889-902` | Počet zaměstnanců | `ALVA.MZDY2API.DB.gPocZam()` vs. `CountsZAM` | InfoPanel: kombinované varování |
| `frmMain.vb:848-872` | Expirace licence | `DateDiff(Day, Today, ValidityTo)` | InfoPanel: "Licence skončí za X dnů" / "Licence skončila…" (threshold: `WEBINFO_LicenseDaysLeftZone`) |
| `frmNoMDI.vb:1147` | Status bar | — | Zobrazení typu + datum expirace |
| `frmNoMDI.vb:1221` | DEMO splash | `license.Type.ToLower = "demo"` | Splash screen označující DEMO licenci |

**Filtrace / rozšiřování práv:**

| Kde | Co | Chování |
| --- | --- | --- |
| `frmMain.GetLicenseRights` (řádky 427-448) | Naplní `licenseRights` Hashtable z `license.Elements` | Použité v `BuildTreeXML` |
| `frmMain.PackageIsLicensed` (řádky 628-656) | Pomocná metoda pro jednotlivé balíky | Ad-hoc kontroly mimo strom |
| `frmMain.AddAdminRights` (řádky 765-775) | Admin fallback (§ 5.2) | Přidává `SpolecnaEvidence`, `formEDISONMAINSettingsShow`; pokud `Type ≠ "Nezadána"`, navíc `formUSERSMain` + `formTMList` |
| `EDUsers.GetAllEdisonAccesses` (řádky 723-758) | Pro `apId=3` (admin) vrátí všechny form/package + celý `license.Elements` | Admin dostane kompletní aplikační přístup, stále limitovaný licencí |

**Pozor — co není blokováno:**

- **Runtime lock:** pokud licence expiruje za běhu session, nic se nezamkne;
  projeví se až při dalším loginu / refreshi.
- **Grace period:** žádná — po `ValidityTo` aplikace přejde do "read-only módu"
  (psaní blokuje sama aplikace, ne databáze).
- **Admin bypass licence:** **není** — admin má jen širší user-access, ale
  licence omezuje i jeho (viz invariant v § 5.2).

### 13.9 Multi-firma a partner řešení

- Jedna instalace EDISONu = **jedna aktivní licence v daný okamžik**.
- Archiv v `^[ALVA.LICENCE]LICENCE` obsahuje historii + licence pro různé IdFa.
- Při přepnutí firmy (§ 3.3) se licence znovu načítá přes `GetActiveLicense()` —
  vybere tu vhodnou pro cílovou `Idfa`.
- `"MLINE_ALL"` je speciální IdFa pro interní M-line licence, která pasuje na
  jakoukoli instalaci (pro supportové účely).

### 13.10 UI a administrace

| Soubor | Role |
| --- | --- |
| `frmAddLicense.vb` | Import licenčního klíče (Base64) |
| `formEDISONMAINSettingsShow.vb` (1405-1461) | Sekce "Licence" v Nastavení — Add / Refresh / Detail |
| `LicenceInfo\formLICENCEINFOManage.vb` | Admin UI — seznam + editace všech licencí |
| `LicenceInfo\FrmShowInitKey.vb` | Zobrazení inicializačního klíče |
| `Licence\Licence\FrmLicence.vb` | Interní editor licence (M-line tool) |
| `Licence\Licence\FrmManage.vb` | Správa archivu |
| `Licence\Licence\FrmCreateAdd.vb` | Vytvoření nové licence |

### 13.11 Klíčové soubory (cheatsheet)

```text
Licence/EDLicence/
  Lic.vb              — POCO Lic (struktura)
  CLicence.vb         — mapping na Caché archiv
  Api.vb              — GetLicFromCodeLic (dekryptování + parser)
  Activation.vb       — GetActiveLic / Add / Delete
  CryptText.vb        — 3DES/MD5 encr-decr
  Update.vb           — UpdateLicFromMlineToCustomer
  MConsts.vb          — klíč, verze, typy
  CEdisonForm.vb      — vztah form → licence
  CEdisonRight.vb     — jednotlivé právo
  CTrees.vb           — konstrukce stromů

EDISONMAIN/
  Rozhrani (mainform)/frmNoMDI.vb     — GetActiveLicense, status bar, menu refresh
  frmMain.vb                           — LoadTreeFromXML, GetLicenseRights, AddAdminRights, expirace
  formEDISONMAINSettingsShow.vb        — Add/Refresh license UI
  EDMiniApplication.vb                 — filtrování mini-apps

EDUsers/EDUsers/EDUsers.vb             — VerifyLic, UpdateLicFromMlineServer, GetEdisonAccess
LicenceInfo/                           — admin UI pro správu licencí
```

### 13.12 Odpovědnosti EDISONMAIN2

**Musí zachovat:**

- Kompletní `Lic` strukturu (všechny properties z § 13.1).
- Dekryptování všech 7 formátů (1.001 → 2.002) pro kompatibilitu se stávajícími
  licencemi v produkci.
- Úložiště v Caché globalu `^[ALVA.LICENCE]LICENCE`.
- `VerifyLic` kontrolu při loginu (blokace přes `CountsLOGIN`).
- Validaci `lic.Idfa` při importu (+ speciál `MLINE_ALL`).
- Default fallback licenci (§ 13.4) — aby se zamknutá firma dostala do nastavení.
- Admin fallback invariant z § 5.2 (admin nikdy neobchází licenci).
- 15-denní auto-refresh z M-line serveru.
- Expirační countdown v status baru / InfoPanel.
- Varování o překročeném počtu RZ / ZAM (neblokující).

**Musí rozhodnout:**

- Zachovat legacy `< 1.003` (blacklist) formát, nebo opustit?
- Zachovat statický šifrovací klíč `"mline2k9"`, nebo přejít na moderní šifrování?
- Runtime lock při běhové expiraci — přidat?
- Práva pro `AddLicense` — přidat explicitní permission?
- Strategie online dostupnosti M-line serveru (offline režim pro remote klienty?).
- Viz § 23.

---

## 14. Error handling

- Globální handler: `ApplicationEvents.vb` → `MyApplication_UnhandledException`.
- Zobrazovač chyb: `ALVA.LIBRARY.MLErrorHelper` (`GetStack`, `ShowError`).
- Kontrola transakčních anomálií: `MESSAGE_QueryTLevelErrorBefore` /
  `MESSAGE_QueryTLevelErrorAfter` — nedokončená Caché transakce ukončí aplikaci.
- V EDISONMAIN2 má ekvivalentní roli `PopupDisplayer` + automatický error handling
  v `Command(...)` helperu na `EDViewModel` (viz CLAUDE.md).

---

## 15. Systémové nástroje (menu "Nástroje")

Menu `frmNoMDI` → položka **Nástroje** — jednotný vstup pro admin / support akce:

- **Refresh licence** — reload licenčního souboru.
- **Správa zámků** — přehled Caché locks, rozdělené na:
  - **Sessiony** (`formUSERSActivity.vb`) — seznam ACTIVITY zámků (kdo je přihlášen,
    z jaké IP, jak dlouho, poslední request). Admin může "odhlásit" session
    (volá `Activity.LockDel`).
  - **Business zámky** (`formUSERSLocks.vb`) — editorské zámky na konkrétní
    záznamy (např. `PARKKARTY/KARTA_EDIT#id`), admin je může uvolnit.
  - **Logy zámků** (`formUSERSLocksLogs`) — historie zámků, auditní pohled.
  - Viz § 3.4-3.8 pro detaily ACTIVITY mechaniky.
- **Aktivita uživatele** — kdo co dělá (seznam přihlášených + jejich oken).
- **Jazykové nastavení** — viz § 10.
- **Log seznam** — `formEDISONMAINLogList`.
- **TeamViewer** — spustí `TeamViewerQS` pro remote support (`MESSAGE_StartingTeamViewer`).
- **Helpdesk** — odkaz do ticketovacího systému (vyžaduje vyplněný e-mail uživatele —
  `MESSAGE_HelpdeskEmailMissing`).
- **Spustit kopii EDISONu** — druhá instance (pozor na transakce, session).
- **Styl / font nastavení** — uživatelské UI preference.

Menu **Nápověda** — informace, verze, helpdesk.
Menu **EDISON** — výběr DB, přihlásit/odhlásit, volby, ukončit.
Menu **Firma** — přepnutí IdFa.
Menu **Uživatel** — přehled, změna hesla.

### 15.1 Skryté klávesové zkratky

V hlavním okně jsou dvě klávesové zkratky, které nejsou v žádném menu a otevírají
"specializovaná" okna. Obě musí EDISONMAIN2 zachovat. **Pro rychlý roll-out
se předpokládá, že tato okna se nebudou přepisovat do WPF — jen se v EDISONMAIN2
zavolají jako existující okna (ekvivalent "otevři Windows Form v hosted kontextu"
nebo spuštění legacy `.exe` v dialog módu).** Reimplementace je mimo rozsah
migrace shellu.

#### Ctrl+W+W — vývojářské utility (chord)

- Detekce: `frmMain.vb:1665-1685` (`edDesktop_KeyDown`) + `2067-2087` (`tvw_KeyDown`).
  Je to **chord** — první Ctrl+W nastaví `mCacheWebServicesShortcutPressed = True`
  a zobrazí status bar hint *"(Ctrl+W) was pressed. Waiting for another sequence…"*,
  druhý Ctrl+W dialog skutečně otevře.
- Heslo: `Utilities/Utilities/FrmPassword.vb:9-48` — **dynamicky spočítané per
  aktuální datum**, vzorec:

  ```text
  daySum = ciferný součet dne (1–9 pro den 1–9, 1 pro 10, 2 pro 11, … 3 pro 30)
  pw = (year + daySum)[2. znak] + (month + daySum) + (year + daySum)[3. znak]
  ```

  Žádná role / ECL kontrola — kdo zná vzorec, dostane se dovnitř (kancelářský
  standard M-line).
- Otvírá: `formUTILITIESMain` (`Utilities/Utilities/formUTILITIESMain.vb:57-176`)
  — TreeView s kategoriemi:
  - **EDISON**: `formEDISONMAINInit` (správa čísla verze + umělá M-line doména),
    `formFIRMA2CtrlWWSkupina` (sdílení číselníků napříč firmami).
  - **Mzdy**: `formMZDYInit`, `formMZDY2Import`, `formMZDY2Export`.
  - **Sklady**: `formESUTILITYCtrlWWInit`, `formESUTILITYCtrlWWInitEKOS`,
    `formESUTILITYCtrlWWInitCiselniky`.
  - **Nástroje**: `formUTILITIESCacheNSDB`, `formUTILITIESConnectionTest`,
    `formUTILITIESEmailTest`, `formAUDITAuditList`, `formLICENCEINFOManage`.
- Reuse strategie v EDISONMAIN2: projekt `Utilities.csproj` musí fungovat
  v koexistenci (§ 18). Shell EDISONMAIN2 detekuje chord a otevře okno — buď
  přes `WindowsFormsHost` (pokud `.dotnet` target), nebo spustí legacy `.exe`
  jako dialog. Prozatím v EDISONMAIN2 nepřepisovat.

#### Ctrl+Shift+B — úlohy na pozadí

- Detekce: `frmNoMDI.designer.vb:471-472` — `ShortcutKeys = (Control Or Shift) Or Keys.B`,
  menu item `mnuToolShowBgTasks` *"Úlohy na pozadí"*. Handler
  `frmNoMDI.vb:3398-3406` → `ToolShowBackgroundTasks()`.
- **Bez hesla**, dostupné všem uživatelům.
- Otvírá: `formEDISONMAINTaskList.vb` — `EDFlexGrid2` se seznamem Caché
  background tasks. Filtry: `ShowTasksWithIdOnly`, `ShowTasksWithActionOnly`,
  `AdminMode`. Akce: Refresh / Delete / Note / Show / Add.
- Existuje i debug varianta `DebugShowBackgroundTasks()` (řádek 3990)
  s `AdminMode=True` — pro vývojáře.
- Reuse strategie v EDISONMAIN2: stejně jako Ctrl+W+W — zachovat zkratku,
  otevřít existující WinForms okno (beze změny). Reimplementace do WPF
  nepatří do scope shellu.

**Žádné další skryté zkratky v `frmNoMDI`** nejsou — jen tyto dvě a pak
standardní klávesy jako Insert/Delete v Oblíbených, Enter v TreeView atd.

### 15.2 Odpovědnosti EDISONMAIN2

- Zachovat Ctrl+W+W chord a Ctrl+Shift+B jako globální zkratky v `MainWindow`.
- Pro Ctrl+W+W — zachovat stejný password-prompt flow (včetně date-dependent
  vzorce) kvůli kontinuitě zvyklostí M-line vývojářů.
- Obě okna (`formUTILITIESMain`, `formEDISONMAINTaskList`) **zachovat
  v původní WinForms formě** a jen je z EDISONMAIN2 otevřít. Přepsání do WPF
  je low-priority (málo používané, mostly internal tooling).
- Pokud by časem WPF reimplementace dávala smysl, je to **samostatný projekt**
  mimo rozsah migrace shellu.

---

## 16. MLRest klient a `IMLRest` abstrakce

EDISONMAIN2 **MUSÍ** poskytovat inicializovaný `IMLRest` přes `EDServices.Current`.
Podrobnosti viz [CLAUDE.md](../CLAUDE.md) — "MLRest — použití v .NET".

**Odpovědnosti shellu:**

- Vytvořit `MLRest` klienta po připojení k serveru — **jedinou instanci na proces**.
- Nastavit `Mlp`, `Idfa`, `DataNamespace`, `Delimiter` po přihlášení / přepnutí firmy.
- Zaregistrovat do `EDServices.Current`.
- Poskytovat binární upload/download (`BinarySave` / `BinaryLoad`).
- **Udržovat stabilní `GUID`** napříč všemi hostovanými okny (viz § 3.7 — staré
  MLRest generovalo nový GUID pro každou instanci, což rozbíjelo ACTIVITY zámek).
  EDISONMAIN2 má z definice jediný singleton a žádné okno si vlastní MLRest
  nevytváří.
- Reagovat na event `EdisonActivityFailed` (§ 3.5) — server signalizuje, že
  ACTIVITY zámek zmizel. Shell musí uživatele odhlásit / nabídnout re-login.
- Paralelní alternativa: `EdisonCacheMcp` (.NET 10, MCP-wrapper nad MLRest) — nová
  integrační vrstva určená pro AI agenty a sdílené služby. Zatím ne kritická pro
  EDISONMAIN2, ale bude ji sdílet.

---

## 17. Sdílená UI infrastruktura

EDISONMAIN2 mergne do `App.xaml` resource dictionaries z `EDControls`:
- `EDIcons.xaml` — ikony
- `EDStrings.xaml` — sdílené texty tlačítek
- stylování společných komponent (EDFooter, EDToolbar, EDInputForm, EDList,
  EDActionInProgress)

**Odpovědnosti:**
- Mergovat správné resource dictionaries v `App.xaml` (jinak `{StaticResource}`
  vyhodí runtime výjimku).
- Mít `DispatcherUnhandledException` handler s `e.Handled = true`, aby aplikace
  po výjimce nespadla.
- Poskytovat `WindowNavigator`, `PopupDisplayer`, `EDServices` na úrovni
  aplikace.
- Base třída `EDViewModel` — `Command(...)` helper s error handlingem,
  `MLRest` property z `EDServices.Current`.

---

## 18. Koexistence EDISONMAIN ↔ EDISONMAIN2

Po nějakou dobu budou oba klienti běžet paralelně — část uživatelů na staré
VB.NET verzi, část na nové WPF verzi — proti **téže Caché instanci**. Migrace
není one-shot přepnutí; je to postupný roll-out, který může trvat měsíce.
Všechno, co se Caché týká, musí oba klienty bezešvě obsloužit.

### 18.1 Co je sdílené (a fungovat to bude)

| Komponenta | Sdílená forma |
| --- | --- |
| **`MLRest.dll`** | Totožný soubor `C:\Edison\MLRest.dll`, obě verze ho referencují. Wire formát (CVar / CList / numerické subscripty) je identický. EDISONMAIN2 preferuje `CVar`, ale na Caché straně se to serializuje stejně. |
| **Caché třídy `ALWA.SYS.*`, `ALVA.*`, `ALVA.SYS.*`** | Sdílené backendové třídy (Login, Activity, Rest, Lock). Upgrade se řeší na Caché serveru, ne v klientech. |
| **ACTIVITY zámky** | Oba klienty volají `Activity.LockSet(idfa, ip, guid)` stejně. Zámky se ve sdíleném globalu prolínají (viz § 3.4). |
| **Business zámky** | Pokud EDISONMAIN2 používá stejný pattern `<className>#<id>` (viz § 3.4), oba klienty si zámky vidí a respektují je. |
| **Licence** | Čteno z `^[ALVA.LICENCE]LICENCE`, kompatibilní pro oba (§ 13). |
| **Caché REST endpoint** | Stejný port, stejné autentizační flow. |

### 18.2 Kde budou nové problémy

**Identifikace klienta:**

- `Login.cls:66` ukládá `ClientExecutableName="EDISON"` — **nelze rozlišit EDISONMAIN
  od EDISONMAIN2 sessiony**.
- Admin v `formUSERSActivity.vb` vidí obě, ale nepozná je. Pro diagnostiku
  (který klient má problém s updatem, který volal legacy API atd.) je to slepé.
- **Doporučení:** do ACTIVITY metadata přidat pole `ClientKind = "EDISONMAIN" | "EDISONMAIN2"`,
  ukládané při `Activity.UpdateInfo`. Stará admin konzole to ignoruje, nová
  konzole to použije pro filtraci.

**Version compatibility check:**

- `ALWA.SYS.Rest.CheckVersion()` porovnává `clientVersion` (datum + číslo
  v `VERZE.INF`) s `ALVA.SYSTEM.Main.GetVersion()`. Při nesouladu hází
  `"EdisonVersionFailed!"`.
- EDISONMAIN2 musí serveru hlásit **kompatibilní** verzi, nebo server musí
  akceptovat obě. **Dnes zřejmě ne** — je to potřeba vyřešit před prvním
  paralelním nasazením.
- **Doporučení:** změnit check na seznam "podporovaných verzí klienta"
  (per-klient type), nebo obejít check pro EDISONMAIN2 (hlásí se jako
  samostatný typ a má vlastní verzi).

**Updater:**

- EDISONMAIN2 **dnes nemá** žádný update mechanismus (v `C:\MLineRepos\EDISON2\`
  neexistuje analog `clsUpdate.vb` ani ekvivalent `EDUPDATE.EXE`).
- Stávající `EDUPDATE.EXE` je WinForms, nelze ho přímo sdílet.
- Distribuční server (`\\...\Webis\EDISON\RELEASE\`) dnes hostuje jen EDISONMAIN
  binárky. Musí přibýt `/EDISONMAIN2/RELEASE/` endpoint.
- **Rozhodnutí:** EDISONMAIN2 buď dostane **vlastní** updater (s vlastní
  `VERZE.INF`, vlastní složkou, ale spolehlivě fungující se stejnou
  `VzdaleneUkonceniEdisona` mechanikou jako stará — viz § 12.3), nebo se sdílí
  jeden updater který umí distribuovat obě binárky souběžně. **TBD.**

**Licenční knihovna:**

- `EDLicence.dll` je .NET Framework 4.8 — EDISONMAIN2 (.NET 8+) ho **nemůže**
  přímo linkovat.
- Varianty:
  - Port `EDLicence` do .NET Standard 2.0 (běží v Framework i Core).
  - EDISONMAIN2 si sáhne na licenci přímo přes MLRest (`ALVA.LICENCE.*` Caché metody)
    a nepoužije lokální DLL.
- **Preferovaný směr:** varianta 2 (Caché je zdroj pravdy). Legacy
  `EDLicence.dll` zůstává pro EDISONMAIN.

**Stav (Registry vs. appsettings.json):**

- EDISONMAIN: `HKCU\Software\M-Line\EDISON\*` — velikosti oken, oblíbené, atd.
- EDISONMAIN2: `appsettings.json` a/nebo vlastní user-level store.
- **Žádný konflikt**, ale taky **žádný sdílený stav**. Uživatel přepínající mezi
  verzemi si layout / oblíbené nepřenese.
- **Doporučení:** pro kritické user state (oblíbené, startup form) ukládat
  do Caché per `Mlp` — oba klienty pak čtou tentýž zdroj. Pro layout oken
  (pozice, velikost) může zůstat per-klient.

**Filesystem a single-file publish:**

- EDISONMAIN v `C:\Edison\` má volné DLL. EDISONMAIN2 má `dotnet publish
  --self-contained` s single-file výstupem — žádné volné DLL.
- Oba klienti mohou žít vedle sebe v `C:\Edison\` bez konfliktu DLL.
- `MLRest.dll` je stále sdílené (oba ho referencují z `C:\Edison\`).

### 18.3 Kritéria pro "roll-out ready"

Před tím, než pustíme EDISONMAIN2 do produkčního paralelního provozu,
**musí být splněno**:

1. **Oba klienty se přihlásí** do stejné Caché instance bez chyby "EdisonVersionFailed!".
2. **ACTIVITY zámky obou klientů** se v `^||ML(...)` neprolínají (nesdílejí
   náhodně GUID), admin vidí obě sessiony a dokáže je individuálně odhlásit.
3. **Business zámek vytvořený v EDISONMAIN2** uvidí uživatel v EDISONMAIN
   (a naopak) — testováno na konkrétní entitě (např. firma, zákazník, faktura).
4. **Update mechanismus** — admin může vydat update EDISONMAIN i EDISONMAIN2 tak,
   aby se nerozbíjely sessiony druhého klienta (§ 12.3 system-wide update lock
   musí být implementovaný, nebo dokumentovaný workaround).
5. **Licence** funguje pro oba klienty — EDISONMAIN2 se "podívá" na `^[ALVA.LICENCE]LICENCE`
   přes MLRest a dostane stejnou licenci jako EDISONMAIN.
6. **Legacy workaround** `UNDONE: 2021-07-07` v `formUSERSActivity.cls`
   (řádky 16-18) — buď odstranit, nebo ověřit že neomezuje EDISONMAIN2 klienta.

### 18.4 Migrační timeline — high level

Dokument nepopisuje konkrétní termíny (to je jiný dokument), ale **pořadí musí být:**

1. Caché backend rozšířit o `ClientKind` v ACTIVITY metadata + seznam
   podporovaných klientů ve `CheckVersion`.
2. EDISONMAIN2 updater — implementovat.
3. Pilotní nasazení na vybraných stanicích (typicky interní M-line) obou klientů.
4. Postupná migrace modulů z EDISONMAIN do EDISONMAIN2, sdílení stejného shell
   běhu.
5. Deprecation EDISONMAIN pro novou práci, read-only režim, pak sunset.

---

## 19. Hostované agendy (referenční výčet — **nikoliv odpovědnost EDISONMAIN2**)

Pro úplnost — EDISONMAIN2 jen spouští, zbytek je v sesterských projektech.
Počty formulářů jsou přibližné (VB.NET original, nikoliv migrovaný stav).

| Doména | Projekty | ~počet form |
|---|---|---|
| Jízdní řády — linky/spoje/trasy | `JRMAIN`, `JRLIC`, `JRSETTINGS` | 40+ |
| Jízdní řády — zastávky | `JRZAST` | 40+ |
| Tisk JR | `TISKJR`, `TISKJRRELACE`, `TiskJR`, `TiskJRRELACE` | 15+ |
| Řidiči | `RIDICI` | 10 |
| Vozidla | `VOZIDLA2`, `VOZIDLA2API` | 40+ |
| Zákazníci | `ZAKAZNICI2`, `ZAKAZNICI2API` | ~20 |
| Mzdy a personalistika | `MZDY2`, `MZDY2API`, `MZDYISIR` | 140+ |
| Účetnictví | `ESMAIN`, `ESUTILITY`, `ESSALDO`, `ESFINANCOVANI`, `ESKOMPENZACE`, `ESLUZBY` | 60+ |
| Turnusy a plánování dopravy | `ADTURNUSY`, `ADPLANDOPRAVY`, `ADPLAN`, `ADCALCULATOR` | 40+ |
| Dispečink | `ADZPRACOVANI`, `ADDISPECING` | 15+ |
| MHD | `MHD2Ciselniky`, `MHD2Linky`, `MHD2Ridici`, `MHD2Tisk`, `MHD2PlanDopravy`, `TiskMHD` | 60+ |
| Docházka | `DOCHAZKA` | 13 |
| Jízdenky a tarify | `TICKETS` | 35+ |
| Datový sklad | `DSSETUP`, `DSREPORT`, `DSVIEWER`, `DSINPUT`, `DSPODILOVANI` | 30+ |
| Majetek (HIM) | `HIM` | 26 |
| Dotace | `DOTACE` | 18 |
| Výběrová řízení | `EVIDENCEVYBEROVYCHRIZENI` | 38+ |
| Pokladna / prodej | `POKLADNA`, `POKLADNAEX`, `PREPRAVNIPOKLADNA`, `PRODEJ` | ? |
| Autoopravna / servis | `AUTOOPRAVNA`, `AUTOOPRAVNA2`, `EMSERVISIMPORT` | ? |
| EET (elektronická evidence tržeb) | `EETService` | ? |
| TelMax (karty / odbavovač) | `TELMAX`, `TELMAX2` | 20+ |
| Vlaky | `VLAKY2` | ? |
| Mapy / GPS | `MAPY`, `MAPY2` (CefSharp) | ~10 |
| ISY (integrovaný systém) | `ISY*` | ? |
| ISDS (datové schránky) | `ISDSAPI`, `ISDSFRONTEND` | ? |
| Firma (struktura, střediska) | `FIRMA2`, `FIRMA2API` | ? |
| Uživatelé | `USERS`, `USERS2` | ? |
| Tisk/Reporty (generic) | `REPORTGENERATOR`, `EDREPORTS`, `FAKREPORT`, `TISK`, `TISK2` | 30+ |
| Podpora / helpdesk | `EDHELPDESKB`, `FEEDBACK`, `AUDIT` | ~10 |
| Business case optimalizace | `BCOPTIM1`, `BCSTANDARD`, `BCCENAJIZDNEHO`, `BCINTDOP`, `BCCLEANING` | 20+ |
| Nákladní doprava | `NDMAIN`, `NDOBJEDNAVKY` | ? |
| Task management | `TASKMANAGER` | ? |
| Události / Sherlock / Trees | `UDALOSTI`, `SHERLOCK`, `TREES` | ? |
| Obecné utility | `GENERAL`, `UTILITY`, `IMPORT` | ? |

**Celkem přes 650 formulářů** v `EDISFormEnum` — ne všechny jsou aktivní, některé jsou
obsolete / archiv. Výběr toho, co z toho migrovat do EDISONMAIN2, je samostatná
otázka mimo rozsah tohoto dokumentu.

---

## 20. Externí závislosti shellu

### Placené / komerční
- **ComponentOne (C1)** 4.5.2 / 4.6 — reporting, PDF, Excel, grid, barcode,
  schedule, print preview. Licence musí být řešena pro EDISONMAIN2 zvlášť
  (pokračovat / nahradit / vyvinout vlastní).

### Open-source
- `EPPlus` (starší verze, Excel export)
- `ClosedXML` 0.96 (alternativa pro Excel)
- `DocumentFormat.OpenXml` 3.2 (Word)
- `PdfSharp` 6.1.1 (PDF, v `TISK2`)
- `CefSharp` 105.3 (Chromium embedded, pro mapy)
- `Newtonsoft.Json`

### Interní ALVA DLL (proprietární)
`EDLibrary`, `EDLibraryEx`, `EDCore`, `EDCOREMAIN`, `EDComponents`, `EDComponents2`,
`EDForms`, `EDAPI`, `EDISONAPI`, `EDUsers`, `EDLicence`, `EDREPORTS`, `MLRest`,
`MLServer`, `GENERALAPI`, `FIRMA2API`.

### Caché konektory
- `InterSystems.Data.CacheClient` (ADO.NET driver) — používán přímo pro specifické
  operace mimo MLRest.
- `MLRest.dll` — hlavní kanál.

---

## 21. Hardware integrace (z EDISONMAIN2 obvykle jen zprostředkované)

- Tiskárny — přes C1PrintDocument + Windows print spooler.
- Termotiskárny jízdenek — řešeno v `TISK2` / `POKLADNA*` modulech.
- Čtečky čárových kódů — HID (emulace klávesnice).
- RFID / čipové karty — přes `TELMAX*` / `POKLADNA*`.
- GPS / palubní počítače — přes `VOZIDLA2API` / `MHDZpracovaniAPI`.

Shell sám hardware typicky neovládá, pouze hostí modul který ho obsluhuje.

---

## 22. Co tento dokument **nepopisuje** (vědomě)

- Implementační detaily a kód hostovaných modulů (JR, MZDY, TICKETS, …).
- Business logiku v Caché (má vlastní dokumentaci — `MLRest.md`).
- Konkrétní migrační roadmap / pořadí přepisu — je to jiný dokument.
- UI design guide — také mimo rozsah.

---

## 23. Otevřené otázky / TBD

- Ponechat vlastní updater nebo standardní řešení?
- MDI-style docking vs. samostatná okna pro hostovaná okna v EDISONMAIN2?
- Kompletní lokalizační infrastruktura (Caché → .NET) — strategie?
- Strategie pro licenční knihovnu (držet `EDLicence.dll` nebo přepsat)?
- C1 reporting — zachovat, nebo přepsat (Telerik / DevExpress / vlastní)?
- **Licenční formáty** (§ 13.3) — zachovat všech 7 verzí (1.001 → 2.002) pro
  kompatibilitu s nasazenými licencemi, nebo rebase na novou verzi?
- **Licenční šifrování** (§ 13.2) — statický klíč `"mline2k9"` + 3DES je dnes slabý;
  upgrade na AES-GCM s rotovatelným klíčem?
- **Runtime license lock** (§ 13.8) — zavést kontrolu expirace za běhu session
  (mezi requesty MLRest), nebo ponechat "projeví se až při dalším loginu"?
- **Permission pro `AddLicense`** (§ 13.7) — dnes stačí mít přístup do Nastavení;
  přidat explicitní licencí nebo ECL-podmíněné oprávnění?
- **Offline režim** pro M-line refresh (§ 13.6) — jak se chovat u zákazníků bez
  trvalé konektivity na M-line cloud?
- **ACTIVITY lock v EDISONMAIN2** (§ 3.4-3.8) — udržovat 1:1 stávající mechaniku
  (LockSet při loginu, UpdateHistory per request, LockDel při logoutu), nebo
  přepsat na moderní "session token + server-side expirace" model?
- **GUID singleton invariant** (§ 3.7) — jak vynutit, že žádný modul nevytvoří
  vlastní `MLRest`? Code analyzer? Private constructor + factory? Architektonická
  dokumentace v CLAUDE.md?
- **Multi-login blokace** (§ 3.8) — oživit stávající zakomentovaný check
  `MESSAGE_UserDatabaseCannotBeChanged`, nebo povolit multi-session jako dnes?
- **Reakce na `EdisonActivityFailed`** (§ 3.5) — vynucený logout, nabídka
  re-loginu se zachováním stavu oken, nebo jiný UX?
- **System-wide update lock** (§ 12.3-12.4) — přidat do Caché global
  `^EDISON("System","Update","InProgress")`? Broadcast na sessiony? Cleanup
  business zámků při force logout?
- **EDISONMAIN2 updater** (§ 18.2) — dnes vůbec neexistuje; rozhodnout, zda
  sdílet stávající `EDUPDATE.EXE` (po úpravě pro .NET 8+), nebo psát vlastní
  (Squirrel / MSIX / ClickOnce / custom).
- **ClientKind v ACTIVITY** (§ 18.2) — přidat pole `ClientKind` do
  `Activity.UpdateInfo`, aby admin rozlišil EDISONMAIN vs. EDISONMAIN2 session?
- **Version compatibility check** (§ 18.2) — upravit `Rest.CheckVersion()` tak,
  aby přijal kompatibilní seznam verzí pro různé klienty (EDISONMAIN i EDISONMAIN2).
- **Licence v EDISONMAIN2** (§ 18.2) — portovat `EDLicence.dll` na .NET Standard,
  nebo jít čistě přes MLRest? Preferovaný: čistě přes MLRest.
- **Sdílení user state mezi klienty** (§ 18.2) — oblíbené, startup form, layout —
  co ukládat do Caché a co může zůstat per-klient?
- **Odstranění legacy workaroundu z 2021** (§ 18.3) v `formUSERSActivity.cls`
  řádky 16-18 — má dnes ještě smysl?
- **Save-and-exit UX pro auto-logout** (§ 3.9-3.10) — nahradit balloon tip
  modálním dialogem s tlačítky "Uložit a odhlásit", "Prodloužit", "Zahodit"?
- **`OnBeforeLogout` callback** (§ 3.9-3.10) pro hostované moduly — návrh API
  (async `Task<bool> OnBeforeLogoutAsync()`?), úložiště rozdělané práce
  (Caché global vs. lokální filesystem vs. příští MLRest request).
- **Windows idle detection** (§ 3.9-3.10) — přidat `GetLastInputInfo` /
  `InputManager.PreProcessInput` jako druhý signál pro pre-warning?
- **Per-user timeout UI** (§ 3.9) — doplnit editor feature #30 v user admin
  formuláři; nebo zachovat dnešní stav (jen globální timeout)?
- **Resume session po re-loginu** (§ 3.10) — obnovit otevřená okna a drafts?
  Kde ukládat (Caché per-user, local profile)?
- **Kick-and-login pro admina** (§ 2.9) — nová Caché metoda
  `ALVA.SYS.Login.KickAndLogin(targetGuid)` + klientský dialog s výběrem
  session k odpojení, autorizace server-side, audit log. Edge case: kick
  uživatele s kritickým business zámkem v transakci.
- **`EDISONini.xml` strategie pro EDISONMAIN2** (§ 11.1-11.2) — sdílet jeden
  soubor s EDISONMAIN, nebo zavést separátní `EDISONMAIN2ini.xml`? Preferovaně
  sdílet pro čtení, EDISONMAIN2 vlastní pro zápis do full cutoveru.
- **`IRIS` flag** (§ 11.1) — má smysl v EDISONMAIN2, který `MLServer` nepoužívá?
  Pokud ne, přezkoumat jako mrtvý tag.
- **Sdílený vs. per-klient `securedMode`** (§ 2.6, § 11.1) — když běží
  EDISONMAIN a EDISONMAIN2 vedle sebe a sdílejí XML, `securedMode` se aplikuje
  na oba; je to žádoucí, nebo by měl každý klient mít vlastní secured mode?
- **Migrace navigačních stromů z XML do Caché** (§ 5) — struktura globalu, admin UI
  pro editaci, identifikátory uzlů (zachovat číselné ID z `EDISFormEnum` nebo
  přejít na string-based `"Balík.Formulář"`?), kompatibilita s `license.Elements`.
- **Legacy licenční formát `< 1.003`** (blacklist variant) — ponechat jen whitelist
  (`≥ 1.003`) nebo zachovat obě varianty? Pravděpodobně lze opustit.
