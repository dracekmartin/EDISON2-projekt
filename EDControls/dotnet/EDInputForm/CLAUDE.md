# EDInputForm — poznámky pro vývojáře

## Účel

`EDInputForm` je samostatný WPF `UserControl`, který načte, vykreslí, validuje a uloží
dynamický formulář přes Caché (InterSystems) back-end. Hostitelské okno potřebuje pouze
nastavit `CacheClass` — `MLRest` se čte automaticky z `EDViewModel` DataContext rodičovského
okna. Veškerá Caché komunikace a serializace je řešena interně.

## Mapa souborů

| Soubor | Role |
| --- | --- |
| `EDInputForm.xaml / .xaml.cs` | UserControl. Vlastní všechna Caché volání, serializační helpery a Items DP. |
| `EDInputFormController.cs` | Testovatelná orchestrace (Init/Validate/Save) bez WPF závislosti. Přijímá `IMLRest` a `CacheClass` jako parametry konstruktoru. |
| `Item.cs` | Abstraktní báze pro všechny položky formuláře. Property `Number`, `Visibility` (`EItemVisibility`), `EffectiveVisibility` (virtuální), `HelpText`, `HelpTextVisibility`. Implementuje `INotifyPropertyChanged`. |
| `EHelpTextMode.cs` | Enum: `Tooltip`, `Column`. Pomocná třída `EHelpTextModeWire` — konverze `ToWire()` / `FromWire()`. |
| `Group.cs` | Hlavička sekce: `GroupName`, `IsExpanded` (computed z `Visibility`: Visible=expanded, Collapsed=collapsed). `Visibility` zděděna z `Item`. |
| `Field.cs` | Editovatelné pole: `Key`, `Label`, `FieldType`, `Value`, `BoolValue`, `DateValue`, `ListItems`, `FileFilter`, `ValidationError`, `HasError`, `Required`, `ValidateOnChange`, `Editable`, `ParentGroup`, `EffectiveVisibility` (override). Metoda `GetTypeError()` — klientská typová validace. `Visibility` zděděna z `Item`. (Volba `ListItem(Key, Label)` žije v root namespace `EDControls` — sdílená, viz `EDControls/dotnet/ListItem.cs`.) |
| `EFieldType.cs` | Enum: `Text`, `Number`, `Date`, `Decimal`, `Bool`, `Button`, `List` (strict dropdown), `ComboBox` (editable s autocomplete), `File`, `Color`. Pomocná třída `EFieldTypeWire` — konverze `ToWire()` / `FromWire()`. |
| `HexToBrushConverter.cs` | IValueConverter pro Color field — parsuje `#AARRGGBB` / `#RRGGBB` string na `SolidColorBrush` pro swatch binding. |
| _(ComboBox field)_ | Renderuje se sdíleným controlem `EDControls.EDFilterComboBox` (volný režim) — viz `EDControls/dotnet/EDFilterComboBox/`. Dřív lokální `SuggestComboBox`, přesunut jako obecná kontrolka. |
| `EItemVisibility.cs` | Enum: `Visible`, `Collapsed`, `UserHidden`, `ProgrammerHidden`. Pomocná třída `EItemVisibilityWire` — konverze `ToWire()` / `FromWire()`. |
| `EValidationTrigger.cs` | Enum: `Init`, `Change`, `BeforeSave`. |
| `ValidationContext.cs` | Datový objekt — trigger + `ChangedKey` (klíč pole, které vyvolalo validaci). |
| `NumericInput.cs` | Attached behavior — omezuje vstup TextBoxu na numerické znaky. Režimy: `Integer`, `Decimal`. |
| `DesignTimeData/EDInputFormDesignTimeData.cs` | Design-time ukázková data; nikdy se neinstanciuje za běhu. |

## Integrace s hostem (minimální)

```csharp
// V XAML:
<eif:EDInputForm x:Name="TheInputForm" CacheClass="NAMESPACE.MyClass"/>

// MLRest se čte automaticky z DataContext rodičovského okna (EDViewModel).
// Init() se volá automaticky při Loaded, pokud je CacheClass nastaveno.

// Handlery tlačítek (pokud je potřeba ruční řízení):
void Validate_Click(...) { bool ok = TheInputForm.Validate(); }
void Save_Click(...)     { bool ok = TheInputForm.Save();     }
```

Žádné nastavování MLRest není potřeba — control ho čte z DataContext rodičovského okna.

