# Changelog

## 2026-07-03 — T4 malé kousky (R4 fix + rename) + StopList kaskáda testy

- ✅ **R4 fix — `TableEditorDialogViewModel` bez WPF.** Inline WPF picker šablon tabulky (Window + ListBox
  + Buttons stavěný ve ViewModelu) přesunut do code-behind `TableEditorDialog.xaml.cs` přes callback
  `RequestPickTableTemplate` (VM zná jen výsledný klíč). VM je teď 0× `System.Windows` (R4). Behavior-preserving.
- ✅ **Rename `MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`** — soubor teď odpovídá názvu třídy
  (`git mv`, obsah beze změny, SDK globbing).
- ✅ **`StopListCascadeTests` (9 testů)** — regresní pokrytí StopList-specifického cascade wiring: feature
  override aktuální/projeté zastávky (`ResolveRowStyle`), legacy `col.Alignment` fallback jen když kaskáda
  HAlign nenastaví, per-column `Style` priorita, element-level `HeaderStyle`. `ResolveHeaderStyle/DataStyle/
  RowStyle` zpřístupněny `public` (stejný vzor jako DepartureGrid Resolve*). Tím je uzavřen **malý T track**
  (T5a, T6, T4 R4+rename, StopList testy); velké T1b-robustní + plný T4 strangler zůstávají záměrně.

## 2026-07-03 — T5a: agenda kód→třída lookup bez hardcoded mirroru

- ✅ **`AgendyRegistr.GetClassForCode(agendaCode)`** — nový sdílený lookup (auto-discovery z `%Dictionary`,
  stejná cesta jako `ListAgendy`). `EDList/Instance.GetClassForAgenda` dřív hardcoduje mapování kód→FQN
  třídy (mirror Wave 2) → teď deleguje na `GetClassForCode`. Přidání nové agendy = jen nová `BaseAgenda`
  subclass, žádný druhý seznam k údržbě. Ověřeno: `ListAgendy` (stejný mechanismus) vrací správné mapování
  pro AD/MHD/FAKTURY.
- ⏭️ **T5b (`ZastavkoveJrAd.GetMockData` → statický mock) přeskočeno** — živý náhled z reálné linky
  ponechán záměrně (není zásadní; na rozdíl od MHD/Faktury zůstává AD design-time preview z živých dat).

## 2026-07-03 — T6 dokončeno (architektonický track drobnosti)

- ✅ **`architecture.md` doplněn o `/Services` (6 souborů) a `/PdfImport` (12 souborů)** — dřív v mapě
  složek chyběly (~7k LOC neviditelných v doc). Doplněn stručný popis každého souboru.
- ✅ **Per-frame `new SKPaint` v Render() běžných elementů — posouzeno a zavřeno** (nebude se dělat):
  painty jsou disposované (`using var`), per-element (ne per-buňka, jen 1–5/element/repaint), a závislé
  na měnitelných barvách elementu → cache by vyžadovala invalidaci za zanedbatelný zisk. Schedule elementy
  s 100+ buňkami painty pre-createují už dnes. Tím je **T6 celé hotové**.

## 2026-07-03 — StatusText segmentace: „stav výběru" × „akce/progres"

- ✅ **Status bar rozdělen na dvě zóny**, aby se nepřepisovaly. Dřív jeden `StatusText` míchal stav
  výběru (element/sekce), transientní feedback (uloženo/chyba) i progres (export N/N) — akční hláška se
  ztratila hned, jak uživatel klikl jinam. Nově: nová property **`SelectionStatusText`** (klidová zóna,
  píše ji jen `UpdateStatusForSelection`) + stávající **`StatusText`** repurposed jako transientní
  „akce/progres" zóna (~14 zápisů: export, uložení, chyby, načítání dat, migrace, schránka). XAML:
  levý `*` sloupec = výběr (šedý, ellipsis), pravý Auto sloupec = akce (tučný, primární barva),
  overflow blok posunut. Akční hláška zůstává viditelná i po změně výběru.

## 2026-07-03 — Escape ruší probíhající operaci (drag / resize / rubber band)

- ✅ **Escape během tažení / změny velikosti vrátí element na začátek** (`EndDrag`/`EndResize` s
  `commit: false` — už existující restore-z-init cesty). **Escape během rubber bandu** ho zahodí bez
  změny výběru. Bez operace = zruší výběr (beze změny), během přidávání z palety = zruší přidávání
  (beze změny). `OnWindowKeyDown` je na `PreviewKeyDown` okna → Escape dorazí i při mouse-capture dragu.

## 2026-07-03 — Discoverability: skryté featury do context menu

- ✅ **Context menu (pravý klik na element)** dostal položky, které dřív šly jen zkratkou:
  **Seskupit (Ctrl+G)** — jen při 2+ vybraných; **Rozeskupit (Ctrl+Shift+G)** — jen když je výběr
  ve skupině; **Rozdělit → Vodorovně (Ctrl+Shift+H) / Svisle (Ctrl+Shift+J)**; **Upravit text (F2)** —
  jen u TextElementu. Umístění: za „Z-pořadí", před „Vlastnosti elementu…", obklopeno separátory.
  Zviditelňuje featury bez nutnosti znát zkratku.

## 2026-07-03 — Zoom: tlačítko × zkratka konzistentní + přepis tabulky zkratek

- ✅ **Zoom — tlačítko a zkratka se shodují.** Dřív „1:1" tlačítko dělalo reset na 100 %, ale Ctrl+0
  dělal fit-to-page → nekonzistentní. Nově dvě konzistentní dvojice: **„Fit" tlačítko + Ctrl+0** =
  přizpůsobit stránce; **„1:1" tlačítko + Ctrl+1** = skutečná velikost 100 %. `ResetZoomTo100()` sdílený
  helper, `BtnFitPage_Click` → `FitToPage()`. (Dva nezávislé combo-zoomy designer/náhled ponechány —
  jsou to různé pohledy.)
- ✅ **`ux-conventions.md §3` přepsáno 1:1 dle KeyDown handleru** (byl drift). Smazán neexistující
  Ctrl+Shift+S (Uložit jako nemá zkratku); doplněno ~10 chybějících: F2 (inline text), Ctrl+P (tisk),
  Ctrl+Shift+P (vlastnosti stránky), Ctrl+Shift+A (změna agendy), Ctrl+B (levý panel),
  Ctrl+PageUp/PageDown (Z-order), šipky (posun elementu / sekce), Escape, Ctrl+0/Ctrl+1 (zoom).

## 2026-07-03 — Pre-pilot UX P0 dokončeno: U2 (context menu) + U6 (labely tlačítek)

- ✅ **U2 — context menu „Vlastnosti elementu…" otevře dialog.** Dřív jen vybralo element + přepnulo
  inline panel — u stejně pojmenované akce „Vlastnosti sekce…" (dialog) nekonzistentní, a „…" v názvu
  dialog implikuje. Nově volá `OpenPropertiesForElement(el)` (stejná cesta jako dvojklik).
- ✅ **U6 — sjednocení labelů tlačítek.** Cancel byl v TD rozjetý („Zrušit" 5× vs „Storno" 2×). Vše
  převedeno na `{StaticResource EDStrings.Cancel}` = „Storno" (firemní standard — EDStrings + EDFooter);
  „OK"/„Uložit" literály na `EDStrings.Ok`/`EDStrings.Save` (7 dialogů: CacheTemplatePicker, ChangeAgenda,
  InstancePicker, SaveSectionTemplate, SaveToCache, ElementEdit, NormalizePositions). Pravidlo (cancel =
  „Storno" přes EDStrings, primární = `EDStrings.Ok`/akční sloveso) zapsáno do `ux-conventions.md §1`.
- 🏁 **Tím je celé pre-pilot P0 (U1–U7) hotové.**

## 2026-07-03 — Pre-pilot UX P0: U3 (klik na paletu) + U4 (dvojklik do prázdna) + U7 ověřeno

- ✅ **U3 — klik na paletu vkládá element.** Dřív palette tlačítko jen zahájilo drag-to-canvas;
  prostý klik (bez tažení) nic nevložil (drag-only = neobjevitelné). Fallback v `EndAddingElement`:
  když se pustí bez cílové sekce (`_addingTargetSection == null`) a jde o element z palety, vloží se
  do `State.ActiveSection` na výchozí pozici 5×5 mm. Drag-to-canvas beze změny.
- ✅ **U4 — dvojklik do prázdna neotevírá vlastnosti stránky.** Fallback dvojkliku na prázdnou plochu
  (`OpenPropertiesForPage`) nahrazen zrušením výběru — dialog do prázdna byl překvapení. Vlastnosti
  stránky zůstávají na Ctrl+Shift+P / menu. Dvojklik na element / záhlaví sekce beze změny.
- ✅ **U7 ověřeno jako hotové** (audit byl stale): M/O snap toggly už tooltipy mají.

## 2026-07-03 — Pre-pilot UX P0: U1 (bug ukládání) + U5 + async confirm/close

- ✅ **U1 — BUG opraveno:** volba **„Ano = uložit"** v potvrzení neuložených změn (Nový / Otevřít /
  změna agendy / zavření okna) dřív ukládala do **JSON souboru** (`SaveAndContinue` → `SaveFileDialog` +
  `TemplateSerializer.Save`), ne do DB. Z pohledu uživatele ztráta dat — šablona nebyla v DB, kde ji čeká.
  Teď `SaveAndContinueAsync` volá **stejnou DB cestu jako Ctrl+S** (`SaveToCacheAsync`).
