# Vydání shellu EDISONMAIN2 - co distribuovat

Jen to, co existuje nebo se mění kvůli novému shellu. Zbytek (C1 knihovny, EDCore...) chodí
k zákazníkům se starým shellem už dnes. TemplateDesigner je vydaný samostatně a sem nepatří;
sdílí ale `EDControls.dll` - breaking change jejího API znamená vydat i TD (root `CLAUDE.md`).

## Binárky

Z repa EDISON2 (publish profilem `SingleFileCEdison` do `C:\Edison\`, soubory vybírat ručně,
složku nikdy nezipovat - leží tam stale `.pdb`/`.deps.json` a dev `VERZE.INF`):

- `EDISONMAIN2.exe` - single-file bundle (obsahuje `EDISONMAIN2.LegacyMiniApps`, AvalonDock,
  WebView2 i snapshot `MLRest.dll`/`EDLicence.dll`/`EDISONAPI.dll` ze stroje, kde se publikovalo -
  publikovat jen tam, kde `C:\Edison\` odpovídá vydávané legacy verzi)
- `EDControls.dll` - volná vedle exe
- `EDUPDATE2.exe` - klientský updater; bez něj ve zdrojovém adresáři serveru shell po detekci
  nové verze nenastartuje (`CopyUpdater` selže s popupem)

Z repa EDISON (build do `C:\Edison`):

- `EDISONMAIN2API.exe` + `EDISONMAIN2API.exe.config` - generický host agend (projekt je od 06/2026
  WinExe; zbylá `.dll` u zákazníka stíní `.exe`, viz codeBase pin v `EDISONMAIN.exe.config`)
- existující soubory, build musí být z legacy HEAD ≥ 2026-09-11 (`git log --grep=EDISONMAIN2`):
  `EDISONMAIN.exe` (retrofit `OnRun`), `EDISONMAIN.exe.config` (codeBase pin), `EDLibrary.dll`
  (shortcut sink), `EDUsers.dll`, `EDCore.dll`, `MLRest.dll`, `EDLicence.dll`, `EDISONAPI.dll`,
  `USERS.exe`, `UTILITIES.exe`, `EDISON.exe` (EDLOADER, přepínač shellu níže: build ≥ 2026-09-23).
  `EDUPDATE.EXE` zůstává jen starému shellu.

Nové řádky `FILES_DESCRIPTION.INF` (FULL, typ EXE/DLL): `EDISONMAIN2.exe`, `EDControls.dll`,
`EDUPDATE2.exe`, `EDISONMAIN2API.exe`, `EDISONMAIN2API.exe.config`; řádek `EDISONMAIN2API.dll`
zrušit. Ostatní soubory výše řádky už mají.

## Caché projekty (řádky `FULL~~XML~<projekt>.prj`)

Nové - projekt musí existovat na REL Caché (Compiler DEV→REL), `.project` v repu je jen jméno:

- `ALVA_EDISONMAIN2` - `ALVA.EDISONMAIN2.API.Tree`, `ALVA.EDISONMAIN2.EDInputForm.UserProfile`;
  bez package `Tests`
- `ALVA_EDControls` - `UserData`, `DesktopShortcuts`, `EDInputForm.{Form,Field,Group,Item}`,
  `EDList.{List,Column}` **a include `EDInputForm.inc`** (bez něj se třídy nezkompilují); bez
  `Tests` a bez demo `EDIFExample`/`MockList`
- **Nevydávat** `ALVA_EDTesting` (test framework)

Existující, které shell volá (řádky jsou; verze musí nést změny pro shell): FULL `ALVA_SYS_Login`,
`ALVA_SETUP` (řádek `newShell` v `LoadSettings` pro přepínač níže), `ALVA_SYSTEM`, `ALVA_EDISON`,
`ALVA_LIBRARY`, `ALVA_LICENCE`, `ALWA_MLREST`;
PATCH `ALVA_USERS`, `ALVA_ISY_ISYCloud` (Telemetry), `ALVA_MHD2_PlanDopravy` (DispecinkMiniApp),
`ALVA_MZDY2` (FiltrZam2 dual-write).

## Ops prerekvizity

- `FILES_DESCRIPTION.INF` na Cloud2 (edituje se ručně) dostane řádky výše; bez nich nevznikne
  záznam v `EDISONCRC.bin` a EDUPDATE2 soubor na stanice nikdy nezkopíruje.
- Compiler potřebuje záznam projektu s těmi binárkami a Caché projekty, jinak se do REL nic nepřenese.
- Stanice: **.NET 10 Desktop Runtime x64** (publish je framework-dependent) a WebView2 Evergreen
  runtime (bez něj se pruh podpory tiše nevyplní). Legacy stack to nepotřeboval.
- Serverový adresář musí mít `VERZE.INF` a `EDUPDATE2.exe`; shell bez `VERZE.INF` vedle exe skončí
  hláškou, instaluje se tedy vždy do adresáře EDISONu, nikdy samostatně.
- Setup wizard shellu nikdy nespouštět v serverovém instalačním adresáři: přepíše `EDISONini.xml`,
  který EDUPDATE roznáší na všechny stanice (a při updatu ho stejně vrátí).

Připojení shell čte z `EDISONini.xml` serverové instalace; dev kopii z repa publish nevydává
(`CopyToPublishDirectory="Never"` v csproj).

## Zapnutí u zákazníka

Zástupce na ploše míří na `EDISON.exe` (EDLOADER). Ten místo `EDISONMAIN.EXE` spustí
`EDISONMAIN2.exe`, jen když platí obojí:

- na serveru zákazníka je `^|"MLINE"|EDISON("System","NewShell")=1` (EDLOADER ho čte přes
  `ALVA.SETUP.Main.LoadSettings`, řádek `newShell`),
- vedle `EDISON.exe` jsou `EDISONMAIN2.exe`, `EDControls.dll`, `EDISONMAIN2API.exe` a stanice má
  x64 .NET 10 Desktop Runtime ve výchozím umístění (`%ProgramW6432%\dotnet`).

Jinak, i při chybě serveru, spustí starý shell. Přepínač tedy potřebuje obojí: `EDISON.exe`
s přepínačem a `ALVA_SETUP`, jehož `LoadSettings` řádek vrací. Chybí-li kterékoli, uzel nemá
účinek. Uzel leží mimo `^EDISON("System","Update")`, protože ten maže každé uložení legacy
Nastavení. Zapnutí i vypnutí (`k` uzlu) platí od příštího startu přes zástupce, běžící shelly
dojedou. Platí pro celou instalaci, tedy pro všechny firmy na serveru. Jedna stanice se ke starému
shellu vrátí zástupcem na `EDISONMAIN.EXE reason=2` (obchází brány EDLOADERu). Na sdíleném dev
serveru hodnota 1 přepne každou stanici s novým EDLOADERem.
