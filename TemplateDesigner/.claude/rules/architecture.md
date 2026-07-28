# Glob: dotnet/**

## Architektura projektu

**Struktura složek repozitáře:**
```
TemplateDesigner/
├── CACHE/                    - Caché ObjectScript kód (AgendaAPI, ZastavkoveJrMhdAPI, TemplateDesignerFAKTURY)
│   ├── .project              - "ALVA_TEMPLATE_DESIGNER"
│   └── ALVA/TemplateDesigner/API/
├── dotnet/                   - veškerý .NET/WPF zdrojový kód + .csproj
│   ├── TemplateDesigner.csproj
│   ├── ... (zdrojové soubory)
│   └── bin/, obj/            - generované buildem, v .gitignore
├── .vscode/tasks.json        - build task (Ctrl+Shift+B), debug přes C# Dev Kit (F5)
├── CLAUDE.md
└── README.md
```

**Zdrojový kód v `dotnet/`:**
```
/Models
  Template.cs               - kořenový objekt šablony
  PageSize.cs               - rozměry stránky v mm
  Section.cs                - univerzální sekce + enums (SectionPrintBehavior, SectionOverflowBehavior)
  TemplateElement.cs        - abstraktní základ elementu
  DataContext.cs            - data pro renderování (print engine / preview)
  RenderContext.cs          - kontext renderování (canvas + MmToPx + DataContext + origin sekce)
  /Elements                 - konkrétní typy elementů
  /Schema                   - schéma dostupných DB polí (design time only)
    DataSchema.cs           - DataSchema, DataField, DataFieldGroup, enums
    IDataSchemaProvider.cs  - rozhraní pro načítání schémat

/Serialization
  TemplateSerializer.cs             - JSON ukládání a načítání šablony + sekcí
  SectionTemplateManager.cs         - správa custom šablon sekcí (file-based V1, připraveno na DB V2)
  JsonFileDataSchemaProvider.cs     - načítání *.schema.json ze složky schemas/
  CacheAgendaProvider.cs            - IDataSchemaProvider + ITableTemplateProvider přes MLRest/AgendaAPI
  CompositeDataSchemaProvider.cs    - Caché primary + JSON fallback

/Config
  AppSettings.cs            - POCO konfigurace + static Load() z templatedesigner.appsettings.json
  CacheSchemaWiring.cs      - vytváří CacheAgendaProvider z AppSettings + MLRest

/Commands
  IDesignerCommand.cs       - rozhraní pro undo/redo
  /Implementations          - konkrétní příkazy

/Designer
  DesignerState.cs          - stav designeru (výběr, zoom, undo stack)
  DesignerCanvas.cs         - myš, klávesnice, hit testing, render
  PreviewCanvas.cs          - render šablony bez chrome (live preview); používá PageLayoutEngine pro multi-page layout

/Models/Layout
  LayoutResult.cs           - výstupní struktury: LayoutResult, Page, PlacedSection
  IElementHeightCalculator.cs - interface pro měření výšky dynamických elementů (decoupling od SkiaSharp)
  PageLayoutEngine.cs       - hlavní layout algoritmus: Template + DataContext → List<Page>
  SkiaHeightCalculator.cs   - implementace IElementHeightCalculator pomocí SkiaSharp

/Rendering                  - sdílená infrastruktura pro stylování buněk tabulek
  CellDefaults.cs           - autoritativní výchozí hodnoty (ResolvedTextStyle); poslední vrstva kaskády
  CellRenderer.cs           - DrawCell: bg → padding → HAlign+VAlign → decorations; jediné místo pozicování textu buňky
  ColumnRenderer.cs         - DrawRow: orchestruje řádek sloupců + volitelné svislé mřížkové čáry
  CellStyleEditOptions.cs   - record s Allow<X> flagy pro per-kontextové skrývání UI editace stylu

/Services                   - aplikační služby (async wrappery nad Caché + AI)
  CacheTemplateService.cs   - async CRUD šablon přes ALVA.TemplateDesigner.API.Sablony (Task.Run wrapper)
  TdPreferencesService.cs   - typed wrapper nad IUserPrefsService (per IdFa×user state, viz Persistence stavu)
  CapabilityCatalog.cs      - katalog schopností designeru (pro AI kontext)
  AnthropicApiClient.cs     - sdílený HTTP klient k Anthropic API (statický HttpClient, per-request headers)
  AiImportEnricher.cs       - sémantické obohacení PDF importu (heuristiky → AI enrichment)
  AiVisionImportService.cs  - Claude Vision rozpoznání JŘ struktury (AgendaPromptContext prompt systém)

/PdfImport                  - PDF → šablona pipeline (PdfPig extrakce + AI vision)
  PdfPageExtractor.cs / PdfPageRasterizer.cs / PdfTextGrouper.cs / PdfCoordinateConverter.cs / PdfFontMapper.cs - extrakce textu, čar, obrázků, geometrie
  PdfToTemplateConverter.cs - heuristická konverze extrahovaných dat → elementy
  AiGuidedScheduleBuilder.cs / AiVisionImportPipeline.cs - AI-řízený import JŘ (rasterizace → Vision → přesné elementy z PdfPig)
  ExtractedData.cs / EnrichedImportResult.cs - mezistruktury pipeline

/schemas
  *.schema.json             - schémata agend (offline fallback); kopírováno do output dir

/section-templates (runtime)
  *.json                    - custom šablony sekcí (uložené uživatelem); vedle .exe

templatedesigner.appsettings.json - konfigurace Caché připojení + seznam agend (CopyToOutput); záměrně non-generický název kvůli sdílené složce C:\Edison\
MainWindow.xaml / .cs       - hlavní okno s toolbarem a panely
ElementPropertiesWindow.xaml / .cs - dialog vlastností elementu/sekce
```