## Veřejné API

| Člen | Popis |
| --- | --- |
| `string CacheClass` | Musí být nastaveno před jakýmkoliv voláním. Caché třída implementující Init/Validate/Save. |
| `CVar? InitParams` | Volitelné initial parametry předané do Caché `Init` (čte přes `^||ML("IN","key")`). Pro pre-fill polí z hostitelského contextu. Nastav před prvním `Loaded`. |
| `void Init()` | Volá Caché `Init` (s `InitParams` pokud nastavené), naplní Items, spustí `ExecuteValidate(Init)`. Volá se automaticky při `Loaded` pokud je `CacheClass` nastaveno. |
| `bool Validate()` | Volá Caché `Validate` s Change triggerem; vrací true pokud nejsou chyby. |
| `bool Save()` | Validuje s BeforeSave triggerem, volá Caché `Save` pouze pokud je čistý stav; vrací true při úspěchu. |
| `IEnumerable<Item>? Items` | DP — aktuální položky formuláře; aktualizuje se po každém Caché volání. |
| `EHelpTextMode HelpTextMode` | DP — režim zobrazení nápovědy (`Tooltip` nebo `Column`). Čte se z Caché odpovědi. |
| `static CVar SerializeItemsCVar(items)` | Převede Items → CVar pro odeslání do Caché. |
| `static List<Item> ParseItems(CVar output)` | Převede Caché CVar výstup → seznam Item; přeskočí klíče "ERR" a "helpTextMode". |
| `static EHelpTextMode ParseHelpTextMode(output)` | Přečte `helpTextMode` z CVar výstupu. |

### EDInputFormController (testovatelný, bez WPF)

```csharp
// Pro unit testy nebo non-WPF scénáře:
var controller = new EDInputFormController(mockMLRest, "NAMESPACE.MyClass");
controller.Init();
bool ok = controller.Save();
List<Item> items = controller.Items;
```

## Stavba formuláře v Caché — preferovaný pattern

`Form.cls` nabízí dvě úrovně API:

**1) Helpery pro simple typy** (one-liner per field) — preferovaný pro nejčastější case:

```objectscript
s form = ##class(ALVA.EDControls.EDInputForm.Form).%New()
d form.AddGroup("font", "Font")

d form.AddText("text", "Obsah", initText)                                       ; Text field
d form.AddBool("bold", "Tučně", 0)                                              ; Bool checkbox
d form.AddNumber("count", "Počet", "10", "0", "999", "ks", 1)                   ; Integer s range/unit/required
d form.AddDecimal("size", "Velikost", "10", "4", "200", "pt", "0.0#")           ; Decimal s range/unit/format

; Helper vrací Field — caller může nastavit další properties:
s f = form.AddBool("debug", "Debug log", 0)
s f.helpText = "Uloží .import-debug.txt vedle PDF"
```

Helpery: `AddText`, `AddBool`, `AddNumber`, `AddDecimal`. Pokrývají 80% praktického použití.

**2) AddFieldToLastGroup + property setters** — pro complex typy (List/ComboBox/Color/File/Date):

```objectscript
; List/ComboBox potřebuje AddListItem
s f = form.AddFieldToLastGroup("stav", "Stav", $$$FieldTypeList, 0, 1)
d f.AddListItem("A", "Aktivní")
d f.AddListItem("N", "Neaktivní")
s f.value = "A"

; Color
s f = form.AddFieldToLastGroup("barva", "Barva textu", $$$FieldTypeColor, 0, 0)
s f.value = "#FF000000"

; File s filtrem
s f = form.AddFieldToLastGroup("soubor", "PDF soubor", $$$FieldTypeFile, 0, 0)
s f.fileFilter = "PDF (*.pdf)|*.pdf"
```

Tento pattern je extensibilní — pro každou novou Field property stačí `s f.xxx = ...` řádek.

## Caché wire protokol (CVar/CList)

### Položky formuláře (oba směry)

Položky se serializují jako CList záznamy se string-indexovanými klíči:

```text
CVar["1"] = CList("GROUP", number, key, groupName, visibility, helpText)
CVar["2"] = CList("FIELD", number, key, label, fieldType, value, validateOnChange, required, validationError, editable, visibility, helpText, listItems, fileFilter, min, max, format, unit)
CVar["helpTextMode"] = "tooltip" | "column"   ; form-level setting, default "tooltip"
```

