# EDList — poznámky pro vývojáře

## Stav: VE VÝVOJI — zatím neimplementováno

## Účel

`EDList` je samostatný WPF `UserControl` zobrazující read-only seznam (tabulka se sloupci
a řádky) řízený z Caché backendu. Host nastaví `MLRest` a `CacheClass`, zavolá
`InitializeList()` pro definici sloupců a `LoadList()` pro načtení dat. Výběr řádku
a standardní interakce (dvojklik, kontextové menu) jsou vystaveny jako eventy.

Více instancí v jednom okně je standardní scénář — každá instance je samostatný objekt
se svým `CacheClass` a voláními:

```csharp
ListVozidla.InitializeList();
ListVozidla.LoadList();

ListSpoje.InitializeList();
ListSpoje.LoadList();

ListVozidla.SelectionChanged += (_, id) => ListSpoje.LoadList();
```

---

## Mapa souborů

### C# / WPF  (`EDControls/dotnet/EDList/`)

| Soubor | Role |
| --- | --- |
| `EDList.xaml / .xaml.cs` | UserControl. Host pro DataGrid, generování sloupců, napojení eventů. |
| `ListColumn.cs` | Definice sloupce: `Key`, `Label`, `Type`, `Format`, `Width`, `Align`, `Sortable`, `Visible`, `IsLink`. |
| `EListColumnType.cs` | Enum datových typů sloupce — viz sekce níže. |
| `EListColumnAlign.cs` | Enum: `Auto`, `Left`, `Center`, `Right`. |
| `ColumnLayout.cs` | Record pro ukládání uživatelského nastavení sloupců (pořadí + šířka). Připraveno pro budoucí implementaci. |

### Caché ObjectScript  (`EDControls/CACHE/ALVA/EDControls/EDList/`)

| Soubor | Role |
| --- | --- |
| `Column.cls` | `%RegisteredObject` + `%JSON.Adaptor`. Model definice sloupce. |
| `List.cls` | `%RegisteredObject`. Builder: `AddColumn`, `AddRow`, `WriteColumnsToOut`, `WriteRowsToOut`. |

---

## Datové typy sloupců

Typ sloupce určuje výchozí zarovnání, způsob formátování, řazení a renderování buňky.
Zarovnání lze vždy přebít vlastností `align`. Chování "link" (klikatelná buňka) je
samostatná vlastnost `isLink` — nezávislá na typu.

### Přehled typů

| Typ (enum) | Řetězec v JSON | Popis | Výchozí zarovnání |
|---|---|---|---|
| `Text` | `"TEXT"` | Prostý string | Vlevo |
| `Number` | `"NUMBER"` | Celé nebo desetinné číslo | Vpravo |
| `Date` | `"DATE"` | Datum | Vlevo |
| `DateTime` | `"DATETIME"` | Datum + čas | Vlevo |
| `Bool` | `"BOOL"` | Logická hodnota — zobrazí se jako „Ano" / „Ne" | Střed |
| `Image` | `"IMAGE"` | Název resource z WPF resource dictionary | Střed |
| `Color` | `"COLOR"` | Barva ve formátu R;G;B — buňka se vybarví | Střed |
| `HoursMinutes` | `"HHMM"` | Čas v minutách jako int (830 = 08:30) | Vpravo |
| `Codebook` | `"CODEBOOK"` | Číselník — kód + label *(do budoucna)* | Vlevo |

### Mapování z legacy typů

Staré typy používaly typ pro vyjádření zarovnání nebo chování odkazu.
V novém designu jsou toto samostatné vlastnosti (`align`, `isLink`):

| Legacy | Nový typ | align | isLink |
|---|---|---|---|
| `T` | `TEXT` | *(auto)* | false |
| `Tc` | `TEXT` | `CENTER` | false |
| `TL` | `TEXT` | *(auto)* | **true** |
| `N`, `N2` | `NUMBER` | *(auto)* | false |
| `Nc` | `NUMBER` | `CENTER` | false |
| `NL` | `NUMBER` | *(auto)* | **true** |
| `D` | `DATE` | *(auto)* | false |
| `DT` | `DATETIME` | *(auto)* | false |
| `B`, `ano/ne` | `BOOL` | *(auto)* | false |
| `I` | `IMAGE` | *(auto)* | false |
| `CLR` | `COLOR` | *(auto)* | false |
| `HHMM` | `HHMM` | *(auto)* | false |
| `CBO` | `CODEBOOK` | *(auto)* | false |

