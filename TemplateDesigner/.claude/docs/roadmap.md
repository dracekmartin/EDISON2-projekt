# Roadmap

> **Stav k 2026-07-13.** **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet — patří do archivu.

---

## 📋 Aktivní / nejbližší kroky

**Co reálně čeká (pořadí = priorita):**

1. **Vydání verze** — zbytek [`pre-version-checklist.md`](pre-version-checklist.md): push (pre-push testy),
   publish blok E + smoke, release kroky, interní poznámka + zákaznický e-mail.
2. **Governance šablon** — v1 HOTOVO (embed → MLINE → picker 2 sekce, zámek MLINE, kurátorovaná zastávková AD). Budoucnost: samostatný distribuční kanál + další šablony MHD/linkový (viz produkční sekce).
3. **Feature tracky dle priorit** — zastávkové business tickety / Fáze C-F (viz backlog).

*(Import PDF „projít před produkcí" — HOTOVO 2026-07-13: Sonnet 5, anti-halucinace, retry,
standardní dialog — viz changelog. Zbývá jen dlouhodobé ladění → sekce „Import PDF" níže.)*

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
- **Výkon MHD tisku** (audit C-b) — memoizace `GetNazevHrl`/`PopisDopravce`/ENUMS barvy dle AD vzoru; behavior-preserving, gold-safe. Až výkon reálně bolí (velcí dopravci).
- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) — architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** — tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".
- **UX drobnosti z pre-version průchodu (2026-07-13):** snap/indikace čtverce při resize obdélníku
  (à la Figma — threshold + vizuální hint, S–M) · **per-šablona hodnoty tiskových parametrů**
  (PrintDialog volba „načíst z aktuální šablony"; dnes per-agenda — chce design kam uložit
  a co má přednost; navazuje na existující TODO „extrakce na Template/agenda-config", M)
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`legacy-nahrada.md §7`](legacy-nahrada.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní — otevřené

1. **Headless tisk (A3)** — „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe — v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** (MHD + linkový) — mechanismus embed→MLINE hotový, jen kurátorovat + přidat JSON do `SystemTemplates/`. (Táž práce evidovaná i v Fázi C+D: krok 44b / C4.)
4. **Zákaznická dokumentace na wiki (post-release)** — 2 dokumenty, seedy HOTOVÉ a aktuální
   (2026-07-13): `uzivatelska-prirucka.md` (část I obecné ovládání + část II zastávkový JŘ;
   nové agendy = nové části) + `plan-a-zmeny.md` („Novinky a plány"). Zbývá: obrázky + publikace.

---

## 🅿️ Zastávkový JŘ — business logika (otevřené body)

> **Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
> [`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md). Zde jen **otevřená
> akční práce**, seřazená pro postupné odbavování. Vel.: S = do hodiny, M = půl dne, L = víc dní.

> Malé dispatch kandidáty (TR2/TR3/TR4) hotové 2026-07-10 → archiv/customer-requirements.
> **TR8 („<1" místo „0") park** (analýza 2026-07-10): naše časy jsou kumulativní, 0-segment se tiskne
> jako opakované číslo, ne „0". Per-segment sloupec TD nemá. Blokuje potvrzení od TRANSDEV
> (který sloupec — legacy „Průběhy"?). Detail → `customer-requirements §TR/8`.
> **TR2** implementováno (opt-in `SOUHRN_NA_ZNAMENI`), ⚠️ čeká SK ověření kódování na-znamení — kolega.

### Větší — chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | Caché filtr |
| TR7 | Výběr sloupců variant jízdních dob (stabilní číslování, per-písmeno) | M | relates L1 multi-varianta (`VARIANTY_TRAS`) |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | nový datový model |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | Caché-input (Studio JŘ) |
| TR5 | Definice cíle — střední (per zast.×směr) + ideál (DB cílů) | M–L | datový model; *(min „cíl per směr" = hotovo přes `POPIS_SMER`=konečná)* |
| A5+ | Odsazení zastávky per-směr / per-Tč pásmo (#7937) | M | `^JRL` dta7 je single-per-point → změna zdroje + editor (base AD hotový) |
| B4 | Per-sloupek zastávky → samostatná vývěska (#8828) | ~0.5–1d | datový průzkum (kde je sloupek v `ALVA.MHD.*`) + business rozhodnutí; 100 % Caché |

---

## 🟠 Fáze B: Element flexibility — jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony). Hotová část
(conditional formatting, počítaná pole, min/max, versioning framework) → archiv.

**Fixní (statická) tabulka — analýza + plán HOTOVY (2026-07-12), implementace odložena na poptávku faktur/F3:**

> Rozhodnutí z analýzy: **koncept implementovat** (FastReport/Stimulsoft svět našich uživatelů Table
> objekt čeká; tabulka dává logiku, kterou skupina elementů principiálně neumí — **reflow podmíněných
> řádků** (skrytý řádek zkolabuje, díra nezůstane), **auto výška řádku** (obsah odtlačí řádky pod sebou),
> strukturální editace (insert řádku, sloupcový resize), sémantika pro Caché distribuci + AI).
> Pro JŘ není potřeba (bloky záhlaví = 2×3 bez podmínek) → **nedělat hned, ale s poptávkou faktur / F3**.
> Průzkum stavu: model+render Fixed módu KOMPLETNÍ a na sdíleném jádru (kaskáda, CellRenderer,
> ResolveWidths), 5-tab `TableEditorDialog` funkční vč. „Ze šablony…" na hotový `GetTableTemplate`
> kontrakt (agendy už nabízejí ZAHLAVI_LINKY/PLATNOST_DOPRAVCE/FAKTURA_*).

- **Krok 1 — střízlivé oživení (S–M):** paleta „Tabulka (pevná)" + factory (`Mode=Fixed`, Mode v UI
  zamčený — dynamickou dělá `DynamicTableElement`); opravy nálezů: `Clone()` zapomíná `SummaryRows`,
  `RowVM.ToModel()` zahazuje per-cell `Format`+`Style`; render dorovnat na `ColumnRenderer.DrawRow`
  (sjednotí mřížku, auto-scale, **předpoklad pro merge**); drift-test allowlist zúžit.
- **Krok 2 — UX modernizace + merge (M, s poptávkou):** inline editace buněk na plátně (dvojklik,
  à la F2 na TextElementu — standardní pocit z FastReportu, řeší „5 tabů je moc"), redukce dialogu
  na 2-tab vzor, **ColSpan** merged cells (aditivní JSON, span-aware DrawRow). **RowSpan vědomě
  odložen** (vzácný, těžký — spanování výšek × zebra × VisibilityExpression) — až na poptávku / F3.

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
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** — vývěsky
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
[`legacy-nahrada.md`](legacy-nahrada.md) §3.1 + §5 etapa 4.

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

> Pre-produkční průchod HOTOVÝ 2026-07-13 (Sonnet 5, anti-halucinace, retry, standardní dialog,
> jednostránkový JŘ import) → changelog. Zde jen dlouhodobé ladění:

| Krok | Co | Stav |
|---|---|---|
| 32 | **Obecný import: detekce tabulek z mřížky čar** — místo 50 čar + 20 textů jeden tabulkový element. ⚠️ **Gated na oživení fixní tabulky (Fáze B)** — cíl importu statické tabulky je Fixed mód, který je dnes uspaný; do té doby nedělat | ⬜ |
| 33 | JŘ import: **ladění na širší sadě JŘ** (jiné layouty než KV_DP vzor — jiná města, SK, netypické legendy) | 🟡 |

Křivky/Bézier v obecném importu — low-prio, bez poptávky.

---

## 🤖 AI integrace — v pozadí

> **Sekundární, ne core.** Aktuální funkce (pomocník, generátor šablon, AI Vision import PDF — vše Sonnet 5
> přes EDISONAI/mlweb spravovaný klíč) zůstávají funkční; další rozšíření až budou reální uživatelé
> s potřebou. JSON šablona + DataSchema jsou stabilní kontrakt pro AI — rozšíření kdykoli bez breaking
> changes. **Princip: copilot, ne autopilot.**

**Otevřené (post-release):** ostrá zkouška EDISONAI cesty bez lokálního klíče u první firmy ·
potvrzení `source: "templatedesigner"` u autora mlwebu (checklist §4) · zveřejnění generátoru šablon
(menu dnes jen pro vývojáře) až po otestování na reálných zadáních.

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
