# Roadmap

## 📋 Roadmap

---

### 🔴 Kritické — Specializované JŘ elementy

> **Detailní architektonický dokument:** [`schedule-elements-architecture.md`](schedule-elements-architecture.md)

**Primární architektonická změna** — přidání specializovaných elementů pro tisk jízdních řádů. Nový princip: Caché vrací čistá data + business logiku (JSON), specializované C# elementy (`ScheduleElementBase` → `StopListElement`, `DepartureGridElement`, `NotesLegendElement`, `BookScheduleElement`, `VehicleScheduleElement`) mají vlastní nastavení a komplexní renderovací logiku. PageLayoutEngine zůstává generický beze změn.

#### Dokončené kroky (společný základ)

| Krok | Co | Stav |
|---|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ (indexy, grouping, společné properties) | ✅ hotovo (2026-03-18) |
| 2 | `StopListElement` + `DepartureGridElement` + `NotesLegendElement` — zastávkový JŘ (MHD) | ✅ hotovo (2026-03-18) |
| 3 | Caché `ZastavkoveJrMhdAPI` — `GetData`/`GetMockData` + wire formát (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY) | ✅ hotovo (2026-03-18) |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementů | ✅ hotovo (2026-03-18) |
| 5 | `PrintDialog` — parametry + export reálných dat z Caché (`GetData`) + multi-data PDF | ✅ hotovo (2026-03-18) |
| 6 | `CacheAgendaProvider` — ParseMockData vnořená pole, GetData, BuildRealData | ✅ hotovo (2026-03-18) |

---

#### Fáze A: Odladění MHD zastávkového JŘ s reálnými daty ⬅️ AKTUÁLNÍ PRIORITA

Cíl: end-to-end proof of concept — šablona v designeru → tisk s reálnými daty z Caché → PDF 1:1 odpovídající současnému MHD JŘ. Zpětná vazba od kolegů.

| Krok | Co | Stav |
|---|---|---|
| 7 | Per-column styling framework — CellStyle/ColumnVisualStyle, dva taby v DepartureGroupEditorDialog, InlineTextStyle pro minuty/anotace | ✅ hotovo (2026-03-21) |
| 7a | StopListElement — per-column styling analogicky k DepartureGrid (CellStyle pro záhlaví/data sloupců zastávek) + generalizace shared helpers do ScheduleElementBase + ColorPickerHelper + ActionId routing | ✅ hotovo (2026-03-23) |
| 7b | Ověření parametrů šablony — funguje přepsání element properties z template parameters (CASOVA_OSA, HODINY_OD, HODINY_DO, TISK_PROJETYCH_ZAST, TISK_SIPKY); fix merge parametrů do real data exportu; auto-open PDF; odebrání BAREVNE_ODLISENI + TISK_TARIFU | ✅ hotovo (2026-03-23) |
| 8 | Ladění zastávkového JŘ na reálných datech — edge cases (prázdné směry, speciální znaky, velké linky) | ⬅️ |
| 8a | Sjednocení textových nastavení — `TextStyle` (nullable POCO, CSS cascade) + `ResolvedTextStyle` (SkiaSharp helpery). Migrace: TextElement, TableElement, ScheduleElementBase + 3 JŘ elementy, PdfImport, CacheAgendaProvider. Breaking JSON change (flat props → `style`/`headerStyle`/`dataStyle`) | ✅ hotovo (2026-03-24) |
| 8b | Proporcionální škálování šablony + UX vylepšení — `ScaleTemplateCommand`, `ApplyScale()` na všech elementech, redistribuce výšek sekcí, výchozí nastavení nových elementů, `HourColumnPerGroup` | ✅ hotovo (2026-03-24) |
| 8c | AI-enhanced PDF import — dvoustupňový pipeline (heuristiky → AI enrichment); `AiImportEnricher` s kontextovými prompty; detekce rámečků, loga placeholder, margin fix, TextElement alignment fix | ✅ hotovo (2026-03-25) |
| 8d | AI Vision import pipeline — `AiVisionImportService` (composable prompty), `AiGuidedScheduleBuilder` (PdfPig geometrie), `AiVisionImportPipeline` (orchestrace); per-column font wrapping; section headers auto-hide; CellStyleVM font defaults; shared HttpClient | ✅ hotovo (2026-04-03) |
| 9 | Vizuální shoda s referenčním PDF — porovnání pixel-by-pixel, doladění mezer/fontů/zarovnání | |
| 10 | Fonty pro speciální znaky (①②③ anotace) — font fallback mechanismus v rendereru | |
| 11 | Proof of concept review — zpětná vazba od kolegů, seznam úprav | |

