# 🗄️ Archiv — hotové kroky

> Vše pod touto čarou je už implementováno. Horní část roadmap obsahuje jen to, co je potřeba dělat.

## Session 2026-07-07 ✅ — Audit před produkcí: nízká priorita (2. dávka)

Pokračování auditu — nízko-prioritní body. Blokery **B1/B2** a vysoká priorita **H1–H5** zůstávají
otevřené v aktivní roadmapě.

- **Renderer (vrstva 4) — testy geometrie ✅.** Pozicovací matematika buňky (padding shrink, H/V
  zarovnání, clamp) vytažena z `CellRenderer.DrawCell` do čistého `Rendering/CellLayout` (behavior-preserving
  refaktor) + `TemplateDesigner.Tests/Rendering/CellLayoutTests.cs` (9 testů). Kreslení na SKCanvas se
  netestuje (SkiaSharp), ale výpočet kotvy ano — deterministicky, bez fontů. **Plný běh: 252/252 zelených.**
- **`App.xaml.cs OnUnhandledException` ✅.** Uživateli už jen `Exception.Message` (ne syrový stack trace),
  plný detail do `Debug.WriteLine`. `MessageBox.Show` v composition-root ponechán záměrně (last-resort net
  nesmí záviset na app službách, které můžou být tou padající věcí).
- **`MainWindow.OnPreferencesLoaded` ✅.** Async tělo obaleno try/catch → výpadek Caché při načítání prefs
  = nenápadný status „⚠ Nepodařilo se načíst uložené parametry" místo propadnutí do globálního handleru.
- **`EDInputForm/PageEdit.cls` deprecated `$$$Init` ✅.** Odstraněn z `Init`+`Validate` (jediný výskyt v TD;
  sladěno se sourozeneckými EDInputForm třídami). Caché → čeká compile v dev flow.
- **Zranitelné závislosti ⛔ akceptováno (mimo TD scope).** Jediná *zranitelná* `Newtonsoft.Json 7.0.1`
  (NU1903 HIGH) je pinnutá kvůli MLRest ABI (bind 7.0.0.0) → nejde bumpnout bez rebuildu MLRest; warning
  NEpotlačovat (memory `project_mlrest_newtonsoft_debt`). `ZXing.Net 0.16.9`/`PdfPig 0.1.13` jsou staré,
  ale ne zranitelné → bump nízká hodnota + riziko API churn. Reálná náprava = rebuild MLRest (EDControls task).

## Session 2026-07-07 ✅ — Audit před produkcí: střední-prioritní tech dluh (M1–M5) + rozhodnutí M6/M7

Z komplexního auditu kvality kódu (4 dimenze: .NET architektura, Caché/ObjectScript, produkční
připravenost, test coverage). Blokery **B1/B2/B3** a vysoká priorita **H1–H5** zůstávají otevřené
v aktivní roadmapě (viz „🔴 Audit před produkcí"). Střední priorita hotová/rozhodnuta:

- **M1 ✅ — prázdný subscript v MHD.** Guard `i (TypProvozuID="")||(SmerID="") continue` v
  `ZastavkoveJrMhd.PromitnutSpoje` — spoj s prázdnou skupinou dnů / směrem se přeskočí místo pádu
  celé linky (`^||TiskMHD(…,"")` = `<SUBSCRIPT>`). Behavior-preserving pro validní data (0/1).
- **M2 ✅ — try/catch kolem 3 živých `GetMockData`.** `ZastavkoveJrAd` (commit 876ada3),
  `KnizniJrAd`, `KnizniJrMhd` — obaleno `try { $$$FillParams … } catch (ex) { $$$CatchErrRest } ret $$$OK`.
  Vadná data na první lince firmy → čistá hláška v preview místo syrového pádu. (Odložený drobný
  follow-up: statický vzorek nezávislý na datech, `ZastavkoveJrAd.cls:1219` — nízká hodnota.)
- **M3 ✅ — únik Skia handles v `DepartureGridElement.Render`.** Alokace fontů/paintů/zebra
  (`CreateFont`/`SKColor.Parse` nad uživatelskými barvami mohou vyhodit) přesunuta dovnitř `try`;
  deklarace zůstaly na method scope (helpery je zachytávají), 4 anotace fonty nullable + `?.Dispose()`.
  Řeší i neuvolněný clip při chybě setupu. Build 0 chyb / 0 nových warningů.
- **M4 ✅ — konstrukce undo příkazů z code-behind do VM.** 4 metody
  `Apply{DepartureGrid,StopColumns,LineTableColumns,DynamicTable}Edit` + helper `ExecuteAsComposite`
  v `TemplateDesignerViewModel` (WPF-free, testovatelné); `OpenXxxDialog` v code-behind už jen otevírá
  dialog + volá VM. Sémantika příkazů identická. (Otevírání dialogu zůstává v code-behind → dořeší H3.)
- **M5 ✅ — unit testy undo/redo.** `TemplateDesigner.Tests/Commands/UndoRedoCommandTests.cs` (14 testů:
  Set/Move/Resize/Add/Delete element+sekce, Move/Reorder sekce, Group/Ungroup, **Composite undo v opačném
  pořadí**, **ScaleTemplate inverzní round-trip**). Všech 14 zelených.
- **M6 ⛔ akceptováno (won't-do).** Import legacy (předrefaktorového) JSON tiše zahodí styling
  (flat text props → `Style` default, `WasMigrated=false` → bez varování). Reálná expozice ≈ nulová:
  DB je primární úložiště od 2026-06-30 a všechny DB šablony jsou už composable; plochý JSON = mrtvý
  dev-era artefakt. Není to pád ani poškození dat (import = nová neuložená šablona, špatný font hned
  vidět). Spolehlivá detekce by false-positivila. YAGNI → neřešit.
- **M7 ⏸️ odloženo (parkovaný perf-item).** `WMapa` kopíruje celou W-mapu (`k`+`m`) při každém lookupu,
  vč. uvnitř smyčky zastávek×variant (`ZastavkoveJrAd.cls:354`), ač `CasJizdyZMapy` čte jen 2 uzly.
  Čistě latence, ne korektnost — výstup gold-ověřený 1:1, engine „výkonově pročištěný", žádná nahlášená
  pomalost. Zásah = riziko parity. Recept (až se objeví pomalá velká linka): (1) předpočítat variantní
  mapy jednou před smyčkou zastávek; (2) číst `^||TiskAd("wmap",ct,poradi)` přímo místo `k`+`m`.
  Zapsáno v backlogu „Tech dluh".

⚠️ **Poznámka:** Caché edity (M1, M2/KnizniJr*) čekají na compile v dev flow — MCP `cache_compile`
míří na `C:\DEV\EDISON`, ne na tento repo. Syntaxe standardní, shodná s `GetData` ve stejných souborech.

## Session 2026-07-06/07 ✅ — Layout tabulek: „sekce vyšší" + DynamicTable varianta A (R2 zrušeno)

**„Sekce vyšší než stránka" ✅** — designové plátno roste přes víc A4 (Slice 1) + dělící čáry stránek
(Slice 2) + fix dvojitého vykreslení rámečku u split sekce. Preview i PDF/PNG dělí přes stránky pixel-ořezem.

**DynamicTable — rozhodnuto: varianta A (chová se jako zastávky).** `Height` = strop, obsah se vejde
auto-scalem, prvek se nerozrůstá přes stránky. Dynamická výška (`Height=null`) roste s obsahem. Sekce
vyšší než stránka → pixel-ořez (jako StopList). Oprava: vnější rámeček tabulky vsazen dovnitř o půl
tahu (`InsetForStroke`) — dřív se hrana ořezávala těsným clipem sekce.

**R2 (svislé řádkové stránkování + opakované záhlaví) — přidáno a zase odstraněno.** Během session
vyzkoušeno (zlom po celých řádcích, opakované záhlaví, respekt Y pozice), funkčně ověřeno renderem.
Po revizi konzistence ale **vědomě zrušeno**: „rostoucí tabulka přes stránky" je nekonzistentní se
zbytkem návrháře (statické prvky i zastávky mají výšku = strop) a čisté „opakované záhlaví na multipagi"
jde jen s rostoucí tabulkou. Rozhodnutí: raději čistě do produkce, R2 přidat zpět jen na **konkrétní
poptávku**. Kód odstraněn kompletně (žádný mrtvý kód): `IRowPaginatedElement`, `LayoutRowPaginated`,
`PlacedSection.StartRowUnit/EndRowUnit/RepeatHeader`, `RenderRowWindow`, R2 větev v `PreviewCanvas`, testy.
**Spec pro budoucí re-implementaci zůstává** v `r2-partial-fit-kontrakt.md`. Detail: [`changelog.md`](changelog.md).

## Session 2026-07-03 ✅ — Wave 4 W4-10: inline panel na EDInputForm

Levý panel „Vlastnosti" nově hostuje **EDInputForm pro jeden element** (všech 9 typů přes `*Edit.cls`
schema + `ElementFormBinder`) místo PropertyDescriptorů → sjednocený „dvojí vizuální jazyk" (panel i
modály jedou na stejném form-driven UI). Detail: [`changelog.md`](changelog.md).

- **Živý write-back:** `EDInputForm.FieldChanged` (nový event) / Enter → `ExtractCommands` → SetPropertyCommand
  (undo) → canvas. Enter v poli commitne (ne otevře dialog); bare klávesy patří poli.
