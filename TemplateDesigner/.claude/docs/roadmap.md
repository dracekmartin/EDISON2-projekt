# Roadmap

> **Stav k 2026-07-03.** Jen otevřená práce. Hotové etapy → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md).
>
> **Hotovo (nejnovější, detail archiv):** **EDISON→TD launch integrace** (A1 session + A2 startup agenda
> z kontraktu, Martin) · zastávkový JŘ (AD) **1:1 kompletní** (track uzavřen) · **DynamicTableElement** ·
> Wave 4 funkčně hotová · architektonický track T1a/T1b/T2/T3 · rozhodnutí R1–R7.

---

## 📋 Aktivní / nejbližší kroky

**Co reálně čeká (pořadí = priorita):**

1. **🟢 Produkční připravenost — launch integrace HOTOVÁ, zbývá RUČNÍ release deploy.** EDISON→TD
   launch (`A1` session z EDISONMAIN2 kontraktu + `A2` startup agenda dle uzlu stromu) **✅ hotové
   (Martin, v main)**. **Zbývá už jen ruční nasazení do verze/release:** build TD.exe z main →
   `C:\Edison` (D3) + Caché třídy na zákaznický server jako součást verze (D4). Tisk (`A3`) +
   systémové šablony (`B`) odloženo. Cesta k pilotu (R7) → odemkne i parkovaný AD zbytek.
2. **Architektonický track** — `T1b` gold regrese ✅ · `T6` 2/4 ✅ (ScheduleDetectorTest, MessageBox).
   Zbývá `T4` god-objects (průběžně), `T5`/`T6` drobnosti. Nízká priorita, průběžně.
3. **AD zastávkový JŘ** — ✅ **1:1 hotový, track uzavřen** (2026-07-03). Zbytek (vizuál legendy, N5/N15)
   navázán na pilot — odemyká reálná zákaznická konfigurace. Jan projde s kolegou.
4. **Wave 4 EDInputForm** — **funkčně hotová** (elementy/sekce/stránka/parametry). Zbývá jen `W4-10/11`
   (inline panel migrace + cleanup — velký refaktor, záměrně na konec, low-prio).

**Backlog / budoucí velké tracky:**
- Nové z prezentací **P1–P3** (neanalyzováno, před implementací ticket)
- Helpdesk odložené **A2-A5 / A6 / B4** (datový průzkum / diskuze s kolegy)
- **Fáze B** element flexibility (merged cells, conditional formatting)
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR — velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- Import PDF doladění · UX polish · virtualized canvas (zoom >500%) · AI (v pozadí)

**Otevřené otázky k rozhodnutí s týmem:** Q1–Q6 (viz sekce níže).

**Docs úklid (drobné, low-prio):**
- ⬜ **changelog archivace** — ~1600 řádků; květnové Wave 1-3 → `changelog-archive-2026-05.md` (mechanické).
- 🟡 **ořez `cache-database-linky.md §6`** — až po uzavření otevřených otázek (FTL/Plk/CNZ, skupina linek, tarif).

---

## 🆕 Nové požadavky z prezentací (P1–P3) — backlog, neanalyzováno

Z živých prezentací 2026-06-24. Před implementací dohledat ticket + přesné zadání. Detail:
[`customer-requirements-zastavkovy-jr.md §P`](customer-requirements-zastavkovy-jr.md).

