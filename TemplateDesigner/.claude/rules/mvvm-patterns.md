# Glob: dotnet/Windows/**

## PropertyDescriptor systém

Každý element implementuje `GetPropertyDescriptors()` vracející seznam deskriptorů pro Properties panel. Panel generuje editovatelné řádky dynamicky.

**Dostupné typy deskriptorů:**
- `StringPropertyDescriptor` — textové pole (s volitelným `MultiLine = true`)
- `FloatPropertyDescriptor` — numerický input s `Min`, `Max`, `Format`, `Unit`
- `BoolPropertyDescriptor` — checkbox
- `ColorPropertyDescriptor` — color picker (WinForms ColorDialog)
- `EnumPropertyDescriptor` — dropdown s definovanými možnostmi `Options = [(label, value), ...]`
- `SuggestStringPropertyDescriptor` — filtrovatelný editovatelný ComboBox; `GetSuggestions()` vrací `(Value, Label)` páry

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

## TableElement — architektura

### Dva módy

| Mód | Kdo definuje strukturu | Kdo dodá řádky při tisku |
|---|---|---|
| **Fixed** | Uživatel v `TableEditorDialog` (sloupce + řádky) | Print engine resolvuje `{PLACEHOLDER}` ze skalárů DataContext |
| **Dynamic** | Uživatel definuje sloupce; řádky z dat | Print engine iteruje `DataContext._rows[dataSource]` |

V JSON šabloně Fixed ukládá `columns[]` + `rows[]`, Dynamic ukládá jen `columns[]`.

### DataContext — jak data tečou

```
DataContext._values["CISLO_LINKY"] = "42"           → Fixed buňky s "{CISLO_LINKY}"
DataContext._rows["ZASTAVKY"] = [DataContext, ...]   → Dynamic TableElement (DataSource="ZASTAVKY")
```

`TableElement.DataSource` = klíč do `_rows`, odpovídá `DataFieldGroup.CollectionKey` ve schématu.

### Budoucí rozšíření nad TableElement

- `VisibilityExpression` na `TableRow` — podmíněné řádky (vyhodnocuje print engine)
- Grouping — Group Header/Footer řádky mezi dynamickými daty
- Filtrování v Dynamic tabulce (předání filtru do DataContext)

---

## Jednotná kaskáda stylů buněk (tabulkové elementy)

Všechny tabulkové elementy (StopList, DepartureGrid, TableElement) sdílejí jednu 4-vrstvou kaskádu:

```
column.Style (per-column override)
  → element.HeaderStyle / DataStyle (element-wide default)
    → element-specific defaults (TableHeaderParent, ScheduleElementBase.HeaderStyle...)
      → CellDefaults.Default (autoritativní konec kaskády)
```

**Klíčové třídy v `Rendering/`:**
- `CellDefaults.Default` — jediný autoritativní zdroj výchozích hodnot buňky (Arial 9pt, VAlign=Middle, padding 0.5mm, ...)
- `CellRenderer.DrawCell(ctx, rectMm, text, style, fontScale)` — **jediné místo v aplikaci**, kde se pozicuje a vykresluje text buňky. Bg → padding shrink → HAlign+VAlign → Underline/Strikethrough
- `ColumnRenderer.DrawRow(ctx, xStartMm, yMm, heightMm, cells, gridPaint)` — orchestruje řádek sloupců přes `CellRenderer` + volitelné svislé čáry
- `CellStyleEditOptions` — record s `Allow<X>` flagy pro per-kontextové skrývání UI kontrolek editace stylu

**Element-side pattern** — každý tabulkový element má dvě vrstvy API:

1. **`Resolve<Zone>Parent(): ResolvedTextStyle`** (public) — vyřešený rodičovský styl pro dialog. Dialogy ho používají jako inherited vrstvu pro `CellStyleVM.Inherited`.
2. **`Resolve<Zone>Style(col): ResolvedTextStyle`** (private, per element) — kompletní kaskáda včetně per-column overridů. Vrací hotový styl pro `CellRenderer.DrawCell`.

