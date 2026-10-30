---
name: novy-element
description: Přidá nový typ elementu do TemplateDesigneru - třída, registrace v ElementTypes a JsonDerivedType, factory switche, toolbar, volitelně paleta a dynamická výška, strom šablony, docs. Použij při zavádění nového TYPU elementu; na novou vlastnost existujícího elementu je to zbytečné (viz sekce na konci).
argument-hint: <NázevElementu>
allowed-tools: Read, Edit, Write, Glob, Grep, Bash(dotnet build *), Bash(dotnet test *), Bash(grep *), Bash(ls *)
---

# Přidání nového typu elementu - kompletní checklist

Tento soubor je autoritativní reference. Kroky jsou uspořádány tak, že po každém jde zkompilovat (build failuje až u prvního skutečného use-site) - lze postupovat inkrementálně.

Reference implementace:
- **Jednoduchý element:** `BarcodeElement` (pevná výška, vlastní rendering, žádný TextStyle)
- **Textový element:** `TextElement` (TextStyle, dynamická výška přes WordWrap)
- **Tabulkový element:** `TableElement` / `StopListElement` / `DepartureGridElement` - používají **jednotnou kaskádu** `column.Style → element.HeaderStyle/DataStyle → defaults → CellDefaults`; pozicování textu přes `Rendering/CellRenderer.DrawCell`; viz `mvvm-patterns.md §Jednotná kaskáda stylů buněk`. Okno sloupců staví na sdílené záložce `Controls/ColumnsTab` (editor stylu `CellStyleEditor` je její součást) a formulář na kostře z `ux-conventions §4` - viz Dodatek
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

    // Volitelné - override pokud element škáluje vlastní mm/pt properties (font, padding, ...)
    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);     // X, Y, Width, Height, BorderWidth
        // + vlastní mm/pt properties: FontSize, Padding, ...
    }
}
```

Pravidla:
- `Clone()` vždy přes `CopyBaseTo(target)` - kopíruje Id (nové Guid), pozice, border, background, rotaci, GroupId, CustomProperties.
- `GetPropertyDescriptors()` vždy končí `.. GetPositionDescriptors()` (pozice, border, viditelnost, skupina).
- `ApplyScale(float)` override **pouze** pokud element má vlastní mm/pt properties mimo base (FontSize v pt, Padding v mm, …). Base třída škáluje X/Y/Width/Height/BorderWidth automaticky.

---

## 2. Rendering

`Render(RenderContext ctx)` je jediná povinná metoda. Pravidla:
- Pracuj v mm, převeď až na canvas: `var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);`
- Placeholdery ({POLE}) resolvuj přes `ctx.Data?.ResolvePlaceholders(...)` - v designeru je `ctx.Data` null.
- Pozadí: `SkBackgroundColor.Alpha > 0` check, pak fill.
- Rámeček **nekreslit** - druhý průchod volá `RenderBorderPass` automaticky nad base `TemplateElement.RenderBorder(...)`.
- Rotace: base `RenderBorderPass` rotaci ošetří. Pokud obsah potřebuje jinou rotaci (jiný pivot), použij `ctx.Canvas.Save()` + `RotateDegrees(Rotation, cx, cy)` + `Restore()`.
- V designeru (`ctx.Data is null`) omez velikost fontu pro čitelnost placeholderů - viz `TextElement.cs`: `float effectiveFontSize = ctx.Data is null ? Math.Min(rs.FontSize, 10f) : rs.FontSize;`.

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

## 5. MainWindowViewModel - výchozí rozměry a factory

Soubor: `dotnet/Windows/MainWindow/TemplateDesignerViewModel.Editing.cs` (ViewModel je od 30. 10. rozdělený
do částí `TemplateDesignerViewModel.*.cs`, tahle drží úpravy prvků)

Dva switch výrazy, oba přes `ElementTypes.NovyElement`:

```csharp
// GetDefaultElementSize
ElementTypes.NovyElement => (80f, 40f),

// CreateElementByType
ElementTypes.NovyElement => new NovyElement { X = x, Y = y, Width = w, Height = h },
```

Výchozí rámeček nastaví `CreateElementByType` automaticky na konci (BorderColor=black, BorderWidth=0.1mm).

---

## 6. Toolbar menu

Soubor: `dotnet/Windows/MainWindow/MainWindow.Menus.cs`, metoda `BtnAddElement_Click`:

```csharp
Add("Nový element", () => TemplateDesignerViewModel.CreateElementByType(ElementTypes.NovyElement, 5, 5));
```

---

## 7. Paleta (volitelné, jen pro často používané elementy)

Soubor: `dotnet/Windows/MainWindow/MainWindow.xaml` (sekce `PaletteButton`):

```xml
<Button Style="{StaticResource PaletteButton}" ToolTip="Nový element - táhni na canvas" Tag="NovyElement"
        PreviewMouseLeftButtonDown="PaletteButton_PreviewMouseLeftButtonDown">
    <Image Source="{StaticResource EDIcons.NovyElement}" Width="16" Height="16"/>
