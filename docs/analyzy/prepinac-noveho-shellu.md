# Přepínač „nový shell" per zákazník: dvě varianty

Stav k 2026-09-22. Zdroje: repo EDISON (EDLOADER, EDISONINS/CACHE/ALVA/SETUP, EDISONMAIN), repo
EDISON2 (`docs/deploy.md`, `EDISONMAIN2/dotnet/Startup`). Rozhodnutí odloženo; tenhle dokument drží fakta
a oba návrhy, aby se nemusely znovu dohledávat.

## Proč přepínač

- Soubory nového shellu jdou k zákazníkům **per verze**: co je ve `FILES_DESCRIPTION.INF`, dostane každý
  zákazník na té verzi; EDUPDATE2 kopíruje jen soubory z `EDISONCRC.bin`. Pilot jednomu zákazníkovi
  tedy nejde udělat „jen jemu soubory", aktivace musí být řízená daty na jeho serveru.
- Zástupce na ploše míří na `EDISON.exe` = EDLOADER. Ten před startem dělá brány (zákaz loginu,
  `pending` instalace, výměna core souborů) a natvrdo spustí `EDISONMAIN.EXE reason=2`
  (`EDLOADER/clsStartup.vb:17` konstanta `PROCESS_EDISON`, `:324-344` `StartEdison`, `:514` volání).
- Server starý a nový shell nerozliší (`sys("C","App")` je konstanta MLRest.dll, `formId` nový shell
  neposílá). Žádný příznak „smí nový shell" dnes neexistuje.
- EDLOADER (`EDISON.exe`) je core file vyměňovaný updaterem (`EDISONAPI/clsUpdater.vb:2376-2386`),
  změna EDLOADERu jde jen FULL verzí všem. Obě varianty ho mění; liší se tím, odkud čte rozhodnutí.
- Koexistence je bezpečná: ACTIVITY zámek má týž klíč, licence se počítá per IP stanice
  (starý + nový shell na jednom PC = jedno místo), `maSeUkoncit` čtou oba.

## Varianta A: příznak v `EDISONini.xml` (návrh kolegy)

Nový uzel, např. `<newShell>1</newShell>`, v souboru serverové instalace, který EDUPDATE/EDUPDATE2
roznáší na stanice jako key file.

Pro:

- EDLOADER soubor už čte před kontaktem se serverem (`initialized`, `connection`, `forceChange`,
  `securedMode`; `clsStartup.vb:71-85,188-204`). Nový uzel = pár řádků VB, žádná změna v Caché.
- Sémanticky patří k `forceChange`/`securedMode`: politika instalace.
- Všichni čtenáři neznámé uzly tolerují: EDLOADER a starý shell čtou konkrétní uzly, nový shell
  (`SettingsLoader.Read`) taky a při zápisu cizí uzly zachovává, EDISONINS z něj čte jen `restport`
  a při FULL instalaci ho obnoví ze zálohy (`EDISONAPI/clsUpdater.vb:3135-3138`).

Proti (nutné ošetřit):

- **Šíří se až s updatem.** Stanice čtou lokální kopii; serverový soubor se na ně dostane s další
  verzí nebo vynucenou vlnou (`NucenyUpdateLocalClientu`, kopíruje vše). Nouzové vypnutí = editace
  serverového souboru + vynucená vlna. Když nový shell nenastartuje, uživatel se k „Spustit starý
  shell" v menu nedostane, vlna je jediná cesta. Výjimka: stanice běžící přímo ze share.
- **Legacy setup soubor přepisuje od nuly.** `frmNoMDI.SaveSettingsToFile` (`:1947-1984`) staví nový
  dokument jen z `initialized`, `forceChange`, `IRIS`, `connection`, `database`; průchod legacy
  nastavením připojení příznak smaže. Oprava v jednom místě (načíst existující, cizí uzly zachovat).
- Bez UI; edituje se ručně na serveru.

## Varianta B: příznak v Caché (`^EDISON("System","Update",13)`)

EDLOADER se na hodnotu ptá při každém startu přes `ALVA.SETUP.Main.LoadSettings` (na `pending` se
dnes ptát chce, viz past níž), změna platí od příštího startu na kterékoli stanici.

Pro:

- Zapnutí i nouzové vypnutí v řádu minut bez update vlny: podpora nastaví hodnotu, uživatel klikne
  na zástupce znovu. Kdo běží dál, dojede nebo ho admin odhlásí remote logoutem.
- Legacy setup ho nesmaže; přirozené místo pro checkbox v legacy Nastavení (záložka update už
  `SetSettings` volá) i pro admin položku v Nástrojích nového shellu.
- Chybějící řádek = starý shell (bezpečný default pro starší servery).

Proti:

- Změna v Caché projektu `ALVA_SETUP` (jde s vydáním tak či tak) a zápisový endpoint navíc.
- Závisí na dostupném serveru; EDLOADER ale server pro brány volá už dnes.
- Sdílený dev server: hodnota 1 přepne EDLOADER všem kolegům, po testech vracet na 0.

Návrh implementace:

- Slot `^EDISON("System","Update",13)=$lb(newShell)`; volné jsou 13-99 a 103+ (12/100/101/102 jsou
  odvozené v `EDISONINS/CACHE/ALVA/SETUP/API.cls:44-47,65-72,126-130`).