- **EDControls (additivní):** `FieldChanged` event + `CacheClass` setter zahodí controller (runtime
  přepnutí schématu — dřív držel první třídu → StopList ukazoval text pole).
- **Nový `Controls/InlinePropertiesForm`** (embedded host + live state machine: re-Prefill na výběr,
  guard proti rekurzi, ošetřený `Loaded` re-init).

**Wave 4 tím funkčně uzavřena.** Zbytek NE-hotový, ale záměrně:
- **W4-11** (smazat PropertyDescriptory) — neproveditelné: drží je FormatToolbar + multi-element edit +
  fallback modál + AI `CapabilityCatalog`. PropertyDescriptory zůstávají jako paralelní systém.
- **W4-12** (EDInputForm wire `$lb()`→JSON, collapsible groups) — volitelné, EDControls-framework concern.
- **Polish (later):** undo/redo nerefreshne zobrazené hodnoty v panelu (display staleness do reselectu);
  přepnutí typu = 1 Caché reload schématu (cache per typ, když bude lagovat).

## Parked known-limitation — canvas perf při vysokém zoomu (2026-07-03)

Vyřazeno z aktivní roadmapy — na stropu 500 % funguje svižně (ověřeno). Vrátit se k tomu jen když se
ukáže reálný problém na uživatelských PC (poptávka).

- **Podstata:** `SKElement` drží byte buffer celé plochy (`pageW × MmToPx`). Zoom 500 % A4 ≈ 3969×5615 px
  ≈ 89 MB; 1000 % ≈ 365 MB. Hardware-bound bottleneck SkiaSharp.Views.WPF.
- **Workaround (aktivní):** zoom zastropován na 500 % (`ZoomLevels` max 5.0).
- **Proper fix (~4–6 h, rizikové):** virtualizace — SKElement jen ve velikosti viewportu, obsah posouvat
  scroll offsetem, kreslit jen viditelnou část. Riziko: `e.GetPosition(skCanvas)` je dnes absolutní px
  celé stránky → každá interakce (drag/resize/rubber band/ghost/hit-test) by potřebovala scroll-offset
  transformaci. Dělat jako samostatnou session, ne mezi cleanupy.

## Session 2026-07-03 ✅ — Architektonický track: malé T kousky

Prošli jsme T1–T6 a odbavili všechny **malé self-contained** kusy (detail v [`changelog.md`](changelog.md)):
- **T5a** — `EDList/Instance.GetClassForAgenda` deleguje na nový `AgendyRegistr.GetClassForCode`
  (auto-discovery z `%Dictionary`, žádný hardcoded mirror). Ověřeno přes `ListAgendy`. **T5b přeskočeno**
  (živý AD mock ponechán záměrně).
- **T6** — `architecture.md` doplněn o `/Services`+`/PdfImport`; per-frame `new SKPaint` posouzeno = zavřeno
  (nevyplatí se). Celé T6 hotové.
- **T4 malé kousky** — R4 fix `TableEditorDialogViewModel` (inline WPF picker → code-behind přes callback,
  VM 0× System.Windows) + rename `MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`.
- **StopList kaskáda testy** — `StopListCascadeTests` (9 testů): feature override aktuální/projeté zastávky,
  legacy `col.Alignment` fallback, per-column Style priorita, element-level HeaderStyle.

**Záměrně otevřené (velké, ne blocker):** T1b-robustní (zmrazit vstup do izolovaného namespace) · plný
T4 god-objects strangler (postupné řezy velkých tříd).

## Session 2026-07-03 ✅ — Pre-pilot UX polish (Fable audit, celý blok)