</Button>
```

`Tag="NovyElement"` zůstává jako literál (XAML nemá `x:Static` ergonomii pro 9 tlačítek; drift odchytne první switch při drop).

Ikona - `EDControls/dotnet/EDIcons/EDIcons.xaml`:
```xml
<BitmapImage x:Key="EDIcons.NovyElement" UriSource="pack://application:,,,/EDControls;component/EDIcons/png/noveElement.png"/>
```
PNG registruj v `EDControls.csproj` jako `<Resource Include="EDIcons\png\noveElement.png"/>`.

---

## 8. Dynamická výška (volitelné - pokud `Height` může být null)

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

Alternativně - pokud element má jen statickou dynamickou výšku (bez dat), stačí override `DesignerPlaceholderHeight` na base:

```csharp
protected override float DesignerPlaceholderHeight => 8f;
```

---

## 9. Název prvku pro uživatele - ElementNames

Soubor: `dotnet/Models/ElementNames.cs`, větev v `ForUser` i `ForType` - týž název jako v paletě
a v nabídce vkládání. Bere ho strom šablony, stavový řádek i hlášky; bez něj ukážou obecné „prvek".
Prvek doplň i do seznamu v `ElementNamesTests.KazdyTypPrvkuMaJmeno` - sám ho test nenajde.
Strom technické údaje prvku neukazuje (`B194` E1).

---

## 10. Dokumentace

- `.claude/docs/changelog.md` - přidat řádek do aktuálního datumu ("NovyElement - popis").
- Pokud jde o ScheduleElementBase-derived element, aktualizovat i `.claude/docs/elementy-architektura.md`.
- **`CLAUDE.md` nechat být.** Nový element tam nepatří - výčet featur se tam nevede. Sáhni tam
  jen tehdy, když element zavádí neodvoditelný invariant (persistovaný klíč, zpětná kompatibilita).

---

## Dodatek: tabulkový element s per-column stylingem

Pokud nový element má **sloupce se samostatným stylingem záhlaví/dat** (jako `TableElement`/`StopListElement`/`DepartureGridElement`), postupuj podle 5 doplňujících kroků (detaily v `.claude/rules/mvvm-patterns.md §Jednotná kaskáda stylů buněk`):

**1. Model - per-column `TextStyle?`:**
```csharp
public class NovyColumn {
    public TextStyle? HeaderStyle { get; set; }  // null = dědit z elementu
    public TextStyle? DataStyle   { get; set; }  // null = dědit z elementu
    // + existing: Header, Width, Visible, ...
}
```

**2. Element - public `Resolve<Zone>Parent()` API:**
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

    // Private - per-column override pro render
    private ResolvedTextStyle ResolveColumnHeaderStyle(NovyColumn col) =>
        col.HeaderStyle is { } h ? h.Resolve(ResolveColumnHeaderParent())
                                 : ResolveColumnHeaderParent();
}
```

**3. Render - přes `CellRenderer.DrawCell`:**
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

**4. Okno se sloupci - sdílená záložka `<ColumnsTab>`:**

Okno (triáda): `Windows/NovyColumnsEditorDialog/{Window,ViewModel,xaml}.cs`, záložky Obecné (EDInputForm) a Sloupce.
Záložku Sloupce **nekopíruj** - dodej jen:
- **řádek** `NovyColumnVM : Controls.ColumnRowVM` - převod sloupce z/do modelu (co okno nenabízí, vrať beze
  změny) a schopnosti (`CanHide`, `CanMove`, `CountsTowardWidth`, `WidthHint`, `DataZoneTitle`…).
  Styl: `CellStyleVM.FromModel(column.HeaderStyle, TableLayout.ResolveCascade(null, element.ResolveColumnHeaderParent(), col.Alignment, …))`
- **ViewModel okna**: `Columns = new ColumnsTabViewModel(rows) { ElementWidth = () => element.Width, CanFit = …, NewRow = … }`,
  v `OnOk` jen výsledky (prvek neměnit - provede je volající jako jeden vratný krok)
- **OK beze změny vrací původní sloupce** přes `ColumnsTabViewModel.ResultOrOriginal`. Okno při načtení
  doplňuje vypnuté sloupce a převádí hodnoty, takže porovnání se šablonou by OK bez dotyku zapsalo a přidalo
  krok do Ctrl+Z. Pojistka `OkBezeZmenyTests` - nový prvek do ní přidej.