Min/Max/Format/Unit (FIELD pozice 15-18): rozšíření pro Number/Decimal typy. `Min` a `Max` jako string formátovaný v InvariantCulture (validuje se klient i server). `Format` je standardní .NET format string (např. "N2", "0.##") — aplikován v UI. `Unit` je suffix label zobrazený vedle inputu (např. "mm", "pt") — pouze visualní, nemění Value.

Visibility (GROUP pozice 5, FIELD pozice 11): `"0"` = Visible (výchozí), `"1"` = Collapsed, `"2"` = UserHidden, `"3"` = ProgrammerHidden. Pokud chybí, položka je viditelná.

Editable (FIELD pozice 10): `"1"` = editovatelné (výchozí), `"0"` = pouze ke čtení. Pokud chybí, pole je editovatelné.

HelpText (GROUP pozice 6, FIELD pozice 12): volitelný text nápovědy. Pokud chybí nebo prázdný, nápověda se nezobrazí.

HelpTextMode: `"tooltip"` (výchozí) zobrazí ikonu `?` s tooltipem, `"column"` zobrazí text v samostatném sloupci vpravo.

ListItems (FIELD pozice 13): volitelný řetězec s volbami pro typ `List`. Oddělovač mezi položkami: `|` (pipe). Oddělovač mezi klíčem a popiskem: `~` (tilda). Příklad: `"A~Aktivní|N~Neaktivní"`. Pokud oddělovač `~` chybí, popisek = klíč. Přidání voleb v Caché:

```objectscript
s f = form.AddFieldToLastGroup("stav", "Stav", $$$FieldTypeList, 1, 0)
d f.AddListItem("A", "Aktivní")
d f.AddListItem("N", "Neaktivní")
s f.value = "A"
```

FileFilter (FIELD pozice 14): konfigurační řetězec pro typ `File`. Speciální hodnota `"$FOLDER"` přepne na výběr složky (`OpenFolderDialog`). Jiná hodnota se použije jako filtr pro `OpenFileDialog` (např. `"PDF (*.pdf)|*.pdf|Všechny soubory (*.*)|*.*"`). Pokud prázdný, výchozí filtr je "Všechny soubory". Příklad:

```objectscript
; File selection with filter
s f = form.AddFieldToLastGroup("soubor", "Soubor", $$$FieldTypeFile)
s f.fileFilter = "PDF (*.pdf)|*.pdf|Všechny soubory (*.*)|*.*"

; Folder selection
s f = form.AddFieldToLastGroup("slozka", "Složka", $$$FieldTypeFile)
s f.fileFilter = $$$FileFilterFolder
```

### ComboBox field type (editable s autocomplete)

Editable ComboBox s live filtrováním. Storage sdílí s `List` přes `Field.ListItems`, ale na rozdíl od List **user může napsat libovolný text** mimo list — items jsou nápověda (autocomplete), ne striktní enum. Analogie HTML `<select>` (= `List`) vs `<input list="...">` (= `ComboBox`).

```objectscript
s f = form.AddFieldToLastGroup("placeholder", "Pole agendy", $$$FieldTypeComboBox)
d f.AddListItem("CISLO_LINKY", "Číslo linky")
d f.AddListItem("NAZEV_LINKY", "Název linky")
d f.AddListItem("DATUM_OD",    "Platnost od")
; User si může napsat "MUJ_VLASTNI_KOD" — list je nápověda, ne enum
```

UI: zobrazí display string ve formátu `"KEY  —  Label"` v dropdownu, po výběru se `Field.Value` nastaví na samotný `Key`. Filter prochází key i label case-insensitive.

**Rozdíl od List:**
- `List` = strict (jen value z items, žádný custom string)
- `ComboBox` = editable (custom value allowed, items jsou nápověda)

### Color field type

`Field.Value` drží hex string `"#AARRGGBB"` (SkiaSharp formát, ARGB s alpha). UI zobrazí barevný čtvereček + monospace TextBox + tlačítko `...` otevírající color picker dialog.

```objectscript
s f = form.AddFieldToLastGroup("textColor", "Barva textu", $$$FieldTypeColor)
s f.value = "#FF003366"   ; tmavě modrá s plnou alpha
```

**Picker implementace:** EDControls je WPF-only knihovna — color dialog **musí dodat host** přes `EDInputForm.PickColorImpl` (static `Func<Window, string, string?>`). Pokud null, tlačítko v Color field templatu nic neudělá (user může psát hex ručně do TextBoxu). Toto rozhodnutí odděluje EDControls od WinForms závislosti.

