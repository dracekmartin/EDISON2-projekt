# CLAUDE.md — Kontext pro AI agenta

Tento soubor popisuje projekt, jeho architekturu a pravidla pro práci s kódem.
Při každé práci na projektu si tento soubor přečti a řiď se jím.

---

## Co je tento projekt

**TemplateDesigner** je WPF desktopová aplikace pro tvorbu šablon tiskových sestav.
Jde o náhradu staršího nástroje postaveného v InterSystems Caché/Globals, který nelze dále rozšiřovat.

Systém se skládá ze dvou částí:
1. **Designer** (tento projekt) — uživatel navrhuje šablony vizuálně
2. **Print Engine** (budoucí projekt) — šablona + data = PDF výstup

---

## ⚡ Aktuální stav projektu

> **📝 POZNÁMKA PRO AI AGENTA:** Tuto sekci **průběžně aktualizuj** během práce na projektu. Když dokončíš úkol, přesuň ho do "✅ Implementováno". Když objevíš nový problém, přidej ho do "🐛 Známé problémy". Když přidáš novou feature, doplň ji do seznamu. **Tato sekce má být vždy aktuální**, aby při startu nové session byl kontext dostupný bez vysvětlování.

**Datum poslední aktualizace:** 2026-03-02 (rozšířeno o aktuální focus a nice to have)

### 🎯 Aktuální focus

**Procházíme a kompletujeme elementy** — cílem je mít všechny klasické standardní komponenty známé z template generátorů (Crystal Reports, Jasper, FastReport).

**Priorita:** Tabulky a speciální komponenty pro jízdní řády (tabulkové sestavy s opakováním řádků, QR kódy pro cestující).

**Co už máme:**
- ✅ Text, Image, Rectangle, Line (základní elementy)
- ✅ Table (základ implementován, chybí UI editor sloupců)

**Co chybí (klasické komponenty):**
- ❌ Barcode/QR kód (HIGH priority — viz TODO)
- ❌ Chart/Graph (budoucnost)
- ❌ Subreport/Vnořená šablona (budoucnost)
- ❌ InfiniteText (textový blok roztahující se podle obsahu)
- ⚠️ Page numbers/Date — lze přes TextElement + placeholders `{PAGE}`, `{DATE}`, ale není explicitní element

**Speciální požadavky pro jízdní řády:**
- TableElement s opakováním řádků (implementováno)
- QR kódy pro cestující (TODO — BarcodeElement)
- Podmíněné řádky tabulky (např. "pouze zastávky kde je přestup")
- Grouping v tabulkách (např. per linka)

### ✅ Implementováno a funkční

**Core features:**
- ✅ Drag & drop, resize, rubber band selection elementů
- ✅ Undo/redo systém (9 command typů: Move, Resize, Add, Delete, SetProperty, Composite, MoveSection, DeleteSection)
- ✅ PropertyDescriptor systém s 5 typy (String, Float, Bool, Color, Enum)
- ✅ Live preview panel (mock DataContext, fit-to-width zoom)
- ✅ Zoom (50/75/100/150/200 %) + Ctrl+scroll
- ✅ Grid & snap to grid
- ✅ Clipboard (interní, copy/paste s offsetem)
- ✅ Section management (D&D reorder, context menu, Ctrl+Shift+↑↓)

**Elements:**
- ✅ TextElement (s BackgroundColor, FontFamily, FontSize, FontStyle, FontWeight, TextColor, HorizontalAlignment, VerticalAlignment, WordWrap)
- ✅ ImageElement
- ✅ RectangleElement
- ✅ LineElement
- ✅ TableElement (základ, bez UI editoru sloupců)

**Sections:**
- ✅ Section refactoring (2026-03) — univerzální třída místo 7 podtříd
- ✅ UI menu presety (Hlavička/Patička stránky, Hlavička sestavy, Patička sestavy, Hlavička kromě 1. str., Oddělovač, Prázdná sekce)
- ✅ Custom section templates (file-based V1) — save/load přes UI, uložení do `section-templates/*.json`

**Data schema:**
- ✅ DataSchema loading (JSON fallback `schemas/*.schema.json`)
- ✅ CacheDataSchemaProvider (delegate pattern, MLRest integration)
- ✅ CompositeDataSchemaProvider (Caché primary + JSON fallback)
- ✅ Fields browser v levém panelu (záložka "Pole agendy")

**Serialization:**
- ✅ Template JSON serialization (camelCase, enums as strings, null omitted)
- ✅ Section JSON serialization (pro custom templates)
- ✅ Polymorfní deserializace (JsonPolymorphic + JsonDerivedType)

### 🐛 Známé problémy

1. **ColorPicker minimalizace Properties window** — NEBLOKUJÍCÍ
   - **Popis:** Po zavření WinForms ColorDialog se minimalizuje ElementPropertiesWindow (kvůli ToolWindow style)
   - **Workaround:** Uživatel znovu otevře Properties panel
   - **Lokace:** `Controls/PropertiesPanel.xaml.cs:416` (TODO komentář)
   - **Priorita:** LOW
   - **Možná řešení:** WPF ColorPicker control, změna WindowStyle, nebo Extended Toolkit

### 📋 TODO Seznam (prioritizováno)

