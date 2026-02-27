# Template Designer — Architektura a vývojářská dokumentace

Tento dokument je určen **vývojářům**. Popisuje strukturu kódu, architektonická rozhodnutí a návody na rozšíření systému.

Pro uživatelskou dokumentaci viz [README.md](README.md).

---

## Přehled systému

Systém se skládá ze dvou nezávislých částí:

**1. Designer** — WPF aplikace pro tvorbu šablon
- Uživatel vizuálně skládá šablonu ze sekcí a elementů
- Výstup: JSON soubor šablony

**2. Print Engine** (budoucí projekt)
- Vstup: JSON šablona + data
- Výstup: PDF soubor
- Renderování pomocí stejného SkiaSharp kódu jako designer (sdílená logika)

Oddělení těchto dvou částí je záměrné — designer neví nic o datech, print engine neví nic o UI.

---

## Mapa codebase

### Kořen projektu

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `MainWindow.xaml / .cs` | `MainWindow` | Hlavní okno aplikace. WPF layout (toolbar, panely, canvas). Registrace PaintSurface handleru. Mouse state machine. |
| `App.xaml / .cs` | `App` | Vstupní bod WPF aplikace. `DispatcherUnhandledException` handler pro zobrazení chyb. |

### /Models — datový model šablony

Čistá C# vrstva bez závislostí na WPF. Sdílená s budoucím print enginem.

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `Template.cs` | `Template` | Kořenový objekt šablony. Obsahuje `PageSize`, seznam `Section`, verzi JSON formátu, metadata. |
| `PageSize.cs` | `PageSize` | Rozměry stránky a okraje v mm. Computed `PrintableWidth/Height`. Statické presety A4/A3. |
| `Section.cs` | `Section` (abstract) | Základní třída pro všechny sekce. Definuje `Elements`, `DesignerHeight`, `PrintBehavior`, `OverflowBehavior`, `CustomProperties`. Nese `JsonPolymorphic` atributy. |
| `Section.cs` | `SectionPrintBehavior` (enum) | Kdy se sekce tiskne: `Once`, `OnEveryPage`, `OnFirstPageOnly`, `OnLastPageOnly`, `OnEveryPageExceptFirst`. |
| `Section.cs` | `SectionOverflowBehavior` (enum) | Co se stane při přetečení: `KeepTogether`, `SplitAcrossPages`, `AllowSplit`. |
| `TemplateElement.cs` | `TemplateElement` (abstract) | Základní třída pro všechny elementy. Pozice/rozměry v mm, `ZIndex`, `IsLocked`, `VisibilityExpression`, `CustomProperties`. Nese `JsonPolymorphic` atributy. Definuje `Render(RenderContext)` a `Clone()`. |
| `DataContext.cs` | `DataContext` | Přenáší data do elementů při renderování. `GetString`, `GetRows` (pro tabulky), `ResolvePlaceholders("{POLE}")`. V designeru null. |
| `RenderContext.cs` | `RenderContext` | Kontext předávaný do `Render()`. Obsahuje `SKCanvas`, `MmToPx`, `DataContext`, origin sekce v px. Helper metody `ToCanvasRect`, `WithSectionOrigin`. |

#### /Models/Elements

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `TextElement.cs` | `TextElement` | Statický nebo datově vázaný text (`{PLACEHOLDER}`). Font, velikost, styl, zarovnání, barva, word wrap. |
| `TextElement.cs` | `TextFontStyle`, `TextFontWeight`, `TextHorizontalAlignment`, `TextVerticalAlignment` (enums) | Typografické volby — záměrně nezávislé na WPF typech. |
| `RectangleElement.cs` | `RectangleElement` | Obdélník / pozadí. Barva výplně, barva okraje, tloušťka okraje, zaoblení rohů. |
| `LineElement.cs` | `LineElement` | Horizontální nebo vertikální čára. Barva, tloušťka, styl (Solid/Dashed/Dotted). |
| `ImageElement.cs` | `ImageElement` | Obrázek — statický (Base64) nebo z datového pole (`DataField`). Módy škálování (Fill/Fit/Stretch/None). |
| `TableElement.cs` | `TableElement` | Tabulka s dynamickými řádky z dat. `DataSource`, `RowHeight`, seznam `TableColumn`. Výška vždy `null` — dynamická. |
| `TableElement.cs` | `TableColumn` | Definice jednoho sloupce: záhlaví, šířka (mm), datové pole, zarovnání. |