```csharp
// V App.xaml.cs nebo wiring (TD má UseWindowsForms a má ColorPickerHelper s recent colors):
EDControls.EDInputForm.EDInputForm.PickColorImpl =
    (owner, currentHex) => TemplateDesigner.Controls.ColorPickerHelper.PickColor(owner, currentHex);
```

### Struktura volání Validate

```text
IN["trigger"]    = "Init" | "Change" | "BeforeSave"
IN["changedKey"] = "fieldKey"   (pouze pro Change trigger s konkrétním polem; jinak chybí)
IN["1"..]        = serializované položky formuláře (CList záznamy)
```

## Automatická inicializace při Loaded

Pokud je `CacheClass` neprázdný, `Init()` se zavolá automaticky při události `Loaded`.
Žádné ruční volání `Init()` v hostitelském okně není potřeba.

## Automatická validace při změně pole

Když `Field` s `ValidateOnChange == true` změní svou `Value`, EDInputForm zavolá
`ExecuteValidate(Change)` automaticky přes `Dispatcher.BeginInvoke` — bez zásahu hostitele.
Klíč změněného pole se předá jako `changedKey` — na Caché straně dostupný přes
`form.changedKey`, `form.HasChangedField()` nebo `form.GetChangedField()`.

Totéž platí pro tlačítka (`FieldTypeButton`): kliknutí spustí validační roundtrip pouze
pokud má pole `ValidateOnChange == true`. Tlačítko s `ValidateOnChange == false` při
kliknutí neprovede žádný server roundtrip.

## Klávesová navigace

**Tab / Shift+Tab** přesouvá fokus přímo mezi vnitřními controly polí (TextBox, ComboBox, DatePicker, CheckBox) — nativní WPF chování.

**Enter** (bez Ctrl) funguje jako Tab — přesune fokus na další pole. Na poli typu Button místo toho rovnou klikne.

**Ctrl+Enter** potvrdí dialog (vyvolá `OkCommand` na EDViewModelu). Samotný Enter tedy NEZAVÍRÁ dialog.

**Ctrl+číslice** naviguje na položku, jejíž `Number` odpovídá akumulované hodnotě (200 ms buffer, např. Ctrl+1,2 → položka 12). Pokud je cíl `Group`, přepne `IsExpanded`.

## DŮLEŽITÉ: Round-trip pravidlo

**Každá** property EDInputFormu (položek i formuláře) musí být nastavitelná na Caché straně a musí se round-tripovat — tj. hodnota nastavená serverem se odešle zpět v dalším Validate/Save volání, aby ji server mohl číst a měnit. Pokud přidáváš novou property, zajisti:

1. Caché ji zapisuje do `Serialize()` / `WriteToOut()`.
2. .NET ji čte v `ParseItems()` / `ParseHelpTextMode()` (nebo analogicky).
3. .NET ji zapisuje zpět v `SerializeItemsCVar()` / `ExecuteValidate()`.
4. Caché ji čte v `ReadFromIn()`.

Bez round-tripu server ztratí stav po prvním round-tripu a přepíše ho výchozí hodnotou.

## Návrhové poznámky

- `MLRest` se čte z `EDViewModel` DataContext rodičovského okna — žádný setter na controlu.
- `EDControls.csproj` odkazuje `C:\Edison\MLRest.dll` přímo (stejná HintPath konvence jako EDISONMAIN2).
- `ExecuteValidate` je private; host používá `Validate()` a `Save()`.
- `OnItemsChanged` přihlašuje/odhlašuje `PropertyChanged` na každém `Field` proti únikům paměti.
- `EDInputFormDesignTimeData` je odkazován pouze z `d:DataContext` v XAML; nikdy neinstanciuj za běhu.
- Stav `Visibility` se přenáší přes wire format — server ho round-tripuje přes `ReadFromIn` → `WriteToOut`. Žádné client-side preservation není potřeba.
- `IsExpanded` na `Group` je computed property mapující `Visibility`: `Visible` = expanded, `Collapsed` = collapsed. Není to samostatný stav.

## Referenční příklad — EDIFExample.cls

`EDControls/CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls` je kompletní ukázka všech EDIF featur (typy polí, povinnost, helpText, editovatelnost, viditelnost, validace, tlačítka, kombinace).
Při přidání nové featury do EDInputFormu přidej odpovídající příklad i do Example.cls.
