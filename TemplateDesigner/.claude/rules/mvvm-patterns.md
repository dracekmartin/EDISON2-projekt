# Glob: dotnet/Windows/**

## PropertyDescriptor systém

Každý element implementuje `GetPropertyDescriptors()` vracející seznam deskriptorů pro Properties panel. Panel generuje editovatelné řádky dynamicky.

### Zúžená role descriptorů vůči EDInputFormu (evidence 2026-07-29)

Hlavní editační cestou je **Caché formulář** (`*Edit.cls`): u elementu se schématem ho
`RefreshInlineProperties` ukáže v panelu i v dialogu. Descriptory tím **nepřestaly platit** - jsou
živé ve třech konkrétních rolích a proto je nelze odstranit:

1. **Formátovací lišta** (`FormatToolbar`) je potřebuje **vždy**, i když se zobrazuje Caché formulář.
2. **Úprava více prvků naráz** jde jen přes ně (Caché formulář edituje jeden cíl) - okno společných
   vlastností nabízí jen pevnou sadu (rámeček, vrstva, uzamčení), ne všechny deskriptory prvků.
3. **Fallback** pro element bez `*Edit.cls` schématu (dnes pevná tabulka).

**Nejsou tedy druhý zdroj pravdy pro schéma** - u base polí je autoritou sdílený
[`BaseElementFields.cls`](../../CACHE/ALVA/TemplateDesigner/EDInputForm/BaseElementFields.cls) a C#
`GetPositionDescriptors()` s ním musí souhlasit v popiscích i skupinách (konstanty `PositionGroup`/
`BorderGroup`/`ConditionsGroup`); hlídá to `EDInputFormSchemaDriftTests`. Nové base pole se přidává
**na obou stranách v jednom commitu**, jinak test spadne. Popisek každého deskriptoru prvku musí
existovat i ve formuláři prvku (protokol importu z PDF a asistent mluví popisky deskriptorů) -
hlídá to `DescriptorLabelsMatchFormTests`, vědomé výjimky jsou v něm s důvodem.

**Dostupné typy deskriptorů:**
- `StringPropertyDescriptor` - textové pole (s volitelným `MultiLine = true`)
- `FloatPropertyDescriptor` - numerický input s `Min`, `Max`, `Format`, `Unit`
- `BoolPropertyDescriptor` - checkbox
- `ColorPropertyDescriptor` - color picker (WinForms ColorDialog)
- `EnumPropertyDescriptor` - dropdown s definovanými možnostmi `Options = [(label, value), ...]`
- `SuggestStringPropertyDescriptor` - filtrovatelný editovatelný ComboBox; `GetSuggestions()` vrací `(Value, Label)` páry

Deskriptor smí nést i **vlastní vratný příkaz** (`MakeCommand`) - pro změnu, která sahá na víc cílů
najednou (formátování skupiny buněk pevné tabulky). Bez toho by zpětný krok vrátil všem jednu
společnou hodnotu z lišty místo té jejich; vzor viz `Models/TableCellFormatting.cs`.

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

## Styling patterny (kdy použít který)

Aplikace má **dva čisté patterny** pro vystavení stylu textu uživateli, podle počtu „zón" elementu.

### Pattern 1 - Single-zone text element

**Pro:** elementy s jednou textovou zónou (`TextElement`).

**Charakteristika:**
- Element má **jednu** `Style: TextStyle` property
- Cascade: `Style → ResolvedTextStyle.Default`
- Vše se edituje v properties panelu jako flat properties (FontFamily, FontSize, Bold, …)
- **Žádný dialog** - uživatel vidí všechno v panelu

**Šablona pro nový jednozónový element:**
```csharp
public class MyElement : TemplateElement
{
    public TextStyle Style { get; set; } = new();

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Font", Group = "Font", Get = () => Style.FontFamily ?? "", Set = v => Style.FontFamily = v },
        new FloatPropertyDescriptor  { Label = "Velikost", Group = "Font", Get = () => Style.FontSize ?? 9f, Set = v => Style.FontSize = v },
        // ... ostatní flat properties
        new ColorPropertyDescriptor  { Label = "Barva textu", Group = "Vzhled", Get = () => Style.TextColor ?? "#FF000000", Set = v => Style.TextColor = v },
        .. GetPositionDescriptors()
    ];

    public override void Render(RenderContext ctx)
    {
        var rs = Style.Resolve();   // ResolvedTextStyle.Default jako fallback parent
        // ... použij rs.FontFamily / rs.FontSize / atd.
    }
}
```