- `Main.LoadSettings` (`Main.cls:7-72`): `if $d(settings(13)) { …"newShell"_del_+$lg($g(settings(13)),1) }`;
  delimiter je `sys("U","delimiter")` = `$c(0)`, ne `~`. Řádek nesmí záviset na `userID`/`IdFa`
  (EDLOADER volá před loginem s holým MLRest; `API.cls:35` bere `systemDB` z `GetSystemDB()`="MLINE").
- `Main.SaveSettings` (`:75-142`): `if $p(param,del,1)="newShell" s settings(13)=$lb(+$p(param,del,2))`.
- `API.SetSettings` (`:104-156`): před `if clear k ^[systemDB]EDISON("System","Update")` zachovat slot
  13 jako `"NucenyUpdateLocalClientu"` (`:110`): `if '$d(settings(13)) s settings(13)=$g(^…,13)`.
  Bez toho ho smaže každé uložení Nastavení: OK maže globál dvakrát s `clear=1`
  (`ALVA.EDISON.Main.SaveSettings` zná jen sloty 0/3-8/11, pak `Update.Save(…,True,True)`).
- EDLOADER: `PROCESS_EDISON2="EDISONMAIN2"`, nová `NewShellIsEnabled(connection)` (vlastní volání
  `LoadSettings`, chyba → False), `StartEdison(reason, useNewShell)`: spustí `EDISONMAIN2.exe` bez
  argumentů jen když existují `EDISONMAIN2.exe`, `EDControls.dll` a adresář
  `%ProgramFiles%\dotnet\shared\Microsoft.WindowsDesktop.App\10.*`, jinak starý shell. Jen větev
  `RunEdison` (`:514`), `RunSetup` beze změny.
- Legacy Nastavení: `chkUpdateNewShell` v `grpUpdateSource`, viditelný jen
  `MLProps.UserIsAdmin AndAlso MLRest.LoginName = "mline"` (vzor `formEDISONMAINSettingsShow.vb:400`);
  `clsUpdate.Update` pole `NewShell`, `Case "newShell"` v `Load`, řádek v `Save`; ve formu načíst přes
  `Update.Load` (ať se nesahá do `ALVA_EDISON`), při uložení `If chk.Visible Then update.NewShell = chk.Checked`
  (neviditelný checkbox nesmí zapsat 0).
- Nový shell: `UpdateSettings.NewShell`, `Updater.SetNewShell` (`SaveSettings` s řádkem `newShell`,
  bez `clear`), admin položka v Nástrojích jen pro `IsAdmin && LoginName=="mline"`, stav načíst při
  otevření menu, přepnutí s potvrzením. Testy v `UpdaterTests` a VM testech.
- Zapnutí u pilota před UI: `s ^|"MLINE"|EDISON("System","Update",13)=$lb(1)` na serveru zákazníka.
- Export `CACHE/ALVA_SETUP.xml` je o commit `3166b1db9d` pozadu; regenerace přibalí i tu změnu.

## Hybrid (volitelně později)

EDLOADER může číst obojí: XML jako výchozí stav instalace, Caché řádek ho přebije. Smysl to má až
při více zákaznících; pro pilot jedné firmy stačí jedna varianta.

## Srovnání

| | A: `EDISONini.xml` | B: Caché |
| --- | --- | --- |
| Granularita | instalace | instalace |
| Pracnost | EDLOADER + oprava `SaveSettingsToFile` | EDLOADER + `ALVA_SETUP` + UI (legacy checkbox, nový shell) |
| Zapnutí | editace serverového souboru + update/vlna | hodnota v Caché, příští start |
| Nouzové vypnutí | editace + vynucená vlna (minuty až déle, plná kopie) | hodnota v Caché, příští start |
| Přežije legacy setup | jen po opravě | ano |
| UI | žádné | přirozené v obou shellech |
| Závislost na serveru | ne | ano (EDLOADER ho volá už dnes) |

## Pasti nalezené při analýze

- `UpdateIsRunning` v EDLOADERu (`clsStartup.vb:211-235`) je mrtvý kód: na `:213` je
  `If Not server.RestStatus.State = Error Then Return False`, čerstvý MLRest má `State=None`, takže
  se vrací dřív, než se server zeptá. Kontrola `pending` tedy roky neběží. Oprava je jednořádková,
  ale mění chování všem zákazníkům (začne blokovat start během serverové instalace); samostatné
  rozhodnutí. Nový shell má vlastní kontrolu (`PassesPreLoginGates`).
- `LoadSettings` má šest nezávislých parserů (EDLOADER, EDTRAY, EDISONINS, EDISONAPI, EDISONMAIN,
  EDISON2), všechny bez default větve: nový řádek je bezpečný, existující klíče a počty polí neměnit.
- Z nového shellu dnes není cesta zpět bez UI (varianta B ji přidává do Nástrojů); starý shell zůstává
  dostupný přes Nástroje → „Spustit starý shell".
- Podpora přihlášená jako `_mline`: starý shell ji do licence nepočítá (`EDUsers.vb:287`), nový ano.
