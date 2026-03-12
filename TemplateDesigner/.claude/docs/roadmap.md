# Roadmap

## 📋 Roadmap (obecný tiskový engine)

Cíl: systém schopný pokrýt libovolnou tiskovou sestavu, nezávisle na JR agendě.
Specifické JR elementy (JRTableElement apod.) jsou mimo scope tohoto dokumentu.

---

### 🔴 Kritické

**1. Grouping v TableElement**
Seskupení řádků dynamické tabulky podle hodnoty pole.
- `TableRowType` enum: `GroupHeader`, `Data`, `GroupFooter`, `Summary`
- `GroupByField` property na TableElement
- Print engine sleduje změnu hodnoty groupovacího pole a vkládá Header/Footer řádky
- GroupFooter může obsahovat agregace ze skupiny (SUM/COUNT)

---

### 🟡 Důležité — omezení flexibility bez toho

**2. Flow layout sekcí (InfiniteTextElement)**
Absolutní pozicování se rozbije jakmile element přeteče svou výšku.
- `InfiniteTextElement` — textový blok roztahující se podle obsahu (výška null)
- Sekce s `OverflowBehavior=SplitAcrossPages` musí posouvat elementy pod přetečeným
- Print engine počítá výšku sekce z obsahu, ne z `DesignerHeight`

**3. Vnořené datasource**
Flat `_values + _rows` nestačí pro hierarchická data.
- `_rows` uvnitř `_rows` — kolekce uvnitř kolekce (faktura → položky → daně)
- TableElement může mít vnořený DataSource odkazující na child kolekci
- DataContext API rozšířit o `GetChildRows(collectionKey, parentIndex)`

**4. Locale / CultureInfo v RenderContext**
Kdo rozhodne o desetinné čárce, formátu čísel a datumů?
- `CultureInfo` jako součást `RenderContext`
- Konfigurovatelné per šablona nebo per agenda
- Konzistentní chování designer ↔ print engine

---

### 🟢 Menší priorita

**5. Chytré duplikování a zarovnání elementů**
Aktuální Ctrl+C / Ctrl+V vloží kopii s offsetem +5 mm — uživatel pak element přesouvá ručně.
- **Duplikovat dolů / vpravo** — Ctrl+D vloží kopii přesně pod (nebo vpravo od) originálu; žádný offset, spodní hrana originálu = horní hrana kopie
- **Duplikovat s mezerou** — Shift+Ctrl+D nabídne mezerník (0 / 2 / 5 mm) před vložením
- Implementace: nový příkaz `DuplicateElementCommand(direction, gap)` volaný z `MainWindow` klávesového handleru; pozice kopie = `original.X, original.Y + original.Height + gap`

**6. Přichytávání elementů k sobě (smart snap)**
Snap to grid zarovná na mřížku, ale ne na jiné elementy. Ve Winforms designeru elementy "přiskočí" k sousedům.
- Při dragu zobrazit vodítka (snaplines) — tenké modré čáry když hrana taženého elementu lícuje s hranou jiného elementu ve stejné sekci
- Přichytit na: levou/pravou/horní/dolní hranu, střed (horizontální i vertikální) jiného elementu
- Malá konzistentní mezera (2 mm) jako alternativní snap — "přiskočí" na souseda + 2 mm vzduchová mezera
- Implementace:
  - `DesignerCanvas.CalculateSnapLines(draggedElements, allElements)` — vrátí seznam `SnapLine(orientation, position, fromElement)`
  - Kreslit snaplines v `OnPaintSurface` nad elementy (tenká modrá čára přes celou sekci)
  - Threshold: pokud vzdálenost < 3 px při aktuálním zoomu, snap se aktivuje
  - Snap má přednost před grid-snapem (nebo kombinovat: snap na element pokud blízko, jinak na grid)

**7. Migrace verzí šablon**
Teď je v JSON pouze `"version": "1.0"` bez jakékoliv migrace.
- Explicitní migrace při načítání starších JSON šablon
- Při změně modelu (přejmenování property, nový povinný field) nerozbít existující soubory
- Minimálně: logovat varování při načtení šablony se starší verzí

**8. Computed fields**
Pole vypočítaná na C# straně ze surových dat před předáním šabloně.
- Definice v agendě (nebo přímo v šabloně)
- Např. `CELKOVA_CENA = CENA * MNOZSTVI` — hotovo před renderováním
- Alternativa k výrazovému jazyku pro komplexnější logiku

**9. Metadata a správa šablon**
Důležité jakmile šablon bude víc než 10.
- Autor, datum změny, kategorie, popis
- Thumbnail preview (PNG generovaný při uložení)
- Verzování šablon (kdo kdy co změnil)

**10. Drag & drop při přidávání elementů a sekcí**
Aktuálně se elementy přidávají přes toolbar menu na pevnou pozici (5×5 mm). Sekce se přidávají na konec seznamu.
- **Drag element z toolbaru/palety na canvas** — uživatel táhne typ elementu přímo na místo v sekci, kde ho chce umístit; element se vytvoří na pozici puštění myši
- **Drag pole z panelu "Pole agendy" na canvas** — přetažení pole vytvoří TextElement s `{POLE}` na cílové pozici
- **Drag & drop sekcí v levém panelu** — přeřazení pořadí sekcí tažením místo tlačítek ▲▼
- **Drag element mezi sekcemi** — přesun elementu z jedné sekce do druhé tažením přes hranici sekce na canvasu
- Vizuální zpětná vazba: drop target highlight, ghost preview při tažení

---

### ✅ Implementováno

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
