# Vydání shellu EDISONMAIN2 - co distribuovat

Jen to, co existuje nebo se mění kvůli novému shellu. Zbytek (MLRest.dll, EDLIBRARY.dll, USERS.exe,
C1 knihovny...) chodí k zákazníkům se starým shellem už dnes.

Z repa EDISON2 (publish `SingleFileCEdison`):

- `EDISONMAIN2.exe`
- `EDControls.dll` (volná vedle exe, sdílená s `TemplateDesigner.exe` - při změně jejího API vydat i ten)

Z repa EDISON (build do `C:\Edison`):

- `EDISONMAIN2API.exe`
- `EDISONMAIN2API.exe.config`
- `EDISONMAIN.exe`
- `EDUPDATE.EXE` (projekt `UPDATERCL`; parametry `WaitProcess`/`RunExe` pro restart shellu po update)

Legacy verze musí být aspoň ta, která má úpravy agend pro shell (`EDLIBRARY` shortcut sink,
`MHD2LINKY`/`TISKJR` launch TemplateDesigneru přes shell session, `MHD2PlanDopravy` API pro
miniaplikaci Dispečink, MZDY2 dual-write `^MZR("FiltrZam2")`) - `git log --grep=EDISONMAIN2` v repu EDISON.

Nedistribuovat: `appsettings.json` z publish výstupu (dev server `192.168.16.1`; u zákazníka ho
vytváří setup wizard).