---

## Detailní mapa zodpovědností

### Kořen projektu
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `MainWindow.xaml/.cs` | `MainWindow` | Hlavní okno, toolbar, mouse state machine, menu handlers |
| `ElementPropertiesWindow.xaml/.cs` | `ElementPropertiesWindow` | Modal dialog pro vlastnosti (wrapper nad PropertiesPanel) |

### /Models/Elements
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `TextElement.cs` | `TextElement` | Text s placeholder supportem, font, barvy, zarovnání |
| `ImageElement.cs` | `ImageElement` | Statický obrázek (Base64) nebo z datasource |
| `RectangleElement.cs` | `RectangleElement` | Obdélník - pozadí, rámeček, fill |
| `LineElement.cs` | `LineElement` | Čára/oddělovač - horizontální nebo vertikální |
| `BarcodeElement.cs` | `BarcodeElement` | QR kód nebo Code128 čárový kód (ZXing.Net) |
| `TableElement.cs` | `TableElement` | Tabulka Fixed/Dynamic, UI editor sloupců + řádků |
| `ScheduleModels.cs` | `ZastavkaData`, `OdjezdData`, ... | Runtime POCO modely pro JŘ elementy (neserializují se) |
| `ScheduleElementBase.cs` | `ScheduleElementBase` | Abstraktní základ JŘ elementů: DataSource klíče, Build helpery, společné properties |
| `StopScheduleElement.cs` | `StopScheduleElement` | Zastávkový JŘ: MHD minutová mřížka, ~30 properties |

### /Commands/Implementations
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `IDesignerCommand.cs` | `IDesignerCommand` | Rozhraní: `string Description`, `Execute()`, `Undo()` |
| `MoveElementCommand.cs` | `MoveElementCommand` | Přesun elementu. Ukládá původní a novou pozici (X, Y v mm) |
| `ResizeElementCommand.cs` | `ResizeElementCommand` | Změna velikosti elementu. Ukládá X, Y, Width, Height (Height může být null) |
| `CompositeCommand.cs` | `CompositeCommand` | Složený příkaz - agreguje více příkazů do jednoho undo/redo kroku. Undo v opačném pořadí |
| `SetPropertyCommand.cs` | `SetPropertyCommand` | Změna jedné vlastnosti. Ukládá starou/novou hodnotu jako `object?` + `Action<object?>` |
| `AddElementCommand.cs` | `AddElementCommand` | Přidání elementu do sekce (s undo = odebrání) |
| `DeleteElementCommand.cs` | `DeleteElementCommand` | Smazání elementu ze sekce (s undo = obnovení na původní index) |
| `DeleteSectionCommand.cs` | `DeleteSectionCommand` | Smazání sekce ze šablony (s undo = obnovení) |
| `MoveSectionCommand.cs` | `MoveSectionCommand` | Přesun sekce v seznamu o ±1 (s undo) |