---

#### Fáze B: UX polish + vylepšení importu

Po schválení MHD proof of conceptu. Zaměření na použitelnost designeru a chytřejší import.

| Krok | Co | Stav |
|---|---|---|
| 12 | UX polish — inline text editing (g), zoom persistence (c), MRU šablon, rulers na canvasu | |
| 13 | Import: detekce tabulek z mřížky čar (3a) — místo 50 čar + 20 textů → jeden `TableElement` | |
| 14 | Import: JŘ-aware mód (3d) — dvoustupňový AI+engine import (viz popis níže) | ⬅️ rozpracováno (2026-04-03) — AI Vision pipeline + AiGuidedScheduleBuilder fungují, ladění na širší sadě JŘ |

**Import PDF — aktuální stav:**
- ✅ Režim 1 (deterministický 1:1 import) hotovo (2026-03-13)
- 3a: Detekce tabulek z mřížky čar (viz popis níže)
- 3b: Křivky/Bézier (nízká priorita — zaoblené rohy rasterizovat jako bitmapa)
- 3c: AI post-processing — nahrazení statických textů za `{PLACEHOLDER}`, detekce záhlaví/patičky
- **3d: JŘ-aware import — dvoustupňový AI + engine přístup:**
  - **Stupeň 1 — AI (sémantika):** Claude dostane screenshot PDF + DataSchema agendy. Vrátí sémantickou mapu oblastí: "levý sloupec = StopList, střed = DepartureGrid se 3 skupinami dnů, spodek = NotesLegend". AI řeší **co** je kde, neřeší přesné rozměry.
  - **Stupeň 2 — Engine (geometrie):** Deterministický kód vezme extrahované čáry/texty z PDF + sémantickou mapu od AI. Z průsečíků čar spočítá přesné pozice (X,Y,W,H), z rozestupů textů odvodí HourColumnWidth/MinuteWidth, z textů v záhlaví identifikuje skupiny dnů, z font size odvodí DataStyle/HeaderStyle. Výstup = přesný Template JSON.
  - **Princip:** AI = rozpoznání struktury (co), engine = přesné rozměry (kde). AI nemusí hádat pixely, engine nemusí rozumět sémantice.
  - **Prerekvizity:** Fungující elementy (krok 8/9), zdokumentované JSON schema elementů, Claude API přístup.
  - **Mezikrok (jednodušší alternativa):** Import PDF jako background obrázek (merge mode All) → uživatel ručně skládá elementy přes referenci → smaže background. Funguje okamžitě, bez AI.

---

#### Fáze C: Meziměstský JŘ

Konceptuálně podobné MHD — zastávky + odjezdy spojů + poznámky. Rozdíl: sloupce odjezdů nejsou typy provozu (jako MHD), ale mají jiný koncept specifický pro meziměsto. Layout je pivot tabulka (zastávky × spoje). Pravděpodobně bude potřeba samostatný element (ne rozšíření DepartureGridElement).

| Krok | Co | Stav |
|---|---|---|
| 15 | Definovat meziměstský layout — analýza referenčních PDF, wire formát dat | |
| 16 | Implementace meziměstského JŘ elementu | |
| 17 | Caché `TiskMeziměsto` — GetData/GetMockData pro meziměstský JŘ | |

---

#### Fáze D: Knižní JŘ

Layout TBD — pravděpodobně kompaktní formát pro knižní publikaci. Definovat layout před implementací.

| Krok | Co | Stav |
|---|---|---|
| 18 | Analýza referenčních knižních JŘ — definice layoutu a wire formátu | |
| 19 | `BookScheduleElement` — implementace (pravděpodobně víc elementů, viz poučení z MHD) | |
| 20 | Caché `TiskKnizni` — GetData/GetMockData | |

---

