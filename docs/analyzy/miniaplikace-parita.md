# Miniaplikace: parita se starým shellem, levná zlepšení a statistika údržby

Stav k 2026-10-22, odkazy ověřené proti `main` 8f2f2e73 v kódu, ne za běhu. Zdroje: repo EDISON2,
legacy repo EDISON (starý shell `EDISONMAIN`, agendy, Caché moduly) a data na DEV `192.168.16.1:57770`.
Odkazy `EDISON/...` míří do legacy repa, ostatní do EDISON2. Položky jsou zadání pro `/streams`.

## Kontext

Miniaplikace v EDISONMAIN2 (Plocha, Přehledy, Dispečink, Automatická obnova) vznikaly postupně
a proti starému shellu v několika věcech zaostávají. Nový shell běží u zákazníků od 2026-10-12,
takže chybějící parita bolí uživatele, kteří přecházejí. Tiket DPKV (statistika údržby vozidel)
se vyřeší miniaplikací v novém shellu.

Rozhodnuto 2026-10-16: Caché metody statistiky údržby (E1) napíše Miroslav Mráz, EDISON2 část
Martin Hubata. Oprava spouštění sestav (D3) se dělá, i když mění tvar uloženého zástupce.

Velikost: **S** do půl dne, **M** 1-2 dny. Větší věci jsou v Nápadech.

## Zásady pro všechny položky

- **Paměť nese komponenta, ne miniaplikace.** EDList si pamatuje řazení, šířky a pořadí sloupců
  (`EDControls/dotnet/EDList/ColumnLayout.cs`, scope `EDControls.EDList`, key = `HostId`, per
  uživatel × firma v Caché). EDFilter si pamatuje hodnoty filtrů (scope `EDControls.EDFilter`).
  Miniaplikace dělá tabulky přes EDList a filtry přes EDFilter se stabilním `HostId` tvaru
  `EDISONMAIN2.MiniApp.<Id>.<část>`. Vlastní DataGrid ani vlastní filtry ne. Stav mimo tabulky
  a filtry (interval obnovy) si miniaplikace uloží sama přes `IUserPrefsService`.
- **Kompatibilita u zákazníků:** položka, která mění klíč nebo tvar uložených dat, převezme staré
  hodnoty nebo na ně spadne. U položky je to uvedené.
- **Kontrakt EDControls** (`EDControls.dll` se vydává samostatně, TemplateDesigner běží u zákazníků):
  - nové členy `MiniAppDescriptor` jako `init` vlastnosti mimo pozicový seznam. Konstruktor
    a `Deconstruct` jsou ve vydaném API (`EDControls/dotnet/PublicAPI.Shipped.txt:739,743`).
  - žádný nový člen ve vydaném rozhraní (`IMiniAppProvider`, `IRefreshableMiniApp`): starý exe
    by s novou DLL spadl. Nová schopnost = nové rozhraní.
  - nové členy do `PublicAPI.Unshipped.txt`, nic neodebírat.
- Texty do `EDControls/dotnet/EDStrings/EDStrings.xaml`, barvy přes `EDTheme` (`DynamicResource`),
  ikony do `EDIcons`.
- Test na nejlevnější úrovni podle root `CLAUDE.md`. Dokumentaci (`EDISONMAIN2/dotnet/MiniApps/CLAUDE.md`,
  `EDISONMAIN2/docs/zastupci-na-plose.md`) měnit v témže commitu, řádky jsou u položek.

## A. Framework miniaplikací

**A1 Práva a licence miniaplikací** (M, parita)
Starý shell skryje panel bez balíku 15 a jednotlivé typy podle práv (`EDISON/EDISONMAIN/frmMain.vb:977-981`,
`2153-2191`). Typy 52001-52999 dostane uživatel podle elementu 15 v licenci (`EDISON/EDUsers/EDUsers/EDUsers.vb:592-600`).
Nový nabízí všechno a nic nekontroluje. `EDISONMAIN2/dotnet/Modules/LegacyDerivedAccess.cs` pravidlo
15 → 52001+ nemá. Změna: `init` vlastnost s klíčem práva v deskriptoru, filtr v katalogu a ve výběrovém
okně, odvození 52001-52999 z licence. Miniaplikace v uloženém layoutu, na kterou uživatel právo nemá,
se neotevře, layout zůstane. Test: unit (katalog, obnova layoutu, odvození práva).

