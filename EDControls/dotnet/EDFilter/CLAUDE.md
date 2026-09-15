# EDFilter - poznámky pro vývojáře

Pruh filtračních polí psaných jako children v XAML (`[ContentProperty(Fields)]`). Pole vlevo,
tlačítko vpravo. Sám nic nenačítá - sebere hodnoty a předá je cíli. API čti ze zdrojáků.

## Cíl filtru

Na konkrétní control **není vázaný**: `Target` je `IEDFilterTarget`
(`ApplyFilterAsync(filter)`, root namespace `EDControls`), implementuje ho `EDList`, stejně
tak může mapa nebo graf. Pro cíl řízený ViewModelem je tu `FilterCommand` (parametr =
`IReadOnlyDictionary<string, string>`); nastavit jde obojí, zavolá se obojí.

**Bez `Target` i bez `FilterCommand` je refresh no-op včetně `FilterChanged`** - host by
jinak reagoval na načtení, které neproběhlo.

## Wire formát

`Key` pole = filter klíč. Prázdná hodnota nebo prázdný `Key` → klíč se neposílá, protože
Caché `$g()` vrací `""` a endpoint filtr přeskočí. Komponenty nad Caché to posílají jako
`^||ML("IN","filter",Key)` (detail v `DOCS/01-wire-formats.md §Filter konvence`).

## Pořadí při startu (na tom záleží)

`Loaded` → obnova uložených hodnot → **jeden** refresh. Obráceně by cíl načítal dvakrát,
nejdřív bez filtru a hned znovu s obnovenými hodnotami. Během obnovy drží `_suppressAutoTrigger`,
jinak každé dosazené pole spustí vlastní refresh i vlastní uložení.

`Loaded` ve zkušebním visual tree nefíruje, proto je celá sekvence v `internal InitialLoadAsync()`.

## Pamatování hodnot

Jen s vyplněným `HostId` (konvence `"<Projekt>.<Consumer>"`, prázdný = vypnuto). Jde přes
`EDServices.Current.UserPrefs`, scope `EDControls.EDFilter`, key `HostId` - tedy per uživatel
× IdFa, ne per stroj. Blob `{"version":1,"values":{...}}`; varianty filtru přibudou jako
sourozenecké klíče, čtení `values` se nezmění. Ukládá se s 500 ms debounce a jistí se
v `Unloaded`. Best-effort: persistence nikdy nesmí shodit UI.

## Pole (`EDFilterFieldBase`)

Potomek implementuje `GetValue()` (hodnota → string) a `RestoreValue(string)` (zpátky).
`RestoreValue` **není** `SetValue` schválně, aby se nepletlo s WPF přetížením.

`EDFilterCombo` dosazuje obnovenou hodnotu hledáním položky, jejíž `SelectedValuePath`
hodnota má shodný `ToString()` - uložená hodnota je vždy string, kód položky může být číslo.
Past: VM plní `ItemsSource` často až po `Loaded`, na prázdné kolekci by se hodnota ztratila,
proto si ji pole drží v `_pendingRestore` a dosadí ji, jakmile položky dorazí (hlídá
i `CollectionChanged`, samotná změna DP nestačí - `ObservableCollection` se plní `Add`em).

## Layout

`DockPanel LastChildFill="False"`, **tlačítko musí stát v XAML před poli** - DockPanel
rozdává prostor v pořadí deklarace a `WrapPanel` polí by mu jinak sebral celou šířku.
Po dobu refreshe drží `IsBusy` a tlačítko je zakázané: cizí `IEDFilterTarget` nemusí mít
ochranu proti souběžným dotazům, kterou má `EDList`.