### Wire formát hodnot z Caché

Caché vždy posílá **interní formát**; typ + `format` na C# straně řídí zobrazení.

| Typ | Caché pošle | C# zobrazí |
|---|---|---|
| `TEXT` | `"Praha"` | `"Praha"` |
| `NUMBER` | `"1234.5"` (vždy tečka) | `"1 234,50"` dle `format` (`"N0"`, `"N2"`...) |
| `DATE` | `"2024-03-15"` (ISO 8601) | `"15.03.2024"` dle `format` |
| `DATETIME` | `"2024-03-15T08:30:00"` | `"15.03.2024 08:30"` dle `format` |
| `BOOL` | `"1"` / `"0"` | `"Ano"` / `"Ne"` |
| `IMAGE` | `"ic_bus"` (název resource) | ikona z WPF resource dictionary |
| `COLOR` | `"255;128;0"` (R;G;B) | podbarvení buňky danou barvou |
| `HHMM` | `"830"` (int, 830 = 08:30) | `"08:30"` |
| `CODEBOOK` | `"CB"` (raw kód) | `"České Budějovice"` (label z options) |

### Vlastnost `format`

Používá se pouze u typů, kde má smysl:

| Typ | Příklad `format` | Popis |
|---|---|---|
| `NUMBER` | `"N0"`, `"N2"` | C# numeric format string |
| `DATE` | `"dd.MM.yyyy"` | C# date format string |
| `DATETIME` | `"dd.MM.yyyy HH:mm"` | C# date format string |
| ostatní | *(prázdný)* | Ignorováno |

### Vlastnost `isLink`

Pokud `isLink = true`, buňka se zobrazí jako odkaz (podtržený text).
Kliknutí vyvolá event `CellLinkClicked` s `rowId` a `columnKey`.

```csharp
// Event na EDList:
event EventHandler<(string RowId, string ColumnKey)>? CellLinkClicked
```

---

## Komunikace

Stejný transport jako `EDInputForm`: process-private globály `^||ML("IN",...)` /
`^||ML("OUT",...)`, volání přes `MLRest.Query`.

EDList používá **JSON** místo delimitovaných stringů — data řádků jsou dynamická,
JSON objekty jsou samo-popisné a nevyžadují poziční parsování. `%DynamicObject`
v Caché a `System.Text.Json` v C# obstarají serializaci bez vlastních `Serialize()` metod.

### InitializeList — formát OUT

`OUT[1]` = JSON pole objektů sloupců:

```json
[
  {"key":"nazev","label":"Název","type":"TEXT","width":150,"align":"","sortable":true,"visible":true,"isLink":false},
  {"key":"pocet","label":"Počet","type":"NUMBER","format":"N0","width":80,"align":"","sortable":true,"visible":true,"isLink":false},
  {"key":"aktivni","label":"Aktivní","type":"BOOL","width":60,"align":"","sortable":false,"visible":true,"isLink":false},
  {"key":"barva","label":"Barva","type":"COLOR","width":60,"align":"","sortable":false,"visible":true,"isLink":false}
]
```

### LoadList — formát IN / OUT

**Vstup (IN):** Zatím se nic neposílá. Do budoucna bude `IN` obsahovat pojmenované
klíče (CVar formát nebo JSON) pro filtrování — žádné poziční číselné indexy.
Caché metoda má pro budoucí filtr připraven komentář:

```objectscript
// Budoucí filtr — pojmenované klíče, např.:
// s filterText = $g(^||ML("IN","filter"))
// s datumOd    = $g(^||ML("IN","od"))
```

**Výstup (OUT):** jeden JSON objekt na řádek, vždy s klíčem `"id"`:

```
OUT[1] = {"id":"42","nazev":"Linka 1","pocet":"15","aktivni":"1","barva":"255;128;0"}
OUT[2] = {"id":"43","nazev":"Linka 2","pocet":"8","aktivni":"0","barva":"0;128;255"}
```