Audit 19 oken proti produktové filozofii („srozumitelnost nad úplností"). **Celý P0 + akční střední/malé
položky hotové**; detail v [`changelog.md`](changelog.md) 2026-07-03.

**🔴 P0 (U1–U7) — vše hotovo:**
- **U1** BUG: „Ano = uložit" v confirm neuložených změn ukládalo do JSON souboru místo DB → teď DB
  (stejná cesta jako Ctrl+S). Celý confirm/close flow převeden na async (`ConfirmUnsavedChangesAsync`,
  `SaveAndContinueAsync`) + fix pádu při zavření čistého okna (Close přes `Dispatcher.BeginInvoke`).
- **U2** context menu „Vlastnosti elementu…" → otevře dialog (dřív jen přepnulo panel).
- **U3** klik na paletu (bez tažení) vloží element do aktivní sekce (fallback v `EndAddingElement`).
- **U4** dvojklik do prázdna už neotevírá vlastnosti stránky — zruší výběr.
- **U5** Ctrl+S bez DB → popup s nabídkou JSON exportu (dřív tiché do status baru).
- **U6** cancel sjednocen na „Storno" přes `{StaticResource EDStrings.Cancel}` (7 dialogů) + pravidlo do `ux-conventions §1`.
- **U7** M/O snap tooltipy — už existovaly (audit stale).

**🟠 Střední — akční hotové:**
- **Skryté featury do context menu** — Seskupit/Rozeskupit (Ctrl+G/Ctrl+Shift+G), „Rozdělit → Vodorovně/Svisle" (Ctrl+Shift+H/J), „Upravit text (F2)".
- **Drift tabulky zkratek** — `ux-conventions §3` přepsáno 1:1 dle KeyDown handleru (Ctrl+Shift+S smazán; doplněno ~10 chybějících).
- **Zoom tlačítko × zkratka konzistentní** — „Fit"+Ctrl+0 = přizpůsobit; „1:1"+Ctrl+1 = 100 %.
- **StatusText segmentace** — status bar rozdělen na „stav výběru" (`SelectionStatusText`, klid) + „akce/progres" (`StatusText`, transient) → nepřepisují se.
- **Escape ruší probíhající drag/resize/rubber band** (restore z init).

**🟢 UX polish (otevřené) — prošlo 2026-07-03, ověřeno proti kódu:**
- **Stylový pod-formulář** ✅ hotové — `StyleFields.cls` (sdílený balíček) pohání stylové zóny ve všech
  EDInputForm „Obecné" tabech (DepartureGrid Data/Minuta/Symbol/Text, DynamicTable + StopList Header/Data);
  `StyleEditDialog` používá sdílený `CellStyleEditor` (tri-state). `StyleEdit.cls` = záměrně parkovaná
  server-side varianta A (dokumentováno v hlavičce, ne mrtvý kód).
- **Sjednotit editor sloupců** ✅ hotové — `ColumnListEditor`/`IColumnRowVM` sdílí DepartureGroup,
  DynamicTable, StopColumns; dormant TableElement vyňat záměrně.
- **Font warning** + **ad-hoc overflow tlačítko** = YAGNI (přetečení se hlásí při exportu; font se vybírá
  z InstalledFamilies) → přesunuto do backlogu.
- **Tabulkové featury** (grouping / computed fields / nested datasource) → backlog (bez customer demand).

**Vědomě odloženo / parkováno:**
- **Ikonový jazyk** (Fluent + glyfy) — necháváme, není zásadní.
- **Rubber band napříč sekcemi** — nepotřebujeme; sahá do selection sémantiky, pilot potvrdí.
- **Dvojí vizuální jazyk = left-panel refaktor** (PropertyDescriptor → EDInputForm, W4-10/11) — vyčleněno
  do backlogu jako samostatný velký track (chceme do budoucna).
- **Layout robustnost:** „sekce vyšší než stránka" (var B, největší koncepční layout dluh) — pilot-gated,
  vytaženo do „Větší dedikované tracky" v roadmapě. „Element drag mezi sekcemi" — **zamítnuto 2026-07-03
  (nepotřebujeme; cut+paste stačí).**
- **🧭 Filozofie:** rozšířit UX mřížku o vrstvu menu/toolbar/zkratka (`feature-design-principles`) — doc, low-prio.

## Session 2026-07-03 ✅ — EDISON launch integrace + úklidy

**EDISON → TD launch integrace (A1 + A2, Martin Hubata, merged do main).** TD se spouští ze stromu
starého EDISONu (uzel „Návrhář sestav (nový)" 76016 v MHD + JŘ; launcher `formTISKJRTemplateDesigner`
+ MHD2 „Linky a oběhy") přes `Process.Start TemplateDesigner.exe` s **EDISONMAIN2 arg kontraktem**:
- **A1 session** — `EDControls.EdisonLaunchArgs` parsuje kontrakt (sentinel `EDISONMAIN2` + server,
  restPort, namespace, idfa, login, fullName, mlp [, formName, shellGuid, inputParam]); `App.OnStartup`
  staví MLRest ze session shellu (`CacheSchemaWiring.CreateMlRest`) místo z appsettings → TD běží pod
  **správnou firmou/uživatelem**. Bez argů = standalone z appsettings (fallback). `shellGuid` se
  nepřebírá (TD má vlastní session, neváže se na ACTIVITY zámek).
- **A2 startup agenda** — `inputParam "Agenda=<kód>"` (JŘ→`ZASTAVKOVY_JR_AD`, MHD→`ZASTAVKOVY_JR_MHD`)
  → nová šablona startuje rovnou nad tou agendou. Ověřeno na devu: DPKV → linky DPKV, CB_CSADAD → linky
  CB_CSADAD. **Open-designer flow per-zákazník funguje**; pro v1 zbývá už jen ruční release deploy (D3/D4).

**T1b gold regrese base** — `ZastavkoveJrAdGoldTest` (2 gold linky 0365001+0365005, digest = počty +
MD5 celého GetData výstupu), v pre-push (27/27). Pevně vůči CB_CSADAD; robustní verze (zmrazit vstup)
odložena.

**Úklidy:** T6 `ScheduleDetectorTest.cs` odstraněn z prod assembly; poslední `MessageBox` v MainWindow
odstraněn spolu s vestigiálním menu „Vlastnosti textu…" (dvojklik/Enter dělá totéž). W4-7 Page potvrzeno
hotové (`PageEdit.cls` přes generický `ElementEditDialog`). DynamicTable unit testy (6). R1a průchod
(vnitřek čistě rozvrstvený; jediný nález = zarovnat render na sdílený `ColumnRenderer.DrawRow`, odloženo).

**AD zastávkový JŘ track uzavřen** — 1:1 hotový; zbytek (vizuál legendy POZNAMKY, AD-N5 `MHD_NEZNZ123`,
AD-N15 prázdninové NZ) záměrně navázán na reálný pilot (odemyká je konkrétní zákaznická konfigurace/data,
kterou teď nemáme). Odložené helpdesk: A2-A5 (#7937 indent), A2-A6 (#7959 filtr neobsluhovaných), A2-B4
(#8828 per-sloupek) — datový průzkum / diskuze.

## Session 2026-07-02 ✅ — DynamicTableElement + AD 1:1 uzávěrka + architektonický track

**DynamicTableElement** — rozdělení tabulek: dynamická (řádky z `DataSource` kolekce) samostatný
element + 2-tab editor (Obecné EDInputForm `DynamicTableElementEdit.cls` + Sloupce přes
`ColumnListEditor`/`CellStyleEditor`); statická `TableElement` uspána (JsonDerivedType jen pro
legacy načtení, z UI pryč). Auto-scale do pevné Height, auto-distribuce šířky, seed sloupců z polí
kolekce centralizovaný v dialogu, dvojklik otevře dialog. **Uzavírá Wave 4 W4-9** (komplexní
elementy: StopList ✅ + DepartureGrid ✅ + dynamická tabulka ✅). Detail changelog 2026-07-02.

**AD zastávkový JŘ = 1:1 kompletní.** Hotové AD-N: N1, N2, N3, N6, N7, N8, N8b, N9, N10, **N13
(multi-varianta TrasaMin / L1, 1:1 gold 0365005)**; číslování omezení `cnz` ✅; pořadí legendy
ověřeno ✅. **AD-N4/N11/N12/N14 přeřazeny do linkových JŘ** (legacy zastávkový je nepoužívá).
`GetSchema` kolekce (ZASTAVKY/SKUPINY_DNU/ODJEZDY/POZNAMKY) synced 1:1 s reálnými `Build*` daty
(doplněna skutečná skalární pole, phantom NADPIS2 odstraněn). Zbývá jen vizuál legendy + N5/N15
(blokované dormant daty).

**Helpdesk A.2-D ✅** — #7947 („jede až do XY"), #7955 (custom komentář přebíjí auto + NZ bug),
#7161 (skrytí „na znamení" + vypnutí agregátu bezbariér) zavřeno pro AD agendu. #11300 (KORID
přípoje) přeřazeno k linkovým JŘ (není zastávkový koncept). Port na MHD agendu neproveden (až bude
potřeba). Odložené A2-A5/A6/B4 zůstávají (datový průzkum / diskuze).

**Architektonický track** — T1a C# testy (164, pre-push) ✅, T1b Caché testy (25/40) ✅ částečně
(zbývá compare-gold skript), T2 async multi-instance export nad snapshotem ✅, T3 migrator guardy
(forward-compat R5) ✅. Rozhodnutí **R1–R7** pro škálování agend zdokumentována
([`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md)).

**Caché Migrace** — Wave 1/2/3 ✅; Wave 4 kroky W4-0..W4-9 ✅ (elementy + Image + Section + **Page**
`PageEdit.cls` + parametry + StopList/DepartureGrid/dynamická tabulka). Zbývá jen W4-10/11 (inline
panel migrace + PropertyDescriptor cleanup — velký refaktor, záměrně na konec).

## Handoff session 2026-06-21 (historický záznam) ✅

> Archivováno 2026-06-30. Tento blok byl původně živý na vršku roadmapy jako handoff pro pokračování
> z druhého PC. Většina je dnes hotová nebo překonaná; ponecháno jako historický kontext.

**Hotovo v session 2026-06-21:** AD nastavení linky + ruční poznámky (Komentáře / nepoznámkovat /
směrové), #7955 přepínač, „jede až do" (#7947), skrytí na znamení + agregát bezbariér (#7161 D5/D6),
layout legendy (sloupce 1-4 + směr wrapu + zarovnaný marker + řazení). Helpdesk zavřeno: **#7955,
#7959, #7947, #7161**.

**DEV tweaky session** (vyřešeno): AD startup šablona revertována zpět na produkční MHD+3 sekce
(commit „revert DEV startup tweak"). Dev firma později přepnuta na **CB_CSADAD** (demo / gold linka
0365001, commit 9ac5577) — aktivní stav viz živá roadmap „Před produkcí".

**Doc-cleanup body z handoffu** (1 = ořez `cache-database-linky.md §6/§10`, 2 = hlavičkové poznámky,
3 = cross-refs, 4 = changelog archivace) — řešeno průběžně v živé roadmapě. Prezentační prep docs
(osnova, režie demo) konsolidovány 2026-06-30 do
[`prezentace-a-gap-2026.md`](prezentace-a-gap-2026.md), zdrojové artefakty smazány. **Gap analýza
legacy vs nový ponechána** jako samostatná evergreen reference (`gap-analyza-legacy-vs-novy-jr.md`).

## Wave 3 — Template storage ✅ (2026-05-25)

Caché-driven persistent storage šablon s per-IDFa izolací + schema versioning framework pro defensive ochranu před budoucími breaking changes.

**Implementováno** (3 commity):

| Krok | Co | Commit |
|---|---|---|
| W3-1 | Caché `ALVA.TemplateDesigner.API.Sablony` — 5 CRUD metod (List, Load, Save, Delete, Rename) | `5ac9dd0` (2026-05-15) |
| W3-2 | .NET `CacheTemplateService` async wrapper + `CacheTemplateException` | `5ac9dd0` |
| W3-3 | `CacheTemplatePickerDialog` + `SaveToCacheDialog` UI | `5ac9dd0` (L5 migration na EDList: `9f5b165`) |
| W3-4 Fáze 1 | Schema versioning + `TemplateMigrator` framework (H-18) | `174d169` (2026-05-25) |

**Storage layout:**
```
^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Metadata") = $lb(Name, AgendaCode, Author, CreatedAt, ModifiedAt, ModifiedBy, Version)
^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Data")     = <JSON blob šablony>
^ALVA.TemplateDesigner.Templates(IdFa, "IDX", "ByAgenda", AgendaCode, TemplateID) = ""
```

**Schema versioning framework** (Fáze 1):
- Template JSON má top-level `version` (semver `"1.0.0"`)
- `TemplateMigrator` pipeline aplikuje sekvenci migrací při loadu
- Žádné konkrétní migrace zatím — framework no-op, připraven na budoucí breaking changes
- Convention v [`template-schema-versions.md`](template-schema-versions.md)

**Vědomě skipnuté** (decision rationale):

| Item | Důvod skipu |
|---|---|
| Auto-snapshots history (Fáze 2) | Duplicate templates (Save As) + within-session undo pokrývají 90%+ use cases. Velké tools (Adobe, Sketch, AutoCAD) auto-snapshot taky nemají; jen massové cloud tools (Figma, Canva) ho mají jako defense in depth pro broad audience. TD scope (3-5 customers, 5-10 users/customer) tomu neodpovídá. |
| Per-template named history (Fáze 2 β) | Save As pattern poskytuje stejnou hodnotu — verze viditelné v picker, lze porovnat paralelně |
| Audit log (W3-6) | Bez user requestu. Revisit on real need (compliance, debugging) |
| Thumbnails (W3-7) | Nice-to-have, low priority. Picker list zobrazí metadata (Name, Author, ModifiedAt) — stačí |
| Soft-delete | Hard delete + Caché backup procedures stačí |
| Import wizard (W3-9) | Málo legacy templates, manual Open + Save to Cache stačí |
| Server-side migration | .NET-side migrátor v Fázi 1 stačí (single source of truth při loadu) |

**Klíčová architektonická rozhodnutí:**
- **Per-IDFa storage** — multi-tenant isolation
- **Atomic Metadata + Data split** — list zobrazí jen metadata (rychlé), Data fetch on demand
- **Per-agenda sekundární index** (`IDX/ByAgenda`) — efektivní filter v picker
- **JSON wire formát (R3)** — typed records v .NET
- **Semver versioning** — flexibilita pro budoucí major/minor distinction
- **Idempotentní migrace** — repeated apply = same result
- **Defensive load** — chybějící version v JSON → assume `1.0.0`, neházet exception
- **Single-point wire-up** — `TemplateSerializer.Deserialize` routes through migrator pro file i Caché path

---

## Wave 2 — Shared catalogs ✅ (2026-05-20)

**Scope** po diskusi zúžen ze 4 položek na 1 implementovanou + 3 skipnuté:

| # | Položka | Stav | Důvod |
|---|---|---|---|
| W2-1 | Agenda registry → Caché | ✅ Hotovo | Auto-discovery z `BaseAgenda` subclasses (commit `de0f872`) |
| W2-2 | Section templates → Caché | ⛔ Skipnuto | Funkčně překryto Wave 3 Template starters (Open + Save As pattern). Bez user requestu. Existující file-based zůstává jako side-feature. |
| W2-3 | Element defaults → Caché | ⛔ Skipnuto | Hardcoded v C# je rozumné; Wave 4 InputGrid persist do Template JSON pokrývá use case |
| W2-4 | Company palette → Caché | ⛔ Skipnuto | Wave 1.5 RecentColors pokrývá; brand palette není user request |

### W2-1 Agenda registry — auto-discovery design

**Princip:** „Třída JE registrace". Eliminuje drift mezi registrem a impl (vs JSON config list).

**Caché:**
- `BaseAgenda.cls` — 2 class parametry (`AgendaCode`, `AgendaLabel`); konvence `<TYP_SESTAVY>_<DATOVY_ZDROJ>`
- `ZastavkoveJrMhd.cls` — `AgendaCode = "ZASTAVKOVY_JR_MHD"` (přejmenováno z `"MHD"` — MHD je datový modul, ne typ sestavy)
- `Faktury.cls` — `AgendaCode = "FAKTURY"`
- NEW `API/AgendyRegistr.cls` — `ListAgendy` přes `%Dictionary` SQL nad subclasses BaseAgenda, vrátí JSON pole `[{code, label, cacheClass}, ...]`

**.NET:**
- NEW `Serialization/IAgendaRegistry.cs` — interface + `AgendaRegistryEntry` record
- NEW `Serialization/CacheAgendaRegistry.cs` — impl s Task.Run wrap (R1), synteticky prepend GENERAL agendy, fallback při chybě jen GENERAL
- `Config/CacheSchemaWiring.cs` — `TryCreate(MLRest, IReadOnlyList<AgendaRegistryEntry>)`
- `Config/AppSettings.cs` — odstraněna `Agendas` property + `SchemaAgendaSettings` třída
- `App.xaml.cs` — sync load agendy ze Caché s 3s timeoutem
- `MainWindow(MLRest?, prefs?, agendas?)` — 3. ctor param
- `templatedesigner.appsettings.json` — smazáno `agendas[]`

**Architektonická rozhodnutí:**
- Per-customer kontrola odložena (YAGNI — všichni zákazníci mají stejný kód)
- AgendaOrder parameter neřešen — alfabeticky podle code
- AgendaEnabled neřešeno — pre-release work řešen git branches
- Naming `<TYP_SESTAVY>_<DATOVY_ZDROJ>` — budoucí JŘ: `KNIZNI_JR_MHD`, `MEZIMESTSKY_JR_MHD`, `VOZOVY_JR_MHD`

**Commits:** `7d6704a` (docs scope reduction), `de0f872` (impl).

---

## Wave 1 + 1.5 — Per-user state přes Caché ✅ (2026-05-20)

První dvě dávky Caché Migration — persistence per-user × IDFa state v `^ALVA.EDControls.UserData` přes `IUserPrefsService`. Žádné lokální JSON soubory (TD běží embedded z EDISONMAIN2). Detail dokumentace, registr scope ownerships a 3-typová taxonomie state v [`EDControls/DOCS/05-userdata-scopes.md`](../../../EDControls/DOCS/05-userdata-scopes.md).

**Wave 1 (foundation + UX integrace):**

| Krok | Co | Commit |
|---|---|---|
| W1-0 | `IUserPrefsService` foundation + Caché `ALVA.EDControls.UserData` (generic blob storage per IdFa × userID × scope × key) | F1 (2026-05-15) |
| W1-1 | `TdPreferencesService` wrapper + `MainStatePref` typed record (Typ 1 — UI state, jeden blob) | 7cd39ab |
| W1-2 | Wire-up: `App.xaml.cs` async load + `TdPreferencesService.Loaded` event fallback pro cold-start | 7cd39ab |
| W1-3 | `MainWindowViewModel.ApplyLoadedPreferences` — Zoom/Grid/Snap/SnapGapMm/ShowGrid/ShowSectionHeaders/ShowRuler | 7cd39ab |
| W1-3b | UI-side prefs — splittery (LeftPanelWidthPx, PreviewPanelWidthPx), ActiveLeftTabIndex, ShowPreviewPane | 7cd39ab |
| W1-4 | Recent Files menu (Soubor → Otevřít poslední) + AddRecentFile při Open/SaveAs, missing-file graceful (Typ 2 — activity log) | 7cd39ab |
| W1-6 | Instance defaults per šablona (scope `"TemplateDesigner.InstanceDefaults"`, key = template Guid, Typ 3 — per entity) | 7cd39ab |

**Wave 1.5 (Recent Colors + PrintParams):**

| Krok | Co | Commit |
|---|---|---|
| W1.5-1 | Recent Colors — 8 last-used barev v color pickeru (`"TemplateDesigner"` / `"RecentColors"`, Typ 2). DRY refactor `ColorPickerHelper` jako single entry point — odstranění duplicitního WinForms ColorDialog kódu z `PropertiesPanel` | 06eeda2 |
| W1.5-2 | PrintDialog params per-agenda persist + live sync s preview lištou (`"TemplateDesigner.PrintParams"` / `<agendaCode>`, Typ 3 per agenda). Sdílený dict + cross-notification callback. Crash fix (odstranění sync `.Result` blocking UI thread) + re-entrance guard pro rychlé Ctrl+P. | 06eeda2 |
| W1.5-3 | PropertiesPanel group expanded state | ⛔ Odloženo do Wave 4 (EDInputForm migrace nahradí PropertiesPanel) |
| W1.5-4 | Font override (analogie EDISONMAIN2 LocalPrefs.FontFamily/FontSize) | ⛔ Odloženo (žádný user request) |

**Doc commity:**

| Commit | Obsah |
|---|---|
| ec654e4 | `EDControls/DOCS/05-userdata-scopes.md` — registr UserData scopes |
| ced96fa | 3 typy state taxonomy + Wave 1.5 priorities |

**Klíčová architektonická rozhodnutí:**
- **Žádné lokální JSON soubory** pro persistent state (TD běží embedded z EDISONMAIN2)
- **Async-first pattern** — žádné sync `.Result` block na UI threadu (poučení z crash fixu)
- **Per-agenda granularita pro PrintParams** — nová šablona stejné agendy zdědí poslední hodnoty
- **Sdílený dict + cross-notification** — single source of truth pro params mezi PreviewLišta a PrintDialog
- **3-typová taxonomie state** dokumentována jako závazná pro budoucí konzumenty (Typ 1 UI state, Typ 2 activity log, Typ 3 per entity)
- **Schema evolution backward-compat** — všechna pole records mají defaults v primary ctoru
- **DRY ColorPickerHelper** — jediný entry point pro color picker napříč TD

---

## v1.0 demo balíček ✅ (2026-05-15)

Tři commity tvoří kompletní release readiness pro testovací nasazení — zákazníkovi lze ukázat funkční end-to-end workflow pro zastávkový JŘ MHD. Detaily viz [`changelog.md`](changelog.md) 2026-05-15 entry.

| # | Co | Commit |
|---|---|---|
| R1 | **Agenda + Linka picker UX** — `ChangeAgendaDialog` se 3 entry pointy (menu / Ctrl+Shift+A / klik na status chip); `InstancePickerDialog` (multi-výběr linek, search, batch); Caché `GetAvailableInstances` iteruje skupinu linek; permanentní status indikátor (zelený agenda chip + modrý instance chip s ◀▶ + window title `[MHD]` prefix) | `82b68be` + `fd25008` |
| R2 | **Template storage v Caché** — třída `ALVA.TemplateDesigner.API.Templates` (5 metod, per-IDFa, JSON wire); `CacheTemplateService` async wrapper; `CacheTemplatePickerDialog` (správce: list + filtr + search + Open/Rename/Delete); `SaveToCacheDialog`; menu „Soubor → Otevřít z Caché... / Uložit do Caché [jako...]" + „Nástroje → Šablony v Caché..."; Caché namespace refactor `API/Agendas/Forms`; GENERAL agenda fallback | `5ac9dd0` |
| R4 | **Polish** — status bar chips, ConfirmUnsavedChanges integrace, error reporting přes IPopupDisplayer, BuildAllPreviewDataPages bugfix pro šablony bez agendy, debug logging do `^jm` (po debugu odstraněno) | průběžně |

**Performance:** 600 stránek MHD JŘ za ~9s (5.2s fetch + 3.6s render, ~15ms/stránka) — top decile vs commercial reporting (Crystal Reports 30-60s, SSRS 20-45s, JasperReports 15-30s).

**Architektonická pravidla R1–R5 (async/IPopupDisplayer/JSON/MVVM/versioning) dodržena ve všech 3 commitech.**

## Fáze A.2: Zákaznické úpravy zastávkového JŘ ✅ (2026-05-11 → 2026-05-12)

Suspended tickety od DPKV (#10660) a TRANSDEV (#7937, #7959, #9036) z helpdesku. Detail viz [`customer-requirements-zastavkovy-jr.md §G`](customer-requirements-zastavkovy-jr.md).

| Krok | Tiket | Co | Datum |
|---|---|---|---|
| A2-A1 | #10660 | StopList: `DirectionArrowPosition` (Left/Right asymetricky) | 2026-05-12 |
| A2-A2 | #10660 | StopList: `MarkersPosition` (Before/After name) + auto-fit marker sub-cell | 2026-05-12 |
| A2-A3 | #10660 | StopList: `CurrentStopBgColor` + odstranění master toggle `HighlightCurrentStop` | 2026-05-12 |
| A2-A4 | #10660 | StopList: per-column header font (verifikace, již plně podporováno) | 2026-05-12 |
| A2-B1 | #10660 | DepartureGrid: per-line font v dvouřádkovém headeru (`HeaderLine2Style`) | 2026-05-12 |
| A2-B2 | #10660 | DepartureGrid: fix nestejného podtržení (`DrawTextDecorations` na header) | 2026-05-12 |
| A2-B3 | #10660 + #9036 | DepartureGrid: časová osa přes půlnoc + extended hours + „Roztáhnout na výšku" | 2026-05-12 |
| A2-C1 | #10660 | NotesLegend: DB-driven barvy poznámek (`PoznamkaSpoj.BarvaZnakuRGB/BarvaPozadiRGB`) | 2026-05-12 |
| A2-C2 | #10660 | NotesLegend: konfigurovatelná hlavička (verifikace, již podporováno) | 2026-05-12 |
| A2-E1 | #8772 | VisibilityExpression placeholdery — verifikace funkčnosti | 2026-05-11 |
| A2-A5 | #7937 | ⏸️ StopList per-row indent — odloženo (chybí Caché data flow, mimo TD scope) | — |
| A2-A6 | #7959 | 🔍 Filtr neobsluhovaných zastávek — již implementováno v Caché, vyžaduje verifikaci | — |
| A2-B4 | #8828 | ⏸️ Per-sloupek zastávky — odloženo (Caché datový průzkum + business decision) | — |

## Fáze B0.5 — Code Hygiene Sprint

| # | Co | Hotovo |
|---|---|---|
| H-1 | Smazat `ShowZkraceneOznaceniLinky` — ghost property v `DepartureGridElement` (nikde se nečetla) | 2026-04-21 |
| H-2 | `async void` → `async Task` + `AsyncRelayCommand` (EDControls) + `CommandAsync` helper; Open/Save/SaveAs + bonus GenerateCommand | 2026-04-21 |
| H-3 | `ElementTypes` statické konstanty v `Models/ElementTypes.cs` — nahrazeno ~30 magic strings v JsonDerivedType, `CreateElementByType`, `GetDefaultElementSize` a toolbar menu | 2026-04-21 |
| H-4 | JSON wire format verify — průzkum potvrdil 100% čistý JSON (Caché `obj.%ToJSON()` → .NET `JsonNode.Parse`); opraven zastaralý docstring `GetData` v `AgendaAPI.cls` + `cache-api.md` + přidána sekce "Wire formát pravidlo (R3)" | 2026-04-21 |
| H-5 | Silent catch v `PropertiesPanel.xaml.cs` color picker seed nahrazen za `SKColor.TryParse` — odstranil try/catch jako control flow | 2026-04-21 |
| H-6 | Nový `.claude/rules/new-element-checklist.md` — 10-kroková autoritativní reference (třída, ElementTypes, JsonDerivedType, 2 switches, toolbar menu, paleta, dynamická výška, GetSummary, docs); konsolidace z `mvvm-patterns.md` | 2026-04-21 |
| H-7 | `MessageBox.Show` → `IPopupDisplayer` — 5 VM (AiTemplateAssistant, TableEditor, ElementProperties, MainWindow, PdfImport); PdfImport odstranil 3 callback properties; YesNoCancel kept s TODO (IPopupDisplayer nemá ekvivalent); Warning → Info (WPF rozdíl byl jen ikona) | 2026-04-21 |
| H-8 | Extract `Controls/TemplateTreePanel/` — ~150 řádků ze `MainWindow.xaml.cs` přesunuto do samostatného UserControlu; `MainWindow` jen volá `TreePanel.Rebuild()`; smazán orphan `Windows/TemplateTreeDialog/` (nikdy se neinstanciovalo) | 2026-04-21 |
| H-9 | Extract `Windows/SaveSectionTemplateDialog/` — inline WPF konstrukce dialogu (~60 řádků StackPanel+TextBox+Button) z `MainWindowViewModel` přesunuta do Window+ViewModel+XAML dle závazného standardu; `SaveSectionTemplate(Section, string)` nyní čistá save logika | 2026-04-21 |
| H-10 | **N/A** — `TemplatePickerDialog` v kódu neexistoval. Template picker = `OpenFileDialog` (standardní Windows), section template picker = menu položky v "Add Section" dropdown. Žádný samostatný dialog k extrakci. Uzavřeno bez změn. | 2026-04-21 |
| H-11 | ⏭️ **Skip do potřeby** — `PropertyDescriptor.VisibleWhen` predikát. Aktuální ad-hoc skrývání v dialozích stačí, žádný blocker. | 2026-04-30 |
| H-12 | ⏭️ **Skip do potřeby** — `SharedPropertyDescriptors` helper. Duplikace deskriptorů akceptovatelná za stávající cenu. | 2026-04-30 |
| H-13 | ⏭️ **Skip do potřeby** — rozšíření error flow nad rámec `IPopupDisplayer`. Strukturované logování / telemetrie přidat až bude konkrétní požadavek. | 2026-04-30 |

## Sjednocení patternů + UX vyladění ✅ (2026-04-30 → 2026-05-01)

Kontinuální polish nad rámec původního B0.5 scope, motivovaný přípravou na 1:1 ladění zastávkového JŘ:

| Co | Stručný popis |
|---|---|
| **Sjednocené Header/Data zóny v panelu** | Princip „rychlý přístup × kompletní cascade" napříč StopList/DepartureGrid/NotesLegend/TableElement; helper `GetCellZoneBaseColorDescriptors()` |
| **Per-template editor preferences** | `Template.EditorPreferences` (grid + snap) — designové prostředí přežije reload |
| **Click-vs-drag threshold** | Klik na element nespustí soft-snap (4px threshold) |
| **ImageElement ScaleMode** | Renderer honoruje Fit/Fill/Stretch/None + tlačítko „Přizpůsobit poměru obrázku" |
| **Sub-pixelové rámečky** | Adaptive `RenderBorder` — pod 1px AA fractional, nad 1px pixel-snap |
| **Descriptor sort by group** | Stable sort v PropertiesPanel — bez duplicitních hlaviček |
| **Border v „Obecné" group** | Konzistentní rychlý přístup k základním vlastnostem |
| **DataRowHeight hoist** | Z duplicitních deklarací do `ScheduleElementBase` |
| **Úklid mrtvého UI** | Smazán `LineSpacing` z `CellStyleEditor` (CellRenderer ho ignoroval) |
| **Dokumentace stylingových patternů** | `mvvm-patterns.md` — Pattern 1 (single-zone) vs Pattern 2 (dual-zone) |

## Specializované JŘ elementy — společný základ ✅ (2026-03-18)

| Krok | Co |
|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ (indexy, grouping, společné properties) |
| 2 | `StopListElement` + `DepartureGridElement` + `NotesLegendElement` — zastávkový JŘ (MHD) |
| 3 | Caché `ZastavkoveJrMhdAPI` — `GetData`/`GetMockData` + wire formát (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY) |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementů |
| 5 | `PrintDialog` — parametry + export reálných dat z Caché (`GetData`) + multi-data PDF |
| 6 | `CacheAgendaProvider` — ParseMockData vnořená pole, GetData, BuildRealData |

## Fáze A: Odladění MHD zastávkového JŘ ✅ (2026-05-11)

Cíl: hotová první kompletní verze šablony zastávkového JŘ a její review s kolegy.

> **Detailní architektonický dokument:** [`schedule-elements-architecture.md`](schedule-elements-architecture.md)

| Krok | Co | Stav |
|---|---|---|
| A | Hotová 1:1 šablona zastávkového JŘ + review s kolegy → seznam úprav v `customer-requirements-zastavkovy-jr.md` (= Fáze A.2) | ✅ (2026-05-11) |

### Poučení z implementace zastávkového JŘ — co vzít v potaz pro další JŘ typy

| Téma | Popis |
|---|---|
| **Jeden element → tři elementy** | Původní plán počítal s jedním `StopScheduleElement`. Ve skutečnosti se rozpadl na 3 nezávislé elementy — lépe odpovídá filozofii designeru (uživatel skládá ze stavebních bloků). Počítat s tím od začátku u dalších JŘ typů. |
| **Synchronní GetData** | `GetData` běží synchronně na UI thread — pro malé linky OK, pro velké může zamrznout UI. Vozový JŘ bude mít víc dat → async loading s progress barem bude nutný. Viz pravidlo R1. |
| **SKTypeface/SKPaint alokace** | ✅ Opraveno — `ScheduleElementBase` cachuje `SKTypeface` přes statický `ConcurrentDictionary<string, SKTypeface>`; `SKPaint` se vytváří a ukládá per instance cache. |
| **Caché business logika je rozsáhlá** | ZastavkoveJrMhdAPI má ~1200 řádků. Počítat s tím v odhadu pro další JŘ typy. |
| **Definovat layout před implementací** | Aby se neopakovala situace, kde se plán změní v průběhu. |

### Fáze A — hotové kroky

| Krok | Co | Stav |
|---|---|---|
| 7 | Per-column styling framework — CellStyle/ColumnVisualStyle, dva taby v DepartureGroupEditorDialog, InlineTextStyle pro minuty/anotace | ✅ (2026-03-21) |
| 7a | StopListElement — per-column styling analogicky k DepartureGrid (CellStyle pro záhlaví/data sloupců zastávek) + generalizace shared helpers do ScheduleElementBase + ColorPickerHelper + ActionId routing | ✅ (2026-03-23) |
| 7b | Ověření parametrů šablony — funguje přepsání element properties z template parameters (CASOVA_OSA, HODINY_OD, HODINY_DO, TISK_PROJETYCH_ZAST, TISK_SIPKY); fix merge parametrů do real data exportu; auto-open PDF; odebrání BAREVNE_ODLISENI + TISK_TARIFU | ✅ (2026-03-23) |
| 8a | Sjednocení textových nastavení — `TextStyle` (nullable POCO, CSS cascade) + `ResolvedTextStyle` (SkiaSharp helpery). Migrace: TextElement, TableElement, ScheduleElementBase + 3 JŘ elementy, PdfImport, CacheAgendaProvider. Breaking JSON change (flat props → `style`/`headerStyle`/`dataStyle`) | ✅ (2026-03-24) |
| 8b | Proporcionální škálování šablony + UX vylepšení — `ScaleTemplateCommand`, `ApplyScale()` na všech elementech, redistribuce výšek sekcí, výchozí nastavení nových elementů, `HourColumnPerGroup` | ✅ (2026-03-24) |
| 8c | AI-enhanced PDF import — dvoustupňový pipeline (heuristiky → AI enrichment); `AiImportEnricher` s kontextovými prompty; detekce rámečků, loga placeholder, margin fix, TextElement alignment fix | ✅ (2026-03-25) |
| 8d | AI Vision import pipeline — `AiVisionImportService` (composable prompty), `AiGuidedScheduleBuilder` (PdfPig geometrie), `AiVisionImportPipeline` (orchestrace); per-column font wrapping; section headers auto-hide; CellStyleVM font defaults; shared HttpClient | ✅ (2026-04-03) |

## Fáze B0 — UX Sprint ✅ (2026-04-20)

| # | Co |
|---|---|
| B0-1 | Strikethrough na TextStyle + descriptor; oprava tichého nefungování Underline |
| B0-2 | Více barcode typů — EAN-13, EAN-8, PDF417, DataMatrix; konsolidace palety |
| B0-4 | Precision fix — `MmMath.Round(0.1mm)`; resize end, ScaleTemplate, DistributeH/V, properties panel |
| B0-5 | DistributeH/V: přesný gap — zaokrouhlit, poslední element absorbuje zbytek |
| B0-6 | Normalizovat pozice (Ctrl+Shift+N) — dialog s krokem, live preview, CompositeCommand undo |
| B0-8 | Properties panel float formát — `"0.0#"` pro mm pole, round on commit |
| B0-9 | Grid size v UI — ComboBox 1/2/5/10mm v toolbaru, DesignerState.GridSize property |
| B0-11 | Inline validace — červený rámeček + tooltip místo textu pod polem |
| B0-12 | Copy Style / Paste Style (Ctrl+Shift+C/V) — StyleClipboard, kopíruje TextStyle + BorderColor/Width + BackgroundColor |
| B0-13 | Kontextový format mini-toolbar — B/I/U/S + L/C/R/J + ▲/▬/▼ nad properties panelem |
| B0-14 | Rotation property na TemplateElement — float, stupně, JSON serializace |
| B0-15 | Render rotace — canvas.Save/RotateDegrees(cx,cy)/Restore + IsRotated bool helper |
| B0-16 | Hit-testing rotovaných elementů — inverzní rotace test pointu kolem středu |
| B0-17 | Resize handles zakázány pro rotované elementy + rotation badge (angle label) |

---

## Další implementované oblasti

**UX audit — bezpečnost + frustrace + polish** ✅ (2026-03-18)
- Sanitizace VisibilityExpression (whitelist tokenizer + live validace v UI)
- Canvas fixed height + section clamping (sekce nepřesáhnou stránku)
- SectionRole (Header/Body/Footer) — patičky od spodu stránky
- Ctrl+X, Ctrl+0, context menu na canvasu, bound resize, potvrzení mazání sekce
- Nahrávání obrázků s auto-downscale (max 5 MB), SelectAll

**Zastávkový JŘ + Caché integrace** ✅ (2026-03-18)
- ScheduleElementBase + 3 elementy (StopListElement, DepartureGridElement, NotesLegendElement)
- ZastavkoveJrMhdAPI (~1200 řádků business logiky z legacy refaktoru, sloučeno z AgendaJRAPI + TiskMHD + TemplateDesignerMHD)
- CacheAgendaProvider: ParseMockData vnořená pole, GetData, BuildRealData
- PrintDialog: parametry + export reálných dat + multi-data PDF
- SkiaHeightCalculator: schedule element height calculation

**PageLayoutEngine + multi-page** ✅ (2026-03-17)
- Template + DataContext → `List<Page>` s PlacedSections; čistá logika (vrstva 3), žádný SkiaSharp/WPF
- Klasifikace sekcí (headers/footers/body), stránkování (KeepTogether/SplitAcrossPages), dvouprůchodový layout pro OnLastPageOnly
- Flow layout uvnitř sekcí, PerDataRow expanze, PAGE/TOTAL_PAGES injekce
- Multi-page preview (stránky pod sebou) + multi-page PDF export
- IElementHeightCalculator: SkiaHeightCalculator měří TextElement (word wrap) a TableElement

**Flow text v designeru (Word wrap + dynamická výška)** ✅ (2026-03-17)
- TextElement: `WordWrap=true` + `Height=null` → text se zalamuje, výška se dopočítá z obsahu
- `LineSpacing` property, vertikální zarovnání víceřádkového textu, fix `\r\n`
- InfiniteTextElement jako samostatný typ zamítnut — jedno chování řízené properties

**ElementGroup — seskupování elementů** ✅ (2026-03-12)
- `GroupId` property na `TemplateElement` — lehká logická vazba bez nested souřadnic
- Kliknutí na člen skupiny vybere celou skupinu; Alt+klik = jen ten jeden element
- Ctrl+G / Ctrl+Shift+G — seskupit / rozeskupit (plný undo/redo)
- Vizuální indikace: barevný badge v rohu, bounding box skupiny při výběru
- Strom šablony zobrazuje skupiny jako sbalitelné uzly
- Copy/Paste/Duplicate přemapují GroupId (klony nedědí skupinu originálu)
- PDF import automaticky seskupí grafiku podle typu a merge mode

**Locale / CultureInfo v šabloně** ✅ (2026-03-12)
- `Template.CultureCode` — kultura pro formátování čísel a datumů (cs-CZ, sk-SK, en-US, de-DE)
- Protéká celým pipeline: Template → DataContext.Culture → PlaceholderFormatter → TableElement
- Dropdown ve vlastnostech stránky; null = cs-CZ (výchozí); parsování vstupů zůstává InvariantCulture

**Drag & drop při přidávání elementů a sekcí** ✅ (2026-03-12)
- Drag element z toolbaru/palety na canvas — ghost preview, drop target highlight, clamp + snap
- Drag pole z panelu "Pole agendy" na canvas — vytvoří TextElement nebo append k existujícímu
- Drag & drop sekcí v levém panelu — přeřazení tažením, vizuální indikátor, `ReorderSectionCommand`
- Drag element mezi sekcemi — záměrně odloženo (nízká priorita, aktuální UX postačuje)

**Chytré duplikování elementů** ✅ (2026-03-12)
- Ctrl+D → šipka = směrové duplikování (kopie vedle originálu s 4mm mezerou)
- Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Multi-select, clamp na bounds sekce, plný undo/redo

**Přichytávání elementů k sobě (smart snap)** ✅ (2026-03-12)
- Edge↔Edge (4 kombinace), Center↔Center, Gap snap (4mm mezera) — bez kategorické priority, vyhraje nejbližší match
- Zvýraznění cílových elementů (šedý dashed rámeček), gap indikátor (černá čárka v mezeře)
- Element snap má přednost před grid snapem; osy X/Y nezávisle
- Toggle v toolbaru + menu; výchozí zapnuto

**Výrazový jazyk** ✅ (2026-03-12)
- Part 1 — podmínky a logické operátory: `{TISK_SIPKY}`, `{POCET} > 0`, `{TYP} == "PD"`, `&&`, `||`, `!`, závorky; VisibilityExpression na elementech a sekcích; ExpressionEvaluator + Parser + AST nodes
- Part 2a — agregace: `SUM({CENA})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})` v TableElement; SummaryRows; AggregationResolver; záložka Souhrn v TableEditorDialog
- Part 2b — formátování placeholderů: `{POLE:N2}`, `{POLE:dd.MM.yyyy}` (.NET formáty); PlaceholderFormatter + DataContext rozšíření
- Part 2c — řetězcové funkce: `{POLE:LEFT(4)}`, `{POLE:UPPER}`, `{POLE:PADLEFT(3,'0')}`, `{POLE:TRIM}`

**UX polish — hotové položky**
- **f. Side panel vlastností** ✅ základ (2026-03-21) — inline záložka "Vlastnosti" v levém panelu; modální dialog zachován jako fallback
- **h. Ikonový toolbar** ✅ (2026-03-21) — 33 Fluent UI ikon, 7 skupin, ToolbarStyles.xaml

---

## 🖨️ PageLayoutEngine + stránkovaný preview (detail) ✅ (2026-03-17)

Preview v designeru = plný engine. "Tisk" = ten samý engine s reálnými daty → PDF.
Architektonické rozhodnutí (2026-03-17): engine je **integrovaný** v TemplateDesigneru, ne samostatný projekt. Oddělení do samostatných .csproj se udělá až bude druhý konzument.

### Vrstvy systému

Viz diagram v `CLAUDE.md` (sekce "Architektura — vrstvy systému").

### PageLayoutEngine — vstup/výstup

```csharp
class PageLayoutEngine {
    LayoutResult Layout(Template template, DataContext? data);
}
class LayoutResult { List<Page> Pages; int TotalPages; }
class Page { int PageNumber; List<PlacedSection> Sections; }
class PlacedSection {
    Section Source;        // odkaz na originální sekci
    float Y, Height;      // absolutní pozice na stránce (mm)
    float? ClipTop, ClipBottom;  // ořez pro SplitAcrossPages
    DataContext? Data;     // obohacená o PAGE/TOTAL_PAGES
}
```

### Implementační kroky ✅ vše hotovo

| Krok | Co | Stav |
|---|---|---|
| 1 | `PageLayoutEngine` — section ordering + repeating (OnEveryPage atd.) | ✅ |
| 2 | Stránkovaný preview — multi-page pod sebou se stíny a rámečky | ✅ |
| 3 | Flow layout — dynamická výška elementů posouvá elementy pod nimi | ✅ |
| 4 | Page breaking — sekce SplitAcrossPages se rozdělí přes stránky (ClipTop/ClipBottom) | ✅ |
| 5 | PerDataRow expanze — sekce se opakuje pro každý řádek dat | ✅ |
| 6 | PAGE/TOTAL_PAGES injekce + VisibilityExpression per stránku | ✅ |
| 7 | Multi-page PDF export — každá stránka = jedna PDF stránka | ✅ |

### Budoucí modularizace (až bude potřeba)

```
TemplateDesigner.Core.csproj      ← vrstvy 1+2+3 (žádný SkiaSharp, žádné WPF)
TemplateDesigner.Rendering.csproj ← vrstva 4 (SkiaSharp, žádné WPF)
TemplateDesigner.csproj           ← vrstva 5 WPF Designer
Edison.PrintService.csproj        ← vrstva 5 headless tisk z EDISONu
```

### Use cases díky oddělitelnosti

| Use case | Vrstvy | Popis |
|---|---|---|
| WPF Designer + preview | 1–5 | Aktuální aplikace |
| Tisk z EDISONu | 1–4 | Print dialog → GetData → engine → PDF |
| Batch tisk | 1–4 | Noční job: 200 faktur → 200 PDF |
| PDF jako služba | 1–4 | EDISON interně: dej PDF pro fakturu #123 |
| REST API | 1–4 | HTTP POST šablona + data → PDF response |
| Web preview | 1–4 | ASP.NET endpoint → PNG/PDF → iframe |
| Email příloha | 1–4 | Auto-generování PDF a připojení k emailu |

---

## Session 2026-07-04/05 ✅ — Architektonická rozhodnutí R1–R7 vyřešena

Celá rozhodovací tabulka R1–R7 uzavřena (přesunuto z aktivní roadmapy). Plný text principů:
[`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

| # | Rozhodnutí | Výsledek |
|---|---|---|
| R1 | Sdílené řádkové jádro | ✅ **implementováno** `Rendering/TableLayout.cs` (ResolveWidths + ResolveCascade), napojeno DynamicTable/LineTable/StopList. StopColumn/TableColumn NEsjednoceny (nulové riziko šablon), DepartureGrid mimo (pivot). 180/180 testů + render. Referenční doc `tabulkove-elementy-jadro.md`, návrh `r1-sdilene-tabulkove-jadro-navrh.md`. |
| R1a | Render-loop sjednocení | ✅ **implementováno** — DynamicTable tři ruční smyčky → `ColumnRenderer.DrawRow`. Vyšší orchestrátor záměrně NE (LineTable pásy/přípoje se nevejdou, nízká hodnota). |
| R2 | Partial-fit kontrakt (svislá osa) | ✅ **kontrakt rozhodnut** `r2-partial-fit-kontrakt.md` (`IRowPaginatedElement`); implementace až s paginačním/bin-packing trackem (svislá+vodorovná C3 spolu). |
| R3 | Datový kontrakt / prezentační nápovědy | ✅ **rozhodnuto** `cache-api.md` — v1 = `FORE_COLOR`/`BACK_COLOR` + princip + evoluce jen-aditivně; širší seznam (ZVYRAZNENI…) na F1 dle RG usage. |
| R4 | Headless tisk | ✅ **rozhodnuto** → akční krok „tisk = shortcut z agendy bez návrháře přes PrintEngine fasádu"; impl s pilotem (blok A). |
| R5 | Fonty embedded v .exe | ✅ **rozhodnuto** (jde: `SKTypeface.FromStream`) → akční krok/TODO (základní Helvetica/PostScript s licenční poznámkou + JŘ Timetable symboly = C2); impl s pilotem/C2. |
| R6 | Systémové šablony s buildem | 🟡 rozhodnuto (read-only s releasem, DB jen zákaznické); impl v bloku B. **Navázaný otevřený bod:** koncept centrální správy šablon napříč agendami (viz aktivní kroky). |
| R7 | Pilot-first | obecný track = spustit pilot k zákazníkovi; žije v „Aktivní / nejbližší kroky", ne architektonické rozhodnutí. |

---

## Session 2026-07-05 ✅ — Tabulkové featury A1–A3 + klasifikační pravidlo

Rychlé samostatné položky nad čerstvým sdíleným tabulkovým jádrem (R1). Vše review + testy + Caché
schema. Commity: `ce2fffc`, `3d279c5`, `c1b878d`, `0402c39`, `9313105`, `be873c6`, `d0caccd`.

| # | Featura | Výsledek |
|---|---|---|
| A1 | Podmíněné řádkové featury | ✅ **viditelnost řádku** (`RowVisibilityExpression`) + **zvýraznění řádku** (`RowHighlightExpression`+`RowHighlightColor`, jen pozadí) přes sdílený `Models/Expressions/RowConditions` (výraz nad daty řádku, fail-open). Zvýraznění na **DynamicTable + StopList + LineTable**, viditelnost DynamicTable+StopList+LineTable. DepartureGrid mimo (pivot). Aditivní/default-off = behavior-preserving pro gold StopList i pilot LineTable. Řádkový POCO nese `SourceRow`. |
| A2 | Počítaná pole | ✅ **aritmetika ve výrazovém jádru** (`+ - * /`, unární minus, priorita, závorky; `ExpressionEvaluator.EvaluateToString` — číslo invariantně, `FormatNumber` odstraní IEEE-754 artefakty + NaN/Inf→prázdno). **`TableColumn.Expression`** = počítaný sloupec DynamicTable (`{CENA}*{MNOZSTVI}`, přebíjí Field, formátovatelný). Bonus fix: editor sloupců dřív `Format` neukládal. |
| A3 | Min/max řádků | ✅ **`MinRows`** doplní prázdné řádky do minima (pevná výška boxu — faktura), **`MaxRows`** ořízne. DynamicTable-only. Render 3 typy řádků (placeholder/data/padding), výška↔render parita, kombinuje s filtrem viditelnosti. |
| — | Klasifikační pravidlo | 📐 `tabulkove-elementy-jadro.md §0b` — 3 kategorie featur (nad daty řádku → sdílet / struktura sloupce+počet řádků → DynamicTable / doménové render → element-specifické). Generické featury pro data dostupné přes DynamicTable na tutéž kolekci. NEslučovat StopColumn/TableColumn. |

**Vědomě odloženo (ne dluh):**
- **Rozšíření podmíněného formátování** (seznam pravidel místo 1, barva textu, per-sloupec/buňka) →
  až dle reálné produkce (rozhodnutí uživatele 2026-07-05: „ted bych to nechal takto").
- **A4 sloučené buňky** (`ColSpan`/`RowSpan`) — větší layout kus, zůstává v aktivní Fázi B.
- **Grouping** (`GroupHeader`/`GroupFooter`) + **vnořené datasource** — backlog bez customer demand.

---

## Session 2026-07-05 ✅ — B1: embedding fontů do .exe (mechanismus, R5)

Commit `fd7b267`. `FontResolver` refaktorován na sdílené cachované typefacy + auto-discovery embedded
fontů z manifest resources pod `Fonts/` (glob `<EmbeddedResource>` v .csproj → single-file .exe).
Priorita `embedded → OS → Arial → Skia default`; embedded = deterministický výstup nezávislý na
nainstalovaných fontech. Varianty Bold/Italic = samostatné soubory. Všechny cesty renderu
(text/buňky/JŘ/legenda) už tečou přes `FontResolver.Resolve` → embedding platí všude. Testy 213/213
(4 nové). `dotnet/Fonts/README.md` dokumentuje konvenci + licence.

**Zbývá jen (až bude potřeba):** dropnout konkrétní `.ttf` do `Fonts/` — základní free bezpatkový
(Arimo/Liberation, NE Helvetica/Arial) a **JŘ symbolový Timetable_nove(+SK)** = C2 knižního JŘ
(vyjasnit zdroj/licenci). Mechanismus je připraven, kód se už nemění.

---

## Session 2026-07-06 ✅ — Layout: „sekce vyšší než stránka" (Slice 1+2 + fix)

Návrhové plátno už není omezené na jednu A4. Commity: `a88763a` (Slice 1), `56c1837` (Slice 2),
`12fc8d9` (fix dvojitého rámečku), `da198f3` (review fix footer WYSIWYG + mrtvý kód).

- **Slice 1 — plátno roste:** `DesignerCanvas.GetSectionOrigins` skládá sekce souvisle v pravé
  (neclampnuté) výšce; `TotalPaperHeightPx` (okraje + Σ výšek sekcí, min 1 stránka); `Render` +
  `UpdateCanvasSize` + `RequestCanvasRefresh` na rostoucí výšku; `SectionHeightPx` místo clampu,
  `GetClampedSectionHeightPx`/`GetPageBottomY` odstraněny.
- **Slice 2 — dělící čáry:** `DrawPageBreakLines` — přerušované modré čáry + „Strana N" po výšce
  tisknutelné oblasti, jen když obsah > 1 stránka. Varianta 1 (geometrické vodítko) — vědomě ne
  pixel-přesné vůči preview; přesný stránkovaný výsledek zůstává v preview/PDF.
- **Footer** kotven ke spodku rostoucího papíru (WYSIWYG s preview u 1 stránky).
- **Fix (pre-existing):** split sekce (SplitAcrossPages) kreslila rámeček elementu i na 2. straně —
  `PreviewCanvas.RenderPage` border-pass běžel bez clipu → ořezán na rámec stránky (opraveno i pro
  PDF/PNG, sdílený `RenderPage`). Regres test `SplitSectionPaginationTest`.
- Nezávislý review: bez BLOCKER/MAJOR, bez regrese jednostránkových šablon, hit-testing konzistentní.
  Testy 214/214.

**Navazuje (aktivní track):** R2 (svislé stránkování řádků — zpřesní zlomy na celé řádky) → C3 (vodorovný Strih).

---

## Session 2026-07-07 ✅ — Pre-produkční zámek struktury šablony (blok PA) + úklid roadmapy

Cíl: připravit v1 do produkce tak, aby se **nemusela měnit struktura šablony** (breaking JSON) v příští
verzi. Grounded analýza (4 průzkumy kódu) → [`produkce-pripravenost-2026-07-07.md`](produkce-pripravenost-2026-07-07.md).
Blok PA vyřešen, vše behavior-preserving (uživatel nepozná, jen zamčená struktura). Commity `23e6a90`
(analýza+checklist), `6dadb66`, `484881c`, `f1e01e9`, `cf43a56`. Testy 218 → 229.

- **PA1 — klíče sloupců → konstanty.** `StopColumn.Key` (`"Zone"`/`"StopName"`/`"Trip"`…) byly
  magic-string literály roztroušené ve switchích, ale persistují se do JSON → přejmenování by tiše
  rozbilo uložené šablony. Centralizováno do `Models/ColumnKeys.cs` (vzor `ElementTypes`) +
  `ColumnKeysTests` zamyká wire hodnoty. Klíč = interní směrovací identifikátor renderu (C#), ne
  datové pole (kontrakt Caché) → proto C#.
- **PA2 — mrtvý `ColorRules`.** Domnělé „dva modely" zvýraznění řádku: `List<ColorRule>` na
  DepartureGrid byl mrtvý kód (nikde nekonzumován) → odstraněn. Živý zůstává 1-pravidlový
  `RowHighlightExpression`, který strukturu nezamyká (multi-pravidlo lze přidat aditivně jako budoucí
  `RowColorRules`). `ColorRule` třída ponechána jako rezervovaný budoucí model.
- **PA3 — Fixed `TableElement` zmražen.** Šel z UI vytvořit jen zdánlivě (paleta/menu = dynamická),
  ale továrna měla mrtvou větev → odebrána; nově ho nic nevyrobí. Tripwire test (`ElementFactoryTests`)
  + docstring. Zachováno: třída, `JsonDerivedType` (legacy read), `TableEditorDialog` (legacy edit) →
  staré šablony se otevřou, Fixed tabulka připravená k oživení (pár řádků zpět).
- **PA4 — verzování dokumentováno.** Nález: migrátor je hotový **i otestovaný** (9 testů) — obava
  „pipeline neběžela" neopodstatněná. „Bump na 1.1.0 teď" **zamítnuto** (proti konvenci *bump jen pro
  breaking change*; navíc falešné WasMigrated). Zapsáno: historická flat→style změna jako
  nemigrovatelná + rozhodnutí NEbumpovat (`template-schema-versions.md`). Kosmetika „1.0"/„1.0.0"
  funkčně irelevantní, neřešena.
- **PA5 — logování neznámých polí = YAGNI (zavřeno).** Zvažováno `[JsonExtensionData]` + `Debug.WriteLine`.
  Zamítnuto jako okrajové: forward-compat už hlídá výjimka migrátoru, budoucí breaking změny mají
  migrace, drift chytají round-trip testy. Cena (trvalá změna 3 model tříd) > přínos. Přídavek je
  ~20 min kdykoli, když vznikne reálná potřeba.

**Úklid roadmapy 2026-07-07:** C3 (2D bin-packing) přesunut z layout tracku do Fáze C+D. Dvě
překrývající se produkční sekce sloučeny do jedné (PB provoz / PC governance). Sekce R1–R7 (dořešena
2026-07-04/05) a produkční done-items (A1+A2 launch, D1 embedded fallback, D2/E1/F1/F2/F5) přesunuty sem.

**PB triáž 2026-07-07 (produkční provoz):** release deploy (build .exe + Caché třídy) uznán jako
**standardní EDISON release proces**, ne TD-specifický úkol → bývalé D3/D4/PB-D1/PB-D2 odstraněny.
`SelfContained=false` / .NET 10 (PB3) neřešíme — runtime je prerekvizita celé suity (EDISONMAIN2 taky
`net10`). Build-vázaný-na-stroj (PB4) — kdo dělá release, má `C:\Edison`; CI je budoucí nice-to-have.
**Hotová drobná hygiena:** PB7 doc drift (CLAUDE.md .NET 9→10, PdfPig 0.1.9→0.1.13); PB6 — `ScheduleDetectorTest.cs`
už byla dávno smazaná (jen stará bin/obj artefakta), smazány mrtvé rozbité scratch soubory
`test-detector.csproj`/`test-detector-main.cs`/`RunDetectorTest.csx`; PB1 — vyprázdněn tracked
`templatedesigner.appsettings.json` (dev creds martin.hubata/MLP 167/interní IP se zapékaly do .exe),
přesunuto do gitignored `local.json`; produkce session stejně bere z launch args, standalone dev z local.json.


---

## Audit 2026-07-07 — Blokery (B1–B3) vyřešeny

Všechny tři produkční blokery z auditu hotové (přesunuto z aktivní audit sekce).

- **B1 — KV hardcode v MHD tiskové cestě.** `POPIS_DOPRAVCE` místo natvrdo „…Karlovy Vary, a.s.……."
  čte reálného dopravce linky z `ALVA.MHD.LSO` (helper `PopisDopravce`: `DruhDopravceID` → vlastní firma
  `$$gIdFa^MlJFapi` / cizí `$$gDopFa^JRapi`, defensivní). `NEJEDOU_SPOJE` napojen na generátor `TiskyFce`
  (gated flagem linky). `TITULEK`/`POZNAMKA_POD_DOPRAVCE`/`INFORMACE` (MHD nemá zdroj) odstraněny ze
  schématu i GetData (AD si je nechává). Schéma ověřeno živě. Commit `ddb1dfa`. Zbývá eyeball: reálné
  jméno dopravce na MHD firmě (defensivní → nejhůř prázdno). Součástí i M1 guard + oprava MHD picker crashe.
- **B2 — export tiše nahrazoval reálná data mock daty při selhání Caché.** `GetData` na `RestStatus.Error`
  /výjimku hází `CacheAgendaException` místo `null`; `null` = jen „legitimně žádná data". Chyba propadne
  přes export/preview/PrintDialog (žádné false-success PDF). 252/252 testů. (Řešeno v paralelní session.)
- **B3 — mrtvý secret + rotace klíče.** Bludný `dotnet/appsettings.local.json` (špatný název, TD ho nečte —
  čte prefixovaný; untracked/gitignored) smazán ručně (bez git změny). Pozor: generický `appsettings.local.json`
  je LEGIT pro EDISONMAIN2 (SettingsLoader ho čte) — nemazat tam. Rotace API klíče = na uživateli.
