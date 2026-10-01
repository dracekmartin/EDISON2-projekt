# Vydání shellu EDISONMAIN2 - co distribuovat

Jen to, co existuje nebo se mění kvůli novému shellu. Zbytek (C1 knihovny, EDCore...) chodí
k zákazníkům se starým shellem už dnes. TemplateDesigner je vydaný samostatně a sem nepatří;
sdílí ale `EDControls.dll` - breaking change jejího API znamená vydat i TD (root `CLAUDE.md`).

## Binárky

Z repa EDISON2 (publish profilem `SingleFileCEdison` do `C:\Edison\`, soubory vybírat ručně,
složku nikdy nezipovat - leží tam stale `.pdb`/`.deps.json` a dev `VERZE.INF`):

- `EDISONMAIN2.exe` - single-file bundle (obsahuje `EDISONMAIN2.LegacyMiniApps`, AvalonDock,
  WebView2 i snapshot `MLRest`, `EDLicence`, `EDUsers`, `EDISONAPI`, `EDCore` a `EDLibrary` z `C:\Edison\`
  stroje, kde se publikovalo - publikovat jen tam, kde `C:\Edison\` odpovídá vydávané legacy verzi
  a je v Release: Debug `EDLicence` stahuje licenci z `127.0.0.1` a chyby ukazuje dialogem `ERROR`)
- `EDControls.dll` - volná vedle exe
- `EDISONRUNTIME.zip` - privátní .NET 10 runtime (`tools/build-runtime-zip.ps1`: oficiální
  `dotnet-runtime` + `windowsdesktop-runtime` win-x64 sloučené, SHA512 z metadat Microsoftu, ~70 MB).
  Záplaty runtime vydáváme **my** (Microsoft Update ho nevidí): nová patch verze = nový zip + verze

Z repa EDISON (build do `C:\Edison`):

- `EDISONMAIN2API.exe` + `EDISONMAIN2API.exe.config` - generický host agend (projekt je od 06/2026
  WinExe; zbylá `.dll` u zákazníka stíní `.exe`, viz codeBase pin v `EDISONMAIN.exe.config`)
- existující soubory, všechno buildit z legacy HEAD ≥ `d64a020f1` (2026-09-23, přepínač shellu;
  `git log --grep=EDISONMAIN2`): `EDISONMAIN.exe` (retrofit `OnRun`), `EDISONMAIN.exe.config`
  (codeBase pin), `EDLibrary.dll` (shortcut sink), `EDUsers.dll`, `EDCore.dll`, `MLRest.dll`,
  `EDLicence.dll`, `EDISONAPI.dll`, `USERS.exe`, `UTILITIES.exe`, `EDISON.exe` (EDLOADER).
- `EDUPDATE.EXE` - klientský updater obou shellů, build ≥ 2026-06-17 (`WaitProcess`, `49d19295d9`),
  starší na zavření nového shellu nepočká. Bez něj ve zdrojovém adresáři serveru nový shell po
  detekci nové verze nenastartuje (`CopyUpdater` selže s popupem). `EDUPDATE2.exe` (repo EDISON2)
  jen po přepnutí shellu na něj (`Updater.ActiveKind`).

Řádky manifestu `FILES_DESCRIPTION.INF`: FULL `EDISONMAIN2API.exe`, `EDISONMAIN2API.exe.config`
a `FULL~~DLL~...\EDISONRUNTIME.zip`; sekce `PATCH~EDISON2` `EDISONMAIN2.exe`, `EDControls.dll`
a Caché projekty níže. `FULL~~EXE~...\EDUPDATE2.exe` má jen TEST manifest, do ostrého patří před
přepnutím `Updater.ActiveKind`. Ostatní soubory výše mají řádky z legacy (`USERS.exe` v
`PATCH~USERS`, `EDISONMAIN.exe.config` jen v `PATCH~MHD2Linky`). FULL bere řádky FULL i PATCH. Patch
se staví jen jako XML PATCH a z řádků zaškrtnutých balíčků bere jen XML a INF
(`EDISON/SETUPWIZARD/frmMain.vb`): všechny binárky shellu včetně `EDISONMAIN2.exe`, `EDControls.dll`
a `EDISONRUNTIME.zip` (i záplata runtime) chodí k zákazníkovi jen FULL verzí, patch EDISON2 nese jen
Caché projekty. Runtime jde zipem, rozbalený strom distribuce neunese (plochý SETUP.DAT, strop 999
souborů, duplicitní jména `*.resources.dll`).

## Caché projekty

Nové jsou v sekci `PATCH~EDISON2` (řádky `PATCH~EDISON2~XML~<projekt>.prj`). Projekt musí existovat
na REL Caché (Compiler DEV→REL), `.project` v repu je jen jméno:

- `ALVA_EDISONMAIN2` - `ALVA.EDISONMAIN2.API.Tree`, `ALVA.EDISONMAIN2.EDInputForm.UserProfile`
  a testy `ALVA.EDISONMAIN2.Tests.*`
- `ALVA_EDControls` - `UserData`, `DesktopShortcuts`, `EDInputForm.{Form,Field,Group,Item}`,
  `EDList.{List,Column}` **a include `EDInputForm.inc`** (bez něj se třídy nezkompilují); navíc
  testy `ALVA.EDControls.Tests.*` a dema `EDIFExample`/`MockList`
- `ALVA_EDTesting` - test framework (`MLTestCase`, `TestRunner`); testy obou projektů z něj dědí,
  proto jde ve vydání s nimi.

Past: instalace u zákazníka kompiluje projekty po jednom a končí na první chybě
(`EDISON/EDISONINS/CACHE/ALVA/SETUP/MainEx.cls`). `ALVA_TEMPLATE_DESIGNER` nese testové třídy
odvozené z `MLTestCase`, sekce `PATCH~TEMPLATEDESIGNER` ale `ALVA_EDTesting` nemá: patch TD
u zákazníka, který EDTesting ještě nedostal, na kompilaci testů skončí. Stejně instalaci zastaví
jediná nezkompilovatelná testová třída v kterémkoli vydávaném projektu.

Existující, které shell volá (řádky jsou; verze musí nést změny pro shell): FULL `ALVA_SYS_Login`,
`ALVA_SETUP` (řádek `newShell` v `LoadSettings` pro přepínač níže), `ALVA_SYSTEM`, `ALVA_EDISON`,
`ALVA_LIBRARY`, `ALVA_LICENCE`, `ALWA_MLREST`;
PATCH `ALVA_USERS`, `ALVA_ISY_ISYCloud` (Telemetry), `ALVA_MHD2_PlanDopravy` (DispecinkMiniApp),
`ALVA_MZDY2` (FiltrZam2 dual-write).

## Ops prerekvizity

- `FILES_DESCRIPTION.INF` na Cloud2 (edituje se ručně) dostane řádky výše; bez nich nevznikne
  záznam v `EDISONCRC.bin` a updater soubor na stanice nikdy nezkopíruje.
- Compiler potřebuje záznam projektu s těmi binárkami a Caché projekty, jinak se do REL nic nepřenese.
- Stanice: x64 Windows a WebView2 Evergreen runtime (bez něj se pruh podpory tiše nevyplní).
  Data WebView2 má každý uživatel Windows v `%LOCALAPPDATA%\m-line\EDISONMAIN2\WebView2`; složku
  `EDISONMAIN2.exe.WebView2` vedle exe ze starších buildů lze smazat.
  .NET 10 instalovat nemusí: publish je framework-dependent, ale exe hledají runtime nejdřív
  v `dotnet\` vedle sebe (`SingleFilePublish.targets`) a EDLOADER ho tam rozbalí z `EDISONRUNTIME.zip`,
  když globální instalace chybí.
- Serverový adresář (zdroj stanic: UNC share, nebo http URL jako u legacy) musí mít `VERZE.INF`
  a `EDUPDATE.EXE`; shell bez `VERZE.INF` vedle exe skončí hláškou, instaluje se tedy vždy
  do adresáře EDISONu, nikdy samostatně.
- Klientský update (EDUPDATE i vynucenou vlnu) dostane jen stanice ze seznamu počítačů v Nastavení >
  Aktualizace (`localPath`: jméno PC, `\\PC\...` nebo IPv4, plné doménové jméno nefunguje; legacy
  `clsUpdate.IsServer`). Ostatní instalace (server, TS, stanice spouštěné ze share) shell bere jako
  serverové a jen porovná `VERZE.INF` s verzí v Caché. Prázdný seznam znamená, že jsou serverové
  všechny, lokální klienti proto v seznamu být musí. Flag „update" v Nastavení na to vliv nemá (legacy
  parita); update check vynechá jen server bez nainstalovaného release (prázdné
  `^EDISON("System","Update",2)`, dev).
- Moduly v hostu posílají serveru verzi z `VERZE.INF` stanice (dev `999999.9` ne) a při nesouladu
  skončí hláškou „Rozdílná verze Caché a verze EDISON, modul bude ukončen." stejně jako shell.
- Setup wizard shellu nikdy nespouštět v serverovém instalačním adresáři: přepíše `EDISONini.xml`,
  který EDUPDATE roznáší na všechny stanice (a při updatu ho stejně vrátí).

Připojení shell čte z `EDISONini.xml` serverové instalace; dev kopii z repa publish nevydává
(`CopyToPublishDirectory="Never"` v csproj).

## Terminálový server

TS s vlastní instalací musí být v seznamu `localPath` (pravidlo v Ops prerekvizitách), jinak ho update
přeskočí a po instalaci verze skončí hláškou „Nesouhlasí verze programů a objektů". TS, který běží
přímo ze serverové instalace, v seznamu být nemá: soubory mu obnoví instalace verze na serveru.

Aktivní updater (legacy `EDUPDATE.EXE`) čeká bez limitu, až na stroji skončí všechny procesy
`EDISONMAIN2`, `EDISONMAIN` a `EDTRAYMAIN`, i v sezeních ostatních uživatelů, a po dokončení čeká na
potvrzení hlášky. Kdo mezitím spustí shell, dostane „Aktualizaci nelze spustit" (kopie běžícího
`EDUPDATE.EXE` selže) a shell skončí. Soubor zamčený jiným procesem z adresáře EDISONu (agenda,
TemplateDesigner, naplánovaná úloha) shodí kopii a updater vrátí starou verzi. Novou verzi i vynucenou
vlnu proto na TS nasazuj mimo provoz:

1. Zablokovat nová přihlášení (`change logon /drain`), přihlášené odhlásit; `query user` ukáže jen
   vlastní sezení.
2. Nainstalovat verzi na serveru.
3. V PowerShellu spuštěném jako správce `Get-Process | ? Path -like '<adresář EDISONu>\*'` nic nevrátí
   (pokrývá i `EDTRAYMAIN`, zbylý `EDUPDATE`, `EDISONMAIN2API` a `TemplateDesigner`).
4. Správce v jediném sezení spustí EDISON zástupcem, u instalace pod Program Files jako správce
   (`EDUPDATE.EXE` běží `asInvoker`, bez práva zápisu kopie selže). Updater doběhne, hlášku potvrdí,
   shell znovu nastartuje a `VERZE.INF` v adresáři TS nese novou verzi.
5. `change logon /enable`.

`EDUPDATE2` (po přepnutí `Updater.ActiveKind`) nečeká donekonečna: po 2 minutách update vzdá. TS se
tak ani s ním za provozu neaktualizuje, postup platí i pro něj.

## Zapnutí u zákazníka

Zástupce na ploše míří na `EDISON.exe` (EDLOADER). Ten místo `EDISONMAIN.EXE` spustí
`EDISONMAIN2.exe`, jen když platí obojí:

- na serveru zákazníka je `^|"MLINE"|EDISON("System","NewShell")=1` (EDLOADER ho čte přes
  `ALVA.SETUP.Main.LoadSettings`, řádek `newShell`),
- vedle `EDISON.exe` jsou `EDISONMAIN2.exe`, `EDControls.dll`, `EDISONMAIN2API.exe` a runtime:
  kompletní `dotnet\` vedle exe, nebo globální x64 .NET 10 Desktop Runtime (`%ProgramW6432%\dotnet`).
  `EDISONRUNTIME.zip` EDLOADER rozbalí do `dotnet\`, když globální chybí, a existující `dotnet\`
  aktualizuje vždy (exe ho preferují i před globálním).

Runtime se rozbaluje přes staging a atomický přesun pod zámkem `dotnet.lock\lock`. Zámek platí pro
sezení TS i pro stanice spouštěné ze share a po pádu držitele ho uvolní systém; rozbalení potřebuje
právo zápisu do adresáře EDISONu. Na zámek čeká jen první instalace, nejvýš 2 minuty, a když selže,
spustí se starý shell. Záplatu existujícího `dotnet\` stanice buď provede, nebo, když zámek drží jiná
stanice nebo záplata selže, rovnou spustí nový shell na stávající verzi. Existující, ale neúplný
`dotnet\` znamená starý shell: exe by na globální runtime nepřešel.

Jinak, i když selže `LoadSettings`, spustí starý shell; nedostupný server zastaví start už
v `CheckLogin` (beze změny). Fallback končí spuštěním procesu: nový shell, který spadne hned po
startu, starý shell nespustí; náprava je vypnutí uzlu níže. Přepínač tedy potřebuje obojí:
`EDISON.exe` s přepínačem a `ALVA_SETUP`, jehož `LoadSettings` řádek vrací. Chybí-li kterékoli,
uzel nemá účinek. Uzel leží mimo `^EDISON("System","Update")`, protože ten maže každé uložení
legacy Nastavení.

Nastavuje se v Caché Terminalu na serveru zákazníka. Rozšířený odkaz `^|"MLINE"|` funguje
z libovolného namespace:

- zapnout: `s ^|"MLINE"|EDISON("System","NewShell")=1`
- zkontrolovat: `w ^|"MLINE"|EDISON("System","NewShell")`
- vypnout: `k ^|"MLINE"|EDISON("System","NewShell")`

Zapnutí i vypnutí platí od příštího startu, běžící shelly dojedou. Platí i pro shell připnutý na hlavní
panel nebo spuštěný přímo z `EDISONMAIN2.exe`: start bez značky `reason=2` shell přesměruje přes
`EDISON.exe`. `EDISON.exe` a `EDISONMAIN2.exe` proto vydávej spolu; se starším EDLOADERem bez značky
to funguje taky, jen s jedním přesměrováním navíc.

Přepínač platí pro celou instalaci, tedy pro všechny firmy na serveru. Jedna stanice se vrátí ke
starému shellu zástupcem `EDISONMAIN.EXE reason=2`, k novému shellu bez EDLOADERu vede zástupce
`EDISONMAIN2.exe reason=2`; oba obcházejí brány EDLOADERu. Na sdíleném dev serveru hodnota 1 přepne
každou stanici s novým EDLOADERem.
