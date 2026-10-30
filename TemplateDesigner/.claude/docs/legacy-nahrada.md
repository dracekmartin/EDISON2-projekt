# Legacy vs TemplateDesigner - náhrada tiskových modulů (reference)

> Sloučeno 2026-07-13 ze 4 dokumentů (analyza-stav-a-legacy-nahrada-2026-07,
> gap-analyza-legacy-vs-novy-jr, legacy-schedule-generators, prezentace-a-gap-2026).
> Analýzy udělané jednou - nedělat znovu; aktuální stav vždy v roadmap.md.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Technický audit + etapy náhrady legacy (2026-07) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Komplexní analýza: technický stav TD + plán náhrady legacy návrhářů

> **Datum:** 2026-07-02 · **Metoda:** hloubkové čtení kódu (ne jen docs) - nový TD
> (`dotnet/` + `CACHE/`) a legacy `C:\DEV\EDISON\` (REPORTGENERATOR, TiskMHD, TiskJR,
> TiskJRRELACE - VB.NET klienti + Caché třídy). Doplněno o existující
> [`legacy-nahrada.md`](legacy-nahrada.md) (TiskMHD detail)
> a [`legacy-nahrada.md`](legacy-nahrada.md).
>
> **Cíl (zadání):** plně pokrýt a nahradit REPORTGENERATOR + TiskMHD + TiskJR jedním
> novým návrhářem, kde se jednotlivé agendy jen kontextově přepnou.
>
> **⚠️ Status (2026-07-02): akční položky zamergovány do [`roadmap.md`](roadmap.md)** -
> architektonický track (T1–T6), blok A (A4 výstupní kanály), blok B (B4/B5 governance detaily),
> linkové a knižní JŘ (náhrada TiskJR), Relace a vozové JŘ, **report generátor** a otevřené
> otázky Q1–Q6. **Aktuální stav položek = roadmap; tento dokument = evergreen referenční detail**
> (legacy inventář §3, průřezová témata §4, zdůvodnění etapizace §5) - neaktualizuje se per položka.

---

## 1. Verdikt (TL;DR)

**Nový TD je architektonicky zdravý a na správné trajektorii** - deklarované vrstvy
(Models → Expressions → PageLayoutEngine → Renderer → App) reálně drží, Caché wire
konvence (čistý JSON) se dodržují, agenda pattern (`BaseAgenda` + kontextové přepínání)
je přesně ten model, který sjednocení tří legacy systémů vyžaduje. Zastávkový JŘ je
datově 1:1 hotový - tzn. **nejtěžší část náhrady TiskMHD je za námi**.

**Tři vážné technické dluhy** je ale potřeba řešit dřív, než poroste plocha:
1. **Nula automatizovaných testů** (30k LOC C# + 5,5k Caché, vč. 1:1 reimplementace
   legacy tiskového enginu - ověřováno jen ručně proti gold oracle, neopakovatelně).
2. **Sync multi-instance export na UI threadu** (~9 s zamrzlé UI při 600 stránkách) -
   porušení vlastního pravidla R1 na nejexponovanější akci.
3. **TemplateMigrator je prázdný framework** - breaking change (TextStyle) už proběhla
   bez migrace a chybí forward-compat guard; DB je od 30.6. primární úložiště.

**Rozsah zbývající náhrady:** legacy k nahrazení ≈ **145k řádků VB + 17k Caché**
(z toho reálná doménová logika odhadem 15–25 %, zbytek Designer boilerplate a
copy-paste). TiskMHD je z ~90 % pokryt. TiskJR = jeden velký nový element + agenda.
REPORTGENERATOR = největší kus, ale ne kvůli featurám - kvůli **ekosystému 28 agend**,
které implementují jeho datový kontrakt.

**Průřezové zjištění:** všechny tři legacy systémy sdílejí stejnou „vendor vrstvu"
(M-line distribuční šablony ID ≥ 1000, GOF export/import, `AktualizujAgendu`, audit log,
právo na designer) a stejné výstupní kanály (PDF merge, e-mail, headless volání z
EDISONu). To je **jedna sdílená infrastruktura k postavení jednou** - má vyšší prioritu
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
`TemplateDesignerViewModel` - název souboru nesedí), `DepartureGridElement.cs` 1 479,
`AiGuidedScheduleBuilder.cs` 1 170, `DesignerCanvas.cs` 1 018. Caché:
`ZastavkoveJrMhd.cls` 1 697, `ZastavkoveJrAd.cls` 1 483, zbytek < 240.

### 2.2 Co drží (silné stránky)

1. **Vrstevní hranice reálně platí** - `PageLayoutEngine` (495 LOC) je pure logic bez
   `using SkiaSharp` (grep 0 hitů), SkiaSharp jen v adaptéru `SkiaHeightCalculator`;
   renderer nezná WPF. Deklarace ≈ realita - to je předpoklad budoucího headless
   tisku (vrstvy 1–4) i server API.
2. **Caché konvence dodržené** - JSON wire všude (`%ToJSON` → `JsonNode`), žádný `$lb`
   na wire, žádná `@` indirection, malé pojmenované helpery, `$$$FillParams`.
3. **Výkonové vzory v schedule elementech** - DataContext-reference parse cache,
   statická typeface cache, layout fingerprint, pre-created painty.
4. **Jednotná stylová kaskáda** (`Rendering/CellDefaults+CellRenderer+ColumnRenderer`,
   208 LOC) + sdílený `CellStyleEditor` napříč 4 tabulkovými elementy.
5. **Command pattern + serializace** - 14 malých `IDesignerCommand`, polymorfní JSON
   přes `ElementTypes` konstanty; hygiena kódu výjimečná (0 TODO, 0 zakomentovaného
   kódu v C#).

### 2.3 Rizika a dluhy (seřazeno dle závažnosti)

| # | Riziko | Detail | Doporučení |
|---|---|---|---|
| T1 | **Žádné testy** | Žádný `TemplateDesigner.Tests`, žádný TD Caché `Tests` package; pre-push hook TD nekryje (`tools/run-cache-tests.ps1:15`). Nejlevněji testovatelné vrstvy (PageLayoutEngine, ExpressionEvaluator, TemplateMigrator, TextStyle kaskáda) jsou přesně pure-logic. Gold-oracle 1:1 ověření není opakovatelné. | Založit xUnit projekt: PageLayoutEngine (stránkování, patičky, split), ExpressionEvaluator, TemplateMigrator round-trip, TextStyle Resolve. Caché: `ALVA.TemplateDesigner.Tests` s gold snapshot testy `ZastavkoveJrAd` (JSON výstup pro 5 gold linek) + přidat do pre-push. |
| T2 | **Sync export na UI threadu** - ✅ **opraveno 2026-07-02** | Fetch + render na threadpoolu nad snapshotem šablony (`DeepClone` - editace během exportu výstup neovlivní), dvoufázový progress ve status baru, orchestrace z code-behind → VM (první řez T4). | Zbývá jen: první `GetSchema` hit sync (mitigován `_schemaCache`); cancel až na reálnou potřebu. |
| T3 | **Versioning bez obsahu** - ✅ **guardy opraveny 2026-07-02** | Forward-compat guard (verze > current → odmítnout), selhání migrace → výjimka (ne tichý `Debug.WriteLine`), sémantické porovnání verzí, ctor pro test injection. V1 v DB neexistují (ověřeno: DB storage vznikl po TextStyle breaking change). | Zbývá jen: potvrdit neexistenci starých JSON souborů z éry před 2026-03-24 (bez `version` pole); pokud se objeví → H-19 migrace + heuristika detekce. |
| T4 | **God objects** | `MainWindow.xaml.cs` (2 671, 167 členů) + `TemplateDesignerViewModel` (2 471, bez vnitřního členění). R4 porušení: `TableEditorDialogViewModel.cs:609-643` staví WPF UI ve VM; VM vytváří dialogy s `Owner=Application.Current.MainWindow` a sahá na `Clipboard`. | Postupný rozpad VM na služby (InstanceCoordinator, TemplatePersistence - shluky už de facto existují); nové featury nepřidávat do těchto dvou souborů. `TableEditorDialogViewModel` UI konstrukci přesunout do view. |
| T5 | **Živé duplikace bez pojistky** | PropertyDescriptor (C#) × EDInputForm (Caché) ~1,1k LOC mirror (vědomě akceptovaný dluh, ale bez driftového testu); `EDList/Instance.cls:113` hardcoded kopie agend registru; `ZastavkoveJrAd.GetMockData:1174` čte živá firemní data. | Driftový test (reflexe: property path z *Edit.cls existuje na elementu); `Instance.cls` napojit na `AgendyRegistr`; statický mock pro AD. |
| T6 | Drobnosti | `architecture.md` nezná `PdfImport/` (4,3k LOC) ani `Services/` (2,6k); `MessageBox.Show` v `MainWindow.xaml.cs:1021`; 96× `new SKPaint/SKFont` per-frame v Render() běžných elementů (schedule elementy to řeší, zbytek ne); `ScheduleDetectorTest.cs` kompilovaný do produkční assembly. | Úklid při příležitosti. |

---

## 3. Legacy inventář - co přesně nahrazujeme

### 3.1 REPORTGENERATOR - univerzální generátor sestav (primární cíl)

**Rozsah:** ~28 850 LOC VB (renderer 10 102, designer 5 410) + 6 055 LOC Caché
(12 tříd). Obsluhuje **~28 agend** (faktury, mzdy, sklady, jízdenky, pokladny, …).
Reálné nasazení: **33 000 textových elementů, 17 000 DB polí, 774 tabulek** napříč
zákazníky (`Main.cls:161-188` - usage statistika existuje, použít pro prioritizaci!).

**Model:** šablony čistě v globals `^AlvaReportGenerator("STROM", IdFa, "SESTAVY", ...)`;
strom USER × MLINE větev → agenda → oblasti (složky) → sestavy; **jazykové varianty**
(EN/DE kopie provázaná na CS zdroj); „Sada" = field set publikovaný agendou
(FIELDS/QRKODY/TABLES+COLS/GRAFY/NTEXTS), kopíruje se do sestavy a synchronizuje.
WYSIWYG designer existuje (5 410 ř., bez undo/redo, bez snapu/gridu - TD je zde lepší).

**Datový kontrakt:** hard-coded if-řetěz dispatch na 28 agend (`Data.cls:627-654`);
každá agenda implementuje `GetSeznamSad`/`GetSadu`/`GetData` s `~`-delimited tokenovým
streamem (`FIELD~`, `TABLE_START~`, `ROW_START~`, `CELL~`, `GRAF_START~`, `NTEXT~`).

**Feature highlights nad rámec TD:**
- **Tabulky řízené daty za běhu**: per-řádek zvýraznění 0–5 (každé s kompletním stylem),
  „znovu renderovat řádek na další stránce" (mezisoučty), text přes celou šířku,
  per-řádek/buňka custom barvy, **5 režimů overflow buňky** (vč. zalomení → klonování
  řádků), kombinovaná tabulka (řádek = tabulkový NEBO nadesignovaný layout -
  `stSekceSTabulkouSDesignemRadku`), datové typy sloupců (datum 5 formátů, minuty→HH:MM,
  záporné červeně, netisknout nuly), vícerádkové nadpisy.
- **Sekce**: 8 typů vč. nekonečného textu tekoucího přes stránky, pružné výšky,
  podmínky „tiskni pokud/dokud byla vytištěna sekce S1", `TisknoutAzDoluNaStranku`,
  `Odstrankovat`.
- **Elementy**: gradient+šipky na obdélníku, stínovaný text, svislý text, justify,
  checkbox (5 typů), calculated pole (binární op), grafy (bar/XY/radar/koláč - jen
  12 použití!), AZTEC (2 použití), QR.
- **Podmíněný tisk**: AND-seznam s 20 operátory (numeric/string/date) na sekci i elementu.
- **Výstupy**: PDF (+heslo, +el. podpis PDFSIGNER), JPG, **Excel** (server formátuje dle
  ColType), **e-mail s PDF**, **Epson znakový tisk** (jehličkové, pokladny/jízdenky),
  **headless CLI AUTOEXPORT=1 / AUTOPRINT=1** volané přímo z Caché agend.

**Kde je TD už dnes lepší:** výrazový jazyk (RG má jen ploché AND + binární kalkulačku),
agregace, undo/redo, snap/grid, JSON verzovatelné šablony, moderní čárové kódy,
JŘ elementy, PDF import, AI asistence.

### 3.2 TiskMHD - zastávkový JŘ (z ~90 % pokryt)

**Rozsah:** ~31 400 LOC ručního VB (render engine `_Module.vb` 16 179 - z velké části
copy-paste ~230 téměř identických metod) + 4 313 LOC Caché (6 tříd). Jednoúčelový:
**pouze zastávková vývěska**, dva datové zdroje (`VolanoZ`: JRMAIN × MHD).

**Stav náhrady:** datově 1:1 hotovo (agendy `ZastavkoveJrMhd` + `ZastavkoveJrAd`,
gold oracle ověřeno). Detailní mapovací matice → `legacy-nahrada.md`.
Čerstvé čtení kódu potvrdilo známé mezery a doplnilo:

| Mezera | Legacy reference | Stav v TD |
|---|---|---|
| TrasaMin1–15 multi-varianta + Pracovní2–5 sloupce | `Struktury.vb:56-70` | roadmap P2-1 / AD-N13 (L) |
| Barevná tarifní pásma dle minut (Ceny1–6, server-side) | `Struktury.vb:356-379`, `Data.cls:84-107` | P2-2 (M, od nuly) |
| **M-line distribuce šablon** (MlineSestava ≥ 1000, GOF, `AktualizujAgendu`, audit log, právo na designer) | `Main.cls:83-158`, `Sestavy.cls` | P2-6 - **potvrzeno jako sdílená infrastruktura všech 3 systémů** |
| JPG export, e-mail s PDF, merge hromadného exportu do 1 PDF | `formTISKMHDReportDotNet.vb:944-1193` | chybí (PDF/PNG + split existuje) |
| Per-doklad výběr šablony (různé šablony v 1 print jobu) | vstup `"SestavaId~ValueId~…"` | TD = 1 šablona × N instancí |
| Vizuální detaily (trojúhelník pozadí, zaoblené rohy, per-strana bordery, obrázkový symbolový font, transparent pixel, justify) | `Struktury.vb`, `Obecne.vb:120` | P4 nízká priorita |

### 3.3 TiskJR (+ TiskJRRELACE) - výlepový/knižní JŘ linky (nepokryto)

**Rozsah:** TiskJR ~33 400 LOC VB + 3 290 Caché; TiskJRRELACE (slovenský relační fork,
copy-paste) ~49 700 LOC VB + 3 900 Caché. **Pozor:** výpočetní engine JŘ
(`mkPLinWin`/`mkPRelWin` v `ALVA.JRMAIN.MLAPI` + MAC rutiny) leží mimo a v LOC není.

**Co to je:** sloupcový JŘ celé linky - svislý seznam zastávek (Zóna/Km/Tč/Název/Šipky)
× sloupce spojů s časy; řádek platností se symboly/NZ pod čísly spojů; řádky
Vyčká/Navazuje (návaznosti); skupinová záhlaví PD/So/Ne (3 režimy dělení); poznámky
10 druhů v konfigurovatelném pořadí. `ALVA.TISKJR.Data.LoadData` je jen překladová
vrstva - **skutečný oracle je `mkPLinWin` (JRMAIN)**, ne `ALVA.TISKMHD.Data`.

**Klíčové schopnosti k náhradě:**
1. **Nový element (LineTableElement / „knižní JŘ") + agenda `KnizniJrAd`** - největší kus
   (odhadem srovnatelný se zastávkovým JŘ, který trval ~1 měsíc čistého času).
2. **Flow skládání datových paketů** - N směrů/bloků vedle sebe i pod sebe dle
   zbývajícího místa, server-řízené `ODSTRANKOVAT_PRED_TISKEM`, „dotisk poznámek
   z minulé stránky" - nová layout schopnost pro PageLayoutEngine.
3. **Auto-fit fontu na šířku se zpětným přepočtem sloupců** (legacy iteruje po 0,1 mm;
   `_Module.vb:1434-1491`). `ComputeFontScale` v TD je jen výškový. Legacy „měřicí
   řetězce" ping-pong (server→klient→server) v TD odpadá - SkiaHeightCalculator měří lokálně.
4. **Symbolový font Timetable_nove (+SK varianta)** - 66+ dopravních symbolů
   (`Main.cls:35-107` mapování, záporné kódy = composite symboly kreslené kódem).
   Nutné pro jakýkoli knižní/výlepový JŘ: embed TTF do SkiaSharp renderingu + převodní
   tabulka, nebo SVG glyph sada.
5. **Per-linka šablony a overrides** - linka v JRMAIN má přiřazenou jinou šablonu pro
   A4 a jinou pro A5 + ~17 vlastností per formát (`LoadNastaveniProTisk`). TD nemá
   koncept „výchozí šablona přiřazená instanci".

**⚠️ „Tisk turnusů ke spojům" je v legacy citlivý na tvar čísla linky** (zjištěno 28. 8. 2026).
Engine předá `lin` do dotazu tak, jak ho dostal, ale odpověď filtruje číslem z turnusového záznamu
(`108360`) - s **indexovým tvarem s nulami** („0108360") se nespáruje nic a `TUmax` zůstane 0
(`JRapiLinPrtWin.mac:2684-2722` × `ALVA.ADMAIN.MLAPI:545`). Změřeno oběma tvary („0108360" → 0 spojů,
`108360` → 3 spoje) + pracovním globalem enginu po běhu.

**Otevřené: co posílá skutečný klient.** Komentář v překladové vrstvě ukazuje „4|820701" bez vodicí
nuly (`ALVA.TISKJR.Data:125`), takže v produkci turnusy tisknout **mohou**. Rozhodne jedině tisk ze
starého modulu se zapnutou volbou (nastavení sekce „Data linky" v návrháři sestav). **Pro paritu to
znamená, že u turnusů zatím není proti čemu porovnávat** - TD je ověřuje proti témuž zdroji, jaký
volá legacy.

**TiskJRRELACE** = třetí typ výstupu (řádky = odjezdy, sloupce = linka/čas/směr/
nástupiště). Samostatné rozhodnutí - ověřit, zda je slovenský zákazník live use case
(open question v `legacy-nahrada.md`).

---

## 4. Průřezová témata (sdílená všemi legacy systémy)

Tohle je nejcennější poznatek analýzy: **tři legacy systémy nejsou tři nezávislé sady
featur** - sdílejí identickou infrastrukturu, kterou TD postaví jednou:

| Téma | Legacy podoba (3×) | TD stav | Priorita |
|---|---|---|---|
| **Vendor šablony + distribuce** | MlineSestava (ID ≥ 1000) vs user (< 1000); export do `.gof` v release; `AktualizujAgendu` rozkopíruje do všech IdFa; audit log `^Alva*Log`; právo „pouštět designer" | per-IDfa DB storage bez vendor vrstvy (gap P2-6, roadmap B1/B2) | 🔴 vysoká - bez ní nejde nic nasadit „jako produkt" |
| **Headless tisk z EDISONu** | EDForm volání s `PoleSestavaValue`; RG navíc CLI AUTOEXPORT/AUTOPRINT přímo z Caché agend | TD startup nečte args (roadmap blok A - A1/A2/A3) | 🔴 blocker |
| **Výstupní kanály** | PDF merge do 1 souboru (`MergePDFFiles`), e-mail s PDF přílohou, JPG; RG navíc Excel, PDF heslo+podpis, Epson | jen PDF/PNG z GUI (split-per-soubor ✅) | 🟠 střední (merge+email nejdřív - chtějí je všechny 3 systémy) |
| **Per-instance výběr šablony** | per-doklad SestavaId (TiskMHD), per-linka šablona A4/A5 + overrides (TiskJR) | 1 šablona × N instancí | 🟠 potřebné pro JŘ produkci |
| **Jazykové varianty šablon** | RG: EN/DE kopie provázané na CS zdroj, kaskádový delete/copy | jen `CultureCode` (formátování) | 🟡 až s RG agendami |
| **Symbolový font** | Timetable_nove/SK + ~100 dopravních TTF v `_ruzne/Fonty pro dopravu/` | Arial fallback; `FontResolver` tiše | 🟠 prerekvizita knižního JŘ |

---

## 5. Návrh roadmapy náhrady (doporučení)

Řazeno tak, aby každá etapa doručila samostatnou hodnotu a infrastruktura předcházela
featurám. Etapy 0–2 jsou z velké části už v roadmapě (blok A, P2-6) - tady se potvrzuje
jejich priorita čtením legacy kódu.

### Etapa 0 - Technická konsolidace (před růstem plochy; ~1–2 týdny)
- T1 testy: xUnit pro PageLayoutEngine/ExpressionEvaluator/TemplateMigrator + Caché
  gold-snapshot testy ZastavkoveJrAd → do pre-push hooku.
- T2 async export (R1), T3 migrator guardy (+ ověřit H-19), T5 driftové pojistky.
- Bez toho každá další etapa zvyšuje regresní riziko netestovaného 35k LOC systému.

### Etapa 1 - EDISON integrace + headless tisk (roadmap blok A; blocker nasazení)
- A1 session kontext (vzor `EDISONMAIN2API.FormToRunGetter`), A2 launch nad linkou,
  A3 end-user tisk. **Doplnit o RG vzor:** headless CLI režim (AUTOEXPORT/AUTOPRINT
  ekvivalent) - vrstvy 1–4 to architektonicky už umí, chybí entrypoint.
- Výstupní kanály fáze 1: **merge PDF do jednoho souboru + e-mail s PDF** (společný
  požadavek všech tří legacy systémů, malé M).

### Etapa 2 - Governance a distribuce šablon (legacy „MlineSestava" ekvivalent)
- Systémové/zamčené šablony (M-line) vs uživatelské; distribuční pipeline v release
  (ekvivalent `ExportReportToGlb`/`AktualizujAgendu` - dnes JSON, ne GOF); audit log
  změn šablon; právo na designer (ECL). Roadmap B1/B2/B3 + P2-6.
- Per-instance přiřazení šablony (linka → šablona A4/A5) - datový model
  `InstanceDefaults` už existuje, rozšířit o šablonu.

### Etapa 3 - Knižní/výlepový JŘ (náhrada TiskJR; v roadmapě „Linkové a knižní JŘ")
- Legacy generuje vývěsní i knižní z téhož - **meziměstský i knižní jsou jeden track**
  s oraclem `mkPLinWin` (JRMAIN).
- Pořadí: (1) symbolový font Timetable do SkiaSharp + převodní tabulka
  (`ConvertSpecialniZnak` mapování už známe z obou legacy systémů);
  (2) agenda `KnizniJrAd` - čistá reimplementace à la ZastavkoveJrAd, gold oracle
  `mkPLinWin`; (3) `LineTableElement` (zastávky × spoje) + layout rozšíření
  (flow paketů, auto-fit na šířku, dotisk legendy).
- Velikost: srovnatelná se zastávkovým JŘ (≈ 1 měsíc+), ale metodika je vyzkoušená.

### Etapa 4 - REPORTGENERATOR: strategie postupné náhrady
Ne big-bang (must-not-happen #4). Doporučený postup:
1. **Prioritizovat podle usage statistiky** (`Main.cls:161-188` - reálná čísla per
   zákazník existují!). 774 tabulek a 33k textů se koncentruje v několika agendách.
2. **Rozhodnout bridge vs migrace per agenda:** buď adaptér překládající legacy
   `~`-tokenový protokol (`FIELD~`/`TABLE_START~`/…) na TD JSON DataContext - jedna
   `LegacyRgAgenda` třída by zpřístupnila všech 28 agend najednou s omezenou věrností;
   nebo čisté reimplementace vybraných agend (vzor Faktury). Pragmaticky: **bridge
   nejdřív** (rychlé pokrytí), čisté agendy tam, kde se investice vyplatí.
3. **TableElement dorovnat na RG úroveň** (navazuje na Dynamic-first tabulku):
   per-řádek zvýraznění/barvy z dat, overflow módy buňky, opakování mezisoučtového
   řádku, řádek-jako-designový-layout (koncepčně PerDataRow sekce), datové typy sloupců.
   Merged cells + conditional formatting už jsou v backlogu roadmapy (tabulkové prvky).

   **Rozpis roadmapy `F3`** (navazuje na dynamickou tabulku a `B76`):
   - per-řádek zvýraznění 0-5 z dat, vlastní barvy per řádek a buňka;
   - overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce;
   - datové typy sloupců (datum, minuty, záporné červeně, skrýt nuly), řádek jako layout;
   - **seskupení řádků** (nadpis a mezisoučet za skupinu) a **vnořená data** (doklad → jeho položky).

   ⚠️ Poslední dvě visely samostatně v backlogu.
   Patří sem, protože mají smysl jen nad daty (pevná tabulka je vyplňovaná ručně), a jejich tvar se má odvodit **z reálných sestav RG**, ne navrhnout dopředu naslepo.
   Poptávku nemá ani jedna.
4. **Výstupní kanály fáze 2:** Excel export, PDF heslo/podpis, Epson znakový tisk -
   podle reálné poptávky (Epson = pokladny/jízdenky, ověřit zda vůbec migruje do TD
   nebo zůstane v legacy TISKu).
5. **Vědomě neportovat bez poptávky:** grafy (12 použití), AZTEC (2), calculated pole
   (2 - TD výrazový jazyk je nadmnožina), checkbox (8). Ověřit se zákazníky a škrtnout.

### Etapa 5 - Relace (TiskJRRELACE) + vozové JŘ (v roadmapě „Relace a vozové JŘ")
- Nejdřív zodpovědět open questions (live use case Relace? priorita vozových?) -
  viz `legacy-nahrada.md`. Vozové JŘ mají vlastní legacy generátory
  (`ALVA.MHD.ReportGenerator`, `AD.ADREPORTGEN`) - samostatná analýza až bude aktuální.

---

## 6. Souhrnná tabulka pokrytí

| Legacy systém | Rozsah (VB + Caché LOC) | Pokrytí v TD dnes | Zbývá | Odhad |
|---|---|---|---|---|
| TiskMHD | 31 400 + 4 313 | ~90 % (datově 1:1, elementy ✅) | TrasaMin multi-varianta, tarifní pásma, vizuální detaily | M–L (jednotlivosti) |
| TiskJR | 33 400 + 3 290 | 0 % (jiný formát než zastávkový) | element + agenda + font + layout rozšíření | L (≈ zastávkový JŘ) |
| TiskJRRELACE | 49 700 + 3 900 | 0 % | rozhodnout use case; pak element + agenda | ? (čeká validace) |
| REPORTGENERATOR | 28 850 + 6 055 | ~15 % (Faktury vzor, TableElement základ) | 28 agend (bridge/migrace), tabulkové featury, kanály | XL (etapizovat) |
| **Sdílená infrastruktura** | (napříč) | částečně (DB storage, versioning framework) | vendor šablony, EDISON launch, headless, merge/email | L - **první** |

---

## 7. Otevřené otázky (k rozhodnutí s týmem)

1. **Epson znakový tisk** - migruje do TD, nebo zůstane navždy v legacy TISK? (Určuje,
   zda TD potřebuje ne-grafický výstupní kanál - architektonicky cizí prvek.)
2. **RG bridge vs čisté agendy** - akceptovatelná dočasná věrnost bridge překladu?
3. **Relace (SK zákazník)** - živý use case?
4. **Grafy/AZTEC/checkbox** - škrtnout, nebo má některý zákazník tvrdou závislost?
   (Usage čísla: 12/2/8 výskytů celkem.)
5. **Jazykové varianty šablon** - potřebné i pro JŘ (dvoujazyčné pohraniční vývěsky,
   AD-N11 cizojazyčné poznámky), nebo jen RG doména?
6. **V1 šablony v terénu** - existují? (Určuje urgenci H-19 migrace.)

---

## 8. Zdroje

- Audit TD: čtení `dotnet/**`, `CACHE/**` (2026-07-02).
- Legacy: `C:\DEV\EDISON\REPORTGENERATOR` (klient + `ALVA.REPORTGENERATOR.*` 12 tříd),
  `C:\DEV\EDISON\TiskMHD` (+ `ALVA.TISKMHD.*` 6 tříd), `C:\DEV\EDISON\TiskJR`
  (+ `ALVA.TISKJR.*` 5 tříd), `C:\DEV\EDISON\TiskJRRELACE`.
- Navazuje na: [`legacy-nahrada.md`](legacy-nahrada.md)
  (TiskMHD mapovací matice - evergreen), [`legacy-nahrada.md`](legacy-nahrada.md),
  [`roadmap.md`](roadmap.md).



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Gap analýza legacy TiskMHD vs nový zastávkový JŘ ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Gap analýza: legacy TiskMHD vs. nový zastávkový JŘ

> **Status (2026-06-30):** trvalá reference „co legacy umí a my (ne)máme". Analýza udělaná jednou,
> nedělat znovu. Sekce **„kompletní mapovací matice" (prvek po prvku) + P2/P3/P4 mezery** jsou
> evergreen. Den-D části (P0 ranní checklist, „plán na zítřek") jsou historické - seminář 24.6.
> proběhl. **Aktuální stav mezer** = roadmap (AD-N tabulka). Prezentační shrnutí (sdělení, FAQ,
> zpětná vazba) viz [`legacy-nahrada.md`](legacy-nahrada.md).

> **Účel:** porovnání plochy funkcí starého návrháře (`C:\DEV\EDISON\TiskMHD\`, VB.NET + Caché
> `ALVA.TISKMHD`) proti současné implementaci v TemplateDesigneru. Cíl: zjistit, co reálně chybí
> před **uživatelským seminářem 24.6.2026**, a stanovit priority na dodělání během zítřka.
>
> **Datum:** 2026-06-22 · Analýza čtena z legacy formulářů + Caché Data/Designer/Sestavy + našich
> elementů `StopListElement`/`DepartureGridElement`/`NotesLegendElement`.
>
> **Rozsah pokrytí (2 kola):** ✅ **kompletní.** 1. kolo: 13 editorů sekcí + vlastnosti sestavy +
> všech 6 Caché tříd návrháře (`Data`/`Designer`/`Sestavy`/`Main`/`Linka`/`Images`). 2. kolo:
> hlavní okno návrháře (`formTISKMHDDesignerEdit`, 3472 ř.), datové struktury (`Struktury.vb` -
> autoritativní seznam properties), enumy, .NET renderer (`formTISKMHDReportDotNet` + `ProDrawString`
> + `Obecne`), správce sestav. **Vyřazeno jako irelevantní:** `frmPomocny_*` (generické dialogy),
> app shell (`MainRun`/`ApplicationEvents`/`frmMain`), přístupy, e-mail. **Mimo rozsah (pokryto
> dříve):** datový engine zastávkového tisku v JRMAIN/MHD (`JRapi WinTiskMhd`, `LinkyPrintFormatMHD`)
> - to je gold oracle, reverzně zdokumentován v `ad-data-mapa.md §10`.

---

## Verdikt (TL;DR)

Datově je nový zastávkový JŘ **ověřený 1:1** proti legacy na gold linkách a výkonově pročištěný.
Plocha **uživatelských** nastavení je u 3 specializovaných prvků **bohatší nebo srovnatelná** s
legacy ve většině oblastí (legenda, časová osa, styly buněk).

**Skutečné funkční mezery vůči legacy jsou tři druhy:**
1. **Demo-riziko, ne mezera** - false-positive varování přetečení + DEV tweaky v repu. *Tohle je
   jediná věc, která může kazit živé demo. P0.*
2. **Velké odložené položky** - multi-varianta časů tras (15× TrasaMin). Vědomě deferred, čeká na
   reálný request. Na seminář se zmíní slovně.
3. **Niche / dekorativní** - trojúhelník na pozadí, ellipse/triangle tvary, uppercase jako
   font-flag, zaoblené rohy. Nízká hodnota pro JŘ demo.

**Pro seminář (uživatelský, ne dodávka) není nutné dodělat žádnou funkční featuru.** Stačí
zajistit, aby demo nevypadalo rozbitě (P0) a mít připravené poctivé odpovědi na mezery (P2).

---

## ☑️ Rozhodovací checklist na 24.6 (projít a rozhodnout udělat / neudělat / později)

> Jedno místo k proklikání ráno. Velikost: S = do hodiny, M = půl dne, L = víc dní.
> Do sloupce **Rozhodnutí** dopisuj: ✅ udělat dnes / ⏭️ později / ❌ ne.

| Pol. | Co | Velikost | Priorita | Rozhodnutí |
|---|---|---|---|---|
| P0-1 | Vrátit DEV tweaky (CB_GWBUS→firma, startup šablonu necháváme AD) | S | 🔴 blocker | ⏭️ firmu vyřešit později; startup AD ponechán |
| P0-2 | Opravit #8 false-positive přetečení | S–M | 🔴 blocker | ✅ hotovo 2026-06-23 (879489e) - minimální fix |
| P0-3 | Smoke test gold linek v PDF (obsah, ne pixely) | M | 🔴 blocker | ✅ 2026-06-23 - 0320144 (FTL3) base OK, jako smoke test postačuje |
| P1-2 | ~~Zkrácené označení linky u minut~~ - **NENÍ quick win** | L | 🟠 mezera | ❌ 2026-06-23: pole `ZKRACENE_OZNACENI_LINKY` obě agendy (AD i MHD) emitují **vždy prázdné** (`BuildOdjezd` voláno bez `zkrOznLinky`). Render bez dat nemá co ukázat → vyžaduje celý sdružený JŘ (L), překlasifikováno na P2 |
| P1-1 | Uppercase jako font-flag | S | ⚪ nízká | ⏭️ 2026-06-23 odloženo: UPPERCASE už jde přes `{POLE:UPPER}`; font-flag jen na TextElementu = nekonzistentní polotvar. Udělat později cross-component (vč. malých písmen) |
| P1-3 | NZ na nový řádek v legendě (AD-N4) | S–M | 🟡 quick win | |
| P2-1 | **Multi-varianta časů tras (TrasaMin)** - *jediná velká věc* | **L** | 🟠 mezera | |
| P2-2 | Barevně odlišit časy (6 pásem dle minut) na StopListu | M | 🟠 mezera | ⏭️ 2026-06-23 po demu: NENÍ kopie - `ColorRule`/`DepartureGrid.ColorRules` je mrtvý kód (nikdy se nevyhodnocuje). M od nuly + rozhodnutí client vs Caché |
| P2-3 | NZ (3 ř.) + spec. znak (3×) v záhlaví mřížky | M | 🟠 mezera | |
| P2-7 | Režimy výšky řádku + „nadesignovaná výška = minimum" | S–M | 🟠 mezera | |
| P2-6 | Governance šablon (systémové/zamčené vs uživatelské) | M | 🟠 strategické | |
| P2-4 | Cizojazyčné poznámky | S–M | 🟠 mezera | |
| P2-5 | Potlačit NZ v některých sloupcích dnů (dormant, bez dat) | M–L | ⚪ čeká data | |
| P4 | Dekorativní: justify text, per-strana dash rámeček, zaoblené rohy, tvary (elipsa/trojúhelník), trojúhelník na pozadí, transparentní pixel obrázku | S každé | ⚪ nízká | |

**Doporučený default na ráno:** P0-1/2/3 = ✅, P1-2 = ✅ (bonus, data hotová), zbytek = ⏭️ později.
Nezahajovat L/M položku na poslední chvíli před demem.

---

## P0 - Nutné do zítřka (aby demo nevypadalo rozbitě)

| # | Co | Proč to hoří | Effort |
|---|---|---|---|
| **P0-1** | **Vrátit DEV tweaky** v repu na produkční default | firma **CB_GWBUS → KV_DP**, startup šablona **AD+1 sekce → MHD+Záhlaví/Obsah/Patička**. Jinak se demo spustí v cizí firmě / divné šabloně | S |
| **P0-2** | **Rozseknout #8 false-positive přetečení** (červený rámeček na nové šabloně) | příčina potvrzena: `PreviewCanvas.PageOverflows` ignoruje `ClipTop/ClipBottom` u `SplitAcrossPages` sekcí → každá legitimně rozdělená sekce falešně bliká červeně. Na živém demu vypadá jako bug | S–M |
| **P0-3** | **Smoke test gold linek** v PDF (0410101 / 0425017 / 0320144 / 0320993 / 0365001) | poslední vizuální kontrola 1:1 před publikem; zachytí regrese z dnešních commitů | M |

> Detail #8: oprava varianty A - pro `SplitAcrossPages` sekce přetečení nehlásit (obsah se nikdy
> neztratí, jen přeteče), hlásit jen u `KeepTogether` proti plné výšce sekce. ~30 řádků v
> `PreviewCanvas.cs` + regresní test v `PageLayoutEngine`.
>
> **✅ Vyřešeno 2026-06-23 (minimální fix):** `PreviewCanvas.PageOverflows` přeskakuje
> ne-`KeepTogether` sekce (jeden guard). Ověřeno: `SplitAcrossPages` 400 mm → 2 stránky bez
> červené. **Nález při testu:** engine si `KeepTogether` sekci sám roztáhne na obsah
> (`CalculateSectionHeight = max(DesignerHeight, maxBottom)`), takže element-vs-sekce kontrola
> v praxi nikdy nenaskočí → červené varování je teď fakticky dormantní. To je pro demo OK
> (žádné falešné poplachy), ale **genuine detekce chybí** → viz follow-up níže.
>
> **✅ Genuine page-overflow detekce - hotovo 2026-06-23.** `PageOverflows` u `KeepTogether` sekcí
> hlásí `placed.Y + placed.Height > PrintableHeight + tol` (sekce se nevejde a nejde rozdělit →
> obsah se ořízne). Ověřeno live: KeepTogether 400 mm → červená; SplitAcrossPages → bez červené;
> KeepTogether 100 mm + čistá šablona → bez false-positive.

---

## P1 - Quick wins (volitelné, pokud zbyde čas)

Drobnosti s viditelnou hodnotou, malý zásah. **Žádná není blokující.**

| # | Co | Stav | Effort |
|---|---|---|---|
| ~~**P1-1**~~ | **Uppercase jako font-flag** → **odloženo** | ⏭️ *2026-06-23:* UPPERCASE už pokrývá výrazový jazyk (`{POLE:UPPER}`). Font-flag jen na `TextElement` (jedno místo, jen velká, ne malá) = nekonzistentní s kaskádou. Prototyp navrhnut (`TextStyle.Uppercase` + `ApplyCasing` v `CellRenderer`/`TextElement`) a **vrácen** - udělat až jako cross-component style-flag (vč. malých písmen), aby fungoval všude stejně | S→M |
| ~~**P1-2**~~ | **Zkrácené označení linky u minut** → **přesunuto na P2** | ⚠️ *Korekce 2026-06-23:* C# pole `OdjezdData.ZkraceneOznaceniLinky` se parsuje, ale **obě Caché agendy (AD i MHD) ho emitují vždy prázdné** - `BuildOdjezd` se volá bez `zkrOznLinky` (AD ř. 452, MHD ř. 1041). Render bez dat = nic. Vyžaduje celý sdružený JŘ (sloučení víc linek do mřížky + výpočet zkr. označení) = **L**, ne quick win | L |
| **P1-3** | **Negativní značky na nový řádek v legendě** (AD-N4) | legacy `PATCH,"NegZnNR"`; návrh existuje, chybí property + render v `NotesLegendElement` | S–M |

---

## P2 - Funkční mezery vůči legacy (zmínit slovně, NEslibovat termín)

Co legacy uměl a my (zatím) ne. Pro seminář připravit poctivou odpověď „je na roadmapě".

| # | Legacy funkce | Náš stav | Effort | Poznámka |
|---|---|---|---|---|
| **P2-1** | **15× sloupec „TrasaMin" + 4× „Pracovní"** ve seznamu zastávek (multi-varianta časů jízdy vedle sebe) | máme **1** sloupec časů (gold `TrasaMin1`) | **L** (3–5h) | Největší položka. Caché emituje flat seznam variant, renderer musí dynamicky vytvořit sloupce + strop přehlednosti. Gold: **0345061** (8 variant), 0365001 (2). Odloženo na reálný request |
| **P2-2** | **6 pravidel „barevně odlišit ceny"** (`stSSZ_BarevneOdlisitCeny1..6`: práh `PocetMinut` + ForeColor + BackColor + on/off) - obarví buňky časů jízdy dle prahu minut | ⬜ chybí úplně | M | ⚠️ *Korekce 2026-06-23:* `ColorRule` třída + `DepartureGrid.ColorRules` existují, ale jsou **mrtvý kód** - nikde se nevyhodnocují (potvrzeno grepem). Skutečné barvení mřížky jede přes per-buňku `FORE_COLOR`/`BACK_COLOR` z **Caché**. Legacy pravidla vyhodnocoval taky **server-side** (Caché) → emitoval hotové barvy. **Rozhodnutí:** client-side (oživit ColorRule + eval při renderu) vs Caché-side (jako legacy). M od nuly, ne kopie |
| **P2-3** | **Negativní značka (3 řádky) + spec. znak (3×) v záhlaví mřížky** odjezdů | DepartureGrid záhlaví = jen nadpis skupiny dnů | M | Blok NZ/spec-znak v hlavičce sloupce skupiny dnů. Ověřit, jestli to zákazníci reálně používají |
| **P2-4** | **Cizojazyčné poznámky** (AD-N11, `prtCzjPzn`) | ⬜ chybí | S–M | Meziměstské/pohraniční linky. Caché rozšíření + toggle v legendě |
| **P2-5** | **Potlačit NZ v některých sloupcích** skupin dnů (AD-N5) | ⬜ dormant | M–L | Data prázdná všude, čeká na reálný případ |

---

## P3 - Vědomé rozdíly (NE mezery - jiný/lepší přístup)

Nevyžadují akci; jen aby se to nepletlo s „chybí".

- **Časová osa odjezdů** - legacy měl 3 režimy (`CasovaOsaOdjezduBezMezer`). My máme **4** (`Compact` / `DataBounded` / `Range` vč. wrapu přes půlnoc / `Full`). **Bohatší.**
- **Legenda poznámek** - legacy uměl jen 1–2 sloupce + šířku + výšku řádku, řazení/zarovnání bylo zapečené v enginu. My máme **1–4 sloupce, řazení dle délky, směr wrapu, zarovnaný marker, filtr per typ**. **Bohatší.**
- **Styly buněk** - legacy 4 pevné font-profily; my máme **kaskádu** (sloupec → prvek → defaults) + per-sloupec override + zvýraznění aktuální/projeté zastávky. **Srovnatelné/bohatší.**
- **Pozicování sekcí** - legacy „přichytit zdola/zprava k sekci" (relativní flow). My máme absolutní pozice + flow layout v engine + drag&drop řazení. **Jiný model, funkčně pokryto.**
- **Čárové kódy** - legacy jen QR (JPG, bez ECC/barev). My QR + Code128 + EAN-13/8 + PDF417 + DataMatrix. **Bohatší.**
- **Vlastnosti stránky** - legacy A2–A6 + landscape + okraje + vodítka. My editor formátů + orientace + okraje + mřížka. **Pokryto.**
- **Wire formát** - legacy tilde-delimited; my JSON. **Lepší (verzovatelné, debugovatelné).**

---

## P4 - Dekorativní / nízká priorita (mimo JŘ jádro)

Legacy obecné prvky, které pro zastávkový JŘ nejsou klíčové:

- **Tvary** - legacy `Shape` měl 12 typů (ellipse, plná kružnice, trojúhelníky A–D plné/obrysové). My máme `RectangleElement` + `LineElement`, **žádné trojúhelníky/elipsy**.
- **Trojúhelník na pozadí** seznamu zastávek (směrový klín, 3 typy zúžení) - dekorativní, **chybí**.
- **Zaoblené rohy rámečku** (`BorderRadius`) - legacy jen u DBField; my pravé rohy nemáme.
- **Transparentní pixel obrázku přes souřadnici X/Y** (color-key) - niche, chybí.
- **„Přizpůsobit" písmo per zóna** - my máme jeden globální auto-scale (`ComputeFontScale`), ne per-zóna. Funkčně postačuje.

---

## Druhé kolo - doplňující nálezy (mimo editory sekcí)

Analýza hlavního okna návrháře, struktur a renderovacího enginu odhalila tyto věci, které editory
sekcí neukazovaly:

### ⚠️ Klíčová nuance: vizuální 1:1 NENÍ totéž co datové 1:1
Datové 1:1 je ověřené proti gold oracle (`ALVA.TISKMHD.Data`) - **správný obsah**. Ale legacy
**vizuální** render běží na **GDI+ (`System.Drawing`)**, náš na **SkiaSharp**. To jsou jiné
sázecí enginy → **pixelová shoda není garantovaná a ani se o ni neusiluje** (nový nástroj, vizuálně
podobný). Legacy navíc obsahuje empirické „fudge" konstanty (smršťování znaků `/15`,`/5`,`/40`,
`camfrnous=+1`, font v mm × 4 body/mm, auto-fit krok 0.3 mm, šířka sloupce minut hledaná po 0.1 mm).

**Důsledek pro P0-3 smoke test:** porovnávat **obsah a rozložení** (správné minuty, anotace,
poznámky, sloupce), ne pixely. Místa nejnáchylnější k odlišnosti, která stojí za pohled:
- pozice horních indexů u minut (nahoru/dolů/doprava + zmenšené písmo),
- inverzní negativní značka (bílé na černém - velikost černého obdélníku),
- šířka sloupce minut a auto-fit písma (zalomení / počet znaků na řádek),
- zalomení dlouhých poznámek v legendě.

### Nové funkční mezery (doplnit do priorit)
| # | Legacy funkce | Náš stav | Priorita |
|---|---|---|---|
| **P2-6** | **Governance šablon** - systémové (M-line) vs uživatelské sestavy, mimo M-line doménu read-only; M-line je distribuuje zákazníkům | máme per-IDfa storage v Caché, ale **žádné „systémová/zamčená šablona"** ani distribuční model | M - *strategické, ne demo* |
| **P2-7** | **Režimy výšky řádku** + „nadesignovaná výška = minimum, sekce roste" | máme `DataRowHeight` (null=auto) + auto-scale; explicitní „výška je minimum a roste" model částečně | S–M |
| **P4-x** | **Zarovnání textu do bloku (justify)** v textovém prvku | `TextElement` justify nemá | S, nízká |
| **P4-x** | **Per-strana styl rámečku (Solid/Dot/Dash) + zaoblené rohy** sekce/prvku | jednoduchý border (barva+šířka), bez per-strana dash a bez zaoblení | S–M, nízká |
| **P4-x** | **Sekce vyšší než stránka v designeru** - designer je model „jedna pevná A4": `GetSectionOrigins` + `GetClampedSectionHeightPx` ořezává sekce na spodek stránky, ale elementy s velkým Y přetečou na šedé pozadí. Preview stránkuje korektně (a po dnešku i varuje červeně). ⏭️ odloženo 2026-06-23. **Zjištění:** (A) clamp NEjde čistě - aktivní editor sekce je EDInputForm `SectionEdit` (starý `PrepareForSection` clamp je mrtvý kód, nevolá se); clamp by se musel lepit do generického binderu. (B) „canvas roste + dělící čáry A4" = **M** (přepis jádra: zrušit clamp sekcí, růst canvasu/pozadí, dělící čáry + **ripple:** patičky kotvené na `bottomY`, resize bounds `_resizeSectionH`, hit-test, element clamp - vše počítá s 1 stránkou). Doporučeno B po demu | jednostránkový canvas | M, nízká |

### Potvrzeno jako pokryté (druhé kolo)
- **Marker legendy 3 typy** (normální / bílé-na-černém / pictografický znak) → máme inverzní barvy NZ + spec. znaky. ✅
- **NZ invertovaně u minut v mřížce** (černý box + bílý znak) → ✅ 2026-06-23 (DepartureGrid, MHD). AD záměrně bez NZ u minut (CNZ jen klasifikuje sloupce dnů - ověřeno 1:1 gold).
- **Zebra liché/sudé řádky** (hodiny i minuty) → `AlternatingRowColor` + per-group. ✅
- **Pořadí sloupců** (`PoradiSloupcu`) → `Columns` order. ✅
- **Samostatná sekce „Hodina odjezdu" + NegZnacka/SpecZnak** → sloučeno do DepartureGrid/anotací (jiný, čistší model). ✅
- **Export/import šablony** (legacy `.GOF` raw global) → JSON export. ✅+

---

## Kompletní mapovací matice (prvek po prvku)

### Seznam zastávek (StopList)
| Legacy volba | Náš ekvivalent | Stav |
|---|---|---|
| Sloupce: Časy/Zóny/Průběhy/ZákladníTrasa/Pracovní2-5 | `Columns` (Key/Header/Width/Visible/Align/Style) | ✅ jádro, ⬜ Pracovní2-5 |
| TrasaMin 1–15 (multi-varianta) | 1 sloupec časů | 🔴 P2-1 |
| Per-sloupec šířka / zarovnání / pozadí / pravý okraj | `StopColumn.Style` + `GridColor` + separátory | ✅ (granularita per-border čar částečná) |
| 4 font-profily (hlavička/aktuální/projetá/neprojetá) | `HeaderStyle`/`DataStyle` + `CurrentStop*` + `PassedStopsForeColor` | ✅ |
| Zvýraznění aktuální zastávky (font + pozadí) | `CurrentStopBold/Underline/FontSize/BackgroundColor` | ✅ |
| Šipka vlevo (barva) | `ShowDirectionArrow` + `DirectionArrowPosition(Left/Right)` + barva | ✅ |
| Trojúhelník na pozadí (3 typy) | - | 🟡 P4 |
| Barevně odlišit ceny (6× práh minut) | - (ColorRule je mrtvý kód i na DepartureGridu; barvení jede přes Caché FORE/BACK_COLOR) | 🟠 P2-2 (M od nuly) |
| Uppercase font-flag | jen `{POLE:UPPER}` | 🟡 P1-1 |
| Atributy/poznámky zastávky, „na znamení" | `ShowStopAttributes/Notes/SignalStopIndicator` + filtr | ✅ |
| Výška řádku pevná/dynamická | `DataRowHeight` (null=auto) + auto-scale | ✅ |

### Mřížka minut (DepartureGrid)
| Legacy volba | Náš ekvivalent | Stav |
|---|---|---|
| Skupiny dnů jako sloupce | `GroupColumns` + filtr `SkupinaDnuId` | ✅ |
| Sloupec hodin (vlastní nadpis, per-group) | `HourColumn*` + `HourColumnPerGroup` | ✅ |
| Časová osa (3 režimy) | `TimeAxisMode` (4 režimy + wrap půlnoc) | ✅+ |
| Zebra (lichý/sudý) | `AlternatingRowColor` + per-group override | ✅ |
| Anotace u minut (NZ, spec. znak, dny, rozdíly tras) | `ShowAnotace` + `AnotaceSymbolStyle/TextStyle` + engine | ✅ |
| Zmenšené písmo anotace (%) | `AnotaceFontSize` + oddělené styly | ✅ (jinak) |
| Vertikální zarovnání záhlaví | `HeaderVerticalAlignment` | ✅ |
| NZ (3 řádky) + spec. znak (3×) v záhlaví | - | 🟡 P2-3 |
| Zkrácené označení linky u minut | data parsována, render chybí | 🟡 P1-2 |
| Barevná pravidla (data-driven) | `ColorRules` | ✅ |

### Legenda poznámek (NotesLegend)
| Legacy volba | Náš ekvivalent | Stav |
|---|---|---|
| Počet sloupců (1–2) | `Columns` (1–4) | ✅+ |
| Šířka sloupce s kódy | `MarkerSirka` | ✅ |
| Výška řádku, font, barvy | `NoteLineHeight`/`NoteFontSize`/styly | ✅ |
| Řazení / zarovnání markeru / směr wrapu | `Razeni` + `MarkerSirka` + `WrapSmer` | ✅+ (nad legacy) |
| Filtr per typ (NZ / spec / text) | `ShowNegativniZnacky/SpecialniZnaky/TextovePoznamky` | ✅ |
| NZ na nový řádek | - | 🟡 P1-3 |
| Filtr per kategorie | jen per-typ | 🟡 (overlap s D5/D6, tech debt) |

---

## Doporučený plán na zítřek (24.6, ráno před seminářem)

1. **P0-1** vrátit DEV tweaky (5 min).
2. **P0-2** opravit #8 false-positive overflow + regresní test (~1h).
3. **P0-3** vyexportovat gold linky do PDF a vizuálně zkontrolovat (~30 min).
4. *(pokud zbyde čas)* **P1-2** dorenderovat zkrácené označení linky (data už máme) - pěkné „bonus" pro sdružené JŘ.
5. Připravit slovní odpovědi na **P2** (hlavně TrasaMin multi-varianta - „na roadmapě, přidáme dle vašich potřeb").

**Nezahajovat** žádnou P2/P4 položku na poslední chvíli - riziko regrese před demem převažuje hodnotu.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Mapa legacy Caché generátorů JŘ ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Legacy Caché generátory JŘ - reference pro TD migrace

Mapování typů jízdních řádů na **legacy Caché report generátory** + business kontext per typ. Slouží jako reference před implementací nových agend v TD (linkové a knižní JŘ, Relace a vozové - PRIORITA 2, zastávkový JŘ pro linkové JŘ data).

> **Stav 2026-05-29.** Některé položky jsou open questions (?označené) - doptat se Martina Hubaty.

---

## 1) Zastávkové JŘ - `ALVA.TISKMHD`

**Použití:** vylepuje se na zastávky (zastávková vývěska). **V linkové dopravě se moc nepoužívá** - primárně MHD.

**Vlastnosti:**
- Podporuje **město i linkový formát** (dva módy v jednom generátoru)
- **Typy provozu** v linkové variantě se odvozují na základě **značek + negativních značek** (= „typ provozu" - pattern jiný než MHD kde je `TypProvozuID` přímo entitou)
- Používají **všichni 3 zákazníci** (TRANSDEV / KORID / DPKV)

**Stav v TD:** Implementováno jako agenda `ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd` (MHD datový zdroj). **Linková varianta dosud chybí** - PRIORITA 2 v roadmap.

**MHD specifika:** zákazníci dělají i **knižní** zastávkové JŘ, ale „knižní se už moc nevydávají" - nízká priorita pro knižní MHD.

---

## 2) Linkové JŘ (vývěsní a knižní) - `ALVA.TISKJR`

**Použití:** **celá linka** v jednom tisku. Vylepuje se na zastávky v linkové dopravě (vývěsní) a tiskne se v knižních publikacích.

> ❓ **TODO:** doptat se Martina, jestli `ALVA.TISKJR` umí i MHD nebo je čistě linkový.

**Požadavky (business):**
- **Zóny** - aby jich bylo víc (vícezónový tarif)
- **Km** se tisknou, ale **jezdí se podle zón**, ne podle kilometrů (km = informativní sloupec)
- **Tč** (tarifní číslo)
- **Zastávky** (názvy, řazení)
- **Obousměrný** JŘ nebo **jednosměrný** (config)
- **PostScript trik:** vkládá prázdné sloupce, aby výsledek byl **vždy stejně široký** (fixed layout)
- **Tisk podle typu provozu** - analogie MHD typů provozu
- **Návaznosti** - mohou být:
  - Zapsané **dole** (samostatná sekce pod tabulkou), nebo
  - **Přímo v tabulce** (jako řádek/anotace) - „tisk návazností"

**Stav v TD:** zatím **neimplementováno**. Bude potřeba samostatný element / sada elementů (StopList neuumí celou linku v jednom tisku jako vyžaduje vývěsní linkový JŘ).

---

## 3) Relace (Slováci) - `ALVA.TiskJRRELACE`

**Použití:** slovenský zákazník (CTC?). Něco mezi zastávkovým a linkovým.

**Vlastnosti:**
- Podobné zastávkovému, **ale vypadá to trochu jinak**
- Klíčový obsah: **odjezdy ze zastávky a kam všude to jede** (cílové destinace per odjezd)

**Stav v TD:** zatím **neimplementováno**. Pravděpodobně extension zastávkového JŘ nebo samostatný element.

---

## 4) Vozové JŘ (turnusy)

Dva legacy generátory podle typu:
- **MHD:** `ALVA.MHD.ReportGenerator`
- **Linky:** `AD.ADREPORTGEN`

**Použití:** v linkové dopravě se tiskne **v turnusech** (oběhy vozidel - který spoj jede které vozidlo).

**Stav v TD:** zatím **neimplementováno**. V roadmapě „Relace a vozové JŘ".

---

## Souhrn priority migrace v TD

Per roadmap PRIORITA 2:

1. **Zastávkové JŘ pro linkové JŘ data** - rozšířit `ZastavkoveJrMhd` o linkový datový zdroj (nebo nová agenda). Zachovat dvojí formát „město × linková" stejně jako legacy `ALVA.TISKMHD`.

V roadmapě linkové/knižní JŘ a Relace/vozové (později, nižší priorita):

2. **Linkové vývěsní/knižní JŘ** - `ALVA.TISKJR` ekvivalent (meziměstský; legacy generuje obojí)
3. **Relace** - `ALVA.TiskJRRELACE` ekvivalent (specifický slovenský use case)
4. **Vozové JŘ** - `ALVA.MHD.ReportGenerator` + `AD.ADREPORTGEN` ekvivalent (vozové JŘ)

---

## Open questions (doptat se)

- ❓ `ALVA.TISKJR` - funguje i pro MHD nebo jen linkový?
- ❓ Datové zdroje linkového JŘ v Caché (analogie `ALVA.MHD.LSO/CHR/ZST` pro MHD)?
- ❓ Relace - který zákazník přesně, je to live use case nebo legacy?
- ❓ Vozové JŘ - koho přesně to zajímá, časová priorita?



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Prezentace zákazníkům 2026 (sdělení, FAQ, P1–P3) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Prezentace zákazníkům 2026 - shrnutí + gap vs legacy (reference)

> **Trvalé shrnutí.** Prezentace proběhly (statická ~19.6., živá seminář 26.6.2026). Tento dokument
> konsoliduje to, co má hodnotu i po akci: **produktové sdělení** (pro příští prezentace/rozhodování),
> **co se zjistilo** (zpětná vazba), a **gap vs legacy** jako referenci. Přípravné artefakty (osnova
> 60 min, podrobná režie demo, ranní gap-checklist) byly smazány - vznikly pro konkrétní akci a doslova
> se „proklikaly". Historie v gitu.

---

## 1. Produktové sdělení a filozofie (reusable)

**Klíčová věta:** *„Stejná data, stejný výsledný tisk - ale návrh šablony je teď vizuální, okamžitý a
ve vašich rukou."*

**Co prodáváme proti legacy** (starý nástroj = formuláře + čísla, bez náhledu, výsledek až po tisku):
- vizuální WYSIWYG editace místo „naslepo do globálů",
- okamžitý náhled, vše vratné (undo/redo),
- šablona reaguje na data (placeholdery, podmíněná viditelnost, agregace),
- centrální úložiště v databázi per firma (ne roztroušené konfigurace po stanicích),
- nová aplikace = prostor přidávat funkce (u starého nástroje prakticky nešlo).

**⭐ Produktová filozofie „nedávat do zásoby" (důležité pro rozhodování o featurách):**
Záměrně stavíme nástroj **odlehčeně**. Starý nástroj měl tolik voleb, že se v něm uživatelé sami
těžko vyznali. Cíl = **srozumitelnost** → přidávat jen to, co zákazník **reálně používá**, ne co by
*teoreticky* šlo mít. Při sběru požadavků aktivně odrazovat od „požadavků do zásoby". (Promítá se do
`feature-design-principles.md` - klasifikace typu variace 1/2/3 a per-customer divergence.)

**Otázky na zákazníky** (sběr po prezentaci - co reálně potřebují):
1. Která nastavení/přepínače ve starém AD reálně používáte?
2. Bez čeho se neobejdete (must-have)?
3. Máte vzorové JŘ výstupy na ověření shody 1:1?
4. Liší se něco mezi vašimi linkami/provozy, co musíme podchytit?

**Připravené odpovědi na typické dotazy (FAQ):**
- *„Můžeme si šablonu upravit sami?"* → Ano, to je celý smysl - vizuálně + uložení do databáze.
- *„Co se starými šablonami?"* → V1 neimportuje 1:1; nové se navrhnou znovu (rychle, WYSIWYG).
- *„Bude to vypadat stejně?"* → U AD JŘ datově ověřeno 1:1; vizuál je nový engine (SkiaSharp místo GDI+),
  obsah a rozložení sedí, pixelová shoda se negarantuje (a ani neusiluje).
- *„Jak se to k nám dostane?"* → Integrované v EDISONu, tisk stejným enginem jako náhled.
- *„A umí to ještě…?"* → většina nastavení je v demu; co nepoužíváte, schválně nezahlcujeme.

---

## 2. Co se zjistilo (zpětná vazba z prezentací)

**Nové požadavky** zaznamenané z živých prezentací (neanalyzováno, před implementací založit ticket) -
detail viz [`customer-requirements-zastavkovy-jr.md §P`](customer-requirements-zastavkovy-jr.md),
backlog v [`roadmap.md`](roadmap.md):
- **P1** - ruční poznámky do patičky JŘ *(vyřazeno 2026-07-10 - nejasné zadání)*
- **P2** - sloučení více linek se stejným posledním trojčíslím (i různá Tč / více dopravců) do 1 výstupu *(řeší se mimo tuto agendu)*
- **P3** - logická imposice 2×A5 na A4 *(✅ hotovo 2026-07-12, v1 bez brožury/duplexu)*

**Poctivé výhrady prezentované zákazníkům** (NEpodávat jako 100% hotové):
- Helpdesk tickety vyřešené převážně v **AD agendě** (TRANSDEV jezdí linkově); MHD modul může mít
  drobné odlišnosti.
- #7959 (filtr neobsluhovaných) funguje, ale „ověřit na reálných TRANSDEV datech" → „připraveno k ověření".
- #7955 pod-položka „výběr spojů jen jednociferně" = bug v **EDISON Studiu JŘ**, ne v Návrháři.

> Mapování vyřešených helpdesk tiketů → konkrétní featura: [`customer-requirements-zastavkovy-jr.md §D`](customer-requirements-zastavkovy-jr.md)
> + roadmap tabulka A.2.D.

---

## 3. Gap vs legacy → samostatná reference

Kompletní porovnání **legacy TiskMHD vs. nový zastávkový JŘ** (verdikt, element-po-elementu mapovací
matice StopList/DepartureGrid/NotesLegend, zbývající mezery P2–P4, vědomé rozdíly „kde jsme bohatší",
nuance vizuální vs datové 1:1) žije v samostatném dokumentu - **analýza udělaná jednou, nedělat znovu**:

➡️ [`legacy-nahrada.md`](legacy-nahrada.md)

Aktuální stav otevřených mezer se trackuje v [`roadmap.md`](roadmap.md) (AD-N tabulka). Přehled „co
umíme navíc oproti legacy" pro komunikaci se zákazníky: [`customer-requirements-zastavkovy-jr.md §F`](customer-requirements-zastavkovy-jr.md).

---

## 4. Opsané pomocné funkce Caché agend - podklad k případnému sjednocení

Analýza 5. 8. 2026, přesunuto z roadmapy 4. 9. - **udělané jednou, nedělat znovu.** Samo sjednocení
čeká, dokud se tvar výstupních dat neustálí (rozhodnutí Jan 5. 8. a 31. 8.).

27 pomocných funkcí nese stejný název ve dvou a více agendách, ale:

- **15 jsou doslovné kopie** - ≈ 97 řádků k odstranění, ne 400-500, jak tvrdil původní odhad.
- **4 mají stejnou signaturu a rozešlé tělo, a to záměrně** - `AddStandardJRFields` se liší
  v uživatelských popiscích polí (AD „Číslo linky" × MHD „Číslo linky (dle licence)"),
  `AddStandardJRParameters` nabízí jiné parametry sestavy.
- **8 sdílí jen název a dělá jinou věc** - `BuildPoznamka` má dvě rodiny s **jiným datovým
  kontraktem** (zastávková posílá `KATEGORIE` + barvy, linková `DRUH`), `PopisDopravce` existuje
  3× se třemi signaturami.

Sloučit jde doslovné kopie. Zastávkové agendy kryjí zlaté odtisky a 46 testů helperů, linkové
mají vlastní sady (`LinkovyJrGoldTest`, `LinkovyJrMhdTest`, porovnání se starým tiskem
`LinkovyJrLegacyOracle` a `LinkovyJrMhdLegacyOracle`). Vzor existuje - `BaseAgenda` už čtyři
sdílené helpery hostuje.

**Změřeno 21. 9.:** vývěsky mají 10 metod znak po znaku shodných (~100 řádků: barvy, atributy
zastávky, stavba odjezdu a skupiny dnů, obě metody předdefinovaných tabulek); ze schématu je 25 z 27
popisů polí MHD totožných s AD (u linkových 22 z 24). Liší se dva popisky po ose AD × MHD
(`CISLO_LINKY`, `LOKALNI_OZNACENI_LINKY`) - ve společné metodě by byly parametrem. Popisky polí hlídá
`AgendaSchemaDriftTests`. Tvar sdílení je rozhodnutý (29. 10.): **pomocné třídy mimo hierarchii agend**,
ne mezivrstva `ZastavkovyJrBase` / `LinkovyJrBase` - registr agend by ji schoval z nabídky
([`architektonicka-rozhodnuti-agendy.md` R8](architektonicka-rozhodnuti-agendy.md)).

**Doplněno 23. 10. (review `B193`)** - kde začít, protože se to už rozešlo:

- **Výběr skupin linek a linek** - u AD dvě kopie (`GetAvailableSkupiny`, `GetAvailableInstances`).
  MHD je od 23. 10. sdílí (`ZastavkoveJrMhd.SeznamLinek`, `B193c`), kopie se do té doby rozešly
  v příznaku zablokované skupiny.
- **Hlavička linky** (číslo, platnost, typ JŘ, dopravce) - každá agenda ji skládá zvlášť; odtud čtyři
  tvary popisu platnosti a dva texty návrhu (`B193f`, viz changelog 23. 10.).
- **Společné parametry vývěsek** (Pořadí stránek, šipky, projeté zastávky, časová osa, roztažení) jsou
  opsané (`ZastavkoveJrAd.cls:3521-3675` × `ZastavkoveJrMhd.cls:1774-1826`). Linkové agendy mají jednu
  sdílenou definici (`LinkovyJrAd.cls:373-446`) a to je vzor.
- **Předpřipravené tabulky** (`GetTableTemplates`/`GetTableTemplate`, vývěsky a faktury): návrhář je
  nikde nenabízí a do 23. 10. při volání padaly (`B193f`). Zrušit, nebo napojit do vkládání tabulky.
- **Linkové helpery** `BuildPoznamka`, `BuildSkupina`, `FormatCas` (`LinkovyJrAd.cls:1814, 2120, 2131`
  × `LinkovyJrMhd.cls:1176-1195`) a `RgbToHex` vedle `BaseAgenda.RgbNaHex`.

**Popisky voleb rozdělení sloupců (`P2`, revize `B194` 23. 10., ověřeno na serveru)** - patří ke kroku R5
(parametry vývěsek do jedné definice), čeká na úklid nastavení tisku v jízdních řádech, který dodělají kolegové
(domluveno 29. 10., backlog).

- Totéž rozdělení sloupců má ve vývěsce a v linkovém JŘ jiné popisky voleb: „Pracovní / Sobota / Neděle+svátky" × „Pracovní dny / Soboty / Neděle+svátky", k tomu kryptické prázdninové volby vývěsky.
  Vnitřní klíče jsou stejné (`PRAC_SO_NE`, `PRAC_VIKEND`).
- Volby jsou zamčené (ukládá se zobrazený text, `ux-conventions §8`), proto přidat souběžný klíč `optionLabels` a `options` nechat jako text.
  `CacheAgendaProvider.ParseParameters` čte `options` jako pole textů: volba poslaná jako objekt by vyhodila výjimku a návrhář by ztratil všechny parametry agendy.
- Třetí kopie popisků je v `DepartureColumnPresets` (vzory vlastních sloupců).
- Popisky a nápovědy parametrů výstup ani uložené volby nemění, návrhář je bere ze serveru.
  Citují je ale hlášky linky bez dat (`BuildStrankaLinky` v obou linkových agendách), při přejmenování opravit i je.

**Vědomé rozdíly mezi agendami JŘ (rozhodnutí Jan 26. 10., revize `B194`)** - každá agenda tu drží tvar
svého starého modulu, sloučení kódu je nesmí srovnat.

- **T5 Nadpis skupiny dnů:** ve vývěsce MHD dvouřádkový, v linkovém MHD jedním řádkem.
  Vývěska má úzké sloupce skupin, v Tabulce spojů se nadpis táhne přes všechny spoje skupiny.
  Požadavek DPKV #10660 se týkal vývěsky; dva řádky v linkovém by se dělaly novým polem.
- **T6 Telefon dopravce:** „tel:" ve vývěskách a „tel." v linkových JŘ.
  Každý tvar odpovídá svému starému tisku a vývěsky jsou vydané.
- **T4c Vysvětlivka „na znamení"** (potvrzuje srpnové rozhodnutí): v Zastávkovém JŘ AD krátká a napevno („na znamení", `ZastavkoveJrAd.TextAtributu`), Linkový JŘ AD ji bere z číselníku stálých značek firmy („zastávka je jen na znamení nebo požádání"), agendy MHD z číselníku atributů MHD.
  Starý zastávkový tisk měl „zastávka pouze na znamení".
- **T8 Zóny zastávky** skládá každá agenda pravidlem svého starého modulu: Zastávkový AD z trasy linky se záskokem na registr (`ZonyZastavky`), Linkový AD jen z trasy a „-" (`BuildZastavkaRadek`), Zastávkový MHD všechny zóny bez filtru systému, Linkový MHD jen systém linky spojené „/" (`ZonyZastavky`).
  Rozdíl je vidět jen u zastávek se zónou mimo trasu nebo ve dvou IDS.
- **T10b Stanoviště u názvu zastávky:** Linkový JŘ AD ho tiskne v řádku tabulky u zastávky s příznakem trasy „S" jako „, stan.16" (parita starého linkového tisku, `LinkovyJrAd.BuildZastavkaRadek`), Zastávkový JŘ AD jen v nadpisu rozdělené vývěsky jako „, označník 3" / „, sloupek 1" (vzor KOVED #8828, `ZastavkoveJrAd.BuildStrankaZastavky`).
  Jiné místo i účel; zkratka „stan." neumí rozlišit označník a sloupek.

**Opsané pod jiným jménem (revize `B194g` R2, změřeno a rozhodnuto 30. 10.)** - kopie, které analýza z 5. 8. hledala podle názvu, a proto je nenašla.
Měřeno sondou `DuplicityAgendProbe` na šesti testovacích firmách (96 531 linek, 2,4 mil. spojů).

Sjednotit v `B56`, tisk se nezmění:

- **Platnost linky** (`PATCH,"Plati"` = „od~do:…") se čte na sedmi místech dvěma způsoby a příponu za dvojtečkou odříznou jen `BaseAgenda.HorologPlatnostiOdLinky` a `ZastavkoveJrAd.PlatnostLinky`.
  Starý modul ji odřízne vždy, ostatní kopie by ji poslaly v datu „do".
  V datech ji nemá žádná linka (0 z 96 531), sjednocení na jednu metodu s odříznutím proto tisk nezmění.
  K tomu tři převody data na `$h`.
- **Omezení 14 a 15** („jede jen v sudých / lichých týdnech") vytiskne `LinkovyJrAd.SestavTextOmezeni` i bez data, `ZastavkoveJrAd.SestavTextOmezeni` je bez data přeskočí.
  Bez data není žádné z 319 výskytů; sjednotit na chování linkového JŘ (věta platí i bez data).
  Pozor na jazyk předložky: vývěska ho bere z parametru šablony (`PrefixOmezeni`), linkový JŘ z kódu jazyka (`PredlozkaOmezeni`).

Vědomě nesjednocovat (rozhodnutí Jan 30. 10.):

- **Popis dopravce** (`POPIS_DOPRAVCE`): tři kopie v agendách a skládání v návrháři (`PageLayoutEngine`, `LineTableElement.PopisDopravcuStranky`).
  Vývěska AD podle starého zastávkového tisku dává sídlo firmy a sídlo provozovny každé na řádek, takže u GW BUS (320157) vyjde adresa dvakrát; linkový JŘ dává vše na jeden řádek s čárkami a adresu jednou.
  Každý tvar odpovídá svému starému tisku, dvojí adresu (#13158) řeší pole dopravce po položkách.
- **Rozsahy čísel spojů**: souhrn dopravců ve vývěsce (`ZastavkoveJrAd.RozsahyCisel`) vede řadu jen přes čísla těsně po sobě („1-35, 37, 39, 41, 43, 45, 101"), Tabulka spojů (`LineTableElement.Rady`) i přes ob jedno, protože stránka nese jeden směr („1-45, 101").
  Každé pravidlo sedí na svůj případ.
- **Barva „0;0;0"** znamená v každém zdroji něco jiného, nejde o kopii téhož.
  Barva spoje AD (`#`-pole 12, `BaseAgenda.RgbNaHex`) = bez barvy: má ji 2 185 160 z 2 189 806 spojů s barvou.
  Barva linky (`ZastavkoveJrAd.BarvaLinky`) a barvy poznámek ke spojům MHD (`ZastavkoveJrMhd`) jdou přes `RgbToHex` a černá je černá: v KV_DP má 24 poznámek černý znak záměrně.
  Při `B56` jen pojmenovat podle zdroje; komentář „kopie téhož" u `RgbNaHex` neplatí.
- **Značky u odjezdů a kolekce vývěsek**: `AddAnotace` AD posílá navíc `KATEGORIE`, `SPOJ` a `V_ROZSAHU_NEPOZNAMKOVAT`, `AddStandardJRCollections` AD má o pět polí víc.
  Společná metoda jen tak, aby MHD tyto klíče dál neposílal - jinak by filtr Poznámek podle kategorie začal skrývat poznámky i v MHD.

Stejné jméno, jiný význam - při přesunu do pomocné třídy přejmenovat, ne sloučit:
`Jazyk` (`*SK`/`CZ` z `JRSET` × `cs`/`sk` z `^||TiskAd`), `ZonaBezPismen` (úprava textu × ano/ne z nastavení), `NactiNastaveniNazvu` (výchozí „Plný CIS" × „Zkrácený", vědomý rozdíl hlídaný v `KontraktAgendTest.Vyjimka`), `PopisDopravce` (viz výš), `BuildStrankaLinky`.
Pracovní globály se jmenují jinak a liší se klíčem: `WMapa` (`^||TiskKnizni("wmap",trasa)` × `^||TiskAd("wmap",db,sku,lin,trasa)`), `ZaznamZastavky` bere datum z jiného globálu.

**Postup `B56`** (přesunuto z `konzistence-agend-jr.md §7` 30. 10.):

- **Pojistky:** `KontraktAgendTest` (kontrakt každé agendy z registru, data odpovídají nabídce, sdílené parametry shodné), integrační `KontraktDatAgendTests` (položky, které čtou prvky, ve správném tvaru), `TestovaciLinkyTest` (linky testů v datech jsou), zlaté odtisky všech čtyř agend JŘ a snímek výstupu agend.
- **Snímek výstupu je sonda, ne test** (výstup závisí na datech dev serveru): schéma, parametry a ukázka všech agend z registru a `GetData` linek z `KontraktAgendTest.LinkyAgend` - 45 souborů, 16 MB.
  Spouští se `TD_SNIMEK_AGEND=<složka> dotnet test TemplateDesigner.Tests --filter SnimekAgendNaostro` (u Jana `C:\Users\miesb\source\EDISON2-snimek-agend`, mimo repo); první běh uloží, další porovná bajt po bajtu.
  Porovnává se **před krokem** na nezměněném kódu (data mění i kolegové; když se liší, sejme se znovu) a **po kroku** (musí vyjít stejně).
  Všechny odtisky i snímek jedou jen s výchozími parametry; rozdílový linkový JŘ kryjí jen umělá data a jedna dvojice linek.
- **Pořadí kroků:** nejdřív R5 (parametry vývěsek do jedné definice).
  Sedm společných parametrů (`PORADI_STRANEK`, `TISK_SIPKY`, `TISK_PROJETYCH_ZAST`, `CASOVA_OSA`, `HODINY_OD`, `HODINY_DO`, `ROZTAHNOUT_NA_VYSKU`) je znak po znaku shodných; AD je má proložené asi 15 vlastními a musí je přidávat ve stávajícím pořadí, jinak se panel parametrů přeskupí.
  Pak doslovné kopie, jedna skupina = jeden commit + snímek: vývěsky `AddAtribut`, `AddPoznamkaZastavky`, `AddZona`, `BuildOdjezd`, `BuildSkupinaDnu`, `ConvertSpecialniZnak`, `IntToHex2`, `RgbToHex`, `GetTableTemplate(s)`; linkové `BuildPoznamka`, `BuildSkupina`, `FormatCas`; AD `NazevLinky`, `NormalizLinku`, `PrvniLinka`, `CasHodnota`, `GetAvailableSkupiny`/`GetAvailableInstances`.
  Pak sjednocení z odstavce výš (platnost, omezení 14 a 15).
  Metody, které čtou nastavení linky (`NactiNastaveniNazvu`, `ZonaBezPismen`, `TiskBarevSpoju`), až po backlogu „Úklid nastavení tisku v jízdních řádech".
- **Pojistky můžou při přesunu kódu ztichnout:** drift testy čtou jen `Agendy/*.cls` bez podsložek, test schématu vynechává `BaseAgenda.cls`, pořadí polí se kontroluje po souborech a `ServerSideParamsDriftTests` hledá doslova `AddBoolParam(arr,`.
  Před prvním přesunem do pomocné třídy rozhodnout, kam třídy půjdou, a drift testy upravit v témže commitu; serverové kontroly (`KontraktAgendTest`, `KontraktDatAgendTests`) přesun přežijí.
