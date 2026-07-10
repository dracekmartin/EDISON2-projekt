# Roadmap

> **Stav k 2026-07-09.** **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet — patří do archivu.

---

## 📋 Aktivní / nejbližší kroky

**Co reálně čeká (pořadí = priorita):**

> **Tabulkové jádro — levné švy (pětice #1–#5) HOTOVO 2026-07-08** → detail v
> [`roadmap-archive.md`](roadmap-archive.md). Zbývající navazující (aditivní, bez migrace) → backlog níže.

1. **🔍 Audit před produkcí — projít checklist a odbavit** (sekce níže) — nálezy z komplexního
   auditu 2026-07-09; nejdřív blok A (datový kontrakt zastávkového JŘ).
2. **Logická imposice 2×A5 → A4** (N-up + duplex/brožura) — „Sheet layout" vrstva nad PageLayoutEngine (`RenderPage` už bere offset). Odhad M–L.
3. **Import PDF — doladění** (nice-to-have, projít před produkcí) — AI Vision pipeline funguje; chce ladění na širší sadě JŘ + detekce tabulek z mřížky čar. Detail: sekce „Import PDF" níže.
4. **Governance šablon** — v1 HOTOVO (embed → MLINE → picker 2 sekce, zámek MLINE, kurátorovaná zastávková AD). Budoucnost: samostatný distribuční kanál + další šablony MHD/linkový (viz produkční sekce).
5. **Feature tracky dle priorit** — zastávkové business tickety / Fáze C-F (viz backlog).

---

## 🔍 Audit před produkcí (2026-07-09) — rozhodovací checklist

> Komplexní audit architektury + agend + UI/UX (3 paralelní průzkumy kódu). **Detail každého
> bodu (file:line, lidské vysvětlení, doporučený fix) →
> [`audit-pred-produkci-2026-07-09.md`](audit-pred-produkci-2026-07-09.md).** Projít, do
> sloupce Rozhodnutí dopsat ✅ udělat / ⏭️ později / ❌ ne; hotové řádky přesouvat do archivu.
> Velikost: S = do hodiny, M = půl dne, L = víc dní.

**Blok A — datový kontrakt zastávkového JŘ, skaláry (🔴 před produkcí).** Kolekce prošly
revizí (POZNAMKY/SKUPINY_DNU), skaláry ne — šablona sdílená mezi MHD a AD tiskne u těchto
polí různé věci. Bolí zpětně: šablony si na špatné klíče zvyknou.

| # | Co | Lidsky | Vel. | Rozhodnutí |
|---|---|---|---|---|
| A1 | `POPIS_SMER`: MHD „T/Z" vs AD název konečné → zarovnat MHD na AD/Linkovy (+ doplnit `SMER`) | pole „Popis směru" u MHD vytiskne písmeno místo konečné | S–M | |
| A2 | `NEJEDOU_SPOJE`: MHD text prvních/posledních spojů vs AD nástupiště → rozdělit na `PRVNI_POSLEDNI_SPOJ` + `NASTUPISTE` (aditivně, R3) | jedno pole = dvě různá data podle agendy | S–M | |
| A3 | MHD mock `POPIS_SMER="Bystrc, zoo"` ≠ ostrá data „T" | preview ukazuje jiný obsah než tisk | S (s A1) | |
| A4 | AD emituje pole mimo schéma (`*_CIS`, `VARIANTY_TRAS`, `TRASA_MIN`) → deklarovat/zdokumentovat | agenda posílá data, která autor šablony nemůže objevit | S | |

**Blok B — .NET: viditelné bugy editace + rizika (🟠).**

| # | Co | Lidsky | Vel. | Rozhodnutí |
|---|---|---|---|---|
| B2 | DepartureGrid: overflow varování vs auto-scale počítají jinou přirozenou výšku. **⚠️ NE mechanický fix — nese UX rozhodnutí:** Render záměrně bere scale z font-driven výšky (komentář `DepartureGridElement.cs:514`), aby velká `DataRowHeight` nezmenšovala fonty; overflow detekce bere `layout.RowH`. Sjednocení musí rozhodnout, co u velké `DataRowHeight` vyhraje (shrink fontů vs. varování). | varování a zmenšování si můžou protiřečit | S→M | |
| B3 | `CapabilityCatalog` (AI průvodce) — string heuristika, nezná LineTable → napojit na registr/`AgendaCapabilities` | AI radí jiné elementy, než nabízí paleta | S | |
| B4 | Nový element = ~8 ručně synchronizovaných míst bez strážce → drift test „každý JsonDerivedType má záznam všude" | zapomenuté místo nic nehlásí (B3 je přesně tento drift) | S–M | |
| B6 | Reset stránky: string klíč `"PageSize.Width"` bez pojistky + `async void` | přejmenování pole formuláře reset tiše vypne | S | |
| B7 | R1: první `GetSchema` sync na UI threadu (`RefreshFieldsList`); + `GetAvailableSkupiny` bez cache | otevření šablony cizí agendy na pomalé síti zamrzne UI | M | |

**Blok C — Caché hygiena + výkon (🟠/🟡).**

| # | Co | Lidsky | Vel. | Rozhodnutí |
|---|---|---|---|---|
| C-b | MHD bez memoizace (`GetNazevHrl` ~O(n²), `PopisDopravce` per stránka, ENUMS barvy per anotace) — AD vzor existuje | export velkých MHD firem zbytečně pomalý | M | |
| C-e | AD `GetMockData` čte živá firemní data (vlastní TODO) → statický vzorek | preview u zákazníka závislé na jeho datech; pomalé | S–M | |
| C-f (zbytek) | export `PER_STOP`/`PER_LINE` nefiltrován dle ElementKind (chce plumbing ElementKind do AgendaParams) · `AgendyRegistr` jen přímé subclassy (vědět při C8) | kosmetika / vědět o tom | S | |

**Blok D — UI/UX + dokumentace (🟡 rychlé).**

| # | Co | Lidsky | Vel. | Rozhodnutí |
|---|---|---|---|---|
| D2 | 6 dialogů OK/Save přes code-behind `Click` místo VM Command — sjednotit postupně | proti závaznému vzoru oken | S/dialog | |
| D3 (zbytek) | Dvojí mechanismus renamu agendy (`"MHD"→"ZASTAVKOVY_JR_MHD"` post-deserializační remap vs `KNIZNI→LINKOVY` verzová migrace) — příští rename sjednotit do migračního pipeline | úklid, spustí až další rename | S | |

> **Bezpečný sweep odbaven 2026-07-10** (C-a/C-c/C-d, C-f q→ret, B1, B5, D1, D3-drobné, D4) →
> [`roadmap-archive.md`](roadmap-archive.md). Zbývá viz níže.

**Doporučené pořadí (zbytek):** A1–A4 (kontrakt — jediné, co po nasazení bolí zpětně) → B2 (rozhodnout
UX tenzi) + B7 (zamrzání) → C-e (před nasazením k zákazníkům) → B3+B4 (než poroste plocha agend) →
C-b (výkon MHD) → B6 + D2 + C-f/D3 zbytky průběžně. Mimo checklist (dlouhodobé, bez akce): god
objects (T4), sjednocení cache patternů elementů (při R1 extrakci jádra), MHD legacy MAC volání
(patří do C8) — detail v auditním dokumentu.

---

### 📥 Backlog / budoucí velké tracky

> Audit stavu v1 (scan 2026-07-07) — P-A/P-B/P-C hotové, P-D rehomed do produkční sekce →
> **detail v [`roadmap-archive.md`](roadmap-archive.md)** (Session 2026-07-08). Konsolidace jádra
> hotová, plocha může růst.

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ — business logika" níže
- **Fáze B — odložené:** **oživení fixní tabulky** (paleta + Fixed editor) + **merged cells** (gated na ni).
  (Conditional formatting seznam pravidel = HOTOVO #1, 2026-07-08.)
- **Zvýraznění řádků — navazující (aditivní, bez migrace):** **per-sloupec render** `HighlightRule.TargetColumn`
  (model už pole nese, dnes render barví celý řádek) + editace `TargetColumn` v dialogu · barva textu / kurzíva
  pravidla. Na poptávku.
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR — velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`analyza-stav-a-legacy-nahrada-2026-07.md §7`](analyza-stav-a-legacy-nahrada-2026-07.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní — otevřené

1. **Headless tisk (A3)** — „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe — v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** (MHD + linkový) — mechanismus embed→MLINE hotový, jen kurátorovat + přidat JSON do `SystemTemplates/`. (Táž práce evidovaná i v Fázi C+D: krok 44b / C4.)

---

## 🅿️ Zastávkový JŘ — business logika (parkováno na pilot + nové požadavky)

> Otevřená business logika zastávkového JŘ na jednom místě (AD datové jádro už hotové → archiv):
> zbytek gated na pilot + nové zákaznické požadavky z prezentací.

**Odemyká reálný pilot** (konkrétní zákaznická konfigurace/data): vizuál legendy POZNAMKY,
AD-N5 (`MHD_NEZNZ123`), AD-N15 (prázdninové NZ, kód hotový/čeká data) + odložené helpdesk **A2-A5**
(#7937 indent), **A2-A6** (#7959 filtr neobsluhovaných), **A2-B4** (#8828 per-sloupek) — datový
průzkum / diskuze s kolegy. Podklad: [`ad-jr-nastaveni-linky-analyza.md`](ad-jr-nastaveni-linky-analyza.md), [`cache-database-linky.md §10`](cache-database-linky.md).

**TRANSDEV konsolidovaná sada (2026-07-03, k analýze)** — 10 požadavků (SK legislativa + MHD Trenčín);
detail + TD-stav [`customer-requirements-zastavkovy-jr.md §TR`](customer-requirements-zastavkovy-jr.md):
časová os cez polnoc (jen ověřit u klienta) · na-znamení-„všechny" souhrn (pattern jako bezbariér) ·
nástupiště (data `Oznacnik` existují) · auto výška řádku (částečně = auto-scale) · **definice cíle** (datový model, M–L) ·
**barva linky** (bg/text ve vlastnostech linky → volba v šabloně, M) · výběr sloupců jízdních dob (relates L1 multi-varianta) ·
„<1" místo „0" (S) · obrácené poznámkování spojů (M) · skrýt spoje So/Ne/svátky (S–M).

---

## 🟠 Fáze B: Element flexibility — jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony). Hotová část
(conditional formatting, počítaná pole, min/max, versioning framework) → archiv.

**Odloženo (backlog, ne blocker):**
- **Fixní (statická) tabulka — oživení v UI** — `TableElement` Fixed mód existuje v modelu, ale je uspaný
  (`JsonDerivedType` jen pro legacy načtení, není v paletě/UI). Vzkřísit: paleta + Fixed editor (sloupce + řádky
  s `{PLACEHOLDER}` ze skalárů). Užitečné pro strukturované sestavy (faktury, bloky záhlaví/platnost).
- **Merged cells** (`ColSpan`/`RowSpan` + merge editor) — **gated na fixní tabulku**: slučování patří
  strukturovaným tabulkám (DynamicTable má uniformní datové řádky). Vrátit se spolu s fixní tabulkou.

---

## 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) — pravděpodobně JEDEN track

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** —
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Krok 43 začíná
> designem sdíleného jádra (StopList + DynamicTable + LineTable), ne kódem elementu.

Legacy `TiskJR` generuje vývěsní i knižní JŘ z téhož enginu → C + D je nejspíš jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

**Prerekvizity (pořadí):** (1) symbolový font Timetable_nove(+SK) do Skia — **základ hotový 9.7.** (embed
TTF + locale-aware `FontResolver.SymbolFontFamily`, single-char symboly zastávkového JŘ); pro linkovou část
zbývá převodní tabulka `GSId` (66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`) na multichar/kompozity; (2) agenda
`KnizniJrAd`; (3) `LineTableElement` + layout rozšíření (flow paketů, „dotisk poznámek z minulé stránky",
**auto-fit fontu na šířku** — dnes je ComputeFontScale jen výškový); (4) per-linka šablony (viz B4).

> Pilotní část hotová (`KnizniJrAd`/`KnizniJrMhd` datové jádro, element, editor, paleta) → archiv
> 2026-07-04 + detail `linkovy-knizni-jr-implementace-analyza.md` §11-12. Otevřené kroky:

| Krok | Co | Stav |
|---|---|---|
| 42b | Symbolový font Timetable do Skia (= C2 níže) — **zastávkový JŘ hotový 9.7.**; zbývá linková část (LineTable `ZNAK` sloupce, multichar/kompozity) | 🟡 zastávkový ✅, linkový ⬜ |
| 43 | `LineTableElement` layout rozšíření (flow paketů, auto-fit šířka, dotisk legendy = C3 níže) | 🟡 element hotový (standalone); layout rozšíření ⬜ |
| 44b | Výchozí **linková** šablona do distribuce — mechanismus embed→MLINE hotový (governance v1); zbývá kurátorovat + přesunout `linkovy-jr-ad-vychozi.json` do `SystemTemplates/` | 🟡 mechanismus ✅, linkový JSON ⬜ |

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

> Pilot uzavřen 2026-07-04 jako „testovací v1" (detail → archiv). Dál dle priorit, ne nutně hned zde.

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C2 | **Symbolový font Timetable_nove(+SK)** do Skia (krok 42b, gate R5). **Zastávkový JŘ hotový 9.7.** — embed `TimeTable2.ttf`/`TimeTable2SK.ttf`, locale-aware `FontResolver.SymbolFontFamily`, single-char `SpecialniZnak` → piktogram (NotesLegend + DepartureGrid); na-znamení `x/y/z` = text. **Zbývá linková část**: `ZNAK` sloupce LineTable (multichar/kompozitní symboly, převodní tabulka `GSId` 66+ symbolů) | vizuální parita; bez linkové části knižní/výlepový JŘ „tiše lže" u dopravních symbolů (R5) | M→S |
| C3 | **2D bin-packing / `Strih`** (krok 43 + gate R1/R2, *přesunuto z layout tracku 2026-07-07*) — široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků. Součást: **auto-fit fontu na šířku** (dnes jen výškový `ComputeFontScale`), flow paketů, „dotisk zbytku legendy na další stránce". **Zde proběhne R1 extrakce sdíleného řádkového jádra** = zavedení **row-plan vrstvy** (`List<RowSpec>`: druh řádku data/header/groupHeader/groupFooter/subrow + buňky → **jedna sdílená kreslicí smyčka** nad `ColumnRenderer.DrawRow`), ať „řádkový layout" (grouping, page-spanning, per-typ-řádku, merged) přestane být per-element `Render` smyčka (dnes N míst → pak 1 datové tvarování). **LineTable = prototyp** — už heterogenní řádky skládá inline. Pozn.: sdílené low-level (buňka/sloupec/kaskáda/šířky) i řádkové podmínky (viditelnost/zvýraznění) jsou **hotové** (pětice levných švů 2026-07-08); row-plan je poslední zbývající řádková vrstva. Zastávkový JŘ to nepotřebuje (DepartureGrid = málo sloupců). | nutné pro reálný tisk MHD linek; největší kus, dělá se jednou pořádně | L |
| C4 | **Rozšíření výchozích šablon na další agendy** — mechanismus embed→MLINE hotový (governance v1, zastávková AD); přidat MHD + linkový JSON do `SystemTemplates/` | provozní; malý | S |
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