### Pattern 2 - Dual-zone tabulkový element

**Pro:** elementy s `Header` a `Data` zónou (`StopListElement`, `DepartureGridElement`,
`NotesLegendElement`, `LineTableElement`, `TableElement`, `DynamicTableElement`).

**Charakteristika:**
- Element má **`HeaderStyle: TextStyle` + `DataStyle: TextStyle`** properties (kompozice)
- Cascade: `column.Style → element.HeaderStyle/DataStyle → element-specific defaults → CellDefaults`
- **Panel:** 4 base barvy přes helper `GetCellZoneBaseColorDescriptors()` (rychlý přístup)
- **Dialog sloupců:** kompletní cascade přes sdílený `CellStyleEditor` (font, padding, align, decorations) + per-column overridy
- Render přes `Rendering/CellRenderer.DrawCell` - jediné místo pozicování textu

**Princip „rychlý přístup × cascade":**
- Panel má jen nejčastěji upravované properties (bg + text color pro Header a Data)
  a **velikost písma obou zón** (`GetCellZoneFontSizeDescriptors`) - ta není kosmetika, nese
  výšku řádku i šířku sloupce, takže bez ní se rozměr tabulky nedal změnit jinak než po
  sloupcích v dialogu (nález Jan 7. 9.). ⚠️ Zóny mají **různé** zapečené výchozí velikosti
  (záhlaví 9 pt, buňky 8 pt) - jedno číslo pro obě by ve formuláři ukazovalo jinou hodnotu,
  než se vykreslí
- Vše ostatní v dialogu kde má smysl per-column granularita
- **Promoted shortcut** (výjimka): nejčastější toggle v elementu může jít do panelu ze záměrných UX důvodů (např. `Tučné záhlaví` u TableElement). Použít zřídka, dokumentovat proč

**Šablona pro nový dual-zone element (schedule typu - dědí ze ScheduleElementBase):**
```csharp
public class MyScheduleElement : ScheduleElementBase
{
    // HeaderStyle, DataStyle, Grid (GridStyle), DataRowHeight zděděné z base

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        .. GetGridDescriptors(),                 // mřížka (které čáry, barva, tloušťka)
        .. GetCellZoneBaseColorDescriptors(),    // 4× Header/Data bg/text color
        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "...", ButtonLabel = "Upravit sloupce...", ActionId = ... },
        GetDataRowHeightDescriptor(),            // 0=auto, mm
        // ... element-specific properties
        .. GetPositionDescriptors()
    ];
}
```

**Nový dual-zone element v jiné hierarchii** (jako TableElement): paralelní implementace 4 ColorPropertyDescriptorů přímo v `GetPropertyDescriptors()`. **Nesdílet** přes interface - různé baked defaults a nullability HeaderStyle, sdílení by stálo víc kódu než duplikace.

### Pravidla pro inheritance vs composition

- **Inheritance:** „is-a" vztah (StopList JE schedule element). Maximálně 3 úrovně (`TemplateElement → ScheduleElementBase → StopScheduleElementBase → konkrétní`; mezivrstva sdílí poznámky/atributy zastávkových elementů). Žádné hlubší hierarchie.
- **Composition:** „has-a" vztah (StopList MÁ HeaderStyle). Styling, render util, dialogy - vše přes composition. TextStyle žije nezávisle na hierarchii elementu, sdíleno mezi `TextElement` a všemi tabulkovými elementy.
- **Pro nové komplexní structures preferovat composition** - sdílené services, helper třídy, struct s explicit Resolve(), ne deep inheritance.

### Známé záměrné výjimky (NE bug)

| Element | Co | Proč ne v cascade |
|---|---|---|
| NotesLegend | `NoteFontSize` (scalar) místo `DataStyle.FontSize` | Element nemá dialog → font size MUSÍ být v panelu. Migrace by stála víc než hodnota. |
| DepartureGrid | `MinuteStyle`, `AnotaceSymbolStyle`, `AnotaceTextStyle` flat v panelu | Element má 5 zón, ne 2. Pattern 2 pokrývá Header/Data; ostatní zóny jsou v panelu jako flat - kompletní dialog s 5 taby by byl horší UX. |
| TableElement | `Tučné záhlaví` v panelu i dialogu | „Promoted shortcut" - bold pro header je nejčastější quick toggle u tabulek. Záměrná výjimka, ne plošný pattern. |

