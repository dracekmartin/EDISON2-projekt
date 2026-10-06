# EDInputForm - poznámky pro vývojáře

Dynamický formulář definovaný a validovaný v Caché. Host nastaví `CacheClass` (+ volitelně
`InitParams`), `Init()` proběhne automaticky na `Loaded` **jednou per `CacheClass`** (návrat do
stromu, např. přepnutí tabu, ho neopakuje; změna `CacheClass` nebo explicitní `Init()` ano).
Ukázka všech featur `CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls` - **novou přidej i tam**.

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

Každá property se musí round-tripovat (`Serialize`/`WriteToOut` ⟷ `ParseItems` / `SerializeItemsCVar` ⟷
`ReadFromIn`), jinak ji server po prvním round-tripu ztratí. **Výjimka: Image** - Base64 value se na server
neposílá (limit MLRest) a preservuje se client-side v controlleru. Save rozlišuje „server vrátil form" vs. no-op
podle číselných subscriptů v OUT (`HasFormItems`) - bez nich zachová klientské Items.

## Pasti

- Host hooky: color/image picker dodává host přes static `PickColorImpl` /
  `PickImageImpl` (EDControls je WPF-only, dialog nemá). Null → tlačítko nic nedělá.
- Eventy: `FieldChanged` při **každé** změně Value bez ohledu na `ValidateOnChange`, `ButtonClicked`
  při každém kliku (roundtrip jen s `ValidateOnChange=1`). `Initialized` = deprekovaný alias
  `SchemaLoaded` (stínil `FrameworkElement.Initialized`), smaže se až s koordinovaným publish obou aplikací.
- ⚠️ **Validace je záměrně synchronní** (na změnu jen odložená za handlery hosta): odpověď nahrazuje celé Items, takže
  asynchronní odpověď by zahodila fokus i rozepsaný text a echo by vracelo úpravy hosta. Async je **odložené k diskusi**:
  pasti `.claude/reviews/architecture.md` A15, hotová implementace větev `odlozeno/a15-async-edinputform`. Nezavádět bez vlastníka.
- Klientská validace: type-error má prioritu před required; type-check běží hned při setu Value.
- **Číselné pole mění při setu `Value` čárku na tečku** (uvnitř i na drátě vždy tečka). Čárka je
  **vždy desetinná**, nikdy oddělovač tisíců (`NumberStyles.Float`, ne `Number` - jinak „1.500"
  projde jako 1500). Normalizuje i setter `FieldType`, takže na pořadí typ/hodnota nezáleží.
- Volitelný `List` dostane implicitní prázdnou položku, povinný ne.
- `ComboBox` = editable s autocomplete (custom hodnota povolena, items jsou nápověda);
  `List` = strict enum. Renderuje se sdíleným `EDFilterComboBox`.
- Kořen: brandový `EDComponentToolbar` (týž jako EDList), pod ním rovnou `ScrollViewer` - **toolbar
  nesmí spadnout dovnitř scrolleru**, scrollbar ukrojí pravý konec hledání (`EDInputFormHeaderTests`).
  `IsSharedSizeScope` na vnějším Gridu sjednocuje šířku popisků;
  `HorizontalScrollBarVisibility="Disabled"` drží obsah v šířce viewportu (`Hidden` měří na
  nekonečné šířce a úzký formulář ořízne).
- **Pod 380 px jde popisek nad hodnotu** (`IsNarrowLayout`, zpět nad 400 px - hystereze proti
  kmitání). Dvousloupcová minima se do `MinWidth=240` nevejdou, proto dvě šablony řádku místo
  přepínání `ColumnDefinition`. **Hostitel nesmí formulář rozměrovat podle obsahu** - šířka řídí
  režim a režim by řídil šířku (`EDInputFormNarrowLayoutTests`).
- Search vstupuje do `EffectiveVisibility` (pole může být skryté hledáním nezávisle na
  `Visibility`). **Navigace mezi nálezy tu záměrně není** - filtrování nechá vidět jen nálezy.
- `SearchMemoryKey` (opt-in) drží hledaný výraz v `ISessionState` do konce běhu aplikace; dosazuje
  se až po `SchemaLoaded` (dřív by filtr běžel nad prázdnými Items), smazaný filtr se zapomene.
- Enter = Tab (na Buttonu klikne); dialog potvrzuje jen Ctrl+Enter (window-level
  binding hosta). Ctrl+číslice skáče na položku dle `Number` (200 ms buffer).
- Popup nápovědy (`HelpCommand`) se generuje z `KeyboardShortcuts` v `EDInputForm.xaml.cs` -
  **při přidání/změně zkratky aktualizuj seznam**; jen reálně nadrátované zkratky.
