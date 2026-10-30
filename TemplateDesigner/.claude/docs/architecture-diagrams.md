# Architektonické diagramy - TemplateDesigner

> **Konceptuální diagramy** (vrstvy, datový tok, hierarchie). Cenné pro onboarding a školení.
> Autoritativní zdroj struktury kódu je vždy `.claude/docs/architecture.md` + samotný kód - diagramy
> mohou mírně zaostávat za detaily. Při rozporu vyhrává kód/rules.

## 1. Vrstvy systému

```mermaid
graph TB
    subgraph "Vrstva 5 - Aplikace"
        MW["MainWindow<br/><small>WPF hlavní okno</small>"]
        Dialogs["Dialogy<br/><small>10 oken/dialogů</small>"]
        VM["ViewModely<br/><small>MainWindowViewModel + 5 dalších</small>"]
    end

    subgraph "Vrstva 4 - Renderer"
        DC["DesignerCanvas<br/><small>editační canvas</small>"]
        PC["PreviewCanvas<br/><small>live preview + PDF export</small>"]
        SE["SnapEngine<br/><small>smart snap</small>"]
    end

    subgraph "Vrstva 3 - Layout Engine"
        PLE["PageLayoutEngine<br/><small>Template + Data → List&lt;Page&gt;</small>"]
        SHC["SkiaHeightCalculator<br/><small>měření výšek elementů</small>"]
        LR["LayoutResult<br/><small>Page → PlacedSection</small>"]
    end

    subgraph "Vrstva 2 - Výrazy"
        EE["ExpressionEvaluator<br/><small>podmínky: &&, ||, !</small>"]
        PF["PlaceholderFormatter<br/><small>{POLE:FORMAT}</small>"]
        AR["AggregationResolver<br/><small>SUM, COUNT, AVG</small>"]
    end

    subgraph "Vrstva 1 - Modely"
        T["Template"] --> S["Section"]
        S --> TE["TemplateElement"]
        DCx["DataContext"]
        DS["DataSchema"]
    end

    MW --> VM
    VM --> DC
    VM --> PC
    DC --> SE
    PC --> PLE
    PLE --> SHC
    PLE --> LR
    TE --> EE
    TE --> PF
    PLE --> T
    PLE --> DCx

    style MW fill:#4a9eff,color:#fff
    style VM fill:#4a9eff,color:#fff
    style DC fill:#7c4dff,color:#fff
    style PC fill:#7c4dff,color:#fff
    style PLE fill:#00bfa5,color:#fff
    style T fill:#ff9800,color:#fff
    style S fill:#ff9800,color:#fff
    style TE fill:#ff9800,color:#fff
```

---

## 2. Hierarchie elementů

```mermaid
classDiagram
    class TemplateElement {
        <<abstract>>
        +Guid Id
        +float X, Y, Width
        +float? Height
        +int ZIndex
        +bool IsLocked
        +string? VisibilityExpression
        +string? BorderColor
        +float BorderWidth
        +Guid? GroupId
        +Render(RenderContext)
        +Clone() TemplateElement
        +GetPropertyDescriptors()
    }

    class TextElement {
        +string Text
        +float FontSize
        +string FontFamily
        +string TextColor
        +bool WordWrap
        +float LineSpacing
    }

    class ImageElement {
        +string? ImageBase64
        +string? DataField
        +ImageScaleMode ScaleMode
    }

    class RectangleElement {
        +string FillColor
        +float CornerRadius
    }

    class LineElement {
        +LineOrientation Orientation
        +LineStyle Style
        +string Color
        +float Thickness
    }

    class BarcodeElement {
        +BarcodeElementType BarcodeType
        +string DataField
    }

    class TableElement {
        +TableMode Mode
        +string? DataSource
        +List~TableColumn~ Columns
        +List~TableRow~ Rows
    }

    class ScheduleElementBase {
        <<abstract>>
        +string FontFamily
        +float FontSize
        +ScheduleDisplayMode DisplayMode
        #CreateCellFont()
        #CreateCellTextPaint()
        #CreateCellBgPaint()
        #RenderCellBackground()
        #MakeFontStyle()
        #ComputeFontScale()
    }

    class StopListElement {
        +List~StopColumn~ Columns
        +bool ShowDirectionArrow
        +float HeaderHeight
    }

    class DepartureGridElement {
        +float HourColumnWidth
        +float MinuteColumnWidth
        +TimeAxisMode TimeAxis
    }

    class NotesLegendElement {
        +float SymbolColumnWidth
        +float TextFontSize
    }

    TemplateElement <|-- TextElement
    TemplateElement <|-- ImageElement
    TemplateElement <|-- RectangleElement
    TemplateElement <|-- LineElement
    TemplateElement <|-- BarcodeElement
    TemplateElement <|-- TableElement
    TemplateElement <|-- ScheduleElementBase
    ScheduleElementBase <|-- StopListElement
    ScheduleElementBase <|-- DepartureGridElement
    ScheduleElementBase <|-- NotesLegendElement
```

