# Aktualizace EDISONu: starý vs. nový shell (mapování stavu)

Stav k 31. 8. 2026; §3 a §5 aktualizovány 8. 9. 2026 po zavedení vlastního updateru
EDUPDATE2. Zdroje: repo EDISON2 (`c:\MLineRepos\EDISON2`), legacy repo EDISON
(`c:\MLineRepos\EDISON`), živé ověření proti dev Caché `192.168.16.1:57770` a release
serveru `release.m-line.cz` (viz §7). Odkazy `EDISON/...` míří do legacy repa,
ostatní do EDISON2.

Motivací jsou tři známé mouchy starého mechanismu:

- **a)** distribuce jede přes http, ne https,
- **b)** patch (čistě Caché update) občas na klientech způsobí stahování DLL,
- **c)** vynucení aktualizace lokálních klientů občas způsobilo, že shell nešel
  vůbec spustit.

Všechny tři mají v kódu dohledatelnou příčinu (§4). Nový shell mouchu a) dědí
v omezené míře (notice kanál), b) řeší od EDUPDATE2 částečně (vlastní updater je vůči
chybějícímu/oříznutému manifestu odolný, serverovou příčinu v EDISONINS neopravuje)
a mechanismus z c) má od EDUPDATE2 vlastní, bez legacy pastí (§3, §5).

## 1. Řetězec distribuce (společný pro oba shelly)

```
vývojář (VS build, ručně)
  └─> C:\Edison\            staging na stroji vývojáře ("Zdroj" Compileru)
       └─> Compiler.exe     kopie na DEV share + REL FTP (ftp2.m-line.cz
            │                → c:\inetpub\ftp.m-line.cz\compiler\ na Cloud2);
            │                blokace DEBUG assembly; Caché DEV→REL import
            └─> menu Verze → MLRest ALVA.Compiler.Function.runSetupWizard
                 └─> SETUPWIZARD.exe (na Cloud2)   vyrobí verzi do
                      c:\inetpub\release.m-line.cz\edison\release\  (ostrá)
                      c:\inetpub\release.m-line.cz\edison\TEST\     (test)
                      servíruje IIS: http://release.m-line.cz:59993 (i :80)
                       └─> server zákazníka: EDTRANSFER2.exe (stáhne balík,
                            pending=1) → EDISONINS.exe (záloha, rozbalení,
                            kopie binárek, import+kompilace Caché XML,
                            zápis VERZE.INF, bump ^EDISON("System","Update",2),
                            pending=0)
                             └─> stanice: shell detekuje novou verzi →
                                  EDUPDATE.EXE vymění soubory → restart shellu
```

Bez CI: build ručně ve VS, koordinace FULL verzí přes WhatsApp,
`FILES_DESCRIPTION.INF` se edituje ručně přímo na Cloud2.

### Typy verzí a `VERZE.INF`

Nositelem verze je textový soubor `VERZE.INF` (ne assembly verze). Klíče `LAST=`
(číslo verze, schéma `yyMMdd.N[.P]`, N = full, P = patch), `TYPE=`, `V=` (verze
updateru, 2.01 = nové verzování), `BlokovatAktualizace=` (0/1, přepíná Compiler
přes `ALVA.Compiler.Function.BlokovatAktalizace`, SETUPWIZARD při každé nové verzi
resetuje na 0).

| TYPE | Typ | Obsah balíku | Restart klientů |
|---|---|---|---|
| 1 | FULL | vše z manifestu `FILES_DESCRIPTION.INF` (EXE, DLL, XML, GLB, ...) | ano, tvrdá synchronizace |
| 2 | PATCH | jen zaškrtnuté balíčky, může nést EXE/DLL | ano |
| 3 | XMLPATCH | jen XML + INF (Caché-only) | ne (runtime gate toleruje patch segment) |

Patche jsou kumulativní: `PATCHES.INF` ve FULL složce eviduje, které balíčky nese
který patch; nový XML patch převezme balíčky předchozího
(`EDISON/SETUPWIZARD/frmMain.vb:2295-2321`).

