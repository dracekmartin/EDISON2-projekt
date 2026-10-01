# Post mortem vydání nového shellu v ostré FULL verzi (2026-09-29)

Datovaný snímek ke dni vzniku, neudržuje se. Kontroly běžely 29. 9. 13:56-14:16 a 18:18-18:45, jen čtením;
mezi 14:16 a 18:18 stroj nepracoval (smyčka hlídání `VERZE.INF` skončila zápisem 14:15:41 a pak rovnou
timeoutem v 18:18:39). Časy jsou v CEST, hlavičky `Last-Modified` (GMT) jsou přepočtené. Cesty:
`EDISON/` = `C:\MLineRepos\EDISON`, `EDISON2/` = `C:\MLineRepos\EDISON2`.

Stav rep: EDISON2 `origin/main` = `148dd4ba` (build `6b3a1c73` + 4 commity TD), EDISON `origin/master` =
`2bbb7be4f` (build `201f968ca`), v kontrolovaných složkách bez necommitnutých změn. Manifesty
`FILES_DESCRIPTION.INF` staženy ve 13:57 a znovu v 18:22, beze změny (release 1160 řádků, TEST 1161);
čísla řádků odkazují na ně.

Doověření z 1. 10. je v sekci 6: mění výsledek K1.3, K1.4, K5.5 a K6 I13 a opravuje předpoklad
o podepsaných souborech.

## 1. Verdikt

