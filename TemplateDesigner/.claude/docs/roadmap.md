# Roadmap

> **Stav k 2026-07-07.** **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet — patří do archivu.

---

## 📋 Aktivní / nejbližší kroky

**Co reálně čeká (pořadí = priorita):**

1. **v1 je kódově připravená k vydání** — vydání = standardní EDISON release (build TD.exe → `C:\Edison`
   + kompilace Caché tříd na server), ne roadmap úkol. Viz blok PB.
2. **Governance šablon (blok PC níže)** ⬅️ **rozhodnout scope pro v1** — systémové vs zákaznické, distribuce.
3. **Feature tracky dle priorit** — zastávkové business tickety / Fáze C-F / import PDF (viz backlog).

**Backlog / budoucí velké tracky:**
- **Zastávkový JŘ business** (P1–P3 nové požadavky + parkované helpdesk A2-A5/A6/B4) → viz sekce „Zastávkový JŘ — business logika" níže
- **Tech dluh (ne blocker, průběžně):** T1b-robustní gold test (zmrazit vstup `^JRL`/`^JRZ`/`^JRSET` do
  izolovaného namespace — data-drift-proof) · T4 god-objects strangler (postupné řezy `MainWindow.xaml.cs`
  + `TemplateDesignerViewModel` na služby).