---

## 3. Tiskový pipeline (data flow)

```mermaid
flowchart LR
    subgraph "Design Time"
        Designer["Designer<br/>šablona.json"]
        Mock["GetMockData()<br/>vzorová data"]
        Preview["PreviewCanvas<br/>live preview"]
    end

    subgraph "Print Time"
        Params["PrintDialog<br/>parametry tisku"]
        GetData["GetData()<br/>reálná data z Caché"]
    end

    subgraph "Engine"
        PLE["PageLayoutEngine"]
        SHC["SkiaHeightCalculator"]
        EE["ExpressionEvaluator"]
        PF["PlaceholderFormatter"]
    end

    subgraph "Output"
        Screen["WPF Canvas<br/>SKElement"]
        PDF["PDF soubor<br/>SkiaSharp"]
    end

    Designer --> |Template| PLE
    Mock --> |DataContext| Preview
    Preview --> |Template + Mock| PLE
    Params --> GetData
    GetData --> |DataContext[]| PLE
    PLE --> |měření| SHC
    PLE --> |LayoutResult| Screen
    PLE --> |LayoutResult| PDF
    EE -.-> |visibility filter| PLE
    PF -.-> |{POLE} → hodnota| PLE

    style Designer fill:#ff9800,color:#fff
    style PLE fill:#00bfa5,color:#fff
    style PDF fill:#e91e63,color:#fff
    style Screen fill:#4a9eff,color:#fff
```

---

## 4. Okna a dialogy

```mermaid
flowchart TB
    MW["MainWindow<br/><small>MainWindowViewModel</small>"]

    MW --> |"dvojklik / Enter"| EPW["ElementPropertiesWindow<br/><small>univerzální dialog vlastností</small>"]
    MW --> |"tisk Ctrl+P"| PD["PrintDialog<br/><small>parametry + export</small>"]
    MW --> |"import PDF"| PID["PdfImportDialog<br/><small>PdfPig extrakce</small>"]
    MW --> |"AI asistent"| AI["AiTemplateAssistantDialog"]
    MW --> |"F1"| Help["HelpDialog"]
    MW --> |"strom šablony"| Tree["TemplateTreeDialog"]

    EPW --> |"TableElement"| TE["TableEditorDialog<br/><small>sloupce + řádky</small>"]
    EPW --> |"DepartureGrid"| DGE["DepartureGroupEditorDialog<br/><small>per-column styling</small>"]
    EPW --> |"StopList"| SCE["StopColumnsEditorDialog<br/><small>sloupce + styling</small>"]

    subgraph "Sdílené komponenty"
        PP["PropertiesPanel<br/><small>PropertyDescriptor → UI</small>"]
        CSV["CellStyleVM<br/><small>header/data styl</small>"]
        CPH["ColorPickerHelper"]
    end

    EPW --> PP
    DGE --> PP
    SCE --> PP
    DGE --> CSV
    SCE --> CSV
    DGE --> CPH
    SCE --> CPH

    style MW fill:#4a9eff,color:#fff
    style EPW fill:#7c4dff,color:#fff
    style PP fill:#00bfa5,color:#fff
    style CSV fill:#00bfa5,color:#fff
```