### /Controls
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `PropertiesPanel.xaml/.cs` | `PropertiesPanel` | UserControl generující editovatelné řádky z `PropertyDescriptor` seznamu. `SetContext()` napojí callbacks. `ShowElement()` / `ShowDescriptors()` / `ShowMessage()` přepínají obsah |

### /Designer
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `DesignerState.cs` | `DesignerState` | Stav editoru: výběr, zoom, snap, grid, undo/redo stacky. `Execute(cmd)` volá command |
| `DesignerCanvas.cs` | `DesignerCanvas` | Renderovací pipeline, hit testing (elementy, sekce, resize handles), výpočet souřadnic sekcí |
| `DesignerCanvas.cs` | `ResizeHandle` (enum) | 8 resize handles: TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight |
| `PreviewCanvas.cs` | `PreviewCanvas` | Live preview bez designer chrome, používá PageLayoutEngine, multi-page rendering |

### /Models/Layout
| Soubor | Třída | Zodpovědnost |
|---|---|---|
| `LayoutResult.cs` | `LayoutResult`, `Page`, `PlacedSection` | Výstupní datové struktury layout enginu - co kam nakreslit |
| `IElementHeightCalculator.cs` | `IElementHeightCalculator` | Interface pro měření výšky dynamických elementů (decoupling od SkiaSharp) |
| `PageLayoutEngine.cs` | `PageLayoutEngine` | Hlavní algoritmus: klasifikace sekcí, výpočet výšek, stránkování, hlavičky/patičky, split, flow layout, PAGE/TOTAL_PAGES |
| `SkiaHeightCalculator.cs` | `SkiaHeightCalculator` | Měření výšky TextElement (word wrap) a TableElement pomocí SkiaSharp |

---

## Architekturální rozhodnutí (MVVM hranice)

### Proč SkiaSharp nelze bindovat - vždy imperativní

`SKElement` (SkiaSharp WPF kontrolka) nemá žádné bindovatelné `DependencyProperty` pro obsah.
Jediný způsob jak dostat obraz na canvas je `PaintSurface` event + imperativní volání SkiaSharp API:

```csharp
// NEJDE - SKElement nemá Content property
skCanvas.Content = "{Binding Template}"  // ❌

// MUSÍ BÝT - event + přímé kreslení
skCanvas.PaintSurface += OnPaintSurface;  // ✅ vždy v code-behind
private void OnPaintSurface(object? sender, object e)
{
    var args = (dynamic)e;
    _canvas.Render(args.Surface.Canvas, ...);  // imperativní SKCanvas API
}
```

Navíc `PaintSurface` nesmí být registrován v XAML (bug `SkiaSharp.Views.WPF` s .NET 9+) - pouze code-behind.

**Důsledek:** vše co ovlivňuje vizuální výstup canvasu (zoom, výběr, poloha elementů) se projeví
až při příštím překreslení vyvolaném `skCanvas.InvalidateVisual()`. Překreslení nevolá ViewModel
přímo - volá callback `RequestCanvasRefresh`.

---

### Co patří do ViewModelu vs. code-behind

**Do `TemplateDesignerViewModel` (business logika, testovatelné):**
- File operace (NewTemplate, OpenTemplate, SaveTemplate, ExportPreviewPdf)
- Clipboard (CopySelected, PasteClipboard, DuplicateSelected)
- Editace (DeleteSelected, ChangeZIndex, AlignElements, MoveByKey)
- Section management (AddSection, MoveSection, DeleteSection, ActivateSection)
- Undo/Redo wrapping + Commands (UndoCommand, RedoCommand, ...)
- Schema / parametry (RefreshFieldsList, LoadAgendaParameters, BuildPreviewMockData)
- ObservableCollections pro binding (Sections, SchemaGroups, PreviewParameters)
- Bindovatelné properties (ShowGrid, SnapToGrid, CanUndo, CanRedo, StatusText, ...)