---

## Caché model

### Column.cls

```objectscript
Class ALVA.EDControls.EDList.Column Extends (%RegisteredObject, %JSON.Adaptor)
{
    Property key      As %String;
    Property label    As %String;
    Property type     As %String [ InitialExpression = "TEXT" ];
    // TEXT|NUMBER|DATE|DATETIME|BOOL|IMAGE|COLOR|HHMM|CODEBOOK

    Property format   As %String;
    // NUMBER: "N0","N2",...  DATE/DATETIME: "dd.MM.yyyy",...  ostatní: prázdné

    Property width    As %Integer [ InitialExpression = 0 ];  // 0 = auto
    Property align    As %String [ InitialExpression = "" ];  // ""|LEFT|CENTER|RIGHT
    Property sortable As %Boolean [ InitialExpression = 0 ];
    Property visible  As %Boolean [ InitialExpression = 1 ];
    Property isLink   As %Boolean [ InitialExpression = 0 ];
}
```

### List.cls

```objectscript
Class ALVA.EDControls.EDList.List Extends %RegisteredObject
{
    Property columns As list Of ALVA.EDControls.EDList.Column;
    Property rows    As %DynamicArray [ InitialExpression = {##class(%DynamicArray).%New()} ];

    /// Přidá sloupec a vrátí ho pro případné další nastavení.
    Method AddColumn(key As %String, label As %String, type As %String = "TEXT",
                     width As %Integer = 0, align As %String = "",
                     sortable As %Boolean = 0) As Column

    /// Vytvoří nový řádek s daným id, přidá ho do interní %DynamicArray a vrátí ho.
    /// Volající nastaví pole přímo: s row.nazev = "Linka 1"
    Method AddRow(id As %String) As %DynamicObject

    /// Zapíše OUT[1] = JSON pole definic sloupců.
    Method WriteColumnsToOut()

    /// Zapíše OUT[1..n] = jeden JSON objekt na řádek.
    Method WriteRowsToOut()
}
```

### Vzor BL třídy (app-specifická)

```objectscript
Class APP.VozidlaList Extends %RegisteredObject
{
    ClassMethod InitializeList(ByRef sys As %String) As %Status
    {
        $$$Init
        try {
            $$$FillParams
            s list = ##class(ALVA.EDControls.EDList.List).%New()
            d list.AddColumn("rz",      "RZ",       "TEXT",   100)
            d list.AddColumn("typ",     "Typ",      "TEXT",   150)
            d list.AddColumn("aktivni", "Aktivní",  "BOOL",    60)
            d list.AddColumn("odjezd",  "Odjezd",   "HHMM",    70)
            d list.WriteColumnsToOut()
        }
        catch (ex) { $$$CatchErrRest }
        ret ret
    }

    ClassMethod LoadList(ByRef sys As %String) As %Status
    {
        $$$Init
        try {
            $$$FillParams
            // Budoucí filtr:
            // s filterText = $g(^||ML("IN","filter"))

            s list = ##class(ALVA.EDControls.EDList.List).%New()
            // ... SQL / global procházení ...
            s row = list.AddRow(id)
            s row.rz      = rz
            s row.typ     = typ
            s row.aktivni = aktivni   // "1" nebo "0"
            s row.odjezd  = odjezd   // int, např. "830"
            d list.WriteRowsToOut()
        }
        catch (ex) { $$$CatchErrRest }
        ret ret
    }
}
```

---

## C# veřejné API

```csharp
// Konfigurace (nastavit před prvním voláním)
MLRest  MLRest     { get; set; }
string  CacheClass { get; set; }

// Volání
void InitializeList()   // naplní Columns, přegeneruje sloupce DataGridu
void LoadList()         // naplní Rows; budoucí přetížení přijme CVar/JSON filtr

// Stav
IReadOnlyList<ListColumn>?   Columns       { get; }
string?                      SelectedRowId { get; }

// Eventy
event EventHandler<string>?                        RowDoubleClicked   // arg = rowId
event EventHandler<string?>?                       SelectionChanged   // arg = rowId nebo null
event EventHandler<(string RowId, string ColKey)>? CellLinkClicked    // pro isLink sloupce

// Do budoucna: ukládání uživatelského nastavení sloupců
ColumnLayout[] GetCurrentLayout()
void           ApplyLayout(ColumnLayout[] layout)
```

