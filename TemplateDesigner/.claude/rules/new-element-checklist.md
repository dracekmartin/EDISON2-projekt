# Glob: dotnet/Models/Elements/**

# Přidání nového typu elementu — kompletní checklist

Tento soubor je autoritativní reference. Kroky jsou uspořádány tak, že po každém jde zkompilovat (build failuje až u prvního skutečného use-site) — lze postupovat inkrementálně.

Reference implementace:
- **Jednoduchý element:** `BarcodeElement` (pevná výška, vlastní rendering, žádný TextStyle)
- **Textový element:** `TextElement` (TextStyle, dynamická výška přes WordWrap)
- **Tabulkový element:** `TableElement` / `StopListElement` / `DepartureGridElement` — používají **jednotnou kaskádu** `column.Style → element.HeaderStyle/DataStyle → defaults → CellDefaults`; pozicování textu přes `Rendering/CellRenderer.DrawCell`; viz `mvvm-patterns.md §Jednotná kaskáda stylů buněk`. Pro sloupce použij sdílený `Controls/CellStyleEditor` UserControl v dialogu (nikdy neduplikuj inline XAML)
- **Komplexní element:** `NotesLegendElement` (dědí z `ScheduleElementBase`, per-column styling, data cache)

---

## 1. Třída elementu

Soubor: `dotnet/Models/Elements/NovyElement.cs`

```csharp
using SkiaSharp;
namespace TemplateDesigner.Models.Elements;

public class NovyElement : TemplateElement
{
    // Vlastní properties (data, styl)
    public string Content { get; set; } = "";

    public override void Render(RenderContext ctx) { /* ... */ }

    public override TemplateElement Clone()
    {
        var clone = new NovyElement { Content = Content /* ... */ };
        CopyBaseTo(clone);   // Id, X/Y/Width/Height, border, background, rotation, ...
        return clone;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        // Vlastní deskriptory
        new StringPropertyDescriptor { Label = "Obsah", Group = "Kód", Get = () => Content, Set = v => Content = v },
        // Povinně na konci:
        .. GetPositionDescriptors()
    ];

    // Volitelné — override pokud element škáluje vlastní mm/pt properties (font, padding, ...)
    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);     // X, Y, Width, Height, BorderWidth
        // + vlastní mm/pt properties: FontSize, Padding, ...
    }
}
```

Pravidla:
- `Clone()` vždy přes `CopyBaseTo(target)` — kopíruje Id (nové Guid), pozice, border, background, rotaci, GroupId, CustomProperties.
- `GetPropertyDescriptors()` vždy končí `.. GetPositionDescriptors()` (pozice, border, viditelnost, skupina).
- `ApplyScale(float)` override **pouze** pokud element má vlastní mm/pt properties mimo base (FontSize v pt, Padding v mm, …). Base třída škáluje X/Y/Width/Height/BorderWidth automaticky.

---

## 2. Rendering

`Render(RenderContext ctx)` je jediná povinná metoda. Pravidla:
- Pracuj v mm, převeď až na canvas: `var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);`
- Placeholdery ({POLE}) resolvuj přes `ctx.Data?.ResolvePlaceholders(...)` — v designeru je `ctx.Data` null.
- Pozadí: `SkBackgroundColor.Alpha > 0` check, pak fill.
- Rámeček **nekreslit** — druhý průchod volá `RenderBorderPass` automaticky nad base `TemplateElement.RenderBorder(...)`.
- Rotace: base `RenderBorderPass` rotaci ošetří. Pokud obsah potřebuje jinou rotaci (jiný pivot), použij `ctx.Canvas.Save()` + `RotateDegrees(Rotation, cx, cy)` + `Restore()`.
- V designeru (`ctx.Data is null`) omez velikost fontu pro čitelnost placeholderů — viz `TextElement.cs`: `float effectiveFontSize = ctx.Data is null ? Math.Min(rs.FontSize, 10f) : rs.FontSize;`.

---

## 3. Konstanta v ElementTypes.cs

Soubor: `dotnet/Models/ElementTypes.cs`

```csharp
public const string NovyElement = "NovyElement";
```

Jedna autoritativní definice string identifikátoru. **Nikdy neduplikovat jako literál** na jiných místech.

---

## 4. JsonDerivedType atribut