**HIGH priority:**
1. **Implementovat BarcodeElement** — QR kód + Code128
   - Třída `BarcodeElement : TemplateElement`
   - Properties: `BarcodeType` (enum: QR, Code128), `Content`, `Width`, `Height`
   - Render pomocí knihovny (např. ZXing.Net nebo SkiaSharp.QrCode)
   - PropertyDescriptors pro obsah a typ

**MEDIUM priority:**
2. **Vytvořit UI editor sloupců pro TableElement**
   - Dialog nebo panel pro definici sloupců tabulky
   - Properties: název sloupce, šířka, datové pole, formát, zarovnání
   - Záhlaví tabulky (label, font, barva)
   - Aktuálně se sloupce definují pouze ručně v JSON

**LOW priority:**
3. **Opravit ColorPicker minimalizaci** — viz Známé problémy výše

**OPTIONAL:**
4. **Přidat context menu na záhlaví sekce v designeru**
   - Pravý klik na záhlaví sekce (ne v levém panelu, ale přímo na canvasu)
   - Volby: Vlastnosti, Duplikovat, Smazat, Přesunout nahoru/dolů
   - Alternativa k levému panelu pro rychlejší přístup

### 📜 Nedávné změny (changelog)

**2026-03-02:**
- ✅ Přidán BackgroundColor na TextElement (barva pozadí textu)
- ✅ Custom section templates implementovány (file-based V1, `SectionTemplateManager`)
- ✅ Documentation consolidation (architektura.md merged → CLAUDE.md, smazány obsolete soubory)

**2026-03-02 (dříve):**
- ✅ Section refactoring — sloučení 7 podtříd do univerzální `Section` třídy
- ✅ UI menu presety pro přidání sekcí (zjednodušení menu)
- ✅ Odstranění deprecated section subtypes (SectionHeader, SectionFooter, atd.)
- ✅ Cleanup JSON polymorphismu (přímá serializace bez type discriminatoru)

### 🔮 Budoucí rozšíření (neplánováno, vize)

**Nice to have:**
- **Drag & drop ze toolboxu** — táhnout elementy/sekce z levého panelu přímo na canvas (místo "Přidat" + umístit)
- **Drag & drop sekcí** — přeuspořádání sekcí táhnutím přímo na canvasu (místo tlačítek ▲▼)
- **Drag & drop elementů mezi sekcemi** — přesun elementu z jedné sekce do druhé táhnutím
- **AI asistent pro generování/úpravu šablon** — chat rozhraní kde uživatel popíše požadavek a AI:
  - Generuje šablonu z popisu ("vytvoř šablonu pro fakturu s logem a tabulkou položek")
  - Upravuje existující šablonu ("přidej QR kód do zápatí", "změň font záhlaví na tučný")
  - Radí s layoutem a best practices
  - Automaticky vytváří sekce, elementy a nastavuje properties podle kontextu
  - Integrováno přímo v designeru (panel nebo modal dialog)

**Komponenty:**
- **Fragmenty / kompozice elementů** — uložené skupiny elementů pro opakované použití
- **InfiniteTextElement** — textový blok roztahující se podle obsahu
- **ChartElement** — grafy (sloupcové, koláčové, čárové)
- **SubreportElement** — vnořená šablona (šablona v šabloně)

**Pokročilé features:**
- **V2 VisibilityExpression** — výrazy místo checkbox (např. `{POCET} > 0`)
- **Conditional formatting** — barva/font podle dat (např. červeně pokud zpoždění > 5 min)
- **Grouping v tabulkách** — Group Header/Footer (např. per linka v jízdním řádu)
- **DB backend pro custom šablony** — `CacheSectionTemplateProvider` místo file-based

---

## Technologický stack

| Technologie | Použití |
|---|---|
| .NET 9, WPF | UI framework |
| SkiaSharp 3.119.2 | Renderování canvasu i PDF |
| SkiaSharp.Views.WPF 3.119.2 | SKElement WPF kontrola |
| System.Text.Json | Serializace šablon |

### Důležité poznámky k SkiaSharp
- `SkiaSharp.Views.WPF` nefunguje správně s net9.0 přes XAML — `PaintSurface` event se **nesmí** registrovat v XAML, pouze v code-behind:
  ```csharp
  skCanvas.PaintSurface += OnPaintSurface;
  ```
- Parametr eventu se přijímá jako `object` a přetypovává přes `dynamic` kvůli omezení kompilátoru
- NU1701 warningy jsou očekávané a neškodné — balíčky fungují správně za běhu
- `using SkiaSharp;` **není** v implicitních using WPF projektu — přidej explicitně do každého souboru, kde se používají SK typy (`SKCanvas`, `SKPaint`, `SKColor`, `SKFont`…)
- `using System.IO;` taktéž není implicitní — přidej do souborů pracujících se soubory
- `SKFilterQuality` v SkiaSharp 3.x **neexistuje** — byl odebrán; pro kvalitu renderování nastav `SKSamplingOptions` nebo nech výchozí hodnotu

---

## Architektura projektu