---

## Nastavení WPF DataGridu

```csharp
dataGrid.CanUserReorderColumns = true;
dataGrid.CanUserResizeColumns  = true;
dataGrid.CanUserSortColumns    = true;
dataGrid.SelectionMode         = DataGridSelectionMode.Single;
dataGrid.IsReadOnly            = true;
```

Generování sloupců po `InitializeList`:
- Základní typy (`TEXT`, `NUMBER`, `DATE`, `DATETIME`, `BOOL`, `HHMM`) → `DataGridTextColumn`
- `COLOR` → `DataGridTemplateColumn` s barevným pozadím buňky
- `IMAGE` → `DataGridTemplateColumn` s `Image` elementem, resource lookup přes `Application.Current.Resources`
- `isLink = true` → `DataGridHyperlinkColumn` nebo `DataGridTemplateColumn` s `TextBlock` jako odkaz

Šířka: `width == 0` → `DataGridLengthUnitType.Auto`, `width > 0` → fixní pixely.

---

## Roadmapa

### MVP — ověření s mock daty

Cíl: funkční EDList zapojený v testovacím okně, data z Caché mock třídy.

**Caché:**
- [ ] `Column.cls` — model sloupce
- [ ] `List.cls` — builder (AddColumn, AddRow, WriteColumnsToOut, WriteRowsToOut)
- [ ] `MockList.cls` — testovací třída s hardcoded daty (InitializeList + LoadList)

**C#:**
- [ ] `ListColumn.cs`, `EListColumnType.cs`, `EListColumnAlign.cs` — datové modely
- [ ] `EDList.xaml / .xaml.cs` — UserControl
  - `InitializeList()` — volá Caché, parsuje JSON sloupce, generuje `DataGrid.Columns`
  - `LoadList()` — volá Caché, parsuje JSON řádky, naplní `ItemsSource`
  - `SelectedRowId` property
  - `RowDoubleClicked` a `SelectionChanged` eventy
- [ ] Testovací okno v EDISONMAIN2 — zapojí EDList s `MockList` třídou

### Po MVP

- [ ] Filtrování — `LoadList` přijme `CVar` nebo JSON s pojmenovanými klíči
- [ ] `ReloadData()` — refresh se zapamatovaným posledním filtrem
- [ ] `CellLinkClicked` event pro `isLink` sloupce
- [ ] `CODEBOOK` typ — options inline v definici sloupce
- [ ] `ColumnLayout` — ukládání pořadí a šířky sloupců per uživatel
- [ ] Design-time mock data v C# (analogie `DesignTimeData` z EDInputForm)

---

## Návrhová rozhodnutí

- Zarovnání a chování odkazu jsou vlastnosti sloupce (`align`, `isLink`), ne součást typu — legacy typy jako `Tc`, `NL` se mapují na `TEXT`/`NUMBER` + příslušné vlastnosti.
- `BOOL` vždy zobrazuje „Ano" / „Ne"; vstupní hodnota z Caché je `"1"` / `"0"`.
- `HHMM` přijímá celé číslo v HHMM formátu (830 = 08:30), C# zobrazuje `HH:mm`.
- `COLOR` přijímá `"R;G;B"` (255;128;0), C# vykreslí jako podbarvení buňky.
- `IMAGE` přijímá název resource; lookup jde přes `Application.Current.Resources` — nezávislé na konkrétním projektu.
- `rows` v `List.cls` je `%DynamicArray` (ne `list Of %DynamicObject`) — spolehlivé a JSON-nativní.
- Žádné vlastní `Serialize()` metody — `%JSON.Adaptor` a `%DynamicObject` obstarají vše.
- `%RegisteredObject` (ne `%Persistent`) — žádné ukládání do databáze.
- Filtr bude předáván pojmenovanými klíči, nikoli číselnými indexy.
- Kontextové menu je odpovědností hostu — EDList žádné nedefinuje.
