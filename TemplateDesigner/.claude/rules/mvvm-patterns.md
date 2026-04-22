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

**Element-side pattern** — každý tabulkový element implementuje `Resolve*Style` helpery:
```csharp
private ResolvedTextStyle ResolveColumnHeaderStyle(TableColumn col) {
    var elementResolved = ResolveTableHeaderStyle();              // element → defaults
    var resolved = col.HeaderStyle is { } h
        ? h.Resolve(elementResolved) : elementResolved;           // column override
    // Legacy fallback: col.Alignment (jen pokud cascade nenastavila HAlign)
    if (col.HeaderStyle?.HAlign is null && HeaderStyle?.HAlign is null)
        resolved = resolved with { HAlign = col.Alignment };
    return resolved;
}
```

Pro větší počet buněk na frame (100+, např. DepartureGrid minuty) **pre-create fonty/painty** per-group/per-column jednou před render loopem (6 dictionary: font/textPaint/bgPaint × header/data), dispose v `finally`.

## Sdílený `CellStyleEditor` (dialogy sloupců)

`Controls/CellStyleEditor.xaml` — UserControl konzumovaný všemi třemi dialogy (`StopColumnsEditorDialog`, `DepartureGroupEditorDialog`, `TableEditorDialog`). DataContext = `CellStyleVM`.

**Dependency properties:**
- `EditOptions` (`CellStyleEditOptions`) — řídí viditelnost řádků přes `BooleanToVisibilityConverter`
- 4× `ICommand` — `Pick/ClearTextColor`, `Pick/ClearBgColor` — dialog passuje své VM commands
- `HAlignmentOptions`/`VAlignmentOptions`/`FontSizeOptions` — s rozumnými defaults, lze override per dialog

**Host dialog (VM) musí poskytnout:**
- `HeaderEditOptions` + `DataEditOptions` (zatím všude `AllEnabled`)
- Pick/Clear commands per zóna × barva (Text/Bg) = 8 commands
- `RequestColorPicker: Func<string, string?>` callback (dialog wire `ColorPickerHelper.PickColor`)
- `ResetStyleCommand` (volitelný, pro tlačítko „Resetovat styl")

**Host dialog (XAML):**
```xml
<controls:CellStyleEditor
    DataContext="{Binding SelectedColumnVM.HeaderStyle}"
    EditOptions="{Binding DataContext.HeaderEditOptions, RelativeSource={RelativeSource AncestorType=Window}}"
    PickTextColorCommand="{Binding DataContext.PickHeaderTextColorCommand, RelativeSource={RelativeSource AncestorType=Window}}"
    ... /> <!-- 3 zbývající color commands -->
```

**Známé omezení** (pro budoucí fix):
- `CellStyleVM.Bold/Italic/Underline/Strikethrough` jsou `bool` (ne `bool?`) — `ToModel()` vždy emituje explicit hodnotu, takže open+OK beze změn uloží non-null TextStyle. Pokud defaultní VM.Bold neodpovídá element-level default Bold, kaskáda se rozbije. Volající musí passovat `defaultBold: true` v `CellStyleVM.FromModel(...)` pro záhlaví.
- UI neukazuje placeholder hodnoty pro dědené vlastnosti — user vidí prázdný FontFamily i když default "Arial" existuje. Tri-state checkbox + placeholder converter = budoucí iterace.

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