**A2 Jedna instance** (S, parita)
Starý shell měl každý typ jednou. Nový dovolí víc: přehledy pak sdílejí uložené řazení a dvě
Automatické obnovy jsou dva časovače. `init` vlastnost `AllowMultiple` (výchozí false), výběrové
okno otevřenou jen aktivuje, uložený layout se dvěma instancemi otevře jednu.
Doc: komentář v `EDControls/dotnet/MiniApps/MiniAppDescriptor.cs:9` („vícekrát"). Test: unit.

**A3 Průběžné ukládání layoutu docku** (S)
Dnes jen při zavření shellu a výměně session (`EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs:923`
→ `MiniAppSessionController.Persist`), po pádu se přidané miniaplikace ztratí. Uložit s odstupem
po změně instancí a rozložení, klíče beze změny. Doc: `EDISONMAIN2/dotnet/MiniApps/CLAUDE.md:49`. Test: unit.

**A4 Obnovit vše** (S, parita)
Starý panel měl „Aktualizovat" pro všechny dlaždice s přepočtem (`EDISON/EDISONMAIN/frmMain.vb:2264-2273`,
`LoadData(True)`, s dotazem). Položka v menu Miniaplikace + zkratka, přepočítá
(`EDISONMAIN2/dotnet/MiniApps/MiniAppManager.cs:22` `RefreshAll`), bez dotazu. Test: unit.

**A5 Ctrl+F do hledání tabulky s fokusem** (S)
Shell při Ctrl+F vždy skočí do hledání ve stromu (`EDISONMAIN2/dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs:342-346`,
`PreviewKeyDown`), tabulky přehledů přitom Ctrl+F nabízejí. S fokusem v docku nechat Ctrl+F komponentě
(`EDComponentToolbar` vybere hledání komponenty s fokusem). Test: unit pomocné logiky.

**A6 Kopírování a export z tabulky** (M, parita s tiskem dlaždice)
Starý shell měl na dlaždici Tisk a menu tabulky (`EDISON/EDISONMAIN/EDMiniApplication.vb:183`). EDList nemá
kontextové menu ani export. Do EDListu „Kopírovat" a „Export do CSV". Přínos i agendám.
Test: unit (formát CSV).

**A7 Ikony záložek a položek nabídky** (S, volitelné)
`EDControls/dotnet/MiniApps/EDMiniAppViewModel.cs:23` vrací `Icon => null`. Ikona podle modulu
(`init` vlastnost v deskriptoru, PNG v `EDIcons`).

## B. Výběrové okno miniaplikací

Dnes 420×540, plochý ListBox s dvouřádkovými položkami (vidět asi 7 ze 17), skupina „Přehledy"
napevno u 15 přehledů (`EDISONMAIN2.LegacyMiniApps/dotnet/LegacyDashboardMiniAppProvider.cs:21`), druhý
ListBox na umístění, bez hledání, jedna miniaplikace najednou, potvrzení Ctrl+Enter.

**B1 Metadata pro nabídku** (S)
`init` vlastnosti deskriptoru: skupina = modul (Mzdy, Vozidla, Fakturace, Sklady MTZ, Datový sklad,
Autobusová doprava, MHD, Nástroje) a popis (jedna věta). `IMiniAppProvider.GroupTitle` zůstane jako
výchozí skupina.

**B2 Okno nad EDListem** (M, závisí na B1, A2)
- EDList se sloupci Název, Modul, Popis a značkou „otevřeno". Hledání bez diakritiky v pruhu
  EDListu, řazení a šířky se pamatují (`HostId`).