**Ostrá FULL verze vznikla: `260929.0`, `TYPE=1`, v `edison/release/`.** Soubory verze mají čas
15:29:04, `release/VERZE.INF` se přepsal na `LAST=260929.0` v 15:37:44. Během tvorby je tam
`LAST=0.0.0.0`, přepsání tedy znamená dokončenou tvorbu. Log verze už ale není k dispozici
(`release_version_progress` vrací jen „Momentálně neprobíhá tvorba verze."), takže kdo verzi spustil,
jestli log končí „FULL verze byla úspěšně vytvořena" a jaké soubory zapsala, zůstává NEOVĚŘENO.

**Nese nový shell ze správného buildu? Pravděpodobně ano, důkaz chybí.** Binárky v `C:\Edison` mají md5
z přípravy, jsou Release a od 13:40 se nezměnily. Po buildu nepřibyl žádný commit do kódu shellu ani
sdílených legacy knihoven, testy prošly (4083/4083). Mezi naším nahráním do REL (13:40) a ostrou verzí
ale proběhla ještě **TEST FULL 260929.4 (14:43-14:50) od neznámého autora** a kolega, který ve 13:22
nahrál do REL vlastní `EDControls.dll`, ve 14:32 pushnul další commit TD. Jestli REL v 15:29 nesl naše
soubory, bez zápisu nebo stažení `SETUP.zip` zjistit nejde (K1.4). I v horším případě by šlo jen
o `EDControls.dll` stejného zdroje: jeho kopie je z commitu `0df681a0`, naše z `6b3a1c73`, a mezi nimi
se EDControls nemění (K3.7).

**Zákazníky se starým shellem vydání nerozbije.** EDLOADER bez `NewShell=1` spustí `EDISONMAIN.EXE` jako
dřív a na `dotnet\` ani zip nesáhne. EDUPDATE bez běžícího `EDISONMAIN2` nic nezdrží. Opravy I07 a I18
se projeví jen pod hostem.

**Co je špatně (nic neblokuje):**

- Infotext `EDISONMAIN.INF` nemá blok k této verzi. První blok je z 24.03.2026, verze tak vyšla bez
  popisu (K5.4).
- Všichni zákazníci dostanou 74 MB `EDISONRUNTIME.zip` na server i na každou stanici a EDLOADER při
  každém startu dotaz na server navíc (K4.1, K4.8). Jde o provozní dopad, ne o chybu.
- `docs/deploy.md:33-34` uvádí `EDUPDATE2.exe` mezi řádky manifestu, ostrý manifest ho nemá. Ostrá verze
  se tím liší od TEST verzí (K2.1).
- DEV share nese `EDControls.dll` kolegy, ne vydávanou. Na DEV a v REL se liší komentář ve `Form.cls`
  (K3.7, K5.2).

**Co ověřit (člověk):** stáhnout `release/260929.0/SETUP.zip` a v `EDControls.dll` a `EDISONMAIN2.exe`
najít `1.0.0+6b3a1c73` (K1.4), vyhodnotit log verze, pokud ho někde má autor, ověřit na REL řádek
`newShell` v `ALVA_SETUP` (K5.5) a u zákazníka bez `ALVA_EDTesting` pořadí kompilace (K6 I13).

## 2. Kontroly

| ID | Kontrola | Výsledek | Důkaz | Co udělat |
|---|---|---|---|---|
| K1.1 | Vznik ostré verze | OK | `release/VERZE.INF`: 13:56-14:15 `LAST=260922.0.5` `TYPE=3`, v 18:21 `LAST=260929.0` `TYPE=1`, `Last-Modified` 15:37:44. `release/260929.0/VERZE.inf` (`LAST=260929.0`, `TYPE=1`) a `_filelist.inf` 15:29:04. | - |
| K1.2 | Kdo a kdy spustil, FULL do `edison\release\`, závěr logu, bez chyby | NEOVĚŘENO (čas a cíl OK) | Cíl `edison/release/`, typ FULL, čas 15:29-15:38 z hlaviček výše. `release_version_progress` (lastRow=0) v 13:56, 14:12 i 18:21: „Momentálně neprobíhá tvorba verze." Nástroj vrací jen běžící tvorbu, ne poslední běh (fakt z přípravy neplatí: log TEST 260929.3 nebyl k dispozici už ve 13:56). | Autor verze předá log, jinak ho nemá nikdo. Příště číst průběh během tvorby. |
| K1.3 | Zápisy `EXE~EDISONMAIN2.exe`, `EXE~EDControls.dll`, `EXE~EDISONMAIN2API.exe` + `.config`, `EXE~EDISON.exe`, `EXE~EDISONMAIN.exe`, `EXE~EDUPDATE.exe`, `DLL~EDISONRUNTIME.zip`, `XML~ALVA_EDISONMAIN2.XML`, `XML~ALVA_EDControls.XML`, `XML~ALVA_EDTesting.XML` | NEOVĚŘENO (nepřímo OK) | Log chybí. Nepřímo: FULL zapisuje každý řádek FULL i PATCH (`EDISON/SETUPWIZARD/frmMain.vb:1302-1320`) a každý soubor otevírá (`:1219-1224`). Chybějící soubor nebo neexportovatelný projekt by vyhodil výjimku a `VERZE.INF` by zůstal `0.0.0.0`. Všech 12 položek má řádek v ostrém manifestu (K2.2, K2.3), manifest se od 10:38 nezměnil. | Viz K1.2. |
| K1.4 | Verze nese náš build (nikdo po 13:40 nenahrál jiné soubory shellu) | NEOVĚŘENO | Mezi 13:40 a 15:29 vznikla TEST FULL 260929.4 (`TEST/260929.4/_filelist.inf` 14:42:58, `TEST/VERZE.INF` 14:50:22), autora ani nahrané soubory neznám. Kolega pushnul `148dd4ba` (TD) ve 14:32; DEV share od 13:18 žádný novější soubor shellu ani TD nemá. REL `compiler\` jde číst jen přes FTP (mimo povolené), podepsané soubory se od `C:\Edison` v md5 liší. | Stáhnout `release/260929.0/SETUP.zip` (mimo povolené GET) a hledat `InformationalVersion`: naše `EDControls.dll` a `EDISONMAIN2.dll` v bundlu mají `1.0.0+6b3a1c73...`, kolegova `EDControls.dll` `1.0.0+0df681a0...`. |
| K2.1 | Manifest release vs TEST | NÁLEZ (drobný) | Liší se jen řádkem `FULL~~EXE~...\EDUPDATE2.exe` (TEST ř. 58). Neškodí: `EDISON2/EDISONMAIN2/dotnet/Startup/Updater.cs:79` `ActiveKind = LegacyEdupdate`. `EDISON2/docs/deploy.md:33-34` ale řádek `EDUPDATE2.exe` mezi řádky manifestu vede, zatímco `:30-31` ho váže až na přepnutí updateru. | Srovnat `deploy.md:33-34` se skutečností (EDUPDATE2 jen v TEST), nebo řádek přidat i do ostrého manifestu. |
| K2.2 | Řádky binárek shellu vs `deploy.md:22-36` | OK | release: `EDUPDATE.exe` ř. 35, `EDISONMAIN.exe` 55, `EDISONMAIN2API.exe` 56, `.config` 57, `EDISONRUNTIME.zip` 357, `EDISON.exe` 1160 (poslední), legacy DLL 109-129, `UTILITIES.exe` 38, `USERS.exe` 856 (`PATCH~USERS`), `EDISONMAIN.exe.config` 1097 (jen `PATCH~MHD2Linky`, s mezerou na konci, viz Předpoklady). | - |
| K2.3 | Sekce `PATCH~EDISON2` a Caché projekty vs `deploy.md:43-66` | OK | release ř. 1143-1147: `ALVA_EDControls`, `EDControls.dll`, `ALVA_EDISONMAIN2`, `EDISONMAIN2.exe`, `ALVA_EDTesting`. Existující projekty: `ALVA_SETUP` 367, `ALVA_SYSTEM` 368, `ALWA_MLREST` 371 a 962, `ALVA_SYS_Login` 375, `ALVA_EDISON` 377, `ALVA_LIBRARY` 386, `ALVA_LICENCE` 394, `ALVA_MZDY2` 809, `ALVA_USERS` 854, `ALVA_ISY_ISYCloud` 1061, `ALVA_MHD2_PlanDopravy` 1101. FULL bere řádky FULL i PATCH (`EDISON/SETUPWIZARD/frmMain.vb:1307`). | - |
| K3.1 | Commity EDISON2 po `6b3a1c73` ve složkách shellu | OK | `git log 6b3a1c73..origin/main -- EDISONMAIN2 EDControls EDUPDATE2 EDISONMAIN2.LegacyMiniApps SingleFilePublish.targets`: prázdné (18:22). Po buildu jen TD: `3d2aea7a`, `a9647eb5`, `0df681a0`, `148dd4ba`. | - |
| K3.2 | Commity EDISON po `201f968ca` ve sdílených a shellových složkách | OK | `git log 201f968ca..origin/master -- MLRest EDCORE Licence EDLIBRARY EDUsers EDISONAPI EDISONMAIN2API EDLOADER EDISONMAIN UPDATERCL USERS Utilities EDISONINS/CACHE/ALVA/SETUP`: prázdné (18:22). Po buildu 23 commitů (včetně merge) v agendách (MZDY2, VOZIDLA2, ISY*, GENERAL, `CACHE/ALVA_MZDY2.xml`, `CACHE/ALVA_GENERALAPI.xml`). | - |
| K3.3 | md5 binárek v `C:\Edison` | OK | Ve 13:58 i v 18:24: `EDISONMAIN2.exe` 8b766a96c2a0 (13:08:31), `EDControls.dll` 32d9a3cc1301 (13:08:26), `EDISONMAIN2API.exe` 21fd0309d27b, `EDISON.exe` 475213619e65 (13:06:53), `EDISONMAIN.exe` df7a7a74a719 (13:06:58), `EDUPDATE.EXE` 16945fc28915 (build 2026-09-15, splňuje `deploy.md:28` ≥ 2026-06-17). Shoda s přípravou; nejnovější soubor v `C:\Edison` je z 13:08. | - |
| K3.4 | Legacy exe a DLL jsou Release | OK | `ReflectionOnlyLoadFrom` + `DebuggableAttribute`, závislosti z `C:\Edison`: `EDISONMAIN2API.exe`, `EDISON.exe`, `EDISONMAIN.exe`, `EDUPDATE.EXE`, `MLRest`, `EDCore`, `EDLicence`, `EDLibrary`, `EDUsers`, `EDISONAPI`, `USERS.exe`, `UTILITIES.exe`: všechny modes=2 (bit 256 nenastaven). | - |
| K3.5 | Binárky EDISON2 jsou Release | OK | `EDISONMAIN2.exe` obsahuje bajtově `EDISON2/EDISONMAIN2/dotnet/obj/Release/net10.0-windows/win-x64/EDISONMAIN2.dll` (13:08), Debug kopii ne. `EDControls.dll` má md5 shodné s `EDControls/dotnet/obj/Release/net10.0-windows7.0/EDControls.dll`. Profil `SingleFileCEdison.pubxml`: `Configuration=Release`. | - |
| K3.6 | `dotnet test EDISON2.sln --filter "Category!=Integration"` | OK | exit 0: EDUPDATE2.Tests 99, EDControls.Tests 914, EDISONMAIN2.Tests 1035, TemplateDesigner.Tests 2035, žádné selhání. Běželo nad `0df681a0`, kód shellu shodný s `6b3a1c73`. | - |
| K3.7 | DEV share nese vydávané soubory | NÁLEZ (drobný) | `//192.168.16.1/Edison/Dev/` (13:59 i 18:25): `EDISONMAIN2.exe`, `EDISONMAIN2API.exe`, `EDISON.exe`, `EDISONMAIN.exe` md5 shodné (13:09). `EDControls.dll` 681205457f44 (13:18, 401 920 B): Release build kolegy (PDB `C:\Users\miesb\source\EDISONMAIN2\...`), `InformationalVersion 1.0.0+0df681a0`, jiné SDK (WPF 10.0.11 proti našemu 10.0.10). `git diff 6b3a1c73 0df681a0` v EDControls je prázdný, zdroj je tedy stejný. `EDUPDATE.EXE` na share je z 2024-09-02. | Rozhodnout, kterou kopii na DEV nechat. SDK v repu připnuté není (chybí `global.json`), buildy z různých strojů se proto liší. |
| K4.1 | EDLOADER bez `NewShell=1` | OK, NÁLEZ (provozní) | Start je beze změny: `EDISON/EDLOADER/clsStartup.vb:759-763` spustí `StartEdison(..., RunEdison)` = `EDISONMAIN.EXE reason=2` (`:338-358`). `EnsureLocalRuntime` je až za `NewShellIsEnabled` v řetězci `AndAlso`, takže bez uzlu se `dotnet\`, `dotnet.lock` ani zip nečtou ani nepíšou. Nové je: po FULL má každá stanice `EDISONMAIN2.exe`, `EDControls.dll`, `EDISONMAIN2API.exe` a zip, `NewShellFilesPresent` (`:361-371`) proto projde a EDLOADER při každém startu volá `ALVA.SETUP.Main.LoadSettings` (`:551-568`). Dřív ho v praxi nevolal: `UpdateIsRunning` při funkčním spojení skončí před dotazem (`:227`). Chyba nebo výpadek dotazu znamená starý shell (`:556`, `:564-567`). `LoadSettings` na DEV jen čte (`GetSettings`), vrací i řádek `admin` se jménem a šifrovaným heslem. | Přijmout: jeden dotaz navíc při startu. |
| K4.2 | EDUPDATE přijme `WaitProcess=EDISONMAIN2` ze starého shellu a bez běžícího `EDISONMAIN2` nezdrží | OK | Starý shell spouští lokální `EDUPDATE.EXE` s novým argumentem (`EDISON/EDISONMAIN/clsUpdate.vb:814`, `:818`). EDUPDATE čte argumenty podle jména (`EDISON/UPDATERCL/frmClientUpdate.vb:159`, `EDISON/UPDATERCL/clsAPI.vb:90-108`), starší build neznámý argument ignoruje. `WaitForProcessClosed` (`clsAPI.vb:1553-1562`) bez procesu `EDISONMAIN2` smyčku přeskočí; jméno se porovnává přesně, na `EDISONMAIN2API` nečeká. UPDATERCL se za poslední měsíc nezměnil (poslední commit `49d19295d9`, 2026-06-17). | - |
| K4.3 | I07 jen pod hostem | OK | `TryCast(Me.Owner, EDForm)` (`EDISON/EDISONMAIN/formEDISONMAINLanguageList.vb:807-808`). Starý shell dialog otevírá `frm.ShowDialog(Me)` z `frmNoMDI` (`EDISON/EDISONMAIN/Rozhrani (mainform)/frmNoMDI.vb:3576`), vlastník je `EDForm`, cesta je stejná jako dřív. | - |
| K4.4 | I18 jen pod hostem | OK | `EDISON/EDISONMAIN/formEDISONMAINInit.vb:88-90`: odhlášení se vynechá jen při `cmdArgs(1) = "EDISONMAIN2"`. Literál jako první argument posílá jen host (`EDISON/EDISONMAIN2API/FormToRunGetter.cs:39`); jinde v legacy kódu je jen jako hodnota `WaitProcess` pro EDUPDATE. | - |
| K4.5 | Kód hostu na cestě starého shellu | OK | `EDISONMAIN.exe` volá `EDISONMAIN2API.FormToRunGetter.TryGetFormFromArgs` při každém startu (`EDISON/EDISONMAIN/ApplicationEvents.vb:20-25`, od 05/2026). Metoda s argumentem `reason=2` hned vrátí false (`FormToRunGetter.cs:20-23`, `:39`). `e468ada4f` a `201f968ca` mění jen `FormToRunGetter.cs` a `Program.cs`, žádnou veřejnou signaturu (diff `4722907d8..201f968ca` bez řádku `public`); host volají i `MHD2Linky` a `TiskJR`. | - |
| K4.6 | Kontrola aktualizace v Nastavení (`b52d0ec2c`) | OK | S vlastníkem `frmNoMDI` beze změny (`EDISON/EDISONMAIN/formEDISONMAINSettingsShow.vb:1494-1498`). Jen když forma běží bez `frmNoMDI`, uživatel místo chyby uvidí hlášku o starém shellu. | - |
| K4.7 | Odkazy v popisu verze (`949743011`) | NÁLEZ (zamýšlená změna) | Všichni zákazníci: klik na odkaz v okně „Aktualizace systému" otevře prohlížeč (`EDISON/EDISONMAIN/frmUpdateDialog.vb:145-154`). | Zmínit v infotextu. |
| K4.8 | Runtime zip a binárky shellu u všech zákazníků | NÁLEZ (provozní) | FULL nese všem `EDISONRUNTIME.zip` (73,7 MB, release ř. 357), `EDISONMAIN2.exe` (4,4 MB), `EDControls.dll` a `EDISONMAIN2API.exe`. Legacy EDUPDATE kopíruje kořenové soubory: s `EDISONCRC.bin` jen změněné (`EDISON/UPDATERCL/clsAPI.vb:1110-1112`), bez něj všechny při každém updatu (`:1157-1167`). Stanice aktualizované přes http tak jednou stáhnou asi 78 MB navíc, každá další záplata runtime 74 MB. | Přijmout, zákazníky s pomalou linkou upozornit (infotext). |
| K5.1 | Čas tříd `ALVA_EDISONMAIN2`, `ALVA_EDControls`, `ALVA_EDTesting` na DEV ≤ 29. 9. 13:41 | OK | `cache_get_source` (13:59, znovu v 18:26): nejnovější `ALVA.EDISONMAIN2.API.Tree` 2026-09-25 13:53:59, `ALVA.EDTesting.MLTestCase` 2026-09-23 15:17:56, ostatní 2026-05-15 až 2026-09-23. Členy: `ALVA_EDISONMAIN2` 7, `ALVA_EDControls` 16 (včetně `EDInputForm.INC`), `ALVA_EDTesting` = balíček `ALVA.EDTesting` (2 třídy). | - |
| K5.2 | Obsah tříd DEV vs repo | OK, NÁLEZ (kosmetický) | 12 tříd hlásí „different". Po rstrip shodná `Tree` (826 ř.). Obsahem prošlé `SessionLockTest`, `TelemetryContractTest`, `UserData`, `UserProfileTest`, `DesktopShortcutsTest`, `UserDataTest`: Caché ukládá `/// ` s mezerou a blok `///` připojí k dalšímu členu bez prázdného řádku. `TreeTest`, `FormTest`, `ListTest`, `DesktopShortcuts` jen podle vzoru (první rozdíl na řádku `///` nebo prázdném). Věcný rozdíl jediný: `ALVA.EDControls.EDInputForm.Form` ř. 6 má na serveru `// Zrcadlí C# EValidationTrigger.`, repo `ValidationTrigger` (`70fb470c`, 28. 8., nasazení hook nespustilo). Jde jen o komentář. | Při příští úpravě `Form.cls` nasadit přes hook. |
| K5.3 | Členství projektu vs repo | NÁLEZ (známý, I23) | `ALVA_EDISONMAIN2` na DEV nemá `ALVA.EDISONMAIN2.Tests.NewShellSwitchContractTest` (třída na DEV existuje, ts 2026-09-23 13:35, jen není v projektu). Do vydání nejde, nic na ní nezávisí. | - |
| K5.4 | Infotext `EDISONMAIN.INF` | NÁLEZ | `release_infotext_get EDISONMAIN.INF` (14:09 i 18:27): 268 řádků, první blok `24.03.2026` (články do EDISON WEB). O verzi 260929.0 ani o novém shellu nic. Soubor jde do FULL (release ř. 544), verze tedy vyšla s tímto textem. | Rozhodnout, jestli doplnit blok (dotaz v Nastavení, odkazy v okně aktualizace, velikost aktualizace); zápis dělá člověk a zákazník ho uvidí až s další verzí. |
| K5.5 | REL `ALVA_SETUP` nese řádek `newShell` | NEOVĚŘENO | DEV `ALVA.SETUP.Main.LoadSettings` řádek vrací (`settings(13)`, ts 2026-09-23 13:32). REL třídy číst nejde (MCP má jen DEV profil) a DEV → REL ve 13:41 `ALVA_SETUP` neneslo. Chybí-li na REL, `NewShell=1` u pilota nemá účinek (EDLOADER pustí starý shell); ostatních zákazníků se to netýká. | Před pilotem ověřit na REL, nebo u pilota po instalaci nastavit uzel a spustit `EDISON.exe`. |
| K6 | Otevřené nálezy auditu | viz tabulka níže | `.claude/reviews/2026-09-29-integrace-shellu.md` §6 | - |

### K6: otevřené a odložené nálezy auditu

| Nález | Blokuje ostrou verzi? | Důkaz |
|---|---|---|
| I06 (otevřený) | Ne. Projeví se jen pod novým shellem, který zákazník bez `NewShell=1` nespustí; pilot ho obejde restartem. | brána `EDISON/EDLOADER/clsStartup.vb:759-763`; výstup hostované formy shell neobsluhuje (`EDISON2/EDControls/dotnet/EDViewModel/WindowNavigator.cs:41`) |
| I20 (otevřený) | Ne. Ukončení modulu při chybě exportu je parita s legacy cestou EDPROCESS, starého shellu se netýká. | `EDISON/REPORTGENERATOR/REPORTGENERATOR/formREPORTGENERATORReportDotNet.vb:485-488`, `EDISON/TISK/TISK/frmReportShow.vb:4736-4738` |
| I13 (odložený) | Ne pro FULL, protože `ALVA_EDTesting` v ní je (release ř. 1147). Neověřené zůstává pořadí: u zákazníka, který EDTesting ještě nemá, instalace naimportuje všechny projekty bez kompilace a pak je kompiluje v pořadí manifestu, `ALVA_EDControls` (ř. 1143, testy dědí z `MLTestCase`) před `ALVA_EDTesting` (ř. 1147). Projde jen tehdy, když Caché při kompilaci podtřídy zkompiluje i naimportovanou nadtřídu. | import `EDISON/EDISONAPI/clsUpdater.vb:3480-3497` (`$system.OBJ.Load(file,"-d")`, `EDISON/EDISONINS/CACHE/ALVA/SETUP/MainEx.cls:150`), kompilace po projektech `clsUpdater.vb:3500-3513` |
| I21 (odložený) | Ne. Chybějící `Jazyk`, `Scheme`, `MainEdisonForm` a `EDCaller` se týkají jen formy v hostu. | `EDISON/EDLIBRARY/clsForm.vb:703-710` (kopíruje jen CreateForm) |
| I23 (odložený) | Ne pro shell. `ALVA_EDISONMAIN2` jen postrádá testovou třídu (K5.3). `ALVA_TEMPLATE_DESIGNER` má na DEV dál 74 členů včetně probe tříd a tří produkčních mimo git a FULL ho dá všem; to je otázka vydání TD. | `cache_project_info ALVA_TEMPLATE_DESIGNER` (74 členů, např. `Tests.ScanTmp`, `API.InstancesList`) |
| I24 (vynechaný) | Ne. `EDISONMAIN2.exe` v `C:\Edison` je prokazatelně Release (K3.5); riziko kontroly v Compileru platí jen pro ruční kopii. | `EDISON2/EDISONMAIN2/dotnet/Properties/PublishProfiles/SingleFileCEdison.pubxml` |

### Co nového dostali všichni zákazníci (EDLOADER, EDISONMAIN, UPDATERCL za poslední měsíc)

| Commit | Změna | Dopad se starým shellem |
|---|---|---|
| EDLOADER `d64a020f1`, `2e91c08ce`, `c62c12ed2` | přepínač shellu, rozbalení runtime pod zámkem, `reason=2` pro nový shell | jen dotaz `LoadSettings` při startu navíc (K4.1) |
| EDISONMAIN `e12c9f3c6` | `EDISONMAIN.exe.config` pinuje `EDISONMAIN2API.exe` přes codeBase | žádný; brání pádu na zapomenuté `EDISONMAIN2API.dll` |
| EDISONMAIN `949743011` | odkazy v popisu verze otevřou prohlížeč | nová funkce (K4.7) |
| EDISONMAIN `b52d0ec2c` | kontrola aktualizace v Nastavení bez `frmNoMDI` ukáže hlášku | beze změny, jen okrajová cesta (K4.6) |
| EDISONMAIN `a06bedf95`, `240df31a1` | I07, I18 | žádný (K4.3, K4.4) |
| EDISONMAIN `96e8fba66` | `WaitProcess=EDISONMAIN2` při updatu ze starého shellu | žádný bez běžícího nového shellu (K4.2) |
| UPDATERCL | bez commitu, vydává se build z 2026-09-15 | - |

## 3. Co ve vydání není

- EDISON2 `3d2aea7a`, `a9647eb5`, `0df681a0`, `148dd4ba` (jen TemplateDesigner, shell ani EDControls
  nemění). Který `TemplateDesigner.exe` ostrá nese, jsem neověřoval (poslední známé nahrání do REL kolegou
  ve 13:22, na DEV share je jeho build z 13:18).
- EDISON: 23 commitů (včetně merge) po `201f968ca` v agendách (MZDY2, VOZIDLA2, VOZIDLA2API, GENERAL, ISYADNasCis,
  ISYJR2, ISYOBEHY, ISYPlanDopravy, ISYZastavky, ISYZpracovani, ISYProjekty). Žádný nemění kontrolované
  složky; jestli jejich binárky nebo Caché projekty do REL před 15:29 někdo nahrál, se shellu netýká.
- `EDUPDATE2.exe` (jen v TEST manifestu, ř. 58).
- Blok infotextu k verzi a novému shellu.
- Otevřené nálezy auditu I06 a I20, odložené I13, I21, I23, vynechaný I24.

## 4. Neověřeno

- Log ostré verze 260929.0: kdo ji spustil, závěrečná věta, chyby, zápisy `EXE~`/`DLL~`/`XML~` pro
  12 položek shellu. Nástroj log po doběhnutí nevrací.
- Kdo vytvořil TEST FULL 260929.4 (14:43-14:50) a co před ní nahrál do REL.
- Obsah REL `compiler\` v 15:29: původ souborů shellu, verze `EDISONRUNTIME.zip`, `EDLibrary.dll`,
  `EDISONAPI.dll`, `EDUsers.dll`, `EDCore.dll`, `MLRest.dll`, `EDLicence.dll`, `USERS.exe`,
  `UTILITIES.exe` (ve 13:40 šlo do REL jen šest souborů, ostatní tam jsou z dřívějška).
- Obsah `release/260929.0/SETUP.zip`: stažení je mimo povolené GET.
- Caché na REL: `ALVA_SETUP` s řádkem `newShell` a stav `ALVA_SYS_Login`, `ALVA_EDISON`, `ALVA_SYSTEM`,
  `ALVA_LIBRARY`, `ALVA_LICENCE`, `ALWA_MLREST`, který shell potřebuje (`deploy.md:62-66`).
- Kompilace `ALVA_EDControls` a `ALVA_EDISONMAIN2` u zákazníka bez `ALVA_EDTesting` (K6 I13).
- Instalace FULL s 74 MB zipem u zákazníka mimo TEST (rozbalení `SETUP.DAT` v EDISONINS).
- `TreeTest`, `FormTest`, `ListTest`, `DesktopShortcuts`: shoda DEV s repem jen podle vzoru prvního
  rozdílu a časů, ne řádek po řádku.
- Bez člověka nejde:
  - ruční testy na TEST 260929.3: strom práv v USERS a tisk MZDY2, TS a stanice spouštěné ze share,
    dvě stanice nad jedním share, připnutí na hlavní panel;
  - E2E smoke nového shellu po sadě `EDISON2.IntegrationTests`;
  - nastavení pilotního zákazníka: `NewShell=1`, seznam `localPath` (lokální klienti a TS s vlastní
    instalací), způsob spouštění stanic, x64 a WebView2 na stanicích.

## 5. Předpoklady

- Mezi 14:16 a 18:18 kontrola neběžela (stroj nepracoval). Ostrá verze vznikla právě v této mezeře.
  Průběh tvorby jsem proto neviděl a hodnotím jen stav po ní.
- „Verze doběhla" opírám o přepsání `release/VERZE.INF` z `0.0.0.0` na `260929.0` v 15:37:44 (fakt
  z přípravy: během tvorby je `LAST=0.0.0.0`). Chyby, které tvorbu nezastaví, by v logu být mohly.
- Fakta z přípravy beru jako daná: nahrání šesti souborů do REL ve 13:40, DEV → REL ve 13:41 bez chyby,
  TEST FULL 260929.3 bez chyby (hlavičky to potvrzují: soubory verze ve 13:41:47), bundle s Release
  kopiemi legacy DLL. Tvrzení z přípravy, že `release_version_progress` ukazuje poslední běh, neplatí.
- Soubory v REL jsou podepsané, md5 proti `C:\Edison` proto neporovnávám. Shodu opírám o md5
  v `C:\Edison`, které se od nahrání nezměnilo (nejnovější soubor 13:08).
- Testy běžely nad `0df681a0`, protože lokální `main` mezitím někdo posunul z `6b3a1c73`. Rozdíl je jen
  v TemplateDesigneru, pro shell je výsledek platný.
- Veřejné API legacy DLL (`EDLibrary`, `EDCORE`, `EDISONAPI`, `MLRest`, `Licence`, `EDUsers`) se od 16. 9.
  nezměnilo (`14c62e123` a commity EDCORE mění vnitřek a XML zdroje). Předpokládám proto, že nové
  `EDISONMAIN.exe` a `EDISONMAIN2API.exe` poběží s tím, co z těchto DLL v REL leží.
- Kolegova `EDControls.dll` nese jen commit (`+0df681a0`), ne příznak necommitnutých změn. Stejný zdroj
  jako naše kopie předpokládám z prázdného `git diff 6b3a1c73 0df681a0 -- EDControls` a ze shodných
  řetězců I02 v obou DLL.
- Řádek `EDISONMAIN.exe.config` s mezerou na konci (release ř. 1097) SETUPWIZARD nečistí
  (`frmMain.vb:1219-1224`) a spoléhá na normalizaci cest ve Windows. Beru ho jako funkční: bez toho
  souboru by starý shell přišel o binding redirecty C1 a zákazníci by to hlásili.
- „Dotaz navíc" v K4.1 stojí na tom, že `UpdateIsRunning` při funkčním spojení vrací false ještě před
  dotazem (`clsStartup.vb:227`). Je to stávající chyba legacy kódu a neřešil jsem ji.
- „Blokuje" v K6 znamená: rozbije ostrou verzi nebo zákazníka se starým shellem. Dopady na pilot
  s `NewShell=1` uvádím zvlášť.

## 6. Doověření (2026-10-01)

Staženo `release/260929.0/SETUP.zip` (231 MB, `Last-Modified` 29. 9. 15:34:59) a rozbaleno
`C:\Edison\7za.exe`. `SETUP.DAT` (758 MB, čas 15:30) je rozparsovaný celý: 728 souborů, žádné zbylé
bajty. Formát popisuje `EDISON/SETUPWIZARD/frmMain.vb:1251-1260`.

| Kontrola | Dřív | Teď | Důkaz |
|---|---|---|---|
| K1.3 zápisy položek shellu | NEOVĚŘENO | OK | V `SETUP.DAT` jsou `EDISONMAIN2.exe`, `EDControls.dll`, `EDISONMAIN2API.exe` a `.config`, `EDISON.exe`, `EDISONMAIN.exe`, `EDUPDATE.exe`, `EDISONRUNTIME.zip`, `ALVA_EDISONMAIN2.XML`, `ALVA_EDControls.XML` a `ALVA_EDTesting.XML`. `EDUPDATE2.exe` chybí, souhlasí s ostrým manifestem (TEST 260929.3 měla 729 souborů). `EDISONMAIN.exe.config` je v balíku pod jménem s mezerou na konci, stejně jako 19 knihoven C1. |
| K1.4 verze nese náš build | NEOVĚŘENO | OK | `EDISONMAIN2.exe`, `EDControls.dll`, `EDISONMAIN2API.exe`, `EDISON.exe`, `EDISONMAIN.exe` a `EDUPDATE.exe` jsou bajtově shodné s `C:\Edison` (md5 z K3.3). Shodné md5 má `EDISONRUNTIME.zip` (34d0943538dd) i oba configy. `EDControls.dll` nese `+6b3a1c73` a od kolegovy kopie na DEV share se liší. `TemplateDesigner.exe` je kolegův build z `0df681a0`. |
| K1.2 log verze | NEOVĚŘENO | beze změny | Log se už nezíská. Obsah verze je ověřený přímo, log k tomu potřeba není; autora verze zná uživatel. |
| K5.5 `newShell` v `ALVA_SETUP` | NEOVĚŘENO | OK | `ALVA_SETUP.XML` ve verzi: `ALVA.SETUP.Main` (TimeChanged 2026-09-23 13:32:59) plní `settings(13)` z `^[systemDB]EDISON("System","NewShell")` a vrací řádek `newShell`. `GetSystemDB` vrací napevno `"MLINE"` (`EDISON/EDISONMAIN/CACHE/ALVA/EDISON/Main.cls:7-10`), příkaz v `docs/deploy.md` tedy platí u každého zákazníka. |
| K5.1, K5.2 Caché ve verzi | OK (DEV) | OK (verze) | TimeChanged všech 24 tříd ve verzi (`ALVA_EDISONMAIN2` 7, `ALVA_EDControls` 15 a include, `ALVA_EDTesting` 2) se shoduje s časy na DEV z K5.1. `Form` nese starý komentář `EValidationTrigger`, shodně s K5.2. |
| K6 I13 pořadí kompilace | NEOVĚŘENO | pravděpodobně OK | Instalace nejdřív naimportuje všechny projekty bez kompilace (`EDISON/EDISONAPI/clsUpdater.vb:3480-3498`, `$system.OBJ.Load(file,"-d")` v `EDISON/EDISONINS/CACHE/ALVA/SETUP/MainEx.cls:150`) a teprve pak je kompiluje (`clsUpdater.vb:3500-3514`, `CompileProject(project,"c-dkfvb")` v `MainEx.cls:183`). `MLTestCase` je tedy při kompilaci `ALVA_EDControls` v databázi; nezkompilovanou nadtřídu kompilátor Caché přibere s podtřídou. Na čisté instalaci bez EDTesting neověřeno. |
| K2.1 `deploy.md` a manifest | NÁLEZ | opraveno | `docs/deploy.md`: `EDUPDATE2.exe` má řádek jen v TEST manifestu, do ostrého patří před přepnutím `Updater.ActiveKind`. Doplněn i úplný seznam legacy DLL v bundlu `EDISONMAIN2.exe` a podmínka Release. |

Oprava předpokladu ze sekce 5: soubory ve verzi **nejsou podepsané**. Žádný z porovnaných PE souborů
nemá Authenticode podpis, i legacy (`MLRest.dll`, `EDLicence.dll`, `USERS.exe` a další), přestože log
tvorby verze hlásí „Certifikace". Md5 se proto s `C:\Edison` shoduje. S novým shellem to nesouvisí,
týká se celé distribuce.

Legacy DLL ve verzi (`MLRest`, `EDLicence`, `EDUsers`, `EDISONAPI`, `EDCore`, `EDLibrary`, `USERS.exe`,
`UTILITIES.exe`) jsou jiné buildy než v `C:\Edison`, do REL se 29. 9. nenahrávaly. Všechny jsou Release
(`DebuggableAttribute` bez bitu 256). Do `EDISONMAIN2.exe` jsou zabalené Release kopie z `C:\Edison`.

K předpokladu o `UpdateIsRunning` (`EDISON/EDLOADER/clsStartup.vb:227`): podmínka je obrácená od
`66d953e7b79` (2021). Při funkčním spojení funkce vrátí false bez dotazu, hláška „Probíhá aktualizace
systému, aplikace bude ukončena." se proto nikdy neukáže a stanice startují i během instalace verze na
serveru. Stará chyba, oprava změní chování u všech zákazníků, patří do samostatného úkolu.

Zbývá: infotext `EDISONMAIN.INF` (K5.4), kopie `EDControls.dll` na DEV share a připnutí SDK (K3.7),
komentář v `Form.cls` (K5.2), členství `NewShellSwitchContractTest` v projektu (K5.3) a body, které
bez člověka nejdou (sekce 4).
