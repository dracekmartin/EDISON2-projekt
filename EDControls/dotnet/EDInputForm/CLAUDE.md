# EDInputForm - poznámky pro vývojáře

Dynamický formulář definovaný a validovaný v Caché. Host nastaví `CacheClass`
(+ volitelně `InitParams`), `Init()` proběhne automaticky na `Loaded`. Služby se čtou
z `EDServices.Current` - **ne** z DataContext okna. API čti z `EDInputForm.xaml.cs`;
orchestrace bez WPF je `EDInputFormController.cs`, kodek wire formátu `EDInputFormWire.cs`
(obojí testovatelné bez controlu); Caché builder je
`ALVA.EDControls.EDInputForm.Form`. Referenční ukázka featur:
`EDControls/CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls` - **při přidání nové featury přidej příklad i tam**.

## Wire formát (pořadí pozic = protokol, neměnit!)

- `FIELD`: `number, key, label, type, value, validateOnChange, required, validationError,
  editable, visibility, helpText, listItems, fileFilter, min, max, format, unit, placeholders`
  (`Field.cls::Serialize` ⟷ `ParseItems`).
- `GROUP`: `number, key, groupName, visibility, helpText`.
- Visibility: `"0"`=Visible (default, i když chybí), `"1"`=Collapsed, `"2"`=UserHidden,
  `"3"`=ProgrammerHidden. Konstanty v `EDControls/CACHE/EDInputForm.inc`.
- ListItems: položky dělí `|`, klíč od popisku `~`; bez `~` popisek = klíč.
- FileFilter: značka `"$FOLDER"` → výběr složky; jinak filtr pro OpenFileDialog.
- Color: value `"#AARRGGBB"` (SkiaSharp ARGB). Validate posílá
  `IN["trigger"]` (`Init|Change|BeforeSave`) + `IN["changedKey"]` (jen u Change
  s konkrétním polem; Caché čte `form.changedKey` / `GetChangedField()`).

## Round-trip pravidlo

Každá property se musí round-tripovat (server ji zapisuje v `Serialize`/`WriteToOut`,
.NET čte v `ParseItems` a posílá zpět v `SerializeItemsCVar`, server čte v `ReadFromIn`),
jinak ji server po prvním round-tripu ztratí. **Výjimka: Image** - Base64 value se
na server neposílá (limit MLRest) a preservuje se client-side v controlleru.
Save rozlišuje „server vrátil form" vs. no-op podle číselných subscriptů v OUT
(`HasFormItems`) - bez nich zachová klientské Items.

## Pasti

- Host hooky: color/image picker dodává host přes static `PickColorImpl` /
  `PickImageImpl` (EDControls je WPF-only, dialog nemá). Null → tlačítko nic nedělá.
- Eventy `SchemaLoaded`, `ButtonClicked`, `FieldChanged`: `FieldChanged` fajruje při
  **každé** změně Value bez ohledu na `ValidateOnChange`; `ButtonClicked` při každém
  kliku (server roundtrip jen s `ValidateOnChange=1`). `Initialized` je deprekovaný alias
  `SchemaLoaded` (stínil `FrameworkElement.Initialized`) - fajruje pořád, smaže se až při
  koordinovaném publish obou aplikací.
- Setter `CacheClass` resetuje controller - příští `Init()` načte schema novou třídou.
- Klientská validace: type-error má prioritu před required; type-check běží hned
  při setu Value.
- Volitelný `List` dostane implicitní prázdnou položku, povinný ne.
- `ComboBox` = editable s autocomplete (custom hodnota povolena, items jsou nápověda);
  `List` = strict enum. Renderuje se sdíleným `EDFilterComboBox`.
- `EDComponentToolbar` sedí `Flat="True"` přímo v hlavičkovém pruhu `Položka | Hodnota`
  (jeden pruh, pozadí kreslí Border formuláře). Search vstupuje do `EffectiveVisibility`
  (pole může být skryté hledáním nezávisle na `Visibility`); match je diakritika-insensitive,
  stav rozbalení skupin se před hledáním ukládá a po zrušení obnovuje.
- Enter = Tab (na Buttonu klikne); dialog potvrzuje jen Ctrl+Enter (window-level
  binding hosta). Ctrl+číslice skáče na položku dle `Number` (200 ms buffer).
- Popup nápovědy (ikona v hlavičkovém pruhu, `HelpCommand`) se generuje z `KeyboardShortcuts`
  v `EDInputForm.xaml.cs` - **při přidání/změně zkratky aktualizuj tento seznam**.
  Vypisuj jen reálně nadrátované zkratky (search Enter/Shift+Enter/Esc zatím nefungují).
- Save při validačních chybách zobrazí popup přes `PopupDisplayer` a vrátí false.