- Zaškrtnutí více miniaplikací (`SelectionMode=Check`), jako starý `EDISON/EDISONMAIN/frmMiniAppList.vb:229-342`.
  Vybrané se otevřou do zvoleného místa jako záložky. Mění návratový typ
  `EDISONMAIN2/dotnet/Windows/EdisonMainWindow/ShellDialogs.cs:24,43`, obsluhu v `EdisonMainViewModel.cs:121-125`
  i testovací náhrady (`EDISONMAIN2.Tests/EdisonMainViewModelDialogTests.cs:32`, `EdisonMainViewModelStartTests.cs:29`).
- Umístění jako jeden ComboBox pod seznamem.
- Větší výchozí okno, pamatuje se jen velikost, pozice se dál centruje na hlavní okno (vědomé
  rozhodnutí `MiniAppPickerViewModel.cs:91`, `PersistedFormId => null`). Enter potvrdí, Esc zavře.
- Doc: `EDISONMAIN2/dotnet/MiniApps/CLAUDE.md:51-52`. Test: `MiniAppPickerViewModelTests` (hledání,
  více výběr, otevřené), okno `[WindowFact]`.

## C. Přehledy, Dispečink, Automatická obnova

**C1 Dispečink na standardní komponenty** (M, závisí na E2)
Vlastní DataGrid bez paměti řazení, filtry bez paměti, po otevření se načtou jen pracoviště, chyba
vyskočí popupem (s Automatickou obnovou po 10 s pořád). Změna: EDList (`HostId`), EDFilter
s pracovištěm (pamatuje se) a datem (pole z E2, výchozí dnes), jízdy hned po pracovištích, stav
načítání, prázdný stav a chyba v `EDInfoBar` jako u přehledů, jeden titulek.
Soubory `EDISONMAIN2.LegacyMiniApps/dotnet/Dispecink*`. Test: `DispecinkMiniAppViewModelTests`.

**C2 Přehledy si při obnově nechají hledání a scroll** (S)
Obnova dělá `Sections.Clear()` a staví EDListy znovu (`EDISONMAIN2.LegacyMiniApps/dotnet/LegacyDashboardMiniAppViewModel.cs:71,77`).
Aktualizovat data ve stávajících. Test: `LegacyDashboardMiniAppViewModelTests`.

**C4 Přehledy bez dat z nabídky** (S)
52007 Kalibrace měřidel a 52009 Evidence reklam mají serverovou větev zakomentovanou
(`EDISON/VOZIDLA2/CACHE/ALVA/VOZIDLA/MiniAplikace.cls:132-222`). Vyřadit z nabídky
(`LegacyDashboardMiniAppProvider.cs:32,34`). Test: `LegacyDashboardMiniAppProviderTests`.

**C5 Automatická obnova** (S, závisí na A2)
- Pamatovat zapnutí a interval, jedna instance (A2).
- Tik má přehledy jen znovu načíst. Dnes každý tik každého klienta přepočítá požadované typy
  přehledů na serveru: `EDISONMAIN2/dotnet/MiniApps/PeriodicRefreshMiniAppViewModel.cs:87` →
  `MiniAppManager.RefreshAll` (`MiniAppManager.cs:22`, předává ho přes `IMiniAppRefreshController` na `:128`)
  → `IRefreshableMiniApp.Refresh()` → `LegacyDashboardMiniAppViewModel.cs:61` → `LoadAsync(refresh: true)`
  (`:32`) → `EDISON/EDISONMAIN/CACHE/ALVA/EDISON/MiniAplikaceAPI.cls` `PrepareData`. Starý shell
  přepočítával jen na ruční tlačítko a v serverové úloze.
- Řešení podle kontraktu: nové rozhraní (např. `IReloadableMiniApp`) a `MiniAppManager.ReloadAll`,
  přehledy ho implementují načtením s refresh=0. `IRefreshableMiniApp` se nemění, „Načíst" a A4
  přepočítávají dál.
- Doc: `EDISONMAIN2/dotnet/MiniApps/CLAUDE.md:29-33`. Test: `PeriodicRefreshMiniAppViewModelTests`,
  `MiniAppManagerTests`, `LegacyDashboardMiniAppViewModelTests`.

## D. Plocha