```csharp
// Public — pro dialog kaskády
public ResolvedTextStyle ResolveColumnHeaderParent() =>
    (HeaderStyle ?? new TextStyle()).Resolve(TableHeaderParent);

// Private — pro render (per-column override)
private ResolvedTextStyle ResolveColumnHeaderStyle(TableColumn col) {
    var elementResolved = ResolveColumnHeaderParent();
    var resolved = col.HeaderStyle is { } h ? h.Resolve(elementResolved) : elementResolved;
    // Legacy fallback: col.Alignment (jen pokud cascade nenastavila HAlign)
    if (col.HeaderStyle?.HAlign is null && HeaderStyle?.HAlign is null)
        resolved = resolved with { HAlign = col.Alignment };
    return resolved;
}
```

Pro větší počet buněk na frame (100+, např. DepartureGrid minuty) **pre-create fonty/painty** per-group/per-column jednou před render loopem (6 dictionary: font/textPaint/bgPaint × header/data), dispose v `finally`.

## Sdílený `CellStyleEditor` (dialogy sloupců)

`Controls/CellStyleEditor.xaml` — UserControl konzumovaný všemi třemi dialogy (`StopColumnsEditorDialog`, `DepartureGroupEditorDialog`, `TableEditorDialog`). DataContext = `CellStyleVM`. Converters v `CellStyleEditor.Converters.cs`.

**Dependency properties:**
- `EditOptions` (`CellStyleEditOptions`) — řídí viditelnost řádků přes `BooleanToVisibilityConverter`
- 4× `ICommand` — `Pick/ClearTextColor`, `Pick/ClearBgColor` — dialog passuje své VM commands
- `HAlignOptions`/`VAlignOptions`/`FontSizeOptions` — defaults s rozumnými labely, lze override per dialog

**`CellStyleVM` — tri-state kaskáda (nullable = dědit):**

Všechny editovatelné vlastnosti jsou nullable (`string?`, `float?`, `bool?`, `TextHorizontalAlignment?`…). Null = dědit od rodiče.

- `Inherited: ResolvedTextStyle` (read-only) — vyřešený parent styl, XAML ho zobrazuje jako šedivý italic placeholder v prázdných polích
- `EffectiveBold`/`EffectiveItalic`/`EffectiveUnderline`/`EffectiveStrikethrough` derived gettery (`_bold ?? Inherited.Bold`) — binding pro 2-stavové checkboxy. Setter zapisuje explicit override
- `HasExplicit<X>` derived — viditelnost „×" reset tlačítek (jen když user explicitně přepsal)
- `ResetFieldCommand(paramName: string?)` — jedno command, CommandParameter = název property („Bold", „HAlign", …)
- `FromModel(TextStyle? explicit, ResolvedTextStyle inherited)` — povinně bere inherited z elementu
- `ToModel()` emituje nullable TextStyle — prázdná pole zůstanou null (idempotentní: open+OK beze změn nezmění JSON)

**Host dialog (VM) musí poskytnout:**
- `HeaderEditOptions` + `DataEditOptions` (zatím všude `AllEnabled`; do budoucna per-kontextové skrytí irelevantních voleb)
- Pick/Clear commands per zóna × barva (Text/Bg) = 8 commands
- `RequestColorPicker: Func<string, string?>` callback (dialog wire `ColorPickerHelper.PickColor`)
- `ResetStyleCommand` (volitelný, pro tlačítko „Resetovat styl")
- V konstruktoru: `CellStyleVM.FromModel(columnTextStyle, element.Resolve<Zone>Parent())` — nikdy nevytváří VM bez inherited!

**Host dialog (XAML):**
```xml
<controls:CellStyleEditor
    DataContext="{Binding SelectedColumnVM.HeaderStyle}"
    EditOptions="{Binding DataContext.HeaderEditOptions, RelativeSource={RelativeSource AncestorType=Window}}"
    PickTextColorCommand="{Binding DataContext.PickHeaderTextColorCommand, RelativeSource={RelativeSource AncestorType=Window}}"
    ... /> <!-- 3 zbývající color commands -->
```

---

## Jak přidat nový typ elementu

Kompletní checklist včetně dotčených souborů, factory switches, dynamické výšky a strom šablony → viz **`.claude/rules/new-element-checklist.md`**.

Reference implementace: `BarcodeElement` (minimální), `TextElement` (TextStyle + dynamická výška), `ScheduleElementBase`-derived (komplexní s cache).

---

## Jak přidat vlastnost k existujícímu elementu

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

---

## Jak přidat nový typ sekce

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
