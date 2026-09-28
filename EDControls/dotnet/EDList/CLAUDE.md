# EDList - poznámky pro vývojáře

Read-only tabulka řízená z Caché. Host nastaví `CacheClass`, zavolá `InitializeList()`
(definice sloupců) a `LoadList()` / `LoadList(filter)` (data); služby čte z `EDServices.Current`
(host **nemusí** mít VM dědící `EDViewModel`), více instancí v okně (master-detail) je
standardní scénář. Caché builder `ALVA.EDControls.EDList.List`, referenční `MockList.cls`
a testovací okno `EDISONMAIN2/dotnet/Windows/EDListTestWindow/`.

⚠️ **Preferuj `InitializeListAsync()` / `LoadListAsync(filter)`.** Synchronní varianty drží UI
vlákno po celý round-trip - na pomalé lince zmrazí **celou aplikaci**. Async varianty navíc
serializují souběžné dotazy (sdílený `MLRest` jede přes `^||ML`) a odpověď staršího dotazu už
nedosedne - rychlé přepínání filtru nezamíchá řádky. Filtrovaný seznam přes `EDFilter`: EDList je
jeho `IEDFilterTarget` (`ApplyFilterAsync` → `LoadListAsync`), reload si řídí filtr sám a host
nemusí dělat nic; ruční trigger je `EDFilter.RefreshAsync()`.

⚠️ **`CacheClass` (a každá další DP) se nesmí číst uvnitř `Task.Run`** - skončí
`InvalidOperationException` („volající vlákno nemá přístup..."); hodnotu si vytáhni do lokální
proměnné **před** přechodem na threadpool. Testy to nechytí (WPF kontroluje vlákno až za běhu).

**Data zvenčí**: `SetData(columns, rows)` naplní seznam bez `CacheClass` a MLRest (host
s vlastním transportem). Řádky jsou **raw** serverové hodnoty včetně `id`, formátování i sort
klíče staví stejná cesta jako `LoadList`. Vzhled z dat řádku (barva textu) přes `CellStyle` a
skrytý sloupec (`Visible = false`): projde indexerem, ale nerenderuje se ani nehledá.
Referenční `EDISONMAIN2.LegacyMiniApps/dotnet/DashboardListSource.cs`.

## Wire kontrakt (JSON přes `^||ML`)

Detaily ve sdíleném `EDControls/docs/wire-formaty.md` - tam patří změny formátu.

- `InitializeList`: `OUT[1]` = **jedno** JSON pole definic sloupců (parser čte jen `OUT[1]`).
- `LoadList`: `OUT[1..n]` = jeden JSON objekt na řádek; **každý řádek musí mít `id`** -
  bez něj se tiše zahodí. Klíč `"ERR"` parser přeskakuje.
- Filtr: pojmenované klíče `^||ML("IN","filter","klíč")`, ne poziční (viz EDFilter/CLAUDE.md).
- Hodnoty vždy interní formát jako string: NUMBER s **tečkou**, DATE ISO-8601,
  DATETIME `yyyy-MM-ddTHH:mm:ss`, BOOL `"1"`/`"0"`, HHMM int (`830` = 08:30),
  COLOR `"R;G;B"`. Zobrazení řídí `type` + `format` na klientovi.

## Pasti

- Řazení je **typové** - comparer (`EDListSort`) nad raw hodnotami přes `CustomSort`, ne
  `SortDescriptions` (ty řadí formátované stringy lexikograficky). Shift+klik = multi-sort.
- **NUMBER bez `format` se nijak neformátuje** - v UI zůstane „142.5" s tečkou.
- **IMAGE ani CODEBOOK nejsou v renderingu** - propadnou na text (raw název resource).
  Neparsovatelné datum se zobrazí raw, bez chyby. COLOR kreslí obdélník, ne podbarvení buňky.
- `align`/`isLink` jsou samostatné vlastnosti sloupce, ne součást typu - legacy typy
  (`Tc`, `NL`, ...) se mapují na TEXT/NUMBER + vlastnost.

## Search, Check mode, persist layoutu

- **Brandový pruh je bezpodmínečný** (shodně s `EDInputForm`em), host plní jen `Title`.
- Search filtruje **formátované** hodnoty („8:30" najde „08:30", ne „830"); nepersistuje se.
- `SelectionMode = Check` → checkbox sloupec. Binding checkboxů je záměrně **OneWay** +
  explicitní `Click` handler (TwoWay v CellTemplate je buggy; header `Click` brání rekurzi
  CheckAll→Sync). **Dvojklik na řádek** přepne zaškrtnutí, **Shift+klik** na checkbox zaškrtne
  rozsah od kotvy (posledního kliku) v zobrazeném pořadí.
- Persist layoutu (šířky, pořadí, sort): nastav `HostId` (konvence `"<Projekt>.<Consumer>"`;
  prázdný = off). Storage `^ALVA.EDControls.UserData(IdFa, userID, "EDControls.EDList", HostId)`,
  save debounce 500 ms + force na Unload, load async po `LoadList`, best-effort.
- `LoadList` resetuje checked state a re-aplikuje search + layout.