**D1 Převzetí staré plochy** (S-M, přednost)
Přecházející uživatelé dnes plochu ze starého shellu nevidí. První implementace (2026-10-09) byla
zahozená, udělat znovu proti `main`, kde shell posílá jednotlivé změny
(`EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/FavoritesCoordinator.cs:137-154`). Ověřený postup:
- serverová třída v balíku EDISONMAIN2 čte parametry `desktop_shortcut_N` z číselníku LIBRARY/Form,
  formulář 2001, cesta `2001~state~<userID>` (`ISML.SYS.MLFirma.API.Ciselnik.Load`, zapisuje je
  `EDISON/EDISONMAIN/Rozhrani (mainform)/frmNoMDI.vb:2353-2456`).
- jednou per uživatel × IdFa, hlídá značka v `UserData` (ne chybějící blob, testeři ho po ★ mají),
  sloučení podle `DesktopShortcuts.KeyOf`, existující klíče přeskočit.
- jen moduly hlavní nabídky (ItemType 0, starý formát s oddělovačem „^" a typy 0-3), Id = ElementId,
  `ElementId` jako JSON string (číslo by blob na klientovi shodilo).
- pozice nepřevádět. Pořadí čtení: řádek tvoří ikony do půl rozestupu (75 px) pod první ikonou řádku,
  pak X.
- volat z `EDISONMAIN2/dotnet/Modules/SessionDataLoader.cs` před načtením plochy, chyba jen do logu
  (telemetrie by se ozvala i z unit testů VM).

Test: Caché (formáty, pořadí, sloučení, značka), unit (`SessionDataLoaderTests`), integrační
(zápis přes `ALVA.EDISON.Main.SaveUserSettings` pod TEST_EDISON2_IT/999).

**D2 Práva a licence u sestav, pohledů DS a forem balíku** (M)
Starý shell je při načtení plochy kontroluje podle balíku hostitelské formy (licence balíku, aspoň
jedno právo do balíku, `EDISON/EDISONMAIN/frmMain.vb:628-676`, `1442-1494`). Nový ve stromu dohledává jen
moduly hlavní nabídky (`EDISONMAIN2/dotnet/Desktop/ModuleShortcutsService.cs:119-133`), ostatní druhy zobrazí
i spustí bez kontroly. Kontrolovat stejně, nepovolené skrýt a nechat v datech (starý shell je mazal,
to nepřebírat). Test: `ModuleShortcutsServiceTests`, `FavoritesCoordinatorTests`.

**D3 Způsob spuštění sestav (CallType)** (M, dvě repa)
Sestavy 7 agend (ADSERVIS, ADDISPECING, ADZPRACOVANI, MZDY, Tickets, Přepravní pokladna, ADPLAN) se
z plochy otevřou jako formulář místo tisku (např. `EDISON/ADSERVIS/Struktury.vb:57`). Shell odvozuje
CallType z druhu (`EDISONMAIN2/dotnet/Desktop/ShortcutLaunchPayload.cs:56`).
- **Mění tvar uloženého zástupce:** nové volitelné pole `CallType` v `DesktopShortcutDto`
  a `DesktopShortcut`. Chybí-li, platí dnešní odvození, staré zástupce fungují dál. Caché `Add`
  a `Place` neznámá pole zachovávají (`EDControls/CACHE/ALVA/EDControls/DesktopShortcuts.cls:23-118`).
- Repo EDISON: `EDISON/EDISONMAIN2API/CacheShortcutSink.cs` CallType pošle.

Test: `ShortcutLaunchPayloadTests`, integrační JSON sinku.

**D4 Výběrové sestavy HIM** (S, repo EDISON, nejdřív ověřit za běhu)
Zástupce nese `HIM.exe`, formulář je z ESREPORT, host hledá typ jen v exe agendy
(`EDISON/EDISONMAIN2API/FormToRunGetter.cs:84`, `HIM.vbproj` na ESREPORT neodkazuje). Oprava v sinku:
exe podle balíku formy.

D5 vyřazena: shell posílá jednotlivé změny od `adaa8329` (2026-10-12).

**D6 Zástupce z agendy hned** (S)
Blob se přenačte jen po skončení agendy otevřené jako okno (`EDControls/dotnet/EDViewModel/LegacyModuleHost.cs:184-188`
`TrackModeless`). Spuštění ze zástupce (`:113`), s čekáním (`:121`) a jako dialog (`:124`) ho
nevyvolají. Přenačíst i při aktivaci hlavního okna, s odstupem a `Apply` jen při rozdílu proti `_sent`
(každé `Apply` přestaví záložku Oblíbené a ztratí její rozbalení). Test: `FavoritesCoordinatorTests`.

**D7 Ikona podle druhu zástupce** (S)
Starý shell má 5 ikon (`EDISON/EDISONMAIN/frmMain.designer.vb:161-167`), dlaždice ikonu nemá.

**D8 Klávesnice a čtečky** (M)
Fokus na dlaždicích, Enter spustí, Delete odebere, šipky, `AutomationProperties.Name`. Starý shell
měl Enter a Delete. Test: unit příkazů (`DesktopMiniAppViewModelTests`).

**D9 Seřadit dlaždice** (S)
Kontextové menu prázdné plochy „Seřadit": dlaždice po řádcích bez mezer (starý „Zarovnat ikony").
Test: `ModuleShortcutsServiceTests`.

