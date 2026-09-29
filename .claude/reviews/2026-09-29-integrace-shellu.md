# Audit napojení nového shellu na legacy EDISON (2026-09-29)

Datovaný snímek ke dni vzniku, neudržuje se. Stav rep: EDISON2 `f48950c7`, EDISON `2e91c08ce`, obě bez
necommitnutých změn. Cesty v tabulkách: `EDISON/` = `C:\MLineRepos\EDISON`, `EDISON2/` = `C:\MLineRepos\EDISON2`.

Naživo jen čtení:

- manifest vydání `http://release.m-line.cz:59993/edison/TEST/FILES_DESCRIPTION.INF` (GET 2026-09-29,
  1160 řádků; dále „manifest", čísla řádků odkazují na tuto kopii),
- DEV Caché `192.168.16.1:57770` přes MCP `cache_project_info`, `cache_get_source`, `cache_zwglobal`
  (slovník tříd `^oddDEF`/`^oddCOM`), nic nezapisující.

Žádný build, žádné spuštění aplikací, žádné úpravy mimo tento soubor.

**Oprava po vydání (2026-09-29):** I02 je přeřazený z „blokuje pilot" na riziko a jeho popis je přepsaný.
Důvod: při ověřování rozsahu se ukázalo, že příznak licence má v repu jen 8 práv a v praxi se projeví
jen 3 práva ECM, takže pilot zasáhne jen zákazníka s ECM ručním vstupem. Zároveň se ukázalo, že chyba
práva při uložení aplikačního přístupu maže, což původní text neuváděl.

Stav řešení nálezů je v sekci 6.

## 1. Souhrn

- **Blokuje pilot (1).** V manifestu chybí `EDISONRUNTIME.zip`: stanice bez globálního .NET 10 tiše
  zůstanou na starém shellu a FULL by ručně nahraný zip ze serveru smazal (I01).
- **Nejvyšší rizika.** Hostovaný USERS při uložení aplikačního přístupu smaže licencovaná práva, která
  zákazník má, protože host skládá licenci bez `Version`. V praxi jde o tři práva ECM „Ruční vstup" (I02).
  Vypnutí uzlu `NewShell`, tedy záchranná brzda pilotu, nezabere u uživatele, který si
  shell připne na hlavní panel (I03). Nový shell nečte stav licence: propadlá licence nevede k hlášce ani
  k režimu čtení v modulech (I04). Licence z M-line se nestahuje ani po „Nástroje > Obnovit licenci",
  přestože hláška tvrdí opak (I05).
- **Chování pod novým shellem.** Změny z Nastavení a USERS se neprojeví do restartu (I06). Nastavení
  jazyka po OK vždy ukáže chybu (I07). Tisky MZDY2 a ISYJR vycházejí bez názvu firmy (I08). Přihlášení
  novým shellem chybí v sestavě „Přehled přihlášení" (I09).
- **Nasazení podle konfigurace zákazníka.** Chybí legacy rozlišení serverové instalace (I10), rozbalování
  runtime na share nemá zámek mezi stanicemi (I11), data WebView2 leží vedle exe (I12), do vydání jdou
  testové třídy s nesouladnou závislostí (I13), terminálový server se při updatu zablokuje (I14).
  Před pilotem je potřeba znát u zákazníka `edisonPath`, `localSourcePath`, `localPath`, terminálový
  server a spouštění ze share.
- **Caché.** Třídy, které shell volá, jsou na DEV shodné s repem a zkompilované. Jediná pozičně parsovaná
  odpověď bez kontraktního testu je `ALVA.SYS.Login.Login` (I22).

## 2. Nálezy

| ID | Závažnost | Oblast | Co se stane (scénář pod novým shellem) | Důkaz | Návrh opravy | Odhad | Ověřeno |
|---|---|---|---|---|---|---|---|
| I01 | blokuje pilot | vydání | Stanice bez globálního x64 .NET 10 Desktop Runtime: EDLOADER nenajde runtime ani zip a spustí starý shell bez hlášky. Kdo zip nahraje na server ručně, přijde o něj při nejbližší FULL: EDISONINS smaže kořenové soubory a vrátí jen dva ini. | manifest: řádek `EDISONRUNTIME` neexistuje; `EDISON/EDLOADER/clsStartup.vb:361-366`, `:434-435`; FULL maže kořen `EDISON/EDISONAPI/clsUpdater.vb:3779-3818`, vrací `:3135-3139`; požadavek `EDISON2/docs/deploy.md:16-19`, `:35` | Přidat `FULL~~DLL~...\EDISONRUNTIME.zip` do manifestu a zip do Compileru; ověřit na stanici bez .NET 10. | 1 h | naživo (manifest) + kódem |
| I02 | riziko (blokuje zákazníka s ECM ručním vstupem) | parita licence | Admin v USERS otevře práva aplikačního přístupu (formEDISONMAINAccessTree). Host dává `License.Version=""`, `Val("")=0 < 1.003` → filtr licencovaných položek použije starý výklad (licence = zakázaná práva) a skryje právě ta práva, která zákazník **má**. Po OK strom vrátí jen viditelné položky a `SetRightsAP` z nich profil složí znovu, skrytá práva se tedy z profilu smažou. „Kopírovat práva z jiné firmy" ukládá rovnou. Příznak licence mají v repu jen ECM Archiv dokumentů, Ruční vstup - zapisovat, měnit, rušit (47003051/53/55), a pět práv „Údržba" starých VOZIDEL (28001), která jsou jen ve starém stromu pro licence verze < 2. Za běhu modulu jsou práva správně, opačný směr (zaškrtnout nelicencované právo) nemá účinek. | host `EDISON/EDISONMAIN2API/FormToRunGetter.cs:150-153`; filtr `EDISON/EDISONMAIN/frmMain.vb:293-307`; OK `EDISON/EDISONMAIN/formEDISONMAINAccessTree.vb:1348`; přepis profilu `EDISON/USERS/CRights.vb:101-127`; kopie `EDISON/USERS/Users-v4/FrmUSERSRightAPCopyFromOtherIdfa.vb:185-189`; příznak `EDISON/ECM/CACHE/ALVA/ECM/Main.cls:13-15` (DEV shodný), `EDISON/VOZIDLA2/CACHE/ALVA/VOZIDLA/Main.cls:26-30`; strom podle verze licence `EDISON/EDISONMAIN/frmMain.vb:203-207`; za běhu `EDISON/EDUsers/EDUsers/EDUsers.vb:704-708`; CreateForm kopíruje License `EDISON/EDLIBRARY/clsForm.vb:706`; USERS ve stromu `EDISON2/EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/Tree.cls:791` | Host si licenci načte sám (`Activation.GetActiveLic`) a nastaví License, LicenseTable a ReadOnlyMode jako legacy EDPROCESS cesta (`EDISON/EDLIBRARY/clsForm.vb:5018-5029`, `:5076-5092`); args[12] nechat jako fallback. Oprava zároveň řeší režim čtení z I04. | 0,5 d | kódem |
| I03 | riziko | vydání, rollback | Uživatel si běžící shell připne na hlavní panel. Windows pak spouští `EDISONMAIN2.exe` napřímo, mimo EDLOADER. Vypnutí uzlu `NewShell` na něj nepůsobí a nový zip runtime se mu nerozbalí. Starý shell přímý start odmítá. | legacy stráž `EDISON/EDISONMAIN/Rozhrani (mainform)/frmNoMDI.vb:4700-4705`; v EDISON2 není AppUserModelID ani relaunch (grep `AppUserModel`, `RelaunchCommand` bez výskytu); vypnutí `EDISON2/docs/deploy.md:100-104` | Nastavit AppUserModelID s relaunch příkazem na `EDISON.exe`, nebo start bez značky od EDLOADERu přesměrovat přes `EDISON.exe`. | 0,5-1 d | kódem, chování pinu odhad |
| I04 | riziko | parita licence | Licence vyprší nebo má neplatný klíč. Nový shell zahodí hlášku z `GetActiveLic` a stav licence nečte, host nenastaví `ReadOnlyMode`. Moduly tak dál zapisují (legacy: hláška a režim čtení). Chybí i upozornění z nástěnky starého shellu: licence skončí za N dní, licence skončila, překročen počet vozidel nebo zaměstnanců, stará verze. | shell `EDISON2/EDISONMAIN2/dotnet/Startup/LicenceService.cs:31-32` (`info` se nepoužije), `State`/`ValidityTo` v EDISON2 grep bez výskytu; host `FormToRunGetter.cs:129-184`; legacy `frmNoMDI.vb:410-412`, `:1258-1259`, `EDISON/EDLIBRARY/clsForm.vb:5081-5082`, texty `EDISON/Licence/EDLicence/Activation.vb:268-284`; konzumenti např. `EDISON/MajorAPI/CAccess.cs:50-53`, `EDISON/ADSETTINGS/formADSETTINGSGrafikon.vb:97-101`; nástěnka `EDISON/EDISONMAIN/frmMain.vb:836-898` | Shell po načtení licence zobrazí `info` a v pruhu nástěnky upozorní na blížící se konec a překročené počty. Režim čtení v modulech dořeší I02. | 0,5-1 d | kódem |
| I05 | riziko | parita licence | Zákazník dokoupí modul a podpora mu řekne „Nástroje > Obnovit licenci". Nový shell licenci jen znovu načte z Caché a ohlásí „Licence byla aktualizována", z M-line nic nestáhne. Přihlášení admina (a uživatele s právem UpdateEdison) navíc nestahuje licenci ani neposílá feedback do M-line (`ALVA.FEEDBACK.Update`). Stažení kryje jen `GetActiveLic` 15 dní před koncem platnosti a instalace verze. | shell `EDISON2/EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs:1457-1461`, login přímo `EDISON2/EDISONMAIN2/dotnet/Windows/LoginWindow/UserAuthenticator.cs:34`; legacy `frmNoMDI.vb:3498-3505`, `EDISON/EDUsers/EDUsers/EDUsers.vb:170`, `:320-330`, `:344-365`; částečné krytí `Activation.vb:286-291`, `EDISON/EDISONAPI/clsUpdater.vb:2740` | V „Obnovit licenci" a po přihlášení admina nebo UpdateEdison volat `EDUsers.UpdateLicFromMlineServer` (EDUsers.dll je v bundlu), potom přestavět session jako dnes. | 0,5 d | kódem |
| I06 | riziko | parita frmNoMDI | Admin v hostovaném Nastavení naimportuje licenci, upraví nebo smaže databázi nebo změní „Databázi uživatelů". Starý shell po zavření obnovil hlavní okno a menu firem, případně ohlásil restart. Nový shell nic neudělá, strom a firmy zůstanou staré. Totéž po úpravách v USERS (`isUpdated`). Obejití: „Obnovit licenci" (jen licence) nebo restart. | výstup `EDISON/EDISONMAIN/formEDISONMAINSettingsShow.vb:505-530`, příznaky `:1114`, `:1142`, `:1186`, `:1430`; legacy obsluha `frmNoMDI.vb:958-998`; v hostu událost nevznikne `EDISON/EDLIBRARY/clsForm.vb:2075` | Host vrátí výsledek exit kódem (např. obnovit / restart) a shell ho obslouží v `LegacyModuleExited` (`EDISON2/EDControls/dotnet/EDViewModel/WindowNavigator.cs:41`). | 1 d | kódem |
| I07 | riziko | frmNoMDI v legacy kódu | Menu nového shellu „Nastavení > Jazyk" → OK → NRE na `DirectCast(Me.Owner, EDForm).MLProps` (Owner je Nothing), chybový dialog a Cancel. Data se před tím uloží, uživatel vidí chybu. Retrofit zapíná `WithSettings = True`, takže to nastane pokaždé. | `EDISON/EDISONMAIN/formEDISONMAINLanguageList.vb:806-816` (uložení `:799-804`); `EDISON/EDISONMAIN/ApplicationEvents.vb:69-73`; menu `EDISON2/EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs:323` | `TryCast` + kontrola Nothing, jazyk do MLProps jen když Owner existuje. | 1 h | kódem |
| I08 | riziko | parita MLProps | Tiskové hlavičky hostovaných modulů mají „ IČ: 12345678" bez názvu firmy a „Tisk: jnovak" místo celého jména. Hláška o obsazeném zámku ukáže login. Parametr uložený s UserName v klíči se mezi starým a novým shellem nepřenese. | legacy UserName = FullName `frmNoMDI.vb:722`, kopie `EDISON/EDLIBRARY/clsForm.vb:694`, `:703` (IdFaName); host `FormToRunGetter.cs:131`, IdFaName nenastaví; MZDY2 33 formulářů, např. `EDISON/MZDY2/formMZDY2Main.cs:654-655`, `EDISON/ISYJR/_formISYJR.cs:334-335`; zámek `EDISON/EDLIBRARY/clsForm.vb:826`, `:876`; klíč `EDISON/FAKTURACEEX/formFAKTURACEEXStalePlatbyKalendar.cs:212-214`, `:233` | Host: `UserName = fullName` (args[6] už ho nese). Shell pošle název firmy jako další pole kontraktu (A12 pozice). | 2-3 h | kódem |
| I09 | riziko | parita frmNoMDI | Přihlášení novým shellem se nezapíše do `^ALVA.USERS(...,"reports","login")`. Sestava USERS „Přehled přihlášení" pak tyto uživatele nezobrazí. | legacy `frmNoMDI.vb:3347`, `:4948` → `EDISON/EDUsers/EDUsers/EDUsers.vb:239-250` → `EDISON/EDUsers/EDUsers/CACHE/ALVA/SYS/Login.cls:119-138`; sestava `EDISON/USERS/CACHE/ALVA/USERS/formUSERSUsersPrintPrehledPrihlaseni.cls:35-54`; v EDISON2 volání `LogLogin` není (grep) | Po úspěšném VerifyLic, přihlášení jiného uživatele a přepnutí firmy volat `ALVA.SYS.Login.LogLogin` s názvem stanice; test šva. | 2 h | kódem |
| I10 | riziko (u některých konfigurací blokuje) | aktualizace | Nový shell pouští klientskou kontrolu verze na každou instalaci. Starý shell ji pouštěl jen stanicím ze seznamu `localPath`, ostatní bral jako serverové. (a) Stanice spouštěná ze share při prázdném `localSourcePath`: zdroj = `edisonPath` serveru (např. `D:\EDISON`), hláška „nelze zjistit serverovou verzi" a konec při každém startu. (b) Na serveru nebo TS při http `localSourcePath`: vynucená vlna spustí EDUPDATE s cílem = serverová instalace, ze které běží ostatní. | legacy `EDISON/EDISONMAIN/clsUpdate.vb:222-248` (IsServer), `:627-680`; shell `EDISON2/EDISONMAIN2/dotnet/Startup/Updater.cs:23`, `:104-143` (zdroj `:121-124`, vlna `:129-137`), shoda instalace `:187-189` (http vždy false) | Převzít `IsServer` (seznam `localPath` z LoadSettings), nebo aspoň za serverovou brát instalaci, jejíž VERZE.INF je tatáž jako v `edisonPath`. Před pilotem u zákazníka zkontrolovat `LoadSettings` a způsob spouštění stanic. | 1 d | kódem, konfigurace zákazníků neověřena |
| I11 | riziko | runtime, share | Dvě stanice spouštěné z téhož share startují poprvé po vydání. Mutex `Global\` platí jen na jednom stroji, takže druhá smaže rozpracovaný staging první. První pak přesune neúplný strom do `dotnet\`. Kontrola úplnosti hlídá jen hostfxr, deps.json a marker, a tak může projít. Všechny stanice ze share pak startují na rozbitém runtime a fallback není (známé). | `EDISON/EDLOADER/clsStartup.vb:442-455`, `:470-472`, `:501-507`, `:411-430`, `:486-490` | Zámek souborem na share (`FileShare.None` na lock souboru vedle `dotnet\`), nebo na UNC cestě runtime nerozbalovat. | 0,5 d | kódem, souběh odhad |
| I12 | riziko | TS, share | Pruh podpory ukládá data WebView2 do `EDISONMAIN2.exe.WebView2` vedle exe. Kam uživatel nesmí zapisovat (TS, Program Files, share jen pro čtení), tam pruh zůstane prázdný, chyba se zahodí. Na TS navíc sdílí cookies a sezení widgetu všichni uživatelé. | `EDISON2/EDISONMAIN2/dotnet/MiniApps/FontZoomWebView.cs:90` (bez prostředí), `:97`; složka naživo `C:\Edison\EDISONMAIN2.exe.WebView2\EBWebView` | `CoreWebView2Environment.CreateAsync` se složkou v `%LOCALAPPDATA%\m-line\EDISONMAIN2\WebView2`. | 2 h | kódem + naživo (složka), chování na TS odhad |
| I13 | riziko | Caché vydání | Projekty na DEV obsahují testové třídy (ALVA_EDISONMAIN2 5, ALVA_EDControls 5 + demo `EDIFExample`, `MockList`, ALVA_TEMPLATE_DESIGNER desítky), všechny dědí z `ALVA.EDTesting.MLTestCase`. `ALVA_EDTesting.prj` je jen v sekci `PATCH~EDISON2`, sekce `PATCH~TEMPLATEDESIGNER` ho nenese. Patch TD u zákazníka bez EDTesting, nebo jedna nezkompilovatelná testová třída (jako 28. 9. na REL, `a300778e`), zastaví instalaci Caché objektů: kompilace projektu končí na první chybě. `deploy.md` přitom tvrdí „bez Tests" a „Nevydávat ALVA_EDTesting". | manifest ř. 1143-1147, 1151-1152; `cache_project_info` (DEV); např. `EDISON2/TemplateDesigner/CACHE/ALVA/TemplateDesigner/Tests/SablonyTest.cls:11`; kompilace `EDISON/EDISONINS/CACHE/ALVA/SETUP/MainEx.cls:183-184` → `EDISON/EDISONAPI/clsUpdater.vb:2691-2692`; `EDISON2/docs/deploy.md:45-49` | Testy a dema do samostatných nevydávaných projektů. Do té doby přidat `ALVA_EDTesting.prj` i do sekce TD a srovnat deploy.md se skutečností. | 0,5 d | naživo (manifest, DEV) + kódem |
| I14 | riziko (zděděné) | TS, aktualizace | Na TS spustí jeden uživatel update. Legacy EDUPDATE bez timeoutu čeká, až skončí všechny procesy `EDISONMAIN2` na stroji, tedy i cizích uživatelů. Kdo mezitím spustí shell, dostane „Aktualizaci nelze spustit" (kopie běžícího EDUPDATE.exe) a shell skončí. Po úspěchu EDUPDATE ještě čeká na klik. Starý shell se chová stejně, pilot na TS to ale zasáhne celý server. | `EDISON/UPDATERCL/clsAPI.vb:1533-1540`, `:1553-1562`; `EDISON2/EDISONMAIN2/dotnet/Startup/AppBootstrapper.cs:186-199`, `Updater.cs:260-268`; `EDISON/UPDATERCL/frmClientUpdate.vb:467-471` | Pro pilot na TS: update serverové instalace mimo provoz, zapsat do deploy.md. Dlouhodobě EDUPDATE2 (má čekání s limitem). | 1 h (návod) | kódem |
| I15 | drobnost | vydání | Pět legacy DLL (C1 ×3, CacheClient, AegisImplicitMail) je vyňatých z bundlu, ale zůstávají v `EDISONMAIN2.deps.json` jako runtime assety. Host .NET chybějící asset z deps.json bere jako fatální chybu startu. EDLOADER je nekontroluje a fallback nemá. V manifestu jsou (ř. 107, 232, 305, 321, 343), riziko tedy hrozí jen při ručním nasazení. Komentář v csproj tvrdí, že je shell nenačítá. | `EDISON2/EDISONMAIN2/dotnet/EDISONMAIN2.csproj:61-69`; deps.json Release výstupu ř. 223-262; `EDISON/EDLOADER/clsStartup.vb:361-366` | Buď je vyřadit i z deps.json, nebo je přidat do `NewShellFilesPresent`. | 2 h | kódem, chování hostu odhad |
| I16 | drobnost | runtime | Stanice má rozbalený `dotnet\` a později dostane globální .NET 10. EDLOADER pak `dotnet\` přestane aktualizovat, exe ho přitom dál používá přednostně. Záplata runtime novým zipem se na ni nedostane. | `EDISON/EDLOADER/clsStartup.vb:435`; `EDISON2/SingleFilePublish.targets:20-21` | Aktualizovat existující `dotnet\` i při globálním runtime. | 1 h | kódem |
| I17 | drobnost | host, verze | Hostované moduly neposílají `EdisonVersion`, serverová kontrola verze je tedy přeskočí. Shell verzi posílá, jenže poll `maSeUkoncit` běží s Locks=2 a route `Query` kontroluje verzi jen při Locks=1. Kdo pracuje jen v modulech, na nesoulad verzí nenarazí. Host nemá ani handlery `EdisonActivityFailed`/`EdisonVersionFailed` (parita s legacy EDPROCESS). Zmírňuje to, že EDISONINS neinstaluje při aktivních uživatelích. | `FormToRunGetter.cs:112-127`; `EDISON/MLRest/MLRest/CACHE/ALWA/SYS/Rest.cls:83`, `:1705-1718`; shell `EDISON2/EDISONMAIN2/dotnet/Startup/SessionFactory.cs:41`, poll `LogoutWatcher.cs:66` | Host nastaví `EdisonVersion` z VERZE.INF (jako `frmNoMDI.vb:4942-4943`) a při `EdisonVersionFailed` formu zavře s hláškou. | 2 h | kódem |
| I18 | drobnost | ukončení z hostu | UTILITIES (Ctrl+W dvakrát, heslo, jen na serveru) > Init > „Změna čísla verze": `EDUser.Logout()` nad MLRest s GUID shellu smaže ACTIVITY zámek i business zámky celé session. `Application.Exit` pak ukončí jen UTILITIES. Shell a moduly spadnou až na dalším požadavku s Locks=1. | `EDISON/EDISONMAIN/formEDISONMAINInit.vb:82-88`; `EDISON/EDUsers/EDUsers/EDUsers.vb:191-206`; `EDISON/EDLIBRARY/clsForm.vb:729`, `:734`; spuštění `EdisonMainViewModel.cs:1601-1608` | Pod hostem (MainEdisonForm Is Nothing) neodhlašovat, jen skončit s exit kódem pro shell (viz I06). | 1 h | kódem |
| I19 | drobnost | aktivita | Požadavky hostovaného modulu jdou s prázdným `formId` (`ApplicationFullName`) do historie aktivity, dokud modul neotevře podformu přes CreateForm. Při zavření formy se nastaví „EDISON". Admin v „Aktivitě uživatelů" nevidí, ve kterém modulu uživatel je. | `EDISON/MLRest/MLRest/MLRest.cs:124`; `EDISON/MLRest/MLRest/CACHE/ALWA/SYS/Activity.cls:121`; legacy `EDISON/EDLIBRARY/clsForm.vb:728`, `:2036-2040`; host nenastaví `FormToRunGetter.cs:112-127` | Host nastaví `ApplicationFullName` = balíček\titulek jako CreateForm. | 1 h | kódem (zobrazení v USERS neověřeno) |
| I20 | drobnost | ukončení z hostu | Chyba při automatickém exportu do PDF v REPORTGENERATOR (`Application.Exit`, kód 2) nebo při přímém exportu TISK volaném z AUTOOPRAVNA (`Environment.Exit(1)`) ukončí celý hostovaný modul bez dialogu. Totéž dělala legacy EDPROCESS cesta. Shell to nahlásí jako pád; kód 2 se kryje s `LaunchFailed`. | `EDISON/REPORTGENERATOR/REPORTGENERATOR/formREPORTGENERATORReportDotNet.vb:485-488`; `EDISON/TISK/TISK/frmReportShow.vb:4736-4738`, `:4847-4853`; volání `EDISON/AUTOOPRAVNA/formAUTOOPRAVNAZakazkyList.vb:4490-4493` (Tree.cls:197) | Bez zásahu, parita. Jen do hlášení pádů rozlišit kód 2 z REPORTGENERATOR. | 1 h | kódem |
| I21 | drobnost | parita kontextu | Hostovaná forma nemá `MLProps.Jazyk` (lokalizace se přeskočí), `Scheme` (barva firmy), `MainEdisonForm` (položka nápovědy „Lokalizace" je aktivní a nic neudělá, kurzor čekání se nenastaví) ani `EDCaller` (chybový dialog bez nadřazené formy). Retrofit EDISONMAIN.exe při neprázdné hlášce licence vytvoří skrytou default instanci `frmNoMDI` a ta přepne kulturu vlákna. | `EDISON/EDLIBRARY/clsForm.vb:703-710` (co CreateForm kopíruje), `:439-447`, `:526-552`; `EDISON/EDComponents/EDComponents/EDButton.vb:174-181`, `:644`; `frmNoMDI.vb:410-412`, `:911-926` | Řešit jen při stížnosti. Jazyk a Scheme lze přidat do hostu levně. | 0,5 d | kódem |
| I22 | drobnost | Caché kontrakt | `ALVA.SYS.Login.Login` shell parsuje podle pozice 11 polí bez kontraktního testu. Dnes pozice sedí a DEV je shodný s repem, přeházení pořadí na serveru by ale tiše prohodilo admin, adresář nebo e-mail. Ostatní volání parsují pojmenované řádky nebo mají test šva. | `EDISON2/EDISONMAIN2/dotnet/Windows/LoginWindow/UserAuthenticator.cs:36-53`; `EDISON/EDUsers/EDUsers/CACHE/ALVA/SYS/Login.cls:69`; pokrytí `EDISON2/EDISON2.IntegrationTests/*` (Login není) | Kontraktní test v `EDISON2.IntegrationTests` nebo na Caché straně s testovacím uživatelem. | 2 h | kódem + naživo (drift) |
| I23 | drobnost | Caché projekty | V DEV projektu `ALVA_TEMPLATE_DESIGNER` jsou tři produkční třídy, které v repu nejsou (`API.InstancesList`, `API.Templates`, `Templates`), a 8 probe tříd. Čtyři testy z repa (např. `ZastavkoveJrAdDopravciTest`) v projektu chybí. V `ALVA_EDISONMAIN2` chybí `Tests.NewShellSwitchContractTest`. Obsah vydání se tak nedá odvodit z gitu. Zkontrolované třídy jsou zkompilované po poslední změně. | `cache_project_info` vs soubory v `EDISON2/*/CACHE`; `cache_get_source` (na disku nejsou); `^oddDEF(...,63)` < `^oddCOM(...,63)` | Srovnat projekt s repem, rozhodnout o třech třídách mimo git. | 1 h | naživo |
| I24 | drobnost | vydání | Kontrola DEBUG buildu v Compileru .NET 10 single-file exe nerozpozná (`Assembly.Load` selže a vrátí „není Debug"). DEBUG shell by u zákazníka měl verzi 999999.9: nikdy se neaktualizuje a obchází kontrolu verze. Publish profil je Release, riziko tedy hrozí jen při ruční kopii. | `EDISON/Compiler/CompilerApi/Obecne.cs:555-577`; `EDISON2/EDISONMAIN2/dotnet/Startup/VerzeInf.cs:24-29`; `EDISON2/EDISONMAIN2/dotnet/Properties/PublishProfiles/SingleFileCEdison.pubxml:4` | Pro exe nového shellu číst `DebuggableAttribute` z PE přes `MetadataLoadContext`, nebo porovnat VERZE/DevVersion. | 1 h | kódem |
| I25 | drobnost | smíšený provoz | Starý shell spouští EDUPDATE bez `WaitProcess`, takže ten na procesy nového shellu nečeká. Nastane při „Spustit starý shell" nebo zástupci `reason=2` vedle běžícího nového shellu (TS, share). Výsledkem je rollback. | `EDISON/EDISONMAIN/clsUpdate.vb:805-812`; `EDISON/UPDATERCL/clsAPI.vb:1553-1562` | Přidat `WaitProcess=EDISONMAIN2` i do legacy volání. | 1 h | kódem |

### Nedosažitelné z nového shellu (bod 1)

Kód dosažitelný jen z plochy starého shellu (`EDISON/EDISONMAIN/frmMain.vb:1352-1353`,
`:1667`, `:1689`, `:2069`, `:2091`, `EDISON/EDISONMAIN/EDDesktop.vb:466`, `EDISON/EDISONMAIN/modMain.vb:209`,
`:234-237`) se pod hostem nespustí. Čtení `MainEdisonForm._remotePuvodni` (`EDISON/EDLIBRARY/clsForm.vb:3636`,
`:3662`) je za kontrolou nebo bez volajícího. `NotifyQueryTLevelWarning`/`NotifyQueryError` spouští jen MLServer,
který host nevytváří (`clsForm.vb:2095-2112`, `:664`). `clsUpdate.StartUpdate` je dosažitelný jen z frmNoMDI
(Nastavení ho ošetřuje od `b52d0ec2c`). Hledání `frmNoMDI` v `Application.OpenForms` v repu není.
`RunningStandAloneProcesForm` se vytváří líně a nikdy není Nothing (`clsForm.vb:4213-4221`).

### K otázkám bodu 4 (vydání a aktualizace)

- **Nese vydání všechno?** Ne, chybí `EDISONRUNTIME.zip` (I01). `EDISONMAIN2.exe`, `EDControls.dll`
  a Caché projekty jsou v `PATCH~EDISON2` (ř. 1143-1147). Do FULL se dostanou, protože FULL bere řádky
  FULL i PATCH (`EDISON/SETUPWIZARD/frmMain.vb:1306-1307`). `deploy.md:33` přitom chce řádky FULL. FULL
  řádky mají `EDISONMAIN2API.exe`, jeho `.config`, `EDUPDATE2.exe`, `EDISON.exe` a pět legacy DLL.
  `EDISONMAIN.exe.config` (codeBase pin na host, `EDISON/EDISONMAIN/app.config:33-39`) je jen v sekci
  `PATCH~MHD2Linky` (ř. 1097, s mezerou na konci), patch EDISON2 ho tedy nenese.
- **Čeká EDUPDATE na všechny procesy?** Aktivní je legacy EDUPDATE (`Updater.cs:71`). Čeká jen na
  EDISONMAIN, EDTRAYMAIN a `WaitProcess=EDISONMAIN2` (`EDISON/UPDATERCL/clsAPI.vb:1533-1562`), na
  EDISONMAIN2API ani TemplateDesigner ne. Na samostatné stanici to kryje job, se kterým moduly skončí spolu
  se shellem (`WindowNavigator.cs:423`). Na TS moduly ostatních uživatelů běží dál, kopie selže a
  proběhne rollback (I14). EDUPDATE2 je čeká (`EDISON2/EDUPDATE2/dotnet/UpdateEngine.cs:49`), zatím se ale
  nepoužívá.
- **Stanice spouštěná ze share:** I10 (a), I11, I12.
- **Terminálový server:** I10 (b), I12, I14. Rozbalení runtime chrání mutex napříč sezeními jednoho
  stroje (`clsStartup.vb:442-455`), to je v pořádku.
- **Runtime a vynucená vlna:** updatery `dotnet\` nepřepisují. Legacy EDUPDATE kopíruje jen kořen,
  `EDFiles`, `PS` a `locales` (`EDISON/UPDATERCL/clsAPI.vb:1157`). Zip je běžný kořenový soubor,
  vlna ho tedy přenese na každou stanici (asi 70 MB). EDLOADER rozbalí novou verzi vedle staré a nic
  nepřepisuje (`clsStartup.vb:511-516`). Na stanici s globálním runtime se ale zip už nerozbalí (I16).

### Oblast 5 (Caché)

- **Drift a kompilace: bez nálezu.** Třídy volané shellem (`ALVA.SYS.Login`, `ALVA.EDISON.Main`,
  `ALVA.SYSTEM.Main`, `ALVA.SETUP.Main`, `ALVA.EDISONMAIN2.*`, `ALVA.EDControls.*`,
  `ALVA.EDTesting.MLTestCase`) jsou na DEV obsahově shodné s repem. MCP u části hlásí „different" jen
  kvůli koncovým mezerám v řádcích `/// `. Zkontrolované třídy jsou zkompilované po poslední změně
  definice: hromadná kompilace proběhla 28. 9. kolem 12:30. Poslední běh Caché testů TD skončil
  171/171 a integrační sada 16/16 (`.husky/cache-tests.log`, `.husky/integration-tests.log`).
- **Kontrakty.** Pozičně se parsuje `Login` (I22), `VerifyLic`, `ChangePassword` a `GetEdisonAccess`.
  `GetEdisonAccess` a `GetUserDefaultForm` mají test šva. `LoadUser`, `LoadSettings` a
  `LoadDatabaseList` parsují pojmenované řádky.
- **Export a instalace:** viz I13 a I23.

## 3. Parita frmNoMDI vs. nový shell

| Odpovědnost frmNoMDI | Legacy | Nový shell | Stav |
|---|---|---|---|
| Poll `maSeUkoncit` (60 s), odpočet, ukončení | `frmNoMDI.vb:5013-5094`, `:5097-5119` | `EDISON2/EDISONMAIN2/dotnet/Startup/LogoutWatcher.cs:30`, `:66` (Locks=2), okno odpočtu `AppBootstrapper.cs:536` | má |
| Hláška a konec při `EdisonActivityFailed` / `EdisonVersionFailed` | `frmNoMDI.vb:607-609`, `:5213-5232` | `AppBootstrapper.cs:470-487` | má (v hostu chybí, I17) |
| Varování `EdisonConnectionFailed` | `frmNoMDI.vb:608` | nenalezeno | chybí, dopad malý (reconnect je známé F10) |
| Tray ikona (jen při odpočtu), minimalizace do tray | `frmNoMDI.designer.vb:150-168`, `frmNoMDI.vb:5099-5111`; minimalizace do tray neexistuje | odpočet je vlastní okno | nahrazeno |
| Zprávy a sdílení (`DefMessage`, `DefShare`) | frmNoMDI je nezpracovává, jde o typy zámků v MLRest pro agendy | - | není co převzít |
| `CloseEdison`: uložení firmy a uživatele, Logout + LockClear, Logout a kill samostatných procesů, Exit | `frmNoMDI.vb:2731-2769`, `:2460`, `:2773` | `SessionLogout.cs:32-40`, `WindowNavigator.CloseAll`, poslední firma `CompanySwitcher.cs:112` (LocalPrefs) | má; poslední firma jen lokálně, ne v Caché a registru |
| Strom procesů pro timeout (`UpdateSeznamProcesu`) | `frmNoMDI.vb:263-279`, `:329` | moduly sdílí GUID shellu (`FormToRunGetter.cs:123-127`) | vědomě nahrazeno (`.claude/reviews/hd13132-feedback.md`, F17) |
| Autologin | `frmNoMDI.vb:698-700`, `EDUsers.vb:128-130` | `AppBootstrapper.cs:109`, `:420` | má |
| `IsDomainMline` (M-line uzly, debug menu) | `frmNoMDI.vb:4731-4734`, `frmMain.vb:1099` | `MlineDomainDetector.cs:24`, `ModuleTreeState.cs:16` | má |
| Start bez EDLOADERu odmítnout | `frmNoMDI.vb:4700-4705` | chybí | I03 |
| Kontrola verze klienta vs. server | `frmNoMDI.vb:620-631`, `clsUpdate.vb:623-718` | `Updater.cs:104-143` | má, bez `IsServer` (I10) |
| Licence: načtení, hláška, režim čtení | `frmNoMDI.vb:405-435`, `:1251-1270` | `LicenceService.cs:15-33` | částečně (I04) |
| VerifyLic, okno aktivity pro admina | `frmNoMDI.vb:2569-2587` | `LicenceVerifier.cs:26-67` | má |
| Update při startu | `frmNoMDI.vb:4917`, `:4400-4588` | `AppBootstrapper.cs:179-210` | má |
| Nastavení stanice (EDISONini.xml), setup | `frmNoMDI.vb:4750-4757` | `SettingsLoader.cs`, `SetupWindow` | má |
| Založení databáze při startu | `frmNoMDI.vb:4835-4844` | nenalezeno | chybí (jen první instalace, nízký dopad) |
| Changelog po přihlášení | `frmNoMDI.vb:4935` | `EdisonMainViewModel.cs:409` | má |
| Vynucená změna hesla | `frmNoMDI.vb:734-769` | `AppBootstrapper.cs:118` | má |
| Nástěnka: konec licence, počty vozidel a zaměstnanců, stará verze | `frmMain.vb:836-898` | nová verze ve stavovém řádku (`AppBootstrapper.cs:206-209`), ostatní nenalezeno | částečně (I04) |
| `LogLogin` | `frmNoMDI.vb:3347`, `:4948` | chybí | I09 |
| Licence z M-line a feedback u admina | `EDUsers.vb:170`, `:320-365` | chybí | I05 |
| `EdisonVersion` do MLRest | `frmNoMDI.vb:4942-4943` | `SessionFactory.cs:41` | má (v hostu chybí, I17) |
| `SetMainEdisonForm` pro formy | `frmNoMDI.vb:4944`, `clsForm.vb:738` | host nenastaví | vědomě (host), důsledky I21 |
| Zpracování výstupu formulářů (`NotifyDataSent`) | `frmNoMDI.vb:958-998` | chybí | I06 |
| Nástroje: obnovit licenci | `frmNoMDI.vb:3498-3505` | `EdisonMainViewModel.cs:1457-1461` | jiné chování (I05) |
| Nástroje: zámky, aktivita, úlohy, jazyk, kopie EDISONu, reset oken | `frmNoMDI.vb:3399-3425` | `EdisonMainViewModel.cs:288-324` | má (jazyk viz I07) |
| Nástroje: log, TeamViewer | `frmNoMDI.vb:3399-3410` | nenalezeno | chybí, nízký dopad |

## 4. Předpoklady místo otázek

- Pilot znamená zákazníka s `NewShell=1` pro celou instalaci. Stanice mají lokální kopii aktualizovanou ze
  serverového zdroje. Terminálový server a spouštění ze share jsem posuzoval jako možné varianty, jejich
  výskyt u pilotních zákazníků neznám.
- Závažnost „blokuje pilot" má jen to, co pilotní zákazník potká v běžném provozu bez rozumného obejití,
  nebo co rozbije zapnutí či vypnutí nového shellu. Podmíněné nálezy (I10, I11) jsou vedené jako riziko
  s poznámkou, kdy blokují.
- Za referenci legacy chování beru obě cesty starého shellu: in-process dialog (Owner = frmNoMDI) a
  samostatný proces EDPROCESS. Kde host odpovídá EDPROCESS, píšu paritu.
- Obsah vydání posuzuji podle manifestu TEST (GET 29. 9.), ne podle repo kopie `FILES_DESCRIPTION.INF`
  (je stale). Obsah Caché projektů na REL jsem nečetl (`release_*` zakázáno), předpokládám kopii DEV
  projektů (Compiler DEV → REL).
- I01 předpokládá, že pilotní stanice nemají globální .NET 10. S admin instalací runtime I01 neblokuje.
- Výchozí chování hostu .NET při chybějícím assetu z deps.json (I15) a sdílení WebView2 dat mezi uživateli
  (I12) znám z dokumentace platformy, nespouštěl jsem je.
- Odhady jsou hrubé a odpovídají práci vývojáře bez AI (pochopení, oprava, testy), bez testů šva a bez
  vydání. S AI chatem se zkrátí hlavně psaní kódu. Review, buildy a ruční ověření zůstávají.
- Známé otevřené body (F10, F11, F17, A4, A12, zástupci bez filtru práv, hlášení se snímkem, fallback
  EDLOADERu, plná serverová aktualizace z Nastavení, vynucená vlna a HTTP smyčka legacy EDUPDATE) jsem
  neopakoval. Odkazuji na ně jen tam, kde nový nález na nich stojí.

## 5. Co zůstalo neprojité

- Nic neběželo živě kromě čtení Caché a manifestu: ani stanice bez .NET 10 (I01), ani připnutí (I03),
  ani chybějící DLL (I15), ani TS (I12, I14).
- Konfigurace zákazníků (`LoadSettings`: `edisonPath`, `localSourcePath`, `localPath`), práva zápisu
  na TS a share, verze OS.
- Konzumenty `ReadOnlyMode` (1344 výskytů) a `MLProps.UserName` jsem prošel jen vzorkem. Legacy plochu
  `frmMain.vb` jen po částech.
- Příznak licence u práv (I02) jsem hledal jen jako literál v definicích `GetECL` v `.cls` souborech repa.
  Definici skládanou za běhu by hledání nenašlo.
- Serverové zobrazení historie aktivity v USERS (I19). `EDTRANSFER2` a `UPDATERTR` jsem neprošel.
- Kompilační stav jsem ověřil jen u vzorku tříd (shell, EDTesting, třídy DEV projektů mimo repo), ne
  u všech 74 členů `ALVA_TEMPLATE_DESIGNER`.
- Mimo rozsah, ale stojí za pozornost: `ALVA.SETUP.Main.LoadSettings` vrací řádek `admin` se jménem a
  heslem (`EDISON/EDISONINS/CACHE/ALVA/SETUP/Main.cls`, settings(102)). Metodu volá EDLOADER před
  přihlášením.

## 6. Stav řešení (2026-09-29 odpoledne)

Opravy dělalo pět paralelních chatů. Commity bez předpony repa jsou v EDISON2, s „EDISON" v legacy repu.

| Nález | Stav | Commit |
|---|---|---|
| I01 | hotovo, zip je v manifestu TEST (ř. 358) | manifest na Cloud2 |
| I02 | hotovo; host skládá licenci z polí shellu [12] a [18]-[20], když je v pořádku a platná, jinak volá `GetActiveLic` jako dřív | EDISON `e468ada4f`, EDISON `201f968ca`, `40684fe4` |
| I03 | hotovo, start bez značky `reason=2` jde přes `EDISON.exe` | EDISON `c62c12ed2`, `3b8f3b0f` |
| I04 | hotovo: hláška a upozornění v shellu, režim čtení v modulech s I02 | `c3804594`, EDISON `e468ada4f` |
| I05 | hotovo | `c3804594` |
| I06 | otevřené | - |
| I07 | hotovo | EDISON `a06bedf95` |
| I08 | hotovo; celé jméno z args[6], název firmy dotazem `ALVA.EDISON.Main.LoadSettings` (~90 ms) | EDISON `e468ada4f` |
| I09 | hotovo | `c3804594` |
| I10 | hotovo, parita s legacy `IsServer` | `e44439f0` |
| I11 | hotovo, zámek `dotnet.lock\lock` | EDISON `c62c12ed2` |
| I12 | hotovo, jeho test má výjimku v hlídači `MachineLocalStateGuardTests` | `c05b71c9`, `3479c228` |
| I13 | odloženo, vydávání testů se teď nemění; deploy.md popisuje skutečný stav | `a7be0193` |
| I14 | hotovo | `24b6ea6b` |
| I15 | nepotvrdilo se: host .NET 10 chybějící volnou DLL při startu nekontroluje, změněn jen komentář | `73b017db` |
| I16 | hotovo | EDISON `c62c12ed2` |
| I17 | hotovo | EDISON `e468ada4f` |
| I18 | hotovo | EDISON `240df31a1` |
| I19 | hotovo | EDISON `e468ada4f` |
| I20 | otevřené | - |
| I21 | odloženo | - |
| I22 | hotovo | `c3804594` |
| I23 | odloženo s I13 | - |
| I24 | vynecháno, Compiler se teď nemění | - |
| I25 | hotovo | EDISON `96e8fba66` |

Deploy.md k I03, I10, I11, I12, I16 a I19: `42acd48e`.

Zbývá: ruční ověření podle checklistů chatů (strom práv a tisk MZDY2, TS a share, dvě stanice nad jedním
share a připnutí na hlavní panel), push EDISON2 a I06 s I20.