| # | Co | Odhad | Pozn. |
|---|---|---|---|
| P1 | Ruční poznámky do patičky JŘ (volný text / pole „Poznámky") | XS–M | `TextElement` v patičce funguje; M jen pokud param-driven |
| P2 | Sloučení linek se stejným posledním trojčíslím (i různá Tč / více dopravců) → 1 výstup | L | Agenda + InstancePicker + agregace `POPIS_DOPRAVCE`; chce analýzu legacy |
| P3 | Logická imposice 2×A5 oboustranně na A4 (N-up + duplex/brožura) | M–L | „Sheet layout" vrstva nad PageLayoutEngine; `RenderPage` už bere offset |

---

## 🔧 Architektonický track — technické dluhy T1–T6 (z analýzy 2026-07)

> Zdroj: [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md) §2.3.
> Optika: regresní pojistky + zdraví kódu před růstem plochy. Běží paralelně; vybírat tak, aby
> nekolidovaly s rozpracovanými soubory. **T1a/T2/T3 ✅, T1b částečně ✅** (archiv 2026-07-02).

| ID | Co | Stav |
|---|---|---|
| T1b | **Caché testy** — **základní gold regrese ✅ 2026-07-03** (`ZastavkoveJrAdGoldTest`: 2 gold linky 0365001+0365005, digest = hrubé počty + MD5 celého GetData výstupu, v pre-push). ⚠️ pevně vůči **CB_CSADAD** datům (per-firma namespace) — data drift = falešný pád (přegeneruj digest), jiný server bez CB_CSADAD → skip. **Robustní verze** (zmrazit vstup `^JRL`/`^JRZ`/`^JRSET` do izolovaného namespace, data-drift-proof) odložena. | 🟡 základ hotový, robustní odloženo |
| T4 | **God objects strangler** — `MainWindow.xaml.cs` (~2,7k) + `TemplateDesignerViewModel` (~2,5k): pravidlo „nové featury = nová služba/soubor" + postupné řezy (Export ✅, Instance, TemplatePersistence); R4 fix `TableEditorDialogViewModel:609` (WPF UI ve VM); rename `MainWindowViewModel.cs` → název třídy | ⬜ průběžně |
| T5 | **Zbylé duplikace** — `EDList/Instance.cls:113` hardcoded mirror agend → napojit na `AgendyRegistr`; `ZastavkoveJrAd.GetMockData` → statický mock (nezávislý na firemních datech) | ⬜ malé |
| T6 | Drobnosti — ✅ `ScheduleDetectorTest.cs` odstraněn (2026-07-03), ✅ `MessageBox` v MainWindow odstraněn (2026-07-03, s vestigiálním menu „Vlastnosti textu"; App.xaml.cs `MessageBox` = legitimní bootstrap/crash fallback, zůstává). Zbývá: `architecture.md` nezná `PdfImport/`+`Services/` (~7k LOC); per-frame `new SKPaint` v Render() běžných elementů | ⬜ zbývají 2 drobnosti |
| — | **StopList kaskáda testy** (analogie DepartureGridCascadeTests) | ⬜ |

---

## 🧭 Architektonická rozhodnutí R1–R7 — stav a nejbližší akce

> Plný text (co znamená / proč / možnosti): [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).
> Tady jen stav + co z toho aktuálně plyne. Diskuze 2026-07-03 upřesnila R1–R3.

| # | Rozhodnutí | Stav | Kdy nejpozději |
|---|---|---|---|
| R1 | **Sdílené řádkové jádro** (šířky sloupců / měření / render řádek-po-řádku jednou, v `Rendering/` vedle CellRenderer; elementy dodávají jen odlišnosti). **Rozhodnuto 2026-07-03: zárodek jádra = DynamicTableElement** (nejčistší řádková tabulka); **extrakce až u LineTable** (3. konzument ukáže, co je opravdu společné — ne dřív, přepis gold-ověřeného StopListu by do té doby nesl jen riziko). Pozn.: jádro ≠ PageLayoutEngine — jsou to dvě patra (sekce/stránky × vnitřek elementu) | ✅ rozhodnuto (extract-at-LineTable) | design LineTable (Fáze C+D) |
| R1a | **Průchod hotový (2026-07-03):** vnitřek je čistě rozvrstvený — model sloupců / měření (`ComputeColumnWidths`+`NaturalHeight` čisté funkce) / render odděleny, žádná vazba na dialog, žádná duplikace měření (Render i CalculateHeightFromData sdílí `NaturalHeight`). **Jediný nález:** DynamicTable kreslí řádky vlastní per-buňka smyčkou místo sdíleného `Rendering/ColumnRenderer.DrawRow` (StopList ho už používá) → drobná per-element duplikace. Zarovnat na `DrawRow` je levné + behavior-preserving (DynamicTable je čerstvá + má testy), ale **záměrně odloženo za blok A** (integrace/deploy má přednost). Vyšší jádro (šířky+měření+orchestrace) dál až u LineTable per R1. | 🟡 nález hotový, srovnání odloženo | za blokem A |
| R2 | **Layout zodpovědnosti**: engine = sekce/stránky (+ imposice jako vrstva NAD ním), element = jen svůj vnitřek, nahoru hlásí výsledek. **Model potvrzen 2026-07-03 (= dnešní stav, nic se nemění).** Zbývá jediné: navrhnout rozšíření hlášení z „jsem vysoký X mm" na „vykreslil jsem řádky 1–40, zbývá 12" (partial-fit) — jednou, pro všechny tabulkové elementy stejně | 🟡 model ✅, partial-fit rozhraní ⬜ | před Fází C+D |
| R3 | **Datový kontrakt agend**: základ už existuje (`cache-api.md` + `EDControls/DOCS/01-wire-formats.md` — tvary metod, čistý JSON, konvence). **Chybí 2 sekce**: (a) povolený seznam **prezentačních nápověd** — princip „volitelné + sémantické kde to jde" (`ZVYRAZNENI: 0–5`, typ řádku, opakuj-na-další-straně, přes-celou-šířku; přímé `FORE_COLOR`/`BACK_COLOR` legalizovat jen pro legacy paritu), v1 krátký + rozšíření při F1 dle RG usage; (b) **pravidlo evoluce**: kontrakt se mění jen přidáváním klíčů, změna významu zakázána | ⬜ ~0,5 dne do cache-api.md | před F2 (RG bridge) |
| R4 | **Headless tisk**: pilot = .exe na stanici, ale render oddělit **PrintEngine fasádou** (šablona+data→PDF bez DesignerState; T2 udělalo půlku) → později serverová služba = zabalení, ne přepis | ⬜ | návrh bloku A |
| R5 | **Fonty embedded v aplikaci** (`SKTypeface.FromStream`), nikdy spoléhat na instalaci ve Windows — u Timetable fontu je chybějící font věcná chyba JŘ, ne kosmetika | ⬜ | start Fáze C+D |
| R6 | **Systémové šablony distribuovat s release** (read-only, verzované s buildem), DB jen zákaznické → verzní matice šablona×exe nevznikne | ⬜ | návrh bloku B |
| R7 | **Pilot-first**: zastávkový JŘ k reálnému zákazníkovi (TRANSDEV) přes celou trubku (A1–A3 + min. B + deploy) dřív, než se migrují další agendy; slíbený termín „polovina 2026" nastal | ⬜ = další velký blok | teď |

---

## 🅿️ AD zastávkový JŘ — uzavřeno, zbytek parkován na pilot

1:1 hotový; **track uzavřen 2026-07-03** (detail → [archiv](roadmap-archive.md) session 2026-07-03).
Zbytek **odemyká reálný pilot** (konkrétní zákaznická konfigurace/data): vizuál legendy POZNAMKY,
AD-N5 (`MHD_NEZNZ123`), AD-N15 (prázdninové NZ, kód hotový/čeká data) + odložené helpdesk **A2-A5**
(#7937 indent), **A2-A6** (#7959 filtr neobsluhovaných), **A2-B4** (#8828 per-sloupek) — datový
průzkum / diskuze s kolegy. Podklad: [`ad-jr-nastaveni-linky-analyza.md`](ad-jr-nastaveni-linky-analyza.md), [`cache-database-linky.md §10`](cache-database-linky.md).

---

## 🔵 Caché Migrace — zbývající vlny

**Wave 1/2/3 ✅** (archiv). **Wave 4 W4-9 ✅** (StopList/DepartureGrid/dynamická tabulka — archiv 2026-07-02).

### Wave 4 — Form-driven UI (EDInputForm) — 🟡 zbytek

Generický `ElementEditDialog` přes reflexi (field key = property path na C# elementu) + Caché schema
class per element (`ALVA.TemplateDesigner.EDInputForm.<Typ>Edit`). **Přidání elementu = 1 schema class +
1 řádek ve switchi.** ⚠️ **Schema-duplikace dluh** (Caché schema = manuální mirror PropertyDescriptors) —
akceptováno kvůli patchovatelnosti; memory `project_edinputform_schema_duplication.md`.

**Kandidát na EDInputForm** (pravidlo, 2026-06-12): ✅ formuláře s pevným server-definovaným schématem
(vlastnosti elementu/sekce/stránky, parametry agendy); ❌ triviální 1-pole confirm, dialogy s živě
počítaným náhledem (Normalizovat pozice), plně dynamické formy bez schématu.

Elementy/sekce/**stránka** (`PageEdit.cls`)/parametry ✅ hotové. Zbývá jen:

| Krok | Co | Stav |
|---|---|---|
| W4-10 | Inline `PropertiesPanel` → EDInputForm (live mode) — plný dedup PropertyDescriptors. Velký refaktor | ⬜ odložené |
| W4-11 | Cleanup: smazat PropertyDescriptor systém až po 100% migraci | ⬜ na konec |
| W4-12 | (volitelné) EDInputForm wire `$lb()` OUT → JSON envelope; collapsible groups | ⬜ |

### Wave 5 — Business logic per agenda (paralelně)

Čisté rozdělení agenda-specific logiky do Caché tříd (`AgendaAPI` base + per-agenda impl:
`GetSchema/GetData/GetMockData/GetTableTemplates/GetTableTemplate`). Base + MHD + FAKTURY ✅. Nové
agendy (Fáze C/D/E) pattern využijí. Shared helpery extrahovat do base až budou 2+ konzumenti (ne dopředu).

| Krok | Co | Stav |
|---|---|---|
| W5-2/3/4 | Fáze C (meziměstský) / D (knižní) / E (vozový) — využijí pattern | ⬜ (viz Fáze C-E níže) |
| W5-5 | Refaktor: extract `AgendaAPIBase` helpery až budou 2+ konzumenti | ⬜ |

---

## 🟠 Fáze B: Element flexibility

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony). Může běžet paralelně.

**Template versioning — ✅ framework hotový** (H-18, Wave 3 + T3 guardy). H-19 (konkrétní V1→V2 migrace)
zůstává no-op: V1 v DB neexistují (ověřeno 2026-07-02), zbytkové riziko jen staré JSON soubory na disku.

| Oblast | Krok | Co |
|---|---|---|
| Merged cells | 35/36 | `ColSpan`/`RowSpan` na `TableCell` (JSON + render skip) + merge editor v dialogu |
| Conditional formatting | 37/38 | `ColorRules` na `TableColumn` (`{expression, foreColor, backColor}`) + vyhodnocení v print engine |
| Layout-reservation | — | Deklarativní „pokud property X=true → rezervuj Y mm" (dnes ad-hoc u šipky StopListu) |

---

## 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) — pravděpodobně JEDEN track

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** —
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Krok 43 začíná
> designem sdíleného jádra (StopList + DynamicTable + LineTable), ne kódem elementu.

Legacy `TiskJR` generuje vývěsní i knižní JŘ z téhož enginu → C + D je nejspíš jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

**Prerekvizity (pořadí):** (1) symbolový font Timetable_nove(+SK) do Skia + převodní tabulka (66+ symbolů,
TTF v `C:\DEV\EDISON\TiskJR\_ruzne`) — prerekvizita jakéhokoli knižního/výlepového JŘ; (2) agenda
`KnizniJrAd`; (3) `LineTableElement` + layout rozšíření (flow paketů, „dotisk poznámek z minulé stránky",
**auto-fit fontu na šířku** — dnes je ComputeFontScale jen výškový); (4) per-linka šablony (viz B4).

| Krok | Co | Stav |
|---|---|---|
| 42 | Analýza referenčních výstupů (vývěsní + knižní PDF) + wire formát | ⬜ |
| 42b | Symbolový font Timetable do Skia + převodní tabulka | ⬜ |
| 43 | `LineTableElement` + layout rozšíření (flow paketů, auto-fit šířka, dotisk legendy) | ⬜ |
| 44 | Caché `KnizniJrAd` — GetData/GetMockData (Wave 5), oracle `mkPLinWin` | ⬜ |

---

## 🟠 Fáze E: Vozový JŘ + Relace

Layout TBD — hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-schedule-generators.md`](legacy-schedule-generators.md). **Relace (TiskJRRELACE)** — vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case (Q3)**.

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ — layout + wire formát | ⬜ |
| 49 | `VehicleScheduleElement` (pravděpodobně víc elementů) | ⬜ |
| 50 | Caché `VozovyJrAPI` — GetData/GetMockData + async (velký objem dat) | ⬜ |

---

## 🟠 Fáze F: Náhrada REPORTGENERATOR (univerzální sestavy)

> ⚠️ **Gate před F2: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** —
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Největší kus náhrady — ne kvůli featurám (TD je napřed), ale kvůli **ekosystému ~28 agend** s RG
kontraktem (`GetSeznamSad`/`GetSadu`/`GetData`, `~`-tokenový stream). Nasazení: 33k textových elementů,
774 tabulek. **Ne big-bang** — etapizovat. Detail:
[`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md) §3.1 + §5 etapa 4.

| Krok | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage** — legacy `Main.cls:161-188` drží reálná čísla použití; seřadit agendy | ⬜ první (levný) |
| F2 | **Bridge vs čisté agendy (Q2)** — bridge `LegacyRgAgenda` (překlad `~`-protokolu na TD JSON, všech 28 najednou s omezenou věrností) vs čisté reimplementace; doporučení: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + Fáze B): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout | ⬜ L |
| F4 | **Výstupní kanály fáze 2** — Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk (Q1) | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** — per-jazyk kopie provázaná na CS zdroj (Q5) | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** — grafy (12×), AZTEC (2×), calculated (2× — výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout (Q4) | ⬜ validace |

---

## ❓ Otevřené otázky z analýzy 2026-07 (k rozhodnutí s týmem)

| # | Otázka | Dopad |
|---|---|---|
| Q1 | **Epson znakový tisk** — migruje do TD, nebo zůstane v legacy TISK? | Ne-grafický výstupní kanál — F4 |
| Q2 | **RG bridge vs čisté agendy** — akceptovatelná dočasná věrnost bridge? | F2, tvar celé Fáze F |
| Q3 | **Relace (SK zákazník)** — živý use case? | Fáze E scope |
| Q4 | **Grafy/AZTEC/checkbox/calculated** — tvrdá závislost? (usage 12/2/8/2) | F6 škrtnutí |
| Q5 | **Jazykové varianty šablon** — i pro JŘ (pohraniční vývěsky), nebo jen RG? | F5 priorita |
| Q6 | ~~V1 šablony~~ — v DB neexistují (2026-07-02); zbývá potvrdit staré JSON soubory na disku | H-19 uzávěrka |

---

## 🔵 Produkční / integrační připravenost (pro nasazení k zákazníkům)

> **Optika:** ne featury, ale „aby to šlo nasadit a integrovat". **Launch integrace HOTOVÁ
> (2026-07-02, Martin):** TD bere session + agendu z EDISONMAIN2 launch kontraktu, fallback appsettings.
>
> **Kritická cesta:** ~~A1/A2 launch~~ ✅ → **D deploy (ruční release, jediné co reálně zbývá pro v1)**
> → pak B systémové šablony · C H-19 migrace · A3 end-user tisk.

### 🟢 A. EDISON integrace — launch HOTOVÝ (Martin, v main), zbývá deploy + (později) tisk

> ⚠️ **Gate pro A3/A4: R4 (headless tisk + PrintEngine fasáda), R7 (pilot-first).** Legacy vzor: RG má
> headless CLI `AUTOEXPORT/AUTOPRINT` (`MainRun.vb`). Vrstvy 1-4 TD to umí, chybí jen entrypoint.

| ID | Co | Stav |
|---|---|---|
| A1+A2 | **EDISON launch — session + startup agenda z kontraktu** ✅ (Martin, detail archiv 2026-07-03). Předvybraná konkrétní **instance/linka** ⬜ — v1 nepotřebuje (uživatel vybere z pickeru, který ukazuje firmu ze session). | ✅ hotovo |
| A3 | **End-user tiskový vstup** — headless tisk (vrstvy 1-4) z modulu / „vytiskni linku X"; dnes print jen v designeru. Legacy: RG CLI. **Záměrně odloženo** (v1 = jen návrhář). | ⬜ odloženo |
| A4 | **Výstup fáze 1** — sloučení hromadného exportu do 1 PDF (`MergePDFFiles`) + odeslání e-mailem (společné všem 3 legacy) | ⬜ M |

### 🟠 B. Šablony pro zákazníky (governance + distribuce)

> ⚠️ **Gate: R6 (systémové šablony s release, DB jen zákaznické — eliminuje verzní matici)** — může
> zásadně zmenšit B1/B2. Legacy vzor „MlineSestava" (vendor ID ≥ 1000 read-only × zákaznické < 1000),
> release pipeline `ExportReportToGlb`→`ImportTransferGlobal`→`AktualizujAgendu`, audit log, ECL právo.

| ID | Co | Stav |
|---|---|---|
| B1 | **Systémové/zamčené šablony** (M-line tvoří + distribuuje, zákazník read-only) — dnes jen per-IDfa bez „zamčené" | ⬜ |
| B2 | **Výchozí sady šablon per agenda** (MHD/AD JŘ/faktury) + distribuční pipeline v release | ⬜ |
| B3 | Kategorie + popis šablony v UI (metadata už drží Author/Created/Modified/Version) | ⬜ |
| B4 | **Per-instance přiřazení šablony** — linka → výchozí šablona (legacy: jiná A4 / A5 + ~17 vlastností, `LoadNastaveniProTisk`). `InstanceDefaults` rozšířit; potřebné pro A2/A3 launch | ⬜ |
| B5 | Audit log změn šablon + právo „pouštět designer" (ECL) — legacy parita, malé | ⬜ |

### 🟠 C. Bezpečnost dat · 🟡 D. Konfigurace/deploy · 🟢 E/F. Multi-tenant / provoz

| ID | Co | Stav |
|---|---|---|
| C1 | **H-19 konkrétní migrace** (V1→V2) — framework ✅, migrace nenapsaná (no-op) | 🟡 |
| C2 | Full migration pipeline pro každou breaking change | ⬜ |
| D1 | Embedded `CB_CSADAD` = jen **standalone-dev fallback**; při launchi z EDISONu firma jde ze session (A1 ✅), embedded default se přebíjí | ✅ řeší A1 |
| D3 | **← RUČNÍ RELEASE KROK:** build TD.exe z `main` (má A1/A2) přes `publish.ps1` → `C:\Edison\TemplateDesigner.exe` → **přidat do verze/release**. Zbývá zapojit do release pipeline. | ⬜ ruční nasazení |
| D4 | **← RUČNÍ RELEASE KROK:** nasadit Caché třídy (agendy, `API.Sablony`, EDInputForm schémata) na zákaznický server **jako součást verze** — proces/pipeline ověřit. | ⬜ ruční nasazení |
| E2 | Per-zákazník enablement agend (vidí jen své) | ⬜ odloženo (YAGNI) |
| F3 | Logging / telemetrie pro podporu | ⬜ ověřit |
| F4 | CI/CD linting šablon (Caché job před deployem) | ⬜ odloženo |

> Hotovo: D2 API klíč ✅, E1 per-IDfa storage ✅, F1 error handling ✅, F2 testy ✅, F5 výkon ✅.

---

## 🟡 Import PDF — doladění

| Krok | Co | Stav |
|---|---|---|
| 32 | Detekce tabulek z mřížky čar (3a) — místo 50 čar + 20 textů → jeden `TableElement` | ⬜ |
| 33 | JŘ-aware mód (3d) — AI Vision pipeline + `AiGuidedScheduleBuilder` fungují, **ladění na širší sadě JŘ** | 🟡 |

Hotovo: režim 1 (1:1 import) ✅, 3c AI post-processing (statické texty → `{PLACEHOLDER}`). 3b křivky/Bézier low-prio.

---

## 🟢 UX polish (otevřené)

- **Stylový pod-formulář (rozšířit + uklidit)** — 🟡 prototyp 2026-06-22 (1 zóna: `StyleEditDialog` +
  EDInputform `ButtonClicked` event, Odjezdy „Styl záhlaví" → „Upravit styl…"). **Zbývá:** (1) rozšířit
  na všechny zóny Odjezdů (DataStyle/MinuteStyle/Anotace) + StopList → hlavní „Obecné" = scaláry + tlačítka
  „Styl…"; (2) uklidit `StyleEdit.cls` (nepoužitá varianta A) + zvážit budoucnost `StyleFields`.
- **Font follow-ups** — (1) live varování „nenainstalovaný font" (dnes `FontResolver` tiše fallbackuje na
  Arial → chce warning kanál v EDInputformu); (2) rozšířit `Template.DefaultTextStyle` z „jen font" na
  size/barvu/rámeček — jen na vyžádání (YAGNI, model pole má, chybí UI).
- **Ad-hoc kontrola přetečení v tisku** — samostatné tlačítko „Zkontrolovat přetečení" v `PrintDialog`
  (fetch dat + počet bez generování PDF). Zatím YAGNI.
- **Sjednotit editor sloupců** — `IColumnRowVM` + `ColumnListEditor` sdílí StopList/DepartureGrid/dynamická
  tabulka. TableElement (Fixed, dormant) má vlastní DataGrid editor — sjednotit jen pokud bude potřeba.

### Tabulkové featury (backlog, bez customer demand)

Grouping v tabulce (`GroupHeader`/`GroupFooter`/`Summary`) · Computed fields (`CELKOVA_CENA = CENA * MNOZSTVI`) ·
Vnořené datasource (`_rows` v `_rows` — faktura → položky → daně) · Min/max počet řádků dynamic tabulky.

---

## 🟡 Virtualized canvas rendering (zoom >500%)

`SKElement` alokuje byte buffer celé plochy per repaint (zoom 1000% A4 ≈ 365 MB/frame — hardware-bound
bottleneck SkiaSharp Views.WPF). **Workaround:** zoom max 500% (~93 MB). **Proper (~4-6h, až bude potřeba):**
virtualizace — SKElement ve velikosti viewport (~1600×900), custom scroll + hit-test coordinate transform.
**Risk:** mouse coord transformace napříč všemi interakcemi (drag, rubber band).

---

## 🤖 AI integrace — v pozadí

> **Sekundární, ne core.** Aktuální funkce (generování šablony, PDF import enrichment, vision pipeline)
> zůstávají funkční; další rozšíření až budou reální uživatelé s potřebou. JSON šablona + DataSchema jsou
> stabilní kontrakt pro AI — rozšíření kdykoli bez breaking changes. **Princip: copilot, ne autopilot.**

**Hotovo:** AI Assistant (generování šablony z popisu) · schema injection · AI Vision pipeline (JŘ z PDF obrázku).

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