**D10 Doc plochy** (S)
`EDISONMAIN2/docs/zastupci-na-plose.md` ř. 14 (forma balíku bývá i procedura) a ř. 120 (strom filtruje
klient, ne server).

**D11 Licence starší než 1.003** (S, nejdřív zjistit, jestli takové licence existují)
Strom čte prvky licence jako povolené (`EDISONMAIN2/dotnet/Windows/EdisonMainWindow/Modules/ModuleTreeState.cs:39`),
starý shell u verze < 1.003 jako zakázané (`EDISON/EDISONMAIN/frmMain.vb:427-448`). Týká se celého stromu.

## E. Statistika údržby (tiket DPKV)

Data v Caché jsou, metoda s počty neexistuje (`EDISON/VOZIDLA2/CACHE`: vozidla `^DV2`, požadavky
`^DVUR`/`^DVURi`, plány `^DVU("PlanNew")`, vazba požadavek → plán `PlanNewIdxNO`). Vzor miniaplikace je
Dispečink (`EDISON/MHD2PlanDopravy/CACHE/ALVA/MHD2/PlanDopravy/API/DispecinkMiniApp.cls` a
`EDISONMAIN2.LegacyMiniApps/dotnet/DispecinkMiniAppViewModel.cs`).

**E0 Otázky na zákazníka** (před E1, přes Pavla Marka do tiketu)
1. KV_DP má „Částečně opraveno, oprava pokračuje" jako jednu položku 7, zadání je vede zvlášť.
2. Stavy 3 Bez opravy a 6 storno zadání nezmiňuje: počítat, nebo vynechat?
3. Požadavek ve více plánech: který stav plánu se počítá, stav hlavičky, nebo položky plánu?
4. Filtr pracoviště u nezaplánovaných požadavků (nemají plán, vyjde vždy 0).
5. „Dnes uzavřené": datum Dokončeno, nebo datum změny stavu v historii plánu?
6. „Typ vozidla" ve filtru: id typu, druh typu, nebo kód druhu (věta A 5)?
7. Počítat kontroly bez zjištěné závady (starý přehled 52010 je vynechává)?
8. Středisko: dnešní středisko vozidla, nebo k datu požadavku?
9. „Dnes k řešení": plány s datem přistavení dnes ve stavu Zadáno nebo Objednáno, nebo všechny
   plány v těchto stavech bez ohledu na datum?

**E1 Caché metody ve VOZIDLA2** (Miroslav Mráz, repo EDISON)
Kontrakt podle vzoru `DispecinkMiniApp.cls` (`$$$Init`/`$$$FillParams`/`$$$CatchErrRest`):
- `GetFiltry`: střediska, pracoviště, typy vozidel jako indexované řádky `$lb(kód, název)` pod
  `^||ML("OUT", "stredisko"|"pracoviste"|"typ", $i(i))`.
