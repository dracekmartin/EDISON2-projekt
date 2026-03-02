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
```

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
  → klik na element         → Dragging
  → klik na prázdné místo   → RubberBandSelecting
  → klik na resize handle   → Resizing

Dragging → MouseUp          → Idle (zapsat MoveCommand do undo stacku)
Resizing → MouseUp          → Idle (zapsat ResizeCommand)
RubberBandSelecting → MouseUp → Idle (aktualizovat výběr)
```

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