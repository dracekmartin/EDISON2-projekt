# Update flow EDISON systému — legacy mechanismus a design pro EDISONMAIN2

Výstup hloubkového průzkumu legacy repa (`c:\MLineRepos\EDISON`, červen 2026). Popisuje, jak
M-line vydává verze, jak se aktualizují servery a stanice zákazníků, a co z toho EDISONMAIN2
přebírá. Reference vedou do legacy repa (plné cesty), pokud není uvedeno jinak.

---

## 1. Princip verzování: verze = release, ne assembly

.NET assembly verze se nebumpují (EDISONMAIN má staticky `2010.1.1.15` —
`EDISONMAIN\My Project\AssemblyInfo.vb:37`). Jediný nosič verze je soubor **`VERZE.INF`**
ležící vedle binárek:

```ini
LAST=20260526.1.0          ; číslo verze
TYPE=1                     ; 1=FULL, 2=PATCH, 3=XMLPATCH
V=2.01                     ; verze instalátoru (2.01 = date-release schéma)
BlokovatAktualizace=0      ; 1 = M-line pozastavil distribuci
```

Konstanty klíčů: `EDISONMAIN\clsUpdate.vb:20-38`. Schéma čísla: **`yyMMdd.N[.P]`** —
datum.pořadíFull.patch (starší schéma `yyyy.MM.xx.yy` stále podporováno,
`clsUpdate.vb:490-518`).

| TYPE | Obsah | Mění segment |
| --- | --- | --- |
| 1 = FULL | všechno (binárky + Caché projekty + globály) | `yyMMdd.N` |
| 2 = PATCH | změněné binárky + Caché (dnes UI cesta zakomentovaná, nepoužívá se) | jen `.P` |
| 3 = XMLPATCH | **jen Caché projekty**, žádné binárky | jen `.P` |

**Klíčový důsledek:** runtime version gate (§5) porovnává jen segmenty datum+full — patch
segment je tolerován. Caché-only změna (XMLPATCH) tedy **nezabije běžící klienty**; dotáhnou
se při příštím startu. Tvrdá synchronizace klient ↔ server se vynucuje jen u FULL verzí
(tradičně jednou týdně v úterý — `README.md:2651-2711`).

Porovnávací algoritmy (`EDISONMAIN\clsUpdate.vb:938-970`):

- `IsNewVersion(new, old)` — po-složkové numerické porovnání; nová verze s více segmenty
  při shodě společných = newer.
- `IsSameVersion(a, b)` — striktní: stejný počet segmentů + textová shoda všech.

---

## 2. M-line release pipeline (výroba verze)

```text
vývojář: ručně VS build (Release, output C:\Edison)
   ↓
Compiler.exe (interní tool, \\192.168.16.1\u\Edison\Compiler\)
   ├─ kopie binárek na REL: FTP → Cloud2 c:\inetpub\ftp.m-line.cz\compiler\
   ├─ Caché DEV→REL: export projektů z dev Caché → import do REL Caché
   └─ „Verze – Ostrá/TEST – Full/Patch" → MLRest: ALVA.Compiler.Function.runSetupWizard
   ↓
SETUPWIZARD.exe (běží na Cloud2)
   ├─ čte FILES_DESCRIPTION.INF (manifest „co jde do verze")
   ├─ Caché projekty živě exportuje z REL Caché (s kompilací) do XML
   ├─ balí SETUP.zip + DEFINITION.zip, zapíše VERZE.INF
   └─ → c:\inetpub\release.m-line.cz\edison\release\<verze>\
   ↓
IIS: http://release.m-line.cz:59993/edison/release/    (TEST: /edison/TEST)
```

Reference: `Compiler\CompilerApi\SetupWizard.cs:30-51` (spuštění verze),
`SETUPWIZARD\frmMain.vb:5-63` (mapa release složky), `:263-315` (číslování verzí),
`FILES_DESCRIPTION.INF` (repo root, manifest formát `FULL~~TYP~zdroj` /
`PATCH~BALÍČEK~TYP~zdroj`), `DOCS\08-nasazeni.md` (psaný popis procesu).

Bez CI — build ručně ve VS, koordinace FULL verzí přes WhatsApp, e-mail na `verze@m-line.cz`.
`FILES_DESCRIPTION.INF` se edituje ručně přímo na Cloud2. Orchestrátor
`ALVA.Compiler.Function` žije jen na Cloud2, není v repu.

---

