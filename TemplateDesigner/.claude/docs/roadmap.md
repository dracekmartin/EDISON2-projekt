# Roadmap

> **Stav k 2026-07-03.** Jen otevřená práce. Hotové etapy → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md).
>
> **Hotovo (nejnovější, detail archiv):** **EDISON→TD launch integrace** (A1 session + A2 startup agenda
> z kontraktu, Martin) · zastávkový JŘ (AD) **1:1 kompletní** (track uzavřen) · **DynamicTableElement** ·
> **Wave 4 uzavřena** (W4-10 inline panel na EDInputForm 2026-07-03) · architektonický track (malé T ✅) · rozhodnutí R1–R7.

---

## 📋 Aktivní / nejbližší kroky

**Co reálně čeká (pořadí = priorita):**

1. **🔴 Zámek struktury šablony před produkcí (blok PA níže)** ⬅️ **aktivní** — věci, které pokud
   neuděláme teď, vynutí breaking změnu JSON + migraci v příští verzi. Levné, jednorázové. Detail +
   grounded analýza: [`produkce-pripravenost-2026-07-07.md`](produkce-pripravenost-2026-07-07.md).
2. **🟢 Produkční připravenost — launch HOTOVÁ, zbývá RUČNÍ release deploy** (tým/proces, ne kód): build
   TD.exe z main → `C:\Edison` (D3) + Caché třídy na zákaznický server (D4). A1/A2 launch ✅ (Martin, v main).
   Tisk (`A3`) + systémové šablony (`B`) odloženo. Cesta k pilotu (R7).
3. **AD zastávkový JŘ** — ✅ **1:1 hotový, track uzavřen**; zbytek parkován na pilot.

**Rozhodnuté akční kroky (rozhodnutí padlo, implementace čeká na blok/pilot):**
- **Tisk = „shortcut" z konkrétní agendy bez plného návrháře (A3, dříve R4).** Uživatel v EDISONu
  u agendy/linky klikne „Tisk" → otevře se lehký tiskový formulář (parametry sestavy + výběr
  instance) → PDF. NE plný návrhář. Technicky přes **PrintEngine fasádu** (šablona+data→PDF bez
  stavu editoru; T2 udělalo půlku render-na-snapshotu). Pilot = render na stanici; server později
  = zabalení fasády do služby, ne přepis.
- **Koncept centrální správy šablon (chybí — navrhnout).** Dnes žijí šablony per-IDfa v Caché
  (`^ALVA.TemplateDesigner.Templates`, picker + filtr agendy). Chybí **přehled/správa napříč všemi
  agendami a firmami** — zejména pro M-line: jak udržovat systémové šablony centrálně, verzovat je,
  vidět „co kde je". Souvisí s R6 (systémové vs zákaznické) i s distribucí (blok B). Navázat: centrální
  registr/přehled (nejspíš DB-backed) + jasné oddělení systémové (M-line, read-only u zákazníka) vs
  zákaznické. **Nejdřív koncept, pak implementace** — teď jen zapsat jako otevřený návrhový bod.

**Layout tabulek — track uzavřen** (detail → archiv 2026-07-06/07): „Sekce vyšší než stránka" ✅
+ **DynamicTable = varianta A** (jako zastávky: `Height` = strop, auto-scale, bez řádkového
stránkování); **R2 (svislé stránkování) vědomě odstraněno** (spec zůstává v `r2-partial-fit-kontrakt.md`).
Zbývající **C3 (vodorovný `Strih` / 2D bin-packing)** přesunut k linkovému JŘ → viz Fáze C+D
(potřebné až pro ostrý knižní/linkový JŘ, ne pro zastávkový).

**Backlog / budoucí velké tracky:**
- **Zastávkový JŘ business** (P1–P3 nové požadavky + parkované helpdesk A2-A5/A6/B4) → viz sekce „Zastávkový JŘ — business logika" níže
- **Tech dluh (ne blocker, průběžně):** T1b-robustní gold test (zmrazit vstup `^JRL`/`^JRZ`/`^JRSET` do
  izolovaného namespace — data-drift-proof) · T4 god-objects strangler (postupné řezy `MainWindow.xaml.cs`
  + `TemplateDesignerViewModel` na služby). Malé T kousky ✅ (archiv 2026-07-03).