- ✅ **U5 — Ctrl+S bez DB** už netiše nepíše do status baru — `OfferJsonExportFallbackAsync` zobrazí
  popup („Databáze není dostupná — uložit jako JSON?") a na potvrzení spustí export.
- ✅ **Celý confirm/close flow async** (uložení do DB je async přes MLRest): `ConfirmUnsavedChanges()` →
  `ConfirmUnsavedChangesAsync()`, `SaveAndContinue()` → `SaveAndContinueAsync()`; `SaveToCacheAsync`/
  `SaveToCacheAsAsync`/`ExportToJsonAsync`/`OfferJsonExportFallbackAsync` vrací `Task<bool>` (true = reálně
  uloženo → volající pokračuje). `NewCommand`/`ChangeAgendaCommand` převedeny na `CommandAsync`;
  `MenuAiAssistant_Click`/`MenuImportPdf_Click` na `async void`.
- ✅ **Fix pádu při zavření okna** (`InvalidOperationException: nelze volat Close při zavírání`):
  nové async `Window_Closing` u čistého (nedirty) okna zavíralo synchronně uvnitř běžícího Closing eventu.
  Fix: nedirty okno → zavřít normálně (žádný cancel); dirty → `e.Cancel=true` + async potvrzení + `Close()`
  odložený přes `Dispatcher.BeginInvoke` (až po doběhnutí eventu).

## 2026-07-03 — Úklid T6: odstranění vestigiálního menu + MessageBox

- ✅ Odstraněno skryté menu **„Nástroje → Vlastnosti textu… (alternativa k dvojkliku)"** (+ handler
  `MenuTestTextEdit_Click`) — redundantní leftover z vývoje editoru; dvojklik i Enter dělají totéž
  (`OpenPropertiesForElement`). Tím zmizel i poslední `MessageBox` v `MainWindow.xaml.cs` (R2).
  `MessageBox` v `App.xaml.cs` (startup config warning + `OnUnhandledException`) ponechány — legitimní
  bootstrap/crash fallbacky (popup infra tam nemusí být dostupná). Dřív (t.d.) odstraněn i
  `ScheduleDetectorTest.cs` z produkční assembly.

## 2026-07-03 — Gold regresní test AD zastávkového JŘ (T1b základ)

- ✅ **`ALVA.TemplateDesigner.Tests.ZastavkoveJrAdGoldTest`** — základní 1:1 regresní pojistka:
  `GetData` na gold linkách **0365001 + 0365005**, digest = hrubé počty (ZASTAVKY/ODJEZDY/POZNAMKY)
  + MD5 celého spojeného výstupu, zmrazený jako baseline. Běží v pre-push Caché sadě (**27/27**).
  Smysl: zachytit, že se kódem nerozbil datový výstup.