### Evidence souborů (co rozhoduje, co se stáhne)

| Soubor | Kdo čte | Granularita | Vzniká |
|---|---|---|---|
| `VERZE.INF` | všichni | verze celého release | SETUPWIZARD; u zákazníka přepisuje EDISONINS |
| `_filelist.inf` (ve verzní složce) | EDTRANSFER2 | statický seznam balíku | SETUPWIZARD (`frmMain.vb:1865-1902, 2512-2549, 2685-2722`) |
| `DEFINITION.DAT` (v DEFINITION.zip) | EDISONINS | soubory dané verze | SETUPWIZARD z manifestu |
| `EDISONCRC.bin` | EDUPDATE na stanici | **jediná per-soubor evidence** (SHA-256, TripleDES-šifrovaná) | EDISONINS u zákazníka (`EDISON/EDISONAPI/clsUpdater.vb:3061-3130`) |
| `_ListFiles/<yyMMdd.N>/ListFiles.txt` | EDUPDATE (fallback pro HTTP zdroj bez CRC) | řádky `TYP~[podadr~]soubor` | SETUPWIZARD **jen u FULL** (`frmMain.vb:2001-2002`) |

### Caché globály (u zákazníka i na dev)

Mapa v `EDISON/EDISONINS/CACHE/ALVA/SETUP/API.cls:12-36`; klientům je servíruje
`ALVA.SETUP.Main.LoadSettings` (`EDISON/EDISONINS/CACHE/ALVA/SETUP/Main.cls:7-72`).
Binárky v Caché nejsou, distribuce je čistě filesystem + IIS/FTP.

| Global | Význam |
|---|---|
| `^EDISON("System","Update",0)` | $lb(povoleno, isNewUpdate) - dev má povoleno=0 |
| `...,1)` | pending lock (probíhá serverová instalace, zákaz loginu) |
| `...,2)` | verze naposledy **nainstalovaného release** (zdroj runtime gate) |
| `...,3)`/`...,7)` | adresář Edisona / staging instalace |
| `...,4)`/`...,5)`/`...,6)` | zdrojový server (`release.m-line.cz`) / port (`59993`) / adresář |
| `...,8,N)` | lokální adresáře klientů |
| `...,10)`/`...,11)` | autoDownload+autoInstall / localSourcePath |
| `...,"NucenyUpdateLocalClientu")` | $lb($h, "dd.mm.yyyy hh:mm:ss") - vynucená aktualizace klientů (§2, §4c) |

## 2. Starý shell (stanice)

Aktéři: `EDISON.exe` (EDLOADER) → `EDISONMAIN.exe` (detekce + rozhodnutí) →
`EDUPDATE.exe` (výměna souborů).

### 2.1 EDLOADER (`EDISON/EDLOADER/clsStartup.vb:436`)

1. Existuje-li `corefiles_install.tmp` nebo nezapisovatelný `*.tmp` → hlášení a konec.
   Core files = jen `EDISON.exe` a `EDCore.dll` (`:348`) - vyměňují se přes `.tmp`,
   protože EDLOADER sám sebe nahradit nemůže.
2. `UpdateIsRunning` (`:211`) - dotaz `ALVA.SETUP.Main.LoadSettings`, klíč `pending`:
   probíhá-li serverová instalace, klient se nespustí.
3. `ReplaceCoreFiles` (`:389`) - `.tmp` → `.exe/.dll`, 5 pokusů po 1 s.
4. `EDISONMAIN.EXE reason=2`.

### 2.2 Detekce: `Update.UpdateNeeded()` (`EDISON/EDISONMAIN/clsUpdate.vb:623`)

Volá se po loginu (`EDISON/EDISONMAIN/frmNoMDI.vb:3324, 4917` →
`CheckForUpdate`, `:4400`). Nastavení z `ALVA.SETUP.Main.LoadSettings`
(`clsUpdate.vb:1017`, klíče `update`, `pending`, `version`, `edisonPath`,
`sourceServer/Port`, `localSourcePath`, `NucenyUpdatClentu`, ...).