- **Fáze B — odložené:** **merged cells** (gated na Fixed tabulky), **layout-reservation** (YAGNI),
  rozšíření conditional formatting (seznam pravidel) — viz sekce Fáze B.
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR — velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- Import PDF doladění · AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`analyza-stav-a-legacy-nahrada-2026-07.md §7`](analyza-stav-a-legacy-nahrada-2026-07.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční připravenost — PB/PC (jedno místo)

> Grounded analýza: [`produkce-pripravenost-2026-07-07.md`](produkce-pripravenost-2026-07-07.md).
> Do produkce jde nejdřív **zastávkový JŘ (AD)**. **Kritická cesta v1 = PB release deploy** (PC odloženo).
> (Blok PA — zámek struktury šablony — vyřešen, viz archiv.)

### 🟠 PB. Provoz / nasazení — jen odložené (release = standardní EDISON ops)

> **Release deploy není TD-specifická práce:** build TD.exe → `C:\Edison` + kompilace Caché tříd na
> server jde po stejné trati jako zbytek EDISON suity. Launch integrace hotová (Martin); .NET 10 runtime
> je prerekvizita celé suity (EDISONMAIN2/EDControls taky `net10`). Drobná hygiena (dev creds z baseline
> → `local.json`, mrtvé detector-scratch soubory, doc drift .NET 10 / PdfPig) hotová 2026-07-07 → archiv.

**Odloženo (v1 nepotřebuje, dle poptávky):** end-user tisk (A3 — headless „vytiskni linku X" přes
**PrintEngine fasádu** šablona+data→PDF bez editoru; gate R4/R7) · hromadný výstup (A4 — merge do 1 PDF
`MergePDFFiles` + e-mail) · logging/telemetrie pro podporu (F3) · CI/CD linting šablon (F4) · per-zákazník
enablement agend (E2, YAGNI).

> Akceptovaný dluh (neakční): Newtonsoft.Json 7.0.1 (NU1903) vynuceno legacy MLRest — memory `project_mlrest_newtonsoft_debt.md`.

### 🟡 PC. Governance a distribuce šablon (odloženo — jde do Caché metadat, NEzamyká JSON)

> M-line dnes **nemůže bezpečně aktualizovat systémovou šablonu u zákazníka** (per-IDfa storage, žádné
> systémové/zákaznické rozlišení, žádné zámky). Pro v1: pokud M-line nedistribuuje udržované šablony,
> ruční Import JSON stačí. Gate **R6** (vendor ID ≥ 1000 read-only × zákaznické < 1000, legacy vzor
> „MlineSestava" + release pipeline) může PC1/PC2 zásadně zmenšit.

| ID | Co | Stav |
|---|---|---|
| PC1 | **Systémové/zamčené šablony** (M-line tvoří+distribuuje, zákazník read-only) — dnes jen per-IDfa bez zámku (bývalé B1) | ⬜ |
| PC2 | **Výchozí sady šablon per agenda** + distribuční pipeline v release (bývalé B2; váže C4 knižní JŘ — dnes jen ruční Import JSON) | ⬜ |
| PC3 | **Koncept centrální správy šablon** — přehled/správa napříč firmami+agendami pro M-line („co kde je", verzovat). Nejdřív koncept, pak impl | ⬜ návrh |
| PC4 | **Per-instance přiřazení šablony** — linka → výchozí šablona (`InstanceDefaults` rozšířit; bývalé B4) | ⬜ |
| PC5 | Kategorie + popis šablony v UI (metadata už drží Author/Created/Modified/Version; bývalé B3) | ⬜ |
| PC6 | Audit log změn šablon + právo „pouštět designer" ECL (bývalé B5) | ⬜ |

---

## 🅿️ Zastávkový JŘ — business logika (parkováno na pilot + nové požadavky)

> Otevřená business logika zastávkového JŘ na jednom místě (AD datové jádro už hotové → archiv):
> zbytek gated na pilot + nové zákaznické požadavky z prezentací.

**Odemyká reálný pilot** (konkrétní zákaznická konfigurace/data): vizuál legendy POZNAMKY,
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
časová os cez polnoc (jen ověřit u klienta) · na-znamení-„všechny" souhrn (pattern jako bezbariér) ·
nástupiště (data `Oznacnik` existují) · auto výška řádku (částečně = auto-scale) · **definice cíle** (datový model, M–L) ·
**barva linky** (bg/text ve vlastnostech linky → volba v šabloně, M) · výběr sloupců jízdních dob (relates L1 multi-varianta) ·
„<1" místo „0" (S) · obrácené poznámkování spojů (M) · skrýt spoje So/Ne/svátky (S–M).

---

## 🔵 Caché Migrace — zbývající vlny

> Wave 1–4 hotové (form-driven UI všude) → archiv. Otevřená je jen Wave 5 (business logic per agenda).

**Pravidlo — kandidát na EDInputForm** (evergreen): ✅ formuláře s pevným server-definovaným schématem;
❌ triviální 1-pole confirm, dialogy s živě počítaným náhledem, plně dynamické formy bez schématu.
⚠️ Schema-duplikace dluh (Caché schema = manuální mirror) akceptován, memory `project_edinputform_schema_duplication.md`.

Nedokončeno **záměrně** (mimo aktivní práci): **W4-11** smazat PropertyDescriptory = neproveditelné
(drží je FormatToolbar + multi-edit + fallback modál + AI catalog → paralelní systém); **W4-12** wire
`$lb()`→JSON + collapsible groups = volitelný EDControls concern.

### Wave 5 — Business logic per agenda (paralelně)

Čisté rozdělení agenda-specific logiky do Caché tříd (`AgendaAPI` base + per-agenda impl:
`GetSchema/GetData/GetMockData/GetTableTemplates/GetTableTemplate`). Nové agendy (Fáze C/D/E) pattern
využijí. Shared helpery extrahovat do base až budou 2+ konzumenti (ne dopředu).

| Krok | Co | Stav |
|---|---|---|
| W5-2/3/4 | Fáze C (meziměstský) / D (knižní) / E (vozový) — využijí pattern | ⬜ (viz Fáze C-E níže) |
| W5-5 | Refaktor: extract `AgendaAPIBase` helpery až budou 2+ konzumenti | ⬜ |

---

## 🟠 Fáze B: Element flexibility — jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony). Hotová část
(conditional formatting, počítaná pole, min/max, versioning framework) → archiv.

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

> Pilotní část hotová (`KnizniJrAd`/`KnizniJrMhd` datové jádro, element, editor, paleta) → archiv
> 2026-07-04 + detail `linkovy-knizni-jr-implementace-analyza.md` §11-12. Otevřené kroky:

| Krok | Co | Stav |
|---|---|---|
| 42b | Symbolový font Timetable do Skia + převodní tabulka (= C2 níže) | ⬜ (ZNAK ve wire = legacy klíče, fallback text) |
| 43 | `LineTableElement` layout rozšíření (flow paketů, auto-fit šířka, dotisk legendy = C3 níže) | 🟡 element hotový (standalone); layout rozšíření ⬜ |
| 44b | Výchozí šablona `templates/knizni-jr-ad-vychozi.json` → zapojit do distribuce (PC2/R6) | 🟡 šablona v repu, zapojení ⬜ |

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

> Pilot uzavřen 2026-07-04 jako „testovací v1" (detail → archiv). Dál dle priorit, ne nutně hned zde.

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C1 | **MHD gold-oracle diff** — dump wrapper okolo `MakeData` (legacy `LoadData` MHD větev na dev padá kvůli neúplnému `in()`), pak stejný multi-linkový diff jako u AD | MHD je zatím jen strukturálně ověřené; nejlevnější krok k plné důvěře v datovou paritu | S |
| C2 | **Symbolový font Timetable_nove(+SK)** do Skia (krok 42b, gate R5) — embed TTF + převodní tabulka `GSId` (66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`). Dnes se `ZNAK` kreslí jako textový klíč (bezbariér, projíždí, dny…) | vizuální parita; bez něj JŘ „tiše lže" u dopravních symbolů (viz R5) | M |
| C3 | **2D bin-packing / `Strih`** (krok 43 + gate R1/R2, *přesunuto z layout tracku 2026-07-07*) — široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků. Součást: **auto-fit fontu na šířku** (dnes jen výškový `ComputeFontScale`), flow paketů, „dotisk zbytku legendy na další stránce". **Zde proběhne R1 extrakce sdíleného řádkového jádra** (StopList + DynamicTable + LineTable). Zastávkový JŘ to nepotřebuje (DepartureGrid = málo sloupců). | nutné pro reálný tisk MHD linek; největší kus, dělá se jednou pořádně | L |
| C4 | **Zapojení výchozích šablon do distribuce** (PC2/R6) — dnes ruční Import JSON; + výchozí šablona pro MHD (zatím jen AD) | provozní; malý | S |
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

## 🟡 Import PDF — doladění

| Krok | Co | Stav |
|---|---|---|
| 32 | Detekce tabulek z mřížky čar (3a) — místo 50 čar + 20 textů → jeden `TableElement` | ⬜ |
| 33 | JŘ-aware mód (3d) — AI Vision pipeline + `AiGuidedScheduleBuilder` fungují, **ladění na širší sadě JŘ** | 🟡 |

3b křivky/Bézier — low-prio.

---

## 🤖 AI integrace — v pozadí

> **Sekundární, ne core.** Aktuální funkce (generování šablony, PDF import enrichment, vision pipeline)
> zůstávají funkční; další rozšíření až budou reální uživatelé s potřebou. JSON šablona + DataSchema jsou
> stabilní kontrakt pro AI — rozšíření kdykoli bez breaking changes. **Princip: copilot, ne autopilot.**

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
