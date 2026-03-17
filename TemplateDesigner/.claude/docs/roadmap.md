# Roadmap

## 📋 Roadmap (obecný tiskový engine)

Cíl: systém schopný pokrýt libovolnou tiskovou sestavu, nezávisle na JR agendě.
Specifické JR elementy (JRTableElement apod.) jsou mimo scope tohoto dokumentu.

---

### 🔴 Kritické

*(žádné aktivní)*

---

### 🟡 Důležité — omezení flexibility bez toho

**~~1. Flow layout sekcí~~** ✅ designer-side hotovo (2026-03-17)
TextElement s `WordWrap=true` + `Height=null` → dynamická výška dopočítaná z textu. Designer zobrazuje aproximaci.
- ~~`InfiniteTextElement`~~ → zamítnuto; flow řízeno properties na TextElementu
- Zbývá **Print Engine**: posouvání elementů pod dynamickým, stránkování sekcí s `OverflowBehavior=SplitAcrossPages`, výpočet výšky sekce z obsahu

**~~2. Locale / CultureInfo v šabloně~~** ✅ hotovo (2026-03-12)

**3. Import PDF → šablona**
Strojová analýza existujícího PDF a převod na TemplateDesigner šablonu.
- **Režim 1 — "1:1 import" (deterministický):** PdfPig extrahuje text (s pozicí, fontem, barvou), čáry, obdélníky, obrázky → mapování na TextElement, LineElement, RectangleElement, ImageElement, TableElement (Fixed). Detekce tabulek z mřížky čar. Převod pt → mm. Výsledek = vizuálně identická šablona se statickými texty.
- **Režim 2 — "Zchytři to" (AI post-processing):** Vezme výsledek režimu 1 + schéma agendy → AI navrhne nahrazení statických textů za `{PLACEHOLDER}`, identifikuje opakující se záhlaví/patičku (→ sekce OnEveryPage), rozpozná kandidáty na Dynamic tabulku. Uživatel potvrdí/upraví návrhy.
- Knihovna: **PdfPig** (open source, .NET, per-word bounding boxy, vestavěné algoritmy pro seskupování textu)
- Režim 1 je užitečný sám o sobě; režim 2 je nadstavba

**4. Vnořené datasource**
Flat `_values + _rows` nestačí pro hierarchická data.
- `_rows` uvnitř `_rows` — kolekce uvnitř kolekce (faktura → položky → daně)
- TableElement může mít vnořený DataSource odkazující na child kolekci
- DataContext API rozšířit o `GetChildRows(collectionKey, parentIndex)`

---

### 🟢 Menší priorita

**5. Grouping v TableElement**
Seskupení řádků dynamické tabulky podle hodnoty pole.
- `TableRowType` enum: `GroupHeader`, `Data`, `GroupFooter`, `Summary`
- `GroupByField` property na TableElement
- Print engine sleduje změnu hodnoty groupovacího pole a vkládá Header/Footer řádky
- GroupFooter může obsahovat agregace ze skupiny (SUM/COUNT)

**6. Computed fields**
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

**Flow text v designeru (Word wrap + dynamická výška)** ✅ hotovo (2026-03-17)
- TextElement: `WordWrap=true` + `Height=null` → text se zalamuje, výška se dopočítá z obsahu
- `LineSpacing` property, vertikální zarovnání víceřádkového textu, fix `\r\n`
- InfiniteTextElement jako samostatný typ zamítnut — jedno chování řízené properties
- Zbývá engine-side: posouvání elementů pod dynamickým, stránkování

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

### 🔧 UI drobnosti (low priority)

- **Context menu na záhlaví sekce v designeru** — pravý klik přímo na canvasu (Vlastnosti, Duplikovat, Smazat, Přesunout); alternativa k levému panelu

---

## 🖨️ PageLayoutEngine + stránkovaný preview (fáze 2)

Preview v designeru = plný engine. "Tisk" = ten samý engine s reálnými daty → PDF.
Architektonické rozhodnutí (2026-03-17): engine je **integrovaný** v TemplateDesigneru, ne samostatný projekt. Oddělení do samostatných .csproj se udělá až bude druhý konzument.

### Vrstvy systému

```
5. Aplikace         WPF Designer, EDISON tisk, Server API, CLI batch
4. Renderer         element.Render(ctx) → SKCanvas / PDF (SkiaSharp, žádné WPF)
3. PageLayoutEngine Template + DataContext → List<Page> (žádný SkiaSharp, žádné UI)
2. Expressions      ExpressionEvaluator, PlaceholderFormatter, AggregationResolver
1. Models           Template, Section, TemplateElement, DataContext, DataSchema
```

### PageLayoutEngine — vstup/výstup

```csharp
class PageLayoutEngine {
    List<Page> Layout(Template template, DataContext data);
}
class Page {
    int PageNumber, TotalPages;
    List<PageElement> Elements;  // absolutní pozice na stránce (mm)
}
class PageElement {
    TemplateElement Element;
    float X, Y, Width, Height;  // finální rozměry (Height dopočítaná)
    DataContext? RowData;         // pro Dynamic tabulky: konkrétní řádek
}
```

### Implementační kroky

| Krok | Co | Náročnost |
|---|---|---|
| 1 | `PageLayoutEngine` — section ordering + repeating (OnEveryPage atd.) | střední |
| 2 | Stránkovaný preview — navigace stran (← strana 1/3 →), page break vizualizace | střední |
| 3 | Flow layout — dynamická výška elementů posouvá elementy pod nimi | vyšší |
| 4 | Page breaking — sekce SplitAcrossPages se rozdělí přes stránky | vyšší |
| 5 | Napojení reálných dat z Caché (`GetData`) do preview | nízká |
| 6 | Print dialog — parametry před tiskem (rozšíření stávajícího preview panelu) | střední |

Kroky 1–2 jsou užitečné i bez flow layoutu — uživatel uvidí jak se sekce opakují na stránkách.

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
