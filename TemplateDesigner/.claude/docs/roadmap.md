# Roadmap

> **Stav k 2026-07-07.** **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet — patří do archivu.

---

## 📋 Aktivní / nejbližší kroky

**Co reálně čeká (pořadí = priorita):**

1. **🔴 Výběr skupiny linek uživatelem (AD + MHD) — DO PRODUKCE** ⬅️ InstancePicker dnes bere jen
   **jednu** skupinu (default první nezablokovaná — `GetDefaultSkupinaLinekID`). Server už `SKUPINA_LINEK_ID`
   param přijímá (`GetAvailableInstances`/`GetData`), chybí **UI pro výběr skupiny** — musí být pro
   zastávkový JŘ (AD) **i** MHD. Bez toho uživatel nevidí linky z ostatních skupin. Přidat výběr skupiny
   do výběru instancí (dropdown/picker) + propagovat `SKUPINA_LINEK_ID`.
2. **Governance šablon — implementace** — model rozhodnut (hybrid MLINE + per-firma), zbývá picker obou zásuvek + zámek MLINE + distribuce (viz produkční sekce níže).
3. **Feature tracky dle priorit** — zastávkové business tickety / Fáze C-F / import PDF (viz backlog).

**Backlog / budoucí velké tracky:**
- **Zastávkový JŘ business** (P1–P3 nové požadavky + parkované helpdesk A2-A5/A6/B4) → viz sekce „Zastávkový JŘ — business logika" níže
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

## 🚀 Produkční / provozní — otevřené (na poptávku + rozhodnutí governance)

> Zámek struktury šablony i provozní hygiena hotové → archiv. Vydání v1 = standardní EDISON release
> (deploy není roadmap úkol). Zbývají jen provozní featury na poptávku + rozhodnutí governance šablon.

### Provozní featury (odložené, až s reálnou poptávkou)

- **Headless tisk (A3)** — „vytiskni linku X" z modulu přes **PrintEngine fasádu** (šablona+data→PDF
  bez stavu editoru; T2 udělalo půlku render-na-snapshotu). Dnes tisk jen v designeru. Gate R4/R7.
- **Hromadný výstup + e-mail (A4)** — sloučení hromadného exportu do 1 PDF (`MergePDFFiles`) + odeslání e-mailem.
- **Logging/telemetrie pro podporu (F3)**.
- **CI/CD linting šablon (F4)** — Caché job validující šablony před deployem (nice-to-have).

### Governance šablon — hybrid (M-line distribuuje + zákazníci vlastní)

> **Model rozhodnut** (2026-07-07): dvě zásuvky v `^ALVA.TemplateDesigner.Templates` — IdFa `MLINE`
> (naše systémové, sdílené, read-only) + per-firma (zákaznické). Distribuce sáhne jen na větev MLINE,
> zákaznické se nikdy nepřepíšou; zákazník „Uložit jako" → vlastní editovatelná kopie. Master = dev/release,
> verzování zatím ne. Plný koncept: [`governance-sablon-koncept.md`](governance-sablon-koncept.md).

**Otevřená implementace (pořadí):**
1. **Picker ukáže obě zásuvky + otevření MLINE** — `ListTemplates` sjednotit session IdFa + `MLINE`;
   `LoadTemplate` umět zdrojovou IdFa. Odznak „systémová" u MLINE.
2. **Zámek MLINE (server-side)** — `SaveTemplate`/`DeleteTemplate` odmítne zápis do `MLINE` mimo
   distribuční cestu; Ctrl+S na otevřené MLINE šabloně → vynutí „Uložit jako" (nová kopie pod firmou).
3. **Distribuce** — export/import MLINE šablon jako JSON v release (ne GOF). Váže výchozí šablony
   per agenda (knižní JŘ šablona v repu, zapojení chybí).
4. **Navazující featury** — per-instance přiřazení (linka → výchozí, `InstanceDefaults`) · kategorie +
   popis v UI · audit log změn + ECL právo „pouštět designer".

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
| C8 | **Dedup Caché tiskových enginů** (*z auditu H1*) — `CasHodnota` bere `"|"` jinak v `KnizniJrAd` vs `ZastavkoveJrAd`, různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery. Vyextrahovat sdílené `^JRL` readery + `Build*` do `BaseAgenda`/`JrShared`. **Caché analog R1 extrakce jádra** (C3 řeší .NET render-side, C8 Caché data-side). | korektnostní riziko; dělat s ostrým knižním JŘ, ne dopředu | L |
| C9 | **Testy Caché tiskových enginů** (*z auditu H2*) — `KnizniJr*` (~1975 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje. Smoke + frozen-input gold testy; bezpodmínečný gold gate (zmrazit vstup do izolovaného ns). **= T1b.** | důvěra v datovou paritu; s ostrým knižním JŘ | M |

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
