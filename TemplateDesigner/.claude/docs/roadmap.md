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

## 🖨️ Print Engine (fáze 2) — samostatný projekt

Věci které patří do print enginu, nikoliv do designeru.

- **Print dialog** — dynamicky generovaný ze šablony (`Template.Parameters`); stejný UI pattern jako live preview panel v designeru (CheckBox/ComboBox/TextBox); hodnoty předány enginu před tiskem
- **Renderování šablony** — načtení JSON šablony + DataContext z Caché → PDF výstup
- **Flow layout** — sekce s `OverflowBehavior=SplitAcrossPages`, dynamické výšky elementů
- **VisibilityExpression vyhodnocení** — filtrování sekcí a elementů za běhu dle dat

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