#### /Models/Sections

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `SectionHeader.cs` | `SectionHeader` | Záhlaví. Výchozí: `OnEveryPage`, `KeepTogether`, výška 25 mm. |
| `SectionFixedHeight.cs` | `SectionFixedHeight` | Nejběžnější typ — pevná výška, statický obsah. Výchozí výška 30 mm. |
| `SectionTable.cs` | `SectionTable` | Tabulka s řádky z dat (`DataSource`). Dynamická výška, `SplitAcrossPages`. |
| `SectionTableDesigned.cs` | `SectionTableDesigned` | Tabulka kde uživatel navrhne layout jednoho řádku. Elements = šablona jednoho řádku, opakuje se pro každý záznam. |
| `SectionInfiniteText.cs` | `SectionInfiniteText` | Textová sekce roztahující se podle obsahu. `AllowSplit`. |
| `SectionSeparator.cs` | `SectionSeparator` | Tenký oddělovač (mezera nebo čára). Pevná výška (`Height` property). |
| `SectionFooter.cs` | `SectionFooter` | Zápatí. Výchozí: `OnEveryPage`, `KeepTogether`, výška 15 mm. |

### /Serialization

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `TemplateSerializer.cs` | `TemplateSerializer` | JSON serializace/deserializace `Template`. `SaveAsync` / `LoadAsync` / `Serialize` / `Deserialize`. Chyby hlásí přes `TemplateDeserializationException`. |
| `TemplateSerializer.cs` | `TemplateDeserializationException` | Výjimka pro chyby při načítání šablony (neplatný JSON, chybějící pole, I/O chyba). |

### /Commands — undo/redo

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `IDesignerCommand.cs` | `IDesignerCommand` | Rozhraní: `string Description`, `Execute()`, `Undo()`. |
| `Implementations/MoveElementCommand.cs` | `MoveElementCommand` | Přesun elementu. Ukládá původní a novou pozici (X, Y v mm). |
| `Implementations/ResizeElementCommand.cs` | `ResizeElementCommand` | Změna velikosti elementu. Ukládá původní a nové X, Y, Width, Height (Height může být null). |
| `Implementations/CompositeCommand.cs` | `CompositeCommand` | Složený příkaz — agreguje více příkazů do jednoho undo/redo kroku. Undo probíhá v opačném pořadí. |

### /Designer — interakce a stav

| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `DesignerState.cs` | `DesignerState` | Aktuální stav editoru: vybrané elementy, aktivní sekce, zoom, snap, grid, undo/redo stacky. `Execute(cmd)` volá command a uloží na undo stack. |
| `DesignerCanvas.cs` | `DesignerCanvas` | Renderovací pipeline (volá `element.Render()`), hit testing (elementy, sekce, resize handles), výpočet souřadnic sekcí. |
| `DesignerCanvas.cs` | `ResizeHandle` (enum) | 8 resize handles: `TopLeft`, `TopCenter`, `TopRight`, `MiddleLeft`, `MiddleRight`, `BottomLeft`, `BottomCenter`, `BottomRight`. |

---

## Mouse state machine

```
Idle
  → klik na resize handle (single selection)  → Resizing
  → klik na element                           → Dragging
  → klik na prázdné místo                     → clear výběru / aktivace sekce

Dragging → MouseUp   → EndDrag(commit: true)  → MoveElementCommand nebo CompositeCommand
         → ztrácí    → EndDrag(commit: false) → obnova původních pozic

Resizing → MouseUp   → EndResize(commit: true) → ResizeElementCommand
         → ztrácí    → EndResize(commit: false) → obnova původní velikosti
```

**Klíčové detaily dragu:**
- Drag používá **delta přístup**: ukládá canvas px při stisku, každý frame počítá deltaMm = (currentPx - startPx) / MmToPx
- Snap: zaokrouhlí finální pozici referenčního elementu na mřížku, z toho dopočítá delta → konzistentní pohyb i při multi-selectu
- Multi-drag: všechny vybrané elementy se pohybují o stejnou deltaMm
- `CompositeCommand` vznikne pouze pokud se skutečně pohnul víc než 1 element

---

## Renderovací abstrakce

### RenderContext

Elementy dostávají `RenderContext` (ne holý `SKCanvas`). Obsahuje vše potřebné pro vykreslení:

```csharp
public class RenderContext {
    SKCanvas Canvas       // SkiaSharp canvas
    float MmToPx          // koeficient mm → px (liší se dle DPI a zoomu designeru)
    DataContext? Data      // null v designeru, mock v preview, reálná data v print enginu
    float SectionOriginX  // absolutní X pozice sekce na canvasu (px)
    float SectionOriginY  // absolutní Y pozice sekce na canvasu (px)

    float Px(float mm)
    SKPoint ToCanvas(float xMm, float yMm)
    SKRect ToCanvasRect(float xMm, float yMm, float wMm, float hMm)
    RenderContext WithSectionOrigin(float xPx, float yPx)
}
```

