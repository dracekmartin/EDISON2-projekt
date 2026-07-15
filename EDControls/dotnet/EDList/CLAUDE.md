# EDList — poznámky pro vývojáře

Read-only tabulka řízená z Caché. Host nastaví `CacheClass`, zavolá `InitializeList()`
(definice sloupců) a `LoadList()` / `LoadList(filter)` (data). Služby čte z
`EDServices.Current` — host **nemusí** mít VM dědící `EDViewModel`. Více instancí
v okně (master–detail) je standardní scénář. API čti z `EDList.xaml.cs`; Caché builder
je `ALVA.EDControls.EDList.List` (`AddColumn`/`AddRow` + `WriteColumnsToOut`/`WriteRowsToOut`),
referenční implementace `MockList.cls`.

## Wire kontrakt (JSON přes `^||ML`)

Detaily ve sdíleném `EDControls/DOCS/01-wire-formats.md` — tam patří změny formátu.

- `InitializeList`: `OUT[1]` = **jedno** JSON pole definic sloupců (parser čte jen `OUT[1]`).
- `LoadList`: `OUT[1..n]` = jeden JSON objekt na řádek; **každý řádek musí mít `id`** —
  bez něj se tiše zahodí. Klíč `"ERR"` parser přeskakuje.
- Filtr: `LoadList(filter)` posílá `^||ML("IN","filter","klíč")` — pojmenované klíče,
  ne poziční indexy; Caché čte `$g(^||ML("IN","filter","k"))`.
- Hodnoty vždy interní formát jako string: NUMBER s **tečkou**, DATE ISO-8601,
  DATETIME `yyyy-MM-ddTHH:mm:ss`, BOOL `"1"`/`"0"`, HHMM int (`830` = 08:30),
  COLOR `"R;G;B"`. Zobrazení řídí `type` + `format` na klientovi.

## Pasti

- Řazení je **typové** — custom comparer (`EDListSort`) nad raw hodnotami přes
  `ListCollectionView.CustomSort`, ne `SortDescriptions` (ty by řadily formátované
  stringy lexikograficky). Shift+klik = multi-sort.
- **NUMBER bez `format` se nijak neformátuje** — v UI zůstane „142.5" s tečkou.
- **IMAGE typ není implementován v renderingu** — propadne na text a zobrazí raw název
  resource. CODEBOOK taky chybí.
- COLOR vykreslí malý obdélník v buňce, ne podbarvení celé buňky.
- `align`/`isLink` jsou samostatné vlastnosti sloupce, ne součást typu — legacy typy
  (`Tc`, `NL`, ...) se mapují na TEXT/NUMBER + vlastnost.
- Neparsovatelné datum se zobrazí raw, bez chyby.

## Search, Check mode, persist layoutu

- `Title` / `SearchText` / `SearchVisible` (`bool?`, `null` = auto podle Title) — header
  bar. Search filtruje **formátované** hodnoty („8:30" najde „08:30", ne „830");
  nepersistuje se.
- `SelectionMode = Check` → checkbox sloupec. Binding checkboxů je záměrně **OneWay** +
  explicitní `Click` handler (TwoWay v CellTemplate je buggy; header `Click` brání
  rekurzi CheckAll→Sync). Datové sloupce se identifikují přes `Tag = column.Key`;
  checkbox sloupec Tag nemá (přeskakuje ho search i persist).
- Persist layoutu (šířky, pořadí, sort): nastav `HostId` (konvence
  `"<Projekt>.<Consumer>"`); prázdný = off. Storage
  `^ALVA.EDControls.UserData(IdFa, userID, "EDControls.EDList", HostId)`, save debounce
  500 ms + force na Unload, load async po `LoadList`, best-effort.
- `LoadList` resetuje checked state, odhlásí `PropertyChanged` starých řádků
  a re-aplikuje search + layout.

Testovací okno: `EDISONMAIN2/dotnet/Windows/EDListTestWindow/` nad `MockList.cls`.