## 3. Server zákazníka — dvoukroková instalace

Aktualizuje se **obojí najednou — binárky i Caché kód** — proto jsou verze klienta a Caché
svázané:

1. **`EDTRANSFER2.EXE`** (projekt `UPDATERTR`) — stáhne release přes HTTP do staging
   adresáře (`targetPath`), zapíše do Caché `pending=1` (zákaz přihlašování).
   Args: `UPDATERTR\frmTransfer.vb:128-146`; engine `EDISONAPI\clsUpdater.vb:4109-4226`.
2. **`EDISONINS.EXE`** (UI `EDISONINS\frmUpdater.vb`, engine `EDISONAPI\clsUpdater.vb:2541-2938`):
   počká na zánik EDISONMAIN procesů → záloha → rozbalení ZIPů → kopie binárek do
   server-adresáře (core soubory `EDISON.exe`/`EDCore.dll` přes `.tmp` — vymění je loader
   při příštím startu, `:3173-3188`) → **import + kompilace všech `ALVA_*.xml` do
   zákazníkovy Caché** (`ALVA.SETUP.MainEx.ImportCacheProject` = `$system.OBJ.Load`,
   `CompileCacheProject` = `$system.OBJ.CompileProject` —
   `EDISONINS\CACHE\ALVA\SETUP\MainEx.cls:117-178`) → import globálů + per-projekt
   inicializace (`SetupRunInitialization`, `:3412-3440`) → update licence → zápis
   `VERZE.INF` + `EDISON.INF` (`:2748-2749`) → **bump
   `^[MLINE]EDISON("System","Update",2)` jako poslední krok** + `pending=0` (`:2752`).

**Verze v Caché globálu není „verze Caché kódu" — je to verze naposledy nainstalovaného
release.** Bumpne se atomicky až po úspěšné instalaci obojího.

Noční automatika: `ALVA.EDISON.System.RunAutoTransfer/RunAutoUpdate`
(`EDISONMAIN\CACHE\ALVA\EDISON\System.cls:196-370`) — zakáže login, ukončí klienty
(`TerminateClients`), spustí instalaci.

### Nastavení v Caché — `ALVA.SETUP.Main.LoadSettings`

Endpoint `EDISONINS\CACHE\ALVA\SETUP\Main.cls:7-72` čte globál
`^[MLINE]EDISON("System","Update")` (mapa subscriptů:
`EDISONINS\CACHE\ALVA\SETUP\API.cls:12-29`). Klíče výstupu (object[] protokol,
`~`-delimited): `update~<enabled>~<isNewUpdate>`, `pending`, `version` (Caché verze),
`edisonPath`, `sourceServer` (default `release.m-line.cz`), `sourcePort` (default `59993`),
`sourcePath`, `targetPath`, `localPath` (N×), `NucenyUpdatClentu`, `versionNotice`,
`versionUpdate`, `localSourcePath`, `ignoreNotify`, `xmlPatch`, `admin`.

Dev server (192.168.16.1, NS MLINE) má `update~0` a `version~` prázdné → update mechanismus
i runtime gate jsou na dev prostředí neaktivní.

---

## 4. Stanice — klientský update

Stanice mají **plnou lokální kopii** Edison adresáře. Legacy EDISONMAIN při startu (po
loginu a licenci — `frmNoMDI.vb:4726-4729`) porovná lokální `VERZE.INF` proti serverové
instalaci (`LocalClientSourcePath` = `localSourcePath` || `edisonPath`; UNC share nebo HTTP)
a při rozdílu spustí update.

**Rozhodovací strom** (`clsUpdate.UpdateNeeded`, `clsUpdate.vb:623-718`) — klientská větev:
nečitelná serverová verze → error+exit; `IsNewVersion(server, local)` → update; nucený
update (horolog `NucenyUpdatClentu` × lokální `CliUp.ini`) → update; prázdná vlastní verze →
error+exit; `!IsSameVersion(CacheVersion, local)` → hláška + exit (klient to nespraví,
`frmNoMDI.vb:4320-4324`); chyba minulého update (`EDISON.INF LAST_INSTALL_STATE=0`) → retry.

**Klient nemá volbu** — dialog je vždy ForceYes (`frmNoMDI.vb:4256`, tlačítko Ne disabled
`frmUpdateDialog.vb:48-53`); bez updatu „Bez provedené aktualizace nelze pokračovat
v práci" a exit. Pozn.: ForceYes na klientu přebíjí i `Enabled=false` (`frmNoMDI.vb:4259`
se přeskočí).