### DataContext

```csharp
public class DataContext {
    string GetString(string key, string fallback = "")
    object? GetValue(string key)
    IReadOnlyList<DataContext> GetRows(string dataSource)  // pro TableElement
    string ResolvePlaceholders(string template)            // "{JMENO}" → "Jan Novák"
}
```

V designeru je `DataContext` vždy `null` — elementy renderují statický obsah nebo placeholder text.

---

## Jak přidat nový typ elementu

1. Vytvořit třídu dědící z `TemplateElement` v `/Models/Elements/`
2. Implementovat `Render(RenderContext ctx)` a `Clone()`
3. Přidat `[JsonDerivedType(typeof(NovyElement), "NovyElement")]` na `TemplateElement`
4. Přidat do UI (toolbar menu, properties panel)

## Jak přidat nový typ sekce

1. Vytvořit třídu dědící z `Section` v `/Models/Sections/`
2. Přidat `[JsonDerivedType(typeof(NovaSekce), "NovaSekce")]` na `Section`
3. Přidat do toolbar menu v `BtnAddSection_Click`

---

## SkiaSharp — důležité poznámky

- `PaintSurface` event na `SKElement` se **nesmí** registrovat v XAML (SkiaSharp.Views.WPF + .NET 9 omezení) — pouze v code-behind
- Event parameter se přijímá jako `object` a přetypovává přes `dynamic` kvůli omezení kompilátoru
- `using SkiaSharp;` **není** v implicit usings pro WPF projekty — musí být explicitní
- `using System.IO;` taktéž není implicitní v WPF projektech
- `SKFilterQuality` v SkiaSharp 3.x neexistuje (odstraněno)
- NU1701 warningy jsou očekávané a neškodné

---

## Konvence pro barvy

Barvy se ukládají jako string `"#AARRGGBB"` (SkiaSharp formát):
- Černá: `"#FF000000"`, Bílá: `"#FFFFFFFF"`, Průhledná: `"#00FFFFFF"`
- `SKColor.Parse(colorString)` — přímá konverze bez pomocné logiky

---

## Nastavení JSON serializeru

```csharp
WriteIndented = true                          // odsazený výstup, čitelný člověkem
PropertyNamingPolicy = CamelCase              // "printBehavior" místo "PrintBehavior"
DefaultIgnoreCondition = WhenWritingNull      // null hodnoty se do JSON nevypisují
JsonStringEnumConverter()                     // "OnEveryPage" místo 1
RoundedFloatConverter (4 des. místa)          // 0.5000001 → 0.5
```

Přípona souboru šablony: `.json`.

---

## Formát šablony (JSON ukázka)

```json
{
  "version": "1.0",
  "id": "...",
  "name": "Jízdní řád CB_DP",
  "pageSize": {
    "width": 210, "height": 297,
    "marginTop": 10, "marginBottom": 10, "marginLeft": 10, "marginRight": 10
  },
  "sections": [
    {
      "type": "SectionHeader",
      "name": "Záhlaví",
      "designerHeight": 25,
      "printBehavior": "OnEveryPage",
      "overflowBehavior": "KeepTogether",
      "elements": [
        {
          "type": "TextElement",
          "x": 10, "y": 5, "width": 190, "height": 12,
          "text": "Jízdní řád {NAZEV}",
          "fontSize": 14,
          "fontWeight": "Bold"
        }
      ]
    }
  ]
}
```

---

## Chování při tisku (paginace)

### PrintBehavior — kdy se sekce tiskne
| Hodnota | Popis |
|---|---|
| `Once` | Jednou, na místě kde je |
| `OnEveryPage` | Na každé stránce (záhlaví, zápatí) |
| `OnFirstPageOnly` | Pouze na první stránce |
| `OnLastPageOnly` | Pouze na poslední stránce |
| `OnEveryPageExceptFirst` | Na každé stránce kromě první |

### OverflowBehavior — přetečení sekce
| Hodnota | Popis |
|---|---|
| `KeepTogether` | Celá sekce přejde na další stránku |
| `SplitAcrossPages` | Sekce se rozdělí přes stránky (tabulky) |
| `AllowSplit` | Rozdělení povoleno ale ne preferováno |

---

## Rozšiřitelnost

- Nový typ elementu = nová třída dědící z `TemplateElement`
- Nový typ sekce = nová třída dědící z `Section`
- Každý element si renderuje sám sebe — `DesignerCanvas` pouze iteruje
- Print engine (krok 2) používá stejné třídy elementů a stejný `RenderContext`