- **Fáze B** — z větší části ✅ (conditional formatting + počítaná pole + min/max, archiv 2026-07-05).
  Odloženo: **merged cells** (gated na Fixed tabulky), **layout-reservation** (YAGNI) — viz sekce Fáze B.
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`). (Computed fields + min/max ✅ hotovo, archiv 2026-07-05.)
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR — velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- Import PDF doladění · AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`analyza-stav-a-legacy-nahrada-2026-07.md §7`](analyza-stav-a-legacy-nahrada-2026-07.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční připravenost — pre-produkční checklist

> Grounded analýza (4 průzkumy kódu, 2026-07-07): [`produkce-pripravenost-2026-07-07.md`](produkce-pripravenost-2026-07-07.md).
> Do produkce jde nejdřív **zastávkový JŘ (AD)**. **Blok PA zamyká strukturu šablony** — priorita
> #1; ostatní strukturu nezamykají (dohnatelné kdykoli). Postupně odbavovat, hotové → archiv.

### 🔴 PA. Zámek struktury šablony (PŘED prvním produkčním uložením)

Pokud se odloží a natečou reálné šablony do DB → budoucí breaking change + (někdy neproveditelná) migrace.

| ID | Co | Náprava | Cena | Stav |
|---|---|---|---|---|
| PA1 | **Klíče sloupců (`StopColumn.Key`) = magic-string literály** ve switchích, ale persistují se do JSON. Přejmenování → uložená šablona se **tiše rozbije**. | povýšeno do `Models/ColumnKeys.cs` (vzor `ElementTypes`) + `ColumnKeysTests` zamyká wire hodnoty | nízká | ✅ 2026-07-07 |
| PA2 | **Domnělé dva modely** zvýraznění řádku. **Nález:** `List<ColorRule>` na DepartureGrid byl **mrtvý kód** (nikde nekonzumován) — odstraněno. Živý je jen 1-pravidlový `RowHighlightExpression`; multi-pravidlo půjde **aditivně** (budoucí `RowColorRules`), strukturu nezamyká. | dead-field cleanup + docstringy fixnuté; `ColorRule` ponechán jako rezervovaný budoucí model | nízká | ✅ 2026-07-07 |
| PA3 | **`TableElement` (dormant) × `DynamicTableElement`** — překrývající se schémata; riziko šablon s „mrtvým" typem. | továrna už `TableElement` nevyrábí (mrtvé větve odebrány) + tripwire test + docstring; třída/`JsonDerivedType`/editor zachovány pro legacy read + budoucí oživení Fixed tabulky | nízká | ✅ 2026-07-07 |
| PA4 | **Verzování šablon.** Nález: migrátor je hotový **i otestovaný** (9 testů); „bump na 1.1.0 teď" bylo špatné doporučení (proti konvenci *bump jen pro breaking change*; navíc falešné WasMigrated). Reálná mezera = jen dokumentace. | zdokumentováno: flat→style jako nemigrovatelná + rozhodnutí NEbumpovat (`template-schema-versions.md`); kosmetika „1.0"/„1.0.0" záměrně neřešena (funkčně irelevantní) | nízká | ✅ 2026-07-07 |
| PA5 | **Deserializace tiše polyká neznámá pole** (`UnmappedMemberHandling` nenastaveno, `TemplateSerializer.cs:154-168`) → silent corruption. | aspoň **logovat** neznámá pole při loadu (nefailovat) | nízká | ⬜ ⬅️ |

### 🟠 PB. Provozní nasazení (strukturu NEzamyká — dohnatelné kdykoli)

| ID | Co | Stav |
|---|---|---|
| PB1 | **Commitnuté dev credentials + MLP token** (`Mlp "167"`, login `martin.hubata`, interní IP) v trackovaném `templatedesigner.appsettings.json`, zapékají se do .exe → vyprázdnit/neutralizovat baseline (session jde z launch args) | ⬜ triviální |
| PB2 | **Neexistuje ověřený Caché deploy proces** — jen ruční import; MCP `cache_compile` na TD zdroje lže „up-to-date" → aspoň dokumentovaný manuální checklist (které třídy, pořadí, ověření). Souvisí s D4. | ⬜ |
| PB3 | **`SelfContained=false`** → produkce vyžaduje předinstalovaný .NET 10 runtime; rozhodnout self-contained vs runtime jako prerekvizita. Souvisí s D3. | ⬜ |
| PB4 | Build vázaný na stroj (`HintPath C:\Edison\*.dll`, ruční `publish.ps1`, žádná CI) → dokumentovat prerekvizity | ⬜ |
| PB5 | Newtonsoft.Json 7.0.1 (NU1903) vynuceno legacy MLRest — akceptovaný dluh, jen evidovat (memory `project_mlrest_newtonsoft_debt.md`) | ✅ evidováno |
| PB6 | Test třídy v produkční assembly (`ScheduleDetectorTest`) — úklid (= T6) | ⬜ |
| PB7 | **Doc drift:** CLAUDE.md říká .NET 9 (reálně **.NET 10**), PdfPig 0.1.9 (reálně **0.1.13**) → sjednotit stack tabulku | ⬜ triviální |

### 🟡 PC. Governance šablon (odloženo — nezamyká JSON strukturu, jde do Caché metadat)

M-line dnes **nemůže bezpečně aktualizovat systémovou šablonu u zákazníka** (per-IDfa storage, žádné
systémové/zákaznické rozlišení, žádné zámky). Pro v1: pokud M-line nedistribuuje udržované šablony,
ruční Import JSON stačí. Jinak minimum z B1/R6 před vznikem zákaznických úprav. Mapuje na blok B (B1/B2/B5)
+ R6 + „koncept centrální správy šablon" (viz akční kroky výše). Vše ⬜.

---

## 🧭 Architektonická rozhodnutí R1–R7 — ✅ vyřešeno (→ archiv)

Celá rozhodovací tabulka R1–R7 uzavřena 2026-07-04/05 — detail v
[`roadmap-archive.md`](roadmap-archive.md) (session 2026-07-04/05). Shrnutí: R1+R1a **implementováno**
(sdílené tabulkové jádro `Rendering/TableLayout`), R2 (partial-fit) + R3 (prezentační nápovědy)
**kontrakty rozhodnuty** (impl s příslušným trackem), R4/R5 **akční kroky** (tisk-shortcut, embedded
fonty — viz výše), R7 = pilot (aktivní kroky). **Zbývá jen R6** — systémové šablony s buildem
(rozhodnuto, impl v bloku B) + navázaný otevřený bod „koncept centrální správy šablon" (viz akční kroky).
Plný text principů: [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

---

## 🅿️ Zastávkový JŘ — business logika (parkováno na pilot + nové požadavky)

> Vše k business logice zastávkového JŘ na jednom místě: uzavřený AD track (zbytek gated na pilot)
> + nové zákaznické požadavky z prezentací.

**AD 1:1 hotový, track uzavřen 2026-07-03** (detail → [archiv](roadmap-archive.md) session 2026-07-03).
Zbytek **odemyká reálný pilot** (konkrétní zákaznická konfigurace/data): vizuál legendy POZNAMKY,
AD-N5 (`MHD_NEZNZ123`), AD-N15 (prázdninové NZ, kód hotový/čeká data) + odložené helpdesk **A2-A5**
(#7937 indent), **A2-A6** (#7959 filtr neobsluhovaných), **A2-B4** (#8828 per-sloupek) — datový
průzkum / diskuze s kolegy. Podklad: [`ad-jr-nastaveni-linky-analyza.md`](ad-jr-nastaveni-linky-analyza.md), [`cache-database-linky.md §10`](cache-database-linky.md).

**Nové požadavky z prezentací (P1–P3)** — z živých prezentací 2026-06-24, **neanalyzováno**; před
implementací dohledat ticket + přesné zadání. Detail: [`customer-requirements-zastavkovy-jr.md §P`](customer-requirements-zastavkovy-jr.md).

| # | Co | Odhad | Pozn. |
|---|---|---|---|
| P1 | Ruční poznámky do patičky JŘ (volný text / pole „Poznámky") | XS–M | `TextElement` v patičce funguje; M jen pokud param-driven |
| P2 | Sloučení linek se stejným posledním trojčíslím (i různá Tč / více dopravců) → 1 výstup | L | Agenda + InstancePicker + agregace `POPIS_DOPRAVCE`; chce analýzu legacy |
| P3 | Logická imposice 2×A5 oboustranně na A4 (N-up + duplex/brožura) | M–L | „Sheet layout" vrstva nad PageLayoutEngine; `RenderPage` už bere offset |

**TRANSDEV konsolidovaná sada (2026-07-03, k analýze)** — 10 požadavků (SK legislativa + MHD Trenčín);
detail + TD-stav [`customer-requirements-zastavkovy-jr.md §TR`](customer-requirements-zastavkovy-jr.md):
časová os cez polnoc (**✅ hotové B3, ověřit u klienta**) · na-znamení-„všechny" souhrn (pattern jako bezbariér) ·
nástupiště (data `Oznacnik` existují) · auto výška řádku (částečně = auto-scale) · **definice cíle** (datový model, M–L) ·
**barva linky** (bg/text ve vlastnostech linky → volba v šabloně, M) · výběr sloupců jízdních dob (relates L1 multi-varianta) ·
„<1" místo „0" (S) · obrácené poznámkování spojů (M) · skrýt spoje So/Ne/svátky (S–M).

---

## 🔵 Caché Migrace — zbývající vlny

**Wave 1/2/3/4 ✅** (archiv). Form-driven UI (EDInputForm) všude, kde uživatel přímo edituje — elementy,
sekce, stránka, parametry (modály) i **inline panel** (W4-10, 2026-07-03).

**Pravidlo — kandidát na EDInputForm** (evergreen): ✅ formuláře s pevným server-definovaným schématem;
❌ triviální 1-pole confirm, dialogy s živě počítaným náhledem, plně dynamické formy bez schématu.
⚠️ Schema-duplikace dluh (Caché schema = manuální mirror) akceptován, memory `project_edinputform_schema_duplication.md`.

Nedokončeno **záměrně** (mimo aktivní práci): **W4-11** smazat PropertyDescriptory = neproveditelné
(drží je FormatToolbar + multi-edit + fallback modál + AI catalog → paralelní systém); **W4-12** wire
`$lb()`→JSON + collapsible groups = volitelný EDControls concern.

### Wave 5 — Business logic per agenda (paralelně)

Čisté rozdělení agenda-specific logiky do Caché tříd (`AgendaAPI` base + per-agenda impl:
`GetSchema/GetData/GetMockData/GetTableTemplates/GetTableTemplate`). Base + MHD + FAKTURY ✅. Nové
agendy (Fáze C/D/E) pattern využijí. Shared helpery extrahovat do base až budou 2+ konzumenti (ne dopředu).

| Krok | Co | Stav |
|---|---|---|
| W5-2/3/4 | Fáze C (meziměstský) / D (knižní) / E (vozový) — využijí pattern | ⬜ (viz Fáze C-E níže) |
| W5-5 | Refaktor: extract `AgendaAPIBase` helpery až budou 2+ konzumenti | ⬜ |

---

## 🟠 Fáze B: Element flexibility — z větší části hotová

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony).

**Hotovo ✅ (archiv 2026-07-05):** conditional formatting (řádková viditelnost + zvýraznění přes sdílený
`RowConditions`, DynamicTable/StopList/LineTable) · počítaná pole (aritmetika + `TableColumn.Expression`)
· min/max řádků. **Template versioning** framework hotový (H-18); H-19 no-op (V1 v DB neexistují).

**Odloženo (backlog, ne blocker):**
- **Merged cells** (`ColSpan`/`RowSpan` + merge editor) — **gated na oživení Fixed tabulky**: slučování
  patří strukturovaným tabulkám, DynamicTable má uniformní datové řádky (dává smysl jen skupinové
  záhlaví / souhrn). Vrátit se, až se budou dělat Fixed tabulky.
- **Layout-reservation** (deklarativní „když X → rezervuj Y mm") — **YAGNI**: dnes jediný konzument
  (šipka StopListu má ad-hoc gutter). Udělat, až vzniknou 2–3 konzumenti; interní hygiena, ne user featura.

- **Rozšíření conditional formatting** (seznam pravidel místo 1, barva textu, per-sloupec/buňka) —
  odloženo na produkci (rozhodnutí 2026-07-05).

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
| 42 | Analýza referenčních výstupů (vývěsní + knižní PDF) + wire formát | ✅ 2026-07-04 (`linkovy-knizni-jr-implementace-analyza.md`) |
| 42b | Symbolový font Timetable do Skia + převodní tabulka | ⬜ (ZNAK hodnoty ve wire = legacy klíče, fallback text) |
| 43 | `LineTableElement` + layout rozšíření (flow paketů, auto-fit šířka, dotisk legendy) | 🟡 element hotový (standalone, R1 extrakce jádra až při ostré verzi); layout rozšíření ⬜ |
| 44 | Caché `KnizniJrAd` — GetData/GetMockData (Wave 5), oracle `mkPLinWin` | ✅ 2026-07-04 — 22/24 linek 1:1 vs gold (multi-firm diff); zbylé odchylky viz analýza §12 |
| 44b | Výchozí šablona `templates/knizni-jr-ad-vychozi.json` → zapojit do distribuce (B2/R6) | 🟡 šablona v repu, zapojení ⬜ |
| 44c | Caché `KnizniJrMhd` — MHD analogie (legacy `LinkyPrintFormatJR.MakeData`), stejný wire | ✅ 2026-07-04 datové jádro (CB_DP 1001 ověřeno strukturálně); gold diff ⬜ (legacy MHD větev na dev padá — nutný dump wrapper) |
| 45 | EDInputForm + 2-tab dialog (Obecné + Sloupce) pro `LineTableElement` — sdílený `ColumnListEditor`/`CellStyleEditor` | ✅ 2026-07-04 (`LineTableColumnsEditorDialog`, kolekce `Columns : List<StopColumn>`) |
| 46 | Kontextová paleta JŘ elementů dle agendy (KNIZNI_* ↔ ZASTAVKOVY_*) | ✅ 2026-07-04 |

### 🧪 Pilot uzavřen 2026-07-04 — „testovací v1" knižního/linkového JŘ

**Stav:** AD i MHD knižní JŘ mají hotové datové jádro + element + editor, funkčně a datově ověřené
(AD 22/24 linek 1:1 vs gold přes 4 firmy; MHD strukturálně na CB_DP 1001). Konzistentní se
zastávkovými elementy (stejné sloupcové API, EDInputForm, 2-tab dialog, kontextová paleta).
**Uzavřeno jako pilot** — dál se postupuje dle priorit, ne nutně hned tady.

**Vědomě odložené body (z pilotu vyplynulé, seřazené dle hodnoty pro ostré nasazení):**

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C1 | **MHD gold-oracle diff** — dump wrapper okolo `MakeData` (legacy `LoadData` MHD větev na dev padá kvůli neúplnému `in()`), pak stejný multi-linkový diff jako u AD | MHD je zatím jen strukturálně ověřené; nejlevnější krok k plné důvěře v datovou paritu | S |
| C2 | **Symbolový font Timetable_nove(+SK)** do Skia (krok 42b, gate R5) — embed TTF + převodní tabulka `GSId` (66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`). Dnes se `ZNAK` kreslí jako textový klíč (bezbariér, projíždí, dny…) | vizuální parita; bez něj JŘ „tiše lže" u dopravních symbolů (viz R5) | M |
| C3 | **2D bin-packing / `Strih`** (krok 43 + gate R1/R2, *přesunuto z layout tracku 2026-07-07*) — široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků. Součást: **auto-fit fontu na šířku** (dnes jen výškový `ComputeFontScale`), flow paketů, „dotisk zbytku legendy na další stránce". **Zde proběhne R1 extrakce sdíleného řádkového jádra** (StopList + DynamicTable + LineTable). Zastávkový JŘ to nepotřebuje (DepartureGrid = málo sloupců). | nutné pro reálný tisk MHD linek; největší kus, dělá se jednou pořádně | L |
| C4 | **Zapojení výchozích šablon do distribuce** (B2/R6) — dnes ruční Import JSON; + výchozí šablona pro MHD (zatím jen AD) | provozní; malý | S |
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) — legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** — legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | — |
| C7 | **Rozdvojení příjezd/odjezd — jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |

Detail datových odchylek a mezer viz [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §11–12.

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