- `GetStatistika`: vstup `^||ML("IN","stredisko")`, `"pracoviste"` (více hodnot jako `$lb`), `"od"`,
  `"do"` (prázdné = celá data), `"typ"`. Výstup tři tabulky `^||ML("OUT","t1"|"t2"|"t3", $i(i)) =
  $lb(kód, text, počet)`. Tabulka 3 filtry ignoruje.
- `MaintenanceRequestList` bez limitu vrátí jen 1000 záznamů. Caché test a měření na objemu dat DPKV.
- Kontrakt má jít použít i z EDISON WEBu (tiket ho zmiňuje).

**E2 Filtr: více hodnot a období** (M, EDControls)
EDFilter má jen `EDFilterCombo` (jedna hodnota). Přidat pole s výběrem více hodnot z číselníku a pole
období od-do (prázdné = bez omezení), obě s `GetValue`/`RestoreValue` a pamětí přes `HostId`. Dořešit
vložení EDFilteru do `EDMiniAppBar`: EDFilter má vlastní pruh a tlačítko obnovy
(`EDControls/dotnet/EDFilter/CLAUDE.md:4,48-50`), `EDMiniAppBar` vlastní kolekci filtrů. V miniaplikaci
platí tlačítko „Načíst" pruhu miniaplikace, cílem filtru (`IEDFilterTarget`) je VM miniaplikace.
Doc: `EDFilter/CLAUDE.md`. Test: `EDFilterTests`.

**E3 Miniaplikace „Statistika údržby"** (M, závisí na E1, E2, volitelně A1)
Tři EDListy (`HostId`), filtry středisko a pracoviště (více), období, typ vozidla, platí pro tabulky
1 a 2. Stavy načítání, chyby a prázdna jako přehledy, „Načíst" a Automatická obnova (C5). Nové soubory
`EDISONMAIN2.LegacyMiniApps/dotnet/UdrzbaStatistika*`, registrace v `LegacyMiniAppProvider.cs` a šablona
v `LegacyMiniAppTemplates.xaml:9-11` (soubory proudu Přehledy). Test: unit VM, integrační wire.

## Rozdíly bez kroku

- Starý panel: „Upravit miniaplikace" (pořadí dlaždic) a „Rozbalit/Zabalit" nahrazuje dock (přetažení
  záložek, změna velikosti). „Odstranit všechny" nahrazuje „Zavřít miniaplikace".
- Potvrzení před obnovou: nové záměrně bez dotazu.
- Starý výběr zavíral odškrtnuté miniaplikace. Nový jen přidává, zavírá se křížkem.
- Výběr řádků podle hodnot sloupce a maskou v tabulce: EDList má hledání, další nástroje nepřebírat
  bez požadavku.
- Plocha: klik místo dvojkliku, mřížka místo pixelů, odebrání z menu místo Delete (D8 Delete doplní),
  tooltip s cestou, text dlaždice ze stromu, Plocha a Oblíbené jako jeden stav.
- Barva firmy na pozadí plochy: přidat jen na přání (zdroj barvy v novém shellu neověřen).
- Nástěnky a varovný pruh starého shellu: nový má pruh podpory a texty o licenci
  (`EDISONMAIN2/dotnet/Windows/EdisonMainWindow/LicenceNotice.cs:22-41`), parita v pořádku.
- Klíč tabulek přehledů podle názvu tabulky: titulek obsahuje datum nebo období, klíč by se měnil denně.

## Návrh proudů pro /streams (repo EDISON2, jeden soubor = jeden vlastník)