Dvě větve dle `IsServer` (`:222`, hostname/IP proti seznamu LocalPaths):

- **Server**: stáhne `VERZE.INF` z M-line (`GetRemoteVersion`, `:863`);
  `BlokovatAktualizace` → konec; novější verze → `NovaVerzeNaZdrojovemServeru`.
- **Klient** (`:661-689`): porovná lokální `VERZE.INF` (klíč `LAST`) proti
  `VERZE.INF` v `LocalClientSourcePath` (UNC share nebo http); novější →
  `NovaVerzeNaCilovemServeru`. Pak vynucený update (§2.4). Porovnává se **jen
  číslo verze** (`IsNewVersion`, `:960`), hashe až per-soubor v EDUPDATE.
- **Společné** (`:691-711`): prázdná lokální verze → `NelzeZjistitEdisonovouVerzi`;
  `CacheVersion <> EdisonVersion` → `NesediVerzeCache`; `LAST_INSTALL_STATE=0`
  v `EDISON.INF` → `ChybaAktualizace`.

Pro klienta je dialog vždy `ForceYes` (`frmNoMDI.vb:4446`) - buď se aktualizuje,
nebo se shell zavře (`CloseEdison`).

### 2.3 Instalace: EDUPDATE (`EDISON/UPDATERCL/clsAPI.vb`)

Shell nejdřív překopíruje/stáhne samotný `EDUPDATE.EXE`
(`UpdateClientUpdater`, `clsUpdate.vb:590`), pak ho spustí s argumenty
(`clsUpdate.vb:799-818`): `FormMode Async RunEdison SourcePath TargetPath Version
NucenyUpdate NucenyUpdateTime` - a ukončí se. EDUPDATE (`StartUpdate`, `:1235`):

1. busy-wait na zánik procesů `EDISONMAIN` / `EDTRAYMAIN` (podle jména),
2. `GetEdisonFiles` (`:1064`) - **rozhodovací bod per soubor**:
   - existuje `EDISONCRC.bin` a NENÍ `NucenyUpdate` → diferenčně: stáhne jen
     soubory, jejichž lokální SHA-256 nesedí se záznamem; klíčové soubory
     (`MLServerConfig-data.xml`, `EDISONini.xml`, `VERZE.inf`, `EDISONCRC.bin`)
     vždy; `EDUPDATE.exe` nikdy,
   - jinak → **zkopíruj/stáhni všechno** (UNC: celý adresář + `EDFiles`, `PS`,
     `locales`; HTTP: dle `ListFiles.txt`, přeskočí jen GLB/XML/TTF),
3. `BackupFiles` (`:1367`) - do `BKP_Files` a **cílové soubory smaže**,
4. `InstallEdisonFiles` (`:1428`) - `File.Copy` nebo HTTP download; chyba →
   `RestoreFiles` (`:1513`, chyby obnovy jen loguje),
5. zápis `CliUp.ini` při vynuceném updatu (`:1309`), restart `EDISON.exe`
   (`EDISON/UPDATERCL/frmClientUpdate.vb:462` → `RunEdison`, `:86`).

`VERZE.INF` EDUPDATE nikdy nepíše, jen kopíruje ze zdroje.

### 2.4 Vynucená aktualizace lokálních klientů ("NucenyUpdateLocalClientu")

Účel: když vydáme chybu, nahraje se opravený exe/dll do serverového adresáře
Edisona u zákazníka a vynutí se, aby si ho stanice při dalším startu stáhly -
uživatelé dotčeného modulu jen restartují shell, ostatní pracují dál. Bez toho
by oprava vyžadovala plné vydání a zastavení práce všech.

- **Spouštěč**: tlačítko "Aktualizace lokálních klientů" v legacy Nastavení,
  viditelné jen pro `UserIsAdmin AndAlso LoginName = "mline"`
  (`EDISON/EDISONMAIN/formEDISONMAINSettingsShow.vb:400, 2049`).
