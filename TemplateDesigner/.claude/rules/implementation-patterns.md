# Glob: dotnet/**/*.cs

## Klíčové implementační vzory

### Polymorfní JSON serializace
`Section` a `TemplateElement` jsou abstraktní - serializace používá `JsonPolymorphic` + `JsonDerivedType`:
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
[JsonIgnore] public SKColor SkTextColor => ColorHex.ParseOrBlack(TextColor);
```
Průhledná = `"#00FFFFFF"`.

⚠️ **Nikdy `SKColor.Parse` nad uživatelskou hodnotou** - na neplatném vstupu **vyhodí výjimku**,
a barvy jdou z formuláře jako volný text. Padalo to až při kreslení, a protože se nesmysl uložil
do šablony, i při každém dalším otevření (šablona vypadala rozbitá). Vždy přes `Models/ColorHex.cs`:
`ParseOrBlack` (text/čáry/rámečky), `ParseOrTransparent` (pozadí), `ParseOrNull` (nepovinná barva,
kde „nenastaveno" znamená „featura se nepoužije" - střídání řádků, zvýraznění), `Parse(hex, fallback)`
s vlastní zálohou, `TryParse`/`IsValid` pro kontrolu vstupu. `SKColor.Parse` je OK jen nad literálem
v kódu. Vstupní kontrola je druhá vrstva: barevné pole formuláře hlásí neplatný tvar samo
(`Field.GetTypeError` v EDControls) a `ElementFormBinder` neplatnou barvu do modelu nezapíše.

### RenderContext
Elementy dostávají `RenderContext` místo holého `SKCanvas`. Kontext obsahuje:
- `Canvas` - SkiaSharp canvas
- `MmToPx` - koeficient pro převod mm → pixely
- `Data` - DataContext (null v designeru, mock v preview, reálná data v print enginu)
- `SectionOriginX/Y` - absolutní pozice sekce na canvasu v px

Elementy volají `ctx.ToCanvasRect(X, Y, Width, height)` pro přepočet souřadnic.

### Filtrovaný editovatelný ComboBox - vždy sdílený `EDFilterComboBox`

⚠️ **Nepiš vlastní.** Filtrovaný editovatelný ComboBox je v EDControls jako `EDFilterComboBox`
(volný režim = smí i text mimo nabídku, `StrictSelection` = jen platná položka). TD ho používá
v panelu vlastností (`AddSuggestRow`) i ve výběru instancí; **dvě vlastní implementace vedle něj
stály jeden reálný bug** (2026-08-04, `B46`: do skupiny linek nešlo napsat druhý znak).

Proč to nikdy nedělat ručně - dvě pasti, které komponenta drží za tebe:

**Past 1 - filtrování `ICollectionView` nastaveného jako `ItemsSource`.** ComboBox si s ním
synchronizuje výběr, takže vyfiltrování právě vybrané položky vynutí změnu výběru → přepíše text
v editovatelném poli a spustí `SelectionChanged`. Komponenta proto plní `Items` **přímo** a hodnotu
nese v `Tag`.

**Past 2 - pořadí eventů.** Při použití `IsEditable=true` ComboBox s live filtrováním (`TextChanged` → `Items.Clear()` + rebuild) WPF spouští eventy **synchronně** v tomto pořadí při výběru položky:
1. `SelectionChanged` - WPF nastaví `SelectedItem`
2. `combo.Text = item.Content` - WPF nastaví text na display string (e.g. "SPOJE  -  Spoje")
3. `TextChanged` - náš handler spustí `Refilter()` → `Items.Clear()` → **`SelectedItem` se stane null**
4. `DropDownClosed` - `SelectedItem` je null → handler nic nedělá → text zůstane jako display string

Rozlišit uživatelovo psaní od interní změny textu jde jen klávesovým eventem - a **musí to být
`PreviewKeyDown`**, protože `PreviewKeyUp` přichází **až po** `TextChanged`, takže filtr jede o znak
pozadu (druhá polovina bugu `B46`: první napsaný znak neodfiltroval nic). Výběr se navíc nesmí
commitovat z `SelectedItem` (Refilter ho může mezitím zahodit), ale z Key zachyceného už
v `SelectionChanged`.

Referenční řešení všeho výše = `EDControls/dotnet/EDFilterComboBox/EDFilterComboBox.cs` (remarks
třídy + testy v `EDControls.Tests`). Konzumenti v TD: `PropertiesPanel.AddSuggestRow()`,
`InstancePickerDialog`.

### DataContext - resolvování placeholderů
```csharp
ctx.Data?.ResolvePlaceholders("Řidič: {JMENO}") ?? "Řidič: {JMENO}"
```

### Copy/paste - Clone()
Každý element implementuje `Clone()` vracející hlubokou kopii s novým `Guid.NewGuid()` Id.

### Extensibility bag
`TemplateElement` a `Section` mají `Dictionary<string, string>? CustomProperties` -
pro budoucí vlastnosti bez změny modelu a bez nutnosti verzovat JSON schéma.

---

## Důležité poznámky k SkiaSharp

- `SkiaSharp.Views.WPF` nefunguje správně s net9.0 přes XAML - `PaintSurface` event se **nesmí** registrovat v XAML, pouze v code-behind:
  ```csharp
  skCanvas.PaintSurface += OnPaintSurface;
  ```
- Parametr eventu se přijímá jako `object` a přetypovává přes `dynamic` kvůli omezení kompilátoru
- NU1701 warningy jsou očekávané a neškodné - balíčky fungují správně za běhu
- `using SkiaSharp;` **není** v implicitních using WPF projektu - přidej explicitně do každého souboru, kde se používají SK typy (`SKCanvas`, `SKPaint`, `SKColor`, `SKFont`…)
- `using System.IO;` taktéž není implicitní - přidej do souborů pracujících se soubory
- ⚠️ **Písmo z `GetTypeface` / `FontResolver.Resolve` je sdílené z mezipaměti - nikdy `using` ani `Dispose`**
  (uvolňuje se jen `SKFont`). Písmo ze systému SkiaSharp uvolnit nenechá, ale přibalené (`Fonts/*.ttf`) se uvolní
  doopravdy a další měření i kreslení téhož písma tiše vezme výchozí písmo (nález 28. 10. u „&lt;1" v Seznamu zastávek)
- `SKFilterQuality` v SkiaSharp 3.x **neexistuje** - byl odebrán; pro kvalitu renderování nastav `SKSamplingOptions` nebo nech výchozí hodnotu

---

## Debugging: proč se user-set styl nepropíše do renderu?

Historicky nejčastější kořen (2× opraveno v DepartureGrid):

**Symptom:** user nastaví vlastnost (FontSize, Bold, …) ve stylovém dialogu, ale render ji ignoruje / přebíjí starou hodnotou.

**Checklist - kde hledat:**
1. **`ToModel()` → cascade zápis.** Otevři dialog, změň hodnotu, OK. Potvrď že nová hodnota dorazí do `element.HeaderStyle` / `col.Style` (breakpoint v `OnOk` write-back).
2. **Resolve*Parent() / Resolve*Style().** Hledej `with { SomeProp = scalarValue }` bez `if (someProp is null)` checku → to je anti-pattern (přebíjí cascade). Viz `mvvm-patterns.md §Anti-pattern: scalar property override`.
3. **Legacy scalar fallback musí respektovat VŠECHNY vrstvy kaskády.** Pokud element má `MinuteStyle` + `DataStyle` + `Element-scalar MinuteFontSize`, fallback na scalar smí padnout jen když **oba** `MinuteStyle.FontSize` i `DataStyle.FontSize` jsou null.
4. **Ruční kreslicí / měřicí cesta staví typeface z kaskády, ne natvrdo.** V JŘ elementech volej
   `GetTypeface(ResolvedTextStyle)` (rodina + řez přes `ToSKFontStyle`); `GetTypeface(family, SKFontStyle.Bold/Normal)`
   zahodí uživatelovu váhu i kurzívu. Platí i pro **měření** (wrap, min. šířka) - musí trefit stejný font
   jako render, jinak se počet řádků a výška boxu rozejdou.
5. **Render používá resolved cascade, ne raw style properties.** Pokud kód volá např. `HeaderStyle.FontSize ?? 9f` přímo (místo `ResolveXxx().FontSize`), obchází kaskádu. Grep po `Style\.[A-Z]\w*\s*\?\?` - podezřelé.

**Trigger:** tato třída bugů se objevuje při přidávání nové vrstvy do kaskády (např. když `ElementHeaderStyle` začalo být editovatelné v tabu Obecné, starý scalar override který dřív „jen doplňoval" začal přebíjet novou vrstvu).
