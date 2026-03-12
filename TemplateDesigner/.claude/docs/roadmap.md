# Roadmap

## 📋 Roadmap (obecný tiskový engine)

Cíl: systém schopný pokrýt libovolnou tiskovou sestavu, nezávisle na JR agendě.
Specifické JR elementy (JRTableElement apod.) jsou mimo scope tohoto dokumentu.

---

### 🔴 Kritické — bez toho narazíme při prvním reálném požadavku

**1. Parametry sestavy** ✅ Part 1 hotovo — zbývá Part 2 (print dialog)
Uživatel zadá hodnoty před tiskem (datum, filtr, přepínač). Bez toho každá sestava s filtrem vyžaduje změnu kódu.
- ✅ Definice parametrů v Caché `GetParameters` (název, typ `bool/enum/string/date`, label, default)
- ✅ Live preview panel s interaktivními kontrolkami — hodnoty injektovány do DataContextu jako `{PARAM_NAME}`
- ✅ Parametry zobrazeny v pickeru polí s příznakem "(parametr)"
- ❌ Print dialog dynamicky generovaný ze šablony — bez změny kódu aplikace (Part 2 — závisí na VisibilityExpression)
- ✅ Ukládání parametrů do JSON šablony + offline fallback (bez Caché) — hotovo 2026-03-12
- *Předpoklad pro bod 2 (VisibilityExpression)*

**2. Výrazový jazyk** ✅ Part 1 hotovo — zbývá Part 2
- ✅ **Part 1 — podmínky a logické operátory:** `{TISK_SIPKY}`, `{POCET} > 0`, `{TYP} == "PD"`, `&&`, `||`, `!`, závorky; VisibilityExpression na elementech a sekcích; ExpressionEvaluator + Parser + AST nodes
- ❌ **Part 2 — agregace a formátování:** `SUM({CENA})`, `COUNT()`, `AVG({CAS})` v TableElement; Summary řádek (zápatí s agregacemi)
- ❌ Formátovací rozšíření: `{POLE:LEFT(4)}`, `{POLE:UPPER}`, `{POLE:PADLEFT(3,'0')}`
  (konzistentní se stávajícím `dd.MM.yyyy` / `N2` — vše za `:` je instrukce)

**3. Grouping v TableElement**
Seskupení řádků dynamické tabulky podle hodnoty pole.
- `TableRowType` enum: `GroupHeader`, `Data`, `GroupFooter`, `Summary`
- `GroupByField` property na TableElement
- Print engine sleduje změnu hodnoty groupovacího pole a vkládá Header/Footer řádky
- GroupFooter může obsahovat agregace ze skupiny (SUM/COUNT)

---

### 🟡 Důležité — omezení flexibility bez toho

**4. Flow layout sekcí (InfiniteTextElement)**
Absolutní pozicování se rozbije jakmile element přeteče svou výšku.
- `InfiniteTextElement` — textový blok roztahující se podle obsahu (výška null)
- Sekce s `OverflowBehavior=SplitAcrossPages` musí posouvat elementy pod přetečeným
- Print engine počítá výšku sekce z obsahu, ne z `DesignerHeight`

**5. Vnořené datasource**
Flat `_values + _rows` nestačí pro hierarchická data.
- `_rows` uvnitř `_rows` — kolekce uvnitř kolekce (faktura → položky → daně)
- TableElement může mít vnořený DataSource odkazující na child kolekci
- DataContext API rozšířit o `GetChildRows(collectionKey, parentIndex)`

**6. Locale / CultureInfo v RenderContext**
Kdo rozhodne o desetinné čárce, formátu čísel a datumů?
- `CultureInfo` jako součást `RenderContext`
- Konfigurovatelné per šablona nebo per agenda
- Konzistentní chování designer ↔ print engine

---

### 🟢 Menší priorita

**8. Chytré duplikování a zarovnání elementů**
Aktuální Ctrl+C / Ctrl+V vloží kopii s offsetem +5 mm — uživatel pak element přesouvá ručně.
- **Duplikovat dolů / vpravo** — Ctrl+D vloží kopii přesně pod (nebo vpravo od) originálu; žádný offset, spodní hrana originálu = horní hrana kopie
- **Duplikovat s mezerou** — Shift+Ctrl+D nabídne mezerník (0 / 2 / 5 mm) před vložením
- Implementace: nový příkaz `DuplicateElementCommand(direction, gap)` volaný z `MainWindow` klávesového handleru; pozice kopie = `original.X, original.Y + original.Height + gap`

**9. Přichytávání elementů k sobě (smart snap)**
Snap to grid zarovná na mřížku, ale ne na jiné elementy. Ve Winforms designeru elementy "přiskočí" k sousedům.
- Při dragu zobrazit vodítka (snaplines) — tenké modré čáry když hrana taženého elementu lícuje s hranou jiného elementu ve stejné sekci
- Přichytit na: levou/pravou/horní/dolní hranu, střed (horizontální i vertikální) jiného elementu
- Malá konzistentní mezera (2 mm) jako alternativní snap — "přiskočí" na souseda + 2 mm vzduchová mezera
- Implementace:
  - `DesignerCanvas.CalculateSnapLines(draggedElements, allElements)` — vrátí seznam `SnapLine(orientation, position, fromElement)`
  - Kreslit snaplines v `OnPaintSurface` nad elementy (tenká modrá čára přes celou sekci)
  - Threshold: pokud vzdálenost < 3 px při aktuálním zoomu, snap se aktivuje
  - Snap má přednost před grid-snapem (nebo kombinovat: snap na element pokud blízko, jinak na grid)
  - Priorita: snaplines jsou UX vylepšení — implementovat až po výrazovém jazyku Part 2

**10. Migrace verzí šablon**
Teď je v JSON pouze `"version": "1.0"` bez jakékoliv migrace.
- Explicitní migrace při načítání starších JSON šablon
- Při změně modelu (přejmenování property, nový povinný field) nerozbít existující soubory
- Minimálně: logovat varování při načtení šablony se starší verzí

**11. Computed fields**
Pole vypočítaná na C# straně ze surových dat před předáním šabloně.
- Definice v agendě (nebo přímo v šabloně)
- Např. `CELKOVA_CENA = CENA * MNOZSTVI` — hotovo před renderováním
- Alternativa k výrazovému jazyku pro komplexnější logiku

**12. Metadata a správa šablon**
Důležité jakmile šablon bude víc než 10.
- Autor, datum změny, kategorie, popis
- Thumbnail preview (PNG generovaný při uložení)
- Verzování šablon (kdo kdy co změnil)

---

### ✅ Záměrně jinak — neřešit

- **Caché jako source of truth pro tabulkové šablony** — dává smysl pro doménovou logiku
- **Univerzální Section třída** — jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** — správné pro tiskové sestavy s pevným layoutem

---

### 🔧 UI drobnosti (low priority)

- **Context menu na záhlaví sekce v designeru** — pravý klik přímo na canvasu (Vlastnosti, Duplikovat, Smazat, Přesunout); alternativa k levému panelu

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