---

## 5. Undo/Redo command systém

```mermaid
classDiagram
    class IDesignerCommand {
        <<interface>>
        +string Description
        +Execute()
        +Undo()
    }

    class DesignerState {
        +Stack~IDesignerCommand~ UndoStack
        +Stack~IDesignerCommand~ RedoStack
        +Execute(IDesignerCommand)
    }

    class CompositeCommand {
        +List~IDesignerCommand~ Commands
        +Execute() all
        +Undo() reverse order
    }

    IDesignerCommand <|.. AddElementCommand
    IDesignerCommand <|.. DeleteElementCommand
    IDesignerCommand <|.. MoveElementCommand
    IDesignerCommand <|.. ResizeElementCommand
    IDesignerCommand <|.. SetPropertyCommand
    IDesignerCommand <|.. DeleteSectionCommand
    IDesignerCommand <|.. MoveSectionCommand
    IDesignerCommand <|.. ReorderSectionCommand
    IDesignerCommand <|.. GroupCommand
    IDesignerCommand <|.. UngroupCommand
    IDesignerCommand <|.. CompositeCommand

    DesignerState --> IDesignerCommand : executes
    CompositeCommand --> IDesignerCommand : contains
```

---

## 6. Caché API a datové zdroje

```mermaid
flowchart TB
    subgraph "C# (.NET)"
        CSW["CacheSchemaWiring"]
        CAP["CacheAgendaProvider<br/><small>IDataSchemaProvider +<br/>ITableTemplateProvider</small>"]
        COMP["CompositeDataSchemaProvider<br/><small>Caché primary + JSON fallback</small>"]
        JSON["JsonFileDataSchemaProvider<br/><small>schemas/*.schema.json</small>"]
        MLR["MLRest / IMLRest<br/><small>HTTP klient → Caché</small>"]
    end

    subgraph "Caché (ObjectScript)"
        AA["AgendaAPI<br/><small>abstraktní základ</small>"]
        MHD["ZastavkoveJrMhdAPI<br/><small>6-fázový MHD engine</small>"]
    end

    subgraph "Caché DB (globály)"
        LSO["^ALVA.MHD.LSO<br/><small>linky, spoje, trasy</small>"]
        ZST["^ALVA.MHD.ZST<br/><small>zastávky</small>"]
        CHR["^ALVA.MHD.CHR<br/><small>chronometráže</small>"]
        ENUMS["^ALVA.MHD.ENUMS<br/><small>číselníky</small>"]
    end

    CSW --> CAP
    CAP --> MLR
    COMP --> CAP
    COMP --> JSON
    MLR --> |"HTTP CVar"| AA
    AA <|-- MHD
    MHD --> LSO
    MHD --> ZST
    MHD --> CHR
    MHD --> ENUMS

    style MLR fill:#e91e63,color:#fff
    style AA fill:#ff9800,color:#fff
    style MHD fill:#ff9800,color:#fff
    style LSO fill:#795548,color:#fff
    style ZST fill:#795548,color:#fff
    style CHR fill:#795548,color:#fff
    style ENUMS fill:#795548,color:#fff
```

---

## 7. Datový model šablony

```mermaid
erDiagram
    Template ||--o{ Section : "sections[]"
    Template ||--|| PageSize : "pageSize"
    Template ||--o{ TemplateParameter : "parameters[]"
    Section ||--o{ TemplateElement : "elements[]"

    Template {
        Guid Id
        string Name
        string AgendaCode
        string CultureCode
    }

    PageSize {
        float Width_mm
        float Height_mm
        float MarginTop
        float MarginBottom
        float MarginLeft
        float MarginRight
    }

    Section {
        Guid Id
        string Name
        SectionRole Role
        SectionPrintBehavior PrintBehavior
        SectionOverflowBehavior OverflowBehavior
        float DesignerHeight
        string VisibilityExpression
    }

    TemplateElement {
        Guid Id
        string type_discriminator
        float X_mm
        float Y_mm
        float Width_mm
        float Height_mm
        int ZIndex
        bool IsLocked
        Guid GroupId
    }

    TemplateParameter {
        string Key
        string Label
        string Type
        string DefaultValue
    }
```