```
/Models
  Template.cs               — kořenový objekt šablony
  PageSize.cs               — rozměry stránky v mm
  Section.cs                — univerzální sekce + enums (SectionPrintBehavior, SectionOverflowBehavior)
  TemplateElement.cs        — abstraktní základ elementu
  DataContext.cs            — data pro renderování (print engine / preview)
  RenderContext.cs          — kontext renderování (canvas + MmToPx + DataContext + origin sekce)
  /Elements                 — konkrétní typy elementů
  /Schema                   — schéma dostupných DB polí (design time only)
    DataSchema.cs           — DataSchema, DataField, DataFieldGroup, enums
    IDataSchemaProvider.cs  — rozhraní pro načítání schémat

/Serialization
  TemplateSerializer.cs             — JSON ukládání a načítání šablony + sekcí
  SectionTemplateManager.cs         — správa custom šablon sekcí (file-based V1, připraveno na DB V2)
  JsonFileDataSchemaProvider.cs     — načítání *.schema.json ze složky schemas/
  CacheDataSchemaProvider.cs        — načítání z Caché přes MLRest (delegate pattern)
  CompositeDataSchemaProvider.cs    — Caché primary + JSON fallback

/Config
  AppSettings.cs            — POCO konfigurace + static Load() z appsettings.json
  CacheSchemaWiring.cs      — vytváří CacheDataSchemaProvider z AppSettings + MLRest

/Commands
  IDesignerCommand.cs       — rozhraní pro undo/redo
  /Implementations          — konkrétní příkazy

/Designer
  DesignerState.cs          — stav designeru (výběr, zoom, undo stack)
  DesignerCanvas.cs         — myš, klávesnice, hit testing, render
  PreviewCanvas.cs          — render šablony bez chrome (live preview); fit-to-width zoom; mock DataContext

/schemas
  *.schema.json             — schémata agend (offline fallback); kopírováno do output dir

/section-templates (runtime)
  *.json                    — custom šablony sekcí (uložené uživatelem); vedle .exe

appsettings.json            — konfigurace Caché připojení + seznam agend (CopyToOutput)
MainWindow.xaml / .cs       — hlavní okno s toolbarem a panely
ElementPropertiesWindow.xaml / .cs — dialog vlastností elementu/sekce
```

### Detailní mapa zodpovědností

#### Kořen projektu
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `MainWindow.xaml/.cs` | `MainWindow` | Hlavní okno, toolbar, mouse state machine, menu handlers |
| `ElementPropertiesWindow.xaml/.cs` | `ElementPropertiesWindow` | Modal dialog pro vlastnosti (wrapper nad PropertiesPanel) |

#### /Models/Elements
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `TextElement.cs` | `TextElement` | Text s placeholder supportem, font, barvy, zarovnání |
| `ImageElement.cs` | `ImageElement` | Statický obrázek (Base64) nebo z datasource |
| `RectangleElement.cs` | `RectangleElement` | Obdélník — pozadí, rámeček, fill |
| `LineElement.cs` | `LineElement` | Čára/oddělovač — horizontální nebo vertikální |
| `TableElement.cs` | `TableElement` | Tabulka s opakováním řádků, sloupce, záhlaví |

#### /Commands/Implementations
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `IDesignerCommand.cs` | `IDesignerCommand` | Rozhraní: `string Description`, `Execute()`, `Undo()` |
| `MoveElementCommand.cs` | `MoveElementCommand` | Přesun elementu. Ukládá původní a novou pozici (X, Y v mm) |
| `ResizeElementCommand.cs` | `ResizeElementCommand` | Změna velikosti elementu. Ukládá X, Y, Width, Height (Height může být null) |
| `CompositeCommand.cs` | `CompositeCommand` | Složený příkaz — agreguje více příkazů do jednoho undo/redo kroku. Undo v opačném pořadí |
| `SetPropertyCommand.cs` | `SetPropertyCommand` | Změna jedné vlastnosti. Ukládá starou/novou hodnotu jako `object?` + `Action<object?>` |
| `AddElementCommand.cs` | `AddElementCommand` | Přidání elementu do sekce (s undo = odebrání) |
| `DeleteElementCommand.cs` | `DeleteElementCommand` | Smazání elementu ze sekce (s undo = obnovení na původní index) |
| `DeleteSectionCommand.cs` | `DeleteSectionCommand` | Smazání sekce ze šablony (s undo = obnovení) |
| `MoveSectionCommand.cs` | `MoveSectionCommand` | Přesun sekce v seznamu o ±1 (s undo) |

#### /Controls
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `PropertiesPanel.xaml/.cs` | `PropertiesPanel` | UserControl generující editovatelné řádky z `PropertyDescriptor` seznamu. `SetContext()` napojí callbacks. `ShowElement()` / `ShowDescriptors()` / `ShowMessage()` přepínají obsah |

#### /Designer
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `DesignerState.cs` | `DesignerState` | Stav editoru: výběr, zoom, snap, grid, undo/redo stacky. `Execute(cmd)` volá command |
| `DesignerCanvas.cs` | `DesignerCanvas` | Renderovací pipeline, hit testing (elementy, sekce, resize handles), výpočet souřadnic sekcí |
| `DesignerCanvas.cs` | `ResizeHandle` (enum) | 8 resize handles: TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight |
| `PreviewCanvas.cs` | `PreviewCanvas` | Live preview bez designer chrome, fit-to-width zoom, mock DataContext |

---

## Datový model

### Template
```csharp
class Template {
    Guid Id
    string Name
    string Description
    string? AgendaCode        // kód agendy pro databázová pole: "JR", "FAKTURY" — null = obecná šablona
    PageSize PageSize
    List<Section> Sections
}

class PageSize {
    float Width               // mm
    float Height              // mm
    float MarginTop           // mm
    float MarginBottom        // mm
    float MarginLeft          // mm
    float MarginRight         // mm
}
```