### EDUPDATE.EXE — fyzická výměna (projekt `UPDATERCL`)

Záměrně bez závislostí (vše inline v `clsAPI.vb`), aby mohl vyměnit všechny ostatní soubory.

**Argumenty** (`UPDATERCL\frmClientUpdate.vb:135-163`): `FormMode` (0=User, 1=Auto,
2=Task/tichý), `Async`, `RunEdison`, `SourcePath`, `TargetPath`, `Version`, `OldVersion`,
`NucenyUpdate`, `NucenyUpdateTime`. Od úpravy pro EDISONMAIN2 navíc `WaitProcess=`
a `RunExe=` (viz §7).

**Krok za krokem** (`clsAPI.vb:1231-1354`):

1. Log do `TargetPath\_LOG_UPDATER.txt`; validace (300 MB volného místa, přístupy).
2. **Busy-wait na zánik procesů podle JMÉNA** — `"EDISONMAIN"` a `"EDTRAYMAIN"`
   (`clsAPI.vb:1527-1544`). Nezabíjí, jen čeká.
3. Version gate: `IsNewVersion(Version, OldVersion)=False` a není NucenyUpdate → konec.
4. Seznam souborů: existuje-li `SourcePath\EDISONCRC.bin` → **diferenční update**
   (SHA-256 per soubor, kopíruje jen rozdílné; `EDUPDATE.exe` se vždy přeskakuje,
   `:1083`); jinak full copy. Umí UNC/local i HTTP zdroj.
5. Backup do `BKP_Files` + delete originálů; kopie nových; při chybě rollback ze zálohy.
6. `VERZE.INF` nikdy nepíše — jen kopíruje ze zdroje (píše ho serverový EDISONINS).
7. Exit kódy: **0** OK/no-op, **1** chybné parametry, **100** chyba + úspěšný restore,
   **200** chyba + selhal i restore.
8. Restart: `TargetPath\EDISON.exe` (loader) — před úpravou natvrdo
   (`frmClientUpdate.vb:87`).

**Self-update EDUPDATE dělá shell** — před spawnem zkopíruje novou `EDUPDATE.EXE` ze
zdrojového adresáře k sobě (`clsUpdate.UpdateClientUpdater:590-618`). Funguje, protože
EDUPDATE v tu chvíli neběží.

**Restart chain:** EDUPDATE → `EDISON.exe` (loader, projekt `EDLOADER`) → swap core `.tmp`
souborů (`EDLOADER\clsStartup.vb:389-430`) → `EDISONMAIN.EXE reason=2` (natvrdo,
`clsStartup.vb:17`).

---

## 5. Runtime pojistky (za běhu)

### Per-request version gate

Klient posílá `EdisonVersion` v sys kontextu **každého** MLRest requestu
(`MLRest\MLRest\MLRest.cs:67,129,164`). Server v každém REST entry pointu
(`ALWA.SYS.Rest`, kontrola `Rest.cls:1700-1725`) porovná s
`ALVA.SYSTEM.Main.GetVersion()` = `^EDISON("System","Update",2)` — **jen segmenty
datum+full, patch tolerován**. Mismatch → `THROWERROR "EdisonVersionFailed!"` → .NET event
`MLRest.EdisonVersionFailed` (`MLRest.cs:32-36`). Prázdná client verze ⇒ kontrola neběží.

Legacy handler: „Rozdílná verze Caché a verze Edison, software bude ukončen."
(`frmNoMDI.vb:5033-5042`) — bez možnosti odmítnutí.

### Ostatní

- **`maSeUkoncit` polling** (60 s timer) — vzdálené ukončení klientů s countdown
  (`frmNoMDI.vb:4846-4866`; server `EDSystem\CACHE\ALVA\SYSTEM\Main.cls:91-150`).
  V EDISONMAIN2 implementováno jako cooperative logout (`LogoutWatcher`).
- **`pending` lock** `^EDISON("System","Update",1)` — během server instalace loader odmítne
  Edisona spustit; + zákaz loginu `^EDISON("System","Login","Deny")`.
- **Nucený update klientů** — admin horolog `NucenyUpdateLocalClientu` × lokální `CliUp.ini`
  (`clsUpdate.vb:250-270`); vynutí re-sync i bez změny verze.

---

## 6. Matice zodpovědností

