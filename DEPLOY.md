# Vydání shellu EDISONMAIN2 - co distribuovat

Jen to, co existuje nebo se mění kvůli novému shellu. Zbytek (MLRest.dll, EDLIBRARY.dll, USERS.exe,
C1 knihovny...) chodí k zákazníkům se starým shellem už dnes.

Z repa EDISON2 (publish `SingleFileCEdison`):

- `EDISONMAIN2.exe`
- `EDControls.dll` (volná vedle exe, sdílená s `TemplateDesigner.exe` - při změně jejího API vydat i ten)
- `TemplateDesigner.exe` (od F17 přebírá shell session z launch args - starší build by session nepřevzal)
- `EDUPDATE2.exe` (klientský updater nového shellu; bez něj ve zdrojovém adresáři shell
  aktualizaci nespustí - CopyUpdater selže s popupem)

Z repa EDISON (build do `C:\Edison`):

- `EDISONMAIN2API.exe`
- `EDISONMAIN2API.exe.config`
- `EDISONMAIN.exe`
- `EDUPDATE.EXE` (projekt `UPDATERCL`) - zůstává jen pro starý shell; nový shell od
  přechodu na EDUPDATE2 jeho úpravy (`WaitProcess`/`RunExe`) nepotřebuje

Ops prerekvizita na Cloud2: `FILES_DESCRIPTION.INF` musí dostat FULL řádky pro
`EDISONMAIN2.exe`, `EDControls.dll`, `TemplateDesigner.exe` a `EDUPDATE2.exe`
(edituje se ručně; dnes tam žádný z nich není).

Legacy verze musí být aspoň ta, která má úpravy agend pro shell (`EDLIBRARY` shortcut sink,
`MHD2LINKY`/`TISKJR`/`ISYJR2` launch TemplateDesigneru přes shell session, `MHD2PlanDopravy` API pro
miniaplikaci Dispečink, MZDY2 dual-write `^MZR("FiltrZam2")`) - `git log --grep=EDISONMAIN2` v repu EDISON.

Připojení shell čte z `EDISONini.xml` serverové instalace (na stanice ho roznáší EDUPDATE);
dev kopii z repa publish nevydává (`CopyToPublishDirectory="Never"` v csproj).