Soubor: `dotnet/Models/TemplateElement.cs`

```csharp
[JsonDerivedType(typeof(NovyElement), ElementTypes.NovyElement)]
```

Vždy přes konstantu, nikdy literál (C# povoluje `const` expressions v atributech).

---

## 5. MainWindowViewModel — výchozí rozměry a factory

Soubor: `dotnet/Windows/MainWindow/TemplateDesignerViewModel.cs`

Dva switch výrazy, oba přes `ElementTypes.NovyElement`:

```csharp
// GetDefaultElementSize (~řádek 844)
ElementTypes.NovyElement => (80f, 40f),

// CreateElementByType (~řádek 858)
ElementTypes.NovyElement => new NovyElement { X = x, Y = y, Width = w, Height = h },
```

Výchozí rámeček nastaví `CreateElementByType` automaticky na konci (BorderColor=black, BorderWidth=0.1mm).

---

## 6. Toolbar menu

Soubor: `dotnet/Windows/MainWindow/MainWindow.xaml.cs`, metoda `BtnAddElement_Click` (~řádek 999):

```csharp
Add("Nový element", () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.NovyElement, 5, 5));
```

---

## 7. Paleta (volitelné, jen pro často používané elementy)

Soubor: `dotnet/Windows/MainWindow/MainWindow.xaml` (sekce `PaletteButton`):

```xml
<Button Style="{StaticResource PaletteButton}" ToolTip="Nový element — táhni na canvas" Tag="NovyElement"
        PreviewMouseLeftButtonDown="PaletteButton_PreviewMouseLeftButtonDown">
    <Image Source="{StaticResource EDIcons.NovyElement}" Width="16" Height="16"/>
</Button>
```

`Tag="NovyElement"` zůstává jako literál (XAML nemá `x:Static` ergonomii pro 9 tlačítek; drift odchytne první switch při drop).

Ikona — `EDControls/dotnet/EDIcons/EDIcons.xaml`:
```xml
<BitmapImage x:Key="EDIcons.NovyElement" UriSource="pack://application:,,,/EDControls;component/EDIcons/png/noveElement.png"/>
```
PNG registruj v `EDControls.csproj` jako `<Resource Include="EDIcons\png\noveElement.png"/>`.

---

## 8. Dynamická výška (volitelné — pokud `Height` může být null)

Soubor: `dotnet/Models/Layout/SkiaHeightCalculator.cs`

```csharp
return element switch
{
    TextElement te => CalculateTextElementHeight(te, data),
    // ...
    NovyElement nv => nv.CalculateHeightFromData(data),   // přidat
    _ => element.EffectiveDesignerHeight
};
```

Na elementu implementovat `CalculateHeightFromData(DataContext? data)` vracející výšku v mm.

Alternativně — pokud element má jen statickou dynamickou výšku (bez dat), stačí override `DesignerPlaceholderHeight` na base:

```csharp
protected override float DesignerPlaceholderHeight => 8f;
```

---

## 9. Strom šablony — GetSummary

Soubor: `dotnet/Models/TemplateElement.cs`, metoda `GetSummary` (~řádek 236):

```csharp
NovyElement n => $"Obsah: {TruncateText(n.Content, 30)}",
```

Bez tohoto se v záložce „Strom" zobrazí generický fallback `(X, Y) WxH`.

---

## 10. Dokumentace

- `.claude/docs/changelog.md` — přidat řádek do aktuálního datumu ("NovyElement — popis").
- `CLAUDE.md` sekce `### ✅ Co máme` — doplnit element do výčtu.
- Pokud jde o ScheduleElementBase-derived element, aktualizovat i `.claude/docs/schedule-elements-architecture.md`.

---

## Dodatek: tabulkový element s per-column stylingem

Pokud nový element má **sloupce se samostatným stylingem záhlaví/dat** (jako `TableElement`/`StopListElement`/`DepartureGridElement`), postupuj podle 4 doplňujících kroků (detaily v `.claude/rules/mvvm-patterns.md §Jednotná kaskáda stylů buněk`):

**1. Model — per-column `TextStyle?`:**
```csharp
public class NovyColumn {
    public TextStyle? HeaderStyle { get; set; }  // null = dědit z elementu
    public TextStyle? DataStyle   { get; set; }  // null = dědit z elementu
    // + existing: Header, Width, Visible, ...
}
```

**2. Element — public `Resolve<Zone>Parent()` API:**
```csharp
public class NovyElement : TemplateElement {
    public TextStyle? HeaderStyle { get; set; }
    public TextStyle? DataStyle   { get; set; }

    private static readonly ResolvedTextStyle NovyHeaderParent =
        CellDefaults.Default with { Bold = true, /* per-element defaults */ };
    private static readonly ResolvedTextStyle NovyDataParent =
        CellDefaults.Default with { /* per-element defaults */ };

    public ResolvedTextStyle ResolveColumnHeaderParent() =>
        (HeaderStyle ?? new TextStyle()).Resolve(NovyHeaderParent);
    public ResolvedTextStyle ResolveColumnDataParent() =>
        (DataStyle ?? new TextStyle()).Resolve(NovyDataParent);

    // Private — per-column override pro render
    private ResolvedTextStyle ResolveColumnHeaderStyle(NovyColumn col) =>
        col.HeaderStyle is { } h ? h.Resolve(ResolveColumnHeaderParent())
                                 : ResolveColumnHeaderParent();
}
```

**3. Render — přes `CellRenderer.DrawCell`:**
```csharp
public override void Render(RenderContext ctx) {
    foreach (var col in Columns) {
        var style = ResolveColumnHeaderStyle(col);
        var rectMm = new SKRect(colXMm, yMm, colXMm + colWMm, yMm + headerHeightMm);
        CellRenderer.DrawCell(ctx, rectMm, col.Header, style);
        // + per-row iteration similarly for DataStyle
    }
}
```

**4. Dialog sloupců — sdílený `<CellStyleEditor>`:**

Dialog (okno triad): `Windows/NovyColumnsEditorDialog/{Window,ViewModel,xaml}.cs`. VM musí:
- V konstruktoru brát `NovyElement element` a volat `element.ResolveColumnHeaderParent()` / `ResolveColumnDataParent()`
- Předat inherited do `CellStyleVM.FromModel(column.HeaderStyle, headerInherited)`
- Exposovat 8 commands (`Pick/Clear<Header|Data><Text|Bg>ColorCommand`), `ResetStyleCommand`, `RequestColorPicker` callback
- Exposovat `HeaderEditOptions` + `DataEditOptions` (`CellStyleEditOptions.AllEnabled` obvykle)

XAML dialogu — 2× `<controls:CellStyleEditor>` (Header + Data zone):
```xml
<controls:CellStyleEditor
    DataContext="{Binding SelectedColumnVM.HeaderStyle}"
    EditOptions="{Binding DataContext.HeaderEditOptions, RelativeSource={RelativeSource AncestorType=Window}}"
    PickTextColorCommand="{Binding DataContext.PickHeaderTextColorCommand, RelativeSource={RelativeSource AncestorType=Window}}"
    ... /> <!-- 3 zbývající color commands -->
```

**Reference implementace:** `TableEditorDialog` (Fixed+Dynamic sloupce), `StopColumnsEditorDialog` (jednoduché sloupce), `DepartureGroupEditorDialog` (skupiny dnů + sloupec hodin).

**NIKDY neduplikuj inline XAML pro editaci stylu buněk** — vše přes `<CellStyleEditor>`.

---

## Shrnutí — dotčené soubory

| # | Soubor | Povinné |
|---|---|---|
| 1 | `Models/Elements/NovyElement.cs` | ✅ |
| 3 | `Models/ElementTypes.cs` | ✅ |
| 4 | `Models/TemplateElement.cs` (JsonDerivedType) | ✅ |
| 5 | `Windows/MainWindow/TemplateDesignerViewModel.cs` (2 switches) | ✅ |
| 6 | `Windows/MainWindow/MainWindow.xaml.cs` (toolbar menu) | ✅ |
| 7 | `Windows/MainWindow/MainWindow.xaml` + `EDIcons.xaml` + PNG | volitelné |
| 8 | `Models/Layout/SkiaHeightCalculator.cs` | jen u dynamické výšky |
| 9 | `Models/TemplateElement.cs` (GetSummary) | ✅ |
| 10 | `changelog.md`, `CLAUDE.md` | ✅ |