### Section (univerzální)
```csharp
class Section {
    Guid Id
    string Name

    // Chování tisku
    SectionPrintBehavior PrintBehavior      // určuje KDY a JAK ČASTO se sekce tiskne
    SectionOverflowBehavior OverflowBehavior // určuje chování při přetečení stránky

    // Rozměry
    float? DesignerHeight                   // výška v mm pro designer — null u dynamických sekcí

    // Pokročilé
    string? VisibilityExpression            // podmínka viditelnosti, např. "{PAGE} > 1"

    // Speciální properties (pro některé případy použití)
    string? DataSource                      // klíč dat pro PrintBehavior=PerDataRow
    float? RowHeight                        // výška řádku pro PrintBehavior=PerDataRow

    // Obsah
    List<TemplateElement> Elements
    Dictionary<string, string>? CustomProperties
}

enum SectionPrintBehavior {
    Once,                         // vytiskne se jednou (výchozí)
    OnEveryPage,                  // každá stránka (hlavička/patička)
    OnFirstPageOnly,              // jen první stránka
    OnLastPageOnly,               // jen poslední stránka
    OnEveryPageExceptFirst,       // všechny kromě první
    PerDataRow                    // LEGACY — logika opakování patří do elementů (TableElement), ne sekcí
}

enum SectionOverflowBehavior {
    KeepTogether,                 // nepřerušovat, přesunout celou na další stránku
    SplitAcrossPages,             // rozdělí se přes stránky (tabulky, dlouhý text)
    AllowSplit                    // rozdělení povoleno ale ne preferováno
}
```

**DŮLEŽITÁ ZMĚNA (refactoring 2026-03):**
- Section je nyní **univerzální třída** (ne abstract)
- Chování sekce určují **properties** (PrintBehavior, OverflowBehavior), **ne typ třídy**
- Podtřídy (SectionHeader, SectionFooter, ...) existují pouze pro **zpětnou kompatibilitu JSON** a jako **factory konstruktory** s výchozími hodnotami
- Nové šablony by měly používat přímo `Section` s nastavenými properties
- `DesignerHeight = null` znamená dynamickou sekce — designer zobrazí placeholder výšku
- Print engine ignoruje DesignerHeight a počítá výšku za běhu z obsahu

### Běžné případy použití sekcí

| Požadavek | Nastavení |
|---|---|
| **Hlavička stránky** | PrintBehavior=OnEveryPage, OverflowBehavior=KeepTogether, DesignerHeight=25mm |
| **Patička stránky** | PrintBehavior=OnEveryPage, OverflowBehavior=KeepTogether, DesignerHeight=15mm |
| **Titulní stránka** | PrintBehavior=OnFirstPageOnly, DesignerHeight=30mm |
| **Závěrečná stránka** | PrintBehavior=OnLastPageOnly, DesignerHeight=20mm |
| **Hlavička kromě první** | PrintBehavior=OnEveryPageExceptFirst, DesignerHeight=25mm |
| **Pevný obsah** | PrintBehavior=Once, DesignerHeight=30mm |
| **Oddělovač** | PrintBehavior=Once, DesignerHeight=5mm |
| **Tabulka** | PrintBehavior=Once + TableElement s DataSource a Columns (logika opakování je v elementu!) |

### Custom šablony sekcí (uživatelské)

Uživatel si může uložit často používané sekce jako šablony a znovu je používat.

**Ukládání:**
- Pravý klik na sekci v levém panelu → "💾 Uložit jako šablonu..."
- Zadá název → uloží se do `section-templates/{název}.json`
- Formát: přesně stejný jako sekce v Template JSON (s elementy)

**Načítání:**
- Menu `[+ Přidat sekci ▾]` automaticky načte všechny `*.json` z `section-templates/`
- Deep clone s novými Guid při vložení
- Funguje offline (file-based)

**V1 implementace (nyní):**
- `SectionTemplateManager` — file-based provider
- Ukládá do složky vedle .exe: `bin/Debug/net9.0-windows/section-templates/`
- Portable (lze zkopírovat složku jinam)

**V2 plán (budoucnost):**
- DB backend přes `CacheSectionTemplateProvider`
- Sdílené šablony mezi uživateli
- Možnost hybrid (lokální + DB šablony)

### Deprecated typy sekcí (zpětná kompatibilita)

**VŠECHNY podtřídy sekcí byly odstraněny.** Nový kód používá přímo `Section`.

Původní typy a jejich ekvivalent:

| Starý typ | Ekvivalentní nastavení Section |
|---|---|
| `SectionHeader` | PrintBehavior=OnEveryPage, DesignerHeight=25mm |
| `SectionFooter` | PrintBehavior=OnEveryPage, DesignerHeight=15mm |
| `SectionFixedHeight` | PrintBehavior=Once, DesignerHeight=30mm |
| `SectionTableDesigned` | PrintBehavior=PerDataRow, OverflowBehavior=SplitAcrossPages, RowHeight=8mm |
| `SectionSeparator` | PrintBehavior=Once, DesignerHeight=5mm |
| `SectionTable` | ❌ Deprecated — tabulka je element (TableElement), ne sekce |
| `SectionInfiniteText` | ❌ Deprecated — použijte Section + InfiniteTextElement |