---

## TableElement - architektura

### Pevná × dynamická tabulka jsou dva PRVKY, ne dva režimy

`TableElement` = pevná tabulka: uživatel vyplní sloupce i řádky v `TableEditorDialog`, sázecí
engine v buňkách dosadí `{PLACEHOLDER}` ze skalárů DataContextu. Řádky z kolekce dat kreslí
samostatný `DynamicTableElement` (`DataSource` = klíč do `DataContext._rows`, odpovídá
`DataFieldCollection.CollectionKey` ve schématu).

⚠️ `TableElement.Mode` s hodnotou `TableMode.Dynamic` **zůstal jen kvůli starým šablonám**
a z UI se nenastavuje (`TableElement.cs:134`). Je i výchozí hodnotou property kvůli deserializaci,
takže se ta hodnota v kódu potká - není to nabídka pro uživatele. Nová práce patří na
`DynamicTableElement`.

```
DataContext._values["CISLO_LINKY"] = "42"           → buňky pevné tabulky s "{CISLO_LINKY}"
DataContext._rows["ZASTAVKY"] = [DataContext, ...]   → DynamicTableElement (DataSource="ZASTAVKY")
```

---

## Jednotná kaskáda stylů buněk (tabulkové elementy)

> **Přehled celé architektury tabulkových elementů** (co je sdílené jádro `Rendering/TableLayout`
> + `CellRenderer`/`ColumnRenderer`, co drží každý element sám a proč, co vědomě NEsjednocovat,
> triggery pro budoucí extrakci): **`.claude/docs/elementy-architektura.md`** - číst před přidáním
> nebo úpravou tabulkového elementu. Kaskáda níže + výpočet šířek jsou sdílené v `TableLayout`
> (`ResolveCascade` / `ResolveWidths`).

Všechny tabulkové elementy (StopList, DepartureGrid, TableElement) sdílejí jednu 4-vrstvou kaskádu:

```
column.Style (per-column override)
  → element.HeaderStyle / DataStyle (element-wide default)
    → element-specific defaults (TableHeaderParent, ScheduleElementBase.HeaderStyle...)
      → CellDefaults.Default (autoritativní konec kaskády)
```

**Klíčové třídy v `Rendering/`:**
- `CellDefaults.Default` - jediný autoritativní zdroj výchozích hodnot buňky (Arial 9pt, VAlign=Middle, padding 0.5mm, ...)
- `CellRenderer.DrawCell(ctx, rectMm, text, style, fontScale)` - **jediné místo v aplikaci**, kde se pozicuje a vykresluje text buňky. Bg → padding shrink → HAlign+VAlign → Underline/Strikethrough
- `ColumnRenderer.DrawRow(ctx, xStartMm, yMm, heightMm, cells, gridPaint)` - orchestruje řádek sloupců přes `CellRenderer` + volitelné svislé čáry
- `CellStyleEditOptions` - record s `Allow<X>` flagy pro per-kontextové skrývání UI kontrolek editace stylu

**Element-side pattern** - každý tabulkový element má dvě vrstvy API:

1. **`Resolve<Zone>Parent(): ResolvedTextStyle`** (public) - vyřešený rodičovský styl pro dialog. Dialogy ho používají jako inherited vrstvu pro `CellStyleVM.Inherited`.
2. **`Resolve<Zone>Style(col): ResolvedTextStyle`** (private, per element) - kompletní kaskáda včetně per-column overridů. Vrací hotový styl pro `CellRenderer.DrawCell`.

