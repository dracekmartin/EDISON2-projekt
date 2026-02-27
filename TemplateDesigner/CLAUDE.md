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

---

## Architektura projektu

```
/Models
  Template.cs               — kořenový objekt šablony
  PageSize.cs               — rozměry stránky v mm
  Section.cs                — abstraktní základ sekce + enums (SectionPrintBehavior, SectionOverflowBehavior)
  TemplateElement.cs        — abstraktní základ elementu
  DataContext.cs            — data pro renderování (print engine / preview)
  RenderContext.cs          — kontext renderování (canvas + MmToPx + DataContext + origin sekce)
  /Sections                 — konkrétní typy sekcí
  /Elements                 — konkrétní typy elementů

/Serialization
  TemplateSerializer.cs     — JSON ukládání a načítání

/Commands
  IDesignerCommand.cs       — rozhraní pro undo/redo
  /Implementations          — konkrétní příkazy

/Designer
  DesignerState.cs          — stav designeru (výběr, zoom, undo stack)
  DesignerCanvas.cs         — myš, klávesnice, hit testing, render

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

### Section (abstraktní)
```csharp
abstract class Section {
    Guid Id
    string Name
    float? DesignerHeight          // výška sekce v mm pro designer — null u dynamických sekcí
    SectionPrintBehavior PrintBehavior
    SectionOverflowBehavior OverflowBehavior
    List<TemplateElement> Elements
}

enum SectionPrintBehavior {
    Once,                         // výchozí
    OnEveryPage,                  // záhlaví/zápatí
    OnFirstPageOnly,
    OnLastPageOnly,
    OnEveryPageExceptFirst
}

enum SectionOverflowBehavior {
    KeepTogether,                 // nepřerušovat, přesunout na další stránku
    SplitAcrossPages,             // tabulky, jízdní řády
    AllowSplit
}
```

`DesignerHeight` je pouze vizuální pomůcka — udává jak vysoká sekce vypadá na canvasu. U dynamických sekcí (SectionTable, SectionInfiniteText) je `null` a designer zobrazí placeholder výšku. Print engine výšku sekce ignoruje a počítá ji za běhu z obsahu.

### Typy sekcí
| Typ | Popis |
|---|---|
| `SectionHeader` | Záhlaví — jednou nahoře |
| `SectionFixedHeight` | Pevná výška |
| `SectionTable` | Opakující se řádky tabulky |
| `SectionTableDesigned` | Tabulka s vlastním designem řádku |
| `SectionInfiniteText` | Roztahuje se podle obsahu |
| `SectionSeparator` | Oddělovač |
| `SectionFooter` | Zápatí |

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
│ Panel sekcí    │                             │                       │
│ (~200px)       │   Canvas (SKElement)        │  Live preview         │
│                │   — hlavní editační plocha  │  (~300px, skryt.)     │
│ Seznam sekcí   │                             │                       │
│ s ikonkou typu │                             │                       │
│ D&D reorder    │                             │                       │
│                │                             │                       │
│ [+ Přidat]     │                             │                       │
├────────────────┴─────────────────────────────┴───────────────────────┤
│  Panel vlastností (~200px, kontextový podle výběru)                  │
└──────────────────────────────────────────────────────────────────────┘
```

**Implementační poznámky:**
- Layout je WPF `Grid` se třemi sloupci a `GridSplitter` mezi nimi
- Panel sekcí, live preview i panel vlastností jsou skryvatelné
- Výchozí šířky: panel sekcí 200px, canvas zbytek, live preview 300px (skrytý)
- Panel vlastností dole má výchozí výšku ~200px, skryvatelný
- Změna layoutu je triviální — jen úprava `Grid` definice v XAML

---

## Co tento projekt není

- Není to WYSIWYG Word/Excel editor
- Není to free-form canvas bez struktury (sekce jsou povinné)
- Print engine (krok 2) je **samostatný projekt** — designer pouze ukládá šablony jako JSON
- V1 nepodporuje import starých šablon z InterSystems Globals