**V `MainWindow.xaml.cs` (musí zůstat - neodstranitelná vazba na WPF/SkiaSharp):**
- `PaintSurface` eventy (SkiaSharp constraint)
- Mouse state machine (`_mouseMode`, drag, resize, rubber band)
- `UpdateCanvasSize()` - čte `skCanvas.ActualWidth/Height`
- `BtnAddSection_Click` / `BtnAddElement_Click` - runtime `ContextMenu` objekty
- Cursor management (`skCanvas.Cursor = ...`)
- `CaptureMouse()` / `ReleaseMouseCapture()`

---

### Vzor pro každé okno / dialog (závazný standard)

Každé okno v projektu se řídí stejným vzorem. Struktura souborů:

```
Windows/
└── MůjDialog/
    ├── MůjDialog.xaml              - pouze XAML, žádná logika
    ├── MůjDialog.xaml.cs           - jen WPF-specifický kód (viz níže)
    └── MůjDialogViewModel.cs       - veškerá logika + properties + commands
```

**ViewModel** dědí z `EDViewModel`:
```csharp
public class MůjDialogViewModel : EDViewModel
{
    // Bindovatelné properties přes Set()
    public string Foo { get => _foo; set => Set(ref _foo, value); }

    // Commands - RelayCommand pro akce tlačítek
    public RelayCommand DoSomethingCommand { get; }

    // Callbacks pro View-only operace (ViewModel nezná konkrétní WPF kontrolky)
    public Action<SomeType>? RequestSomeViewOperation { get; set; }

    public MůjDialogViewModel(...) { ... }

    // OK zavře dialog s true - override pokud potřebuješ validaci
    protected override void OnOk() { ... CloseDialog(true); }
}
```

**Code-behind** - pouze wiring, bez logiky:
```csharp
public partial class MůjDialog : Window
{
    public MůjDialog(MůjDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        // Napojení callbacků (jen pokud je potřeba přímý přístup ke kontrolce)
        vm.RequestSomeViewOperation = x => SomeControl.DoSomething(x);

        // Standardní zavření - vždy
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    // Pouze WPF-specifické eventy bez alternativy:
    // - DataGrid.BeginEdit() / CommitEdit()
    // - Hyperlink.RequestNavigate
    // - SkiaSharp PaintSurface
}
```

**XAML** - kompletní binding, žádné Click handlery:
```xml
<Button Command="{Binding DoSomethingCommand}"/>
<Button Content="OK"     IsDefault="True" Command="{Binding OkCommand}"/>
<Button Content="Storno" IsCancel="True"/>   <!-- IsCancel zavře bez kódu -->
```

**Otevření dialogu** vždy takto - VM se vytvoří vně, owner se nastaví:
```csharp
var vm = new MůjDialogViewModel(závislosti);
var dlg = new MůjDialog(vm) { Owner = this };
if (dlg.ShowDialog() == true)
    // použij vm.ResultXxx
```

**Pravidla:**
- ViewModel **neví** o existenci View (nezná `Window`, `DataGrid`, ani žádnou WPF kontrolku)
- Pokud VM potřebuje spustit View operaci, použije `Action?` callback - View ho napojí v konstruktoru
- `Click="..."` handlery v XAML **nejsou** - vždy `Command="{Binding ...}"`
- `IsCancel="True"` na tlačítku Storno zavírá dialog automaticky (DialogResult=false) - nepotřebuje handler
- `ColumnVM`/`RowVM` a podobné pomocné ViewModely patří do souboru `*ViewModel.cs`, **ne** do code-behind

**Vědomé výjimky z pravidla „žádné `Click=`" (evidence, revize 2026-07-28):**
- **6 EDInputForm dialogů** (element edit) má OK přes `Click=` - potřebují nejdřív zavolat
  `GeneralForm.Save()` (commit polí formuláře) a teprve pak zavřít; `Command` binding by pořadí
  neuhlídal. Vědomé, nechává se.
- **`TableEditorDialog` má 5 tabů** (limit `ux-conventions §1` je 3). TableElement je **uspaný**
  element (paleta ho nenabízí) → řeší se až u jeho oživení (Fáze B), teď „neřešit".