#### Fáze E: Vozový JŘ

Layout TBD — hlavička oběhu, tabulka spojů, poznámky. Pravděpodobně víc elementů (viz poučení z MHD).

| Krok | Co | Stav |
|---|---|---|
| 21 | Analýza referenčních vozových JŘ — definice layoutu a wire formátu | |
| 22 | `VehicleScheduleElement` — implementace (pravděpodobně víc nezávislých elementů) | |
| 23 | Caché `TiskVozovy` — GetData/GetMockData + async loading (větší objem dat) | |

---

#### Poučení z implementace zastávkového JŘ — co vzít v potaz pro další JŘ typy

| Téma | Popis |
|---|---|
| **Jeden element → tři elementy** | Původní plán počítal s jedním `StopScheduleElement`. Ve skutečnosti se rozpadl na 3 nezávislé elementy — lépe odpovídá filozofii designeru (uživatel skládá ze stavebních bloků). Počítat s tím od začátku u dalších JŘ typů. |
| **Synchronní GetData** | `GetData` běží synchronně na UI thread — pro malé linky OK, pro velké může zamrznout UI. Vozový JŘ bude mít víc dat → async loading s progress barem bude nutný. |
| **SKTypeface/SKPaint alokace** | Schedule elementy alokují SKTypeface a SKPaint per render frame. Funguje, ale není optimální. Cachovat fonty a painty. |
| **Caché business logika je rozsáhlá** | ZastavkoveJrMhdAPI má ~1200 řádků. Počítat s tím v odhadu pro další JŘ typy. |
| **Definovat layout před implementací** | Aby se neopakovala situace, kde se plán změní v průběhu. |

---

### 🟡 Důležité — omezení flexibility bez toho

**4. Min/max počet řádků dynamic tabulky**
Nové properties na `TableElement`: `MinRows`, `MaxRows`. Print engine ořízne přebývající řádky nebo doplní prázdné řádky do minima. Užitečné pro tabulky s proměnlivým počtem dat (faktury, JŘ).

**5. Hranice mezi buňkami tabulky (cell borders)**
`TableElement` property pro vnitřní cell borders (horizontální/vertikální čáry mezi buňkami, šířka, barva). Aktuálně má tabulka jen vnější rámeček — vnitřní oddělení buněk chybí.

**6. Zarovnávání napříč sekcemi**
Aktuálně alignment funguje jen v rámci jedné sekce. Umožnit zarovnat elementy z různých sekcí — referenční bod = stránka, ne sekce. Vyžaduje přepočet souřadnic (element.X/Y jsou relativní k sekci).

**7. Vnořené datasource (hierarchická data)**
Flat `_values + _rows` nestačí pro hierarchická data.
- `_rows` uvnitř `_rows` — kolekce uvnitř kolekce (faktura → položky → daně)
- TableElement může mít vnořený DataSource odkazující na child kolekci
- DataContext API rozšířit o `GetChildRows(collectionKey, parentIndex)`

---

### 🟢 Menší priorita

**UX polish:**
| # | Položka | Náročnost | Popis |
|---|---------|-----------|-------|
| c | Zoom persistence | Malá | Uložit zoom do Caché user preferences; řešit až při nasazení |
| f | Side panel vlastností místo modálního dialogu | Velká | ✅ základ hotovo (2026-03-21) — inline záložka "Vlastnosti" v levém panelu; modální dialog zachován jako fallback |
| g | Inline text editing na canvasu | Velká | Dvojklik na text → inline editace přímo na canvasu |
| h | Ikonový toolbar | Střední | ✅ hotovo (2026-03-21) — 33 Fluent UI ikon, 7 skupin, ToolbarStyles.xaml |

**8. Grouping v TableElement**
Seskupení řádků dynamické tabulky podle hodnoty pole.
- `TableRowType` enum: `GroupHeader`, `Data`, `GroupFooter`, `Summary`
- `GroupByField` property na TableElement
- Print engine sleduje změnu hodnoty groupovacího pole a vkládá Header/Footer řádky
- GroupFooter může obsahovat agregace ze skupiny (SUM/COUNT)