- **Co okno nenabízí, nechá v šabloně na místě** přes `ColumnsTabViewModel.WithNotOffered` (sloupec, ke kterému
  agenda nemá data, skupina dnů jiné linky). Šablona slouží víc linkám i agendám; bez toho OK nastavení ztratí
  nebo přehází. Vzor obou: `StopColumnsEditorDialogViewModel`.
- **XAML**: `<controls:ColumnsTab DataContext="{Binding Columns}"/>`, pole navíc přes `ExtraColumnTemplate` / `ExtraDataTemplate`

**Reference implementace:** `LineTableColumnsEditorDialog` (pevný sloupec spojů, zvláštní zóna šipky),
`DynamicTableEditorDialog` (přidání/odebrání, pole navíc), `DepartureGroupEditorDialog` (skupiny dnů + sloupec hodin).

**NIKDY nekopíruj záložku Sloupce ani editor stylu buněk** - vše přes `<ColumnsTab>`, který `CellStyleEditor` obsahuje.

**5. Formulář `*ElementEdit.cls` (záložka Obecné i panel vlastností):**

Soubor `CACHE/ALVA/TemplateDesigner/EDInputForm/NovyElementEdit.cls`, napojení v `MainWindow.PanelCacheClass`
a v konstantě `GeneralSchemaClass` okna sloupců. Formulář dodrží kostru z `ux-conventions §4`:
- **Nahoře skupiny jen tohoto prvku**, pojmenované podle toho, čeho se na výtisku týkají; skupina „Obecné" se nepoužívá.
- **Pod nimi vždy stejný konec:** Záhlaví, Datové buňky, pak `BaseElementFields.AddBorder`, `AddGrid`, `AddPosition`,
  `AddConditions` (Rámeček prvku, Mřížka, Pozice a velikost, Podmínky).
- **Skupina části** (Záhlaví, Datové buňky): první pole část zapíná, pak pole pro celý řádek (výška, střídání),
  pod nimi sada stylu `StyleFields.Add(form, "HeaderStyle", …)` - sada Buňka (`bunka = 1`, 15 polí) pro záhlaví
  a buňky, sada Text (`bunka = 0`, 8 polí) pro text uvnitř buňky. Pole, které prvek nekreslí, formulář nenabízí.
- **Popisky** podle `ux-conventions §8`; deskriptory v C# mají tytéž popisky a skupiny (`DescriptorLabelsMatchFormTests`,
  `EDInputFormSchemaDriftTests`).
- **Nasazení** `.cls` jen editačním nástrojem a ověření voláním `Init` na serveru - `cache-api.md` §Nasazení `.cls`.

---

## Shrnutí - dotčené soubory

| # | Soubor | Povinné |
|---|---|---|
| 1 | `Models/Elements/NovyElement.cs` | ✅ |
| 3 | `Models/ElementTypes.cs` | ✅ |
| 4 | `Models/TemplateElement.cs` (JsonDerivedType) | ✅ |
| 5 | `Windows/MainWindow/TemplateDesignerViewModel.Editing.cs` (2 switches) | ✅ |
| 6 | `Windows/MainWindow/MainWindow.Menus.cs` (toolbar menu) | ✅ |
| 7 | `Windows/MainWindow/MainWindow.xaml` + `EDIcons.xaml` + PNG | volitelné |
| 8 | `Models/Layout/SkiaHeightCalculator.cs` | jen u dynamické výšky |
| 9 | `Models/ElementNames.cs` (ForUser, ForType) | ✅ |
| 10 | `changelog.md` (`CLAUDE.md` jen u neodvoditelného invariantu) | ✅ |
| D5 | `CACHE/.../EDInputForm/NovyElementEdit.cls` + `MainWindow.PanelCacheClass` | u tabulkového prvku |

---

## Brána - bez tohohle není hotovo

Pořadí záleží: drift test najde chybějící registraci, kterou build neodhalí (`ElementTypes`
konstanta bez `JsonDerivedType`).

1. `dotnet build EDISON2.sln` - musí projít čistě
2. `dotnet test TemplateDesigner.Tests --filter ElementRegistrationDrift` - musí projít
3. `dotnet test TemplateDesigner.Tests --filter CloneCompleteness` - hlídá, že `Clone()`
   nezapomněl novou property
4. U prvku s formulářem `dotnet test TemplateDesigner.Tests --filter EDInputFormSchemaDrift` a `--filter DescriptorLabelsMatchForm`,
   u okna sloupců `--filter OkBezeZmeny`
5. **Přečti výstup**, netvrď „mělo by to projít". Když test spadne, oprav a spusť znovu.

Vizuál (jak element vypadá na canvasu a v PDF) žádný test nevidí - to musí proklikat
uživatel. Do reportu napiš konkrétní cestu: co přidat, kam táhnout, co má být vidět.