### TemplateElement (abstraktní)
```csharp
abstract class TemplateElement {
    Guid Id
    float X, Y               // pozice v mm, relativně k sekci (levý horní roh)
    float Width              // šířka v mm
    float? Height            // výška v mm — null u dynamických elementů (TableElement atd.)
    int ZIndex
    bool IsLocked            // uzamčený element nelze přesunout ani změnit velikost
    string? VisibilityExpression  // podmíněná viditelnost, např. "{POCET} > 0", null = vždy viditelný
    abstract void Render(SKCanvas canvas, DataContext? data)
}
```

**Pozicování — klíčová pravidla:**
- Všechny souřadnice jsou v **milimetrech**, relativní k levému hornímu rohu sekce
- `Height` je `null` u dynamických elementů (TableElement, InfiniteText) — výšku vypočítá print engine za běhu podle dat
- Statické elementy (TextElement, RectangleElement, ImageElement, LineElement) mají vždy pevnou výšku
- Print engine bere pozice jak jsou uloženy v JSON — nepočítá žádný layout kromě dynamických výšek
- Konverze mm → px probíhá až při renderování (designer i print engine zvlášť podle svého DPI/zoomu)

### Typy elementů
| Typ | Popis |
|---|---|
| `TextElement` | Statický nebo datově vázaný text |
| `ImageElement` | Obrázek (statický nebo z dat) |
| `RectangleElement` | Obdélník / pozadí |
| `LineElement` | Čára / oddělovač |
| `TableElement` | Speciální tabulka/jízdní řád s vlastní logikou |

Systém musí být **rozšiřitelný** — nové typy elementů i sekcí přidáváme podtřídami.

### DataSchema (schéma dostupných DB polí — pouze design time)
```csharp
class DataSchema {
    string AgendaCode             // "JR", "FAKTURY" — odpovídá Template.AgendaCode
    string AgendaName             // "Jízdní řád" — pro UI
    List<DataField> Fields        // flat list — primární formát V1, přímá náhrada starého Caché flat-listu
    List<DataFieldGroup> Groups   // volitelné skupiny — rozšíření pro V2+ (není nutné v V1)
}

class DataField {
    string FieldName              // "CISLO_LINKY" → uživatel píše {CISLO_LINKY}
    string Label                  // "Číslo linky" — zobrazuje se v UI browseru
    FieldDataType DataType        // Text | Number | Date | Time | DateTime | Boolean (metadata pro UI)
    string? Example               // "42" — ukázka hodnoty v browseru
    string? Description           // volitelný delší popis pro uživatele
}

class DataFieldGroup {           // volitelné — organizační metadata pro V2+
    string Name                   // "Záhlaví sestavy", "Zastávky"
    FieldScope Scope              // Scalar (jedna hodnota) | Table (kolekce řádků)
    string? CollectionKey         // klíč pro DataContext.GetRows() — pouze pro Scope = Table
    List<DataField> Fields
}
```

**Klíčová pravidla:**
- DataSchema je **pouze metadata pro designer** — print engine ji nepotřebuje a nepoužívá
- `{FIELD_NAME}` placeholder lze použít v libovolném TextElementu kdekoliv v šabloně — schema neeviduje omezení
- Grouping (Scalar/Table scope) je **organizační hint pro UI**, nikoliv tvrdé omezení na použití placeholderu
- Schema je **oddělená od šablony** — Template ukládá jen `AgendaCode` (string reference), schema samotná je v `schemas/JR.schema.json`
- Flat `Fields` (V1) a hierarchické `Groups` (V2) mohou koexistovat v jednom souboru; UI zobrazí skupiny pokud jsou definované, jinak flat list

**Dva typy JSON souborů v systému:**
- `*.json` — šablona (Template) — definuje layout stránky, sekce, elementy
- `*.schema.json` — schéma agendy (DataSchema) — definuje dostupná DB pole, uloženo ve složce `schemas/`

**IDataSchemaProvider** — rozhraní pro načítání schémat:
```csharp
interface IDataSchemaProvider {
    IReadOnlyList<AgendaInfo> GetAvailableAgendas()   // seznam dostupných agend
    DataSchema? GetSchema(string agendaCode)           // načte schéma pro danou agendu
}
```

Implementace (V1):
- `JsonFileDataSchemaProvider` — načítá `schemas/*.schema.json` ze složky aplikace (offline fallback)
- `CacheDataSchemaProvider` — delegate pattern (`Func<string, IReadOnlyList<string>?>`) pro volání MLRest;
  parser `ParseField()` zvládá formáty: `"FIELD|Label"`, `"DBFieldId~FIELD"`, `"DBFieldId~FIELD|Label"`
- `CompositeDataSchemaProvider` — Caché primary + JSON fallback; vrátí výsledek prvního úspěšného
- Wiring: `Config/CacheSchemaWiring.TryCreate(AppSettings)` → vrátí null pokud Caché není nakonfigurováno

---

## Renderovací pipeline

```
OnPaintSurface:
  1. Pozadí stránky + okraj
  2. Grid (pokud zapnut)
  3. Pro každou sekci:
     a. Záhlaví sekce (název, resize handle)
     b. Pro každý element → element.Render(canvas)
  4. Selection handles nad vybranými elementy
  5. Rubber band selection rect (pokud se táhne)
```

---

## Stav designeru

```csharp
class DesignerState {
    Template CurrentTemplate
    List<TemplateElement> SelectedElements
    bool SnapToGrid
    float GridSize
    float Zoom
    Stack<IDesignerCommand> UndoStack
    Stack<IDesignerCommand> RedoStack
}
```

