# EDInputForm - poznámky pro vývojáře

Dynamický formulář definovaný a validovaný v Caché. Host nastaví `CacheClass` (+ volitelně
`InitParams`), `Init()` proběhne automaticky na `Loaded` **jednou per `CacheClass`** (návrat do
stromu, např. přepnutí tabu, ho neopakuje; změna `CacheClass` nebo explicitní `Init()` ano).
Služby se čtou z `EDServices.Current`, **ne** z DataContext okna. Orchestrace bez WPF je
`EDInputFormController.cs`, kodek wire formátu `EDInputFormWire.cs` (obojí testovatelné bez
controlu), Caché builder `ALVA.EDControls.EDInputForm.Form`. Referenční ukázka featur
`CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls` - **novou featuru přidej i tam**.

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
- Eventy: `FieldChanged` fajruje při **každé** změně Value bez ohledu na `ValidateOnChange`;
  `ButtonClicked` při každém kliku (server roundtrip jen s `ValidateOnChange=1`). `Initialized`
  je deprekovaný alias `SchemaLoaded` (stínil `FrameworkElement.Initialized`) - fajruje pořád,
  smaže se až při koordinovaném publish obou aplikací.
- Setter `CacheClass` resetuje controller - příští `Init()` načte schema novou třídou.
- Klientská validace: type-error má prioritu před required; type-check běží hned při setu Value.
- **Číselné pole (`Number`/`Decimal`) mění při setu `Value` čárku na tečku** (uvnitř i na drátě
  vždy tečka, numerická klávesnice dává čárku). Čárka je **vždy desetinná**, nikdy oddělovač
  tisíců (`NumberStyles.Float`, ne `Number` - jinak „1.500" projde jako 1500). Normalizuje
  i setter `FieldType` - volající nemusí plnit typ před hodnotou.
- Volitelný `List` dostane implicitní prázdnou položku, povinný ne.
- `ComboBox` = editable s autocomplete (custom hodnota povolena, items jsou nápověda);
  `List` = strict enum. Renderuje se sdíleným `EDFilterComboBox`.
- Kořen: brandový `EDComponentToolbar` (týž jako EDList), pod ním rovnou `ScrollViewer`.
  **Toolbar nesmí spadnout dovnitř scrolleru** - scrollbar ukrojí pravý konec hledání
  (`EDInputFormHeaderTests`). `IsSharedSizeScope` na vnějším Gridu sjednocuje šířku popisků;
  `HorizontalScrollBarVisibility="Disabled"` drží obsah v šířce viewportu (default `Hidden` měří
  na nekonečné šířce, takže úzký formulář místo zúžení ořízne).
- **Pod 380 px jde popisek nad hodnotu** (`IsNarrowLayout`, zpět nad 400 px - hystereze proti
  kmitání). Dvousloupcová minima se do `MinWidth=240` nevejdou, proto dvě šablony řádku místo
  přepínání `ColumnDefinition`. **Hostitel nesmí formulář rozměrovat podle obsahu** - šířka řídí
  režim a režim by řídil šířku (`EDInputFormNarrowLayoutTests`).
- Search vstupuje do `EffectiveVisibility` (pole může být skryté hledáním nezávisle na
  `Visibility`); match je diakritika-insensitive, stav rozbalení skupin se před hledáním ukládá
  a po zrušení obnovuje. **Navigace mezi nálezy tu není** - filtrování nechá vidět jen nálezy.
- Enter = Tab (na Buttonu klikne); dialog potvrzuje jen Ctrl+Enter (window-level
  binding hosta). Ctrl+číslice skáče na položku dle `Number` (200 ms buffer).
- Popup nápovědy (`HelpCommand`) se generuje z `KeyboardShortcuts` v `EDInputForm.xaml.cs` -
  **při přidání/změně zkratky aktualizuj seznam**; jen reálně nadrátované zkratky.
- Save při validačních chybách zobrazí popup přes `PopupDisplayer` a vrátí false.