---

## 8. PropertyDescriptor systém

```mermaid
classDiagram
    class PropertyDescriptor {
        <<abstract>>
        +string Label
        +string? Group
    }

    class StringPropertyDescriptor {
        +Func~string~ Get
        +Action~string~ Set
        +bool MultiLine
    }

    class FloatPropertyDescriptor {
        +Func~float~ Get
        +Action~float~ Set
        +float Min, Max
        +string Unit
    }

    class BoolPropertyDescriptor {
        +Func~bool~ Get
        +Action~bool~ Set
    }

    class ColorPropertyDescriptor {
        +Func~string~ Get
        +Action~string~ Set
    }

    class EnumPropertyDescriptor {
        +Func~object~ Get
        +Action~object~ Set
        +List Options
    }

    class SuggestStringPropertyDescriptor {
        +Func GetSuggestions
    }

    class ButtonPropertyDescriptor {
        +string ButtonLabel
        +string ActionId
        +Action OnClick
    }

    PropertyDescriptor <|-- StringPropertyDescriptor
    PropertyDescriptor <|-- FloatPropertyDescriptor
    PropertyDescriptor <|-- BoolPropertyDescriptor
    PropertyDescriptor <|-- ColorPropertyDescriptor
    PropertyDescriptor <|-- EnumPropertyDescriptor
    StringPropertyDescriptor <|-- SuggestStringPropertyDescriptor
    StringPropertyDescriptor <|-- TextWithPickerPropertyDescriptor
    PropertyDescriptor <|-- ButtonPropertyDescriptor

    PropertiesPanel --> PropertyDescriptor : renders dynamically

    note for PropertiesPanel "UserControl v Controls/\nGeneruje UI řádky\nz deskriptorů elementu"
```

---

## 9. Celkový přehled souborové struktury

```mermaid
mindmap
  root((TemplateDesigner))
    dotnet/
      Models/
        Template, Section, PageSize
        DataContext, RenderContext
        PropertyDescriptor (8 typů)
        Elements/
          TextElement
          ImageElement
          RectangleElement
          LineElement
          BarcodeElement
          TableElement
          ScheduleElementBase
            StopListElement
            DepartureGridElement
            NotesLegendElement
          ScheduleModels
        Layout/
          PageLayoutEngine
          SkiaHeightCalculator
          LayoutResult
        Schema/
          DataSchema
          IDataSchemaProvider
        Expressions/
          ExpressionEvaluator
          PlaceholderFormatter
          AggregationResolver
      Windows/ (10 oken)
        MainWindow
        ElementPropertiesWindow
        TableEditorDialog
        StopColumnsEditorDialog
        DepartureGroupEditorDialog
        PrintDialog
        PdfImportDialog
        AiTemplateAssistantDialog
        TemplateTreeDialog
        HelpDialog
      Designer/
        DesignerState
        DesignerCanvas
        PreviewCanvas
        SnapEngine
      Controls/
        PropertiesPanel
        CellStyleVM
        ColorPickerHelper
      Commands/ (11 typů)
        Add/Delete Element
        Move/Resize Element
        SetProperty
        Section commands
        Group/Ungroup
        CompositeCommand
      Serialization/
        TemplateSerializer
        CacheAgendaProvider
        CompositeProvider
        SectionTemplateManager
      Config/
        AppSettings
        CacheSchemaWiring
    CACHE/
      AgendaAPI (abstract)
      ZastavkoveJrMhdAPI
```
