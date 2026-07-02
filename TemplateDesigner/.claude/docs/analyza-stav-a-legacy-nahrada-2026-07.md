# Komplexní analýza: technický stav TD + plán náhrady legacy návrhářů

> **Datum:** 2026-07-02 · **Metoda:** hloubkové čtení kódu (ne jen docs) — nový TD
> (`dotnet/` + `CACHE/`) a legacy `C:\DEV\EDISON\` (REPORTGENERATOR, TiskMHD, TiskJR,
> TiskJRRELACE — VB.NET klienti + Caché třídy). Doplněno o existující
> [`gap-analyza-legacy-vs-novy-jr.md`](gap-analyza-legacy-vs-novy-jr.md) (TiskMHD detail)
> a [`legacy-schedule-generators.md`](legacy-schedule-generators.md).
>
> **Cíl (zadání):** plně pokrýt a nahradit REPORTGENERATOR + TiskMHD + TiskJR jedním
> novým návrhářem, kde se jednotlivé agendy jen kontextově přepnou.
>
> **⚠️ Status (2026-07-02): akční položky zamergovány do [`roadmap.md`](roadmap.md)** —
> architektonický track (T1–T6), blok A (A4 výstupní kanály), blok B (B4/B5 governance detaily),
> Fáze C+D (sloučeno, TiskJR náhrada), Fáze E (Relace), **Fáze F (REPORTGENERATOR)** a otevřené
> otázky Q1–Q6. **Aktuální stav položek = roadmap; tento dokument = evergreen referenční detail**
> (legacy inventář §3, průřezová témata §4, zdůvodnění etapizace §5) — neaktualizuje se per položka.

---

## 1. Verdikt (TL;DR)

**Nový TD je architektonicky zdravý a na správné trajektorii** — deklarované vrstvy
(Models → Expressions → PageLayoutEngine → Renderer → App) reálně drží, Caché wire
konvence (čistý JSON) se dodržují, agenda pattern (`BaseAgenda` + kontextové přepínání)
je přesně ten model, který sjednocení tří legacy systémů vyžaduje. Zastávkový JŘ je
datově 1:1 hotový — tzn. **nejtěžší část náhrady TiskMHD je za námi**.

**Tři vážné technické dluhy** je ale potřeba řešit dřív, než poroste plocha:
1. **Nula automatizovaných testů** (30k LOC C# + 5,5k Caché, vč. 1:1 reimplementace
   legacy tiskového enginu — ověřováno jen ručně proti gold oracle, neopakovatelně).
2. **Sync multi-instance export na UI threadu** (~9 s zamrzlé UI při 600 stránkách) —
   porušení vlastního pravidla R1 na nejexponovanější akci.
3. **TemplateMigrator je prázdný framework** — breaking change (TextStyle) už proběhla
   bez migrace a chybí forward-compat guard; DB je od 30.6. primární úložiště.

**Rozsah zbývající náhrady:** legacy k nahrazení ≈ **145k řádků VB + 17k Caché**
(z toho reálná doménová logika odhadem 15–25 %, zbytek Designer boilerplate a
copy-paste). TiskMHD je z ~90 % pokryt. TiskJR = jeden velký nový element + agenda.
REPORTGENERATOR = největší kus, ale ne kvůli featurám — kvůli **ekosystému 28 agend**,
které implementují jeho datový kontrakt.

**Průřezové zjištění:** všechny tři legacy systémy sdílejí stejnou „vendor vrstvu"
(M-line distribuční šablony ID ≥ 1000, GOF export/import, `AktualizujAgendu`, audit log,
právo na designer) a stejné výstupní kanály (PDF merge, e-mail, headless volání z
EDISONu). To je **jedna sdílená infrastruktura k postavení jednou** — má vyšší prioritu
než kterákoli jednotlivá agenda, protože bez ní nejde žádnou náhradu nasadit.

---

## 2. Technický stav nového TD (audit kódu, 2026-07-02)

### 2.1 Metriky

| Oblast | Rozsah |
|---|---|
| C# (bez bin/obj) | ~29 800 LOC, ~512 typů |
| XAML | ~3 560 LOC |
| Caché ObjectScript | 5 510 LOC, 24 .cls |
| Commit velocity | 396 commitů od 2026-02 (~90–120/měsíc, stabilní) |

Největší soubory: `MainWindow.xaml.cs` 2 671, `MainWindowViewModel.cs` 2 471 (třída
`TemplateDesignerViewModel` — název souboru nesedí), `DepartureGridElement.cs` 1 479,
`AiGuidedScheduleBuilder.cs` 1 170, `DesignerCanvas.cs` 1 018. Caché:
`ZastavkoveJrMhd.cls` 1 697, `ZastavkoveJrAd.cls` 1 483, zbytek < 240.

### 2.2 Co drží (silné stránky)

1. **Vrstevní hranice reálně platí** — `PageLayoutEngine` (495 LOC) je pure logic bez
   `using SkiaSharp` (grep 0 hitů), SkiaSharp jen v adaptéru `SkiaHeightCalculator`;
   renderer nezná WPF. Deklarace ≈ realita — to je předpoklad budoucího headless
   tisku (vrstvy 1–4) i server API.
2. **Caché konvence dodržené** — JSON wire všude (`%ToJSON` → `JsonNode`), žádný `$lb`
   na wire, žádná `@` indirection, malé pojmenované helpery, `$$$FillParams`.
3. **Výkonové vzory v schedule elementech** — DataContext-reference parse cache,
   statická typeface cache, layout fingerprint, pre-created painty.
4. **Jednotná stylová kaskáda** (`Rendering/CellDefaults+CellRenderer+ColumnRenderer`,
   208 LOC) + sdílený `CellStyleEditor` napříč 4 tabulkovými elementy.
5. **Command pattern + serializace** — 14 malých `IDesignerCommand`, polymorfní JSON
   přes `ElementTypes` konstanty; hygiena kódu výjimečná (0 TODO, 0 zakomentovaného
   kódu v C#).

### 2.3 Rizika a dluhy (seřazeno dle závažnosti)

| # | Riziko | Detail | Doporučení |
|---|---|---|---|
| T1 | **Žádné testy** | Žádný `TemplateDesigner.Tests`, žádný TD Caché `Tests` package; pre-push hook TD nekryje (`tools/run-cache-tests.ps1:15`). Nejlevněji testovatelné vrstvy (PageLayoutEngine, ExpressionEvaluator, TemplateMigrator, TextStyle kaskáda) jsou přesně pure-logic. Gold-oracle 1:1 ověření není opakovatelné. | Založit xUnit projekt: PageLayoutEngine (stránkování, patičky, split), ExpressionEvaluator, TemplateMigrator round-trip, TextStyle Resolve. Caché: `ALVA.TemplateDesigner.Tests` s gold snapshot testy `ZastavkoveJrAd` (JSON výstup pro 5 gold linek) + přidat do pre-push. |
| T2 | **Sync export na UI threadu** — ✅ **opraveno 2026-07-02** | Fetch + render na threadpoolu nad snapshotem šablony (`DeepClone` — editace během exportu výstup neovlivní), dvoufázový progress ve status baru, orchestrace z code-behind → VM (první řez T4). | Zbývá jen: první `GetSchema` hit sync (mitigován `_schemaCache`); cancel až na reálnou potřebu. |
| T3 | **Versioning bez obsahu** — ✅ **guardy opraveny 2026-07-02** | Forward-compat guard (verze > current → odmítnout), selhání migrace → výjimka (ne tichý `Debug.WriteLine`), sémantické porovnání verzí, ctor pro test injection. V1 v DB neexistují (ověřeno: DB storage vznikl po TextStyle breaking change). | Zbývá jen: potvrdit neexistenci starých JSON souborů z éry před 2026-03-24 (bez `version` pole); pokud se objeví → H-19 migrace + heuristika detekce. |
| T4 | **God objects** | `MainWindow.xaml.cs` (2 671, 167 členů) + `TemplateDesignerViewModel` (2 471, bez vnitřního členění). R4 porušení: `TableEditorDialogViewModel.cs:609-643` staví WPF UI ve VM; VM vytváří dialogy s `Owner=Application.Current.MainWindow` a sahá na `Clipboard`. | Postupný rozpad VM na služby (InstanceCoordinator, TemplatePersistence — shluky už de facto existují); nové featury nepřidávat do těchto dvou souborů. `TableEditorDialogViewModel` UI konstrukci přesunout do view. |
| T5 | **Živé duplikace bez pojistky** | PropertyDescriptor (C#) × EDInputForm (Caché) ~1,1k LOC mirror (akceptovaný Wave 4 dluh, ale bez driftového testu); `EDList/Instance.cls:113` hardcoded kopie agend registru; `ZastavkoveJrAd.GetMockData:1174` čte živá firemní data. | Driftový test (reflexe: property path z *Edit.cls existuje na elementu); `Instance.cls` napojit na `AgendyRegistr`; statický mock pro AD. |
| T6 | Drobnosti | `architecture.md` nezná `PdfImport/` (4,3k LOC) ani `Services/` (2,6k); `MessageBox.Show` v `MainWindow.xaml.cs:1021`; 96× `new SKPaint/SKFont` per-frame v Render() běžných elementů (schedule elementy to řeší, zbytek ne); `ScheduleDetectorTest.cs` kompilovaný do produkční assembly. | Úklid při příležitosti. |

---

## 3. Legacy inventář — co přesně nahrazujeme

### 3.1 REPORTGENERATOR — univerzální generátor sestav (primární cíl)

**Rozsah:** ~28 850 LOC VB (renderer 10 102, designer 5 410) + 6 055 LOC Caché
(12 tříd). Obsluhuje **~28 agend** (faktury, mzdy, sklady, jízdenky, pokladny, …).
Reálné nasazení: **33 000 textových elementů, 17 000 DB polí, 774 tabulek** napříč
zákazníky (`Main.cls:161-188` — usage statistika existuje, použít pro prioritizaci!).

**Model:** šablony čistě v globals `^AlvaReportGenerator("STROM", IdFa, "SESTAVY", ...)`;
strom USER × MLINE větev → agenda → oblasti (složky) → sestavy; **jazykové varianty**
(EN/DE kopie provázaná na CS zdroj); „Sada" = field set publikovaný agendou
(FIELDS/QRKODY/TABLES+COLS/GRAFY/NTEXTS), kopíruje se do sestavy a synchronizuje.
WYSIWYG designer existuje (5 410 ř., bez undo/redo, bez snapu/gridu — TD je zde lepší).

**Datový kontrakt:** hard-coded if-řetěz dispatch na 28 agend (`Data.cls:627-654`);
každá agenda implementuje `GetSeznamSad`/`GetSadu`/`GetData` s `~`-delimited tokenovým
streamem (`FIELD~`, `TABLE_START~`, `ROW_START~`, `CELL~`, `GRAF_START~`, `NTEXT~`).

**Feature highlights nad rámec TD:**
- **Tabulky řízené daty za běhu**: per-řádek zvýraznění 0–5 (každé s kompletním stylem),
  „znovu renderovat řádek na další stránce" (mezisoučty), text přes celou šířku,
  per-řádek/buňka custom barvy, **5 režimů overflow buňky** (vč. zalomení → klonování
  řádků), kombinovaná tabulka (řádek = tabulkový NEBO nadesignovaný layout —
  `stSekceSTabulkouSDesignemRadku`), datové typy sloupců (datum 5 formátů, minuty→HH:MM,
  záporné červeně, netisknout nuly), vícerádkové nadpisy.
- **Sekce**: 8 typů vč. nekonečného textu tekoucího přes stránky, pružné výšky,
  podmínky „tiskni pokud/dokud byla vytištěna sekce S1", `TisknoutAzDoluNaStranku`,
  `Odstrankovat`.
- **Elementy**: gradient+šipky na obdélníku, stínovaný text, svislý text, justify,
  checkbox (5 typů), calculated pole (binární op), grafy (bar/XY/radar/koláč — jen
  12 použití!), AZTEC (2 použití), QR.
- **Podmíněný tisk**: AND-seznam s 20 operátory (numeric/string/date) na sekci i elementu.
- **Výstupy**: PDF (+heslo, +el. podpis PDFSIGNER), JPG, **Excel** (server formátuje dle
  ColType), **e-mail s PDF**, **Epson znakový tisk** (jehličkové, pokladny/jízdenky),
  **headless CLI AUTOEXPORT=1 / AUTOPRINT=1** volané přímo z Caché agend.

**Kde je TD už dnes lepší:** výrazový jazyk (RG má jen ploché AND + binární kalkulačku),
agregace, undo/redo, snap/grid, JSON verzovatelné šablony, moderní čárové kódy,
JŘ elementy, PDF import, AI asistence.

### 3.2 TiskMHD — zastávkový JŘ (z ~90 % pokryt)

**Rozsah:** ~31 400 LOC ručního VB (render engine `_Module.vb` 16 179 — z velké části
copy-paste ~230 téměř identických metod) + 4 313 LOC Caché (6 tříd). Jednoúčelový:
**pouze zastávková vývěska**, dva datové zdroje (`VolanoZ`: JRMAIN × MHD).

**Stav náhrady:** datově 1:1 hotovo (agendy `ZastavkoveJrMhd` + `ZastavkoveJrAd`,
gold oracle ověřeno). Detailní mapovací matice → `gap-analyza-legacy-vs-novy-jr.md`.
Čerstvé čtení kódu potvrdilo známé mezery a doplnilo:

| Mezera | Legacy reference | Stav v TD |
|---|---|---|
| TrasaMin1–15 multi-varianta + Pracovní2–5 sloupce | `Struktury.vb:56-70` | roadmap P2-1 / AD-N13 (L) |
| Barevná tarifní pásma dle minut (Ceny1–6, server-side) | `Struktury.vb:356-379`, `Data.cls:84-107` | P2-2 (M, od nuly) |
| **M-line distribuce šablon** (MlineSestava ≥ 1000, GOF, `AktualizujAgendu`, audit log, právo na designer) | `Main.cls:83-158`, `Sestavy.cls` | P2-6 — **potvrzeno jako sdílená infrastruktura všech 3 systémů** |
| JPG export, e-mail s PDF, merge hromadného exportu do 1 PDF | `formTISKMHDReportDotNet.vb:944-1193` | chybí (PDF/PNG + split existuje) |
| Per-doklad výběr šablony (různé šablony v 1 print jobu) | vstup `"SestavaId~ValueId~…"` | TD = 1 šablona × N instancí |
| Vizuální detaily (trojúhelník pozadí, zaoblené rohy, per-strana bordery, obrázkový symbolový font, transparent pixel, justify) | `Struktury.vb`, `Obecne.vb:120` | P4 nízká priorita |

### 3.3 TiskJR (+ TiskJRRELACE) — výlepový/knižní JŘ linky (nepokryto)

**Rozsah:** TiskJR ~33 400 LOC VB + 3 290 Caché; TiskJRRELACE (slovenský relační fork,
copy-paste) ~49 700 LOC VB + 3 900 Caché. **Pozor:** výpočetní engine JŘ
(`mkPLinWin`/`mkPRelWin` v `ALVA.JRMAIN.MLAPI` + MAC rutiny) leží mimo a v LOC není.

**Co to je:** sloupcový JŘ celé linky — svislý seznam zastávek (Zóna/Km/Tč/Název/Šipky)
× sloupce spojů s časy; řádek platností se symboly/NZ pod čísly spojů; řádky
Vyčká/Navazuje (návaznosti); skupinová záhlaví PD/So/Ne (3 režimy dělení); poznámky
10 druhů v konfigurovatelném pořadí. `ALVA.TISKJR.Data.LoadData` je jen překladová
vrstva — **skutečný oracle je `mkPLinWin` (JRMAIN)**, ne `ALVA.TISKMHD.Data`.

**Klíčové schopnosti k náhradě:**
1. **Nový element (LineTableElement / „knižní JŘ") + agenda `KnizniJrAd`** — největší kus
   (odhadem srovnatelný se zastávkovým JŘ, který trval ~1 měsíc čistého času).
2. **Flow skládání datových paketů** — N směrů/bloků vedle sebe i pod sebe dle
   zbývajícího místa, server-řízené `ODSTRANKOVAT_PRED_TISKEM`, „dotisk poznámek
   z minulé stránky" — nová layout schopnost pro PageLayoutEngine.
3. **Auto-fit fontu na šířku se zpětným přepočtem sloupců** (legacy iteruje po 0,1 mm;
   `_Module.vb:1434-1491`). `ComputeFontScale` v TD je jen výškový. Legacy „měřicí
   řetězce" ping-pong (server→klient→server) v TD odpadá — SkiaHeightCalculator měří lokálně.
4. **Symbolový font Timetable_nove (+SK varianta)** — 66+ dopravních symbolů
   (`Main.cls:35-107` mapování, záporné kódy = composite symboly kreslené kódem).
   Nutné pro jakýkoli knižní/výlepový JŘ: embed TTF do SkiaSharp renderingu + převodní
   tabulka, nebo SVG glyph sada.
5. **Per-linka šablony a overrides** — linka v JRMAIN má přiřazenou jinou šablonu pro
   A4 a jinou pro A5 + ~17 vlastností per formát (`LoadNastaveniProTisk`). TD nemá
   koncept „výchozí šablona přiřazená instanci".

**TiskJRRELACE** = třetí typ výstupu (řádky = odjezdy, sloupce = linka/čas/směr/
nástupiště). Samostatné rozhodnutí — ověřit, zda je slovenský zákazník live use case
(open question v `legacy-schedule-generators.md`).

---

## 4. Průřezová témata (sdílená všemi legacy systémy)

Tohle je nejcennější poznatek analýzy: **tři legacy systémy nejsou tři nezávislé sady
featur** — sdílejí identickou infrastrukturu, kterou TD postaví jednou:

| Téma | Legacy podoba (3×) | TD stav | Priorita |
|---|---|---|---|
| **Vendor šablony + distribuce** | MlineSestava (ID ≥ 1000) vs user (< 1000); export do `.gof` v release; `AktualizujAgendu` rozkopíruje do všech IdFa; audit log `^Alva*Log`; právo „pouštět designer" | per-IDfa DB storage bez vendor vrstvy (gap P2-6, roadmap B1/B2) | 🔴 vysoká — bez ní nejde nic nasadit „jako produkt" |
| **Headless tisk z EDISONu** | EDForm volání s `PoleSestavaValue`; RG navíc CLI AUTOEXPORT/AUTOPRINT přímo z Caché agend | TD startup nečte args (roadmap blok A — A1/A2/A3) | 🔴 blocker |
| **Výstupní kanály** | PDF merge do 1 souboru (`MergePDFFiles`), e-mail s PDF přílohou, JPG; RG navíc Excel, PDF heslo+podpis, Epson | jen PDF/PNG z GUI (split-per-soubor ✅) | 🟠 střední (merge+email nejdřív — chtějí je všechny 3 systémy) |
| **Per-instance výběr šablony** | per-doklad SestavaId (TiskMHD), per-linka šablona A4/A5 + overrides (TiskJR) | 1 šablona × N instancí | 🟠 potřebné pro JŘ produkci |
| **Jazykové varianty šablon** | RG: EN/DE kopie provázané na CS zdroj, kaskádový delete/copy | jen `CultureCode` (formátování) | 🟡 až s RG agendami |
| **Symbolový font** | Timetable_nove/SK + ~100 dopravních TTF v `_ruzne/Fonty pro dopravu/` | Arial fallback; `FontResolver` tiše | 🟠 prerekvizita knižního JŘ |

---

## 5. Návrh roadmapy náhrady (doporučení)

Řazeno tak, aby každá etapa doručila samostatnou hodnotu a infrastruktura předcházela
featurám. Etapy 0–2 jsou z velké části už v roadmapě (blok A, P2-6) — tady se potvrzuje
jejich priorita čtením legacy kódu.

### Etapa 0 — Technická konsolidace (před růstem plochy; ~1–2 týdny)
- T1 testy: xUnit pro PageLayoutEngine/ExpressionEvaluator/TemplateMigrator + Caché
  gold-snapshot testy ZastavkoveJrAd → do pre-push hooku.
- T2 async export (R1), T3 migrator guardy (+ ověřit H-19), T5 driftové pojistky.
- Bez toho každá další etapa zvyšuje regresní riziko netestovaného 35k LOC systému.

### Etapa 1 — EDISON integrace + headless tisk (roadmap blok A; blocker nasazení)
- A1 session kontext (vzor `EDISONMAIN2API.FormToRunGetter`), A2 launch nad linkou,
  A3 end-user tisk. **Doplnit o RG vzor:** headless CLI režim (AUTOEXPORT/AUTOPRINT
  ekvivalent) — vrstvy 1–4 to architektonicky už umí, chybí entrypoint.
- Výstupní kanály fáze 1: **merge PDF do jednoho souboru + e-mail s PDF** (společný
  požadavek všech tří legacy systémů, malé M).

### Etapa 2 — Governance a distribuce šablon (legacy „MlineSestava" ekvivalent)
- Systémové/zamčené šablony (M-line) vs uživatelské; distribuční pipeline v release
  (ekvivalent `ExportReportToGlb`/`AktualizujAgendu` — dnes JSON, ne GOF); audit log
  změn šablon; právo na designer (ECL). Roadmap B1/B2/B3 + P2-6.
- Per-instance přiřazení šablony (linka → šablona A4/A5) — datový model
  `InstanceDefaults` už existuje (Wave 1), rozšířit o šablonu.

### Etapa 3 — Knižní/výlepový JŘ (náhrada TiskJR; roadmap Fáze C/D sloučeně)
- Legacy generuje vývěsní i knižní z téhož — **Fáze C (meziměstský) a D (knižní)
  v roadmapě jsou pravděpodobně jeden track** s oracle `mkPLinWin` (JRMAIN).
- Pořadí: (1) symbolový font Timetable do SkiaSharp + převodní tabulka
  (`ConvertSpecialniZnak` mapování už známe z obou legacy systémů);
  (2) agenda `KnizniJrAd` — čistá reimplementace à la ZastavkoveJrAd, gold oracle
  `mkPLinWin`; (3) `LineTableElement` (zastávky × spoje) + layout rozšíření
  (flow paketů, auto-fit na šířku, dotisk legendy).
- Velikost: srovnatelná se zastávkovým JŘ (≈ 1 měsíc+), ale metodika je vyzkoušená.

### Etapa 4 — REPORTGENERATOR: strategie postupné náhrady
Ne big-bang (must-not-happen #4). Doporučený postup:
1. **Prioritizovat podle usage statistiky** (`Main.cls:161-188` — reálná čísla per
   zákazník existují!). 774 tabulek a 33k textů se koncentruje v několika agendách.
2. **Rozhodnout bridge vs migrace per agenda:** buď adaptér překládající legacy
   `~`-tokenový protokol (`FIELD~`/`TABLE_START~`/…) na TD JSON DataContext — jedna
   `LegacyRgAgenda` třída by zpřístupnila všech 28 agend najednou s omezenou věrností;
   nebo čisté reimplementace vybraných agend (vzor Faktury). Pragmaticky: **bridge
   nejdřív** (rychlé pokrytí), čisté agendy tam, kde se investice vyplatí.
3. **TableElement dorovnat na RG úroveň** (navazuje na Wave 4 W4-9 Dynamic-first):
   per-řádek zvýraznění/barvy z dat, overflow módy buňky, opakování mezisoučtového
   řádku, řádek-jako-designový-layout (koncepčně PerDataRow sekce), datové typy sloupců.
   Merged cells + conditional formatting už jsou ve Fázi B roadmapy.
4. **Výstupní kanály fáze 2:** Excel export, PDF heslo/podpis, Epson znakový tisk —
   podle reálné poptávky (Epson = pokladny/jízdenky, ověřit zda vůbec migruje do TD
   nebo zůstane v legacy TISKu).
5. **Vědomě neportovat bez poptávky:** grafy (12 použití), AZTEC (2), calculated pole
   (2 — TD výrazový jazyk je nadmnožina), checkbox (8). Ověřit se zákazníky a škrtnout.

### Etapa 5 — Relace (TiskJRRELACE) + vozové JŘ (roadmap Fáze E)
- Nejdřív zodpovědět open questions (live use case Relace? priorita vozových?) —
  viz `legacy-schedule-generators.md`. Vozové JŘ mají vlastní legacy generátory
  (`ALVA.MHD.ReportGenerator`, `AD.ADREPORTGEN`) — samostatná analýza až bude aktuální.

---

## 6. Souhrnná tabulka pokrytí

| Legacy systém | Rozsah (VB + Caché LOC) | Pokrytí v TD dnes | Zbývá | Odhad |
|---|---|---|---|---|
| TiskMHD | 31 400 + 4 313 | ~90 % (datově 1:1, elementy ✅) | TrasaMin multi-varianta, tarifní pásma, vizuální detaily | M–L (jednotlivosti) |
| TiskJR | 33 400 + 3 290 | 0 % (jiný formát než zastávkový) | element + agenda + font + layout rozšíření | L (≈ zastávkový JŘ) |
| TiskJRRELACE | 49 700 + 3 900 | 0 % | rozhodnout use case; pak element + agenda | ? (čeká validace) |
| REPORTGENERATOR | 28 850 + 6 055 | ~15 % (Faktury vzor, TableElement základ) | 28 agend (bridge/migrace), tabulkové featury, kanály | XL (etapizovat) |
| **Sdílená infrastruktura** | (napříč) | částečně (DB storage, versioning framework) | vendor šablony, EDISON launch, headless, merge/email | L — **první** |

---

## 7. Otevřené otázky (k rozhodnutí s týmem)

1. **Epson znakový tisk** — migruje do TD, nebo zůstane navždy v legacy TISK? (Určuje,
   zda TD potřebuje ne-grafický výstupní kanál — architektonicky cizí prvek.)
2. **RG bridge vs čisté agendy** — akceptovatelná dočasná věrnost bridge překladu?
3. **Relace (SK zákazník)** — živý use case?
4. **Grafy/AZTEC/checkbox** — škrtnout, nebo má některý zákazník tvrdou závislost?
   (Usage čísla: 12/2/8 výskytů celkem.)
5. **Jazykové varianty šablon** — potřebné i pro JŘ (dvoujazyčné pohraniční vývěsky,
   AD-N11 cizojazyčné poznámky), nebo jen RG doména?
6. **V1 šablony v terénu** — existují? (Určuje urgenci H-19 migrace.)

---

## 8. Zdroje

- Audit TD: čtení `dotnet/**`, `CACHE/**` (2026-07-02).
- Legacy: `C:\DEV\EDISON\REPORTGENERATOR` (klient + `ALVA.REPORTGENERATOR.*` 12 tříd),
  `C:\DEV\EDISON\TiskMHD` (+ `ALVA.TISKMHD.*` 6 tříd), `C:\DEV\EDISON\TiskJR`
  (+ `ALVA.TISKJR.*` 5 tříd), `C:\DEV\EDISON\TiskJRRELACE`.
- Navazuje na: [`gap-analyza-legacy-vs-novy-jr.md`](gap-analyza-legacy-vs-novy-jr.md)
  (TiskMHD mapovací matice — evergreen), [`legacy-schedule-generators.md`](legacy-schedule-generators.md),
  [`roadmap.md`](roadmap.md) (blok A, Wave 4, Fáze B–E).