---

## Interakční model (stavový automat myši)

```
Idle
  → dvojklik na element                       → ShowForElement() (dialog vlastností)
  → klik na resize handle (single selection)  → Resizing
  → klik na element                           → Dragging
  → klik na prázdné místo                     → RubberBand

Dragging   → MouseUp   → EndDrag(commit: true)    → MoveElementCommand nebo CompositeCommand
           → ztrácí    → EndDrag(commit: false)   → obnova původních pozic

Resizing   → MouseUp   → EndResize(commit: true)  → ResizeElementCommand
           → ztrácí    → EndResize(commit: false) → obnova původní velikosti

RubberBand → MouseUp   → EndRubberBand()          → aktualizace SelectedElements (bez undo)
           → ztrácí    → EndRubberBand()
```

### Klíčové detaily interakce

**Rubber band overlay** se kreslí přímo v `OnPaintSurface` po `_canvas.Render()` — modrý průhledný obdélník (SKPaint s Alpha).

**Clipboard** (`_clipboard: List<TemplateElement>`) je interní (ne systémová schránka). Copy = klon každého elementu. Paste = nové klony s offsetem +5 mm, přidané přes `AddElementCommand`.

**Zoom** lze měnit výběrem z ComboBoxu (50/75/100/150/200 %) nebo **Ctrl+scroll** na canvasu — handler `OnCanvasScrollWheel` na `CanvasScrollViewer.PreviewMouseWheel` posune index ComboBoxu o ±1.

**Správa sekcí** v levém panelu: každá položka má tlačítka ▲▼ (volají `MoveSection()`) a context menu (Vlastnosti, Přesunout, Smazat). Klávesová zkratka **Ctrl+Shift+↑↓** přesune aktivní sekci. `DeleteSection()` před smazáním odznaší elementy smazané sekce, přepne `ActiveSection` a skryje dialog vlastností.

**Klíčové detaily dragu:**
- Drag používá **delta přístup**: ukládá canvas px při stisku, každý frame počítá deltaMm = (currentPx - startPx) / MmToPx
- Snap: zaokrouhlí finální pozici referenčního elementu na mřížku, z toho dopočítá delta → konzistentní pohyb i při multi-selectu
- Multi-drag: všechny vybrané elementy se pohybují o stejnou deltaMm
- `CompositeCommand` vznikne pouze pokud se skutečně pohnul víc než 1 element

---

## Klíčové implementační vzory

### Polymorfní JSON serializace
`Section` a `TemplateElement` jsou abstraktní — serializace používá `JsonPolymorphic` + `JsonDerivedType`:
```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "TextElement")]
public abstract class TemplateElement { ... }
```
Při přidání nového typu elementu/sekce: přidat `[JsonDerivedType]` atribut na bázovou třídu.

### Barvy
Barvy jsou uloženy jako string `"#AARRGGBB"` (SkiaSharp formát, **ne** WPF `#AARRGGBB`):
```csharp
public string TextColor { get; set; } = "#FF000000";  // černá
[JsonIgnore] public SKColor SkTextColor => SKColor.Parse(TextColor);
```
Průhledná = `"#00FFFFFF"`. SkiaSharp `SKColor.Parse()` tuto syntaxi zvládá přímo.

### RenderContext
Elementy dostávají `RenderContext` místo holého `SKCanvas`. Kontext obsahuje:
- `Canvas` — SkiaSharp canvas
- `MmToPx` — koeficient pro převod mm → pixely
- `Data` — DataContext (null v designeru, mock v preview, reálná data v print enginu)
- `SectionOriginX/Y` — absolutní pozice sekce na canvasu v px

Elementy volají `ctx.ToCanvasRect(X, Y, Width, height)` pro přepočet souřadnic.

### DataContext — resolvování placeholderů
```csharp
ctx.Data?.ResolvePlaceholders("Řidič: {JMENO}") ?? "Řidič: {JMENO}"
```

### Copy/paste — Clone()
Každý element implementuje `Clone()` vracející hlubokou kopii s novým `Guid.NewGuid()` Id.

### Extensibility bag
`TemplateElement` a `Section` mají `Dictionary<string, string>? CustomProperties` —
pro budoucí vlastnosti bez změny modelu a bez nutnosti verzovat JSON schéma.

### PropertyDescriptor systém

Každý element implementuje `GetPropertyDescriptors()` vracející seznam deskriptorů pro Properties panel. Panel generuje editovatelné řádky dynamicky.

**Dostupné typy deskriptorů:**
- `StringPropertyDescriptor` — textové pole (s volitelným `MultiLine = true`)
- `FloatPropertyDescriptor` — numerický input s `Min`, `Max`, `Format`, `Unit`
- `BoolPropertyDescriptor` — checkbox
- `ColorPropertyDescriptor` — color picker (WinForms ColorDialog)
- `EnumPropertyDescriptor` — dropdown s definovanými možnostmi `Options = [(label, value), ...]`