- **Server**: `ALVA.EDISON.Main.NucenyUpdateLocalClientu`
  (`EDISON/EDISONMAIN/CACHE/ALVA/EDISON/Main.cls:2365`) - kill + set globálu
  `^EDISON("System","Update","NucenyUpdateLocalClientu")` = $lb($h, čitelné datum).
  Bez parametru, nelze jím mazat; globál zůstává navěky (na dev je razítko
  z 8. 9. 2021, ověřeno naživo).
- **Klient**: `NutnyClientUpdate` (`clsUpdate.vb:250-270`) porovná razítko
  s `<StartupPath>\CliUp.ini` (řádek `Spuštěn=<horolog>`); soubor chybí nebo
  razítko nesedí → `UpdateReasonEnum.NucenyUpdate` → EDUPDATE s
  `NucenyUpdate="True"`.
- **Efekt**: `NucenyUpdate` obchází CRC diff i porovnání verzí
  (`EDISON/UPDATERCL/clsAPI.vb:1095, 1272`) → stanice stáhne **všechno**.
  Po úspěchu zapíše `CliUp.ini` (`WriteNucUp`, `:1219-1226`) - tím se pro danou
  stanici vlna "vypne".

## 3. Nový shell (EDISONMAIN2)

Klientská detekce je hotová od 12. 6. 2026 (commit `d60446bd`); serverová
instalace i výroba verze zůstávají legacy beze změny. Od 8. 9. 2026 dělá fyzickou
výměnu souborů **vlastní `EDUPDATE2.exe`** (projekt `EDUPDATE2/`, kontrakt
v `EDUPDATE2/CLAUDE.md`): staging do přechodné složky, swap se zálohou, rollback,
čtení legacy manifestu `EDISONCRC.bin` zůstává bit-kompatibilní. Legacy `EDUPDATE.EXE`
slouží už jen starému shellu. Původní návrhový doc `EDISONMAIN2/docs/UPDATE_FLOW.md`
(neudržovaný) popisuje stav před EDUPDATE2.

### 3.1 Tok

1. Po loginu a licenci `AppBootstrapper.CheckForUpdate`
   (`EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:105-111`, definice `:139-169`);
   záměrně až po `SavePostLoginState`, aby LastIdfa přežila ukončení updatem.
2. `Updater.LoadSettings` (`EDISONMAIN2/dotnet/Startup/Updater.cs:55-78`) -
   totéž `ALVA.SETUP.Main.LoadSettings` jako legacy.
3. `Updater.Check`, v tomto pořadí:
   `!Enabled` → nic (vědomá divergence: legacy klient Enabled ignoruje) →
   dev guard (lokální verze `999999.9` u DEBUG buildu) →
   chybí lokální/serverová verze → `Broken` (popup + konec) →
   novější serverová → `UpdateRequired` →
   **razítko vynucené vlny nesedí s lokální stopou → `UpdateRequired` s `Forced`** →
   `CacheVersion` nesedí → `Broken`.
4. `UpdateRequired`: mandatory popup → `CopyUpdater` (self-update EDUPDATE2) →
   `LaunchUpdate` - tokeny `Key=value` přes `ArgumentList` (`SourcePath`, `TargetPath`,
   `Version`, `WaitProcess`, `RunExe`, `NucenyUpdate=1/0`, `NucenyUpdateTime=<razítko>`;
   razítko se posílá, kdykoli ho shell zná) - a `Shutdown()`.
5. Verze: `VerzeInf.cs` (port `clsUpdate.vb`) - `VERZE.INF` vedle exe,
   dev verze `999999.9`, po-složkové porovnání.

### 3.2 Runtime pojistky

- Per-request version gate: `MLRest.EdisonVersion = VerzeInf.Current`
  (`EDISONMAIN2/dotnet/Startup/SessionFactory.cs:27-40`; DEBUG posílá prázdnou);
  mismatch proti `^EDISON("System","Update",2)` → `EdisonVersionFailed` → popup
  a Force shutdown (`AppBootstrapper.cs:387-392`). Gate běží jen při `CacheLocks=1`
  a porovnává jen segmenty datum+full - XMLPATCH běžící klienty nezabíjí.
