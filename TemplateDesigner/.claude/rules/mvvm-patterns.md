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