**Příklad použití:**
```csharp
public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
[
    new StringPropertyDescriptor {
        Label = "Text", Group = "Obsah",
        Get = () => Text, Set = v => Text = v,
        MultiLine = true
    },
    new FloatPropertyDescriptor {
        Label = "Velikost", Group = "Font",
        Get = () => FontSize, Set = v => FontSize = v,
        Min = 4, Max = 200, Unit = "pt"
    },
    new EnumPropertyDescriptor {
        Label = "Zarovnání", Group = "Formát",
        Get = () => HorizontalAlignment,
        Set = v => HorizontalAlignment = (TextHorizontalAlignment)v,
        Options = [
            ("Vlevo", (object)TextHorizontalAlignment.Left),
            ("Na střed", (object)TextHorizontalAlignment.Center),
            ("Vpravo", (object)TextHorizontalAlignment.Right)
        ]
    },
    .. GetPositionDescriptors()  // X, Y, Width, Height z base class
];
```

---

## Jak přidat nový typ elementu

1. Vytvořit třídu dědící z `TemplateElement` v `/Models/Elements/`
2. Implementovat `Render(RenderContext ctx)` a `Clone()`
3. Implementovat `GetPropertyDescriptors()` — vrací seznam deskriptorů pro panel vlastností
4. Přidat `[JsonDerivedType(typeof(NovyElement), "NovyElement")]` na `TemplateElement`
5. Přidat do toolbar menu v `BtnAddElement_Click`

### Jak přidat vlastnost k existujícímu elementu

```csharp
// 1. Přidat C# property do třídy elementu
public float Opacity { get; set; } = 1.0f;

// 2. Přidat deskriptor do GetPropertyDescriptors()
new FloatPropertyDescriptor {
    Label = "Průhlednost", Group = "Vzhled",
    Get = () => Opacity, Set = v => Opacity = v,
    Min = 0f, Max = 1f, Format = "0.##"
},

// 3. Přidat do Clone()
Opacity = Opacity,

// 4. Použít v Render() — žádná změna v panelu ani dialogu není potřeba
```

### Jak přidat nový typ sekce

Sekce je nyní univerzální třída — nové "typy" sekcí jsou jen presety properties:

1. Přidat preset do `BtnAddSection_Click` menu:
```csharp
var section = new Section {
    Name = "Nový typ sekce",
    PrintBehavior = SectionPrintBehavior.Once,
    OverflowBehavior = SectionOverflowBehavior.KeepTogether,
    DesignerHeight = 30f
};
```

2. Volitelně: Vytvořit custom šablonu sekce pro opakované použití (uživatel to může udělat přes UI)

---

## JSON formáty a příklady

### Formát schématu agendy (*.schema.json)

```json
{
  "agendaCode": "JR",
  "agendaName": "Jízdní řád",
  "fields": [
    { "name": "CISLO_LINKY",            "label": "Číslo linky",            "type": "number", "example": "42" },
    { "name": "LOKALNI_OZNACENI_LINKY", "label": "Lokální označení linky", "type": "text",   "example": "A42" },
    { "name": "TITULEK",                "label": "Titulek sestavy",         "type": "text",   "example": "Jízdní řád linky 42" },
    { "name": "CAS_ODJEZDU",            "label": "Čas odjezdu",             "type": "time",   "example": "08:42" }
  ]
}
```

Pole `groups` je volitelné rozšíření pro V2+:
```json
{
  "agendaCode": "JR",
  "agendaName": "Jízdní řád",
  "fields": [],
  "groups": [
    {
      "name": "Záhlaví sestavy",
      "scope": "scalar",
      "fields": [
        { "name": "CISLO_LINKY", "label": "Číslo linky", "type": "number", "example": "42" }
      ]
    },
    {
      "name": "Zastávky",
      "scope": "table",
      "collectionKey": "ZASTAVKY",
      "fields": [
        { "name": "POPIS_ZASTAVKA", "label": "Název zastávky", "type": "text", "example": "Praha hl.n." }
      ]
    }
  ]
}
```

Platné hodnoty pro `type`: `text`, `number`, `date`, `time`, `dateTime`, `boolean`.
Platné hodnoty pro `scope`: `scalar`, `table`.

### Formát šablony (*.json)

```json
{
  "version": "1.0",
  "id": "...",
  "name": "Jízdní řád CB_DP",
  "agendaCode": "JR",
  "pageSize": {
    "width": 210, "height": 297,
    "marginTop": 10, "marginBottom": 10, "marginLeft": 10, "marginRight": 10
  },
  "sections": [
    {
      "name": "Záhlaví",
      "printBehavior": "OnEveryPage",
      "overflowBehavior": "KeepTogether",
      "designerHeight": 25,
      "elements": [
        {
          "type": "TextElement",
          "x": 10, "y": 5, "width": 190, "height": 12,
          "text": "Jízdní řád {NAZEV}",
          "fontSize": 14,
          "fontWeight": "Bold",
          "textColor": "#FF000000"
        }
      ]
    }
  ]
}
```

### JSON serializer nastavení

```csharp
WriteIndented = true                          // odsazený výstup, čitelný člověkem
PropertyNamingPolicy = CamelCase              // "printBehavior" místo "PrintBehavior"
DefaultIgnoreCondition = WhenWritingNull      // null hodnoty se do JSON nevypisují
JsonStringEnumConverter()                     // "OnEveryPage" místo 1
RoundedFloatConverter (4 des. místa)          // 0.5000001 → 0.5
```

---

## Pravidla pro psaní kódu