- Notice-only kanál: HTTP GET
  `http://{sourceServer}:{sourcePort}/EDISON/RELEASE/VERZE.INF` (`Updater.cs:161`)
  na pozadí plní chip "Nová verze" ve status baru; nikdy nespouští update.
- Changelog: `_DIFF.INF` soubory roznášené updaterem
  (`EDISONMAIN2/dotnet/Windows/ChangelogWindow/InfChangelogSource.cs`), snooze
  přes SHA-256 obsahu.

### 3.3 Co nový shell nemá / co je neuzavřené

- **Vynucená aktualizace existuje od 8. 9. 2026**: razítko `NucenyUpdatClentu`
  z LoadSettings → `Updater.Check` (`Forced`) → EDUPDATE2 plná kopie → stopa
  `CliUp2.ini` vedle binárek (bezpečná náhrada legacy `CliUp.ini`). Spouštěč zůstává
  legacy tlačítko "Aktualizace lokálních klientů" (mline admin).
- Konfigurační UI update nastavení (legacy tab v Nastavení) chybí.
- `ignoreNotify`, dialog detailu verze (`POPIS_VERZE.INF`) - mimo scope.
- Neprovedené E2E ověření: plný update stanice (stará verze × novější serverová
  instalace), forced vlna end-to-end a rozladění runtime gate na sdíleném dev serveru.
- Ops prerekvizity: `EDISONMAIN2.exe`, `EDControls.dll`, `TemplateDesigner.exe`
  a `EDUPDATE2.exe` **nejsou** ve `FILES_DESCRIPTION.INF` na Cloud2 (v repo kopii je
  jen `EDISONMAIN2API.dll`, omylem pod typem EXE) - bez FULL řádků se k zákazníkům
  nedostanou. Upravený legacy `EDUPDATE.EXE` (WaitProcess/RunExe) už nový shell
  nepotřebuje. Seznam binárek vydání: `DEPLOY.md`.
- `MayUpdate` (USERS "UpdateEdison") je jen **právo spustit serverový update**
  z Nastavení - do legacy hosta jde jako launch arg pole [15]
  (`EDControls/dotnet/EDViewModel/WindowNavigator.cs:441-444`); s klientským
  updatem stanice nesouvisí.

## 4. Mouchy a jejich příčiny

### 4a) http místo https

Celý řetězec je plain http a https není nikde konfigurovatelné:

| Místo | Kód |
|---|---|
| `EDISON/EDISONMAIN/clsUpdate.vb:203-207` | `"http://" & SourceServer & ":" & SourctPort & "/EDISON/RELEASE"` |
| `EDISON/EDISONAPI/clsUpdater.vb:1398,1401` | totéž pro serverovou instalaci |
| `EDISON/UPDATERCL/clsAPI.vb:1126` | `http://release.m-line.cz/edison/release/_ListFiles/...` - **natvrdo včetně hostname a bez portu** (funguje jen proto, že IIS poslouchá i na :80, ověřeno naživo) |
| `EDISON/Compiler/Compiler/frmSelectPatch.cs:17` | `http://release.m-line.cz:59993/edison/` |
| `EDISON/EDISONINS/CACHE/ALVA/SETUP/API.cls:38-40` | default `release.m-line.cz` / `59993` |
| `EDISONMAIN2/dotnet/Startup/Updater.cs:161` | notice-only kanál nového shellu, `$"http://{...}"` |

Pasti pro případný přechod: `UriFileInfo.DirectoryFullName`
(`EDISON/UPDATERCL/clsAPI.vb:783`) opravuje jen `http:/` → `http://` (https by
rozbilo URL), transport je `HttpWebRequest` bez nastavení
`ServicePointManager.SecurityProtocol` (.NET 4.8 = default OS, na starých
strojích riziko TLS handshake failu), a release server dnes https vůbec
neposkytuje (`:59993` odmítne TLS handshake, na :443 nic neposlouchá - ověřeno
naživo). Https tedy vyžaduje změny na serveru (certifikát, binding) i ve všech
šesti místech kódu vč. nového shellu.

### 4b) patch občas stáhne DLL