| Komponenta | Zodpovědnost |
| --- | --- |
| **EDISONMAIN** (shell) | detekce (start / re-login / ručně), rozhodovací strom, dialogy, self-update EDUPDATE, spawn helperů, `EdisonVersion` na requestech, maSeUkoncit polling |
| **EDUPDATE** (`UPDATERCL`) | fyzická výměna souborů klienta (diff/full, backup/rollback, log), restart loaderu |
| **EDISON.exe** (`EDLOADER`) | pending check, swap core `.tmp`, start EDISONMAIN |
| **EDTRANSFER2** (`UPDATERTR`) | server: HTTP download release do staging |
| **EDISONINS** + `EDISONAPI\clsUpdater` | server: instalace binárek + import Caché + bump verze + locks |
| **Caché `ALVA.SETUP.*`** | settings store (`^EDISON("System","Update")`), version global, audit `^LOGEdisonUp` |
| **Caché `ALWA.SYS.Rest`** | per-request version gate |
| **Compiler + SETUPWIZARD** (Cloud2) | výroba a publikace verze |

Mrtvý kód: `ClientUpdater\` (net8 „Updater", 2024) — prázdná UI kostra bez logiky.

---

## 7. Design pro EDISONMAIN2

**Role:** EDISONMAIN2 je vždy **stanice** (klientská role). Serverové updaty (EDTRANSFER2,
EDISONINS, noční automatika) zůstávají legacy mechanismu beze změny. Žádná server-side
(Caché) změna.

### Scope

1. **Startup check (klientská větev)** — po loginu + licenci: `LoadSettings` → porovnání
   lokální `VERZE.INF` proti serverové instalaci + Caché verzi → při rozdílu info popup
   (mandatory, žádný confirm) → kopie čerstvé `EDUPDATE.EXE` → spawn → shutdown.
   Implementace: `Updater` + `VerzeInf` (`EDISONMAIN2/dotnet/Startup/`),
   hook `AppBootstrapper.CheckForUpdate`.
2. **Runtime gate** — `EstablishSession` nastavuje `MLRest.EdisonVersion` z lokálního
   `VERZE.INF`; handler `EdisonVersionFailed` → popup + shutdown (vedle
   `EdisonActivityFailed`). Na dev neaktivní (prázdný version global), u zákazníků aktivní.
3. **Úprava EDUPDATE** (legacy repo, zpětně kompatibilní): nové argumenty `WaitProcess=`
   (další jméno procesu, na jehož zánik čekat) a `RunExe=` (exe spuštěné po dokončení místo
   `EDISON.exe`). EDISONMAIN2 spawnuje s `WaitProcess="EDISONMAIN2" RunExe="EDISONMAIN2.exe"`.

### Vědomé divergence od legacy

- **`Enabled` se honoruje vždy** (legacy klient ho přes ForceYes ignoruje). Důvod: dev
  prostředí má `update~0` a nedosažitelný `edisonPath` (`D:\Edison\Dev` na serveru) —
  legacy sémantika by na dev stanici shodila startup. `update~0` → celá kontrola skip.
- **Žádná optional větev** (HTTP check release.m-line.cz + confirm) — v legacy je to
  serverová/admin role, ne klientská. První pilot ji omylem implementoval.
- **Dev verze `999999.9`** — DEBUG build hlásí `VerzeInf.DevVersion` místo čtení
  `VERZE.INF`. Numericky vyšší než jakékoli reálné `yyMMdd` datum → `IsNewVersion` nikdy
  netriggne; explicitní dev guard v `Updater.Check` navíc přeskočí i Caché mismatch
  (jinak by update-enabled server hodil Broken). Verze se zobrazuje ve status baru shellu
  („Verze: 999999.9" = okamžitě poznat dev build).

### Vědomě mimo scope (zatím)

- `NucenyUpdate` / `CliUp.ini` (admin-forced re-sync bez změny verze)
- `maSeUkoncit` countdown — řeší `LogoutWatcher` (cooperative logout)
- EDTRAY notifikace, server-side cokoliv
- `ignoreNotify`, version changelog dialog (`ALVA.SETUP.VersionInfo`)

### Deployment prerekvizity (ops, ruční)

- `EDISONMAIN2.exe` přidat do `FILES_DESCRIPTION.INF` na Cloud2 (FULL řádek) — binárka pak
  teče k zákazníkům existující pipeline (single-file publish → jeden soubor).
- Upravený `EDUPDATE.EXE` vyjde s příští verzí přes tutéž pipeline.