- Jazyk kódu: **anglicky** (názvy tříd, metod, proměnných)
- Komentáře a UI texty: **česky**
- Každá akce uživatele musí být implementována jako `IDesignerCommand` (undo/redo)
- Elementy se renderují samy — designer pouze iteruje
- Pozice elementů jsou **relativní k sekci**, ne k celé stránce
- Jednotky v modelu jsou **milimetry**, konverze na px probíhá při renderování
- Nepoužívat `SKElement.PaintSurface` v XAML — pouze v code-behind
- Data binding syntaxe: `{NAZEV_POLE}` — konzistentní napříč celým systémem
- `Height = null` u elementu znamená dynamická výška — print engine ji vypočítá, designer zobrazí placeholder
- Uzamčený element (`IsLocked = true`) nelze v designeru přesunout, změnit velikost ani smazat bez odemčení
- `VisibilityExpression = null` znamená vždy viditelný — neprázdný řetězec je výraz vyhodnocovaný v print enginu

---

## Rozložení hlavního okna (WPF)

```
┌──────────────────────────────────────────────────────────────────────┐
│  Menu bar: Soubor | Upravit | Zobrazit | Nástroje                    │
├──────────────────────────────────────────────────────────────────────┤
│  Toolbar: [+ Sekce ▾] [+ Element ▾] | [Zpět] [Vpřed] | [Zoom ▾]    │
│           [Grid ☐] [Snap ☐] | [Uložit] [Otevřít]                    │
├────────────────┬─────────────────────────────┬───────────────────────┤
│ TabControl     │                             │                       │
│ (~200px)       │   Canvas (SKElement)        │  Live preview         │
│ ┌─Sekce──Pole─┐│   — hlavní editační plocha  │  (~300px, skryt.)     │
│ │ Seznam sekcí││                             │                       │
│ │ s ikonkou   ││                             │                       │
│ │ D&D reorder ││                             │                       │
│ │             ││                             │                       │
│ │ [+ Přidat]  ││                             │                       │
│ └─────────────┘│                             │                       │
├────────────────┴─────────────────────────────┴───────────────────────┤
│  Panel vlastností (~200px, kontextový podle výběru)                  │
└──────────────────────────────────────────────────────────────────────┘
```

**Implementační poznámky:**
- Layout je WPF `Grid` se třemi sloupci a `GridSplitter` mezi nimi
- Levý panel je `TabControl` se dvěma záložkami:
  - **"▣ Sekce"** — seznam sekcí s D&D reorder, tlačítko Přidat
  - **"⊞ Pole agendy"** — browser DB polí; dvojklik → vloží `{FIELD_NAME}` do TextElement nebo schránky
- Panel sekcí, live preview i panel vlastností jsou skryvatelné
- Výchozí šířky: levý panel 200px, canvas zbytek, live preview 300px (skrytý)
- Panel vlastností dole má výchozí výšku ~200px, skryvatelný
- Změna layoutu je triviální — jen úprava `Grid` definice v XAML

---

## Co tento projekt není

- Není to WYSIWYG Word/Excel editor
- Není to free-form canvas bez struktury (sekce jsou povinné)
- Print engine (krok 2) je **samostatný projekt** — designer pouze ukládá šablony jako JSON
- V1 nepodporuje import starých šablon z InterSystems Globals

---

## Známé problémy a TODO

### ColorPicker minimalizace Properties window
**Problém:** Po použití WinForms ColorDialog v Properties panelu a kliknutí OK se celé hlavní okno aplikace minimalizuje.

**Příčina:** Interakce mezi WinForms ColorDialog a WPF ToolWindow (`ElementPropertiesWindow`). Po zavření WinForms dialogu se Windows snaží vrátit focus, ale kvůli ToolWindow stylu to způsobí nechtěné chování.

**Aktuální stav:** Neblokující — uživatel může Properties panel znovu otevřít. BackgroundColor se ukládá správně do JSON.

**Možná řešení:**
- Implementovat vlastní WPF ColorPicker control místo WinForms dialogu
- Změnit `WindowStyle` Properties dialogu z `ToolWindow` na `None` (ale ztratíme UI benefit)
- Použít WPF Extended Toolkit ColorPicker

**Lokace kódu:** `Controls/PropertiesPanel.xaml.cs` (řádek ~416, TODO komentář)

**Priorita:** Nízká (workaround: uživatel znovu otevře Properties panel)

---

## Nápady pro budoucí rozšíření (neplánováno, ale žádoucí)

### Fragmenty / kompozice elementů
Pojmenované skupiny elementů, které si uživatel může uložit a znovu vkládat.

```
Fragment = název + List<TemplateElement> (relativní souřadnice vůči levému hornímu rohu skupiny)
```

- **Uložení:** vyber elementy → „Uložit jako fragment" → soubory v `fragments/*.fragment.json`
- **Vložení:** třetí záložka v levém panelu „📦 Fragmenty" → double-click → deep-clone s novými Guid do aktivní sekce
- **Architektura:** `Models/Fragment.cs` + `Serialization/FragmentSerializer.cs`; žádný zásah do modelu šablony — fragmenty se při vložení rozloží na běžné elementy

### Editor sloupců TableElement
TableElement zatím nemá UI pro definici sloupců — výšky, šířky, formáty hodnot, záhlaví.

### V2 VisibilityExpression
Checkbox „Tisknout" je V1 (pouze true/false). V2 by podporoval výrazy jako `{POCET} > 0` — vyhodnocuje print engine, designer je zobrazí jako podmíněný badge bez evaluace.