**9. Computed fields**
Pole vypočítaná na C# straně ze surových dat před předáním šabloně.
- Definice v agendě (nebo přímo v šabloně)
- Např. `CELKOVA_CENA = CENA * MNOZSTVI` — hotovo před renderováním
- Alternativa k výrazovému jazyku pro komplexnější logiku

---

### 🔵 Před produkcí (odloženo — řešit až půjde do produkce)

**7. Migrace verzí šablon**
Teď je v JSON pouze `"version": "1.0"` bez jakékoliv migrace. Během vývoje se model mění často a šablony se neverzují — migrace má smysl až při nasazení, kdy existující soubory nesmí přestat fungovat.
- Explicitní migrace při načítání starších JSON šablon (JsonNode pipeline před deserializací)
- Při změně modelu (přejmenování property, nový povinný field) nerozbít existující soubory
- Minimálně: logovat varování při načtení šablony se starší verzí

**8. Metadata a správa šablon**
Důležité jakmile šablon bude víc než 10. Zatím jich je málo — řešit až při nasazení.
- Autor, datum změny, kategorie, popis
- Thumbnail preview (PNG generovaný při uložení)
- Verzování šablon (kdo kdy co změnil)

---

### ✅ Implementováno

**UX audit — bezpečnost + frustrace + polish** ✅ hotovo (2026-03-18)
- Sanitizace VisibilityExpression (whitelist tokenizer + live validace v UI)
- Canvas fixed height + section clamping (sekce nepřesáhnou stránku)
- SectionRole (Header/Body/Footer) — patičky od spodu stránky
- Ctrl+X, Ctrl+0, context menu na canvasu, bound resize, potvrzení mazání sekce
- Nahrávání obrázků s auto-downscale (max 5 MB), SelectAll

**Zastávkový JŘ + Caché integrace** ✅ hotovo (2026-03-18)
- ScheduleElementBase + 3 elementy (StopListElement, DepartureGridElement, NotesLegendElement)
- ZastavkoveJrMhdAPI (~1200 řádků business logiky z legacy refaktoru, sloučeno z AgendaJRAPI + TiskMHD + TemplateDesignerMHD)
- CacheAgendaProvider: ParseMockData vnořená pole, GetData, BuildRealData
- PrintDialog: parametry + export reálných dat + multi-data PDF
- SkiaHeightCalculator: schedule element height calculation

**PageLayoutEngine + multi-page** ✅ hotovo (2026-03-17)
- Template + DataContext → `List<Page>` s PlacedSections; čistá logika (vrstva 3), žádný SkiaSharp/WPF
- Klasifikace sekcí (headers/footers/body), stránkování (KeepTogether/SplitAcrossPages), dvouprůchodový layout pro OnLastPageOnly
- Flow layout uvnitř sekcí, PerDataRow expanze, PAGE/TOTAL_PAGES injekce
- Multi-page preview (stránky pod sebou) + multi-page PDF export
- IElementHeightCalculator: SkiaHeightCalculator měří TextElement (word wrap) a TableElement

**Flow text v designeru (Word wrap + dynamická výška)** ✅ hotovo (2026-03-17)
- TextElement: `WordWrap=true` + `Height=null` → text se zalamuje, výška se dopočítá z obsahu
- `LineSpacing` property, vertikální zarovnání víceřádkového textu, fix `\r\n`
- InfiniteTextElement jako samostatný typ zamítnut — jedno chování řízené properties

**ElementGroup — seskupování elementů** ✅ základ hotovo (2026-03-12)
- `GroupId` property na `TemplateElement` — lehká logická vazba bez nested souřadnic
- Kliknutí na člen skupiny vybere celou skupinu; Alt+klik = jen ten jeden element
- Ctrl+G / Ctrl+Shift+G — seskupit / rozeskupit (plný undo/redo)
- Vizuální indikace: barevný badge v rohu, bounding box skupiny při výběru
- Strom šablony zobrazuje skupiny jako sbalitelné uzly
- Copy/Paste/Duplicate přemapují GroupId (klony nedědí skupinu originálu)
- PDF import automaticky seskupí grafiku podle typu a merge mode