Tři nezávislé mechanismy, seřazené od deterministického k "občas":

1. **`_filelist.inf` každé verze nese 13 binárek instalátoru - vždy, i XMLPATCH.**
   SETUPWIZARD do každé verzní složky kopíruje z REL FTP
   (`c:\inetpub\ftp.m-line.cz\compiler\`) instalátorové binárky (7za.exe,
   EDISONINS.exe, EDCore.dll, EDLibrary(Ex).dll, EDUsers.dll, MLServer.dll,
   MLRest.dll, Newtonsoft.Json.dll, EDISONAPI.dll, CacheClient, C1.Win.C1Sizer)
   a zapíše je do `_filelist.inf` (`EDISON/SETUPWIZARD/frmMain.vb:2685-2722`).
   EDTRANSFER2 stahuje přesně tento seznam → i čistě Caché patch stáhne ~10 DLL
   na **server** zákazníka. Berou se živě z FTP, takže patch je tichý přenašeč
   binárek, které tam kdokoli mezitím nahrál Compilerem. (U XMLPATCH se do
   adresáře Edisona neinstalují - `InstallEdisonFiles` se nevolá - ale stažené
   a v logu updateru vidět jsou.)
2. **Binární PATCH (TYPE=2) zničí `EDISONCRC.bin`.** Serverová instalace CRC
   soubor smaže a přegeneruje **jen ze souborů právě instalovaného balíku**
   (`EDISON/EDISONAPI/clsUpdater.vb:3069-3071` + zápis per soubor `:3117-3129`).
   Patch bez souborových položek → CRC nevznikne vůbec; patch s pár soubory →
   CRC oříznutý jen na ně. Stanice pak (`EDISON/UPDATERCL/clsAPI.vb:1095`)
   spadne do větve "zkopíruj všechno" → **stáhne všechny EXE+DLL**. XMLPATCH
   (TYPE=3) je paradoxně v pořádku - `InstallEdisonFiles` se nevolá a CRC
   z poslední FULL přežije. Protože každý patch bumpne serverový `VERZE.INF`,
   update na stanicích se spustí vždy; kolik se stáhne, rozhoduje výhradně
   existence a obsah `EDISONCRC.bin`.
3. **Vynucená aktualizace vypne diff** (`clsAPI.vb:1095` - `AndAlso Not
   NucenyUpdate`) → plné stažení; totéž stanice, které chybí/nesedí `CliUp.ini`.

Prověrka u zákazníka: datum/velikost `EDISONCRC.bin` v serverovém adresáři po
nasazení patche + řádky `CopyEdisonFile` v `_LOG_UPDATER.txt` na stanici.

### 4c) vynucená aktualizace → shell nejde spustit

Sedm dohledaných mechanismů, jak forced update může zabránit startu (řazeno
podle odhadované pravděpodobnosti):

1. **`CliUp.ini` se zapisuje relativní cestou, čte absolutní.** `WriteNucUp`
   používá `New StreamWriter("CliUp.ini", False)` (`EDISON/UPDATERCL/clsAPI.vb:1221`)
   → soubor vznikne v pracovním adresáři procesu; `NutnyClientUpdate` čte
   `<StartupPath>\CliUp.ini` (`EDISON/EDISONMAIN/clsUpdate.vb:252`).
   `WorkingDirectory` se sice nastavuje, ale zástupce bez "Spustit v", elevace
   nebo relaunch stopu uloží jinam → **nekonečná smyčka update-restart-update**
   (a při každém průchodu plné stažení, viz 4b/3). `CliUp.ini` se navíc zapíše
   jen po úspěchu (`:1309`) - jakákoli chyba updatu = smyčka.
2. **Poškozený `CliUp.ini` shodí shell.** `NutnyClientUpdate` bez try/catch
   indexuje `pole(1)` (`clsUpdate.vb:259-261`); prázdný/oříznutý řádek →
   výjimka → `ErrorState` → a protože klientský dialog je `ForceYes`
   (`EDISON/EDISONMAIN/frmNoMDI.vb:4441-4446, 4492-4498`) → `CloseEdison`.
3. **Busy-wait bez Sleep a timeoutu.** `While Process.GetProcessesByName("EDISONMAIN").Length > 0 : End While`
   (`clsAPI.vb:1536, 1546`) - viselý EDISONMAIN (modální MsgBox, crash dump,
   druhá instance) = EDUPDATE točí 100 % CPU navěky a nikdy nezačne instalovat.
4. **HTTP read smyčka bez ochrany.** `While actualSize < size` čte bez kontroly
   `bytesRead = 0` (`clsAPI.vb:1871-1882`) - přerušené spojení = nekonečná
   smyčka; okno EDUPDATE přitom nejde zavřít, dokud `Pending`
   (`EDISON/UPDATERCL/frmClientUpdate.vb:384-389`).
5. **Smazat dřív než stáhnout.** `BackupFiles` cílové soubory nejdřív smaže
   (`clsAPI.vb:1403-1413`), při vynuceném updatu včetně `EDISON.exe`
   a `EDCore.dll`; selže-li stahování, `RestoreFiles` obnovuje jen to, co se
   stihlo smazat, a chyby obnovy pouze loguje (`:1513-1530`) → částečný update,
   `EDISON.exe` chybí, klient je mrtvý do ručního zásahu.
6. **`ListFiles.txt` na Cloud2.** Fallback URL (`clsAPI.vb:1124-1126`) skládá
   `major.minor` z čísla verze; `_ListFiles\<verze>\` vzniká jen u FULL. Chybí-li
   FULL adresář (úklid, TEST vs RELEASE) → 404 → celý vynucený update selže;
   jednosložková verze → prázdný seznam → update "uspěje" a nezkopíruje nic.
7. **Zavlečený `LAST_INSTALL_STATE=0`.** `EDISON.INF` je typu INF, tedy se při
   plném stažení rozkopíruje ze serveru; měl-li server poslední instalaci
   chybovou (`EDISON/EDISONAPI/clsUpdater.vb:2871`), stanice pak trvale hlásí
   `ChybaAktualizace` → `ForceYes` → cyklus (`clsUpdate.vb:707` +
   `frmNoMDI.vb:4441`). EDUPDATE `EDISON.INF` nikdy neopravuje.

### 4d) Další nálezy mimo zadané mouchy

| Nález | Místo |
|---|---|
| "Změna čísla verze" přepíše `VERZE.INF` jen s `LAST=`, zahodí `TYPE/V/BlokovatAktualizace` → updater spadne do staré logiky verzování a full/patch vyhodnotí chybně | `EDISON/EDISONMAIN/formEDISONMAINInit.vb:94-105` |
| Mrtvý duplikát zápisu CRC do `EDISONCRC.dat` (klient čte `.bin`) - zdroj zmatení | `EDISON/EDISONAPI/clsUpdater.vb:2451-2475` |
| Credentials Cloud2 (`compiler` + heslo) plaintextem ve zdrojáku | `EDISON/SETUPWIZARD/frmMain.vb:211-214` |
| Mrtvý kód `ClientUpdater\` (net8 "Updater" z 2024) - prázdná UI kostra | `EDISON/ClientUpdater/` |
| Launch argumenty legacy hosta se neescapují, jen obalují uvozovkami | `EDControls/dotnet/EDViewModel/WindowNavigator.cs:441-446` |
| `RESPONSIBILITIES.md §12` tvrdí "EDISONMAIN2 nemá update mechanismus" - stale od 12. 6. 2026 | `EDISONMAIN2/RESPONSIBILITIES.md:1170` |

## 5. Parita starý vs. nový shell

| Oblast | Starý shell | Nový shell |
|---|---|---|
| Detekce nové verze | `clsUpdate.UpdateNeeded` (server i klient větev) | `Updater.Check` (jen klient; server = legacy) |
| Výměna souborů | EDUPDATE + `EDISONCRC.bin` diff | vlastní **EDUPDATE2** (týž manifest; staging, rollback, timeouty) |
| `update` (Enabled) flag | klient ignoruje (ForceYes) | honorován (dev server má update~0) |
| Dev prostředí | žádný guard | verze `999999.9` + prázdná `EdisonVersion` v DEBUG |
| Dialog | ForceYes bez volby | mandatory info-popup (ekvivalent) |
| Runtime gate | `maSeUkoncit` polling + verze | per-request `EdisonVersion` gate (CacheLocks=1) |
| Vynucená aktualizace klientů | `NucenyUpdateLocalClientu` + `CliUp.ini` | totéž razítko + `CliUp2.ini` (absolutní cesta, defenzivní čtení) |
| Konfigurace update (UI) | tab v Nastavení | chybí |
| Blokace ze strany M-line | `BlokovatAktualizace` ve `VERZE.INF` | honorováno v notice kanálu |
| Novinky verze | `POPIS_VERZE.INF` dialog | `_DIFF.INF` changelog okno |
| pending lock (instalace na serveru) | EDLOADER odmítne start | neřeší (řeší legacy vrstva serveru) |

## 6. Co z toho plyne (krátká doporučení)

Priority podle poměru dopad/úsilí; nic z toho tento dokument neimplementuje.

1. **Moucha b) má nejlevnější opravu na serveru, ne na klientech**: EDISONINS
   při PATCHi nemazat `EDISONCRC.bin`, ale mergovat (aktualizovat věty souborů
   z balíku, ostatní zachovat) - `clsUpdater.vb:3069-3071`. Jediné místo, žádná
   změna klientů. Přenos 13 instalátorových binárek u XMLPATCH je samostatná
   (menší) věc v SETUPWIZARD.
2. **Moucha c)**: pro nový shell realizováno 8. 9. 2026 (EDUPDATE2 + `CliUp2.ini` -
   absolutní cesta, defenzivní čtení). V legacy zbývá nejlevnější oprava
   `WriteNucUp` na absolutní cestu + ošetření `pole(1)`.
3. **Moucha a)** vyžaduje nejdřív serverovou stranu (certifikát + https binding
   na IIS Cloud2), teprve pak má smysl měnit kód; v novém shellu je to jediné
   místo (`Updater.cs:161`), v legacy šest míst + past v `DirectoryFullName`
   + `SecurityProtocol`.
4. Před nasazením nového shellu k zákazníkům: doplnit `EDISONMAIN2.exe`,
   `EDControls.dll`, `TemplateDesigner.exe` a `EDUPDATE2.exe` (FULL řádky) do
   `FILES_DESCRIPTION.INF` na Cloud2 a provést odložené E2E ověření (§3.3).

## 7. Co bylo ověřeno naživo (31. 8. 2026)

- Dev Caché `192.168.16.1:57770`, namespace MLINE: `^EDISON("System","Update")`
  odpovídá mapě z API.cls - `(2)` prázdná (dev nemá release), `(4)`/`(5)` =
  `release.m-line.cz`/`59993`, `NucenyUpdateLocalClientu` = razítko
  08.09.2021 16:47:29 (globál se skutečně nikdy nemaže).
- `http://release.m-line.cz:59993/EDISON/RELEASE/VERZE.INF` vrací
  `LAST=260825.0.11`, `TYPE=3`, `V=2.01`, `BlokovatAktualizace=0`.
- `https://` na :59993 selže na TLS handshake, na :443 nic neposlouchá.
- Natvrdo zadaná URL bez portu (`clsAPI.vb:1126`) reálně funguje - IIS
  poslouchá i na :80.
- `_ListFiles/260825.0/ListFiles.txt` pro aktuální FULL existuje (HTTP 200).

**Neověřeno** (jen z přečteného kódu): chování EDISONINS/EDUPDATE za běhu
(zejména obsah `EDISONCRC.bin` u zákazníka po patchi - k potvrzení root cause
4b/2 by stačil výpis souboru po nasazení patche), reálné reprodukce scénářů
4c/1-7, obsah `ALVA.Compiler.Function`/`ALVA.SETUPWIZARD` na Cloud2 (třídy
nejsou v repu). Přečtený kód není spuštěný kód.