- **~59 `Click=` v `MainWindow` menu/toolbaru** - migrovat na `Command` **postupně při dotyku**
  daného handleru, ne velký refaktor. Nová okna/dialogy standard dodržují.

---

### Callback pattern: ViewModel → View

ViewModel nemůže volat `skCanvas.InvalidateVisual()` přímo (nezná SKElement).
Místo toho vystavuje `Action?` properties, které code-behind v konstruktoru napojí:

```csharp
// MainWindow konstruktor:
_vm.RequestCanvasRefresh      = () => { skCanvas.InvalidateVisual(); InvalidatePreview(); };
_vm.RequestPreviewRefresh     = InvalidatePreview;
_vm.RequestTitleUpdate        = UpdateTitle;
_vm.RequestCanvasSizeUpdate   = () => { UpdateCanvasSize(); UpdateTitle(); };
_vm.RequestPropertiesPanelUpdate = UpdatePropertiesPanel;
_vm.RequestExportPdf          = filePath => _previewCanvas.RenderToPdf(filePath, ...);
```

`Action?` místo `event` - záměrně: jeden subscriber (code-behind), jednodušší wiring.

---

### Sdílený DesignerState

`DesignerState` je prostá POCO třída (bez INotifyPropertyChanged), sdílená mezi:
- `DesignerCanvas` - čte stav při každém `Render()`
- `PreviewCanvas` - čte šablonu a zoom
- `TemplateDesignerViewModel` - mění stav (výběr, zoom, undo/redo)
- `MainWindow.xaml.cs` - čte při mouse state machine

Záměrně bez INotifyPropertyChanged - změny se projeví při příštím `InvalidateVisual()`,
ne automaticky. Undo/Redo a všechny commands jdou přes `DesignerState.Execute(cmd)`.

---

### Tok dat: kliknutí → překreslení

```
MouseDown → HitTestElement(px) → State.SelectedElements.Add(el)
MouseMove → el.X = initX + deltaMm  ← přímá mutace modelu (live drag)
          → skCanvas.InvalidateVisual()
          → PaintSurface → _canvas.Render() → el.Render(RenderContext)
MouseUp   → new MoveElementCommand(el, initX, finalX)
          → _vm.Execute(cmd) → State.UndoStack.Push(cmd)
          → NotifyUndoRedo() → CanUndo changed → UndoCommand.CanExecute() → tlačítko IsEnabled
```

**Klíčový detail:** elementy se mutují přímo za dragu (okamžitá vizuální odezva),
`MoveElementCommand` vznikne až při `MouseUp` s `(originalPos → finalPos)`.

---

### Souřadnicový systém

| Vrstva | Jednotka | Konverze |
|---|---|---|
| Model (Template, Section, TemplateElement) | **mm** | - |
| DesignerCanvas / PreviewCanvas | **px** | `mm × MmToPx` kde `MmToPx = (96/25.4) × Zoom` |
| PDF export (PreviewCanvas.RenderToPdf) | **pt** | `mm × (72/25.4)` |

Souřadnice elementů jsou **relativní k sekci** (ne ke stránce ani ke canvasu).
`GetSectionOrigins(canvasWidth)` vrátí absolutní px origin každé sekce - sdíleno renderem i hit testingem.

---

### DesignerCanvas vs PreviewCanvas

| | DesignerCanvas | PreviewCanvas |
|---|---|---|
| Účel | Editace | Náhled výsledku |
| Záhlaví sekcí | ✅ modrý pruh s názvem | ❌ |
| Resize handles | ✅ | ❌ |
| Selection overlay | ✅ | ❌ |
| Mřížka / vodítka | ✅ | ❌ |
| VisibilityExpression | ❌ vždy zobrazí | ✅ filtruje |
| DataContext | ❌ žádná data | ✅ mock data z agendy |
| Zoom | fixní z `State.Zoom` | fit-to-width automaticky |
| PDF export | ❌ | ✅ `RenderToPdf()` (multi-page) |
| Layout engine | ❌ vlastní jednoprůchodový | ✅ `PageLayoutEngine` → `LayoutResult` |
| Multi-page | ❌ | ✅ stránky pod sebou s mezerami |