- ⚠️ **Záměrně pevně vůči CB_CSADAD datům** (AD je per-firma namespace): drift JŘ dat = falešný pád
  (přegeneruj digest z hlášky „aktuální: …"), jiný server bez CB_CSADAD → test se přeskočí. Robustní
  verze (zmrazit vstupní `^JRL`/`^JRZ`/`^JRSET` do izolovaného namespace, data-drift-proof) odložena —
  viz roadmap T1b.

## 2026-07-03 — Architektonická rozhodnutí: R1 rozhodnuto, R2/R3 upřesněno, stav do roadmapy

- ✅ **Nová roadmap sekce „🧭 Architektonická rozhodnutí R1–R7 — stav a nejbližší akce"** —
  stavová tabulka všech rozhodnutí na jednom místě (plný text zůstává
  v `architektonicka-rozhodnuti-agendy.md`, kam doplněny poznámky z diskuze).
- ✅ **R1 rozhodnuto (diskuze):** zárodek sdíleného řádkového jádra = `DynamicTableElement`;
  extrakce až u LineTable (3 konzumenti ukážou, co je společné — dřívější extrakce nad dvěma
  by abstrakci tvarovala podle StopList domény). Nová akce **R1a**: hodinový kontrolní průchod
  DynamicTable (čisté rozvrstvení model sloupců / měření / render). Ujasněno: jádro ≠
  PageLayoutEngine (vnitřek elementu × sekce/stránky); domov `Rendering/` vedle CellRendereru.
- ✅ **R2 model potvrzen** (= dnešní architektura, nic se nemění); zbývá jen návrh partial-fit
  rozhraní („vykreslil jsem řádky 1–40, zbývá 12, pokračuj odtud") před Fází C+D.
- ✅ **R3 upřesněno:** základ kontraktu už existuje (`cache-api.md` + `01-wire-formats.md`);
  doplnit (a) prezentační nápovědy v1 (sémantické: `ZVYRAZNENI` 0–5, typ řádku,
  opakuj-na-další-straně, přes-celou-šířku; `FORE_COLOR`/`BACK_COLOR` legalizovat jen pro
  legacy paritu) + (b) pravidlo jen-aditivní evoluce kontraktu. Do `cache-api.md` před F2.

## 2026-07-02 — DynamicTableElement: samostatná dynamická tabulka + 2-tab editor

- ✅ **Rozdělení tabulek** — dynamická tabulka (řádky z `DataSource` kolekce) je samostatný
  element `DynamicTableElement`; statická `TableElement` **uspána** (`JsonDerivedType` zůstává
  pro načtení legacy šablon, z palety/menu odstraněna). Ztráta zpětné kompatibility akceptována.
- ✅ **`DynamicTableEditorDialog`** (2-tab, konzistentní se StopList/DepartureGrid): tab Obecné =
  EDInputForm (`ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit`) + tab Sloupce
  (`ColumnListEditor` + `CellStyleEditor`, Add/Remove, mapování `Field`). Dvojklik na element
  otevře dialog rovnou (jako StopList).
- ✅ **Auto-scale do pevné Height** (jako JŘ elementy) + auto-distribuce šířky sloupců (`Width≤0`
  = rozdělí zbývající šířku); `BuildColumnsFromFields` helper; placeholder u prázdné tabulky.
- ✅ **Zdroj dat = EDInputForm List** plněný klientsky (kolekce agendy); seed sloupců
  **centralizovaný v dialogu** (undoable přes příkaz) + seed při otevření. Reapply výběru po
  re-Initu (EDInputForm auto-Init na `Loaded` = re-Init při návratu na tab). **Nemíchat
  EDInputForm + nativní WPF** — transient stav držet stranou (memory / temp global).
- ✅ **`ZastavkoveJrAd.GetSchema` synced 1:1 s reálnými `Build*` daty** — kolekce
  ZASTAVKY/SKUPINY_DNU/ODJEZDY/POZNAMKY doplněny o skutečná skalární pole (PORADI, JE_AKTUALNI,
  ODSAZENI, ZAKLADNI_TRASA, ZKRACENE_OZNACENI_LINKY, KATEGORIE, FORE/BACK_COLOR); phantom
  NADPIS2 odstraněn. (Vnořená pole ZONY/PRUBEHY/ATRIBUTY/… zůstávají doménou StopListu.)

## 2026-07-02 — Architektonická rozhodnutí R1–R7 pro škálování agend (nový doc)

- ✅ **`architektonicka-rozhodnuti-agendy.md`** — plain-text zápis 7 rozhodnutí, která drží
  model „stabilní tiskový engine + postupně přidávané agendy": R1 sdílené tabulkové jádro
  (gate u LineTableElementu), R2 rozdělení layout zodpovědností engine×element, R3 prezentační
  nápovědy v datovém kontraktu + aditivní evoluce, R4 kde běží headless tisk + PrintEngine
  fasáda, R5 fonty embedded v aplikaci (Timetable = sémantika), R6 systémové šablony s release
  (ne DB → žádná verzní matice), R7 pilot-first. Každé: co znamená / jaký problém řeší /
  co se rozhoduje / doporučení / nejpozději kdy. Úvodní slovník „co je engine, co je agenda".
- Roadmap: ⚠️ gate odkazy na R1/R2/R5 (Fáze C+D), R4/R7 (blok A), R6 (blok B), R3 (Fáze F);
  CLAUDE.md reference („číst před startem každé nové fáze").

## 2026-07-02 — T1b (část 1): Caché testy — SablonyTest + Mhd helpery (8 testů, pre-push)

- ✅ **Nový package `ALVA.TemplateDesigner.Tests`** (`CACHE/ALVA/TemplateDesigner/Tests/`,
  extends `ALVA.EDTesting.MLTestCase`) — přidán do default listu `tools/run-cache-tests.ps1`
  → pre-push hook kryje i TD Caché stranu. Celá Caché sada: 23/23.
- ✅ **`SablonyTest`** (5 testů) — CRUD primárního úložiště šablon, vzor „round-trip nad
  vlastními daty": izolovaná IdFa větev `EDTESTING` globálu (žádná závislost na živých
  firemních datech, cleanup v OnBeforeOneTest). Save→Load byte-identický blob + metadata,
  Delete + prázdný List + Load smazané, update zachová CreatedAt + přeindexuje ByAgenda,
  Rename, validace povinných polí (nevalidní Save nic nezapíše).
- ✅ **`ZastavkoveJrMhdHelperyTest`** (3 testy) — deterministický `ConvertSpecialniZnak`
  (dny 1–7, ASCII >31: 72→H/70→F/120→x/123→{, ≤31 beze změny). Mhd třída — mimo AD WIP.
- **Poznatek o kontraktu (zdokumentován v testu):** Sablony metody vrací `$$$OK` i po chybě —
  `$$$CatchErrRest` plní `ret`, který zvedá až MLRest HTTP vrstva; in-process se chyba
  detekuje přes observable stav (chybějící OUT klíče, nezměněný globál).
- **Kompilace:** root hook `compile-objectscript.sh` (Atelier PUT + compile) — v TD session
  volán ručně (hook je vázaný na root project dir). MCP `cache_compile` hledá zdroje pod
  `C:\DEV\EDISON` → pro TD soubory nepoužitelný (pozor: hlásí „up-to-date" i bez uploadu).
- **Část 2 (tentýž den — AD engine je commitnutý, WIP blocker pominul): +17 testů → 25/25
  (celá Caché sada 40/40):**
  - `ZastavkoveJrAdHelperyTest` (12) — čisté helpery se syntetickými vstupy: `ZarazeniSpoje`
    (FTL 0/1/2/3 vč. prázdninového dělení dle NZ konfigurace), `BitmapNaSkupiny`, `CasHodnota` +
    `CasJizdyZMapy` (W-mapa, čekání, projíždí), `PrefixOmezeni`/`FormatDatumyOmezeni`/`UpravDatum`
    (1:1 txtNZ/UprDat), `JeViditelnyAtribut`/`TextAtributu`, `GetZnk` (zdvojení po vyčerpání),
    `ZiskejMarker` (dedup), `NormalizLinku`.
  - `ZastavkoveJrAdSmokeTest` (3) — strukturální smoke `GetData` nad živými daty: linka hledaná
    dynamicky přes `PrvniLinka` (žádná hardcoded gold linka → přežije editace dat), asserty na
    strukturu wire formátu (values/rows, ZASTAVKY/SKUPINY_DNU neprázdné, HODINA/MINUTA rozsahy,
    vazby odjezdů); chybové cesty (bez LINKA_ID, neexistující linka) → žádné stránky.
  - **Nález zdokumentovaný testem:** `ZarazeniSpoje` s PRÁZDNOU NZ konfigurací řadí pracovní
    spoj jen do sloupce „mimo prázdniny" (prázdné cnz == prázdné nzftl1) — gold-ověřené chování
    (0320144, AD-N15: `MHD_NZ1_NZ2` v praxi prázdné), ne bug.
- **Zbývá z T1b:** už jen `tools/compare-gold.ps1` (1:1 vs legacy jako skript mimo hook).

## 2026-07-02 — T2: Async export PDF/PNG nad snapshotem šablony (UI zůstává plně použitelné)

- ✅ **Export PDF/PNG neblokuje UI (R1) a uživatel může dál editovat** — dřív sync fetch +
  render na UI threadu (~9 s „Not Responding" u 600 stránek). Nově orchestrace ve VM
  (`ExportPreviewPdfAsync`/`PngAsync` přes `CommandAsync`): fetch (`BuildAllPreviewDataPagesAsync`,
  `Task.Run` + `IProgress<string>`) i render na threadpoolu.
- ✅ **Snapshot šablony** — `TemplateSerializer.DeepClone` (JSON round-trip, bez razítkování
  ModifiedAt, bez migratoru) při kliknutí na export; `PreviewCanvas.RenderMultiDataToPdf/Png`
  dostaly `Template?` parametr → renderují snapshot, ne živý stav. **Editace během exportu
  výstup neovlivní** (exportuje se stav v okamžiku kliknutí). Žádný blokující overlay —
  jen `_exportRunning` guard (canExecute obou export commandů) proti souběžnému exportu.
  Pokryto testem (`DeepClone_JeNezavislaKopieANemeniModifiedAt`).
- ✅ **Progress ve status baru ve dvou fázích** — „Načítám data X/N: {instance}…" (per instance
  při fetchi) → „Generuji PDF: X/N datových stránek…" / „Generuji PNG: X/N stránek…"
  (`IProgress<(done,total)>` z render smyčky).
- ✅ **První řez god-object úklidu (T4)** — export logika z code-behind do VM; view wiring
  = 2 callbacky (`RenderPdfToFile`/`RenderPngToFile`, nahrazují `RequestExportPdf/Png`);
  `OpenExportedFile` (Process.Start) ve VM — bez WPF, R4 OK.
- Známý krajní případ: ambientní `DocumentDefaults.Style` se při renderu nastavuje ze snapshot
  šablony, ale je procesně-globální — změna *výchozího fontu šablony* přesně během běžícího
  exportu by mohla prosáknout do části stránek. Ostatní editace jsou plně izolované snapshotem.
  Follow-up mimo scope: první `GetSchema` hit v `RefreshFieldsList` zůstává sync (mitigováno cache).

## 2026-07-02 — Roadmap: merge akčních položek z analýzy legacy náhrady

- ✅ **Akční položky z `analyza-stav-a-legacy-nahrada-2026-07.md` zamergovány do `roadmap.md`**
  (jedno místo pravdy, analýza zůstává jako evergreen referenční detail se status poznámkou):
  - Nová sekce **„Architektonický track T1–T6"** (T1a testy ✅, T3 ✅, T2 async export ⬅️ další,
    T1b Caché testy, T4 strangler, T5 duplikace, T6 drobnosti) + 3. aktivní track nahoře.
  - **Blok A** doplněn o legacy vzory (RG headless CLI AUTOEXPORT/AUTOPRINT pro A3) + **A4**
    (merge PDF + e-mail — společný požadavek všech 3 legacy systémů).
  - **Blok B** doplněn o legacy „MlineSestava" vzor + **B4** (per-instance přiřazení šablony,
    linka → šablona A4/A5) + **B5** (audit log + ECL právo na designer).
  - **Fáze C + D sloučeny** (legacy TiskJR generuje vývěsní i knižní z téhož enginu; oracle
    `mkPLinWin`, prerekvizity: Timetable font → agenda → LineTableElement + layout rozšíření).
  - **Fáze E** rozšířena o Relace (TiskJRRELACE, čeká validace use case).
  - **Nová Fáze F: Náhrada REPORTGENERATOR** (F1 usage statistika, F2 bridge vs čisté agendy,
    F3 TableElement dorovnání, F4 výstupní kanály, F5 jazykové varianty, F6 neportovat bez poptávky).
  - **Otevřené otázky Q1–Q6** (Epson, bridge, Relace, grafy/AZTEC, jazykové varianty, V1 soubory).

## 2026-07-02 — T3: TemplateMigrator guardy (forward-compat + hlášení chyb migrace)

- ✅ **Forward-compat guard (R5)** — šablona s verzí novější než `CurrentVersion` se odmítne
  `TemplateDeserializationException` („aktualizujte TemplateDesigner") místo tichého přerazítkování
  dolů (= ztráta dat novější verze při následném uložení). Chyba doteče uživateli existujícími
  kanály (soubor: `PopupDisplayer.Error`, DB: `CacheTemplateException`) — žádná změna UI.
- ✅ **Selhání migrace = chyba, ne tiché pokračování** — dřívější `Debug.WriteLine + continue`
  („partial migration") nahrazeno výjimkou s popisem migrace; částečně zmigrovaná šablona se
  neotevře (a tedy ani neuloží poškozená).
- ✅ **Sémantické porovnání verzí** — early-out přes `VersionComparer` místo string equality;
  „1.0" == „1.0.0" už nehlásí falešnou migraci.
- ✅ **Testovatelnost pipeline** — `TemplateMigrator(IEnumerable<ITemplateMigration>?)` ctor;
  testy s fake migracemi (transformace, pořadí, skip novějších, propagace chyby). Skip test
  `NovejsiVerze_SeOdmitne` odskipnut → **164 testů, 0 skipů**.
- **H-19 (V1→V2 migrace) — ověřeno v DB:** šablony v `^ALVA.TemplateDesigner.Templates`
  (CB_CSADAD, CB_GWBUS) mají `version: 1.0.0`; DB úložiště (Wave 3, 2026-05-25) vzniklo až po
  TextStyle breaking change (2026-03-24) → V1 v DB neexistují. Zbytkové riziko: staré JSON
  soubory na disku z éry před 2026-03-24 (bez `version` pole = nerozlišitelné od aktuálních);
  pokud se objeví, dopsat migraci + heuristiku detekce flat-TextStyle.

## 2026-07-02 — AD: multi-varianta časů tras (dynamické TrasaMin) — L1

- ✅ **Dynamické variantní časové sloupce ve StopListu** (nahrazuje legacy fixních 15 slotů TrasaMin).
  Agenda `ZastavkoveJrAd` seskupí obsluhující spoje **podle časového profilu W-mapy** (prutmbmp) →
  emituje `TRASA_MIN[]` per zastávka + `VARIANTY_TRAS[]` (hlavičky = písmena rozdílů tras / „ZT").
  N = skutečný počet variant (ne fixních 15, bez capu). StopList rozvine kolonku „Čas" na N sub-sloupců.
- ✅ **1:1 ověřeno proti gold** (`ALVA.TISKMHD.Data`, 0365005): 6 variant B,C,E,F,G,H, časy + `<`
  (projíždí) / prázdno (konec varianty) přesně jako legacy. Grupování 12 `cisloTrasy` → 6 profilů.
- ✅ **Přepínač `ShowAllRouteVariants`** (default zap) — vyp = jeden sloupec = výchozí trasa
  (`ZakladniCisloTrasy`, nejvíc spojů). V panelu i v Caché schématu `StopListElementEdit`.
- ✅ **Fix `CacheAgendaProvider`** — page-level pole/objekty (VARIANTY_TRAS) se zachovají jako JsonNode
  (dřív stringifikovány → zahozeny). Řádkové pole (ZONY…) fungovaly už dřív.
- **Ověřeno 1:1 s legacy i koncepčně:** „2 báze" (nejvíc-spojů pro časy / dta12 `W_POZ` pro odchylky-
  poznámky) odpovídá starému `JRapiLinPrtMhd` (ř.108-127 vs `OmTrasa` VzorTr). Zbývá: šířka variantních
  sloupců je zatím pevná (`VariantColumnWidthMm`), jemnější výběr variant (per-písmeno) = budoucí v2b.

## 2026-07-02 — TemplateDesigner.Tests: první sada automatizovaných testů (krok 0–1 arch. tracku)

- ✅ **Nový projekt `TemplateDesigner.Tests/`** (xUnit, zrcadlo EDControls.Tests) přidán do
  `EDISON2.sln` → pre-push hook TD od teď kryje automaticky. **104 testů (103 ✅ / 1 skip)**:
  - `Layout/PageLayoutEngineTests` (15) — stránkování, KeepTogether/SplitAcrossPages + ClipTop/Bottom,
    PrintBehavior matice (OnEveryPage/OnFirstPageOnly/OnLastPageOnly dvouprůchod/ExceptFirst),
    patička od spodu, PAGE/TOTAL_PAGES injekce, PerDataRow expanze, VisibilityExpression,
    dynamická výška elementu, propagace CultureCode. `FixedHeightCalculator` test double (bez SkiaSharp).
  - `Expressions/*Tests` (~60) — ExpressionEvaluator (porovnání numericky vs. lexikograficky,
    logické operátory, truthiness, fail-open, Validate), PlaceholderFormatter (string funkce,
    N2 cs-CZ×en-US, datum, TimeSpan, fail-safe), AggregationResolver (SUM/COUNT/AVG/MIN/MAX, prázdné řádky).
  - `Serialization/TemplateSerializerRoundTripTests` — round-trip idempotence pro všech 9 typů
    elementů (Serialize→Deserialize→Serialize, `modifiedAt` neutralizován — Serialize razítkuje UtcNow),
    tolerance neznámé property, RoundedFloatConverter, legacy „MHD"→„ZASTAVKOVY_JR_MHD" remap.
  - `Serialization/TemplateMigratorTests` — charakterizace současného chování + 1 Skip test
    (žádoucí forward-compat guard, odskipovat s T3 opravou).
- **Nálezy z psaní testů:** (a) early-out v `TemplateMigrator.Migrate` porovnává verze string
  equality, ne VersionComparerem → „1.0" se hlásí jako migrovaná (neškodné, zvážit při T3);
  (b) `dotnet sln add` vygeneroval kolizní solution folder „TemplateDesigner" → ručně odstraněn ze sln.
- Kontext + celý plán: [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md)
  (technický audit T1–T6 + legacy REPORTGENERATOR/TiskMHD/TiskJR + roadmapa náhrady).
- **Druhá vlna (tentýž den): TextStyle kaskáda + helpery → celkem 150 testů (149 ✅ / 1 skip):**
  - `Models/TextStyleTests` — CSS-like cascade (dědění, override, 3vrstvá kaskáda, trim fontu,
    idempotence, IsEmpty, Clone); `Models/DocumentDefaultsTests` — dokumentový default (overlay,
    prázdný font = no-override, propagace do Resolve), vždy s finally reset globálního stavu.
  - `Models/DepartureGridCascadeTests` — **regresní testy na anti-pattern „scalar přebíjí kaskádu"**
    (2× opravený bug, mvvm-patterns.md): `ResolveGroupMinuteParent` (MinuteFontSize/MinuteBold jen
    když MinuteStyle+DataStyle mlčí), `ResolveGroupHeaderParent` (SkupinaDnuHeaderLineHeight fallback,
    VAlign), padding 0 u minut, HourData dědění.
  - `Models/MmMathTests` (Round 0.1mm AwayFromZero, nullable, Snap), `Rendering/ColumnHeaderTests`
    (override vs. zdroj), `Elements/TextWrapHelperTests` (headless SkiaSharp — normalizace \r\n,
    extrémní šířky, prázdné řádky; bez assertů na pixelové metriky).
  - Testy stylů sdílejí xUnit kolekci `StyleCascade` — `DocumentDefaults.Style` je procesně-globální
    stav, kolekce vynucuje sekvenční běh (žádné vzájemné ovlivnění).
- **Třetí vlna (tentýž den): driftová pojistka EDInputForm schémat → celkem 161 testů (160 ✅ / 1 skip):**
  - `Serialization/EDInputFormSchemaDriftTests` — parsuje `CACHE/.../EDInputForm/*Edit.cls` přímo
    z repa (offline, bez serveru), vytáhne field keys (= property paths pro ElementFormBinder
    reflexi) a reflexí ověří existenci na cílovém C# typu. Mapa 10 schémat (8 elementů + Section
    + Page/Template). `StyleFields.Add` bloky expanduje podle suffixů čtených ze `StyleFields.cls`
    (self-updating). Virtuální klíče: `Format`/`Orientation` (PageEdit), `style:<prefix>` tlačítka
    pod-formuláře → validuje se path za prefixem. Uzavírá driftové riziko Wave 4 duplikace
    (memory `project_edinputform_schema_duplication.md`) — přejmenování property bez úpravy .cls
    od teď zablokuje push.

## 2026-07-02 — Startovní agenda z inputParam kontraktu

- ✅ **`inputParam` "Agenda=<kód>" → startovní agenda šablony.** `App.OnStartup` vytáhne kód
  z `EdisonLaunchArgs.InputParam`, `TemplateDesignerViewModel` ho ověří proti registru agend
  (case-insensitive) a nová šablona startuje rovnou s ní. Zdroj: atribut `input` uzlu stromu
  legacy shellu (JŘ uzel = `Agenda=ZASTAVKOVY_JR_AD`, MHD uzel = `Agenda=ZASTAVKOVY_JR_MHD`)
  a MHD2 tlačítko „Linky a oběhy". Neznámý/chybějící kód → dosavadní default.

## 2026-07-02 — Převzetí session shellu přes EDISONMAIN2 launch kontrakt

- ✅ **`App.OnStartup` parsuje EDISONMAIN2 argumentový kontrakt** (`EDControls.EdisonLaunchArgs`,
  zrcadlí `EDISONMAIN2API.FormToRunGetter`): sentinel `EDISONMAIN2` + server, restPort, namespace,
  idfa, login, fullName, mlp [, formName, shellGuid, inputParam]. Při spuštění ze shellu (starý
  EDISON přes wrapper formu `ALVA.TISKJR.formTISKJRTemplateDesigner`, MHD2 „Linky a oběhy") se
  MLRest staví ze session shellu (`CacheSchemaWiring.CreateMlRest`) místo z appsettings — TD běží
  pod správnou firmou a uživatelem.
- Bez argů beze změny (standalone z appsettings). Sentinel s nevalidními argy → varování + fallback
  standalone. `shellGuid` se záměrně nepřebírá — TD má vlastní session GUID, neváže se na ACTIVITY
  zámek shellu.

## 2026-06-30 — Ověření: pořadí poznámek v legendě 1:1 s legacy (živý gold oracle)

- ✅ **Default řazení legendy potvrzeno 1:1** proti gold oracle (`ALVA.TISKMHD.Data`, VOLANO_Z=JRMAIN)
  na CB_CSADAD lince **0365005** (page 1, Smrkovice → Dobešice). Pořadí **bezbariér → rozdíly tras
  (B,C,E,F,G,H) → omezení (29,68)** se shoduje včetně sub-pořadí a textů. `NotesLegendElement.Razeni
  = Vychozi` zachová Caché emit order → **žádný reorder není potřeba**.
- Vyvrací dřívější (retraktovanou) domněnku o nutném reorderu „typ1→2→3" — ta by pořadí naopak
  rozbila. **Neprověřeno na této lince:** pozice `atributy-zastavky` / `ruční-komentáře` / `směrové`
  vůči těmto třem (na stránce nebyly); emit order je dle kódu atributy → bezbariér → rozdíly →
  komentáře → omezení → směrové.

## 2026-06-30 — Font combobox plošně (sjednocení výběru fontu)

- ✅ **Jednotný font picker všude** — editovatelný ComboBox s nainstalovanými fonty (`SKFontManager`
  přes `FontResolver`) napříč všemi místy výběru fontu, ne jen v PageEdit:
  - `ElementFormBinder.PopulateFontFields` (sdílené, přesun z ElementEditDialog) — voláno ze **3 hostů**
    EDInputformu: `ElementEditDialog` + embedded form v `StopColumnsEditorDialog` / `DepartureGroupEditorDialog`.
  - `TextElementEdit.cls`: font List se 6 natvrdo hodnotami → **ComboBox** (všechny nainstalované,
    editovatelný, volitelný → prázdné dědí dokumentový default).
  - `StyleFields.cls`: font `AddText` → **ComboBox** (propíše se do všech tabulkových/JŘ style zón).
  - `CellStyleEditor` (per-column C#): font TextBox → **editovatelný ComboBox** (`FontFamilyOptions` DP,
    default `FontResolver.InstalledFamilies`); tri-state placeholder „dědit" zachován.
- Filtrování/omezení na podmnožinu fontů půjde později (změna jednoho seznamu). Varování „font není
  nainstalovaný" stále odložené (Arial fallback to mitiguje).

## 2026-06-30 — Výchozí font na úrovni šablony (dokumentový default + font combobox)

- ✅ **`Template.DefaultTextStyle: TextStyle?`** — dokumentový výchozí styl (UI zatím jen font, model
  rozšiřitelný o velikost/barvu). Kořen kaskády: elementy bez vlastního fontu ho zdědí.
- ✅ **Ambient `DocumentDefaults`** — `Apply()` overlayuje výchozí styl na zapečené dno kaskády
  (`ResolvedTextStyle.Default`, `CellDefaults.Default` + schedule/table rodiče převedeni na gettery;
  ze schedule rodičů odstraněn natvrdo `FontFamily="Arial"`; `CreateDefault` už font nenastavuje).
  Sync přes `PreviewCanvas.Layout()` wrapper (chokepoint před každým layoutem — pokrývá preview,
  PDF/PNG export, tisk i měření přetečení) + `DesignerCanvas.Render` + `RebuildSections`.
- ✅ **UI font combobox** — `PageEdit.cls` font pole = editovatelný ComboBox; klient ho po načtení
  schématu naplní nainstalovanými fonty z `SKFontManager` (`ElementEditDialog.OnFormInitialized`).
- ✅ **`FontResolver`** — jediný zdroj fontů (`SKFontManager`) + Arial fallback: neznámý/nenainstalovaný
  font → **Arial** (≈ Helvetica), ne náhodný systémový default. Vstup shovívavý (trim mezer, prázdné=default).
- 🐛 **Fix `ElementPropertyAccessor.Exists`** — traverzace podle typu (ne hodnoty) → zápis přes EDInputForm
  s null mezičlánkem (`DefaultTextStyle.FontFamily`) se už nezahazuje. (Týkalo se obecně vnořených cest.)
- 🐛 **Fix měření↔glyf asymetrie** — měření šířky v `DepartureGridElement`/`StopListElement` používá
  resolved font (vč. dokumentového defaultu), ne raw → sloupce se neořezávají při custom fontu.

## 2026-06-30 — Databáze jako primární úložiště šablon (JSON = import/export)

- ✅ **Sjednocení modelu ukládání** — Otevřít (Ctrl+O) / Uložit (Ctrl+S) / Uložit jako míří do
  **databáze**. Soubor degradován na **Importovat z JSON** / **Exportovat do JSON** (export nemění
  původ — kopie ven, obdoba PDF/PNG). Odstraněny duplicitní menu položky „Otevřít/Uložit do databáze".
- ✅ **Titulek ukazuje původ** `[DB]` / `[neuloženo]` (kam míří Uložit). Nová/importovaná šablona →
  Ctrl+S ji založí do DB (SaveAs formulář s jménem); existující → tichý update (bez confirm).
- **Pohlcuje S2** (asymetrie file/DB původu) — export už původ nemění, problém zaniká. Správce
  „Nástroje → Šablony v databázi" beze změny. Versioning prerekvizita (R5) splněna `TemplateMigrator`em.

## 2026-06-24 — Evidence nových zákaznických požadavků z prezentací (sekce P)

- 📝 **Zaznamenány 3 nové požadavky** z živých prezentací 2026-06-24 do
  `customer-requirements-zastavkovy-jr.md §P` (evidence, neanalyzováno; před implementací založit ticket):
  **P1** ruční poznámky do patičky, **P2** sloučení linek se stejným posledním trojčíslím (i různá Tč /
  více dopravců) do jednoho výstupu, **P3** logická imposice 2×A5 oboustranně na A4.

## 2026-06-23 — AD: datumová omezení jako negativní značky (cnz) + drobné opravy

- ✅ **Číslování omezení = číslo NZ (`cnz`) invertovaně** (1:1 legacy). Marker datumového omezení je
  nově `cnz` spoje (`$p(spoj,"#",7)`) renderovaný jako typ `nz` (invertovaný box), ne sekvenční „1".
  Text dál z per-spoj `SestavTextOmezeni`. Dedup per-cnz. `cnz` prázdné → fallback na staré sekvenční.
  Ověřeno gold oracle (`ALVA.TISKMHD.Data`) na **obou** firmách (CB_CSADAD „29", KV_DP 28/30/31/40).
  **Korekce dřívějších domněnek:** žádný firemní TYPPOZ gate (`MHDnewOM` prázdné u obou firem),
  KV_DP nebyl 1:1 ani předtím, text jde z per-spoj (ne ze zastaralého InPlatDef seedu). Detail §10.
- ✅ **Toggle `InvertovatNegativniZnacky` odstraněn** z `DepartureGridElement` — inverze NZ je teď
  napevno vždy zapnutá (AD i MHD, jako legacy); barvy `NegativniZnacka{Back,Fore}Color` ponechány.
- 🐛 **Drag DB pole nemělo výchozí rámeček** — `AddElementAtPosition` tvořil holý `TextElement` mimo
  `CreateElementByType`. Nově jde přes `CreateElementByType(Text)` → stejné defaulty (rámeček, rozměry)
  jako element z palety.

## 2026-06-23 — AD: konfigurovatelné rozdělení sloupců (typy provozu / FTL override)

- ✅ **Nový parametr „Rozdělení sloupců (typy provozu)"** v AD agendě — override FTL (default
  „Automaticky (dle linky)" = `MHD_TYP_R`) se 4 módy pokrývajícími FTL0-3 (= kombinace
  prázdninový split prac. dnů × sloučení víkendu). **Reuse existujícího `NacistSkupinyDnu` +
  `ZarazeniSpoje`** (engine beze změny, jen `$case` override `ftl`). Uživatel si tak může zvolit
  rozdělení/sloučení sloupců místo napevno dle dat linky.
- ✅ **Server-side param flow** — `RefreshDataForCurrentInstanceAsync` posílá print params do
  `GetData` (dřív jen instanci); změna server-paramu spustí **re-fetch** dat z Caché (client-side
  params jen překreslí, beze změny perf).
- **Datově:** víkend merge/split jede z Plk dat (funkční vždy); prázdninový split potřebuje NZ
  konfig (`MHD_NZ1_NZ2`, dormant ve všech firmách) → sloupec „o prázdninách" prázdný (očekávané).
- **Follow-up:** (1) generalizovat „server-side" flag parametru (zatím hardcoded
  `ROZDELENI_SLOUPCU` v C# re-fetch checku); (2) poznámka „jede v neděli" chybí u jednoho spoje
  0365005 (date-based omezení vs dateless denní note) — k dohledání; (3) jemné custom rozdělení
  (libovolné dny → sloupec) + multi-varianta tras ve StopListu = větší redesign.

## 2026-06-23 — DepartureGrid: adaptivní rozteč minut s anotacemi

- ✅ **Minuta s anotacemi se už nepřekrývá s další minutou** — slot odjezdu v `DrawMinuteCell` je
  nově adaptivní: nominální `scaledMinuteW` + naměřená šířka anotací (zachová nominální mezeru).
  Blok minut se zarovnává ze součtu adaptivních šířek (správný center/right align). Dřív se
  posouvalo o pevnou `MinuteWidth` → husté buňky s poznámkami se slévaly. Šířka sloupce
  (ComputeLayout) zatím beze změny — s redistribucí na šířku elementu bývá slack; měření šířky
  sloupce dle anotací = follow-up, pokud by dense buňka přetékala do sousedního sloupce.

## 2026-06-23 — NotesLegend: zalamování dlouhých poznámek

- ✅ **NotesLegend wrapuje text poznámek** na šířku sloupce (`DrawWrappedBody` přes `TextWrapHelper`)
  — dlouhá poznámka už nepřetéká doprava, ale zalomí se na další řádky (visutý odsazený blok pod
  tělem). Layout přešel z pevné mřížky řádků na **akumulaci Y per sloupec** (víceřádková poznámka
  posune další dolů). `CalculateHeightFromData` zrcadlí (výška = nejvyšší sloupec dle zalomených
  řádků → auto-scale u pevné výšky). Wrap je vždy zapnutý (overflow nikdy nechceme).

## 2026-06-23 — AD StopList: projeté zastávky + oprava dvojitého „na znamení" + názvy sloupců

- ✅ **AD: tisk projetých zastávek** (`ZastavkoveJrAd.BuildStrankaZastavky`) — agenda nyní emituje
  **celou trasu** ve směru (vč. projetých, analogicky MHD): `JE_AKTUALNI` na aktuální, projeté mají
  `casJizdy=0` a bez atributů. StopList je defaultně skryje (`ShowPassedStops=false` → výchozí výstup
  1:1 zachován); „tisk projetých zastávek" je zobrazí ztlumeně. Dřív AD emitovalo jen úsek od aktuální
  → toggle byl inertní (na rozdíl od MHD).
- ✅ **AD: oprava dvojitého „na znamení" (× x)** — agenda nastavovala `NA_ZNAMENI=true` (→ syntetický
  `SignalStopSymbol` „×") **i** přidávala atribut „x" → znak se tiskl dvakrát. Nyní `NaZnameni=0`,
  „na znamení" jde jen přes atribut „x" (1:1 legacy `UVOD^JRapiLinPrtMhd`).
- ✅ **Editor sloupců StopListu: čitelné názvy** — `StopColumnVM.DisplayName` odvozen z `Key`
  („Tarifní číslo (TČ)", „Základní trasa (ZT)", „Čas jízdy", „Průběhy trasy"…); tištěná zkratka
  v poli „Nadpis" zůstává. Uživatel už nemusí luštit „TČ"/„ZT".

## 2026-06-23 — Genuine page-overflow detekce (dokončení P0-2)

- ✅ **`PreviewCanvas.PageOverflows`: detekce sekce vyšší než stránka** — u `KeepTogether` sekcí
  (nelze rozdělit) hlásí přetečení, když `placed.Y + placed.Height > PrintableHeight` → obsah pod
  spodní hranou tiskové plochy se ořízne. Doplňuje dnešní #8 fix: ten odstranil false-positive u
  dělitelných sekcí, ale tím se ukázalo, že element-vs-sekce kontrola fakticky nikdy nenaskočí
  (sekce se sama roztáhne na obsah) → varování bylo dormantní. Teď chytá reálný případ.
- **Ověřeno live:** KeepTogether 400 mm → červená; přepnutí na SplitAcrossPages → bez červené
  (fix #8 drží); KeepTogether 100 mm + čistá šablona → bez false-positive.

## 2026-06-23 — Negativní značky invertovaně v mřížce odjezdů (legacy shoda)

- ✅ **DepartureGrid: NZ u minuty invertovaně** — negativní značka (`TypAnotace.NegativniZnacka`) se
  v mřížce kreslí jako **černý box + bílý znak**, jako legacy (`formTISKMHDReportDotNet_Module.vb`
  `FillRectangle(Color.Black)` + bílý text). Dřív se kreslila plain. Tři nové property:
  `InvertovatNegativniZnacky` (default zap), `NegativniZnackaBackColor` (černá), `NegativniZnackaForeColor`
  (bílá) — editovatelné v panelu (skupina „Anotace — symboly"). Backward-compat: staré šablony dostanou
  invert default = legacy shoda.
- **Ověřeno** na MHD agendě (mock NZ „22" v 6:45 → invertované). **AD záměrně bez NZ u minut** —
  v JRMAIN je CNZ jen klasifikátor sloupců dnů (omezení = textové anotace), ověřeno 1:1 proti gold
  oracle; přidání by AD od legacy odchýlilo.

## 2026-06-22 — Stylový pod-formulář přes CellStyleEditor (Fáze 2 — prototyp, 1 zóna)

- ✅ **`StyleEditDialog` + `StyleEditDialogViewModel`** — znovupoužitelný pod-formulář pro editaci
  JEDNOHO TextStyle (zóny) libovolného elementu přes sdílený **`CellStyleEditor`** (tri-state →
  zděděné hodnoty jako **šedý placeholder**, rozlišuje dědí/explicit, reset). VM čte/zapisuje styl
  reflexí dle prefixu; OnOk → `SetPropertyCommand` (undo). **Nikdy nezapíše null** (nenullovatelný
  HeaderStyle by spadl) — prázdná zóna = nový prázdný TextStyle / žádný command.
- ✅ **EDInputForm `ButtonClicked` event** (EDControls, aditivní) — klik na BUTTON pole dá hostu klíč
  → client-side akce (otevřít pod-formulář). Doplňuje server-side Validate.
- ✅ **Odjezdy „Styl záhlaví" → tlačítka** — HeaderStyle + 2. řádek balíčky (~30 polí) nahrazeny
  tlačítky „Upravit styl…" → pod-formulář. Hlavní „Obecné" se odlehčí. Commandy keyované prefixem
  (opakované otevření zóny nahradí) → aplikují se v parent OK jako jeden undo krok.
- **Prototyp:** jen zóna Styl záhlaví u Odjezdů; ostatní zóny + StopList zatím inline. `StyleEdit.cls`
  (Caché varianta A) ponechán jako nepoužitý server-side fallback. **Dál:** rozšířit na všechny zóny
  + StopList, pak uklidit. Detaily v roadmap (Fáze 2 — stylový pod-formulář).

## 2026-06-22 — Odjezdy „Obecné" → EDInputForm (Fáze 1, krok 4 — C# rewire) ✅ Fáze 1 hotová

- ✅ **Tab „Obecné" dialogu Odjezdů → `<eif:EDInputForm>`** (schéma `DepartureGridElementEdit`) místo
  ručních CellStyleEditorů + PropertiesPanel. Flow změněn z **live** (PropertiesPanel + executeCommand
  + undo na cancel) na **batch na OK** (jako StopList): code-behind `OkButton_Click` → `GeneralForm.Save()`
  → `ElementFormBinder.ExtractCommands` → `GeneralCommand`. Cancel = nic se neaplikuje.
- ✅ **VM odlehčen** — odebrány `ElementHeaderStyle`/`ElementDataStyle` + 9 element-level commandů +
  `PickElementColor` + `GeneralDescriptors` + ctor param; OnOk už nezapisuje HeaderStyle/DataStyle.
- ✅ **`ElementPropertyAccessor.Set` lazy-alokuje null mezičlánky** na cestě (generické) — pro nullable
  zóny jako `HeaderLine2Style`; jen při zápisu (Get/Exists nealokují → Cancel element nemutuje).
- ✅ **MainWindow** — undoDepth/cancel-undo zahozen; OK aplikuje GeneralCommand + GroupColumns +
  HourColumnStyle jako **jeden `CompositeCommand`** (single undo, jako StopList).
- Fix: `HourColumnWidth` jen v tabu Sloupce (řádek Hodiny) — odebráno z Obecné, ať nekoliduje (dvojí zdroj).
- **Fáze 1 (sjednocení tabulkových editorů) HOTOVÁ:** Zastávky i Odjezdy mají kostru Obecné(EDInputForm) /
  Styl záhlaví / Styl datových buněk / [element]-specifické. Známé low: hour header/width direct-write
  není undo; lazy-alloc po undo nechá prázdný `HeaderLine2Style`. **Navazuje:** extrahovat stylový balíček
  do pod-formuláře (StyleEdit + StyleEditDialog) — odlehčí oba editory (5 zón × 15 polí inline).

## 2026-06-22 — EDInputForm 4-skupinová kostra + sdílený StyleFields helper (Fáze 1, krok 4 — Caché část)

- ✅ **Sdílený `ALVA.TemplateDesigner.EDInputForm.StyleFields.Add`** — jediný zdroj „balíčku" polí
  stylu textu (Font/Velikost/Tučné/Kurzíva/Podtržení/Přeškrtnutí/Zarovnání H+V/Barvy/Padding/Řádkování)
  pro daný property-path prefix. Volitelně append do poslední skupiny + prefix labelů (víc zón
  v jedné skupině). Nové pole stylu stačí přidat zde → propíše se do všech zón i elementů.
- ✅ **`StopListElementEdit` přeskupeno** do 4-skupinové kostry: **Obecné · Styl záhlaví · Styl
  datových buněk · [Element]-specifické** (+ Pozice/Podmínky). Stylové skupiny přes helper.
  Stejné property-path klíče → chování zachované (jen seskupení); „Řádky" sloučeno do Obecné,
  „Zastávky/Směr/Zvýraznění" do „Zastávky — specifické".
- ✅ **`DepartureGridElementEdit`** — nové schéma ve stejné kostře (Obecné / Styl záhlaví +2.řádek /
  Styl datových buněk = minuty / Odjezdy — specifické = anotace symboly+text přes prefix labelů).
  `HeaderStyle.VAlign` primární (HeaderVerticalAlignment fallback), `TimeAxisMode` = enum názvy.
  **Zatím nenapojené** na dialog — čeká na C# rewire (lazy HeaderLine2 alloc, collapse legacy
  scalárů, SetHeaderFontSize cleanup v Init/OnOk).
- Standard: „Obecné = EDInputForm" pro jednoduché elementy; TableElement (DataGrid) je výjimka.

## 2026-06-22 — Auto + override šířka sloupců (Fáze 1, krok 2b)

- ✅ **StopList — nullable šířka (`StopColumn.Width` → `float?`)** — prázdné = **auto** (rovnoměrně
  rozdělí zbývající šířku elementu po pevných sloupcích), hodnota = pevná. Jediné místo:
  `StopListElement.ResolveColumnWidths` (Dictionary<StopColumn,float>); napojeno do layoutu
  (pozice šipky, BuildDataRowSpecs/BuildHeaderRowSpecs/RenderColumnHeaders), ApplyScale (škáluje
  jen pevné). Editor: `WidthText` (prázdné = auto), fit-to-width + overflow ošetřeno na null.
  **Default `Width = null`** (ne 10f) — jinak by se auto po uložení ztratilo (serializer WhenWritingNull).
- ✅ **DepartureGrid — per-group `WidthOverride`** (float?) — prázdné = auto (z obsahu + proporční
  dofill, jako dosud), hodnota = **pin** (vyloučen z přerozdělení zbytku). Sloupec Hodin edituje
  `HourColumnWidth`. Layout cache hashuje `WidthOverride`.
- ✅ **Sdílený `Controls.ColumnWidthText.Parse/Format`** — locale-robustní (přijímá tečku i čárku,
  InvariantCulture); použito oběma editory (odstraňuje duplikaci + tichý fail při desetinné čárce).
- Oba editory mají detail „Záhlaví + Šířka" se **stejným modelem** (prázdné = auto, hodnota = pevná).
  Existující šablony beze změny (mají šířky vyplněné = pevné). **Zbývá (krok 4):** Odjezdy
  „Obecné" → EDInputForm. **Později (krok 5):** content-aware auto šířka (dle délky dat).

## 2026-06-22 — Override nadpisu sloupce (AD-N6) + sdílený editor sloupců (Fáze 1, krok 1+2a)

- ✅ **Override nadpisu sloupce/skupiny** — `Rendering/ColumnHeader.Resolve(override, source)` = jediné
  místo sémantiky „override přebíjí zdroj, prázdné → zdroj". Napojeny **všechny tabulkové komponenty**:
  StopList + TableElement (zdroj = autorský `Header`) i DepartureGrid (`DepartureGroupColumnStyle.HeaderOverride`
  přebíjí nadpis skupiny dnů z dat). Koncept se neimplementuje per komponenta — liší se jen ČÍM je zdroj.
- ✅ **Sloupec Hodin přepisovatelný** — `DepartureGridElement.HourColumnHeader` (prázdné → „Hod"); render
  přes `ColumnHeader.Resolve`, Clone. Editor: override jde nastavit pro **všechny** sloupce vč. Hodin.
  Uzavírá legacy AD-N6 („Nadpisy sekcí pro rozvržení").
- ✅ **Sdílený seznam sloupců** `Controls/ColumnListEditor` + `Controls/IColumnRowVM`
  (`DisplayName`/`Visible`/`HasAnyOverride`/`ColumnWidth?`) — levý seznam jednotně pro Zastávky i Odjezdy
  (checkbox + název + override-tečka + „X mm" jen u pevné šířky + ▲▼). `StopColumnVM` i
  `DepartureGroupColumnVM` implementují rozhraní (v DepartureGroup přejmenováno `IsVisible`→`Visible`).
- ✅ **Pořadí skupin dle dialogu** — `ResolveSkupiny` (render) i ctor editoru řadí skupiny dle
  `GroupColumns` (dřív datové pořadí) → přeskládání ▲▼ se projeví ve výstupu a přežije reopen dialogu.
- ✅ **Zarovnání jednotně ve stylu** — odebráno duplicitní inline „Zarovnání" combo ze Zastávek
  (HAlign v `CellStyleEditor` mají oba). Detail „Struktura sloupce" sjednocen (Záhlaví; Šířka jen u Zastávek).
- Layout cache DepartureGridu hashuje override + pořadí (`GroupHeaderOverridesHash`, order-sensitive).
  JSON zpětně kompatibilní (chybějící = null = z dat). **Zbývá (krok 2b):** proporční auto šířka v obou
  (sjednotí i poslední rozdíl „X mm"); pak Obecné→EDInputForm u Odjezdů (krok 4).

## 2026-06-22 — Tisk s daty: členění výstupu + název souboru z dat

- ✅ **Členění výstupu** (`__EXPORT_SPLIT`) — při tisku s daty (více instancí zastávka × směr) volba:
  **Vše do jednoho PDF** (default, dnešní chování) / **Jeden PDF na linku** / **Jeden PDF na
  zastávku × směr** (legacy vývěska per zastávka). Single → `SaveFileDialog`; split →
  `OpenFolderDialog` + N souborů do složky přes existující `RenderMultiDataToPdf` (1 volání per
  skupina, žádná nová PreviewCanvas metoda). Seskupení `GroupBy("CISLO_LINKY")` (zachovává pořadí
  z Caché). Bez ZIP (volné soubory ve složce = přímo použitelné pro tisk vývěsek).
- ✅ **Název souboru z dat** (`__EXPORT_FILENAME`) — pattern s `{POLE}` tokeny vyhodnocený nad první
  instancí skupiny přes `DataContext.ResolvePlaceholders` (konzistentní s `{POLE}` všude jinde).
  Default `{CISLO_LINKY}_{POPIS_ZASTAVKA}_{POPIS_SMER}` (= legacy „NázevZastávky_ČísloLinky_…",
  funguje pro AD i MHD). Sanitizace neplatných znaků + kolize → přípona `_2`, `_3`. Pole viditelné
  jen ve split režimu (`PrintDialog.ApplyVisibility`).
- ✅ **Jako EDInputForm pole, ne bespoke WPF** — obě volby přidává **`AgendaParams.cls`** jako
  universální skupinu „Export" (společná všem agendám, NEjsou v `GetParameters` → neukáží se
  v preview liště). Hodnoty tečou přes sdílený `_paramValues` → **persistence per-agenda zadarmo**
  (přes existující `PrintParams`, líp než per-session). PrintDialog se nově staví i pro agendu bez
  `GetParameters` (`HasForm`, ne `HasParameters` — kvůli export skupině).
- **TODO** (roadmap): extrahovat na úroveň `Template`/agenda-config (firemní default jako legacy
  „Tisk → Linky-MHD"). Dorovnává legacy firemní „Název souboru pro export do pdf" + per-linka split.

## 2026-06-21 — Legenda poznámek: layout (sloupce / řazení / zarovnaný marker)

- ✅ **Víc-sloupcová legenda** — `NotesLegendElement.Columns` (1-4) + `WrapSmer` (po sloupcích / po
  řádcích). Render distribuuje poznámky do sloupců, výška = záhlaví + `ceil(N/Columns)` × řádek.
  Dorovnává legacy „Počet sloupců" (`stPOZ_PocetSloupcu`), bez 1/2 stropu.
- ✅ **Zarovnaný marker** — `MarkerSirka` (0 = inline jako dnes, >0 = pevná levá buňka se znakem →
  svisle zarovnané markery). Dorovnává legacy `stPOZ_SirkaSloupceTextVlevo`.
- ✅ **Řazení poznámek** — `Razeni` (výchozí / podle délky). Dorovnává legacy „Trid_delK".
- Vystaveno v EDInputformu (`NotesLegendElementEdit.cls`, skupina „Rozložení") i inline panelu.
  Default (1 sloupec, inline, výchozí řazení) = beze změny existujících šablon. Reference:
  legacy návrhář `formTISKMHDSekcePoznamkyEdit` (viz `ad-jr-typy-poznamek-layout-analyza.md §1b`).

## 2026-06-21 — AD #7161 D5 (skrýt legendu „× na znamení") + D6 ověřeno

- ✅ **#7161 D5** — `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)`: když šablona má StopList,
  ale žádný nemá zapnuté `ShowSignalStopIndicator` („Na znamení"), skryje se z legendy kategorie
  `atribut-zastavky` („× na znamení"). Legenda následuje viditelnost značky, žádné nové ovládání,
  žádná Caché změna. Test: CB_GWBUS 0320144. **#7161 uzavřen (D5+D6).**
- ✅ **#7161 D6 ověřeno** na CB_CSADAD 0365004 (agregát „všechny spoje bezbariérové" se parametrem skryje).

## 2026-06-21 — AD #7947 + #7161 D6 (rozdíly tras + bezbariér agregát)

- ✅ **#7947 „jede až do XY"** — `RozdilyTras` větev prodloužení vypisuje „jede až do zast. <konečná>"
  místo matoucího legacy „pokračuje přes". Záměrná odchylka (klient), kód analogický k „jede jen do".
- ✅ **#7161 D6 — skrýt agregát bezbariér** — parametr `SCHOVAT_AGREGAT_BEZBARIER` (bool). Caché taguje
  agregátní legendu (`allBBP` → kat. `bezbarier-agregat`), `NoteVisibilityFilter` ji skryje. Filtr-model
  jako #7955. Neověřeno na datech (žádná linka 100% bezbariér v KV_DP/CB_CSADAD — latentní).
- 🔧 **MCP `edison-cache` opraven** — deployovaný exe byl neúplný (chyběl runtime); rebuild +
  `.mcp.json` cesta na podsložku `C:\Edison\EdisonCacheMcp\`. Ověřeno proti gold oraclu (CB_CSADAD).
- 📄 **AD analýzy sloučeny** do `ad-jr-nastaveni-linky-analyza.md`; přípoje/km přesunuty do linkových JŘ.

## 2026-06-20 — AD #7955: co schovat v rozsahu „nepoznámkovat" (Fáze 2)

- ✅ **Parametr sestavy `SCHOVAT_V_ROZSAHU`** (enum: Ruční komentáře [default] / Automatické (rozdíly
  tras) / Obojí) — řeší #7955 (klient chce u spojů s vlastním komentářem schovat auto, ne komentář).
  Nad preview i v Ctrl+P (nativní param infra).
- **Model nadmnožina + TD filtr** (viz `feature-design-principles.md §5.2`): Caché už **nedropuje**,
  emituje všechny anotace/legendu otagované `KATEGORIE` + `SPOJ` + `V_ROZSAHU_NEPOZNAMKOVAT`. Schování
  je render-time `NoteVisibilityFilter` v TD → změna se projeví překreslením, bez Caché re-fetch.
- **Filtr** schová jen anotace v rozsahu „nepoznámkovat" (`V_ROZSAHU=1`) dané kategorie; legendu prořeže
  podle kategorie + přítomnosti znaku (sdílený znak mimo rozsah zůstane; „x na znamení" se neprořezává).
  Napojeno na všechny 3 datové cesty (preview, export, tisk reálných dat). Default = legacy (bez regrese).
- Caché legacy chování zachované (nepřeklopena sémantika „nepoznámkovat") — flip je čistě TD volba.

## 2026-06-20 — AD ruční poznámky linky z tabu MHD (Fáze 1)

- ✅ **AD-N7 Komentáře** — `ZastavkoveJrAd` čte ruční komentáře spoje (`MhD,"SeT_SpeC"` přiřazené přes
  per-spoj `MhD_SpeC`) → znak jako anotace u minuty + znak+text do legendy. 1:1 `JRapiLinPrtMhd1` ř.211-216.
- ✅ **AD-N8 Spoje, nepoznámkovat** — helper `NepoznamkovatSpoj` (1:1 `noPoz`): `MHDSPNPZN`=odTČ~doTČ,
  směrová logika dle parity spoje, fallback na konec trasy. Potlačí **ruční komentář** v rozsahu TČ
  (auto-poznámky běží dál — legacy sémantika; flip = Fáze 2 / #7955).
- ✅ **AD-N9 Směrové poznámky** — `MhD,"SeT_MhD"` tam/zpět/vše filtrované dle směru stránky
  (tam→`>`+`<>`, zpět→`<`+`<>`) → legenda. 1:1 ř.440-447.
- Caché-only (C# renderer beze změny — anotace + POZNAMKY existující cestou). Čitelné názvy proměnných
  (ne legacy zkratky). Zbývá: umístění směrových poznámek (legacy nahoru jako směrové záhlaví vs dole),
  Fáze 2 (#7955 opt-in přepínač).

## 2026-06-20 — AD nastavení linky 1:1 (track PRIORITA 2b)

- 📄 **Business analýza nastavení tisku na úrovni linky** — `ad-jr-nastaveni-linky-analyza.md`:
  podchyceno, jak legacy čte nastavení linky (taby MHD / MHD-další / dialog Nastavení tisku) přes 3
  readery (`gPrpLin`/`gPrpLinMhd`/`gSetLinPrt^JRapiLin*`), per-feature business význam + storage v
  `^JRL` + zda to máme/nemáme. Klíč: `ALVA.TISKMHD.Data` je jen formátovač, logika je v MAC `JRapi*`.
  Roadmap track **PRIORITA 2b (AD-N1..N15)** namapován 1:1 na §-čísla analýzy.
- ✅ **AD-N1 — Poznámka nad linku** — `ZastavkoveJrAd` nově čte `PATCH,"Head"` → placeholder
  `POZNAMKA_NAD_LINKU` (+ ve schématu). Legacy gPrpLin dta4.
- ✅ **AD-N2 — Typ JR** — `TYP_JR` (dosud prázdné) naplněno z `PATCH,"OBJ"` (1→„VÝLUKOVÝ", 2→„návrh
  jízdního řádu", znění 1:1 `JRapiLinPrtPS3`). Pozn.: zastávková větev OBJ nečte → volitelný
  placeholder, ne automatické razítko.

## 2026-06-17 — Preview: oprava falešného varování o přetečení

- 🐛 **Fix falešného červeného varování o přetečení** — `PreviewCanvas.PageOverflows` přestal hlásit
  přetečení pro JŘ element s pevnou výškou, který se do svého boxu vejde **auto-scalem** (zmenšením písma).
  Dřív: nový StopList (pevných 60 mm) na MHD mock datech (>~12 zastávek) → přirozená výška > box →
  `ContentOverflows=true` → červený rámeček celé stránky + „nevejde na stránku", i když element vizuálně
  sedl (auto-scale bez spodní meze ho vždy zmenší na box). Teď se hlásí **jen reálné geometrické
  přetečení** (element přesahuje spodní hranu sekce/stránky). `ContentOverflows` zůstává jako helper
  („box < obsah"), ale neřídí varování.
- ✅ **Agregovaná kontrola přetečení v tisku** — `PreviewCanvas.CountOverflowingPages(dataPages)` (layout-only,
  bez renderu) projede stránky všech instancí; `PrintDialog` při exportu reálných dat (data už fetchnutá
  z Caché → levné) vypíše „⚠ PDF uloženo, ale X z N stránek přetéká". Žádný fetch navíc, žádné nové UI.
  Ad-hoc kontrola bez generování PDF se případně doplní později na žádost zákazníků.

## 2026-06-17 — UI: font tokeny (škálování přes shell) + auto-width comboboxy + DEV cleanup

- ✅ **UI fonty na EDTheme tokeny** — natvrdo zadané `FontSize`/`FontFamily` v XAML (12 souborů) nahrazeny
  `{DynamicResource EDTheme.FontSize.*}` / `EDTheme.FontFamily`. TD nyní respektuje sdílenou user font
  preferenci z EDISONMAIN2 (FontService už TD inicializuje) a proporcionálně škáluje. Mapování
  9,10→Small · 11→Caption · 12→Body · 13,14→Header · 16,18→Large; `Consolas` (výrazy/glyfy) ponecháno.
- ✅ **Comboboxy v hlavním okně na `Width="Auto"`** (CmbGridSize/CmbSnapGap/CmbZoom/CmbPreviewZoom) +
  `MinWidth="64"` — text se neuřízne a roztáhne se i při zvětšeném fontu.
- ✅ **DEV cleanup** — odstraněn `#if DEBUG` auto-open `kv_ad_test.json` po startu (hardcoded osobní cesta;
  AD agenda hotová) v `MainWindow.xaml.cs` + `--test-detector` blok v `App.xaml.cs` (hardcoded `Downloads\*.PDF`).
  `PdfImport.ScheduleDetectorTest` zůstává jako ruční debug utilita (mimo app flow).

## 2026-06-15 — Wave 4: EDInputForm Image field type + migrace ImageElement (W4-6)

- ✅ **Nový `EFieldType.Image`** v EDControls EDInputForm — upload → base64 + náhled přes
  `Base64ToImageSourceConverter`. Image hodnota se **nikdy neposílá na server** (velký base64;
  `EDInputFormController` ji drží client-side a po Validate echu obnoví).
- ✅ **ImageElement migrován** do generického `ElementEditDialog` — Caché schema `ImageElementEdit.cls`;
  `EDInputFormSchema` switch v MainWindow mapuje `ImageElement → ImageElementEdit`. Uzavírá W4-6.
- Předtím (samostatné commity): EDInputForm Color field type + ComboBox field type (editable + autocomplete).

## 2026-06-12 — Wave 4: parametry tisku přes EDInputForm (Caché-driven)

- ✅ **Tiskový dialog → EDInputForm** — parametry sestavy se renderují přes EDInputForm, jehož schéma
  řídí agenda v Caché (`GetParameters`). Generická dispatch třída `ALVA.TemplateDesigner.EDInputForm.AgendaParams`
  (jedna pro všechny agendy) nested-callem získá param JSON a převede ho na `Form` (typy bool/number/enum/date/text);
  prefill aktuálními hodnotami přes `IN["values"]`. Žádná per-agenda EDInputForm třída — `GetParameters` zůstává
  jediný zdroj definic parametrů.
- ✅ **visibleWhen v C#** — `TemplateParameter.IsVisible(values)` (nový, jediný zdroj logiky) sdílí preview lišta
  i tiskový dialog; viditelnost (např. Hodiny od/do při „Vlastní rozsah") se přepíná živě na polích EDInputformu.
  `ParameterControlVM.UpdateVisibility` zredukováno na delegaci.
- ✅ **enum = required** v `AgendaParams` → List dropdown bez úvodního prázdného řádku (musí být vybráno).
- Odstraněn `ParameterControlVM` z tiskového dialogu + duplicitní nadpis; `CacheAgendaProvider.GetCacheClass` accessor.

## 2026-06-12 — Wave 4: „Vlastnosti sekce" přes EDInputForm + fix prázdného řádku v List

- ✅ **Vlastnosti sekce → EDInputForm** (W4-7 část) — nová Caché schema `ALVA.TemplateDesigner.EDInputForm.SectionEdit`
  (zrcadlí `BuildSectionDescriptors`: Název, Výška, Tisk/Umístění/Přetečení, rámeček, viditelnost).
  `ElementEditDialog` generalizován z `TemplateElement` na `object` (umí teď i sekci/stránku);
  `OpenPropertiesForSection` routuje na generický EDInputForm dialog místo `ElementPropertiesWindow`.
  Vědomé odchylky od starého panelu: vynechané „Datový zdroj"/„Výška řádku" (jen legacy PerDataRow,
  EDInputForm neumí conditional fields), bez clampu výšky, bez live validace výrazu viditelnosti.
- ✅ **EDInputForm List bez prázdného řádku u povinných polí** (EDControls `Field.cs`) — `ListItemsForDisplay`
  přidává implicitní prázdnou položku jen pro **nepovinná** pole (umožní vymazat na „nevybráno");
  u povinných (enumy) ji vynechá. Předtím se prázdný řádek zobrazoval ve všech strict List dropdownech.

## 2026-06-12 — Preview: varování o přetečení stránky + cap roztažení DepartureGrid

Dva nálezy z vizuálního testování na velkých linkách (0480202, 78 stránek):

- ✅ **Varování o přetečení v preview** — když se obsah stránky nevejde (přirozená výška obsahu >
  pevná Height boxu), přetékající stránka dostane **červený rámeček** (2,5px) a status bar zobrazí
  **červený blok** „⚠ Obsah se nevejde na stránku" (tooltip s radou zvětšit PageSize / zmenšit obsah).
  Detekce přes novou abstract `ScheduleElementBase.CalculateHeightFromData` (3 JŘ elementy = `override`)
  + `ContentOverflows` (`natural > Height + 0.5mm`, tj. **jakékoli** přetečení, ne práh). Běží v
  `PreviewCanvas.PageOverflows` **před** renderem (pre-detekce) → `OverflowChanged` callback →
  `HasPreviewOverflow` ve VM → status bar binding. Jen preview, ne v PDF/PNG exportu.
  (Iterace na vzhledu: zkusili jsme nádech přes obsah → podbarvení okolí → celé pozadí → finálně
  jen rámeček + blok v liště, což uživateli přišlo dostatečně výrazné a nejmíň rušivé.)
- ✅ **Cap roztažení DepartureGrid** — `AutoExpandToFillHeight` dostal strop `MaxAutoExpandFactor=1.5`.
  Řídká mřížka (např. linka s odjezdy jen v 1 hodinu) se jinak roztáhla na bizarně obří řádky;
  teď se vyplní max 1,5× přirozené výšky, zbytek místa zůstane prázdný.

## 2026-06-12 — ZastavkoveJrAd: dokončení 1:1 vůči legacy AD + performance

Agenda zastávkového JŘ z dat hlavního JŘ modulu (AD/JRMAIN) dotažena na **datové 1:1**
proti legacy gold oracle (`ALVA.TISKMHD.Data` VolanoZ=JRMAIN) + pročištěna výkonově.

**Funkční 1:1 (vše ověřeno proti gold oracle):**
- ✅ **Rozdíly tras** — `RozdilyTras`, 1:1 `OmTrasa^JRapiLinPrtMhd`: „neobsluhuje zast. X" /
  „jede přes…" / „jede jen do…" / „pokračuje…", marker z písmenné sady od „B", per zastávku.
  (Nejdřív zkusil MHD alg 1002 — ověřením proti gold se ukázal jako špatný, revertován; AD
  používá `OmTrasa`/NZ mechanismus.) Ověřeno 0365001.
- ✅ **POPIS_DOPRAVCE — adresa provozovny** — `NazevDopravce` čte i `$2` (ulice) z `^JRSET("DOP")`.
- ✅ **Filtr atributů zastávek** — `JeViditelnyAtribut` zúžen na jen „na znamení" (`x`); legacy
  `UVOD^JRapiLinPrtMhd` jiné atributy (A=autobus/L=vlak…) do StopListu netiskne. Odstraněna over-emise.
- ✅ **Atributy spoje (bezbariér)** — `BezbarierKonfig` (isinv/iscbe z `^JRSET("PS","LIMES")`, allBBP);
  `inv`=hlava:15→„H" (SpecZnak 72), `cbe`=hlava:22→„F" (70); anotace „specialni" + legenda.
- ✅ **Časy jízdy ve StopListu (TrasaMin)** — jeden sloupec = gold `TrasaMin1` (kumulativní min od
  vývěskové zastávky, jako MHD). Multi-varianta (`TrasaMin1-15`) odložena — chce zásah do C# rendereru.

**Performance (behavior-preserving, výstup byte-identický — 0365001: 680373 znaků, 39 stránek):**
- ✅ Per-linka/per-směr invarianty (`ObsluhovanePoradi`, `BezbarierKonfig`) memoizovány do `^||TiskAd`
  místo full-scanu spojů per stránka.
- ✅ W-trasa cachovaná mapou `poradi→cas` per `cisloTrasy` (`WMapa`) — nahradila lineární `$p` sken
  při každém lookupu; W-helpery (offset, časy, bitmapy, základní trasa) čtou z mapy.
- ✅ Bug fix: prázdná `cisloTrasy` (spoj bez trasy) padala na `<SUBSCRIPT>` — `^||` globály neberou
  prázdný subscript; přidán guard. Stejný `$d()` guard na `served` cache (merge z undef = `<UNDEFINED>`).

**Zbývá k úplnému 1:1 (zdokumentováno §10):** multi-varianta TrasaMin (renderer), číslování omezení
TYPPOZ=1 (firm-config), šířka/layout legendy.

---

> **Starší záznamy (2026-05 a dříve)** → [`changelog-archive-2026-05.md`](changelog-archive-2026-05.md).