**Locale / CultureInfo v šabloně** ✅ hotovo (2026-03-12)
- `Template.CultureCode` — kultura pro formátování čísel a datumů (cs-CZ, sk-SK, en-US, de-DE)
- Protéká celým pipeline: Template → DataContext.Culture → PlaceholderFormatter → TableElement
- Dropdown ve vlastnostech stránky; null = cs-CZ (výchozí); parsování vstupů zůstává InvariantCulture

**Drag & drop při přidávání elementů a sekcí** ✅ hotovo (2026-03-12)
- Drag element z toolbaru/palety na canvas — ghost preview, drop target highlight, clamp + snap
- Drag pole z panelu "Pole agendy" na canvas — vytvoří TextElement nebo append k existujícímu
- Drag & drop sekcí v levém panelu — přeřazení tažením, vizuální indikátor, `ReorderSectionCommand`
- Drag element mezi sekcemi — záměrně odloženo (nízká priorita, aktuální UX postačuje)

**Chytré duplikování elementů** ✅ hotovo (2026-03-12)
- Ctrl+D → šipka = směrové duplikování (kopie vedle originálu s 4mm mezerou)
- Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Multi-select, clamp na bounds sekce, plný undo/redo

**Přichytávání elementů k sobě (smart snap)** ✅ hotovo (2026-03-12)
- Edge↔Edge (4 kombinace), Center↔Center, Gap snap (4mm mezera) — bez kategorické priority, vyhraje nejbližší match
- Zvýraznění cílových elementů (šedý dashed rámeček), gap indikátor (černá čárka v mezeře)
- Element snap má přednost před grid snapem; osy X/Y nezávisle
- Toggle v toolbaru + menu; výchozí zapnuto

**Výrazový jazyk** ✅ hotovo (2026-03-12)
- Part 1 — podmínky a logické operátory: `{TISK_SIPKY}`, `{POCET} > 0`, `{TYP} == "PD"`, `&&`, `||`, `!`, závorky; VisibilityExpression na elementech a sekcích; ExpressionEvaluator + Parser + AST nodes
- Part 2a — agregace: `SUM({CENA})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})` v TableElement; SummaryRows; AggregationResolver; záložka Souhrn v TableEditorDialog
- Part 2b — formátování placeholderů: `{POLE:N2}`, `{POLE:dd.MM.yyyy}` (.NET formáty); PlaceholderFormatter + DataContext rozšíření
- Part 2c — řetězcové funkce: `{POLE:LEFT(4)}`, `{POLE:UPPER}`, `{POLE:PADLEFT(3,'0')}`, `{POLE:TRIM}`

---

### ✅ Záměrně jinak — neřešit

- **Caché jako source of truth pro tabulkové šablony** — dává smysl pro doménovou logiku
- **Univerzální Section třída** — jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** — správné pro tiskové sestavy s pevným layoutem

---

## 🖨️ PageLayoutEngine + stránkovaný preview (fáze 2) ✅ hotovo (2026-03-17)

Preview v designeru = plný engine. "Tisk" = ten samý engine s reálnými daty → PDF.
Architektonické rozhodnutí (2026-03-17): engine je **integrovaný** v TemplateDesigneru, ne samostatný projekt. Oddělení do samostatných .csproj se udělá až bude druhý konzument.

### Vrstvy systému

Viz diagram v `CLAUDE.md` (sekce "Architektura — vrstvy systému").

### PageLayoutEngine — vstup/výstup ✅

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

## 🤖 AI integrace — vize a plán

Architektonické rozhodnutí (2026-03-17): **žádné zásadní rozhodnutí teď není potřeba.** JSON šablona + DataSchema jsou dostatečné rozhraní pro AI. Rozšíření se dá přidat kdykoliv později bez breaking changes.

**Princip:** AI = copilot, ne autopilot. Vždy navrhne → uživatel potvrdí/upraví.

### Aktuální stav
- ✅ AI Assistant — generování celé šablony z textového popisu (Claude API, prompt-based)
- ✅ Schema injection — AI dostane seznam polí/kolekcí/parametrů agendy

### Fáze 1 — prompt-based (stačí teď)

Pro většinu use cases stačí dát AI kontext přes systémový prompt (JSON šablona + schema):

