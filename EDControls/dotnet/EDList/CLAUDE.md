# EDList - poznámky pro vývojáře

Read-only tabulka řízená z Caché. Host nastaví `CacheClass`, zavolá `InitializeList()`
(definice sloupců) a `LoadList()` / `LoadList(filter)` (data); služby čte z `EDServices.Current`
(host **nemusí** mít VM dědící `EDViewModel`), více instancí v okně (master-detail) je
standardní scénář. API čti z `EDList.xaml.cs`; Caché builder `ALVA.EDControls.EDList.List`,
referenční implementace `MockList.cls`.

⚠️ **Preferuj `InitializeListAsync()` / `LoadListAsync(filter)`.** Synchronní varianty drží UI
vlákno po celý round-trip - na pomalé lince zmrazí **celou aplikaci**, ne jen hostující okno.
Async varianty navíc serializují souběžné dotazy (sdílený `MLRest` jede přes `^||ML`) a odpověď
staršího dotazu už nedosedne - rychlé přepínání filtru nezamíchá řádky. Filtrovaný seznam přes
`EDFilter`: automatický reload (Loaded + změna hodnoty pole) jede `EDFilter.RefreshAsync()`, host
nemusí dělat nic; synchronní `EDFilter.Refresh()` zůstává pro ruční trigger.

⚠️ **`CacheClass` (a každá další DP) se nesmí číst uvnitř `Task.Run`** - skončí
`InvalidOperationException` („volající vlákno nemá přístup..."); hodnotu si vytáhni do lokální
proměnné **před** přechodem na threadpool. Testy to nechytí (WPF kontroluje vlákno až za běhu).

## Wire kontrakt (JSON přes `^||ML`)

Detaily ve sdíleném `EDControls/DOCS/01-wire-formats.md` - tam patří změny formátu.

- `InitializeList`: `OUT[1]` = **jedno** JSON pole definic sloupců (parser čte jen `OUT[1]`).
- `LoadList`: `OUT[1..n]` = jeden JSON objekt na řádek; **každý řádek musí mít `id`** -
  bez něj se tiše zahodí. Klíč `"ERR"` parser přeskakuje.
- Filtr: `LoadList(filter)` posílá `^||ML("IN","filter","klíč")` - pojmenované klíče, ne
  poziční; Caché čte `$g(^||ML("IN","filter","k"))`.
- Hodnoty vždy interní formát jako string: NUMBER s **tečkou**, DATE ISO-8601,
  DATETIME `yyyy-MM-ddTHH:mm:ss`, BOOL `"1"`/`"0"`, HHMM int (`830` = 08:30),
  COLOR `"R;G;B"`. Zobrazení řídí `type` + `format` na klientovi.

## Pasti

- Řazení je **typové** - custom comparer (`EDListSort`) nad raw hodnotami přes
  `ListCollectionView.CustomSort`, ne `SortDescriptions` (ty by řadily formátované
  stringy lexikograficky). Shift+klik = multi-sort.
- **NUMBER bez `format` se nijak neformátuje** - v UI zůstane „142.5" s tečkou.
- **IMAGE typ není implementován v renderingu** - propadne na text a zobrazí raw název
  resource. CODEBOOK taky chybí. Neparsovatelné datum se zobrazí raw, bez chyby.
- COLOR vykreslí malý obdélník v buňce, ne podbarvení celé buňky.
- `align`/`isLink` jsou samostatné vlastnosti sloupce, ne součást typu - legacy typy
  (`Tc`, `NL`, ...) se mapují na TEXT/NUMBER + vlastnost.

## Search, Check mode, persist layoutu

- `Title` / `SearchText` / `SearchVisible` (`bool?`, `null` = auto podle Title). Search
  filtruje **formátované** hodnoty („8:30" najde „08:30", ne „830"); nepersistuje se.
- `SelectionMode = Check` → checkbox sloupec. Binding checkboxů je záměrně **OneWay** +
  explicitní `Click` handler (TwoWay v CellTemplate je buggy; header `Click` brání rekurzi
  CheckAll→Sync). Datové sloupce identifikuje `Tag = column.Key`; checkbox sloupec Tag nemá
  (přeskakuje ho search i persist). **Dvojklik na řádek** přepne zaškrtnutí, **Shift+klik**
  na checkbox zaškrtne rozsah od kotvy (posledního kliku) v zobrazeném pořadí.
- Persist layoutu (šířky, pořadí, sort): nastav `HostId` (konvence `"<Projekt>.<Consumer>"`;
  prázdný = off). Storage `^ALVA.EDControls.UserData(IdFa, userID, "EDControls.EDList", HostId)`,
  save debounce 500 ms + force na Unload, load async po `LoadList`, best-effort.
- `LoadList` resetuje checked state, odhlásí `PropertyChanged` starých řádků
  a re-aplikuje search + layout.

Testovací okno: `EDISONMAIN2/dotnet/Windows/EDListTestWindow/` nad `MockList.cls`.