| Proud | Položky | Vlastní soubory | Testy | Závisí na |
|---|---|---|---|---|
| EDControls | API části A1, A2, A7, B1, C5 (nové rozhraní), A6, E2, ikony A7/D7 | `EDControls/dotnet/MiniApps/*`, `EDList/*`, `EDFilter/*`, `EDIcons/*`, `EDStrings/*`, `PublicAPI.*.txt`, `EDControls.csproj` | `EDControls.Tests` (`EDListControllerTests`, `EDFilterTests`, nové pro MiniApps) | - |
| Shell miniaplikací | A1, A2, A3, A4, A5, B2, C5 (Automatická obnova, `ReloadAll`), A7 (záložky) | `EDISONMAIN2/dotnet/MiniApps/*`, `MiniAppSessionController.cs`, `EdisonMainWindow.xaml(.cs)`, `EdisonMainViewModel.cs`, `ShellDialogs.cs`, `Windows/MiniAppPickerWindow/*`, `EDISONMAIN2/dotnet/Modules/LegacyDerivedAccess.cs` | `MiniAppManagerTests`, `MiniAppSessionControllerTests`, `MiniAppPickerViewModelTests`, `PeriodicRefreshMiniAppViewModelTests`, `CoreMiniAppsTests`, `EdisonMainViewModelDialogTests`, `EdisonMainViewModelStartTests`, `MiniAppTestDoubles` | EDControls |
| Přehledy | B1 data, A1 klíče práv, A7 ikony providerů, C1, C2, C4, C5 (přehledy) | `EDISONMAIN2.LegacyMiniApps/dotnet/*` kromě `UdrzbaStatistika*` | `LegacyDashboardMiniAppViewModelTests`, `LegacyDashboardMiniAppProviderTests`, `DispecinkMiniAppViewModelTests`, `LegacyMiniAppProviderTests` | EDControls |
| Plocha | D1, D2, D3 (EDISON2), D6, D7, D8, D9, D10, D11 | `EDISONMAIN2/dotnet/Desktop/*`, `Windows/EdisonMainWindow/Modules/*`, `EDISONMAIN2/dotnet/Modules/SessionDataLoader.cs`, `EDISONMAIN2/CACHE/ALVA/EDISONMAIN2/API/*` (nové), `EDISONMAIN2/docs/zastupci-na-plose.md`, `EDISONMAIN2/CLAUDE.md` | `ModuleShortcutsServiceTests`, `FavoritesCoordinatorTests`, `DesktopMiniAppViewModelTests`, `ShortcutLaunchPayloadTests`, `SessionDataLoaderTests`, Caché `ALVA.EDISONMAIN2.Tests`, `EDISON2.IntegrationTests/UserPrefsTests.cs` | EDControls (ikony D7) |
| Údržba | E3 | `EDISONMAIN2.LegacyMiniApps/dotnet/UdrzbaStatistika*` (nové) | nové unit + integrační | E1 (Mráz), EDControls (E2), Přehledy (registrace a šablona) |

Texty do `EDStrings` a ikony posílají ostatní proudu EDControls záznamem `pro`, dokumentaci
`EDISONMAIN2/CLAUDE.md` proudu Plocha. Mimo proudy EDISON2: D3 a D4 část v repu EDISON (Martin Hubata),
E1 (Miroslav Mráz), E0 (Pavel Marek se zákazníkem).

## Ověření

- Každá položka: test ze sloupce Testy, `dotnet test EDISON2.sln`, Caché
  `powershell -File tools/run-cache-tests.ps1 -Package ...`, integrační při `.cls`.
- Ručně po slití Shellu miniaplikací: Miniaplikace → Zobrazit miniaplikaci, hledat „údržba",
  zaškrtnout dva přehledy, Enter, oba se otevřou jako záložky. Ukončit shell přes správce úloh,
  spustit znovu: miniaplikace zůstanou (A3).
- Ručně Dispečink: změnit řazení a pracoviště, restart shellu, obojí zůstane.
- Ručně plocha: uživatel bez práva do balíku nevidí jeho sestavy (D2), sestava ADSERVIS z plochy
  vytiskne (D3), uživatel se starou plochou ji po prvním přihlášení vidí (D1).

## Nápady na později

- Dvojklik na řádek přehledu otevře agendu nebo záznam (server musí posílat modul a id).
- Přidání sestavy přímo z plochy (katalog sestav).
- Počet řádků v titulku záložky přehledu („Požadavky na údržbu (12)").
- Obecná miniaplikace se serverovými filtry (Caché vrátí filtry i tabulky, klient je vykreslí):
  má smysl, až po E3 vznikne druhá podobná.
- Výběr více dlaždic na ploše a hromadné odebrání.