| Use case | Přístup |
|---|---|
| Generování šablony z popisu | ✅ hotovo |
| Vysvětlení šablony ("co dělá tato sekce?") | prompt + aktuální JSON |
| Nápověda k výrazům ("jak skrýt řádek?") | prompt + docs |
| Template linting / validace | prompt + JSON + schema |
| PDF import režim 2 ("zchytři to") | prompt + importovaný JSON + schema |

### Fáze 2 — MCP server (až budou reální uživatelé s potřebou AI asistence)

Pro operace měnící stav aplikace (úpravy šablony hlasem/textem, smart suggestions) by TemplateDesigner vystavil MCP server:

```
MCP Tools:
├── get_template()                  → aktuální JSON šablona
├── get_schema(agendaCode)          → DataSchema dané agendy
├── get_available_agendas()         → seznam agend
├── get_mock_data(agendaCode)       → vzorová data
├── apply_template(json)            → nahradí šablonu (s undo)
├── add_element(sectionId, element) → přidá element (s undo)
├── modify_element(elementId, props)→ změní properties (s undo)
├── add_section(preset, index)      → přidá sekci
├── validate_template()             → vrátí seznam problémů
└── get_help(topic)                 → dokumentace k funkci
```

MCP je tenká vrstva nad ViewModelem — každý tool volá existující metody (`_vm.Execute(command)`).
Standardní protokol (stdio, lokální) — funguje s Claude i jinými AI, žádný vlastní server.

### Fáze 3 — pokročilé use cases (dlouhodobě)

| Use case | Kategorie | Popis |
|---|---|---|
| Onboarding průvodce | Noví uživatelé | AI popíše co nástroj umí, navede uživatele krok za krokem |
| "Přidej patičku s číslem stránky" | Zkušení uživatelé | AI modifikuje existující šablonu (ne generuje od nuly) |
| Smart suggestions | Zkušení uživatelé | Přidáš TableElement → AI navrhne sloupce z DataSchema |
| Migrace starých Caché sestav | Automatizace | AI analyzuje starý kód → navrhne TemplateDesigner šablonu |
| Template linting v CI/CD | Automatizace | AI zkontroluje šablony před deployem |
| Batch generování šablon | Automatizace | Nová agenda → AI vygeneruje základní šablony |

### Proč to neblokuje

JSON šablona je stabilní kontrakt mezi designerem a AI. Dokud AI umí číst a psát JSON v definovaném formátu, integrace funguje bez ohledu na to, jestli je to prompt, MCP, nebo REST API. Rozšíření se dá přidat kdykoliv bez refactoringu.

---

## 🏗️ Plánované architektonické změny (nutné, ne volitelné)

**Caché-driven formuláře a komponenty (střední/dlouhodobý horizont)**

Současný `PropertiesPanel` + `PropertyDescriptor` systém je dočasné řešení. Do budoucna bude veškerá business logika (definice vlastností elementů, formuláře, tabulky) řízena z Caché přes standardní EDControls komponenty.

**Cílová architektura:**
- `ElementPropertiesWindow` — místo vlastního `PropertiesPanel` použít `EDInputForm` (z EDControls)
- Formulář pro každý element bude definovaný v Caché třídě `ALVA.TemplateDesigner.Elements.<Typ>.Init/Validate/Save`
- Editor sloupců `TableElement` — použít `EDDataGrid` (z EDControls, až bude hotový)
- Presety elementů a tabulek (např. JR tabulka se sloupci) — načítat z Caché místo hardkódování v C#

**Co se NEZMĚNÍ:**
- C# třídy elementů (renderování, Clone, model, JSON serializace)
- Drag & drop, resize, canvas logika

**Překladová vrstva .NET ↔ Caché (nutná pro undo/redo):**
- `Init`: .NET serializuje aktuální hodnoty elementu → Caché postaví formulář s hodnotami
- `Save`: Caché vrátí změněné hodnoty → .NET updatuje element přes `SetPropertyCommand` (zachová undo/redo)

**Kdy řešit:** Až bude `EDDataGrid` hotový a/nebo bude potřeba první Caché-driven formulář.
Do té doby `PropertyDescriptor` systém funguje a neblokuje.

**Reference:** Viz `EDControls/EDInputForm/` — existující implementace + Caché třídy `ALVA.EDControls.EDInputForm.*`