```csharp
// Public - pro dialog kaskády
public ResolvedTextStyle ResolveColumnHeaderParent() =>
    (HeaderStyle ?? new TextStyle()).Resolve(TableHeaderParent);

// Private - pro render (per-column override)
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

### ⚠️ Anti-pattern: scalar property override v `Resolve*Parent()`

**Častý bug** (historicky 2× opraven v DepartureGrid): element má scalar property (`MinuteFontSize`, `SkupinaDnuHeaderLineHeight`, …) jako legacy auto-derived fallback, a v `Resolve*Parent()` se **unconditionally** aplikuje přes `with { ... }` po Resolve. Tím přebíjí user-set hodnotu z kaskády.

**❌ Špatně** - scalar override po Resolve:
```csharp
var p = HeaderStyle.Resolve(ScheduleHeaderDefaults) with { FontSize = legacyDefaultPt };
// → HeaderStyle.FontSize je ignorováno, i když user ho nastavil v tabu Obecné!
```

```csharp
var p = MinuteStyle.Resolve(dataResolved);
if (MinuteStyle.FontSize is null)
    p = p with { FontSize = MinuteFontSize };
// → DataStyle.FontSize je ignorováno, pokud MinuteStyle.FontSize je null
//   ale DataStyle.FontSize set (MinuteFontSize přebije DataStyle.FontSize).
```

**✅ Správně** - scalar je **poslední** fallback, aplikuje se jen když **žádná** vrstva kaskády property nenastavila:
```csharp
var p = HeaderStyle.Resolve(ScheduleHeaderDefaults);
if (HeaderStyle.FontSize is null)
    p = p with { FontSize = legacyDefaultPt };
```

```csharp
var p = MinuteStyle.Resolve(dataResolved);
if (MinuteStyle.FontSize is null && DataStyle.FontSize is null)
    p = p with { FontSize = MinuteFontSize };
if (MinuteStyle.Bold is null && DataStyle.Bold is null)
    p = p with { Bold = MinuteBold };
