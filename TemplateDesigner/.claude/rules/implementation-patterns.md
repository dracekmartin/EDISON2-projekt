# Glob: dotnet/**/*.cs

## Klíčové implementační vzory

### Polymorfní JSON serializace
`Section` a `TemplateElement` jsou abstraktní — serializace používá `JsonPolymorphic` + `JsonDerivedType`:
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
[JsonIgnore] public SKColor SkTextColor => SKColor.Parse(TextColor);
```
Průhledná = `"#00FFFFFF"`. SkiaSharp `SKColor.Parse()` tuto syntaxi zvládá přímo.

### RenderContext
Elementy dostávají `RenderContext` místo holého `SKCanvas`. Kontext obsahuje:
- `Canvas` — SkiaSharp canvas
- `MmToPx` — koeficient pro převod mm → pixely
- `Data` — DataContext (null v designeru, mock v preview, reálná data v print enginu)
- `SectionOriginX/Y` — absolutní pozice sekce na canvasu v px

Elementy volají `ctx.ToCanvasRect(X, Y, Width, height)` pro přepočet souřadnic.

### WPF editable ComboBox — event ordering gotcha

**Problém:** Při použití `IsEditable=true` ComboBox s live filtrováním (`TextChanged` → `Items.Clear()` + rebuild) WPF spouští eventy **synchronně** v tomto pořadí při výběru položky:
1. `SelectionChanged` — WPF nastaví `SelectedItem`
2. `combo.Text = item.Content` — WPF nastaví text na display string (e.g. "SPOJE  —  Spoje")
3. `TextChanged` — náš handler spustí `Refilter()` → `Items.Clear()` → **`SelectedItem` se stane null**
4. `DropDownClosed` — `SelectedItem` je null → handler nic nedělá → text zůstane jako display string

**Fix:** Používáme `PreviewKeyDown`/`PreviewKeyUp` k detekci uživatelského psaní. `TextChanged` zavolá `Refilter()` pouze pokud `userIsTyping == true`. WPF interní změna textu při výběru nemá asociovaný klávesový event → Refilter se přeskočí → `SelectedItem` zůstane živý → `DropDownClosed` může přečíst `item.Tag` (skutečnou hodnotu, ne display string).

```csharp
bool userIsTyping = false;
combo.PreviewKeyDown += (_, _) => userIsTyping = true;
combo.PreviewKeyUp   += (_, _) => userIsTyping = false;

combo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) =>
{
    if (_suppressChanges || !userIsTyping) return;  // přeskočit WPF interní změny
    Refilter(combo.Text);
    if (combo.Items.Count > 0) combo.IsDropDownOpen = true;
}));

combo.DropDownClosed += (_, _) =>
{
    if (combo.SelectedItem is not ComboBoxItem item) return;  // SelectedItem je zachován!
    var val = (string)item.Tag!;   // skutečná hodnota (např. "SPOJE")
    _suppressChanges = true;
    combo.SelectedItem = null;
    combo.Text = val;              // přepíše display string "SPOJE  —  Spoje"
    _suppressChanges = false;
};
```

**Kde je použito:** `PropertiesPanel.xaml.cs` `AddSuggestRow()`.

### DataContext — resolvování placeholderů
```csharp
ctx.Data?.ResolvePlaceholders("Řidič: {JMENO}") ?? "Řidič: {JMENO}"
```

### Copy/paste — Clone()
Každý element implementuje `Clone()` vracející hlubokou kopii s novým `Guid.NewGuid()` Id.

### Extensibility bag
`TemplateElement` a `Section` mají `Dictionary<string, string>? CustomProperties` —
pro budoucí vlastnosti bez změny modelu a bez nutnosti verzovat JSON schéma.

---

## Důležité poznámky k SkiaSharp

- `SkiaSharp.Views.WPF` nefunguje správně s net9.0 přes XAML — `PaintSurface` event se **nesmí** registrovat v XAML, pouze v code-behind:
  ```csharp
  skCanvas.PaintSurface += OnPaintSurface;
  ```
- Parametr eventu se přijímá jako `object` a přetypovává přes `dynamic` kvůli omezení kompilátoru
- NU1701 warningy jsou očekávané a neškodné — balíčky fungují správně za běhu
- `using SkiaSharp;` **není** v implicitních using WPF projektu — přidej explicitně do každého souboru, kde se používají SK typy (`SKCanvas`, `SKPaint`, `SKColor`, `SKFont`…)
- `using System.IO;` taktéž není implicitní — přidej do souborů pracujících se soubory
- `SKFilterQuality` v SkiaSharp 3.x **neexistuje** — byl odebrán; pro kvalitu renderování nastav `SKSamplingOptions` nebo nech výchozí hodnotu

---

## Debugging: proč se user-set styl nepropíše do renderu?

Historicky nejčastější kořen (2× opraveno v DepartureGrid):

**Symptom:** user nastaví vlastnost (FontSize, Bold, …) ve stylovém dialogu, ale render ji ignoruje / přebíjí starou hodnotou.

**Checklist — kde hledat:**
1. **`ToModel()` → cascade zápis.** Otevři dialog, změň hodnotu, OK. Potvrď že nová hodnota dorazí do `element.HeaderStyle` / `col.Style` (breakpoint v `OnOk` write-back).
2. **Resolve*Parent() / Resolve*Style().** Hledej `with { SomeProp = scalarValue }` bez `if (someProp is null)` checku → to je anti-pattern (přebíjí cascade). Viz `mvvm-patterns.md §Anti-pattern: scalar property override`.
3. **Legacy scalar fallback musí respektovat VŠECHNY vrstvy kaskády.** Pokud element má `MinuteStyle` + `DataStyle` + `Element-scalar MinuteFontSize`, fallback na scalar smí padnout jen když **oba** `MinuteStyle.FontSize` i `DataStyle.FontSize` jsou null.
4. **Render používá resolved cascade, ne raw style properties.** Pokud kód volá např. `HeaderStyle.FontSize ?? 9f` přímo (místo `ResolveXxx().FontSize`), obchází kaskádu. Grep po `Style\.[A-Z]\w*\s*\?\?` — podezřelé.

**Trigger:** tato třída bugů se objevuje při přidávání nové vrstvy do kaskády (např. když `ElementHeaderStyle` začalo být editovatelné v tabu Obecné, starý scalar override který dřív „jen doplňoval" začal přebíjet novou vrstvu).