```

**Pravidlo:** priorita kaskády je `explicit-nejspecifičtější-vrstva > ... > element-level > element-scalar-fallback > baked-defaults > CellDefaults`. Scalar fallback **NIKDY** nepřebíjí explicitní hodnotu z kaskády, jen doplňuje když je všude null.

**Jak poznat takové místo:** hledej v `Resolve*()` metodách `with { SomeProp = ... }` bez předchozího `if (someProp is null)` checku pro **všechny relevantní vrstvy kaskády** (column + element + sub-style).

**Výjimka - feature overrides (ne cascade fallbacky):** `ResolveRowStyle` u StopListElement zvýrazňuje aktuální zastávku přes unconditional overrides (`Bold = CurrentStopBold || resolved.Bold`). To je záměrné highlighting, ne fallback - user chce aby tato vlastnost přebila. Rozpoznávací znak: feature override **je aktivován runtime stavem** (isCurrent, isPassed), ne statickou konfigurací.

### ⚠️ Anti-pattern: výška řádku z jiného fontu než glyf (asymetrie výška↔text)

**Symptom:** uživatel mění velikost písma (záhlaví/buňky), ale **mění se jen výška řádku, ne text** (nebo naopak). Typicky když element má per-column font override (i zapečený ze starší verze).

**Příčina:** layout **výšku** řádku počítá z **element-level** fontu (`ComputeHeaderH(HeaderStyle.FontSize)`), ale **glyf** se vykresluje z **resolved per-column** fontu (`ResolveHeaderStyle(col)` → `col.Style.Header.FontSize`). Když se ty dva zdroje liší (per-column override), výška a text se rozejdou.

**✅ Správně:** výška řádku se počítá ze **stejného resolved fontu jako glyf** - typicky max přes viditelné sloupce:
```csharp
float MaxResolvedHeaderFontPt() {
    var cols = GetVisibleColumns();
    return cols.Count > 0 ? cols.Max(c => ResolveHeaderStyle(c).FontSize) : (HeaderStyle.FontSize ?? 9f);
}
// V Render() i CalculateHeightFromData() (musí být shodné, jinak nesedí scale/overflow):
float headerH = ComputeHeaderH(MaxResolvedHeaderFontPt());
```
**Pravidlo:** kde se počítá výška z fontu, ber **resolved** font (per-column-aware), ne raw element property. A `Render()` + `CalculateHeightFromData()` musí použít **identický** výpočet.

**Aplikováno:** StopListElement i DepartureGridElement (výška z max resolved přes viditelné sloupce - sloupec může mít legitimně jiný font). U odjezdů to platí **od 16. 9.**; do té doby si element typografii záhlaví vynucoval (`WithParentHeaderTypography`), protože import z PDF zapékal `Bold = true` do každé skupiny dnů a přebíjel tím ovladač prvku. Import to už nedělá a migrace 1.13.0 zapečené hodnoty smazala - **nezavádět zpátky**, správné místo pro takovou opravu je zdroj dat, ne render. TableElement asymetrii nemá (výška = pevná `HeaderHeight` property).

## Sdílený `CellStyleEditor` (dialogy sloupců)

`Controls/CellStyleEditor.xaml` - UserControl konzumovaný všemi třemi dialogy (`StopColumnsEditorDialog`, `DepartureGroupEditorDialog`, `TableEditorDialog`). DataContext = `CellStyleVM`. Converters v `CellStyleEditor.Converters.cs`.

**Dependency properties:**
- `EditOptions` (`CellStyleEditOptions`) - řídí viditelnost řádků přes `BooleanToVisibilityConverter`
- 4× `ICommand` - `Pick/ClearTextColor`, `Pick/ClearBgColor` - dialog passuje své VM commands
- `HAlignOptions`/`VAlignOptions`/`FontSizeOptions` - defaults s rozumnými labely, lze override per dialog

**`CellStyleVM` - tri-state kaskáda (nullable = dědit):**

Všechny editovatelné vlastnosti jsou nullable (`string?`, `float?`, `bool?`, `TextHorizontalAlignment?`…). Null = dědit od rodiče.

- `Inherited: ResolvedTextStyle` (read-only) - vyřešený parent styl, XAML ho zobrazuje jako šedivý italic placeholder v prázdných polích
- `EffectiveBold`/`EffectiveItalic`/`EffectiveUnderline`/`EffectiveStrikethrough` derived gettery (`_bold ?? Inherited.Bold`) - binding pro 2-stavové checkboxy. Setter zapisuje explicit override
- `HasExplicit<X>` derived - viditelnost „×" reset tlačítek (jen když user explicitně přepsal)
- `ResetFieldCommand(paramName: string?)` - jedno command, CommandParameter = název property („Bold", „HAlign", …)
- `FromModel(TextStyle? explicit, ResolvedTextStyle inherited)` - povinně bere inherited z elementu
- `ToModel()` emituje nullable TextStyle - prázdná pole zůstanou null (idempotentní: open+OK beze změn nezmění JSON)

**Host dialog (VM) musí poskytnout:**
- `HeaderEditOptions` + `DataEditOptions` (dialogy sloupců zatím všude `AllEnabled`). Formulář prvku
  rozlišuje sady stylu přes `StyleFields.Add(…, bunka)`: Buňka (15 polí) pro záhlaví a buňky, Text
  (8 polí) pro text uvnitř buňky (minuta, značky, 2. řádek záhlaví). Nenabízet pole, které zóna nekreslí.
  Styl se ve formuláři nikdy neschovává za tlačítko - je rozepsaný (rozhodnutí Jan 28. 10.)
- `ResetStyleCommand` (volitelný, pro tlačítko „Resetovat styl")
- V konstruktoru: `CellStyleVM.FromModel(columnTextStyle, element.Resolve<Zone>Parent())` - nikdy nevytváří VM bez inherited!

⚠️ **Barvy hostitel nedrátuje.** Výběr a smazání barvy textu i pozadí drží `CellStyleVM` (jsou to
jeho hodnoty) a vyvolání výběru mu dosadí sám `CellStyleEditor`, protože jediný ví, v jakém okně
sedí. Do 21. 9. si každý hostitel psal osm příkazů a vlastní `RequestColorPicker` - pět kopií téhož.
Vlastní picker si drží jen dialog, který barvu edituje **mimo** tenhle editor (zebra u skupin dnů,
pravidla zvýraznění).

**Host dialog (XAML):**
```xml
<controls:CellStyleEditor
    DataContext="{Binding SelectedColumnVM.HeaderStyle}"
    EditOptions="{Binding DataContext.HeaderEditOptions, RelativeSource={RelativeSource AncestorType=Window}}"/>
```

---

## Jak přidat nový typ elementu

Kompletní checklist včetně dotčených souborů, factory switches, dynamické výšky a strom šablony → viz **`skill `novy-element``**.

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

// 4. Použít v Render() - žádná změna v panelu ani dialogu není potřeba
```

---

## Jak přidat nový typ sekce

Sekce je nyní univerzální třída - nové "typy" sekcí jsou jen presety properties:

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
